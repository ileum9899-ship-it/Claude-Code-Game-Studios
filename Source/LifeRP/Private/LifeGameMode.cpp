// Life RP - game mode.

#include "LifeGameMode.h"
#include "LifeEnvironment.h"
#include "LifeGameState.h"
#include "LifeHUD.h"
#include "LifePlayerCharacter.h"
#include "LifePlayerController.h"
#include "LifeTownBuilder.h"
#include "Engine/World.h"
#include "EngineUtils.h"

ALifeGameMode::ALifeGameMode()
{
	DefaultPawnClass = ALifePlayerCharacter::StaticClass();
	PlayerControllerClass = ALifePlayerController::StaticClass();
	GameStateClass = ALifeGameState::StaticClass();
	HUDClass = ALifeHUD::StaticClass();
	TownBuilderClass = ALifeTownBuilder::StaticClass();
	EnvironmentClass = ALifeEnvironment::StaticClass();
}

void ALifeGameMode::InitGame(const FString& MapName, const FString& Options, FString& ErrorMessage)
{
	Super::InitGame(MapName, Options, ErrorMessage);

	UWorld* World = GetWorld();
	FActorSpawnParameters Params;
	Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;

	if (!TActorIterator<ALifeEnvironment>(World))
	{
		World->SpawnActor<ALifeEnvironment>(EnvironmentClass, FTransform::Identity, Params);
	}

	TActorIterator<ALifeTownBuilder> Existing(World);
	Town = Existing ? *Existing : World->SpawnActor<ALifeTownBuilder>(TownBuilderClass, FTransform::Identity, Params);
	if (Town)
	{
		// Static geometry now, so the player start exists before the first player logs in.
		// Citizens and traffic spawn in the builder's BeginPlay.
		Town->BuildStaticTown();
	}
}

AActor* ALifeGameMode::FindPlayerStart_Implementation(AController* Player, const FString& IncomingName)
{
	if (Town && Town->GetPlayerSpawnPoint())
	{
		return Town->GetPlayerSpawnPoint();
	}
	return Super::FindPlayerStart_Implementation(Player, IncomingName);
}
