// Life RP - finds what the player is looking at.

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "LifeInteractionComponent.generated.h"

/**
 * Periodically scans a sphere around the owner and focuses the interactable
 * that best matches the camera direction.
 */
UCLASS(ClassGroup = (Life), meta = (BlueprintSpawnableComponent))
class LIFERP_API ULifeInteractionComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	ULifeInteractionComponent();

	virtual void TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction) override;

	/** Current focus (implements ULifeInteractable), or nullptr. */
	UFUNCTION(BlueprintPure, Category = "Life|Interaction") AActor* GetFocus() const { return Focus.Get(); }

	/** Clears focus immediately (e.g. when entering a vehicle). */
	void ClearFocus() { Focus.Reset(); }

	UPROPERTY(EditAnywhere, Category = "Life|Interaction") float Radius = 240.f;
	UPROPERTY(EditAnywhere, Category = "Life|Interaction") float ScanInterval = 0.1f;

private:
	void Scan();

	TWeakObjectPtr<AActor> Focus;
	float ScanTimer = 0.f;
};
