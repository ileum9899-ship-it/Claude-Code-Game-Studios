// Life RP - editor target.

using UnrealBuildTool;

public class LifeRPEditorTarget : TargetRules
{
	public LifeRPEditorTarget(TargetInfo Target) : base(Target)
	{
		Type = TargetType.Editor;
		DefaultBuildSettings = BuildSettingsVersion.Latest;
		IncludeOrderVersion = EngineIncludeOrderVersion.Latest;
		ExtraModuleNames.Add("LifeRP");
	}
}
