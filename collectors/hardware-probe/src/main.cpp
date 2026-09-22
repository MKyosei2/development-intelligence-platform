// Purpose: Defines a focused responsibility within the development intelligence platform.
#include <windows.h>
#include <d3d11.h>
#include <d3dcompiler.h>
#include <dxgi.h>
#include <wrl/client.h>
#include <chrono>
#include <cstring>
#include <iostream>
#include <string>
#include <vector>
using Microsoft::WRL::ComPtr;
static std::string JsonEscape(const std::wstring&w){std::string s;for(wchar_t c:w){if(c=='"'||c=='\\')s.push_back('\\');s.push_back(c<128?(char)c:'?');}return s;}
int wmain()
{
 ComPtr<ID3D11Device> device;ComPtr<ID3D11DeviceContext> ctx;D3D_FEATURE_LEVEL level{};HRESULT hr=D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&device,&level,&ctx);if(FAILED(hr)){std::cerr<<"{\"available\":false,\"gpuScore\":0}";return 2;}
 ComPtr<IDXGIDevice> dxgi;device.As(&dxgi);ComPtr<IDXGIAdapter> adapter;dxgi->GetAdapter(&adapter);DXGI_ADAPTER_DESC desc{};adapter->GetDesc(&desc);
 const char*hlsl=R"(RWStructuredBuffer<float> data:register(u0);[numthreads(256,1,1)]void main(uint3 id:SV_DispatchThreadID){uint i=id.x;float x=data[i];[loop]for(uint k=0;k<256;k++){x=mad(x,1.000001f,0.000001f);x=sin(x)+x;}data[i]=x;})";
 ComPtr<ID3DBlob> code,errors;hr=D3DCompile(hlsl,strlen(hlsl),nullptr,nullptr,nullptr,"main","cs_5_0",D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&code,&errors);if(FAILED(hr)){std::cerr<<"{\"available\":false,\"gpuScore\":0}";return 3;}ComPtr<ID3D11ComputeShader> shader;device->CreateComputeShader(code->GetBufferPointer(),code->GetBufferSize(),nullptr,&shader);
 const UINT count=256*4096;std::vector<float> init(count,0.5f);D3D11_BUFFER_DESC bd{};bd.ByteWidth=count*sizeof(float);bd.Usage=D3D11_USAGE_DEFAULT;bd.BindFlags=D3D11_BIND_UNORDERED_ACCESS|D3D11_BIND_SHADER_RESOURCE;bd.MiscFlags=D3D11_RESOURCE_MISC_BUFFER_STRUCTURED;bd.StructureByteStride=sizeof(float);D3D11_SUBRESOURCE_DATA sd{init.data(),0,0};ComPtr<ID3D11Buffer> buffer;device->CreateBuffer(&bd,&sd,&buffer);D3D11_UNORDERED_ACCESS_VIEW_DESC ud{};ud.Format=DXGI_FORMAT_UNKNOWN;ud.ViewDimension=D3D11_UAV_DIMENSION_BUFFER;ud.Buffer.NumElements=count;ComPtr<ID3D11UnorderedAccessView> uav;device->CreateUnorderedAccessView(buffer.Get(),&ud,&uav);
 D3D11_BUFFER_DESC stageDesc=bd;stageDesc.Usage=D3D11_USAGE_STAGING;stageDesc.BindFlags=0;stageDesc.MiscFlags=D3D11_RESOURCE_MISC_BUFFER_STRUCTURED;stageDesc.CPUAccessFlags=D3D11_CPU_ACCESS_READ;ComPtr<ID3D11Buffer> stage;device->CreateBuffer(&stageDesc,nullptr,&stage);
 ID3D11UnorderedAccessView* views[]={uav.Get()};ctx->CSSetShader(shader.Get(),nullptr,0);ctx->CSSetUnorderedAccessViews(0,1,views,nullptr);auto start=std::chrono::steady_clock::now();for(int pass=0;pass<6;pass++)ctx->Dispatch(4096,1,1);ctx->CopyResource(stage.Get(),buffer.Get());D3D11_MAPPED_SUBRESOURCE mapped{};ctx->Map(stage.Get(),0,D3D11_MAP_READ,0,&mapped);volatile float sink=*reinterpret_cast<float*>(mapped.pData);(void)sink;ctx->Unmap(stage.Get(),0);auto sec=std::chrono::duration<double>(std::chrono::steady_clock::now()-start).count();double ops=(double)count*256.0*6.0;double score=ops/sec/1e9;
 std::cout<<"{\"available\":true,\"adapter\":\""<<JsonEscape(desc.Description)<<"\",\"dedicatedVideoMemoryMb\":"<<(desc.DedicatedVideoMemory/1024/1024)<<",\"gpuScore\":"<<score<<"}";return 0;
}
