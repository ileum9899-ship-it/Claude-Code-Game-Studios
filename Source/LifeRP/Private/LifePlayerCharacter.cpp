// Life RP - the player's resident.

#include "LifePlayerCharacter.h"
#include "CitizenNPC.h"
#include "LifeCareerComponent.h"
#include "LifeGameState.h"
#include "LifeInteractable.h"
#include "LifeInteractionComponent.h"
#include "LifeNeedsComponent.h"
#include "LifePlayerController.h"
#include "LifeRules.h"
#include "LifeSettings.h"
#include "LifeStation.h"
#include "LifeVehicle.h"
#include "LifeWalletComponent.h"
#include "Camera/CameraComponent.h"
#include "Components/CapsuleComponent.h"
#include "EnhancedInputComponent.h"
#include "Engine/World.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "GameFramework/SpringArmComponent.h"
#include "InputActionValue.h"

ALifePlayerCharacter::ALifePlayerCharacter()
{
	CameraBoom = CreateDefaultSubobject<USpringArmComponent>(TEXT("CameraBoom"));
	CameraBoom->SetupAttachment(RootComponent);
	CameraBoom->TargetArmLength = TargetZoom;
	CameraBoom->SocketOffset = FVector(0.f, 45.f, 55.f);
	CameraBoom->bUsePawnControlRotation = true;
	CameraBoom->bEnableCameraLag = true;
	CameraBoom->CameraLagSpeed = 12.f;
	CameraBoom->ProbeSize = 14.f;

	FollowCamera = CreateDefaultSubobject<UCameraComponent>(TEXT("FollowCamera"));
	FollowCamera->SetupAttachment(CameraBoom, USpringArmComponent::SocketName);
	FollowCamera->bUsePawnControlRotation = false;
	FollowCamera->SetFieldOfView(80.f);

	Needs = CreateDefaultSubobject<ULifeNeedsComponent>(TEXT("Needs"));
	Wallet = CreateDefaultSubobject<ULifeWalletComponent>(TEXT("Wallet"));
	Career = CreateDefaultSubobject<ULifeCareerComponent>(TEXT("Career"));
	Interaction = CreateDefaultSubobject<ULifeInteractionComponent>(TEXT("Interaction"));

	Appearance.Shirt = FLinearColor(0.85f, 0.25f, 0.20f);
	Appearance.Pants = FLinearColor(0.10f, 0.14f, 0.28f);
	Appearance.Shoes = FLinearColor(0.92f, 0.92f, 0.92f);
}

void ALifePlayerCharacter::BeginPlay()
{
	Super::BeginPlay();
	Needs->OnNeedCritical.AddDynamic(this, &ALifePlayerCharacter::HandleNeedCritical);
	Needs->OnNeedDepleted.AddDynamic(this, &ALifePlayerCharacter::HandleNeedDepleted);
	UpdateMovementSpeed();
}

void ALifePlayerCharacter::SetupPlayerInputComponent(UInputComponent* PlayerInputComponent)
{
	Super::SetupPlayerInputComponent(PlayerInputComponent);

	ALifePlayerController* PC = Cast<ALifePlayerController>(GetController());
	UEnhancedInputComponent* EIC = Cast<UEnhancedInputComponent>(PlayerInputComponent);
	if (!PC || !EIC)
	{
		UE_LOG(LogTemp, Error, TEXT("LifeRP: Enhanced Input is not active. Check Config/DefaultInput.ini."));
		return;
	}
	const ULifeInputSet* In = PC->GetInputSet();
	EIC->BindAction(In->Move, ETriggerEvent::Triggered, this, &ALifePlayerCharacter::Move);
	EIC->BindAction(In->Look, ETriggerEvent::Triggered, this, &ALifePlayerCharacter::Look);
	EIC->BindAction(In->Zoom, ETriggerEvent::Triggered, this, &ALifePlayerCharacter::Zoom);
	EIC->BindAction(In->Jump, ETriggerEvent::Started, this, &ALifePlayerCharacter::JumpPressed);
	EIC->BindAction(In->Jump, ETriggerEvent::Completed, this, &ACharacter::StopJumping);
	EIC->BindAction(In->Sprint, ETriggerEvent::Started, this, &ALifePlayerCharacter::SprintStart);
	EIC->BindAction(In->Sprint, ETriggerEvent::Completed, this, &ALifePlayerCharacter::SprintStop);
	EIC->BindAction(In->Interact, ETriggerEvent::Started, this, &ALifePlayerCharacter::InteractPressed);
}

