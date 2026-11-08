// Life RP - player controller, input and save/load.

#include "LifePlayerController.h"
#include "LifeCareerComponent.h"
#include "LifeGameState.h"
#include "LifeHUD.h"
#include "LifeNeedsComponent.h"
#include "LifePlayerCharacter.h"
#include "LifeRP.h"
#include "LifeSaveGame.h"
#include "LifeSettings.h"
#include "LifeVehicle.h"
#include "LifeWalletComponent.h"
#include "EnhancedInputComponent.h"
#include "EnhancedInputSubsystems.h"
#include "Engine/LocalPlayer.h"
#include "Engine/World.h"
#include "InputAction.h"
#include "InputMappingContext.h"
#include "InputModifiers.h"
#include "Kismet/GameplayStatics.h"

// ------------------------------------------------------------------ Input set

namespace
{
	UInputAction* MakeAction(UObject* Outer, const TCHAR* Name, EInputActionValueType Type)
	{
		UInputAction* Action = NewObject<UInputAction>(Outer, Name);
		Action->ValueType = Type;
		return Action;
	}

	enum class EMod : uint8 { Plain = 0, Negate = 1, Swizzle = 2, NegateY = 4 };
	ENUM_CLASS_FLAGS(EMod);

	void Map(UInputMappingContext* Context, UInputAction* Action, const FKey& Key, EMod Mods = EMod::Plain)
	{
		FEnhancedActionKeyMapping& Mapping = Context->MapKey(Action, Key);
		if (EnumHasAnyFlags(Mods, EMod::Swizzle))
		{
			UInputModifierSwizzleAxis* Swizzle = NewObject<UInputModifierSwizzleAxis>(Context);
			Swizzle->Order = EInputAxisSwizzle::YXZ;
			Mapping.Modifiers.Add(Swizzle);
		}
		if (EnumHasAnyFlags(Mods, EMod::Negate))
		{
			Mapping.Modifiers.Add(NewObject<UInputModifierNegate>(Context));
		}
		if (EnumHasAnyFlags(Mods, EMod::NegateY))
		{
			UInputModifierNegate* Negate = NewObject<UInputModifierNegate>(Context);
			Negate->bX = false;
			Negate->bY = true;
			Negate->bZ = false;
			Mapping.Modifiers.Add(Negate);
		}
	}
}

void ULifeInputSet::Build()
{
	Move = MakeAction(this, TEXT("IA_Move"), EInputActionValueType::Axis2D);
	Look = MakeAction(this, TEXT("IA_Look"), EInputActionValueType::Axis2D);
	Zoom = MakeAction(this, TEXT("IA_Zoom"), EInputActionValueType::Axis1D);
	Jump = MakeAction(this, TEXT("IA_Jump"), EInputActionValueType::Boolean);
	Sprint = MakeAction(this, TEXT("IA_Sprint"), EInputActionValueType::Boolean);
	Interact = MakeAction(this, TEXT("IA_Interact"), EInputActionValueType::Boolean);
	ToggleStatus = MakeAction(this, TEXT("IA_ToggleStatus"), EInputActionValueType::Boolean);
	SaveAction = MakeAction(this, TEXT("IA_Save"), EInputActionValueType::Boolean);
	LoadAction = MakeAction(this, TEXT("IA_Load"), EInputActionValueType::Boolean);
	Throttle = MakeAction(this, TEXT("IA_Throttle"), EInputActionValueType::Axis1D);
	Steer = MakeAction(this, TEXT("IA_Steer"), EInputActionValueType::Axis1D);
	Handbrake = MakeAction(this, TEXT("IA_Handbrake"), EInputActionValueType::Boolean);
	ExitVehicle = MakeAction(this, TEXT("IA_ExitVehicle"), EInputActionValueType::Boolean);

	Global = NewObject<UInputMappingContext>(this, TEXT("IMC_Global"));
	Map(Global, ToggleStatus, EKeys::Tab);
	Map(Global, ToggleStatus, EKeys::Gamepad_Special_Left);
	Map(Global, SaveAction, EKeys::F5);
	Map(Global, LoadAction, EKeys::F9);

	OnFoot = NewObject<UInputMappingContext>(this, TEXT("IMC_OnFoot"));
	Map(OnFoot, Move, EKeys::W, EMod::Swizzle);
	Map(OnFoot, Move, EKeys::S, EMod::Swizzle | EMod::Negate);
	Map(OnFoot, Move, EKeys::D);
	Map(OnFoot, Move, EKeys::A, EMod::Negate);
	Map(OnFoot, Move, EKeys::Gamepad_Left2D);
	Map(OnFoot, Look, EKeys::Mouse2D, EMod::NegateY);
	Map(OnFoot, Look, EKeys::Gamepad_Right2D, EMod::NegateY);
	Map(OnFoot, Zoom, EKeys::MouseWheelAxis);
	Map(OnFoot, Jump, EKeys::SpaceBar);
	Map(OnFoot, Jump, EKeys::Gamepad_FaceButton_Bottom);
	Map(OnFoot, Sprint, EKeys::LeftShift);
	Map(OnFoot, Sprint, EKeys::Gamepad_LeftThumbstick);
	Map(OnFoot, Interact, EKeys::E);
	Map(OnFoot, Interact, EKeys::Gamepad_FaceButton_Left);

	Driving = NewObject<UInputMappingContext>(this, TEXT("IMC_Driving"));
	Map(Driving, Throttle, EKeys::W);
	Map(Driving, Throttle, EKeys::S, EMod::Negate);
	Map(Driving, Throttle, EKeys::Gamepad_RightTriggerAxis);
	Map(Driving, Throttle, EKeys::Gamepad_LeftTriggerAxis, EMod::Negate);
	Map(Driving, Steer, EKeys::D);
	Map(Driving, Steer, EKeys::A, EMod::Negate);
	Map(Driving, Steer, EKeys::Gamepad_LeftX);
	Map(Driving, Handbrake, EKeys::SpaceBar);
	Map(Driving, Handbrake, EKeys::Gamepad_FaceButton_Right);
	Map(Driving, ExitVehicle, EKeys::E);
	Map(Driving, ExitVehicle, EKeys::Gamepad_FaceButton_Top);
	Map(Driving, Look, EKeys::Mouse2D, EMod::NegateY);
	Map(Driving, Look, EKeys::Gamepad_Right2D, EMod::NegateY);
}

