// Life RP - player controller, input and save/load.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/PlayerController.h"
#include "LifePlayerController.generated.h"

class UInputAction;
class UInputMappingContext;
class ALifePlayerCharacter;

/**
 * Input actions and mapping contexts, built in code so the project needs no
 * input assets. Keyboard/mouse and gamepad are both mapped. To let players
 * rebind, replace these with assets and register them as player-mappable.
 */
UCLASS()
class LIFERP_API ULifeInputSet : public UObject
{
	GENERATED_BODY()

public:
	void Build();

	/** Always active: menus, save/load. */
	UPROPERTY() TObjectPtr<UInputMappingContext> Global;
	/** Walking around. */
	UPROPERTY() TObjectPtr<UInputMappingContext> OnFoot;
	/** Driving a car. */
	UPROPERTY() TObjectPtr<UInputMappingContext> Driving;

	UPROPERTY() TObjectPtr<UInputAction> Move;
	UPROPERTY() TObjectPtr<UInputAction> Look;
	UPROPERTY() TObjectPtr<UInputAction> Zoom;
	UPROPERTY() TObjectPtr<UInputAction> Jump;
	UPROPERTY() TObjectPtr<UInputAction> Sprint;
	UPROPERTY() TObjectPtr<UInputAction> Interact;
	UPROPERTY() TObjectPtr<UInputAction> ToggleStatus;
	UPROPERTY() TObjectPtr<UInputAction> SaveAction;
	UPROPERTY() TObjectPtr<UInputAction> LoadAction;
	UPROPERTY() TObjectPtr<UInputAction> Throttle;
	UPROPERTY() TObjectPtr<UInputAction> Steer;
	UPROPERTY() TObjectPtr<UInputAction> Handbrake;
	UPROPERTY() TObjectPtr<UInputAction> ExitVehicle;
};

/** Owns the input set, swaps contexts between walking and driving, and handles save/load. */
UCLASS()
class LIFERP_API ALifePlayerController : public APlayerController
{
	GENERATED_BODY()

public:
	/** Lazily built input set (pawns bind to it in SetupPlayerInputComponent). */
	ULifeInputSet* GetInputSet();

	/** The resident, even while they are driving. */
	UFUNCTION(BlueprintPure, Category = "Life") ALifePlayerCharacter* GetLifeCharacter() const { return LifeCharacter.Get(); }

	UFUNCTION(BlueprintPure, Category = "Life") bool IsStatusOpen() const { return bStatusOpen; }

	UFUNCTION(BlueprintCallable, Category = "Life") void SaveToSlot();
	UFUNCTION(BlueprintCallable, Category = "Life") bool LoadFromSlot();

protected:
	virtual void BeginPlay() override;
	virtual void SetupInputComponent() override;
	virtual void OnPossess(APawn* InPawn) override;

private:
	void ApplyContexts();
	void ToggleStatus() { bStatusOpen = !bStatusOpen; }
	void SavePressed() { SaveToSlot(); }
	void LoadPressed() { LoadFromSlot(); }

	UPROPERTY() TObjectPtr<ULifeInputSet> InputSet;
	TWeakObjectPtr<ALifePlayerCharacter> LifeCharacter;
	bool bStatusOpen = false;
};

/** Shows a toast on the local player's HUD (and logs it). Safe to call from anything with a world. */
LIFERP_API void LifeNotify(const UObject* WorldContext, const FString& Message, const FLinearColor& Color = FLinearColor::White);
