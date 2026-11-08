// Life RP - world clock.

#include "LifeGameState.h"
#include "LifeRules.h"
#include "LifeSettings.h"

ALifeGameState::ALifeGameState()
{
	PrimaryActorTick.bCanEverTick = true;
	PrimaryActorTick.TickGroup = TG_PrePhysics;
	MinuteOfDay = ULifeSettings::Get()->StartHour * 60.f;
}

void ALifeGameState::BeginPlay()
{
	Super::BeginPlay();
}

float ALifeGameState::RealToGameHours(float DeltaSeconds) const
{
	const float SecondsPerMinute = FMath::Max(0.01f, ULifeSettings::Get()->RealSecondsPerGameMinute);
	return (DeltaSeconds / SecondsPerMinute) * TimeMultiplier / 60.f;
}

void ALifeGameState::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);

	const int32 OldHour = FMath::FloorToInt(GetHourOfDay());
	const int32 OldDay = Day;
	LifeRules::AdvanceClock(MinuteOfDay, Day, RealToGameHours(DeltaSeconds) * 60.f);

	for (int32 NewDay = OldDay + 1; NewDay <= Day; ++NewDay)
	{
		OnNewDay.Broadcast(NewDay);
	}
	const int32 NewHour = FMath::FloorToInt(GetHourOfDay());
	if (NewHour != OldHour)
	{
		OnHourChanged.Broadcast(NewHour);
	}
}

bool ALifeGameState::IsNight() const
{
	const float H = GetHourOfDay();
	return H < 6.5f || H >= 19.5f;
}

FString ALifeGameState::GetClockText() const
{
	static const TCHAR* Weekdays[] = { TEXT("Mon"), TEXT("Tue"), TEXT("Wed"), TEXT("Thu"), TEXT("Fri"), TEXT("Sat"), TEXT("Sun") };
	const int32 TotalMinutes = FMath::FloorToInt(MinuteOfDay);
	return FString::Printf(TEXT("%s   Day %d   %02d:%02d"), Weekdays[(Day - 1) % 7], Day, TotalMinutes / 60, TotalMinutes % 60);
}

void ALifeGameState::SetTimeMultiplier(float Multiplier)
{
	TimeMultiplier = FMath::Max(0.f, Multiplier);
}

void ALifeGameState::SetClock(int32 InDay, float InMinuteOfDay)
{
	Day = FMath::Max(1, InDay);
	MinuteOfDay = FMath::Clamp(InMinuteOfDay, 0.f, 1439.99f);
}
