// Life RP - shared data types.

#pragma once

#include "CoreMinimal.h"
#include "LifeTypes.generated.h"

/** The five needs every resident has to keep up. All are 0 (empty) .. 100 (full). */
UENUM(BlueprintType)
enum class ELifeNeed : uint8
{
	Hunger,
	Energy,
	Hygiene,
	Fun,
	Social,
	Count UMETA(Hidden)
};
ENUM_RANGE_BY_COUNT(ELifeNeed, ELifeNeed::Count);

/** Timed activities the player can perform. Time runs faster while one is active. */
UENUM(BlueprintType)
enum class ELifeActivity : uint8
{
	None,
	Sleep,
	Shower,
	WatchTV,
	Workout
};

/** What an interactable station in the world does. */
UENUM(BlueprintType)
enum class ELifeStationType : uint8
{
	Bed,
	Shower,
	Television,
	Fridge,
	ShopItem,
	Workstation,
	ATMDeposit,
	ATMWithdraw,
	Workout
};

/** One value per need. Used for the need state itself and for rates / restore amounts. */
USTRUCT(BlueprintType)
struct LIFERP_API FLifeNeedValues
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Needs") float Hunger = 0.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Needs") float Energy = 0.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Needs") float Hygiene = 0.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Needs") float Fun = 0.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Needs") float Social = 0.f;

	FLifeNeedValues() = default;
	FLifeNeedValues(float InHunger, float InEnergy, float InHygiene, float InFun, float InSocial)
		: Hunger(InHunger), Energy(InEnergy), Hygiene(InHygiene), Fun(InFun), Social(InSocial) {}

	/** Uniform value for every need. */
	static FLifeNeedValues Uniform(float Value) { return FLifeNeedValues(Value, Value, Value, Value, Value); }

	/** Read one need by enum. */
	float Get(ELifeNeed Need) const;

	/** Write one need by enum. */
	void Set(ELifeNeed Need, float Value);

	/** True when every entry is zero. */
	bool IsZero() const;
};

/** A job the player can be hired for at a workstation. */
USTRUCT(BlueprintType)
struct LIFERP_API FLifeJobDef
{
	GENERATED_BODY()

	/** Stable id referenced by workstations and save games. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Job") FName Id;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Job") FString DisplayName;

	/** Base pay per in-game hour at job level 1. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Job") float HourlyWage = 15.f;

	/** Shift start hour (0-23). A shift may wrap past midnight. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Job") int32 ShiftStartHour = 9;

	/** Shift end hour (0-23, exclusive). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Job") int32 ShiftEndHour = 17;

	/** Extra need drain per in-game hour worked, on top of normal decay. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Job") FLifeNeedValues DrainPerHour;

	/** Hours worked needed for each promotion. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Job") float HoursPerLevel = 16.f;

	/** Wage multiplier gained per level above 1 (0.15 = +15% per level). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Job") float WageGrowthPerLevel = 0.15f;

	/** Highest reachable job level. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Job") int32 MaxLevel = 5;

	/** Job titles per level (index 0 = level 1). Falls back to DisplayName. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Job") TArray<FString> LevelTitles;

	/** Colour used for the workplace accents. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Job") FLinearColor AccentColor = FLinearColor::White;
};

/** Something the player can buy and consume immediately at a shop stand. */
USTRUCT(BlueprintType)
struct LIFERP_API FLifeShopItem
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Shop") FName Id;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Shop") FString DisplayName;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Shop") int32 Price = 5;

	/** Need gained on purchase. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Shop") FLifeNeedValues Restore;

	/** Colour of the item prop on the counter. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Shop") FLinearColor PropColor = FLinearColor::White;
};

/** Tuning for one timed activity. */
USTRUCT(BlueprintType)
struct LIFERP_API FLifeActivityTuning
{
	GENERATED_BODY()

	/** In-game minutes the activity lasts (sleep ignores this and runs to wake-up). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Activity") float DurationMinutes = 30.f;

	/** Need change per in-game hour while active (may be negative). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Activity") FLifeNeedValues RatePerHour;

	/** Game clock multiplier while active. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Activity") float TimeMultiplier = 10.f;
};