// ------------------------------------------------------------------ Input

void ALifePlayerCharacter::Move(const FInputActionValue& Value)
{
	if (IsBusy() || !Controller)
	{
		return;
	}
	const FVector2D Axis = Value.Get<FVector2D>();
	const FRotator YawRotation(0.f, Controller->GetControlRotation().Yaw, 0.f);
	AddMovementInput(FRotationMatrix(YawRotation).GetUnitAxis(EAxis::X), Axis.Y);
	AddMovementInput(FRotationMatrix(YawRotation).GetUnitAxis(EAxis::Y), Axis.X);
}

void ALifePlayerCharacter::Look(const FInputActionValue& Value)
{
	const FVector2D Axis = Value.Get<FVector2D>();
	AddControllerYawInput(Axis.X);
	AddControllerPitchInput(Axis.Y);
}

void ALifePlayerCharacter::Zoom(const FInputActionValue& Value)
{
	TargetZoom = FMath::Clamp(TargetZoom - Value.Get<float>() * 45.f, MinZoom, MaxZoom);
}

void ALifePlayerCharacter::SprintStart()
{
	bSprinting = true;
}

void ALifePlayerCharacter::SprintStop()
{
	bSprinting = false;
}

void ALifePlayerCharacter::JumpPressed()
{
	if (IsBusy())
	{
		EndActivity(TEXT("You stopped."));
		return;
	}
	Jump();
}

void ALifePlayerCharacter::InteractPressed()
{
	if (IsBusy())
	{
		EndActivity(TEXT("You stopped."));
		return;
	}
	AActor* Focus = Interaction->GetFocus();
	ILifeInteractable* Target = Cast<ILifeInteractable>(Focus);
	if (!Target)
	{
		return;
	}
	if (!Target->CanInteract(this))
	{
		LifeNotify(this, FString::Printf(TEXT("Can't: %s"), *Target->GetInteractionPrompt(this)), FLinearColor(1.f, 0.5f, 0.4f));
		return;
	}
	Target->Interact(this);
}

// ------------------------------------------------------------------ Tick

void ALifePlayerCharacter::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);

	CameraBoom->TargetArmLength = FMath::FInterpTo(CameraBoom->TargetArmLength, TargetZoom, DeltaSeconds, 8.f);
	SetBusyAnimation(Career->IsClockedIn() || Activity == ELifeActivity::Workout || Activity == ELifeActivity::Shower);

	if (IsBusy())
	{
		TickActivity(DeltaSeconds);
	}
	else
	{
		UpdateMovementSpeed();
	}
}

void ALifePlayerCharacter::UpdateMovementSpeed()
{
	const ULifeSettings* S = ULifeSettings::Get();
	const bool bMoving = GetVelocity().Size2D() > 50.f;
	const bool bRunning = bSprinting && bMoving && Needs->GetNeed(ELifeNeed::Energy) > 5.f;
	const float Base = bRunning ? S->RunSpeed * (1.f + Fitness * S->RunSpeedPerFitness) : S->WalkSpeed;
	GetCharacterMovement()->MaxWalkSpeed = Base * LifeRules::SpeedMultiplierForEnergy(
		Needs->GetNeed(ELifeNeed::Energy), S->LowEnergyThreshold, S->LowEnergySpeedMultiplier);
	Needs->SetEnergyDecayMultiplier(bRunning ? S->SprintEnergyMultiplier : 1.f);
}

