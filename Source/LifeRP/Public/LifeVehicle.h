// Life RP - drivable car and traffic.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Pawn.h"
#include "LifeInteractable.h"
#include "LifeVehicle.generated.h"

class UBoxComponent;
class UCameraComponent;
class USpringArmComponent;
class USpotLightComponent;
class UPointLightComponent;
class UStaticMesh;
class UStaticMeshComponent;
class ALifePlayerCharacter;
struct FInputActionValue;

/**
 * Arcade car with kinematic, swept movement and ground following (no physics
 * asset needed). The same class is the player's car and AI traffic that loops a
 * lane route and brakes for anything in front of it.
 */
UCLASS()
class LIFERP_API ALifeVehicle : public APawn, public ILifeInteractable
{
	GENERATED_BODY()

public:
	ALifeVehicle();

	virtual void Tick(float DeltaSeconds) override;
	virtual void SetupPlayerInputComponent(UInputComponent* PlayerInputComponent) override;

	/** Body colour. */
	void SetPaint(const FLinearColor& Color);

	/** Turns this car into AI traffic following Route (looped). */
	void SetupAsTraffic(const TArray<FVector>& Route, int32 StartIndex);

	/** Seats (or clears) the player driver; releases the parking brake on entry. */
	void SetDriver(ALifePlayerCharacter* InDriver) { Driver = InDriver; bHandbrake = false; Throttle = 0.f; Steer = 0.f; }

	UFUNCTION(BlueprintPure, Category = "Life|Vehicle") float GetSpeedKmh() const { return FMath::Abs(Speed) * 0.036f; }
	UFUNCTION(BlueprintPure, Category = "Life|Vehicle") bool IsTraffic() const { return bTraffic; }

	// Touch buttons (HUD)
	void SetTouchThrottle(float Value) { Throttle = FMath::Clamp(Value, -1.f, 1.f); }
	void SetTouchHandbrake(bool bOn) { bHandbrake = bOn; }
	/** Same as pressing E while driving. */
	void RequestExit() { ExitPressed(); }

	// ILifeInteractable
	virtual FString GetInteractionPrompt(const ALifePlayerCharacter* Player) const override;
	virtual bool CanInteract(const ALifePlayerCharacter* Player) const override;
	virtual void Interact(ALifePlayerCharacter* Player) override;

protected:
	virtual void BeginPlay() override;

private:
	void ThrottleInput(const FInputActionValue& Value);
	void SteerInput(const FInputActionValue& Value);
	void HandbrakeStart() { bHandbrake = true; }
	void HandbrakeStop() { bHandbrake = false; }
	void Look(const FInputActionValue& Value);
	void ExitPressed();

	void DriveTraffic(float DeltaSeconds);
	void Simulate(float DeltaSeconds);
	void FollowGround(float DeltaSeconds);
	void UpdateVisuals(float DeltaSeconds);

	UStaticMeshComponent* Part(const TCHAR* Name, USceneComponent* Parent, UStaticMesh* Mesh, const FVector& Loc, const FVector& Size, const FRotator& Rot = FRotator::ZeroRotator);

	UPROPERTY(VisibleAnywhere, Category = "Life|Vehicle") TObjectPtr<UBoxComponent> Collision;
	UPROPERTY(VisibleAnywhere, Category = "Life|Vehicle") TObjectPtr<USceneComponent> BodyPivot;
	UPROPERTY(VisibleAnywhere, Category = "Life|Vehicle") TObjectPtr<USpringArmComponent> CameraBoom;
	UPROPERTY(VisibleAnywhere, Category = "Life|Vehicle") TObjectPtr<UCameraComponent> Camera;
	UPROPERTY(VisibleAnywhere, Category = "Life|Vehicle") TObjectPtr<USpotLightComponent> HeadlightL;
	UPROPERTY(VisibleAnywhere, Category = "Life|Vehicle") TObjectPtr<USpotLightComponent> HeadlightR;
	UPROPERTY(VisibleAnywhere, Category = "Life|Vehicle") TObjectPtr<UPointLightComponent> TailGlow;

	UPROPERTY() TArray<TObjectPtr<USceneComponent>> WheelSteer;   // front-left, front-right, rear-left, rear-right
	UPROPERTY() TArray<TObjectPtr<USceneComponent>> WheelSpin;
	UPROPERTY() TArray<TObjectPtr<UStaticMeshComponent>> PaintParts;
	UPROPERTY() TArray<TObjectPtr<UStaticMeshComponent>> GlassParts;
	UPROPERTY() TArray<TObjectPtr<UStaticMeshComponent>> TrimParts;
	UPROPERTY() TArray<TObjectPtr<UStaticMeshComponent>> RubberParts;
	UPROPERTY() TArray<TObjectPtr<UStaticMeshComponent>> HeadlightParts;
	UPROPERTY() TArray<TObjectPtr<UStaticMeshComponent>> TaillightParts;

	UPROPERTY(EditAnywhere, Category = "Life|Vehicle") FLinearColor Paint = FLinearColor(0.7f, 0.05f, 0.05f);

	/** Height of the actor origin above the road surface, cm. */
	UPROPERTY(EditAnywhere, Category = "Life|Vehicle") float RideHeight = 85.f;
	UPROPERTY(EditAnywhere, Category = "Life|Vehicle") float WheelRadius = 34.f;

	TWeakObjectPtr<ALifePlayerCharacter> Driver;
	TArray<FVector> TrafficRoute;
	int32 RouteIndex = 0;
	bool bTraffic = false;

	float Speed = 0.f;        // cm/s along forward, negative = reverse
	float Throttle = 0.f;     // -1..1
	float Steer = 0.f;        // -1..1
	float SteerVisual = 0.f;  // degrees
	float WheelRoll = 0.f;    // degrees
	bool bHandbrake = false;
};
