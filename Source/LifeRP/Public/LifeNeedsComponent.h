// Life RP - needs simulation.

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "LifeTypes.h"
#include "LifeNeedsComponent.generated.h"

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FLifeNeedEvent, ELifeNeed, Need);

/** Decays the five needs over game time and reports when one runs low or empty. */
UCLASS(ClassGroup = (Life), meta = (BlueprintSpawnableComponent))
class LIFERP_API ULifeNeedsComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	ULifeNeedsComponent();

	virtual void TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction) override;

	/** Simulates GameHours of decay. Exposed separately from Tick so it is testable and save-loadable. */
	void SimulateHours(float GameHours);

	UFUNCTION(BlueprintPure, Category = "Life|Needs") float GetNeed(ELifeNeed Need) const { return Needs.Get(Need); }
	UFUNCTION(BlueprintPure, Category = "Life|Needs") const FLifeNeedValues& GetNeeds() const { return Needs; }
	UFUNCTION(BlueprintPure, Category = "Life|Needs") float GetMood() const;

	/** Adds a (possibly negative) delta to every need. */
	UFUNCTION(BlueprintCallable, Category = "Life|Needs") void ApplyDelta(const FLifeNeedValues& Delta);
	UFUNCTION(BlueprintCallable, Category = "Life|Needs") void SetNeeds(const FLifeNeedValues& NewNeeds);

	/** Multiplier on Energy decay (sprinting). */
	void SetEnergyDecayMultiplier(float Multiplier) { EnergyDecayMultiplier = Multiplier; }

	/** Fired once when a need drops to the critical threshold. */
	UPROPERTY(BlueprintAssignable, Category = "Life|Needs") FLifeNeedEvent OnNeedCritical;

	/** Fired once when a need hits zero. */
	UPROPERTY(BlueprintAssignable, Category = "Life|Needs") FLifeNeedEvent OnNeedDepleted;

private:
	void CheckThresholds(const FLifeNeedValues& Before);

	UPROPERTY(VisibleInstanceOnly, Category = "Life|Needs") FLifeNeedValues Needs;
	float EnergyDecayMultiplier = 1.f;
};
