// Life RP - game mode.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameModeBase.h"
#include "LifeGameMode.generated.h"

class ALifeTownBuilder;
class ALifeEnvironment;

/**
 * Sets up a playable town in any level: spawns the sky/time-of-day environment
 * and the procedural town (unless the level already contains them), and starts
 * the player in front of their home.
 */
UCLASS()
class LIFERP_API ALifeGameMode : public AGameModeBase
{
	GENERATED_BODY()

public:
	ALifeGameMode();

	virtual void InitGame(const FString& MapName, const FString& Options, FString& ErrorMessage) override;
	virtual AActor* FindPlayerStart_Implementation(AController* Player, const FString& IncomingName) override;

protected:
	UPROPERTY(EditDefaultsOnly, Category = "Life") TSubclassOf<ALifeTownBuilder> TownBuilderClass;
	UPROPERTY(EditDefaultsOnly, Category = "Life") TSubclassOf<ALifeEnvironment> EnvironmentClass;

private:
	UPROPERTY() TObjectPtr<ALifeTownBuilder> Town;
};
