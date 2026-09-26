// Purpose: Captures desktop video and system audio into evidence artifacts for game QA.
#include <windows.h>
#include <d3d11.h>
#include <dxgi1_2.h>
#include <mfapi.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <mmdeviceapi.h>
#include <audioclient.h>
#include <wrl/client.h>
#include <atomic>
#include <chrono>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <string>
#include <thread>
#include <vector>
using Microsoft::WRL::ComPtr;
namespace fs=std::filesystem;
static std::atomic_bool g_stop=false;
static bool Failed(HRESULT hr,const char* where){if(SUCCEEDED(hr))return false;std::cerr<<where<<" failed: 0x"<<std::hex<<hr<<std::dec<<"\n";g_stop=true;return true;}
static std::wstring Arg(int argc,wchar_t**argv,const std::wstring&key){for(int i=1;i+1<argc;i++)if(argv[i]==key)return argv[i+1];return L"";}
static int ArgInt(int argc,wchar_t**argv,const std::wstring&key,int fallback){auto v=Arg(argc,argv,key);try{return v.empty()?fallback:std::stoi(v);}catch(...){return fallback;}}
static void WriteU32(std::fstream& f,std::streampos pos,uint32_t v){auto cur=f.tellp();f.seekp(pos);f.write(reinterpret_cast<const char*>(&v),4);f.seekp(cur);}

class WavLoopback
{
public:
 bool Start(const fs::path& path)
 {
  path_=path;HRESULT hr=CoInitializeEx(nullptr,COINIT_MULTITHREADED);com_=SUCCEEDED(hr)||hr==RPC_E_CHANGED_MODE;
  ComPtr<IMMDeviceEnumerator> e;if(Failed(CoCreateInstance(__uuidof(MMDeviceEnumerator),nullptr,CLSCTX_ALL,IID_PPV_ARGS(&e)),"MMDeviceEnumerator"))return false;
  ComPtr<IMMDevice> d;if(Failed(e->GetDefaultAudioEndpoint(eRender,eConsole,&d),"GetDefaultAudioEndpoint"))return false;
  if(Failed(d->Activate(__uuidof(IAudioClient),CLSCTX_ALL,nullptr,reinterpret_cast<void**>(client_.GetAddressOf())),"IAudioClient"))return false;
  if(Failed(client_->GetMixFormat(&format_),"GetMixFormat"))return false;
  REFERENCE_TIME dur=10000000;if(Failed(client_->Initialize(AUDCLNT_SHAREMODE_SHARED,AUDCLNT_STREAMFLAGS_LOOPBACK,dur,0,format_,nullptr),"Audio Initialize"))return false;
  if(Failed(client_->GetService(__uuidof(IAudioCaptureClient),reinterpret_cast<void**>(capture_.GetAddressOf())),"IAudioCaptureClient"))return false;
  file_.open(path,std::ios::binary|std::ios::out|std::ios::trunc);if(!file_)return false;WriteHeader();
  if(Failed(client_->Start(),"Audio Start"))return false;thread_=std::thread([this]{Run();});return true;
 }
 void Stop(){if(thread_.joinable())thread_.join();if(client_)client_->Stop();Finalize();if(format_){CoTaskMemFree(format_);format_=nullptr;}if(com_)CoUninitialize();}
private:
 void WriteHeader(){file_.write("RIFF",4);uint32_t z=0;riffSizePos_=file_.tellp();file_.write((char*)&z,4);file_.write("WAVEfmt ",8);uint32_t fmtSize=sizeof(WAVEFORMATEX)+format_->cbSize;file_.write((char*)&fmtSize,4);file_.write((char*)format_,fmtSize);file_.write("data",4);dataSizePos_=file_.tellp();file_.write((char*)&z,4);}
 void Run(){CoInitializeEx(nullptr,COINIT_MULTITHREADED);while(!g_stop.load()){UINT32 packets=0;if(FAILED(capture_->GetNextPacketSize(&packets))){g_stop=true;break;}if(packets==0){Sleep(5);continue;}while(packets){BYTE* data=nullptr;UINT32 frames=0;DWORD flags=0;if(FAILED(capture_->GetBuffer(&data,&frames,&flags,nullptr,nullptr))){g_stop=true;break;}auto bytes=frames*format_->nBlockAlign;if(flags&AUDCLNT_BUFFERFLAGS_SILENT){std::vector<BYTE> zero(bytes);file_.write((char*)zero.data(),bytes);}else file_.write((char*)data,bytes);dataBytes_+=bytes;capture_->ReleaseBuffer(frames);capture_->GetNextPacketSize(&packets);}}CoUninitialize();}
 void Finalize(){if(!file_.is_open())return;auto end=file_.tellp();WriteU32(file_,dataSizePos_,dataBytes_);WriteU32(file_,riffSizePos_,static_cast<uint32_t>(end)-8);file_.close();}
 fs::path path_;bool com_=false;WAVEFORMATEX* format_=nullptr;ComPtr<IAudioClient> client_;ComPtr<IAudioCaptureClient> capture_;std::thread thread_;std::fstream file_;std::streampos riffSizePos_{},dataSizePos_{};uint32_t dataBytes_=0;
};

