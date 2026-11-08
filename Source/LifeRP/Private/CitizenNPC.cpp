// Life RP - townsfolk.

#include "CitizenNPC.h"
#include "AIController.h"
#include "LifeGameState.h"
#include "LifeNeedsComponent.h"
#include "LifePlayerCharacter.h"
#include "LifeRules.h"
#include "LifeSettings.h"
#include "LifeTownBuilder.h"
#include "Camera/PlayerCameraManager.h"
#include "Components/CapsuleComponent.h"
#include "Components/TextRenderComponent.h"
#include "Engine/World.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "GameFramework/PlayerController.h"

ACitizenNPC::ACitizenNPC()
{
	AIControllerClass = AAIController::StaticClass();
	AutoPossessAI = EAutoPossessAI::PlacedInWorldOrSpawned;

	SpeechText = CreateDefaultSubobject<UTextRenderComponent>(TEXT("Speech"));
	SpeechText->SetupAttachment(RootComponent);
	SpeechText->SetRelativeLocation(FVector(0.f, 0.f, 130.f));
	SpeechText->SetHorizontalAlignment(EHTA_Center);
	SpeechText->SetVerticalAlignment(EVRTA_TextBottom);
	SpeechText->SetWorldSize(14.f);
	SpeechText->SetTextRenderColor(FColor::White);
	SpeechText->SetHiddenInGame(true);

	GetCharacterMovement()->MaxWalkSpeed = 150.f;
	GetCharacterMovement()->bUseRVOAvoidance = true;
	GetCharacterMovement()->AvoidanceConsiderationRadius = 180.f;
	GetCharacterMovement()->RotationRate = FRotator(0.f, 300.f, 0.f);
}

void ACitizenNPC::InitCitizen(const FString& InName, const FLifeAppearance& InAppearance, ALifeTownBuilder* InTown, int32 StartNode, int32 Seed)
{
	CitizenName = InName;
	Town = InTown;
	Random.Initialize(Seed);
	CurrentNode = StartNode;
	PreviousNode = INDEX_NONE;
	HomeHour = Random.FRandRange(20.5f, 23.5f);
	OutHour = Random.FRandRange(6.f, 9.f);
	GetCharacterMovement()->MaxWalkSpeed = Random.FRandRange(120.f, 175.f);
	ApplyAppearance(InAppearance);
	LastProgressLocation = GetActorLocation();
	PickNextNode();
}

FString ACitizenNPC::GetInteractionPrompt(const ALifePlayerCharacter* Player) const
{
	const int32 Rel = Player ? Player->GetRelationship(CitizenName) : 0;
	return FString::Printf(TEXT("Talk to %s  (%s %d)"), *CitizenName, *LifeRules::RelationshipLabel(Rel), Rel);
}

void ACitizenNPC::Interact(ALifePlayerCharacter* Player)
{
	if (Player)
	{
		Player->TalkTo(this);
	}
}

void ACitizenNPC::Respond(ALifePlayerCharacter* Player, int32 Relationship)
{
	const ULifeSettings* S = ULifeSettings::Get();
	TalkPartner = Player;
	PauseTimer = 4.f;
	GetCharacterMovement()->StopMovementImmediately();

	FString Line;
	if (Player && Player->GetNeeds() && Player->GetNeeds()->GetNeed(ELifeNeed::Hygiene) < 15.f)
	{
		Line = TEXT("Uh... have you showered lately?");
	}
	else if (Relationship >= 80)
	{
		Line = TEXT("My best friend! Always great to see you.");
	}
	else if (S->SmallTalkLines.Num() > 0)
	{
		Line = S->SmallTalkLines[Random.RandRange(0, S->SmallTalkLines.Num() - 1)];
	}
	Say(FString::Printf(TEXT("%s: %s"), *CitizenName, *Line), 4.f);
}

