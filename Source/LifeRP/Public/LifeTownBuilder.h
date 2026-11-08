// Life RP - procedural town.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "LifeTypes.h"
#include "LifeTownBuilder.generated.h"

class UInstancedStaticMeshComponent;
class UPointLightComponent;
class UStaticMesh;
class ATargetPoint;
class ALifeStation;

/** A building name the HUD draws in the local language near the entrance. */
struct FLifeSignLabel
{
	FVector Location;
	FString Text;
};

/**
 * Builds a seeded grid town at runtime: roads with markings and crosswalks,
 * sidewalks, downtown towers, mid-rise blocks, suburban houses, a central park,
 * six enterable buildings (home, diner, office, bank, garage, gym) furnished with
 * usable stations, street lights, a sidewalk graph for citizens, and traffic.
 *
 * Repeated geometry is drawn with one instanced static mesh per (shape, colour),
 * so the whole town costs a few dozen draw calls.
 */
UCLASS()
class LIFERP_API ALifeTownBuilder : public AActor
{
	GENERATED_BODY()

public:
	ALifeTownBuilder();

	/** Builds roads, buildings, furniture and the player start. Idempotent. */
	void BuildStaticTown();

	/** Where the player begins (in front of home). */
	AActor* GetPlayerSpawnPoint() const;

	const TArray<FVector>& GetWalkNodes() const { return WalkNodes; }
	const TArray<int32>& GetWalkLinks(int32 Node) const;

	/** Localised building names (the 3D signs keep the Latin brand names). */
	const TArray<FLifeSignLabel>& GetSignLabels() const { return SignLabels; }

	virtual void Tick(float DeltaSeconds) override;

protected:
	virtual void BeginPlay() override;

private:
	// ---- primitives (world space, builder sits at the origin)
	UInstancedStaticMeshComponent* GetISM(UStaticMesh* Mesh, const FLinearColor& Color, bool bCollision);
	void Box(const FVector& Center, const FVector& Size, const FLinearColor& Color, const FRotator& Rot = FRotator::ZeroRotator, bool bCollision = true);
	void Cyl(const FVector& Center, const FVector& Size, const FLinearColor& Color, const FRotator& Rot = FRotator::ZeroRotator, bool bCollision = true);
	void Ball(const FVector& Center, const FVector& Size, const FLinearColor& Color, bool bCollision = false);
	/** Box in a local frame (room-relative). */
	void LBox(const FTransform& Frame, const FVector& Local, const FVector& Size, const FLinearColor& Color, float LocalYaw = 0.f, bool bCollision = true);

	// ---- layout helpers
	float Pitch() const;
	float Half() const;
	float RoadCenter(int32 Index) const;
	float BlockMin(int32 Index) const;

	// ---- builders
	void BuildGroundAndHorizon();
	void BuildRoads();
	void BuildBlock(int32 BX, int32 BY);
	void BuildTower(const FVector& Base, const FVector2D& Footprint, float Height, const FLinearColor& Facade, bool bShopFront);
	void BuildHouse(const FVector& LotCenter, float LotSize, float FacingYaw);
	void BuildPark(const FVector& Center, float Size);
	void AddTree(const FVector& Base, float Scale);
	void AddStreetLight(const FVector& Base, const FVector& ArmDir);
	void AddBench(const FVector& Base, float Yaw);

	/** Walls with a door gap at local +X, floor, roof, sign, interior light. Returns nothing; Frame is the room centre at floor level. */
	void BuildRoom(const FTransform& Frame, float Depth, float Width, float Height, float DoorWidth,
		const FLinearColor& Walls, const FLinearColor& Floor, const FLinearColor& Accent, const FString& Sign, const FString& LocalName);

	ALifeStation* SpawnStation(const FTransform& Frame, const FVector& Local, float LocalYaw, ELifeStationType Type, FName Id, const FLinearColor& Accent);

	void BuildHome(const FTransform& F);
	void BuildDiner(const FTransform& F);
	void BuildOffice(const FTransform& F);
	void BuildBank(const FTransform& F);
	void BuildGarage(const FTransform& F);
	void BuildGym(const FTransform& F);

	void BuildWalkGraph();
	void SpawnCitizens();
	void SpawnTraffic();

	UPROPERTY(VisibleAnywhere, Category = "Life|Town") TObjectPtr<USceneComponent> Root;
	UPROPERTY() TMap<FString, TObjectPtr<UInstancedStaticMeshComponent>> ISMs;
	UPROPERTY() TArray<TObjectPtr<UPointLightComponent>> StreetLights;
	UPROPERTY() TObjectPtr<ATargetPoint> PlayerStart;
	UPROPERTY() TArray<TObjectPtr<ALifeStation>> Stations;

	TArray<FVector> WalkNodes;
	TArray<TArray<int32>> WalkLinks;
	TArray<FLifeSignLabel> SignLabels;
	FTransform PlayerCarSpot;
	FRandomStream Rand;
	bool bBuilt = false;
	bool bLightsOn = false;
	float LightCheckTimer = 0.f;

	// Cached settings
	int32 N = 5;
	float B = 5200.f;
	float R = 1300.f;
	float S = 380.f;
};