// ------------------------------------------------------------------ Activities

FString ALifePlayerCharacter::GetActivityLabel() const
{
	switch (Activity)
	{
	case ELifeActivity::Sleep:   return TEXT("Sleeping");
	case ELifeActivity::Shower:  return TEXT("Showering");
	case ELifeActivity::WatchTV: return TEXT("Watching TV");
	case ELifeActivity::Workout: return TEXT("Working out");
	default:                     return FString();
	}
}

void ALifePlayerCharacter::BeginActivity(ELifeActivity NewActivity, ALifeStation* Station)
{
	if (IsBusy() || IsDriving() || NewActivity == ELifeActivity::None)
	{
		return;
	}
	const ULifeSettings* S = ULifeSettings::Get();
	ALifeGameState* GS = GetWorld()->GetGameState<ALifeGameState>();
	if (!GS)
	{
		return;
	}

	switch (NewActivity)
	{
	case ELifeActivity::Sleep:
	{
		const float Hour = GS->GetHourOfDay();
		if (Station && Needs->GetNeed(ELifeNeed::Energy) > 80.f && Hour > S->WakeHour + 1.f && Hour < 20.f)
		{
			LifeNotify(this, TEXT("You're not tired yet."), FLinearColor(1.f, 0.85f, 0.4f));
			return;
		}
		ActivityTuning = S->Sleep;
		ActivityTuning.TimeMultiplier = S->SleepTimeMultiplier;
		ActivityRemainingMinutes = LifeRules::MinutesUntilHour(GS->GetMinuteOfDay(), S->WakeHour);
		break;
	}
	case ELifeActivity::Shower:  ActivityTuning = S->Shower;  ActivityRemainingMinutes = S->Shower.DurationMinutes;  break;
	case ELifeActivity::WatchTV: ActivityTuning = S->WatchTV; ActivityRemainingMinutes = S->WatchTV.DurationMinutes; break;
	case ELifeActivity::Workout: ActivityTuning = S->Workout; ActivityRemainingMinutes = S->Workout.DurationMinutes; break;
	default: return;
	}

	if (Career->IsClockedIn())
	{
		Career->ClockOut(TEXT("You stopped working"));
	}

	Activity = NewActivity;
	PreActivityLocation = GetActorLocation();
	GetCharacterMovement()->StopMovementImmediately();
	GetCharacterMovement()->DisableMovement();

	const bool bLie = NewActivity == ELifeActivity::Sleep;
	if (Station && (bLie || NewActivity == ELifeActivity::Shower || NewActivity == ELifeActivity::Workout))
	{
		GetCapsuleComponent()->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		const FTransform Use = Station->GetUseTransform();
		// Lying: the body pivot (feet) sits 77 cm under the capsule centre; standing: 90 cm.
		SetActorLocationAndRotation(Use.GetLocation() + FVector(0.f, 0.f, bLie ? 77.f : 92.f), Use.Rotator());
	}
	else if (Station && NewActivity == ELifeActivity::WatchTV)
	{
		// Face the screen.
		FVector ToTV = Station->GetActorLocation() - GetActorLocation();
		ToTV.Z = 0.f;
		SetActorRotation(ToTV.Rotation());
	}
	SetLyingDown(bLie);

	GS->SetTimeMultiplier(ActivityTuning.TimeMultiplier);
	LifeNotify(this, FString::Printf(TEXT("%s...  (E / Space to stop)"), *GetActivityLabel()), FLinearColor(0.85f, 0.9f, 1.f));
}

