#include <windows.h>
#include <d3d11.h>
#include <d3dcompiler.h>
#include <wrl/client.h>
#include <algorithm>
#include <chrono>
#include <cstring>
#include <filesystem>
#include <string>
#include <thread>
#include <vector>
using Microsoft::WRL::ComPtr;namespace fs=std::filesystem;
static std::wstring Arg(int argc,wchar_t**argv,const wchar_t*k){for(int i=1;i+1<argc;i++)if(std::wstring(argv[i])==k)return argv[i+1];return L"";}
int wmain(int argc,wchar_t**argv)
{
 double load=50;try{auto v=Arg(argc,argv,L"--load-percent");if(!v.empty())load=std::stod(v);}catch{}load=std::clamp(load,1.0,95.0);auto stop=Arg(argc,argv,L"--stop-file");if(stop.empty())return 2;DeleteFileW(stop.c_str());
 ComPtr<ID3D11Device>d;ComPtr<ID3D11DeviceContext>c;D3D_FEATURE_LEVEL l{};if(FAILED(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&d,&l,&c)))return 3;
 const char*h=R"(RWStructuredBuffer<float> data:register(u0);[numthreads(256,1,1)]void main(uint3 id:SV_DispatchThreadID){float x=data[id.x];[loop]for(uint k=0;k<512;k++)x=mad(x,1.000001f,0.000001f);data[id.x]=x;})";ComPtr<ID3DBlob>b,e;if(FAILED(D3DCompile(h,strlen(h),nullptr,nullptr,nullptr,"main","cs_5_0",D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&b,&e)))return 4;ComPtr<ID3D11ComputeShader>s;d->CreateComputeShader(b->GetBufferPointer(),b->GetBufferSize(),nullptr,&s);const UINT count=256*2048;std::vector<float>init(count,1.f);D3D11_BUFFER_DESC bd{};bd.ByteWidth=count*4;bd.Usage=D3D11_USAGE_DEFAULT;bd.BindFlags=D3D11_BIND_UNORDERED_ACCESS;bd.MiscFlags=D3D11_RESOURCE_MISC_BUFFER_STRUCTURED;bd.StructureByteStride=4;D3D11_SUBRESOURCE_DATA sd{init.data(),0,0};ComPtr<ID3D11Buffer>buf;d->CreateBuffer(&bd,&sd,&buf);D3D11_UNORDERED_ACCESS_VIEW_DESC ud{};ud.ViewDimension=D3D11_UAV_DIMENSION_BUFFER;ud.Format=DXGI_FORMAT_UNKNOWN;ud.Buffer.NumElements=count;ComPtr<ID3D11UnorderedAccessView>u;d->CreateUnorderedAccessView(buf.Get(),&ud,&u);ID3D11UnorderedAccessView*uv[]={u.Get()};c->CSSetShader(s.Get(),nullptr,0);c->CSSetUnorderedAccessViews(0,1,uv,nullptr);
 while(!fs::exists(stop)){auto start=std::chrono::steady_clock::now();auto busy=std::chrono::milliseconds((int)load);while(std::chrono::steady_clock::now()-start<busy){c->Dispatch(2048,1,1);c->Flush();}std::this_thread::sleep_for(std::chrono::milliseconds(100-(int)load));}return 0;
}
