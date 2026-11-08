// Life RP - save data.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/SaveGame.h"
#include "LifeTypes.h"
#include "LifeSaveGame.generated.h"

/** Everything that persists between sessions. Bump SaveVersion when the layout changes. */
UCLASS()
class LIFERP_API ULifeSaveGame : public USaveGame
{
	GENERATED_BODY()

public:
	UPROPERTY() int32 SaveVersion = 1;
	UPROPERTY() int32 Day = 1;
	UPROPERTY() float MinuteOfDay = 480.f;
	UPROPERTY() FVector PlayerLocation = FVector::ZeroVector;
	UPROPERTY() float PlayerYaw = 0.f;
	UPROPERTY() FLifeNeedValues Needs;
	UPROPERTY() int32 Cash = 0;
	UPROPERTY() int32 Bank = 0;
	UPROPERTY() FName JobId;
	UPROPERTY() float JobHours = 0.f;
	UPROPERTY() float Fitness = 0.f;
	UPROPERTY() float Charisma = 0.f;
	UPROPERTY() TMap<FString, int32> Relationships;
};
