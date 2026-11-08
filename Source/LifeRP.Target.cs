// Life RP - game target.

using UnrealBuildTool;

public class LifeRPTarget : TargetRules
{
	public LifeRPTarget(TargetInfo Target) : base(Target)
	{
		Type = TargetType.Game;
		DefaultBuildSettings = BuildSettingsVersion.Latest;
		IncludeOrderVersion = EngineIncludeOrderVersion.Latest;
		// UE 5.8: C++20 is the minimum. Do not set CppStandard to Cpp17/Cpp14.
		ExtraModuleNames.Add("LifeRP");
	}
}