class DesktopVideo
{
public:
 bool Start(const fs::path& output)
 {
  if(Failed(MFStartup(MF_VERSION),"MFStartup"))return false;mf_=true;
  UINT flags=D3D11_CREATE_DEVICE_BGRA_SUPPORT;D3D_FEATURE_LEVEL level{};if(Failed(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,flags,nullptr,0,D3D11_SDK_VERSION,&device_,&level,&context_),"D3D11CreateDevice"))return false;
  ComPtr<IDXGIDevice> dxgi;if(Failed(device_.As(&dxgi),"IDXGIDevice"))return false;ComPtr<IDXGIAdapter> adapter;if(Failed(dxgi->GetAdapter(&adapter),"GetAdapter"))return false;ComPtr<IDXGIOutput> output0;if(Failed(adapter->EnumOutputs(0,&output0),"EnumOutputs"))return false;ComPtr<IDXGIOutput1> output1;if(Failed(output0.As(&output1),"IDXGIOutput1"))return false;DXGI_OUTPUT_DESC desc{};output0->GetDesc(&desc);width_=desc.DesktopCoordinates.right-desc.DesktopCoordinates.left;height_=desc.DesktopCoordinates.bottom-desc.DesktopCoordinates.top;
  if(Failed(output1->DuplicateOutput(device_.Get(),&duplication_),"DuplicateOutput"))return false;
  D3D11_TEXTURE2D_DESC td{};td.Width=width_;td.Height=height_;td.MipLevels=1;td.ArraySize=1;td.Format=DXGI_FORMAT_B8G8R8A8_UNORM;td.SampleDesc.Count=1;td.Usage=D3D11_USAGE_STAGING;td.CPUAccessFlags=D3D11_CPU_ACCESS_READ;if(Failed(device_->CreateTexture2D(&td,nullptr,&staging_),"Create staging texture"))return false;
  if(Failed(MFCreateSinkWriterFromURL(output.c_str(),nullptr,nullptr,&writer_),"MFCreateSinkWriterFromURL"))return false;
  ComPtr<IMFMediaType> outType;MFCreateMediaType(&outType);outType->SetGUID(MF_MT_MAJOR_TYPE,MFMediaType_Video);outType->SetGUID(MF_MT_SUBTYPE,MFVideoFormat_H264);outType->SetUINT32(MF_MT_AVG_BITRATE,8'000'000);outType->SetUINT32(MF_MT_INTERLACE_MODE,MFVideoInterlace_Progressive);MFSetAttributeSize(outType.Get(),MF_MT_FRAME_SIZE,width_,height_);MFSetAttributeRatio(outType.Get(),MF_MT_FRAME_RATE,30,1);MFSetAttributeRatio(outType.Get(),MF_MT_PIXEL_ASPECT_RATIO,1,1);if(Failed(writer_->AddStream(outType.Get(),&streamIndex_),"AddStream"))return false;
  ComPtr<IMFMediaType> inType;MFCreateMediaType(&inType);inType->SetGUID(MF_MT_MAJOR_TYPE,MFMediaType_Video);inType->SetGUID(MF_MT_SUBTYPE,MFVideoFormat_ARGB32);inType->SetUINT32(MF_MT_INTERLACE_MODE,MFVideoInterlace_Progressive);MFSetAttributeSize(inType.Get(),MF_MT_FRAME_SIZE,width_,height_);MFSetAttributeRatio(inType.Get(),MF_MT_FRAME_RATE,30,1);MFSetAttributeRatio(inType.Get(),MF_MT_PIXEL_ASPECT_RATIO,1,1);if(Failed(writer_->SetInputMediaType(streamIndex_,inType.Get(),nullptr),"SetInputMediaType"))return false;if(Failed(writer_->BeginWriting(),"BeginWriting"))return false;
  thread_=std::thread([this]{Run();});return true;
 }
 void Stop(){if(thread_.joinable())thread_.join();if(writer_)writer_->Finalize();writer_.Reset();duplication_.Reset();staging_.Reset();context_.Reset();device_.Reset();if(mf_)MFShutdown();}
