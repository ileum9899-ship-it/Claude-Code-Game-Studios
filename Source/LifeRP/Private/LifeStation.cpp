// Life RP - usable furniture and service points.

#include "LifeStation.h"
#include "LifeCareerComponent.h"
#include "LifeGameState.h"
#include "LifeNeedsComponent.h"
#include "LifePlayerCharacter.h"
#include "LifePlayerController.h"
#include "LifeSettings.h"
#include "LifeVisuals.h"
#include "LifeWalletComponent.h"
#include "Components/PointLightComponent.h"
#include "Engine/World.h"

using namespace LifeVisuals;

ALifeStation::ALifeStation()
{
	PrimaryActorTick.bCanEverTick = false;
	Root = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
	Root->SetMobility(EComponentMobility::Movable);
	SetRootComponent(Root);
}

void ALifeStation::Initialize(ELifeStationType InType, FName InItemId, const FLinearColor& InAccent)
{
	Type = InType;
	ItemId = InItemId;
	Accent = InAccent;
	BuildVisual();
}

FTransform ALifeStation::GetUseTransform() const
{
	return FTransform(GetActorRotation(), GetActorTransform().TransformPosition(UseOffset));
}

void ALifeStation::BuildVisual()
{
	const FLinearColor Wood = RGB(120, 82, 52);
	const FLinearColor Metal = RGB(170, 175, 182);
	const FLinearColor Dark = RGB(28, 30, 34);
	const FLinearColor White = RGB(235, 235, 230);

	switch (Type)
	{
	case ELifeStationType::Bed:
		// Foot end at +X, pillow at -X. 210 x 150.
		AddPart(this, Root, Cube(), FVector(0, 0, 20), FVector(215, 155, 40), Wood);
		AddPart(this, Root, Cube(), FVector(0, 0, 47), FVector(205, 145, 18), White);
		AddPart(this, Root, Cube(), FVector(25, 0, 58), FVector(150, 148, 6), Accent);
		AddPart(this, Root, Cube(), FVector(-82, 0, 62), FVector(35, 90, 16), White);
		AddPart(this, Root, Cube(), FVector(-110, 0, 60), FVector(10, 155, 120), Wood);
		UseOffset = FVector(85, 0, 58);
		break;

	case ELifeStationType::Shower:
		AddPart(this, Root, Cube(), FVector(0, 0, 4), FVector(110, 110, 8), White);
		AddPart(this, Root, Cube(), FVector(-52, 0, 110), FVector(6, 110, 220), RGB(200, 215, 225));
		AddPart(this, Root, Cube(), FVector(0, -52, 110), FVector(110, 6, 220), RGB(200, 215, 225));
		AddPart(this, Root, Cylinder(), FVector(-40, 0, 205), FVector(4, 4, 30), Metal, FRotator(-60, 0, 0), false);
		AddPart(this, Root, Cylinder(), FVector(-28, 0, 195), FVector(18, 18, 4), Metal, FRotator::ZeroRotator, false);
		UseOffset = FVector(0, 0, 8);
		break;

	case ELifeStationType::Television:
		AddPart(this, Root, Cube(), FVector(0, 0, 25), FVector(50, 180, 50), Wood);
		AddPart(this, Root, Cube(), FVector(0, 0, 105), FVector(6, 160, 92), Dark);
		AddPart(this, Root, Cube(), FVector(3.5f, 0, 105), FVector(1, 150, 82), Accent, FRotator::ZeroRotator, false);
		break;

	case ELifeStationType::Fridge:
		AddPart(this, Root, Cube(), FVector(0, 0, 95), FVector(75, 85, 190), White);
		AddPart(this, Root, Cube(), FVector(39, 30, 120), FVector(4, 4, 50), Metal, FRotator::ZeroRotator, false);
		AddPart(this, Root, Cube(), FVector(38, 0, 70), FVector(2, 84, 2), Metal, FRotator::ZeroRotator, false);
		break;

	case ELifeStationType::ShopItem:
		AddPart(this, Root, Cube(), FVector(0, 0, 50), FVector(90, 110, 100), Wood);
		AddPart(this, Root, Cube(), FVector(0, 0, 102), FVector(95, 115, 4), RGB(220, 220, 215));
		AddPart(this, Root, Sphere(), FVector(0, 0, 122), FVector(36, 36, 36), Accent, FRotator::ZeroRotator, false);
		if (const FLifeShopItem* Item = ULifeSettings::Get()->FindShopItem(ItemId))
		{
			AddLabel(this, Root, FString::Printf(TEXT("%s  $%d"), *Item->DisplayName, Item->Price), FVector(48, 0, 165), 18.f, FColor::White);
		}
		break;

	case ELifeStationType::Workstation:
		AddPart(this, Root, Cube(), FVector(0, 0, 75), FVector(90, 160, 6), Wood);
		AddPart(this, Root, Cube(), FVector(0, -70, 37), FVector(80, 6, 74), Wood);
		AddPart(this, Root, Cube(), FVector(0, 70, 37), FVector(80, 6, 74), Wood);
		AddPart(this, Root, Cube(), FVector(-20, 0, 110), FVector(5, 70, 45), Dark, FRotator::ZeroRotator, false);
		AddPart(this, Root, Cube(), FVector(-17, 0, 110), FVector(1, 64, 39), Accent, FRotator::ZeroRotator, false);
		if (const FLifeJobDef* Job = ULifeSettings::Get()->FindJob(ItemId))
		{
			AddLabel(this, Root, Job->DisplayName, FVector(0, 0, 175), 22.f, FColor::White, FRotator(0, 0, 0));
		}
		break;

	case ELifeStationType::ATMDeposit:
	case ELifeStationType::ATMWithdraw:
		AddPart(this, Root, Cube(), FVector(0, 0, 85), FVector(55, 75, 170), RGB(40, 70, 120));
		AddPart(this, Root, Cube(), FVector(28, 0, 125), FVector(2, 50, 35), RGB(90, 200, 255), FRotator::ZeroRotator, false);
		AddPart(this, Root, Cube(), FVector(32, 0, 95), FVector(12, 50, 4), Metal, FRotator::ZeroRotator, false);
		AddLabel(this, Root, Type == ELifeStationType::ATMDeposit ? TEXT("DEPOSIT") : TEXT("WITHDRAW"), FVector(29, 0, 155), 12.f, FColor::White);
		break;

	case ELifeStationType::Workout:
		AddPart(this, Root, Cube(), FVector(0, 0, 12), FVector(180, 80, 24), Dark);
		AddPart(this, Root, Cube(), FVector(0, 0, 25), FVector(160, 60, 3), RGB(60, 60, 60), FRotator::ZeroRotator, false);
		AddPart(this, Root, Cube(), FVector(80, -35, 70), FVector(8, 8, 110), Metal);
		AddPart(this, Root, Cube(), FVector(80, 35, 70), FVector(8, 8, 110), Metal);
		AddPart(this, Root, Cube(), FVector(85, 0, 125), FVector(25, 80, 10), Accent, FRotator::ZeroRotator, false);
		UseOffset = FVector(-10, 0, 27);
		break;
	}
}

