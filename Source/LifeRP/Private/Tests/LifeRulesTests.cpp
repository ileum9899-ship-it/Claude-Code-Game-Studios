// Life RP - unit tests for the pure rules in LifeRules.h.
// Run: UnrealEditor-Cmd <abs>/LifeRP.uproject -ExecCmds="Automation RunTests LifeRP.; Quit" -unattended -nullrhi -stdout

#include "LifeRules.h"
#include "Misc/AutomationTest.h"

#if WITH_DEV_AUTOMATION_TESTS

namespace LifeRulesTestData
{
	constexpr float Tolerance = 0.001f;
	FLifeNeedValues Needs(float Hunger, float Energy, float Hygiene, float Fun, float Social)
	{
		return FLifeNeedValues(Hunger, Energy, Hygiene, Fun, Social);
	}
}
using namespace LifeRulesTestData;

// ------------------------------------------------------------------ Needs

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FNeedsDecayReducesByRateTimesHoursTest, "LifeRP.Needs.DecayReducesByRateTimesHours",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FNeedsDecayReducesByRateTimesHoursTest::RunTest(const FString& Parameters)
{
	TestEqual(TEXT("80 - 5/h * 2h"), LifeRules::DecayNeed(80.f, 5.f, 2.f), 70.f, Tolerance);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FNeedsDecayClampsAtZeroTest, "LifeRP.Needs.DecayClampsAtZero",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FNeedsDecayClampsAtZeroTest::RunTest(const FString& Parameters)
{
	TestEqual(TEXT("cannot go below 0"), LifeRules::DecayNeed(3.f, 10.f, 1.f), 0.f, Tolerance);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FNeedsDeltaClampsAtHundredTest, "LifeRP.Needs.DeltaClampsAtHundred",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FNeedsDeltaClampsAtHundredTest::RunTest(const FString& Parameters)
{
	TestEqual(TEXT("cannot exceed 100"), LifeRules::ApplyDelta(90.f, 55.f), 100.f, Tolerance);
	TestEqual(TEXT("negative delta"), LifeRules::ApplyDelta(40.f, -15.f), 25.f, Tolerance);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FNeedsStarvingDoublesEnergyDecayTest, "LifeRP.Needs.StarvingDoublesEnergyDecay",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FNeedsStarvingDoublesEnergyDecayTest::RunTest(const FString& Parameters)
{
	const FLifeNeedValues Starving = Needs(0.f, 50.f, 50.f, 50.f, 50.f);
	const FLifeNeedValues Fed = Needs(10.f, 50.f, 50.f, 50.f, 50.f);
	TestEqual(TEXT("starving energy rate"), LifeRules::EffectiveDecayRate(ELifeNeed::Energy, 4.f, Starving), 8.f, Tolerance);
	TestEqual(TEXT("fed energy rate"), LifeRules::EffectiveDecayRate(ELifeNeed::Energy, 4.f, Fed), 4.f, Tolerance);
	TestEqual(TEXT("hunger rate unaffected"), LifeRules::EffectiveDecayRate(ELifeNeed::Hunger, 5.5f, Starving), 5.5f, Tolerance);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FNeedsFilthySpeedsSocialDecayTest, "LifeRP.Needs.FilthySpeedsSocialDecay",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FNeedsFilthySpeedsSocialDecayTest::RunTest(const FString& Parameters)
{
	const FLifeNeedValues Filthy = Needs(50.f, 50.f, 10.f, 50.f, 50.f);
	TestEqual(TEXT("social x1.5 when hygiene < 15"), LifeRules::EffectiveDecayRate(ELifeNeed::Social, 3.f, Filthy), 4.5f, Tolerance);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FNeedsMoodWeightsLowestNeedTest, "LifeRP.Needs.MoodWeightsLowestNeed",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FNeedsMoodWeightsLowestNeedTest::RunTest(const FString& Parameters)
{
	TestEqual(TEXT("all full"), LifeRules::ComputeMood(FLifeNeedValues::Uniform(100.f)), 100.f, Tolerance);
	// Average 80, lowest 0 -> 0.6*80 + 0.4*0 = 48.
	TestEqual(TEXT("one empty need"), LifeRules::ComputeMood(Needs(0.f, 100.f, 100.f, 100.f, 100.f)), 48.f, Tolerance);
	return true;
}

// ------------------------------------------------------------------ Career

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FCareerPerformanceCurveTest, "LifeRP.Career.PerformanceCurve",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FCareerPerformanceCurveTest::RunTest(const FString& Parameters)
{
	TestEqual(TEXT("mood 0"), LifeRules::ComputePerformance(0.f), 0.5f, Tolerance);
	TestEqual(TEXT("mood 60"), LifeRules::ComputePerformance(60.f), 1.0f, Tolerance);
	TestEqual(TEXT("mood 100"), LifeRules::ComputePerformance(100.f), 1.25f, Tolerance);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FCareerJobLevelFromHoursTest, "LifeRP.Career.JobLevelFromHours",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FCareerJobLevelFromHoursTest::RunTest(const FString& Parameters)
{
	TestEqual(TEXT("new hire"), LifeRules::ComputeJobLevel(0.f, 16.f, 5), 1);
	TestEqual(TEXT("just under first promotion"), LifeRules::ComputeJobLevel(15.9f, 16.f, 5), 1);
	TestEqual(TEXT("first promotion"), LifeRules::ComputeJobLevel(16.f, 16.f, 5), 2);
	TestEqual(TEXT("capped at max"), LifeRules::ComputeJobLevel(1000.f, 16.f, 5), 5);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FCareerShiftPayScalesWithLevelTest, "LifeRP.Career.ShiftPayScalesWithLevel",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FCareerShiftPayScalesWithLevelTest::RunTest(const FString& Parameters)
{
	// 20/h * (1 + 0.15*2) * 4h * 1.0 = 104.
	TestEqual(TEXT("level 3, 4 hours"), LifeRules::ComputeShiftPay(20.f, 3, 0.15f, 4.f, 1.f), 104.f, Tolerance);
	TestEqual(TEXT("bad mood halves pay"), LifeRules::ComputeShiftPay(20.f, 1, 0.15f, 1.f, 0.5f), 10.f, Tolerance);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FCareerShiftWindowWrapsMidnightTest, "LifeRP.Career.ShiftWindowWrapsMidnight",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FCareerShiftWindowWrapsMidnightTest::RunTest(const FString& Parameters)
{
	TestTrue(TEXT("day shift start inclusive"), LifeRules::IsWithinShift(9.f, 9, 17));
	TestFalse(TEXT("day shift end exclusive"), LifeRules::IsWithinShift(17.f, 9, 17));
	TestTrue(TEXT("night shift before midnight"), LifeRules::IsWithinShift(23.5f, 22, 6));
	TestTrue(TEXT("night shift after midnight"), LifeRules::IsWithinShift(2.f, 22, 6));
	TestFalse(TEXT("night shift at noon"), LifeRules::IsWithinShift(12.f, 22, 6));
	return true;
}

// ------------------------------------------------------------------ Time

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FTimeClockWrapsToNextDayTest, "LifeRP.Time.ClockWrapsToNextDay",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FTimeClockWrapsToNextDayTest::RunTest(const FString& Parameters)
{
	float Minute = 1430.f;
	int32 Day = 3;
	const int32 Crossed = LifeRules::AdvanceClock(Minute, Day, 20.f);
	TestEqual(TEXT("days crossed"), Crossed, 1);
	TestEqual(TEXT("day"), Day, 4);
	TestEqual(TEXT("minute"), Minute, 10.f, Tolerance);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FTimeMinutesUntilWakeTest, "LifeRP.Time.MinutesUntilWake",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FTimeMinutesUntilWakeTest::RunTest(const FString& Parameters)
{
	TestEqual(TEXT("23:00 -> 07:00"), LifeRules::MinutesUntilHour(23.f * 60.f, 7.f), 480.f, Tolerance);
	TestEqual(TEXT("05:30 -> 07:00"), LifeRules::MinutesUntilHour(5.5f * 60.f, 7.f), 90.f, Tolerance);
	TestEqual(TEXT("exactly 07:00 -> next day"), LifeRules::MinutesUntilHour(7.f * 60.f, 7.f), 1440.f, Tolerance);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FTimeSunElevationCycleTest, "LifeRP.Time.SunElevationCycle",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FTimeSunElevationCycleTest::RunTest(const FString& Parameters)
{
	TestEqual(TEXT("sunrise"), LifeRules::SunElevationDegrees(6.f, 60.f), 0.f, Tolerance);
	TestEqual(TEXT("noon"), LifeRules::SunElevationDegrees(12.f, 60.f), 60.f, Tolerance);
	TestEqual(TEXT("midnight"), LifeRules::SunElevationDegrees(0.f, 60.f), -60.f, Tolerance);
	return true;
}

// ------------------------------------------------------------------ Movement / social / economy

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FMovementLowEnergySlowsTest, "LifeRP.Movement.LowEnergySlows",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FMovementLowEnergySlowsTest::RunTest(const FString& Parameters)
{
	TestEqual(TEXT("rested"), LifeRules::SpeedMultiplierForEnergy(50.f, 20.f, 0.65f), 1.f, Tolerance);
	TestEqual(TEXT("empty"), LifeRules::SpeedMultiplierForEnergy(0.f, 20.f, 0.65f), 0.65f, Tolerance);
	TestEqual(TEXT("half of threshold"), LifeRules::SpeedMultiplierForEnergy(10.f, 20.f, 0.6f), 0.8f, Tolerance);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FSocialConversationGainTest, "LifeRP.Social.ConversationGain",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FSocialConversationGainTest::RunTest(const FString& Parameters)
{
	TestEqual(TEXT("stranger, no charisma"), LifeRules::ConversationSocialGain(12.f, 0, 0.f), 12.f, Tolerance);
	// 12 * (1 + 100/200) * (1 + 10/20) = 27.
	TestEqual(TEXT("best friend, max charisma"), LifeRules::ConversationSocialGain(12.f, 100, 10.f), 27.f, Tolerance);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FSocialRelationshipLabelsTest, "LifeRP.Social.RelationshipLabels",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FSocialRelationshipLabelsTest::RunTest(const FString& Parameters)
{
	TestEqual(TEXT("0"), LifeRules::RelationshipLabel(0), FString(TEXT("Stranger")));
	TestEqual(TEXT("50"), LifeRules::RelationshipLabel(50), FString(TEXT("Friend")));
	TestEqual(TEXT("80"), LifeRules::RelationshipLabel(80), FString(TEXT("Best Friend")));
	TestEqual(TEXT("-40"), LifeRules::RelationshipLabel(-40), FString(TEXT("Rival")));
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FEconomyDailyInterestTest, "LifeRP.Economy.DailyInterest",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FEconomyDailyInterestTest::RunTest(const FString& Parameters)
{
	TestEqual(TEXT("1000 at 0.2%"), LifeRules::DailyInterest(1000, 0.002f), 2);
	TestEqual(TEXT("no interest on debt"), LifeRules::DailyInterest(-500, 0.002f), 0);
	return true;
}

#endif // WITH_DEV_AUTOMATION_TESTS
