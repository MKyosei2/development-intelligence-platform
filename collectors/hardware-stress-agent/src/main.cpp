// Purpose: Applies controlled Direct3D compute load for hardware stress verification.
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

using Microsoft::WRL::ComPtr;
namespace fs = std::filesystem;

static std::wstring Arg(
    int argc,
    wchar_t** argv,
    const wchar_t* key)
{
    for (int index = 1; index + 1 < argc; ++index)
    {
        if (std::wstring(argv[index]) == key)
            return argv[index + 1];
    }

    return L"";
}

int wmain(
    int argc,
    wchar_t** argv)
{
    double load = 50.0;

    try
    {
        const auto value = Arg(
            argc,
            argv,
            L"--load-percent");

        if (!value.empty())
            load = std::stod(value);
    }
    catch (...)
    {
        // Invalid user input falls back to the default load.
    }

    load = std::clamp(
        load,
        1.0,
        95.0);

    const auto stopFile = Arg(
        argc,
        argv,
        L"--stop-file");

    if (stopFile.empty())
        return 2;

    DeleteFileW(stopFile.c_str());

    ComPtr<ID3D11Device> device;
    ComPtr<ID3D11DeviceContext> context;
    D3D_FEATURE_LEVEL featureLevel{};

    if (FAILED(D3D11CreateDevice(
            nullptr,
            D3D_DRIVER_TYPE_HARDWARE,
            nullptr,
            0,
            nullptr,
            0,
            D3D11_SDK_VERSION,
            &device,
            &featureLevel,
            &context)))
    {
        return 3;
    }

    const char* shaderSource = R"(
RWStructuredBuffer<float> data : register(u0);

[numthreads(256, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    float value = data[id.x];

    [loop]
    for (uint iteration = 0; iteration < 512; ++iteration)
    {
        value = mad(
            value,
            1.000001f,
            0.000001f);
    }

    data[id.x] = value;
}
)";

    ComPtr<ID3DBlob> shaderBlob;
    ComPtr<ID3DBlob> shaderErrors;

    if (FAILED(D3DCompile(
            shaderSource,
            std::strlen(shaderSource),
            nullptr,
            nullptr,
            nullptr,
            "main",
            "cs_5_0",
            D3DCOMPILE_OPTIMIZATION_LEVEL3,
            0,
            &shaderBlob,
            &shaderErrors)))
    {
        return 4;
    }

    ComPtr<ID3D11ComputeShader> shader;

    if (FAILED(device->CreateComputeShader(
            shaderBlob->GetBufferPointer(),
            shaderBlob->GetBufferSize(),
            nullptr,
            &shader)))
    {
        return 5;
    }

    constexpr UINT elementCount =
        256u * 2048u;

    std::vector<float> initialValues(
        elementCount,
        1.0f);

    D3D11_BUFFER_DESC bufferDescription{};
    bufferDescription.ByteWidth =
        elementCount * sizeof(float);
    bufferDescription.Usage =
        D3D11_USAGE_DEFAULT;
    bufferDescription.BindFlags =
        D3D11_BIND_UNORDERED_ACCESS;
    bufferDescription.MiscFlags =
        D3D11_RESOURCE_MISC_BUFFER_STRUCTURED;
    bufferDescription.StructureByteStride =
        sizeof(float);

    D3D11_SUBRESOURCE_DATA initialData{};
    initialData.pSysMem =
        initialValues.data();

    ComPtr<ID3D11Buffer> buffer;

    if (FAILED(device->CreateBuffer(
            &bufferDescription,
            &initialData,
            &buffer)))
    {
        return 6;
    }

    D3D11_UNORDERED_ACCESS_VIEW_DESC viewDescription{};
    viewDescription.ViewDimension =
        D3D11_UAV_DIMENSION_BUFFER;
    viewDescription.Format =
        DXGI_FORMAT_UNKNOWN;
    viewDescription.Buffer.NumElements =
        elementCount;

    ComPtr<ID3D11UnorderedAccessView> unorderedAccessView;

    if (FAILED(device->CreateUnorderedAccessView(
            buffer.Get(),
            &viewDescription,
            &unorderedAccessView)))
    {
        return 7;
    }

    ID3D11UnorderedAccessView* views[] =
    {
        unorderedAccessView.Get()
    };

    context->CSSetShader(
        shader.Get(),
        nullptr,
        0);

    context->CSSetUnorderedAccessViews(
        0,
        1,
        views,
        nullptr);

    const auto busyMilliseconds =
        static_cast<int>(load);

    const auto idleMilliseconds =
        100 - busyMilliseconds;

    while (!fs::exists(stopFile))
    {
        const auto start =
            std::chrono::steady_clock::now();

        const auto busyDuration =
            std::chrono::milliseconds(
                busyMilliseconds);

        while (
            std::chrono::steady_clock::now() - start
            < busyDuration)
        {
            context->Dispatch(
                2048,
                1,
                1);

            context->Flush();
        }

        std::this_thread::sleep_for(
            std::chrono::milliseconds(
                idleMilliseconds));
    }

    return 0;
}
