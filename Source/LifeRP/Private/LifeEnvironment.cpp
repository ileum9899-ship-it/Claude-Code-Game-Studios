// Life RP - sky, lighting and time of day.

#include "LifeEnvironment.h"
#include "LifeGameState.h"
#include "LifeRules.h"
#include "LifeSettings.h"
#include "Components/DirectionalLightComponent.h"
#include "Components/ExponentialHeightFogComponent.h"
#include "Components/PostProcessComponent.h"
#include "Components/SkyAtmosphereComponent.h"
#include "Components/SkyLightComponent.h"
#include "Components/VolumetricCloudComponent.h"
#include "Engine/World.h"
#include "Materials/MaterialInterface.h"
#include "LifeVisuals.h"

ALifeEnvironment::ALifeEnvironment()
{
	PrimaryActorTick.bCanEverTick = true;
	PrimaryActorTick.TickInterval = 0.05f;

	Root = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
	Root->SetMobility(EComponentMobility::Movable);
	SetRootComponent(Root);

	Sun = CreateDefaultSubobject<UDirectionalLightComponent>(TEXT("Sun"));
	Sun->SetupAttachment(Root);
	Sun->SetMobility(EComponentMobility::Movable);
	Sun->SetAtmosphereSunLight(true);
	Sun->SetAtmosphereSunLightIndex(0);
	Sun->SetIntensity(SunLux);
	Sun->SetLightColor(FLinearColor(1.f, 0.96f, 0.9f));
	Sun->bUseTemperature = true;
	Sun->Temperature = 5900.f;
	Sun->SetDynamicShadowDistanceMovableLight(20000.f);
	Sun->LightSourceAngle = 0.53f;
	Sun->bCastCloudShadows = true;

	Moon = CreateDefaultSubobject<UDirectionalLightComponent>(TEXT("Moon"));
	Moon->SetupAttachment(Root);
	Moon->SetMobility(EComponentMobility::Movable);
	Moon->SetAtmosphereSunLight(true);
	Moon->SetAtmosphereSunLightIndex(1);
	Moon->SetIntensity(MoonLux);
	Moon->SetLightColor(FLinearColor(0.62f, 0.72f, 1.f));
	Moon->SetCastShadows(true);

	Atmosphere = CreateDefaultSubobject<USkyAtmosphereComponent>(TEXT("SkyAtmosphere"));
	Atmosphere->SetupAttachment(Root);

	SkyLight = CreateDefaultSubobject<USkyLightComponent>(TEXT("SkyLight"));
	SkyLight->SetupAttachment(Root);
	SkyLight->SetMobility(EComponentMobility::Movable);
	SkyLight->bRealTimeCapture = true;
	SkyLight->SetIntensity(1.f);
	SkyLight->bLowerHemisphereIsBlack = false;
	SkyLight->SetLowerHemisphereColor(FLinearColor(0.12f, 0.11f, 0.1f));

	Fog = CreateDefaultSubobject<UExponentialHeightFogComponent>(TEXT("HeightFog"));
	Fog->SetupAttachment(Root);
	Fog->SetFogDensity(0.012f);
	Fog->SetFogHeightFalloff(0.2f);
	Fog->SetVolumetricFog(true);
	Fog->SetVolumetricFogScatteringDistribution(0.6f);
	Fog->SetVolumetricFogExtinctionScale(0.6f);

	Clouds = CreateDefaultSubobject<UVolumetricCloudComponent>(TEXT("VolumetricClouds"));
	Clouds->SetupAttachment(Root);

	PostProcess = CreateDefaultSubobject<UPostProcessComponent>(TEXT("PostProcess"));
	PostProcess->SetupAttachment(Root);
	PostProcess->bUnbound = true;
	FPostProcessSettings& PP = PostProcess->Settings;
	PP.bOverride_BloomIntensity = true;           PP.BloomIntensity = 0.6f;
	PP.bOverride_VignetteIntensity = true;        PP.VignetteIntensity = 0.35f;
	PP.bOverride_AutoExposureMinBrightness = true; PP.AutoExposureMinBrightness = -2.5f;
	PP.bOverride_AutoExposureMaxBrightness = true; PP.AutoExposureMaxBrightness = 12.f;
	PP.bOverride_AutoExposureSpeedUp = true;      PP.AutoExposureSpeedUp = 2.f;
	PP.bOverride_AutoExposureSpeedDown = true;    PP.AutoExposureSpeedDown = 1.f;
	PP.bOverride_ColorSaturation = true;          PP.ColorSaturation = FVector4(1.08f, 1.08f, 1.08f, 1.f);
	PP.bOverride_ColorContrast = true;            PP.ColorContrast = FVector4(1.05f, 1.05f, 1.05f, 1.f);
	PP.bOverride_FilmGrainIntensity = true;       PP.FilmGrainIntensity = 0.08f;
	PP.bOverride_SceneFringeIntensity = true;     PP.SceneFringeIntensity = 0.3f;
	PP.bOverride_AmbientOcclusionIntensity = true; PP.AmbientOcclusionIntensity = 0.6f;
}