void ALifePlayerCharacter::TickActivity(float DeltaSeconds)
{
	const ALifeGameState* GS = GetWorld()->GetGameState<ALifeGameState>();
	if (!GS)
	{
		return;
	}
	const float Hours = GS->RealToGameHours(DeltaSeconds);
	FLifeNeedValues Delta = ActivityTuning.RatePerHour;
	for (ELifeNeed Need : TEnumRange<ELifeNeed>())
	{
		Delta.Set(Need, Delta.Get(Need) * Hours);
	}
	Needs->ApplyDelta(Delta);

	if (Activity == ELifeActivity::Workout)
	{
		Fitness = FMath::Min(10.f, Fitness + ULifeSettings::Get()->FitnessPerWorkoutHour * Hours);
	}

	ActivityRemainingMinutes -= Hours * 60.f;
	const bool bRested = Activity == ELifeActivity::Sleep && Needs->GetNeed(ELifeNeed::Energy) >= 100.f;
	if (ActivityRemainingMinutes <= 0.f || bRested)
	{
		const bool bWasSleep = Activity == ELifeActivity::Sleep;
		EndActivity(bWasSleep ? TEXT("Good morning! You feel rested.") : TEXT("Done."));
		if (bWasSleep && ULifeSettings::Get()->bAutoSaveOnWake)
		{
			if (ALifePlayerController* PC = Cast<ALifePlayerController>(GetController()))
			{
				PC->SaveToSlot();
			}
		}
	}
	else if (Activity == ELifeActivity::Workout && Needs->GetNeed(ELifeNeed::Energy) <= 5.f)
	{
		EndActivity(TEXT("Too exhausted to keep going."));
	}
}

void ALifePlayerCharacter::EndActivity(const FString& Message)
{
	if (!IsBusy())
	{
		return;
	}
	const bool bWasLying = Activity == ELifeActivity::Sleep;
	Activity = ELifeActivity::None;
	ActivityRemainingMinutes = 0.f;

	if (ALifeGameState* GS = GetWorld()->GetGameState<ALifeGameState>())
	{
		GS->SetTimeMultiplier(1.f);
	}
	SetLyingDown(false);
	if (GetCapsuleComponent()->GetCollisionEnabled() == ECollisionEnabled::NoCollision)
	{
		SetActorLocation(PreActivityLocation);
		GetCapsuleComponent()->SetCollisionEnabled(ECollisionEnabled::QueryAndPhysics);
	}
	else if (bWasLying)
	{
		SetActorLocation(GetActorLocation() + FVector(0.f, 0.f, 5.f));
	}
	SetActorRotation(FRotator(0.f, GetActorRotation().Yaw, 0.f));
	GetCharacterMovement()->SetMovementMode(MOVE_Walking);
	LifeNotify(this, Message, FLinearColor(0.85f, 0.9f, 1.f));
}

// ------------------------------------------------------------------ Vehicles

void ALifePlayerCharacter::EnterVehicle(ALifeVehicle* Vehicle)
{
	AController* PC = GetController();
	if (!Vehicle || IsBusy() || IsDriving() || !PC)
	{
		return;
	}
	if (Career->IsClockedIn())
	{
		Career->ClockOut(TEXT("You left work"));
	}
	CurrentVehicle = Vehicle;
	Interaction->ClearFocus();
	GetCharacterMovement()->StopMovementImmediately();
	GetCharacterMovement()->DisableMovement();
	SetActorEnableCollision(false);
	SetActorHiddenInGame(true);
	AttachToActor(Vehicle, FAttachmentTransformRules::SnapToTargetNotIncludingScale);
	Vehicle->SetDriver(this);
	PC->Possess(Vehicle);
}

void ALifePlayerCharacter::ExitVehicle(ALifeVehicle* Vehicle, const FVector& ExitLocation)
{
	if (!Vehicle || CurrentVehicle.Get() != Vehicle)
	{
		return;
	}
	AController* PC = Vehicle->GetController();
	CurrentVehicle.Reset();
	DetachFromActor(FDetachmentTransformRules::KeepWorldTransform);
	SetActorLocationAndRotation(ExitLocation, FRotator(0.f, Vehicle->GetActorRotation().Yaw, 0.f));
	SetActorHiddenInGame(false);
	SetActorEnableCollision(true);
	GetCharacterMovement()->SetMovementMode(MOVE_Walking);
	Vehicle->SetDriver(nullptr);
	if (PC)
	{
		PC->Possess(this);
	}
}

