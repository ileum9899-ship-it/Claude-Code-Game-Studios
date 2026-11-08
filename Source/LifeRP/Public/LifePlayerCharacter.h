// Life RP - the player's resident.

#pragma once

#include "CoreMinimal.h"
#include "LifeCharacterBase.h"
#include "LifeTypes.h"
#include "LifePlayerCharacter.generated.h"

class UCameraComponent;
class USpringArmComponent;
class ULifeNeedsComponent;
class ULifeWalletComponent;
class ULifeCareerComponent;
class ULifeInteractionComponent;
class ALifeStation;
class ALifeVehicle;
class ACitizenNPC;
struct FInputActionValue;

/**
 * Third-person resident: walks, runs, talks, works, sleeps, drives.
 * Owns needs, money, career and relationships; the HUD reads everything from here.
 */
UCLASS()
class LIFERP_API ALifePlayerCharacter : public ALifeCharacterBase
{
	GENERATED_BODY()

public:
	ALifePlayerCharacter();

	virtual void Tick(float DeltaSeconds) override;
	virtual void SetupPlayerInputComponent(UInputComponent* PlayerInputComponent) override;

	ULifeNeedsComponent* GetNeeds() const { return Needs; }
	ULifeWalletComponent* GetWallet() const { return Wallet; }
	ULifeCareerComponent* GetCareer() const { return Career; }
	ULifeInteractionComponent* GetInteraction() const { return Interaction; }

	// ---------------------------------------------------------- Activities
	/** Starts a timed activity (sleep, shower, TV, workout). Station may be null (collapse). */
	void BeginActivity(ELifeActivity Activity, ALifeStation* Station);

	/** Stops the current activity. */
	void EndActivity(const FString& Message);

	UFUNCTION(BlueprintPure, Category = "Life") ELifeActivity GetActivity() const { return Activity; }
	UFUNCTION(BlueprintPure, Category = "Life") bool IsBusy() const { return Activity != ELifeActivity::None; }
	UFUNCTION(BlueprintPure, Category = "Life") FString GetActivityLabel() const;
	UFUNCTION(BlueprintPure, Category = "Life") float GetActivityRemainingMinutes() const { return ActivityRemainingMinutes; }

	// ---------------------------------------------------------- Vehicles
	void EnterVehicle(ALifeVehicle* Vehicle);
	void ExitVehicle(ALifeVehicle* Vehicle, const FVector& ExitLocation);
	UFUNCTION(BlueprintPure, Category = "Life") bool IsDriving() const { return CurrentVehicle.IsValid(); }
	ALifeVehicle* GetVehicle() const { return CurrentVehicle.Get(); }

	// ---------------------------------------------------------- Social
	/** Chat with a citizen: social need, relationship and charisma. */
	void TalkTo(ACitizenNPC* Citizen);
	UFUNCTION(BlueprintPure, Category = "Life") int32 GetRelationship(const FString& CitizenName) const;
	const TMap<FString, int32>& GetRelationships() const { return Relationships; }

	// ---------------------------------------------------------- Skills
	UFUNCTION(BlueprintPure, Category = "Life") float GetFitness() const { return Fitness; }
	UFUNCTION(BlueprintPure, Category = "Life") float GetCharisma() const { return Charisma; }

	// ---------------------------------------------------------- Touch buttons (HUD)
	/** Same as pressing E: interact, or stop the current activity. */
	void InteractPressed();
	/** Same as pressing Space: jump, or stop the current activity. */
	void JumpPressed();
	void SetSprintHeld(bool bHeld) { bSprinting = bHeld; }
	bool IsSprintHeld() const { return bSprinting; }

	/** Restores progress from a save. */
	void RestoreState(float InFitness, float InCharisma, const TMap<FString, int32>& InRelationships);

protected:
	virtual void BeginPlay() override;

private:
	void Move(const FInputActionValue& Value);
	void Look(const FInputActionValue& Value);
	void Zoom(const FInputActionValue& Value);
	void SprintStart();
	void SprintStop();

	void TickActivity(float DeltaSeconds);
	void UpdateMovementSpeed();

	UFUNCTION() void HandleNeedCritical(ELifeNeed Need);
	UFUNCTION() void HandleNeedDepleted(ELifeNeed Need);

	UPROPERTY(VisibleAnywhere, Category = "Life|Camera") TObjectPtr<USpringArmComponent> CameraBoom;
	UPROPERTY(VisibleAnywhere, Category = "Life|Camera") TObjectPtr<UCameraComponent> FollowCamera;
	UPROPERTY(VisibleAnywhere, Category = "Life") TObjectPtr<ULifeNeedsComponent> Needs;
	UPROPERTY(VisibleAnywhere, Category = "Life") TObjectPtr<ULifeWalletComponent> Wallet;
	UPROPERTY(VisibleAnywhere, Category = "Life") TObjectPtr<ULifeCareerComponent> Career;
	UPROPERTY(VisibleAnywhere, Category = "Life") TObjectPtr<ULifeInteractionComponent> Interaction;

	UPROPERTY(EditAnywhere, Category = "Life|Camera") float MinZoom = 160.f;
	UPROPERTY(EditAnywhere, Category = "Life|Camera") float MaxZoom = 750.f;

	UPROPERTY(VisibleInstanceOnly, Category = "Life") TMap<FString, int32> Relationships;
	/** Absolute game hour of the last chat that changed a relationship, per citizen. */
	TMap<FString, float> LastTalkHour;

	UPROPERTY(VisibleInstanceOnly, Category = "Life") float Fitness = 0.f;
	UPROPERTY(VisibleInstanceOnly, Category = "Life") float Charisma = 0.f;

	ELifeActivity Activity = ELifeActivity::None;
	float ActivityRemainingMinutes = 0.f;
	FLifeActivityTuning ActivityTuning;
	FVector PreActivityLocation = FVector::ZeroVector;
	bool bSprinting = false;
	float TargetZoom = 420.f;

	TWeakObjectPtr<ALifeVehicle> CurrentVehicle;
};