void ALifeEnvironment::OnConstruction(const FTransform& Transform)
{
	Super::OnConstruction(Transform);
	ApplyTimeOfDay(PreviewHour);
}

void ALifeEnvironment::BeginPlay()
{
	Super::BeginPlay();
	// Engine-provided cloud material; the component keeps its default if this ever moves.
	if (UMaterialInterface* CloudMat = LoadObject<UMaterialInterface>(nullptr,
		TEXT("/Engine/EngineSky/VolumetricClouds/m_SimpleVolumetricCloud_Inst.m_SimpleVolumetricCloud_Inst")))
	{
		Clouds->SetMaterial(CloudMat);
	}
	if (LifeVisuals::IsMobileRenderer(this))
	{
		// Mobile renderer: no volumetric clouds / volumetric fog, lighter post-process.
		Clouds->SetVisibility(false);
		Fog->SetVolumetricFog(false);
		FPostProcessSettings& PP = PostProcess->Settings;
		PP.bOverride_FilmGrainIntensity = true;     PP.FilmGrainIntensity = 0.f;
		PP.bOverride_SceneFringeIntensity = true;   PP.SceneFringeIntensity = 0.f;
		PP.bOverride_BloomIntensity = true;         PP.BloomIntensity = 0.4f;
		Sun->SetDynamicShadowDistanceMovableLight(8000.f);
		Moon->SetCastShadows(false);
	}
	ApplyTimeOfDay(ULifeSettings::Get()->StartHour);
}

void ALifeEnvironment::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (const ALifeGameState* GS = GetWorld()->GetGameState<ALifeGameState>())
	{
		ApplyTimeOfDay(GS->GetHourOfDay());
	}
}

void ALifeEnvironment::ApplyTimeOfDay(float HourOfDay)
{
	const float MaxElevation = ULifeSettings::Get()->MaxSunElevation;
	const float SunElevation = LifeRules::SunElevationDegrees(HourOfDay, MaxElevation);
	const float SunAzimuth = LifeRules::SunAzimuthDegrees(HourOfDay);

	// A directional light points along its forward vector, so pitch is the negative elevation.
	Sun->SetWorldRotation(FRotator(-SunElevation, SunAzimuth, 0.f));
	Moon->SetWorldRotation(FRotator(SunElevation * 0.85f - 8.f, SunAzimuth + 180.f, 0.f));

	// Fade the sun out just after it sets and the moon in as it gets dark.
	const float SunVisible = FMath::Clamp((SunElevation + 4.f) / 8.f, 0.f, 1.f);
	const float MoonVisible = FMath::Clamp((-SunElevation - 2.f) / 10.f, 0.f, 1.f);
	Sun->SetIntensity(SunLux * SunVisible);
	Sun->SetVisibility(SunVisible > 0.001f);
	Moon->SetIntensity(MoonLux * MoonVisible);
	Moon->SetVisibility(MoonVisible > 0.001f);

	// Warmer, lower light at golden hour.
	const float Golden = 1.f - FMath::Clamp(SunElevation / 25.f, 0.f, 1.f);
	Sun->Temperature = FMath::Lerp(6200.f, 3600.f, Golden * SunVisible);
	Sun->MarkRenderStateDirty();

	// Keep the night readable without flattening it.
	SkyLight->SetIntensity(FMath::Lerp(1.6f, 1.f, SunVisible));
}
