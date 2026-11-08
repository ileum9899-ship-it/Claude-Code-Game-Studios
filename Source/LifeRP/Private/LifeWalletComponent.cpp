// Life RP - money.

#include "LifeWalletComponent.h"
#include "LifeGameState.h"
#include "LifePlayerController.h"
#include "LifeRules.h"
#include "LifeSettings.h"
#include "Engine/World.h"

ULifeWalletComponent::ULifeWalletComponent()
{
	PrimaryComponentTick.bCanEverTick = false;
	Cash = ULifeSettings::Get()->StartingCash;
	Bank = ULifeSettings::Get()->StartingBank;
}

void ULifeWalletComponent::BeginPlay()
{
	Super::BeginPlay();
	if (ALifeGameState* GS = GetWorld()->GetGameState<ALifeGameState>())
	{
		GS->OnNewDay.AddDynamic(this, &ULifeWalletComponent::HandleNewDay);
	}
}

bool ULifeWalletComponent::CanAfford(int32 Amount) const
{
	return Amount <= Cash + FMath::Max(0, Bank);
}

bool ULifeWalletComponent::TrySpend(int32 Amount, const FString& Reason)
{
	if (Amount <= 0)
	{
		return true;
	}
	if (!CanAfford(Amount))
	{
		LifeNotify(this, FString::Printf(TEXT("돈이 부족합니다: %s ($%d)"), *Reason, Amount), FLinearColor(1.f, 0.35f, 0.3f));
		return false;
	}
	const int32 FromCash = FMath::Min(Cash, Amount);
	Cash -= FromCash;
	Bank -= (Amount - FromCash);
	return true;
}

void ULifeWalletComponent::AddCash(int32 Amount)
{
	Cash = FMath::Max(0, Cash + Amount);
}

void ULifeWalletComponent::AddBank(int32 Amount)
{
	Bank += Amount;
}

int32 ULifeWalletComponent::DepositAll()
{
	const int32 Moved = Cash;
	Bank += Cash;
	Cash = 0;
	return Moved;
}

int32 ULifeWalletComponent::Withdraw(int32 Amount)
{
	const int32 Moved = FMath::Clamp(Amount, 0, FMath::Max(0, Bank));
	Bank -= Moved;
	Cash += Moved;
	return Moved;
}

void ULifeWalletComponent::HandleNewDay(int32 NewDay)
{
	const ULifeSettings* S = ULifeSettings::Get();
	const int32 Interest = LifeRules::DailyInterest(Bank, S->DailyBankInterest);
	Bank += Interest - S->DailyRent;
	LifeNotify(this, FString::Printf(TEXT("%d일차: 월세 -$%d, 이자 +$%d"), NewDay, S->DailyRent, Interest), FLinearColor(0.8f, 0.85f, 1.f));
	if (Bank < 0)
	{
		LifeNotify(this, TEXT("은행 잔고가 마이너스입니다! 일자리를 구하세요."), FLinearColor(1.f, 0.3f, 0.25f));
	}
}