FString ALifeStation::GetInteractionPrompt(const ALifePlayerCharacter* Player) const
{
	const ULifeSettings* S = ULifeSettings::Get();
	switch (Type)
	{
	case ELifeStationType::Bed:        return TEXT("Sleep");
	case ELifeStationType::Shower:     return TEXT("Take a shower");
	case ELifeStationType::Television: return TEXT("Watch TV");
	case ELifeStationType::Fridge:     return FString::Printf(TEXT("Cook a meal  $%d"), S->FridgeMealCost);
	case ELifeStationType::Workout:    return TEXT("Work out");
	case ELifeStationType::ATMDeposit: return FString::Printf(TEXT("Deposit cash  ($%d)"), Player ? Player->GetWallet()->GetCash() : 0);
	case ELifeStationType::ATMWithdraw:return FString::Printf(TEXT("Withdraw $%d"), S->ATMWithdrawAmount);
	case ELifeStationType::ShopItem:
		if (const FLifeShopItem* Item = S->FindShopItem(ItemId))
		{
			return FString::Printf(TEXT("Buy %s  $%d"), *Item->DisplayName, Item->Price);
		}
		return TEXT("Sold out");
	case ELifeStationType::Workstation:
		return Player ? Player->GetCareer()->GetWorkstationPrompt(ItemId) : FString();
	}
	return FString();
}

