// Purpose: Defines a focused responsibility within the development intelligence platform.
using UnrealBuildTool;

public class DIPCollector : ModuleRules
{
    public DIPCollector(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;
        PrivateDependencyModuleNames.AddRange(new[] {
            "Core", "CoreUObject", "Engine", "UnrealEd", "AssetRegistry", "Projects", "Json", "JsonUtilities"
        });
    }
}