// ------------------------------------------------------------------ Controller

ULifeInputSet* ALifePlayerController::GetInputSet()
{
	if (!InputSet)
	{
		InputSet = NewObject<ULifeInputSet>(this, TEXT("LifeInputSet"));
		InputSet->Build();
	}
	return InputSet;
}

void ALifePlayerController::BeginPlay()
{
	Super::BeginPlay();
	ApplyContexts();

	const ULifeSettings* S = ULifeSettings::Get();
	if (S->bAutoLoadOnStart && UGameplayStatics::DoesSaveGameExist(S->SaveSlotName, 0))
	{
		if (LoadFromSlot())
		{
			LifeNotify(this, TEXT("다시 오신 것을 환영합니다!"), FLinearColor(0.6f, 1.f, 0.7f));
		}
	}
	else
	{
		LifeNotify(this, TEXT("마을에 오신 것을 환영합니다! 일자리를 구하고, 욕구를 챙기고, 친구를 사귀세요."), FLinearColor(0.6f, 1.f, 0.7f));
		LifeNotify(this, TEXT("TAB 키로 상태와 조작법을 볼 수 있습니다."), FLinearColor(0.85f, 0.9f, 1.f));
	}
}

void ALifePlayerController::SetupInputComponent()
{
	Super::SetupInputComponent();
	if (UEnhancedInputComponent* EIC = Cast<UEnhancedInputComponent>(InputComponent))
	{
		const ULifeInputSet* In = GetInputSet();
		EIC->BindAction(In->ToggleStatus, ETriggerEvent::Started, this, &ALifePlayerController::ToggleStatus);
		EIC->BindAction(In->SaveAction, ETriggerEvent::Started, this, &ALifePlayerController::SavePressed);
		EIC->BindAction(In->LoadAction, ETriggerEvent::Started, this, &ALifePlayerController::LoadPressed);
	}
}

void ALifePlayerController::OnPossess(APawn* InPawn)
{
	Super::OnPossess(InPawn);
	if (ALifePlayerCharacter* AsCharacter = Cast<ALifePlayerCharacter>(InPawn))
	{
		LifeCharacter = AsCharacter;
	}
	if (InPawn)
	{
		SetControlRotation(FRotator(-12.f, InPawn->GetActorRotation().Yaw, 0.f));
	}
	ApplyContexts();
}

