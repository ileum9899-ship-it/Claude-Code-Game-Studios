// Life RP - townsfolk.

#pragma once

#include "CoreMinimal.h"
#include "LifeCharacterBase.h"
#include "LifeInteractable.h"
#include "CitizenNPC.generated.h"

class UTextRenderComponent;
class ALifeTownBuilder;

/**
 * A resident who walks the sidewalk graph, goes home at night, and chats with the
 * player. Relationship values live on the player, keyed by citizen name.
 */
UCLASS()
class LIFERP_API ACitizenNPC : public ALifeCharacterBase, public ILifeInteractable
{
	GENERATED_BODY()

public:
	ACitizenNPC();

	/** Sets identity and the sidewalk graph to walk. Call right after spawning. */
	void InitCitizen(const FString& InName, const FLifeAppearance& InAppearance, ALifeTownBuilder* InTown, int32 StartNode, int32 Seed);

	virtual void Tick(float DeltaSeconds) override;

	// ILifeInteractable
	virtual FString GetInteractionPrompt(const ALifePlayerCharacter* Player) const override;
	virtual void Interact(ALifePlayerCharacter* Player) override;

	/** Stops, faces the player and says a line. */
	void Respond(ALifePlayerCharacter* Player, int32 Relationship);

	const FString& GetCitizenName() const { return CitizenName; }

private:
	void PickNextNode();
	void UpdateSchedule();
	void Say(const FString& Line, float Seconds);

	UPROPERTY(VisibleAnywhere, Category = "Life") TObjectPtr<UTextRenderComponent> SpeechText;
	UPROPERTY(VisibleInstanceOnly, Category = "Life") FString CitizenName;
	UPROPERTY() TWeakObjectPtr<ALifeTownBuilder> Town;

	FRandomStream Random;
	int32 CurrentNode = INDEX_NONE;
	int32 PreviousNode = INDEX_NONE;
	float PauseTimer = 0.f;
	float SpeechTimer = 0.f;
	float StuckTimer = 0.f;
	FVector LastProgressLocation = FVector::ZeroVector;
	TWeakObjectPtr<AActor> TalkPartner;

	/** Hour this citizen goes home and comes back out (randomised per citizen). */
	float HomeHour = 22.f;
	float OutHour = 7.f;
	bool bAtHome = false;
};
