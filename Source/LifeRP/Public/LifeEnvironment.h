// Life RP - sky, lighting and time of day.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "LifeEnvironment.generated.h"

class UDirectionalLightComponent;
class USkyAtmosphereComponent;
class USkyLightComponent;
class UExponentialHeightFogComponent;
class UVolumetricCloudComponent;
class UPostProcessComponent;

/**
 * Physically based outdoor lighting driven by the game clock: Sky Atmosphere,
 * real-time captured Sky Light (Lumen), volumetric clouds, volumetric height fog,
 * a sun and a moon directional light, and an unbound post-process grade.
 */
UCLASS()
class LIFERP_API ALifeEnvironment : public AActor
{
	GENERATED_BODY()

public:
	ALifeEnvironment();

	virtual void Tick(float DeltaSeconds) override;

	/** Applies the lighting for a given hour (also used by the editor preview). */
	UFUNCTION(BlueprintCallable, Category = "Life|Environment") void ApplyTimeOfDay(float HourOfDay);

protected:
	virtual void BeginPlay() override;
	virtual void OnConstruction(const FTransform& Transform) override;

	UPROPERTY(VisibleAnywhere, Category = "Life|Environment") TObjectPtr<USceneComponent> Root;
	UPROPERTY(VisibleAnywhere, Category = "Life|Environment") TObjectPtr<UDirectionalLightComponent> Sun;
	UPROPERTY(VisibleAnywhere, Category = "Life|Environment") TObjectPtr<UDirectionalLightComponent> Moon;
	UPROPERTY(VisibleAnywhere, Category = "Life|Environment") TObjectPtr<USkyAtmosphereComponent> Atmosphere;
	UPROPERTY(VisibleAnywhere, Category = "Life|Environment") TObjectPtr<USkyLightComponent> SkyLight;
	UPROPERTY(VisibleAnywhere, Category = "Life|Environment") TObjectPtr<UExponentialHeightFogComponent> Fog;
	UPROPERTY(VisibleAnywhere, Category = "Life|Environment") TObjectPtr<UVolumetricCloudComponent> Clouds;
	UPROPERTY(VisibleAnywhere, Category = "Life|Environment") TObjectPtr<UPostProcessComponent> PostProcess;

	/** Hour shown in the editor viewport before play. */
	UPROPERTY(EditAnywhere, Category = "Life|Environment", meta = (ClampMin = "0", ClampMax = "24")) float PreviewHour = 16.5f;

	UPROPERTY(EditAnywhere, Category = "Life|Environment") float SunLux = 10.f;
	UPROPERTY(EditAnywhere, Category = "Life|Environment") float MoonLux = 0.35f;
};
