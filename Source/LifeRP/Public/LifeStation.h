// Life RP - usable furniture and service points.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "LifeInteractable.h"
#include "LifeTypes.h"
#include "LifeStation.generated.h"

/**
 * A bed, shower, TV, fridge, shop counter item, workstation, ATM or gym machine.
 * The behaviour is chosen by Type; ItemId names the shop item or job it serves.
 * Visuals are generated from basic shapes in BuildVisual(); subclass in Blueprint
 * and override nothing but the meshes to re-skin with real art.
 */
UCLASS()
class LIFERP_API ALifeStation : public AActor, public ILifeInteractable
{
	GENERATED_BODY()

public:
	ALifeStation();

	/** Sets the role and generates the matching geometry. Call once after spawning. */
	void Initialize(ELifeStationType InType, FName InItemId, const FLinearColor& InAccent);

	// ILifeInteractable
	virtual FString GetInteractionPrompt(const ALifePlayerCharacter* Player) const override;
	virtual bool CanInteract(const ALifePlayerCharacter* Player) const override;
	virtual void Interact(ALifePlayerCharacter* Player) override;

	ELifeStationType GetStationType() const { return Type; }
	FName GetItemId() const { return ItemId; }

	/** World transform the player is moved to while using this station (beds, showers). */
	FTransform GetUseTransform() const;

protected:
	void BuildVisual();

	UPROPERTY(VisibleAnywhere, Category = "Life") TObjectPtr<USceneComponent> Root;
	UPROPERTY(EditAnywhere, Category = "Life") ELifeStationType Type = ELifeStationType::Bed;
	UPROPERTY(EditAnywhere, Category = "Life") FName ItemId;
	UPROPERTY(EditAnywhere, Category = "Life") FLinearColor Accent = FLinearColor::White;

	/** Use location relative to the station; zero = player stays where they are. */
	UPROPERTY(EditAnywhere, Category = "Life") FVector UseOffset = FVector::ZeroVector;
};
