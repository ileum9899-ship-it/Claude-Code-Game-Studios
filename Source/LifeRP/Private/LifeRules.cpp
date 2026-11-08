// Life RP - pure gameplay rules.

#include "LifeRules.h"

namespace LifeRules
{
	float DecayNeed(float Current, float RatePerHour, float GameHours)
	{
		return FMath::Clamp(Current - RatePerHour * GameHours, NeedMin, NeedMax);
	}

	float ApplyDelta(float Current, float Amount)
	{
		return FMath::Clamp(Current + Amount, NeedMin, NeedMax);
	}

	float EffectiveDecayRate(ELifeNeed Need, float BaseRate, const FLifeNeedValues& Needs)
	{
		if (Need == ELifeNeed::Energy && Needs.Hunger <= NeedMin)
		{
			return BaseRate * 2.f;
		}
		if (Need == ELifeNeed::Social && Needs.Hygiene < 15.f)
		{
			return BaseRate * 1.5f;
		}
		return BaseRate;
	}

	float ComputeMood(const FLifeNeedValues& Needs)
	{
		float Sum = 0.f;
		float Lowest = NeedMax;
		for (ELifeNeed Need : TEnumRange<ELifeNeed>())
		{
			const float V = FMath::Clamp(Needs.Get(Need), NeedMin, NeedMax);
			Sum += V;
			Lowest = FMath::Min(Lowest, V);
		}
		const float Average = Sum / static_cast<float>(ELifeNeed::Count);
		return FMath::Clamp(0.6f * Average + 0.4f * Lowest, NeedMin, NeedMax);
	}

	float ComputePerformance(float Mood)
	{
		const float M = FMath::Clamp(Mood, 0.f, 100.f);
		if (M <= 60.f)
		{
			return FMath::Lerp(0.5f, 1.0f, M / 60.f);
		}
		return FMath::Lerp(1.0f, 1.25f, (M - 60.f) / 40.f);
	}

	int32 ComputeJobLevel(float TotalHoursWorked, float HoursPerLevel, int32 MaxLevel)
	{
		const int32 Max = FMath::Max(1, MaxLevel);
		if (HoursPerLevel <= 0.f)
		{
			return Max;
		}
		const int32 Level = 1 + FMath::FloorToInt(FMath::Max(0.f, TotalHoursWorked) / HoursPerLevel);
		return FMath::Clamp(Level, 1, Max);
	}

	float ComputeShiftPay(float HourlyWage, int32 JobLevel, float WageGrowthPerLevel, float HoursWorked, float Performance)
	{
		const float LevelMultiplier = 1.f + WageGrowthPerLevel * static_cast<float>(FMath::Max(1, JobLevel) - 1);
		return FMath::Max(0.f, HourlyWage * LevelMultiplier * HoursWorked * Performance);
	}

	bool IsWithinShift(float HourOfDay, int32 StartHour, int32 EndHour)
	{
		const float H = FMath::Fmod(FMath::Max(0.f, HourOfDay), 24.f);
		if (StartHour == EndHour)
		{
			return true; // 24h job
		}
		if (StartHour < EndHour)
		{
			return H >= StartHour && H < EndHour;
		}
		return H >= StartHour || H < EndHour; // wraps midnight
	}

	int32 AdvanceClock(float& MinuteOfDay, int32& Day, float DeltaMinutes)
	{
		constexpr float MinutesPerDay = 1440.f;
		MinuteOfDay += FMath::Max(0.f, DeltaMinutes);
		int32 Crossed = 0;
		while (MinuteOfDay >= MinutesPerDay)
		{
			MinuteOfDay -= MinutesPerDay;
			++Day;
			++Crossed;
		}
		return Crossed;
	}

	float MinutesUntilHour(float MinuteOfDay, float TargetHour)
	{
		const float Target = FMath::Fmod(TargetHour, 24.f) * 60.f;
		float Delta = Target - MinuteOfDay;
		if (Delta <= 0.f)
		{
			Delta += 1440.f;
		}
		return Delta;
	}

	float SunElevationDegrees(float HourOfDay, float MaxElevation)
	{
		const float Angle = (HourOfDay - 6.f) / 24.f * 2.f * UE_PI;
		return MaxElevation * FMath::Sin(Angle);
	}

	float SunAzimuthDegrees(float HourOfDay)
	{
		return FMath::Fmod(HourOfDay / 24.f * 360.f + 180.f, 360.f);
	}

	float SpeedMultiplierForEnergy(float Energy, float LowThreshold, float LowMultiplier)
	{
		if (LowThreshold <= 0.f || Energy >= LowThreshold)
		{
			return 1.f;
		}
		return FMath::Lerp(LowMultiplier, 1.f, FMath::Clamp(Energy / LowThreshold, 0.f, 1.f));
	}

	float ConversationSocialGain(float BaseGain, int32 Relationship, float Charisma)
	{
		const float RelFactor = 1.f + FMath::Clamp(Relationship, -100, 100) / 200.f;
		const float ChaFactor = 1.f + FMath::Max(0.f, Charisma) / 20.f;
		return FMath::Max(0.f, BaseGain * RelFactor * ChaFactor);
	}

	int32 DailyInterest(int32 BankBalance, float DailyRate)
	{
		if (BankBalance <= 0 || DailyRate <= 0.f)
		{
			return 0;
		}
		return FMath::FloorToInt(static_cast<float>(BankBalance) * DailyRate);
	}

	FString RelationshipLabel(int32 Relationship)
	{
		if (Relationship >= 80) return TEXT("Best Friend");
		if (Relationship >= 50) return TEXT("Friend");
		if (Relationship >= 20) return TEXT("Acquaintance");
		if (Relationship > -20) return TEXT("Stranger");
		return TEXT("Rival");
	}
}