bool ALifeStation::CanInteract(const ALifePlayerCharacter* Player) const
{
	if (!Player)
	{
		return false;
	}
	switch (Type)
	{
	case ELifeStationType::ShopItem:
		if (const FLifeShopItem* Item = ULifeSettings::Get()->FindShopItem(ItemId))
		{
			return Player->GetWallet()->CanAfford(Item->Price);
		}
		return false;
	case ELifeStationType::Fridge:
		return Player->GetWallet()->CanAfford(ULifeSettings::Get()->FridgeMealCost);
	case ELifeStationType::ATMDeposit:
		return Player->GetWallet()->GetCash() > 0;
	case ELifeStationType::ATMWithdraw:
		return Player->GetWallet()->GetBank() > 0;
	default:
		return true;
	}
}

void ALifeStation::Interact(ALifePlayerCharacter* Player)
{
	if (!Player)
	{
		return;
	}
	const ULifeSettings* S = ULifeSettings::Get();
	ULifeWalletComponent* Wallet = Player->GetWallet();
	ULifeNeedsComponent* Needs = Player->GetNeeds();

	switch (Type)
	{
	case ELifeStationType::Bed:
		Player->BeginActivity(ELifeActivity::Sleep, this);
		break;
	case ELifeStationType::Shower:
		Player->BeginActivity(ELifeActivity::Shower, this);
		break;
	case ELifeStationType::Television:
		Player->BeginActivity(ELifeActivity::WatchTV, this);
		break;
	case ELifeStationType::Workout:
		Player->BeginActivity(ELifeActivity::Workout, this);
		break;

	case ELifeStationType::Fridge:
		if (Wallet->TrySpend(S->FridgeMealCost, TEXT("groceries")))
		{
			Needs->ApplyDelta(S->FridgeMealRestore);
			LifeNotify(this, TEXT("You cooked a home meal. Yum."), FLinearColor(0.9f, 0.95f, 1.f));
		}
		break;

	case ELifeStationType::ShopItem:
		if (const FLifeShopItem* Item = S->FindShopItem(ItemId))
		{
			if (Wallet->TrySpend(Item->Price, Item->DisplayName))
			{
				Needs->ApplyDelta(Item->Restore);
				LifeNotify(this, FString::Printf(TEXT("Bought %s (-$%d)"), *Item->DisplayName, Item->Price), FLinearColor(0.9f, 0.95f, 1.f));
			}
		}
		break;

	case ELifeStationType::Workstation:
		Player->GetCareer()->UseWorkstation(this);
		break;

	case ELifeStationType::ATMDeposit:
	{
		const int32 Moved = Wallet->DepositAll();
		LifeNotify(this, FString::Printf(TEXT("Deposited $%d. Balance $%d."), Moved, Wallet->GetBank()), FLinearColor(0.6f, 0.85f, 1.f));
		break;
	}
	case ELifeStationType::ATMWithdraw:
	{
		const int32 Moved = Wallet->Withdraw(S->ATMWithdrawAmount);
		LifeNotify(this, FString::Printf(TEXT("Withdrew $%d. Balance $%d."), Moved, Wallet->GetBank()), FLinearColor(0.6f, 0.85f, 1.f));
		break;
	}
	}
}
