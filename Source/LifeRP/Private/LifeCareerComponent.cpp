// Life RP - jobs and shifts.

#include "LifeCareerComponent.h"
#include "LifeGameState.h"
#include "LifeNeedsComponent.h"
#include "LifePlayerController.h"
#include "LifeRules.h"
#include "LifeSettings.h"
#include "LifeStation.h"
#include "LifeWalletComponent.h"
#include "Engine/World.h"
#include "GameFramework/Actor.h"

namespace
{
	FString ShiftText(const FLifeJobDef& Job)
	{
		return FString::Printf(TEXT("%02d:00-%02d:00"), Job.ShiftStartHour, Job.ShiftEndHour);
	}

	FString TitleFor(const FLifeJobDef& Job, int32 Level)
	{
		return Job.LevelTitles.IsValidIndex(Level - 1) ? Job.LevelTitles[Level - 1] : Job.DisplayName;
	}
}

ULifeCareerComponent::ULifeCareerComponent()
{
	PrimaryComponentTick.bCanEverTick = true;
}

const FLifeJobDef* ULifeCareerComponent::GetJob() const
{
	return JobId.IsNone() ? nullptr : ULifeSettings::Get()->FindJob(JobId);
}

int32 ULifeCareerComponent::GetJobLevel() const
{
	const FLifeJobDef* Job = GetJob();
	return Job ? LifeRules::ComputeJobLevel(TotalHours, Job->HoursPerLevel, Job->MaxLevel) : 0;
}

FString ULifeCareerComponent::GetJobSummary() const
{
	const FLifeJobDef* Job = GetJob();
	if (!Job)
	{
		return FString();
	}
	const int32 Level = GetJobLevel();
	return FString::Printf(TEXT("%s (%d레벨)   %s"), *TitleFor(*Job, Level), Level, *ShiftText(*Job));
}

void ULifeCareerComponent::SetCareer(FName InJobId, float InTotalHours)
{
	JobId = InJobId;
	TotalHours = FMath::Max(0.f, InTotalHours);
	bClockedIn = false;
	ShiftHours = 0.f;
	ShiftEarnings = 0.f;
	ActiveStation.Reset();
}

FString ULifeCareerComponent::GetWorkstationPrompt(FName StationJobId) const
{
	const FLifeJobDef* StationJob = ULifeSettings::Get()->FindJob(StationJobId);
	if (!StationJob)
	{
		return TEXT("고장");
	}
	if (JobId != StationJobId)
	{
		return FString::Printf(TEXT("지원하기: %s  시급 $%.0f  %s"), *StationJob->DisplayName, StationJob->HourlyWage, *ShiftText(*StationJob));
	}
	return bClockedIn ? FString::Printf(TEXT("퇴근하기  (+$%.0f)"), ShiftEarnings) : TEXT("출근하기");
}

void ULifeCareerComponent::UseWorkstation(ALifeStation* Station)
{
	if (!Station)
	{
		return;
	}
	const FLifeJobDef* StationJob = ULifeSettings::Get()->FindJob(Station->GetItemId());
	if (!StationJob)
	{
		return;
	}

	if (JobId != StationJob->Id)
	{
		if (bClockedIn)
		{
			ClockOut(TEXT("근무 중에 일을 그만뒀습니다"));
		}
		JobId = StationJob->Id;
		TotalHours = 0.f;
		LifeNotify(this, FString::Printf(TEXT("%s(으)로 채용되었습니다! 근무 시간 %s."), *TitleFor(*StationJob, 1), *ShiftText(*StationJob)), FLinearColor(0.4f, 1.f, 0.5f));
		return;
	}

	if (bClockedIn)
	{
		ClockOut(TEXT("퇴근했습니다"));
		return;
	}

	const ALifeGameState* GS = GetWorld()->GetGameState<ALifeGameState>();
	if (GS && !LifeRules::IsWithinShift(GS->GetHourOfDay(), StationJob->ShiftStartHour, StationJob->ShiftEndHour))
	{
		LifeNotify(this, FString::Printf(TEXT("근무 시간은 %s입니다."), *ShiftText(*StationJob)), FLinearColor(1.f, 0.85f, 0.4f));
		return;
	}

	bClockedIn = true;
	ShiftHours = 0.f;
	ShiftEarnings = 0.f;
	ActiveStation = Station;
	LifeNotify(this, TEXT("출근했습니다. 근무지 근처에 있어야 급여가 쌓입니다."), FLinearColor(0.4f, 1.f, 0.5f));
}

void ULifeCareerComponent::ClockOut(const FString& Reason)
{
	if (!bClockedIn)
	{
		return;
	}
	bClockedIn = false;
	ActiveStation.Reset();

	const int32 Pay = FMath::RoundToInt(ShiftEarnings);
	if (ULifeWalletComponent* Wallet = GetOwner()->FindComponentByClass<ULifeWalletComponent>())
	{
		Wallet->AddBank(Pay);
	}
	LifeNotify(this, FString::Printf(TEXT("%s. %.1f시간 근무, $%d 지급."), *Reason, ShiftHours, Pay), FLinearColor(0.4f, 1.f, 0.5f));
	ShiftHours = 0.f;
	ShiftEarnings = 0.f;
}

void ULifeCareerComponent::TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction)
{
	Super::TickComponent(DeltaTime, TickType, ThisTickFunction);
	if (!bClockedIn)
	{
		return;
	}

	const FLifeJobDef* Job = GetJob();
	const ALifeGameState* GS = GetWorld()->GetGameState<ALifeGameState>();
	if (!Job || !GS)
	{
		return;
	}

	if (!ActiveStation.IsValid() || FVector::Dist2D(ActiveStation->GetActorLocation(), GetOwner()->GetActorLocation()) > WorkRadius)
	{
		ClockOut(TEXT("근무지를 벗어났습니다"));
		return;
	}
	if (!LifeRules::IsWithinShift(GS->GetHourOfDay(), Job->ShiftStartHour, Job->ShiftEndHour))
	{
		ClockOut(TEXT("근무 시간이 끝났습니다"));
		return;
	}

	const float Hours = GS->RealToGameHours(DeltaTime);
	const int32 LevelBefore = GetJobLevel();
	float Performance = 1.f;
	if (ULifeNeedsComponent* Needs = GetOwner()->FindComponentByClass<ULifeNeedsComponent>())
	{
		Performance = LifeRules::ComputePerformance(Needs->GetMood());
		FLifeNeedValues Drain = Job->DrainPerHour;
		for (ELifeNeed Need : TEnumRange<ELifeNeed>())
		{
			Drain.Set(Need, -Drain.Get(Need) * Hours);
		}
		Needs->ApplyDelta(Drain);
	}

	ShiftEarnings += LifeRules::ComputeShiftPay(Job->HourlyWage, LevelBefore, Job->WageGrowthPerLevel, Hours, Performance);
	ShiftHours += Hours;
	TotalHours += Hours;

	const int32 LevelAfter = GetJobLevel();
	if (LevelAfter > LevelBefore)
	{
		LifeNotify(this, FString::Printf(TEXT("%s(으)로 승진했습니다!"), *TitleFor(*Job, LevelAfter)), FLinearColor(1.f, 0.85f, 0.2f));
	}
}
