// Life RP - pure gameplay rules.
//
// Every formula the game uses lives here as a free function with no engine state,
// so it can be unit-tested (see Private/Tests/LifeRulesTests.cpp) and tuned from
// ULifeSettings without touching gameplay classes. Formulas are documented in
// design/game-brief.md section "Formulas".

#pragma once

#include "CoreMinimal.h"
#include "LifeTypes.h"

namespace LifeRules
{
	/** Need range bounds. */
	constexpr float NeedMin = 0.f;
	constexpr float NeedMax = 100.f;

	/** Decays a need by RatePerHour * GameHours, clamped to [0, 100]. */
	LIFERP_API float DecayNeed(float Current, float RatePerHour, float GameHours);

	/** Adds Amount (may be negative) to a need, clamped to [0, 100]. */
	LIFERP_API float ApplyDelta(float Current, float Amount);

	/**
	 * Effective decay rate for a need given the whole need state.
	 * Starving (Hunger == 0) doubles Energy decay; being filthy (Hygiene < 15)
	 * makes Social decay 1.5x faster.
	 */
	LIFERP_API float EffectiveDecayRate(ELifeNeed Need, float BaseRate, const FLifeNeedValues& Needs);

	/**
	 * Mood 0..100: 60% the average need, 40% the lowest need, so one neglected
	 * need drags mood down noticeably.
	 */
	LIFERP_API float ComputeMood(const FLifeNeedValues& Needs);

	/** Work performance multiplier from mood: 0.5 at mood 0, 1.0 at 60, 1.25 at 100. */
	LIFERP_API float ComputePerformance(float Mood);

	/** Job level (1..MaxLevel) for total hours worked. */
	LIFERP_API int32 ComputeJobLevel(float TotalHoursWorked, float HoursPerLevel, int32 MaxLevel);

	/** Pay for HoursWorked: wage * (1 + growth * (level - 1)) * hours * performance. */
	LIFERP_API float ComputeShiftPay(float HourlyWage, int32 JobLevel, float WageGrowthPerLevel, float HoursWorked, float Performance);

	/** True if HourOfDay (0..24) lies in [StartHour, EndHour). Handles shifts that wrap midnight. */
	LIFERP_API bool IsWithinShift(float HourOfDay, int32 StartHour, int32 EndHour);

	/**
	 * Advances the clock. MinuteOfDay wraps at 1440 and increments Day for every
	 * wrap. Returns the number of day boundaries crossed.
	 */
	LIFERP_API int32 AdvanceClock(float& MinuteOfDay, int32& Day, float DeltaMinutes);

	/** In-game minutes from MinuteOfDay until the next occurrence of TargetHour (never 0: a full day if already there). */
	LIFERP_API float MinutesUntilHour(float MinuteOfDay, float TargetHour);

	/** Sun elevation in degrees: 0 at 06:00 / 18:00, MaxElevation at noon, -MaxElevation at midnight. */
	LIFERP_API float SunElevationDegrees(float HourOfDay, float MaxElevation);

	/** Sun azimuth (yaw) in degrees for the hour: one full turn per day. */
	LIFERP_API float SunAzimuthDegrees(float HourOfDay);

	/** Movement speed multiplier: 1 above LowThreshold energy, ramps to LowMultiplier at 0. */
	LIFERP_API float SpeedMultiplierForEnergy(float Energy, float LowThreshold, float LowMultiplier);

	/** Social gain from a conversation: base * (1 + relationship/200) * (1 + charisma/20). */
	LIFERP_API float ConversationSocialGain(float BaseGain, int32 Relationship, float Charisma);

	/** Daily interest on a positive bank balance (rounded down); 0 on debt. */
	LIFERP_API int32 DailyInterest(int32 BankBalance, float DailyRate);

	/** Relationship label for a value -100..100. */
	LIFERP_API FString RelationshipLabel(int32 Relationship);
}
