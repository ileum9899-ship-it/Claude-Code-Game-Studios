// Life RP - data-driven tuning.

#pragma once

#include "CoreMinimal.h"
#include "Engine/DeveloperSettings.h"
#include "UObject/SoftObjectPath.h"
#include "LifeTypes.h"
#include "LifeSettings.generated.h"

/**
 * Every gameplay number in Life RP. Edit in the editor under
 * Project Settings > Game > Life RP (saved to Config/DefaultGame.ini).
 * Code reads it through ULifeSettings::Get().
 */
UCLASS(Config = Game, DefaultConfig, meta = (DisplayName = "Life RP"))
class LIFERP_API ULifeSettings : public UDeveloperSettings
{
	GENERATED_BODY()

public:
	ULifeSettings();

	/** Convenience accessor for the class default object. */
	static const ULifeSettings* Get() { return GetDefault<ULifeSettings>(); }

	/** Finds a job by id, or nullptr. */
	const FLifeJobDef* FindJob(FName JobId) const;

	/** Finds a shop item by id, or nullptr. */
	const FLifeShopItem* FindShopItem(FName ItemId) const;

	virtual FName GetCategoryName() const override { return TEXT("Game"); }

	// ---------------------------------------------------------------- Time
	/** Real seconds per in-game minute (1.0 = a 24 minute day). */
	UPROPERTY(Config, EditAnywhere, Category = "Time", meta = (ClampMin = "0.05")) float RealSecondsPerGameMinute;
	UPROPERTY(Config, EditAnywhere, Category = "Time", meta = (ClampMin = "0", ClampMax = "23.99")) float StartHour;
	UPROPERTY(Config, EditAnywhere, Category = "Time") float WakeHour;
	UPROPERTY(Config, EditAnywhere, Category = "Time") float SleepTimeMultiplier;
	/** Highest sun elevation at noon, degrees. */
	UPROPERTY(Config, EditAnywhere, Category = "Time") float MaxSunElevation;

	// ---------------------------------------------------------------- Needs
	UPROPERTY(Config, EditAnywhere, Category = "Needs") FLifeNeedValues StartingNeeds;
	/** Base decay per in-game hour. */
	UPROPERTY(Config, EditAnywhere, Category = "Needs") FLifeNeedValues DecayPerHour;
	/** A need at or below this shows a warning. */
	UPROPERTY(Config, EditAnywhere, Category = "Needs") float CriticalThreshold;
	/** Energy decay multiplier while sprinting. */
	UPROPERTY(Config, EditAnywhere, Category = "Needs") float SprintEnergyMultiplier;
	/** Social gained per conversation before relationship / charisma bonuses. */
	UPROPERTY(Config, EditAnywhere, Category = "Needs") float ConversationSocialGain;
	/** Relationship change per conversation (once per NPC per in-game hour). */
	UPROPERTY(Config, EditAnywhere, Category = "Needs") int32 ConversationRelationshipGain;

	// ---------------------------------------------------------------- Activities
	UPROPERTY(Config, EditAnywhere, Category = "Activities") FLifeActivityTuning Sleep;
	UPROPERTY(Config, EditAnywhere, Category = "Activities") FLifeActivityTuning Shower;
	UPROPERTY(Config, EditAnywhere, Category = "Activities") FLifeActivityTuning WatchTV;
	UPROPERTY(Config, EditAnywhere, Category = "Activities") FLifeActivityTuning Workout;
	/** Fitness skill gained per workout hour (skills are 0..10). */
	UPROPERTY(Config, EditAnywhere, Category = "Activities") float FitnessPerWorkoutHour;
	/** Charisma skill gained per conversation. */
	UPROPERTY(Config, EditAnywhere, Category = "Activities") float CharismaPerConversation;
	UPROPERTY(Config, EditAnywhere, Category = "Activities") int32 FridgeMealCost;
	UPROPERTY(Config, EditAnywhere, Category = "Activities") FLifeNeedValues FridgeMealRestore;

