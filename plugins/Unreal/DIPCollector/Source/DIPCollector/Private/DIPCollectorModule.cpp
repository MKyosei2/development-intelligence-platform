#include "DIPCollectorModule.h"
#include "AssetRegistry/AssetRegistryModule.h"
#include "Dom/JsonObject.h"
#include "HAL/PlatformProcess.h"
#include "HAL/PlatformMisc.h"
#include "HAL/IConsoleManager.h"
#include "Misc/EngineVersion.h"
#include "Misc/App.h"
#include "Misc/DateTime.h"
#include "Misc/Guid.h"
#include "Misc/Paths.h"
#include "Serialization/JsonSerializer.h"
#include "Serialization/JsonWriter.h"

#if PLATFORM_WINDOWS
#include "Windows/AllowWindowsPlatformTypes.h"
#include <Windows.h>
#include "Windows/HideWindowsPlatformTypes.h"
#endif

IMPLEMENT_MODULE(FDIPCollectorModule, DIPCollector)

static FString DIPPipeName()
{
    FString Value = FPlatformMisc::GetEnvironmentVariable(TEXT("DIP_AGENT_PIPE_NAME"));
    return Value;
}

static FString DIPBridgeToken()
{
    return FPlatformMisc::GetEnvironmentVariable(TEXT("DIP_AGENT_BRIDGE_TOKEN"));
}

static bool DIPWriteNamedPipe(const FString& Json)
{
#if PLATFORM_WINDOWS
    const FString Pipe = FString(TEXT("\\\\.\\pipe\\")) + DIPPipeName();
    if (DIPPipeName().IsEmpty()) return false;
    HANDLE Handle = CreateFileW(*Pipe, GENERIC_WRITE, 0, nullptr, OPEN_EXISTING, 0, nullptr);
    if (Handle == INVALID_HANDLE_VALUE) return false;
    FTCHARToUTF8 Utf8(*(Json + TEXT("\n")));
    DWORD Written = 0;
    const BOOL Ok = WriteFile(Handle, Utf8.Get(), Utf8.Length(), &Written, nullptr);
    CloseHandle(Handle);
    return Ok && Written == static_cast<DWORD>(Utf8.Length());
#else
    return false;
#endif
}

void FDIPCollectorModule::StartupModule()
{
    FAssetRegistryModule& Registry = FModuleManager::LoadModuleChecked<FAssetRegistryModule>(TEXT("AssetRegistry"));
    AssetAddedHandle = Registry.Get().OnAssetAdded().AddRaw(this, &FDIPCollectorModule::OnAssetAdded);
    AssetUpdatedHandle = Registry.Get().OnAssetUpdated().AddRaw(this, &FDIPCollectorModule::OnAssetUpdated);

    auto Register = [this](const TCHAR* Name, const TCHAR* Help, int32 Kind)
    {
        Commands.Add(MakeUnique<FAutoConsoleCommand>(Name, Help,
            FConsoleCommandDelegate::CreateLambda([this, Kind]()
            {
                EmitEvent(Kind, TEXT(""), MakeShared<FJsonObject>());
            })));
    };
    Register(TEXT("DIP.EmitBuild"), TEXT("Emit a completed Unreal build event."), 21);
    Register(TEXT("DIP.EmitCook"), TEXT("Emit a completed Unreal cook event."), 30);
    Register(TEXT("DIP.EmitPackage"), TEXT("Emit a completed Unreal package event."), 31);
    Register(TEXT("DIP.EmitAutomationTests"), TEXT("Emit completed Unreal automation-test evidence."), 32);

    Commands.Add(MakeUnique<FAutoConsoleCommand>(TEXT("DIP.RegisterVideo"), TEXT("DIP.RegisterVideo <project-relative.mp4>"),
        FConsoleCommandWithArgsDelegate::CreateLambda([this](const TArray<FString>& Args)
        {
            if (Args.Num() > 0) EmitEvent(41, TEXT(""), MakeShared<FJsonObject>(), 1, Args[0], TEXT("video/mp4"));
        })));
}

void FDIPCollectorModule::ShutdownModule()
{
    if (FModuleManager::Get().IsModuleLoaded(TEXT("AssetRegistry")))
    {
        FAssetRegistryModule& Registry = FModuleManager::GetModuleChecked<FAssetRegistryModule>(TEXT("AssetRegistry"));
        Registry.Get().OnAssetAdded().Remove(AssetAddedHandle);
        Registry.Get().OnAssetUpdated().Remove(AssetUpdatedHandle);
    }
    Commands.Empty();
}

static TSharedPtr<FJsonObject> DIPAssetMetadata(const FAssetData& AssetData)
{
    TSharedPtr<FJsonObject> Metadata = MakeShared<FJsonObject>();
    const FString AssetClass = AssetData.AssetClassPath.ToString();
    Metadata->SetStringField(TEXT("assetClass"), AssetClass);
    Metadata->SetStringField(TEXT("packageName"), AssetData.PackageName.ToString());
    Metadata->SetBoolField(TEXT("isRedirector"), AssetClass.Contains(TEXT("ObjectRedirector")));
    Metadata->SetNumberField(TEXT("missingDependencyCount"), 0);
    return Metadata;
}

void FDIPCollectorModule::OnAssetAdded(const FAssetData& AssetData)
{
    EmitEvent(22, AssetData.GetObjectPathString(), DIPAssetMetadata(AssetData));
}

void FDIPCollectorModule::OnAssetUpdated(const FAssetData& AssetData)
{
    EmitEvent(22, AssetData.GetObjectPathString(), DIPAssetMetadata(AssetData));
}

void FDIPCollectorModule::EmitEvent(int32 Kind, const FString& RelativePath, const TSharedPtr<FJsonObject>& Metadata,
    TOptional<int32> MediaKind, const FString& MediaRelativePath, const FString& MediaContentType)
{
    TSharedPtr<FJsonObject> Event = MakeShared<FJsonObject>();
    Event->SetStringField(TEXT("eventId"), FString(TEXT("unreal:")) + FGuid::NewGuid().ToString(EGuidFormats::Digits));
    Event->SetNumberField(TEXT("source"), 3);
    Event->SetNumberField(TEXT("kind"), Kind);
    Event->SetStringField(TEXT("toolVersion"), FEngineVersion::Current().ToString());
    if (!RelativePath.IsEmpty())
    {
        Event->SetStringField(TEXT("relativePath"), RelativePath);
        Event->SetStringField(TEXT("artifactRelativePath"), RelativePath);
    }
    Event->SetStringField(TEXT("observedAtUtc"), FDateTime::UtcNow().ToIso8601());
    Event->SetObjectField(TEXT("metadata"), Metadata);
    if (MediaKind.IsSet()) Event->SetNumberField(TEXT("mediaKind"), MediaKind.GetValue());
    if (!MediaRelativePath.IsEmpty()) Event->SetStringField(TEXT("mediaRelativePath"), MediaRelativePath);
    if (!MediaContentType.IsEmpty()) Event->SetStringField(TEXT("mediaContentType"), MediaContentType);

    TSharedPtr<FJsonObject> Root = MakeShared<FJsonObject>();
    Root->SetStringField(TEXT("token"), DIPBridgeToken());
    Root->SetObjectField(TEXT("event"), Event);
    FString Json;
    const TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&Json);
    FJsonSerializer::Serialize(Root.ToSharedRef(), Writer);
    DIPWriteNamedPipe(Json);
}