void ACitizenNPC::Say(const FString& Line, float Seconds)
{
	SpeechText->SetText(FText::FromString(Line));
	SpeechText->SetHiddenInGame(false);
	SpeechTimer = Seconds;
}

void ACitizenNPC::PickNextNode()
{
	const ALifeTownBuilder* T = Town.Get();
	if (!T || !T->GetWalkNodes().IsValidIndex(CurrentNode))
	{
		return;
	}
	const TArray<int32>& Links = T->GetWalkLinks(CurrentNode);
	if (Links.Num() == 0)
	{
		return;
	}
	TArray<int32> Choices = Links;
	if (Choices.Num() > 1)
	{
		Choices.Remove(PreviousNode); // don't immediately turn back
	}
	PreviousNode = CurrentNode;
	CurrentNode = Choices[Random.RandRange(0, Choices.Num() - 1)];
}

void ACitizenNPC::UpdateSchedule()
{
	const ALifeGameState* GS = GetWorld()->GetGameState<ALifeGameState>();
	if (!GS)
	{
		return;
	}
	const bool bShouldBeHome = !LifeRules::IsWithinShift(GS->GetHourOfDay(), FMath::FloorToInt(OutHour), FMath::FloorToInt(HomeHour));
	if (bShouldBeHome != bAtHome)
	{
		bAtHome = bShouldBeHome;
		SetActorHiddenInGame(bAtHome);
		SetActorEnableCollision(!bAtHome);
		GetCharacterMovement()->StopMovementImmediately();
	}
}

void ACitizenNPC::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);

	UpdateSchedule();
	if (bAtHome)
	{
		return;
	}

	if (SpeechTimer > 0.f)
	{
		SpeechTimer -= DeltaSeconds;
		if (SpeechTimer <= 0.f)
		{
			SpeechText->SetHiddenInGame(true);
		}
		else if (const APlayerController* PC = GetWorld()->GetFirstPlayerController())
		{
			if (PC->PlayerCameraManager)
			{
				const FVector ToCam = PC->PlayerCameraManager->GetCameraLocation() - SpeechText->GetComponentLocation();
				SpeechText->SetWorldRotation(FRotator(0.f, ToCam.Rotation().Yaw, 0.f));
			}
		}
	}

	if (PauseTimer > 0.f)
	{
		PauseTimer -= DeltaSeconds;
		if (const AActor* Partner = TalkPartner.Get())
		{
			FVector ToPartner = Partner->GetActorLocation() - GetActorLocation();
			ToPartner.Z = 0.f;
			SetActorRotation(FMath::RInterpTo(GetActorRotation(), ToPartner.Rotation(), DeltaSeconds, 6.f));
		}
		return;
	}
	TalkPartner.Reset();

	const ALifeTownBuilder* T = Town.Get();
	if (!T || !T->GetWalkNodes().IsValidIndex(CurrentNode))
	{
		return;
	}

	FVector ToTarget = T->GetWalkNodes()[CurrentNode] - GetActorLocation();
	ToTarget.Z = 0.f;
	if (ToTarget.SizeSquared() < FMath::Square(90.f))
	{
		// Arrived: sometimes linger, then continue.
		if (Random.FRand() < 0.25f)
		{
			PauseTimer = Random.FRandRange(1.5f, 5.f);
		}
		PickNextNode();
		return;
	}
	AddMovementInput(ToTarget.GetSafeNormal(), 1.f);

	// Unstick: if we barely moved for 3 s, choose another direction.
	StuckTimer += DeltaSeconds;
	if (StuckTimer > 3.f)
	{
		if (FVector::DistSquared2D(LastProgressLocation, GetActorLocation()) < FMath::Square(40.f))
		{
			Swap(CurrentNode, PreviousNode);
			if (!T->GetWalkNodes().IsValidIndex(CurrentNode))
			{
				PickNextNode();
			}
		}
		LastProgressLocation = GetActorLocation();
		StuckTimer = 0.f;
	}
}
