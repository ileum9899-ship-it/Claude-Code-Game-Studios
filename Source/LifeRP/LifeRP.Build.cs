// Life RP - runtime module rules.

using UnrealBuildTool;

public class LifeRP : ModuleRules
{
	public LifeRP(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

		PublicDependencyModuleNames.AddRange(new string[]
		{
			"Core",
			"CoreUObject",
			"Engine",
			"InputCore",
			"EnhancedInput",
			"AIModule",
			"DeveloperSettings"
		});
	}
}
