// Life RP - interaction contract.

#pragma once

#include "CoreMinimal.h"
#include "UObject/Interface.h"
#include "LifeInteractable.generated.h"

class ALifePlayerCharacter;

UINTERFACE(MinimalAPI, meta = (CannotImplementInterfaceInBlueprint))
class ULifeInteractable : public UInterface
{
	GENERATED_BODY()
};

/**
 * Anything the player can use with the Interact key: stations, NPCs, cars.
 * The player's ULifeInteractionComponent picks the best candidate in front of
 * the camera and shows GetInteractionPrompt() on the HUD.
 */
class LIFERP_API ILifeInteractable
{
	GENERATED_BODY()

public:
	/** Short verb phrase shown after "[E]", e.g. "Buy Coffee  $4". */
	virtual FString GetInteractionPrompt(const ALifePlayerCharacter* Player) const = 0;

	/** Whether the prompt is currently usable (greyed out otherwise). */
	virtual bool CanInteract(const ALifePlayerCharacter* Player) const { return true; }

	/** Performs the interaction. */
	virtual void Interact(ALifePlayerCharacter* Player) = 0;
};
