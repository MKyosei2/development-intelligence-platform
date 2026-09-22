// Purpose: Defines a focused responsibility within the development intelligence platform.
#pragma once
#include "Modules/ModuleManager.h"

class FDIPCollectorModule final : public IModuleInterface
{
public:
    virtual void StartupModule() override;
    virtual void ShutdownModule() override;
private:
    void EmitEvent(int32 Kind, const FString& RelativePath, const TSharedPtr<class FJsonObject>& Metadata,
        TOptional<int32> MediaKind = {}, const FString& MediaRelativePath = TEXT(""), const FString& MediaContentType = TEXT(""));
    void OnAssetAdded(const struct FAssetData& AssetData);
    void OnAssetUpdated(const struct FAssetData& AssetData);
    FDelegateHandle AssetAddedHandle;
    FDelegateHandle AssetUpdatedHandle;
    TArray<TUniquePtr<class FAutoConsoleCommand>> Commands;
};