void ALifePlayerController::ApplyContexts()
{
	ULocalPlayer* LP = GetLocalPlayer();
	UEnhancedInputLocalPlayerSubsystem* Subsystem = LP ? LP->GetSubsystem<UEnhancedInputLocalPlayerSubsystem>() : nullptr;
	if (!Subsystem)
	{
		return;
	}
	const ULifeInputSet* In = GetInputSet();
	const bool bDriving = Cast<ALifeVehicle>(GetPawn()) != nullptr;
	Subsystem->ClearAllMappings();
	Subsystem->AddMappingContext(In->Global, 0);
	Subsystem->AddMappingContext(bDriving ? In->Driving : In->OnFoot, 1);
}

// ------------------------------------------------------------------ Save / load

void ALifePlayerController::SaveToSlot()
{
	ALifePlayerCharacter* C = LifeCharacter.Get();
	const ALifeGameState* GS = GetWorld()->GetGameState<ALifeGameState>();
	if (!C || !GS)
	{
		return;
	}
	ULifeSaveGame* Save = Cast<ULifeSaveGame>(UGameplayStatics::CreateSaveGameObject(ULifeSaveGame::StaticClass()));
	Save->Day = GS->GetDay();
	Save->MinuteOfDay = GS->GetMinuteOfDay();
	Save->PlayerLocation = C->IsDriving() && C->GetVehicle() ? C->GetVehicle()->GetActorLocation() + FVector(0, 0, 60) : C->GetActorLocation();
	Save->PlayerYaw = C->GetActorRotation().Yaw;
	Save->Needs = C->GetNeeds()->GetNeeds();
	Save->Cash = C->GetWallet()->GetCash();
	Save->Bank = C->GetWallet()->GetBank();
	Save->JobId = C->GetCareer()->GetJobId();
	Save->JobHours = C->GetCareer()->GetTotalHours();
	Save->Fitness = C->GetFitness();
	Save->Charisma = C->GetCharisma();
	Save->Relationships = C->GetRelationships();

	if (UGameplayStatics::SaveGameToSlot(Save, ULifeSettings::Get()->SaveSlotName, 0))
	{
		LifeNotify(this, TEXT("게임을 저장했습니다."), FLinearColor(0.6f, 1.f, 0.7f));
	}
}

bool ALifePlayerController::LoadFromSlot()
{
	ALifePlayerCharacter* C = LifeCharacter.Get();
	ALifeGameState* GS = GetWorld()->GetGameState<ALifeGameState>();
	const FString& Slot = ULifeSettings::Get()->SaveSlotName;
	if (!C || !GS || !UGameplayStatics::DoesSaveGameExist(Slot, 0))
	{
		LifeNotify(this, TEXT("저장 파일이 없습니다."), FLinearColor(1.f, 0.6f, 0.5f));
		return false;
	}
	const ULifeSaveGame* Save = Cast<ULifeSaveGame>(UGameplayStatics::LoadGameFromSlot(Slot, 0));
	if (!Save)
	{
		return false;
	}
	if (C->IsBusy())
	{
		C->EndActivity(TEXT("불러오는 중..."));
	}
	if (C->IsDriving())
	{
		C->ExitVehicle(C->GetVehicle(), C->GetVehicle()->GetActorLocation() + FVector(0, 0, 150));
	}
	GS->SetClock(Save->Day, Save->MinuteOfDay);
	C->SetActorLocationAndRotation(Save->PlayerLocation, FRotator(0.f, Save->PlayerYaw, 0.f), false, nullptr, ETeleportType::TeleportPhysics);
	C->GetNeeds()->SetNeeds(Save->Needs);
	C->GetWallet()->SetBalances(Save->Cash, Save->Bank);
	C->GetCareer()->SetCareer(Save->JobId, Save->JobHours);
	C->RestoreState(Save->Fitness, Save->Charisma, Save->Relationships);
	LifeNotify(this, TEXT("게임을 불러왔습니다."), FLinearColor(0.6f, 1.f, 0.7f));
	return true;
}

// ------------------------------------------------------------------ Notify

void LifeNotify(const UObject* WorldContext, const FString& Message, const FLinearColor& Color)
{
	UE_LOG(LogLifeRP, Log, TEXT("%s"), *Message);
	const UWorld* World = WorldContext ? WorldContext->GetWorld() : nullptr;
	APlayerController* PC = World ? World->GetFirstPlayerController() : nullptr;
	if (ALifeHUD* HUD = PC ? Cast<ALifeHUD>(PC->GetHUD()) : nullptr)
	{
		HUD->PushNotification(Message, Color);
	}
}
