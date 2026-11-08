// Life RP - world clock.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameStateBase.h"
#include "LifeGameState.generated.h"

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FLifeHourChanged, int32, NewHour);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FLifeNewDay, int32, NewDay);

/**
 * Owns the in-game calendar. Every system converts real delta time to game time
 * through RealToGameHours(), so fast-forwarding (sleep, activities) is a single
 * multiplier here.
 */
UCLASS()
class LIFERP_API ALifeGameState : public AGameStateBase
{
	GENERATED_BODY()

public:
	ALifeGameState();

	virtual void Tick(float DeltaSeconds) override;

	/** Hours of game time that pass in DeltaSeconds of real time at the current multiplier. */
	UFUNCTION(BlueprintPure, Category = "Life|Time") float RealToGameHours(float DeltaSeconds) const;

	UFUNCTION(BlueprintPure, Category = "Life|Time") float GetHourOfDay() const { return MinuteOfDay / 60.f; }
	UFUNCTION(BlueprintPure, Category = "Life|Time") float GetMinuteOfDay() const { return MinuteOfDay; }
	UFUNCTION(BlueprintPure, Category = "Life|Time") int32 GetDay() const { return Day; }
	UFUNCTION(BlueprintPure, Category = "Life|Time") bool IsNight() const;

	/** Weekday, day number and HH:MM, localised (see LifeGameState.cpp). */
	UFUNCTION(BlueprintPure, Category = "Life|Time") FString GetClockText() const;

	/** Fast-forward factor (1 = normal). */
	UFUNCTION(BlueprintCallable, Category = "Life|Time") void SetTimeMultiplier(float Multiplier);
	UFUNCTION(BlueprintPure, Category = "Life|Time") float GetTimeMultiplier() const { return TimeMultiplier; }

	/** Restores the clock from a save. */
	UFUNCTION(BlueprintCallable, Category = "Life|Time") void SetClock(int32 InDay, float InMinuteOfDay);

	UPROPERTY(BlueprintAssignable, Category = "Life|Time") FLifeHourChanged OnHourChanged;
	UPROPERTY(BlueprintAssignable, Category = "Life|Time") FLifeNewDay OnNewDay;

protected:
	virtual void BeginPlay() override;

private:
	UPROPERTY(VisibleInstanceOnly, Category = "Life|Time") float MinuteOfDay = 480.f;
	UPROPERTY(VisibleInstanceOnly, Category = "Life|Time") int32 Day = 1;
	UPROPERTY(VisibleInstanceOnly, Category = "Life|Time") float TimeMultiplier = 1.f;
};
