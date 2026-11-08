// Life RP - shared humanoid body.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Character.h"
#include "LifeCharacterBase.generated.h"

class UStaticMesh;
class UStaticMeshComponent;

/** Colours that make a resident recognisable. */
USTRUCT(BlueprintType)
struct LIFERP_API FLifeAppearance
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Appearance") FLinearColor Skin = FLinearColor(0.80f, 0.58f, 0.45f);
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Appearance") FLinearColor Hair = FLinearColor(0.10f, 0.06f, 0.04f);
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Appearance") FLinearColor Shirt = FLinearColor(0.15f, 0.35f, 0.75f);
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Appearance") FLinearColor Pants = FLinearColor(0.12f, 0.12f, 0.16f);
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Appearance") FLinearColor Shoes = FLinearColor(0.05f, 0.05f, 0.05f);
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Appearance") float HeightScale = 1.f;

	/** Random but plausible appearance from a seeded stream. */
	static FLifeAppearance Random(FRandomStream& Stream);
};

/**
 * Humanoid built from primitive shapes with a procedural walk cycle (limbs swing
 * from shoulder/hip pivots, scaled by ground speed). The skeletal Mesh inherited
 * from ACharacter is left empty: assign a skeletal mesh + AnimBP to it in a
 * Blueprint subclass and call SetProceduralBodyVisible(false) to use real art.
 */
UCLASS(Abstract)
class LIFERP_API ALifeCharacterBase : public ACharacter
{
	GENERATED_BODY()

public:
	ALifeCharacterBase();

	virtual void Tick(float DeltaSeconds) override;

	/** Applies colours to the body parts. */
	UFUNCTION(BlueprintCallable, Category = "Life|Body") void ApplyAppearance(const FLifeAppearance& InAppearance);

	UFUNCTION(BlueprintCallable, Category = "Life|Body") void SetProceduralBodyVisible(bool bVisible);

	/** Lie down (sleeping) or stand. */
	UFUNCTION(BlueprintCallable, Category = "Life|Body") void SetLyingDown(bool bLying);

	/** Plays a short "working / using" arm motion while true. */
	void SetBusyAnimation(bool bBusy) { bBusyAnim = bBusy; }

	const FLifeAppearance& GetAppearance() const { return Appearance; }

protected:
	virtual void BeginPlay() override;

	UStaticMeshComponent* MakePart(const TCHAR* Name, USceneComponent* Parent, UStaticMesh* Mesh,
		const FVector& Location, const FVector& Size, const FRotator& Rotation = FRotator::ZeroRotator);

	UPROPERTY(VisibleAnywhere, Category = "Life|Body") TObjectPtr<USceneComponent> BodyRoot;
	UPROPERTY(VisibleAnywhere, Category = "Life|Body") TObjectPtr<USceneComponent> HipL;
	UPROPERTY(VisibleAnywhere, Category = "Life|Body") TObjectPtr<USceneComponent> HipR;
	UPROPERTY(VisibleAnywhere, Category = "Life|Body") TObjectPtr<USceneComponent> ShoulderL;
	UPROPERTY(VisibleAnywhere, Category = "Life|Body") TObjectPtr<USceneComponent> ShoulderR;
	UPROPERTY(VisibleAnywhere, Category = "Life|Body") TObjectPtr<USceneComponent> Torso;

	UPROPERTY() TArray<TObjectPtr<UStaticMeshComponent>> SkinParts;
	UPROPERTY() TArray<TObjectPtr<UStaticMeshComponent>> HairParts;
	UPROPERTY() TArray<TObjectPtr<UStaticMeshComponent>> ShirtParts;
	UPROPERTY() TArray<TObjectPtr<UStaticMeshComponent>> PantsParts;
	UPROPERTY() TArray<TObjectPtr<UStaticMeshComponent>> ShoeParts;
	UPROPERTY() TArray<TObjectPtr<UStaticMeshComponent>> DetailParts;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Life|Body") FLifeAppearance Appearance;

private:
	float WalkPhase = 0.f;
	float IdleTime = 0.f;
	bool bLyingDown = false;
	bool bBusyAnim = false;
};
