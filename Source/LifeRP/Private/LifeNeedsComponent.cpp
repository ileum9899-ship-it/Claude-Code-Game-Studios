// Life RP - needs simulation.

#include "LifeNeedsComponent.h"
#include "LifeGameState.h"
#include "LifeRules.h"
#include "LifeSettings.h"
#include "Engine/World.h"

ULifeNeedsComponent::ULifeNeedsComponent()
{
	PrimaryComponentTick.bCanEverTick = true;
	Needs = ULifeSettings::Get()->StartingNeeds;
}

void ULifeNeedsComponent::TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction)
{
	Super::TickComponent(DeltaTime, TickType, ThisTickFunction);
	if (const ALifeGameState* GS = GetWorld()->GetGameState<ALifeGameState>())
	{
		SimulateHours(GS->RealToGameHours(DeltaTime));
	}
}

void ULifeNeedsComponent::SimulateHours(float GameHours)
{
	if (GameHours <= 0.f)
	{
		return;
	}
	const FLifeNeedValues Before = Needs;
	const FLifeNeedValues& Base = ULifeSettings::Get()->DecayPerHour;
	for (ELifeNeed Need : TEnumRange<ELifeNeed>())
	{
		float Rate = LifeRules::EffectiveDecayRate(Need, Base.Get(Need), Before);
		if (Need == ELifeNeed::Energy)
		{
			Rate *= EnergyDecayMultiplier;
		}
		Needs.Set(Need, LifeRules::DecayNeed(Needs.Get(Need), Rate, GameHours));
	}
	CheckThresholds(Before);
}

float ULifeNeedsComponent::GetMood() const
{
	return LifeRules::ComputeMood(Needs);
}

void ULifeNeedsComponent::ApplyDelta(const FLifeNeedValues& Delta)
{
	const FLifeNeedValues Before = Needs;
	for (ELifeNeed Need : TEnumRange<ELifeNeed>())
	{
		Needs.Set(Need, LifeRules::ApplyDelta(Needs.Get(Need), Delta.Get(Need)));
	}
	CheckThresholds(Before);
}

void ULifeNeedsComponent::SetNeeds(const FLifeNeedValues& NewNeeds)
{
	for (ELifeNeed Need : TEnumRange<ELifeNeed>())
	{
		Needs.Set(Need, FMath::Clamp(NewNeeds.Get(Need), LifeRules::NeedMin, LifeRules::NeedMax));
	}
}

void ULifeNeedsComponent::CheckThresholds(const FLifeNeedValues& Before)
{
	const float Critical = ULifeSettings::Get()->CriticalThreshold;
	for (ELifeNeed Need : TEnumRange<ELifeNeed>())
	{
		const float Old = Before.Get(Need);
		const float New = Needs.Get(Need);
		if (Old > Critical && New <= Critical)
		{
			OnNeedCritical.Broadcast(Need);
		}
		if (Old > 0.f && New <= 0.f)
		{
			OnNeedDepleted.Broadcast(Need);
		}
	}
}
