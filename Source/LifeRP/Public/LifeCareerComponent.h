// Life RP - jobs and shifts.

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "LifeTypes.h"
#include "LifeCareerComponent.generated.h"

class ALifeStation;

/**
 * Employment state. A workstation hires you; during shift hours you clock in at
 * it and earn pay while you stay nearby. Pay goes to the bank on clock-out and
 * hours worked drive promotions.
 */
UCLASS(ClassGroup = (Life), meta = (BlueprintSpawnableComponent))
class LIFERP_API ULifeCareerComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	ULifeCareerComponent();

	virtual void TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction) override;

	/** Hire / clock in / clock out depending on state. Called by workstations. */
	void UseWorkstation(ALifeStation* Station);

	/** Prompt text a workstation shows for its job. */
	FString GetWorkstationPrompt(FName JobId) const;

	/** Ends the shift (pays out). Reason is shown to the player. */
	UFUNCTION(BlueprintCallable, Category = "Life|Career") void ClockOut(const FString& Reason);

	UFUNCTION(BlueprintPure, Category = "Life|Career") bool HasJob() const { return !JobId.IsNone(); }
	UFUNCTION(BlueprintPure, Category = "Life|Career") bool IsClockedIn() const { return bClockedIn; }
	UFUNCTION(BlueprintPure, Category = "Life|Career") FName GetJobId() const { return JobId; }
	UFUNCTION(BlueprintPure, Category = "Life|Career") int32 GetJobLevel() const;
	UFUNCTION(BlueprintPure, Category = "Life|Career") float GetTotalHours() const { return TotalHours; }
	UFUNCTION(BlueprintPure, Category = "Life|Career") float GetShiftEarnings() const { return ShiftEarnings; }

	/** Title, level and shift hours (localised). Empty if unemployed. */
	UFUNCTION(BlueprintPure, Category = "Life|Career") FString GetJobSummary() const;

	/** Restores from a save. */
	void SetCareer(FName InJobId, float InTotalHours);

private:
	const FLifeJobDef* GetJob() const;

	UPROPERTY(VisibleInstanceOnly, Category = "Life|Career") FName JobId;
	UPROPERTY(VisibleInstanceOnly, Category = "Life|Career") float TotalHours = 0.f;
	UPROPERTY(VisibleInstanceOnly, Category = "Life|Career") bool bClockedIn = false;
	UPROPERTY(VisibleInstanceOnly, Category = "Life|Career") float ShiftHours = 0.f;
	UPROPERTY(VisibleInstanceOnly, Category = "Life|Career") float ShiftEarnings = 0.f;
	UPROPERTY() TWeakObjectPtr<ALifeStation> ActiveStation;

	/** Distance from the workstation that counts as leaving work, cm. */
	UPROPERTY(EditAnywhere, Category = "Life|Career") float WorkRadius = 600.f;
};