private:
 void Run(){CoInitializeEx(nullptr,COINIT_MULTITHREADED);const LONGLONG frameDuration=10'000'000/30;LONGLONG timestamp=0;auto next=std::chrono::steady_clock::now();while(!g_stop.load()){next+=std::chrono::milliseconds(33);DXGI_OUTDUPL_FRAME_INFO info{};ComPtr<IDXGIResource> resource;auto hr=duplication_->AcquireNextFrame(100,&info,&resource);if(hr==DXGI_ERROR_WAIT_TIMEOUT){std::this_thread::sleep_until(next);continue;}if(FAILED(hr)){std::cerr<<"AcquireNextFrame failed\n";break;}ComPtr<ID3D11Texture2D> frame;resource.As(&frame);context_->CopyResource(staging_.Get(),frame.Get());D3D11_MAPPED_SUBRESOURCE mapped{};if(SUCCEEDED(context_->Map(staging_.Get(),0,D3D11_MAP_READ,0,&mapped))){ComPtr<IMFMediaBuffer> buffer;DWORD bytes=width_*height_*4;if(SUCCEEDED(MFCreateMemoryBuffer(bytes,&buffer))){BYTE* dst=nullptr;DWORD max=0;buffer->Lock(&dst,&max,nullptr);for(UINT y=0;y<height_;++y)memcpy(dst+y*width_*4,reinterpret_cast<BYTE*>(mapped.pData)+y*mapped.RowPitch,width_*4);buffer->Unlock();buffer->SetCurrentLength(bytes);ComPtr<IMFSample> sample;MFCreateSample(&sample);sample->AddBuffer(buffer.Get());sample->SetSampleTime(timestamp);sample->SetSampleDuration(frameDuration);writer_->WriteSample(streamIndex_,sample.Get());timestamp+=frameDuration;}context_->Unmap(staging_.Get(),0);}duplication_->ReleaseFrame();std::this_thread::sleep_until(next);}CoUninitialize();}
 bool mf_=false;UINT width_=0,height_=0;DWORD streamIndex_=0;ComPtr<ID3D11Device> device_;ComPtr<ID3D11DeviceContext> context_;ComPtr<IDXGIOutputDuplication> duplication_;ComPtr<ID3D11Texture2D> staging_;ComPtr<IMFSinkWriter> writer_;std::thread thread_;
};

int wmain(int argc,wchar_t**argv)
{
 CoInitializeEx(nullptr,COINIT_MULTITHREADED);
 auto video=Arg(argc,argv,L"--video");auto audio=Arg(argc,argv,L"--audio");auto stop=Arg(argc,argv,L"--stop-file");int maxSeconds=ArgInt(argc,argv,L"--max-seconds",300);if(video.empty()||stop.empty()){std::wcerr<<L"usage: dip-capture-agent --video out.mp4 [--audio system.wav] --stop-file path --max-seconds N\n";return 2;}
 fs::create_directories(fs::path(video).parent_path());DeleteFileW(stop.c_str());DesktopVideo v;WavLoopback a;if(!v.Start(video))return 3;if(!audio.empty()){fs::create_directories(fs::path(audio).parent_path());a.Start(audio);}auto deadline=std::chrono::steady_clock::now()+std::chrono::seconds(maxSeconds);
 while(std::chrono::steady_clock::now()<deadline&&!fs::exists(stop)&&!g_stop.load())std::this_thread::sleep_for(std::chrono::milliseconds(100));g_stop=true;v.Stop();if(!audio.empty())a.Stop();CoUninitialize();return 0;
}