	// ---------------------------------------------------------------- Movement
	UPROPERTY(Config, EditAnywhere, Category = "Movement") float WalkSpeed;
	UPROPERTY(Config, EditAnywhere, Category = "Movement") float RunSpeed;
	/** Run speed bonus per Fitness point (0.03 = +3%). */
	UPROPERTY(Config, EditAnywhere, Category = "Movement") float RunSpeedPerFitness;
	UPROPERTY(Config, EditAnywhere, Category = "Movement") float LowEnergyThreshold;
	UPROPERTY(Config, EditAnywhere, Category = "Movement") float LowEnergySpeedMultiplier;

	// ---------------------------------------------------------------- Economy
	UPROPERTY(Config, EditAnywhere, Category = "Economy") int32 StartingCash;
	UPROPERTY(Config, EditAnywhere, Category = "Economy") int32 StartingBank;
	/** Rent charged from the bank account at midnight. */
	UPROPERTY(Config, EditAnywhere, Category = "Economy") int32 DailyRent;
	UPROPERTY(Config, EditAnywhere, Category = "Economy") float DailyBankInterest;
	UPROPERTY(Config, EditAnywhere, Category = "Economy") int32 ATMWithdrawAmount;
	/** Fee when you pass out from exhaustion and wake up in hospital care. */
	UPROPERTY(Config, EditAnywhere, Category = "Economy") int32 CollapseFee;
	UPROPERTY(Config, EditAnywhere, Category = "Economy") TArray<FLifeJobDef> Jobs;
	UPROPERTY(Config, EditAnywhere, Category = "Economy") TArray<FLifeShopItem> ShopItems;

	// ---------------------------------------------------------------- Town
	UPROPERTY(Config, EditAnywhere, Category = "Town") int32 TownSeed;
	UPROPERTY(Config, EditAnywhere, Category = "Town", meta = (ClampMin = "3", ClampMax = "9")) int32 BlocksPerSide;
	UPROPERTY(Config, EditAnywhere, Category = "Town") float BlockSize;
	UPROPERTY(Config, EditAnywhere, Category = "Town") float RoadWidth;
	UPROPERTY(Config, EditAnywhere, Category = "Town") float SidewalkWidth;
	UPROPERTY(Config, EditAnywhere, Category = "Town") int32 CitizenCount;
	UPROPERTY(Config, EditAnywhere, Category = "Town") int32 TrafficCarCount;
	/** Street lights cast shadows (enable with MegaLights + hardware RT). */
	UPROPERTY(Config, EditAnywhere, Category = "Town") bool bStreetLightShadows;
	UPROPERTY(Config, EditAnywhere, Category = "Town") TArray<FString> CitizenNames;
	UPROPERTY(Config, EditAnywhere, Category = "Town") TArray<FString> SmallTalkLines;

	// ---------------------------------------------------------------- Vehicles
	UPROPERTY(Config, EditAnywhere, Category = "Vehicles") float CarMaxSpeed;
	UPROPERTY(Config, EditAnywhere, Category = "Vehicles") float CarMaxReverseSpeed;
	UPROPERTY(Config, EditAnywhere, Category = "Vehicles") float CarAcceleration;
	UPROPERTY(Config, EditAnywhere, Category = "Vehicles") float CarBrakeDeceleration;
	UPROPERTY(Config, EditAnywhere, Category = "Vehicles") float CarDrag;
	UPROPERTY(Config, EditAnywhere, Category = "Vehicles") float CarSteerDegreesPerSecond;
	UPROPERTY(Config, EditAnywhere, Category = "Vehicles") float TrafficCruiseSpeed;

	// ---------------------------------------------------------------- UI
	/**
	 * Font for the HUD. Empty = engine default (Roboto with its built-in CJK fallback).
	 * If Hangul shows as boxes on your engine build, import a Korean font
	 * (e.g. Noto Sans KR) as a runtime UFont and point this at it.
	 */
	UPROPERTY(Config, EditAnywhere, Category = "UI", meta = (AllowedClasses = "/Script/Engine.Font")) FSoftObjectPath HUDFont;

	// ---------------------------------------------------------------- Save
	UPROPERTY(Config, EditAnywhere, Category = "Save") FString SaveSlotName;
	UPROPERTY(Config, EditAnywhere, Category = "Save") bool bAutoLoadOnStart;
	UPROPERTY(Config, EditAnywhere, Category = "Save") bool bAutoSaveOnWake;
};