// ------------------------------------------------------------------ Social

int32 ALifePlayerCharacter::GetRelationship(const FString& CitizenName) const
{
	const int32* Found = Relationships.Find(CitizenName);
	return Found ? *Found : 0;
}

void ALifePlayerCharacter::TalkTo(ACitizenNPC* Citizen)
{
	const ALifeGameState* GS = GetWorld()->GetGameState<ALifeGameState>();
	if (!Citizen || !GS)
	{
		return;
	}
	const ULifeSettings* S = ULifeSettings::Get();
	const FString& Name = Citizen->GetCitizenName();
	const int32 Rel = GetRelationship(Name);

	const float Gain = LifeRules::ConversationSocialGain(S->ConversationSocialGain, Rel, Charisma);
	FLifeNeedValues Delta;
	Delta.Social = Gain;
	Delta.Fun = Gain * 0.25f;
	Needs->ApplyDelta(Delta);

	// Relationship and charisma grow at most once per in-game hour per person.
	const float Now = GS->GetDay() * 24.f + GS->GetHourOfDay();
	const float* Last = LastTalkHour.Find(Name);
	if (!Last || Now - *Last >= 1.f)
	{
		LastTalkHour.Add(Name, Now);
		// Being filthy makes a bad impression.
		const int32 Change = Needs->GetNeed(ELifeNeed::Hygiene) < 15.f ? -S->ConversationRelationshipGain / 2 : S->ConversationRelationshipGain;
		const int32 NewRel = FMath::Clamp(Rel + Change, -100, 100);
		Relationships.Add(Name, NewRel);
		Charisma = FMath::Min(10.f, Charisma + S->CharismaPerConversation);
		if (LifeRules::RelationshipLabel(NewRel) != LifeRules::RelationshipLabel(Rel))
		{
			LifeNotify(this, FString::Printf(TEXT("%s is now your %s."), *Name, *LifeRules::RelationshipLabel(NewRel)), FLinearColor(1.f, 0.6f, 0.85f));
		}
	}
	Citizen->Respond(this, GetRelationship(Name));
}

void ALifePlayerCharacter::RestoreState(float InFitness, float InCharisma, const TMap<FString, int32>& InRelationships)
{
	Fitness = InFitness;
	Charisma = InCharisma;
	Relationships = InRelationships;
}

// ------------------------------------------------------------------ Needs feedback

void ALifePlayerCharacter::HandleNeedCritical(ELifeNeed Need)
{
	static const TCHAR* Messages[] = {
		TEXT("You're getting really hungry."), TEXT("You're exhausted. Find a bed."), TEXT("You need a shower."),
		TEXT("You're bored. Do something fun."), TEXT("You feel lonely. Talk to someone.") };
	LifeNotify(this, Messages[static_cast<int32>(Need)], FLinearColor(1.f, 0.7f, 0.3f));
}

void ALifePlayerCharacter::HandleNeedDepleted(ELifeNeed Need)
{
	if (Need == ELifeNeed::Hunger)
	{
		LifeNotify(this, TEXT("You're starving! Your energy drains twice as fast."), FLinearColor(1.f, 0.3f, 0.25f));
	}
	else if (Need == ELifeNeed::Energy && !IsBusy() && !IsDriving())
	{
		const int32 Fee = ULifeSettings::Get()->CollapseFee;
		Wallet->AddBank(-Fee);
		LifeNotify(this, FString::Printf(TEXT("You collapsed from exhaustion! Medical bill: $%d"), Fee), FLinearColor(1.f, 0.3f, 0.25f));
		BeginActivity(ELifeActivity::Sleep, nullptr);
	}
}
