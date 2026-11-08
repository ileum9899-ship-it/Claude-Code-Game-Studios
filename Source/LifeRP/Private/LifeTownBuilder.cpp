// Life RP - procedural town.

#include "LifeTownBuilder.h"
#include "CitizenNPC.h"
#include "LifeGameState.h"
#include "LifeRP.h"
#include "LifeSettings.h"
#include "LifeStation.h"
#include "LifeVehicle.h"
#include "LifeVisuals.h"
#include "Components/InstancedStaticMeshComponent.h"
#include "Components/PointLightComponent.h"
#include "Engine/StaticMesh.h"
#include "Engine/TargetPoint.h"
#include "Engine/World.h"

using namespace LifeVisuals;

namespace TownPalette
{
	const FLinearColor Asphalt(0.030f, 0.030f, 0.034f);
	const FLinearColor LineYellow(0.75f, 0.52f, 0.04f);
	const FLinearColor LineWhite(0.78f, 0.78f, 0.76f);
	const FLinearColor Sidewalk(0.40f, 0.39f, 0.37f);
	const FLinearColor Curb(0.55f, 0.54f, 0.52f);
	const FLinearColor Grass(0.09f, 0.22f, 0.045f);
	const FLinearColor ParkGrass(0.07f, 0.26f, 0.05f);
	const FLinearColor Plaza(0.46f, 0.43f, 0.39f);
	const FLinearColor Glass(0.025f, 0.045f, 0.075f);
	const FLinearColor GlassShop(0.05f, 0.09f, 0.12f);
	const FLinearColor Trunk(0.20f, 0.12f, 0.06f);
	const FLinearColor Leaves[3] = { FLinearColor(0.05f, 0.20f, 0.03f), FLinearColor(0.09f, 0.26f, 0.04f), FLinearColor(0.14f, 0.24f, 0.03f) };
	const FLinearColor DarkMetal(0.06f, 0.06f, 0.065f);
	const FLinearColor Roof(0.10f, 0.10f, 0.11f);
	const FLinearColor Wood(0.33f, 0.20f, 0.10f);
	const FLinearColor Water(0.04f, 0.16f, 0.24f);
	const FLinearColor Stone(0.50f, 0.48f, 0.44f);
	const FLinearColor Facades[8] = {
		FLinearColor(0.55f, 0.52f, 0.47f), FLinearColor(0.30f, 0.32f, 0.36f), FLinearColor(0.62f, 0.40f, 0.28f),
		FLinearColor(0.70f, 0.66f, 0.58f), FLinearColor(0.20f, 0.24f, 0.28f), FLinearColor(0.45f, 0.20f, 0.14f),
		FLinearColor(0.75f, 0.74f, 0.70f), FLinearColor(0.36f, 0.40f, 0.30f) };
	const FLinearColor HouseWalls[6] = {
		FLinearColor(0.80f, 0.72f, 0.55f), FLinearColor(0.60f, 0.72f, 0.80f), FLinearColor(0.85f, 0.85f, 0.82f),
		FLinearColor(0.75f, 0.55f, 0.50f), FLinearColor(0.58f, 0.70f, 0.55f), FLinearColor(0.90f, 0.80f, 0.60f) };
	const FLinearColor HouseRoofs[3] = { FLinearColor(0.25f, 0.08f, 0.06f), FLinearColor(0.12f, 0.12f, 0.14f), FLinearColor(0.30f, 0.20f, 0.12f) };
	const FLinearColor Awnings[5] = {
		FLinearColor(0.60f, 0.05f, 0.05f), FLinearColor(0.05f, 0.30f, 0.12f), FLinearColor(0.05f, 0.15f, 0.45f),
		FLinearColor(0.70f, 0.45f, 0.05f), FLinearColor(0.35f, 0.08f, 0.40f) };
}
using namespace TownPalette;

namespace
{
	/** Lots that hold the enterable buildings, relative to the central park block. */
	enum class ESpecial : uint8 { None, Home, Diner, Office, Bank, Garage, Gym };

	struct FSpecialLot { int32 DX, DY, LX, LY; ESpecial Kind; };
	const FSpecialLot SpecialLots[] = {
		{ -1,  0, 1, 0, ESpecial::Home },
		{ -1,  0, 1, 1, ESpecial::Garage },
		{  0, -1, 0, 1, ESpecial::Diner },
		{  0, -1, 1, 1, ESpecial::Office },
		{  1,  0, 0, 0, ESpecial::Bank },
		{  1,  0, 0, 1, ESpecial::Gym },
	};

	struct FRoomSpec { float Depth, Width, Height, Door; };
	FRoomSpec RoomSpecFor(ESpecial Kind)
	{
		switch (Kind)
		{
		case ESpecial::Home:   return { 1100.f, 1300.f, 420.f, 220.f };
		case ESpecial::Diner:  return { 1200.f, 1500.f, 450.f, 300.f };
		case ESpecial::Office: return { 1100.f, 1400.f, 450.f, 260.f };
		case ESpecial::Bank:   return { 1000.f, 1200.f, 480.f, 260.f };
		case ESpecial::Garage: return { 1300.f, 1500.f, 520.f, 620.f };
		case ESpecial::Gym:    return { 1100.f, 1400.f, 450.f, 260.f };
		default:               return { 1000.f, 1000.f, 420.f, 220.f };
		}
	}
}

ALifeTownBuilder::ALifeTownBuilder()
{
	PrimaryActorTick.bCanEverTick = true;
	PrimaryActorTick.TickInterval = 0.5f;
	Root = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
	Root->SetMobility(EComponentMobility::Static);
	SetRootComponent(Root);
}

// ------------------------------------------------------------------ Primitives

UInstancedStaticMeshComponent* ALifeTownBuilder::GetISM(UStaticMesh* Mesh, const FLinearColor& Color, bool bCollision)
{
	const FString Key = FString::Printf(TEXT("%s_%s_%d"), *GetNameSafe(Mesh), *Color.ToFColor(true).ToHex(), bCollision ? 1 : 0);
	if (TObjectPtr<UInstancedStaticMeshComponent>* Found = ISMs.Find(Key))
	{
		return *Found;
	}
	UInstancedStaticMeshComponent* ISM = NewObject<UInstancedStaticMeshComponent>(this);
	ISM->SetMobility(EComponentMobility::Static);
	ISM->SetStaticMesh(Mesh);
	ISM->SetupAttachment(Root);
	ISM->SetCollisionEnabled(bCollision ? ECollisionEnabled::QueryAndPhysics : ECollisionEnabled::NoCollision);
	ISM->SetCanEverAffectNavigation(bCollision);
	ISM->RegisterComponent();
	AddInstanceComponent(ISM);
	Tint(ISM, Color);
	ISMs.Add(Key, ISM);
	return ISM;
}

void ALifeTownBuilder::Box(const FVector& Center, const FVector& Size, const FLinearColor& Color, const FRotator& Rot, bool bCollision)
{
	GetISM(Cube(), Color, bCollision)->AddInstance(FTransform(Rot, Center, Size / 100.f), true);
}

void ALifeTownBuilder::Cyl(const FVector& Center, const FVector& Size, const FLinearColor& Color, const FRotator& Rot, bool bCollision)
{
	GetISM(Cylinder(), Color, bCollision)->AddInstance(FTransform(Rot, Center, Size / 100.f), true);
}

void ALifeTownBuilder::Ball(const FVector& Center, const FVector& Size, const FLinearColor& Color, bool bCollision)
{
	GetISM(Sphere(), Color, bCollision)->AddInstance(FTransform(FRotator::ZeroRotator, Center, Size / 100.f), true);
}

void ALifeTownBuilder::LBox(const FTransform& Frame, const FVector& Local, const FVector& Size, const FLinearColor& Color, float LocalYaw, bool bCollision)
{
	const FRotator Rot = (Frame.GetRotation() * FRotator(0.f, LocalYaw, 0.f).Quaternion()).Rotator();
	Box(Frame.TransformPosition(Local), Size, Color, Rot, bCollision);
}

// ------------------------------------------------------------------ Layout

float ALifeTownBuilder::Pitch() const { return B + R; }
float ALifeTownBuilder::Half() const { return (N * Pitch() + R) * 0.5f; }
float ALifeTownBuilder::RoadCenter(int32 Index) const { return Index * Pitch() + R * 0.5f - Half(); }
float ALifeTownBuilder::BlockMin(int32 Index) const { return R + Index * Pitch() - Half(); }

AActor* ALifeTownBuilder::GetPlayerSpawnPoint() const
{
	return PlayerStart;
}

const TArray<int32>& ALifeTownBuilder::GetWalkLinks(int32 Node) const
{
	static const TArray<int32> Empty;
	return WalkLinks.IsValidIndex(Node) ? WalkLinks[Node] : Empty;
}

// ------------------------------------------------------------------ Build

void ALifeTownBuilder::BuildStaticTown()
{
	if (bBuilt)
	{
		return;
	}
	bBuilt = true;

	const ULifeSettings* Settings = ULifeSettings::Get();
	N = FMath::Clamp(Settings->BlocksPerSide, 3, 9);
	B = Settings->BlockSize;
	R = Settings->RoadWidth;
	S = Settings->SidewalkWidth;
	Rand.Initialize(Settings->TownSeed);

	BuildGroundAndHorizon();
	BuildRoads();
	for (int32 BY = 0; BY < N; ++BY)
	{
		for (int32 BX = 0; BX < N; ++BX)
		{
			BuildBlock(BX, BY);
		}
	}
	BuildWalkGraph();

	int32 Instances = 0;
	for (const TPair<FString, TObjectPtr<UInstancedStaticMeshComponent>>& Pair : ISMs)
	{
		Instances += Pair.Value->GetInstanceCount();
	}
	UE_LOG(LogLifeRP, Log, TEXT("Town built: %dx%d blocks, %d mesh batches, %d instances, %d stations, %d street lights."),
		N, N, ISMs.Num(), Instances, Stations.Num(), StreetLights.Num());
}

void ALifeTownBuilder::BeginPlay()
{
	Super::BeginPlay();
	BuildStaticTown();
	SpawnCitizens();
	SpawnTraffic();
}

void ALifeTownBuilder::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	const ALifeGameState* GS = GetWorld()->GetGameState<ALifeGameState>();
	const bool bNight = GS && GS->IsNight();
	if (bNight != bLightsOn)
	{
		bLightsOn = bNight;
		for (UPointLightComponent* Light : StreetLights)
		{
			Light->SetVisibility(bLightsOn);
		}
	}
}

void ALifeTownBuilder::BuildGroundAndHorizon()
{
	const float Extent = Half() * 2.f;
	// Countryside ground, top at z = -2 (roads sit at 0, blocks at 15).
	Box(FVector(0, 0, -12.f), FVector(Extent * 4.f, Extent * 4.f, 20.f), Grass);

	// Rolling hills on the horizon.
	for (int32 i = 0; i < 18; ++i)
	{
		const float Angle = (i / 18.f) * 2.f * UE_PI + Rand.FRandRange(-0.1f, 0.1f);
		const float Dist = Extent * Rand.FRandRange(1.15f, 1.6f);
		const FVector P(FMath::Cos(Angle) * Dist, FMath::Sin(Angle) * Dist, -600.f);
		Ball(P, FVector(Rand.FRandRange(9000.f, 16000.f), Rand.FRandRange(9000.f, 16000.f), Rand.FRandRange(2500.f, 5200.f)),
			Leaves[i % 3] * 0.8f, true);
	}
	// Scattered trees between town and hills.
	for (int32 i = 0; i < 160; ++i)
	{
		const float Angle = Rand.FRandRange(0.f, 2.f * UE_PI);
		const float Dist = Half() + Rand.FRandRange(900.f, Extent * 0.9f);
		AddTree(FVector(FMath::Cos(Angle) * Dist, FMath::Sin(Angle) * Dist, -2.f), Rand.FRandRange(0.9f, 1.5f));
	}
}

void ALifeTownBuilder::BuildRoads()
{
	const float Length = Half() * 2.f;
	for (int32 i = 0; i <= N; ++i)
	{
		const float C = RoadCenter(i);
		Box(FVector(0.f, C, -5.f), FVector(Length, R, 10.f), Asphalt);           // along X
		Box(FVector(C, 0.f, -5.3f), FVector(R, Length, 10.f), Asphalt);          // along Y
	}

	// Markings per segment between intersections: dashed centre line, solid edge lines, crosswalks.
	for (int32 i = 0; i <= N; ++i)
	{
		const float C = RoadCenter(i);
		for (int32 b = 0; b < N; ++b)
		{
			const float Start = BlockMin(b);
			const float End = Start + B;
			for (int32 Axis = 0; Axis < 2; ++Axis)
			{
				auto P = [Axis, C](float Along, float Across, float Z)
				{
					return Axis == 0 ? FVector(Along, C + Across, Z) : FVector(C + Across, Along, Z);
				};
				const float Yaw = Axis == 0 ? 0.f : 90.f;
				for (float D = Start + 600.f; D + 300.f < End - 600.f; D += 600.f)
				{
					Box(P(D + 150.f, 0.f, 0.5f), FVector(300.f, 14.f, 1.f), LineYellow, FRotator(0, Yaw, 0), false);
				}
				for (float Side : { -1.f, 1.f })
				{
					Box(P((Start + End) * 0.5f, Side * (R * 0.5f - 70.f), 0.5f), FVector(B - 900.f, 12.f, 1.f), LineWhite, FRotator(0, Yaw, 0), false);
				}
				for (float Edge : { Start + 220.f, End - 220.f })
				{
					for (float Across = -R * 0.5f + 90.f; Across < R * 0.5f - 60.f; Across += 110.f)
					{
						Box(P(Edge, Across, 0.6f), FVector(280.f, 55.f, 1.f), LineWhite, FRotator(0, Yaw, 0), false);
					}
				}
			}
		}
	}
}

void ALifeTownBuilder::BuildBlock(int32 BX, int32 BY)
{
	const int32 Center = (N - 1) / 2;
	const float X0 = BlockMin(BX);
	const float Y0 = BlockMin(BY);
	const FVector BlockCenter(X0 + B * 0.5f, Y0 + B * 0.5f, 0.f);
	const float Inner = B - 2.f * S;
	const float Lot = Inner * 0.5f;
	const int32 Ring = FMath::Max(FMath::Abs(BX - Center), FMath::Abs(BY - Center));

	// Sidewalk slab with a curb lip, top at 15 cm.
	Box(BlockCenter + FVector(0, 0, 7.5f), FVector(B, B, 15.f), Sidewalk);
	Box(BlockCenter + FVector(0, 0, 7.6f), FVector(B + 24.f, B + 24.f, 14.f), Curb);

	if (BX == Center && BY == Center)
	{
		BuildPark(BlockCenter, B);
	}
	else
	{
		enum class EStyle : uint8 { Downtown, Midtown, Suburb };
		const EStyle Style = Ring <= 1 ? EStyle::Downtown : (Rand.FRand() < 0.55f ? EStyle::Suburb : EStyle::Midtown);
		Box(BlockCenter + FVector(0, 0, 16.f), FVector(Inner, Inner, 2.f), Style == EStyle::Suburb ? Grass : Plaza);

		for (int32 LY = 0; LY < 2; ++LY)
		{
			for (int32 LX = 0; LX < 2; ++LX)
			{
				const FVector LotCenter(X0 + S + Lot * (LX + 0.5f), Y0 + S + Lot * (LY + 0.5f), 17.f);
				const float FacingYaw = LY == 0 ? -90.f : 90.f;

				ESpecial Kind = ESpecial::None;
				for (const FSpecialLot& Special : SpecialLots)
				{
					if (BX == Center + Special.DX && BY == Center + Special.DY && LX == Special.LX && LY == Special.LY)
					{
						Kind = Special.Kind;
					}
				}

				if (Kind != ESpecial::None)
				{
					const FRoomSpec Spec = RoomSpecFor(Kind);
					const FVector Facing(0.f, LY == 0 ? -1.f : 1.f, 0.f);
					const FTransform Frame(FRotator(0.f, FacingYaw, 0.f), LotCenter + Facing * (Lot * 0.5f - Spec.Depth * 0.5f - 150.f));
					switch (Kind)
					{
					case ESpecial::Home:   BuildHome(Frame); break;
					case ESpecial::Diner:  BuildDiner(Frame); break;
					case ESpecial::Office: BuildOffice(Frame); break;
					case ESpecial::Bank:   BuildBank(Frame); break;
					case ESpecial::Garage: BuildGarage(Frame); break;
					case ESpecial::Gym:    BuildGym(Frame); break;
					default: break;
					}
					continue;
				}

				switch (Style)
				{
				case EStyle::Downtown:
				{
					const FVector2D Foot(Lot * Rand.FRandRange(0.6f, 0.8f), Lot * Rand.FRandRange(0.6f, 0.8f));
					BuildTower(LotCenter, Foot, Rand.FRandRange(2600.f, 7800.f), Facades[Rand.RandRange(0, 7)], Rand.FRand() < 0.6f);
					break;
				}
				case EStyle::Midtown:
				{
					const FVector2D Foot(Lot * Rand.FRandRange(0.65f, 0.8f), Lot * Rand.FRandRange(0.65f, 0.8f));
					BuildTower(LotCenter, Foot, Rand.FRandRange(1100.f, 2300.f), Facades[Rand.RandRange(0, 7)], true);
					break;
				}
				case EStyle::Suburb:
					BuildHouse(LotCenter, Lot, FacingYaw);
					break;
				}
			}
		}
	}

	// Sidewalk trees, keeping corners clear for street lights and crossings.
	for (int32 Edge = 0; Edge < 4; ++Edge)
	{
		for (float D = 900.f; D < B - 900.f; D += 1100.f)
		{
			const float Inset = S * 0.82f;
			FVector P;
			switch (Edge)
			{
			case 0:  P = FVector(X0 + D, Y0 + Inset, 15.f); break;
			case 1:  P = FVector(X0 + D, Y0 + B - Inset, 15.f); break;
			case 2:  P = FVector(X0 + Inset, Y0 + D, 15.f); break;
			default: P = FVector(X0 + B - Inset, Y0 + D, 15.f); break;
			}
			Cyl(P + FVector(0, 0, 1.f), FVector(110.f, 110.f, 4.f), DarkMetal, FRotator::ZeroRotator, false);
			AddTree(P, Rand.FRandRange(0.7f, 0.95f));
		}
	}

	// Street lights on two diagonal corners per block.
	for (int32 CY = 0; CY < 2; ++CY)
	{
		for (int32 CX = 0; CX < 2; ++CX)
		{
			if ((CX + CY) % 2 != 0)
			{
				continue;
			}
			const FVector Base(X0 + (CX ? B - 70.f : 70.f), Y0 + (CY ? B - 70.f : 70.f), 15.f);
			AddStreetLight(Base, FVector(CX ? 1.f : -1.f, 0.f, 0.f));
		}
	}
}

void ALifeTownBuilder::BuildTower(const FVector& Base, const FVector2D& Footprint, float Height, const FLinearColor& Facade, bool bShopFront)
{
	const float FX = Footprint.X, FY = Footprint.Y;
	const FLinearColor Trim = Facade * 0.6f;
	Box(Base + FVector(0, 0, Height * 0.5f), FVector(FX, FY, Height), Facade);

	// Window bands per floor.
	const float FloorHeight = 360.f;
	const int32 Floors = FMath::Max(1, FMath::FloorToInt(Height / FloorHeight));
	for (int32 F = bShopFront ? 1 : 0; F < Floors; ++F)
	{
		const float Z = Base.Z + F * FloorHeight + 200.f;
		if (Z + 80.f > Base.Z + Height - 40.f)
		{
			break;
		}
		Box(FVector(Base.X, Base.Y, Z), FVector(FX + 8.f, FY + 8.f, 150.f), Glass, FRotator::ZeroRotator, false);
	}

	// Vertical fins break up the bands.
	for (float X = -FX * 0.5f + 150.f; X < FX * 0.5f - 100.f; X += 320.f)
	{
		for (float Side : { -1.f, 1.f })
		{
			Box(Base + FVector(X, Side * (FY * 0.5f + 7.f), Height * 0.5f), FVector(22.f, 14.f, Height), Trim, FRotator::ZeroRotator, false);
		}
	}
	for (float Y = -FY * 0.5f + 150.f; Y < FY * 0.5f - 100.f; Y += 320.f)
	{
		for (float Side : { -1.f, 1.f })
		{
			Box(Base + FVector(Side * (FX * 0.5f + 7.f), Y, Height * 0.5f), FVector(14.f, 22.f, Height), Trim, FRotator::ZeroRotator, false);
		}
	}

	// Roof: parapet, plant, antenna.
	Box(Base + FVector(0, 0, Height + 20.f), FVector(FX + 24.f, FY + 24.f, 40.f), Trim);
	Box(Base + FVector(0, 0, Height + 42.f), FVector(FX - 40.f, FY - 40.f, 4.f), Roof, FRotator::ZeroRotator, false);
	const int32 Units = Rand.RandRange(1, 4);
	for (int32 i = 0; i < Units; ++i)
	{
		const FVector P = Base + FVector(Rand.FRandRange(-FX, FX) * 0.3f, Rand.FRandRange(-FY, FY) * 0.3f, Height + 90.f);
		Box(P, FVector(Rand.FRandRange(150.f, 320.f), Rand.FRandRange(150.f, 320.f), 100.f), FLinearColor(0.45f, 0.46f, 0.48f));
	}
	if (Height > 5000.f && Rand.FRand() < 0.6f)
	{
		Cyl(Base + FVector(0, 0, Height + 500.f), FVector(16.f, 16.f, 900.f), DarkMetal, FRotator::ZeroRotator, false);
	}

	if (bShopFront)
	{
		const FLinearColor Awning = Awnings[Rand.RandRange(0, 4)];
		Box(Base + FVector(0, 0, 140.f), FVector(FX + 10.f, FY + 10.f, 260.f), GlassShop, FRotator::ZeroRotator, false);
		Box(Base + FVector(0, 0, 300.f), FVector(FX + 90.f, FY + 90.f, 12.f), Awning, FRotator::ZeroRotator, false);
	}
}

void ALifeTownBuilder::BuildHouse(const FVector& LotCenter, float LotSize, float FacingYaw)
{
	const FTransform F(FRotator(0.f, FacingYaw, 0.f), LotCenter);
	const FLinearColor Wall = HouseWalls[Rand.RandRange(0, 5)];
	const FLinearColor RoofColor = HouseRoofs[Rand.RandRange(0, 2)];
	const float D = Rand.FRandRange(850.f, 1050.f);
	const float W = Rand.FRandRange(950.f, 1250.f);
	const float H = Rand.FRandRange(480.f, 620.f);
	const float CX = -LotSize * 0.08f;

	LBox(F, FVector(CX, 0, H * 0.5f), FVector(D, W, H), Wall);
	LBox(F, FVector(CX, 0, 15.f), FVector(D + 30.f, W + 30.f, 30.f), Stone * 0.7f);

	// Gable roof: a cube pitched 45 degrees about the local Y axis; only the top half shows.
	const float Diamond = (D + 80.f) / UE_SQRT_2;
	const FRotator RoofRot = (F.GetRotation() * FRotator(45.f, 0.f, 0.f).Quaternion()).Rotator();
	Box(F.TransformPosition(FVector(CX, 0, H)), FVector(Diamond, W + 80.f, Diamond), RoofColor, RoofRot);
	LBox(F, FVector(CX - D * 0.2f, W * 0.3f, H + D * 0.35f), FVector(60.f, 60.f, D * 0.5f), Stone * 0.6f);

	const float Front = CX + D * 0.5f;
	LBox(F, FVector(Front + 3.f, 0, 110.f), FVector(8.f, 110.f, 220.f), Wood);
	LBox(F, FVector(Front + 40.f, 0, 230.f), FVector(80.f, 200.f, 10.f), RoofColor, 0.f, false);
	for (float Y : { -W * 0.3f, W * 0.3f })
	{
		for (float Z : { 150.f, H - 150.f })
		{
			if (FMath::Abs(Y) < 120.f && Z < 250.f)
			{
				continue;
			}
			LBox(F, FVector(Front + 2.f, Y, Z), FVector(6.f, 170.f, 130.f), FLinearColor(0.85f, 0.85f, 0.82f), 0.f, false);
			LBox(F, FVector(Front + 5.f, Y, Z), FVector(6.f, 140.f, 105.f), Glass, 0.f, false);
		}
	}

	// Front path, hedges with a gap, yard trees.
	const float Edge = LotSize * 0.5f;
	LBox(F, FVector((Front + Edge) * 0.5f, 0, 1.5f), FVector(Edge - Front, 140.f, 3.f), Plaza, 0.f, false);
	const float HedgeLen = Edge - 150.f;
	for (float Side : { -1.f, 1.f })
	{
		LBox(F, FVector(Edge - 60.f, Side * (100.f + HedgeLen * 0.5f), 45.f), FVector(70.f, HedgeLen, 90.f), Leaves[1]);
	}
	AddTree(F.TransformPosition(FVector(-Edge + 250.f, Edge - 250.f, 0.f)), Rand.FRandRange(0.9f, 1.2f));
	if (Rand.FRand() < 0.6f)
	{
		AddTree(F.TransformPosition(FVector(-Edge + 250.f, -Edge + 250.f, 0.f)), Rand.FRandRange(0.8f, 1.1f));
	}
}

void ALifeTownBuilder::BuildPark(const FVector& Center, float Size)
{
	const float Inner = Size - 2.f * S;
	Box(Center + FVector(0, 0, 16.f), FVector(Inner, Inner, 2.f), ParkGrass);
	// Cross paths.
	Box(Center + FVector(0, 0, 17.5f), FVector(Inner, 300.f, 2.f), Plaza, FRotator::ZeroRotator, false);
	Box(Center + FVector(0, 0, 17.6f), FVector(300.f, Inner, 2.f), Plaza, FRotator::ZeroRotator, false);
	Cyl(Center + FVector(0, 0, 17.7f), FVector(1500.f, 1500.f, 2.f), Plaza, FRotator::ZeroRotator, false);

	// Fountain.
	Cyl(Center + FVector(0, 0, 45.f), FVector(800.f, 800.f, 60.f), Stone);
	Cyl(Center + FVector(0, 0, 70.f), FVector(740.f, 740.f, 12.f), Water, FRotator::ZeroRotator, false);
	Cyl(Center + FVector(0, 0, 160.f), FVector(70.f, 70.f, 200.f), Stone);
	Cyl(Center + FVector(0, 0, 265.f), FVector(260.f, 260.f, 20.f), Stone, FRotator::ZeroRotator, false);
	Cyl(Center + FVector(0, 0, 276.f), FVector(230.f, 230.f, 6.f), Water, FRotator::ZeroRotator, false);

	// Benches around the plaza, trees and flower beds in the quadrants.
	for (int32 i = 0; i < 8; ++i)
	{
		const float A = i / 8.f * 2.f * UE_PI + UE_PI / 8.f;
		AddBench(Center + FVector(FMath::Cos(A) * 640.f, FMath::Sin(A) * 640.f, 17.f), FMath::RadiansToDegrees(A) + 180.f);
	}
	for (int32 i = 0; i < 28; ++i)
	{
		const FVector P(Rand.FRandRange(-Inner, Inner) * 0.45f, Rand.FRandRange(-Inner, Inner) * 0.45f, 17.f);
		if (FMath::Abs(P.X) < 400.f || FMath::Abs(P.Y) < 400.f || P.Size2D() < 1000.f)
		{
			continue;
		}
		AddTree(Center + P, Rand.FRandRange(0.9f, 1.4f));
	}
	for (int32 Q = 0; Q < 4; ++Q)
	{
		const FVector P(Q % 2 ? 900.f : -900.f, Q / 2 ? 900.f : -900.f, 25.f);
		Cyl(Center + P, FVector(500.f, 500.f, 16.f), FLinearColor(0.12f, 0.07f, 0.03f), FRotator::ZeroRotator, false);
		for (int32 k = 0; k < 14; ++k)
		{
			const float A = Rand.FRandRange(0.f, 2.f * UE_PI);
			const float Rr = Rand.FRandRange(0.f, 210.f);
			Ball(Center + P + FVector(FMath::Cos(A) * Rr, FMath::Sin(A) * Rr, 22.f), FVector(40.f, 40.f, 30.f), Awnings[k % 5] * 1.4f);
		}
	}
}

void ALifeTownBuilder::AddTree(const FVector& Base, float Scale)
{
	const float TrunkH = 320.f * Scale;
	Cyl(Base + FVector(0, 0, TrunkH * 0.5f), FVector(26.f, 26.f, TrunkH) * FVector(Scale, Scale, 1.f), Trunk);
	const FLinearColor Leaf = Leaves[Rand.RandRange(0, 2)];
	const float Canopy = 300.f * Scale;
	Ball(Base + FVector(0, 0, TrunkH + Canopy * 0.35f), FVector(Canopy, Canopy, Canopy * 0.85f), Leaf);
	for (int32 i = 0; i < 2; ++i)
	{
		const FVector Offset(Rand.FRandRange(-0.3f, 0.3f) * Canopy, Rand.FRandRange(-0.3f, 0.3f) * Canopy, Rand.FRandRange(0.1f, 0.6f) * Canopy);
		Ball(Base + FVector(0, 0, TrunkH) + Offset, FVector(Canopy * 0.7f), Leaf);
	}
}

void ALifeTownBuilder::AddStreetLight(const FVector& Base, const FVector& ArmDir)
{
	const float PoleH = 620.f;
	Cyl(Base + FVector(0, 0, PoleH * 0.5f), FVector(18.f, 18.f, PoleH), DarkMetal);
	Cyl(Base + FVector(0, 0, 40.f), FVector(32.f, 32.f, 80.f), DarkMetal, FRotator::ZeroRotator, false);
	const FVector Arm = Base + FVector(0, 0, PoleH) + ArmDir * 110.f;
	Box(Arm, FVector(220.f, 10.f, 10.f), DarkMetal, ArmDir.Rotation(), false);
	const FVector Head = Base + FVector(0, 0, PoleH - 8.f) + ArmDir * 200.f;
	Box(Head, FVector(70.f, 34.f, 14.f), FLinearColor(0.95f, 0.85f, 0.6f), ArmDir.Rotation(), false);

	UPointLightComponent* Light = NewObject<UPointLightComponent>(this);
	Light->SetMobility(EComponentMobility::Movable);
	Light->SetupAttachment(Root);
	Light->SetRelativeLocation(Head - FVector(0, 0, 25.f));
	Light->SetIntensityUnits(ELightUnits::Candelas);
	Light->SetIntensity(700.f);
	Light->SetAttenuationRadius(2400.f);
	Light->SetSourceRadius(15.f);
	Light->SetLightColor(FLinearColor(1.f, 0.78f, 0.5f));
	Light->SetCastShadows(ULifeSettings::Get()->bStreetLightShadows);
	Light->SetVisibility(false);
	Light->RegisterComponent();
	AddInstanceComponent(Light);
	StreetLights.Add(Light);
}

void ALifeTownBuilder::AddBench(const FVector& Base, float Yaw)
{
	const FTransform F(FRotator(0.f, Yaw, 0.f), Base);
	LBox(F, FVector(0, 0, 45.f), FVector(55.f, 180.f, 8.f), Wood);
	LBox(F, FVector(-25.f, 0, 80.f), FVector(8.f, 180.f, 45.f), Wood, 0.f, false);
	for (float Y : { -75.f, 75.f })
	{
		LBox(F, FVector(0, Y, 22.f), FVector(50.f, 8.f, 44.f), DarkMetal, 0.f, false);
	}
}

// ------------------------------------------------------------------ Enterable buildings

void ALifeTownBuilder::BuildRoom(const FTransform& Frame, float Depth, float Width, float Height, float DoorWidth,
	const FLinearColor& Walls, const FLinearColor& Floor, const FLinearColor& Accent, const FString& Sign, const FString& LocalName)
{
	const float T = 30.f;          // wall thickness
	const float DoorH = FMath::Min(Height - 80.f, DoorWidth > 400.f ? 420.f : 250.f);
	const float HalfD = Depth * 0.5f, HalfW = Width * 0.5f;

	LBox(Frame, FVector(0, 0, 3.f), FVector(Depth, Width, 6.f), Floor);
	LBox(Frame, FVector(-HalfD + T * 0.5f, 0, Height * 0.5f), FVector(T, Width, Height), Walls);
	LBox(Frame, FVector(0, -HalfW + T * 0.5f, Height * 0.5f), FVector(Depth, T, Height), Walls);
	LBox(Frame, FVector(0, HalfW - T * 0.5f, Height * 0.5f), FVector(Depth, T, Height), Walls);

	// Front wall with door opening, plus shop windows.
	const float Segment = (Width - DoorWidth) * 0.5f;
	for (float Side : { -1.f, 1.f })
	{
		const float Y = Side * (DoorWidth * 0.5f + Segment * 0.5f);
		LBox(Frame, FVector(HalfD - T * 0.5f, Y, Height * 0.5f), FVector(T, Segment, Height), Walls);
		if (Segment > 300.f)
		{
			LBox(Frame, FVector(HalfD + 2.f, Y, 175.f), FVector(6.f, Segment - 120.f, 190.f), DarkMetal, 0.f, false);
			LBox(Frame, FVector(HalfD + 5.f, Y, 175.f), FVector(6.f, Segment - 150.f, 165.f), GlassShop, 0.f, false);
		}
	}
	LBox(Frame, FVector(HalfD - T * 0.5f, 0, DoorH + (Height - DoorH) * 0.5f), FVector(T, DoorWidth, Height - DoorH), Walls);
	for (float Side : { -1.f, 1.f })
	{
		LBox(Frame, FVector(HalfD + 4.f, Side * (DoorWidth * 0.5f + 6.f), DoorH * 0.5f), FVector(12.f, 12.f, DoorH), DarkMetal, 0.f, false);
	}

	// Side windows.
	for (float Side : { -1.f, 1.f })
	{
		for (float X = -HalfD + 250.f; X < HalfD - 200.f; X += 350.f)
		{
			LBox(Frame, FVector(X, Side * (HalfW + 3.f), 190.f), FVector(200.f, 6.f, 140.f), Glass, 0.f, false);
		}
	}

	// Roof and cornice.
	LBox(Frame, FVector(0, 0, Height + 15.f), FVector(Depth + 60.f, Width + 60.f, 30.f), Roof);
	LBox(Frame, FVector(0, 0, Height + 45.f), FVector(Depth + 70.f, Width + 70.f, 30.f), Walls * 0.7f, 0.f, false);

	// Awning and sign.
	LBox(Frame, FVector(HalfD + 70.f, 0, DoorH + 25.f), FVector(140.f, DoorWidth + 160.f, 10.f), Accent, 0.f, false);
	const float SignZ = DoorH + (Height - DoorH) * 0.5f + 20.f;
	LBox(Frame, FVector(HalfD + 8.f, 0, SignZ), FVector(10.f, FMath::Max(400.f, Sign.Len() * 70.f), 85.f), Accent * 0.5f, 0.f, false);
	// The 3D sign keeps the Latin brand name: TextRenderComponent fonts are offline atlases without Hangul.
	AddLabel(this, Root, Sign, Frame.TransformPosition(FVector(HalfD + 15.f, 0, SignZ)), 62.f, FColor(255, 240, 210), Frame.Rotator());
	SignLabels.Add({ Frame.TransformPosition(FVector(HalfD + 40.f, 0, DoorH + 60.f)), LocalName });

	// Interior light.
	UPointLightComponent* Light = NewObject<UPointLightComponent>(this);
	Light->SetMobility(EComponentMobility::Movable);
	Light->SetupAttachment(Root);
	Light->SetRelativeLocation(Frame.TransformPosition(FVector(0, 0, Height - 40.f)));
	Light->SetIntensityUnits(ELightUnits::Candelas);
	Light->SetIntensity(220.f);
	Light->SetAttenuationRadius(FMath::Max(Depth, Width) * 1.1f);
	Light->SetSourceRadius(30.f);
	Light->SetLightColor(FLinearColor(1.f, 0.88f, 0.72f));
	Light->RegisterComponent();
	AddInstanceComponent(Light);
}

ALifeStation* ALifeTownBuilder::SpawnStation(const FTransform& Frame, const FVector& Local, float LocalYaw, ELifeStationType Type, FName Id, const FLinearColor& Accent)
{
	const FRotator Rot = (Frame.GetRotation() * FRotator(0.f, LocalYaw, 0.f).Quaternion()).Rotator();
	FActorSpawnParameters Params;
	Params.Owner = this;
	Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
	ALifeStation* Station = GetWorld()->SpawnActor<ALifeStation>(ALifeStation::StaticClass(), Frame.TransformPosition(Local), Rot, Params);
	if (Station)
	{
		Station->Initialize(Type, Id, Accent);
		Stations.Add(Station);
	}
	return Station;
}

void ALifeTownBuilder::BuildHome(const FTransform& F)
{
	const FRoomSpec Spec = RoomSpecFor(ESpecial::Home);
	BuildRoom(F, Spec.Depth, Spec.Width, Spec.Height, Spec.Door, FLinearColor(0.82f, 0.78f, 0.70f), FLinearColor(0.38f, 0.24f, 0.13f), FLinearColor(0.15f, 0.35f, 0.25f), TEXT("HOME"), TEXT("우리 집"));

	SpawnStation(F, FVector(-300.f, -380.f, 6.f), 0.f, ELifeStationType::Bed, NAME_None, FLinearColor(0.20f, 0.30f, 0.60f));
	SpawnStation(F, FVector(-470.f, 570.f, 6.f), 90.f, ELifeStationType::Shower, NAME_None, FLinearColor::White);
	SpawnStation(F, FVector(60.f, 590.f, 6.f), -90.f, ELifeStationType::Television, NAME_None, FLinearColor(0.25f, 0.45f, 0.85f));
	SpawnStation(F, FVector(300.f, -590.f, 6.f), 90.f, ELifeStationType::Fridge, NAME_None, FLinearColor::White);

	// Couch facing the TV, kitchen counter, dining table, rug, plant.
	LBox(F, FVector(60.f, 230.f, 30.f), FVector(220.f, 90.f, 50.f), FLinearColor(0.35f, 0.18f, 0.12f));
	LBox(F, FVector(60.f, 185.f, 70.f), FVector(220.f, 20.f, 60.f), FLinearColor(0.35f, 0.18f, 0.12f));
	LBox(F, FVector(60.f, 400.f, 25.f), FVector(110.f, 60.f, 40.f), FLinearColor(0.25f, 0.17f, 0.10f));
	LBox(F, FVector(100.f, -590.f, 48.f), FVector(220.f, 70.f, 90.f), FLinearColor(0.85f, 0.85f, 0.82f));
	LBox(F, FVector(100.f, -250.f, 40.f), FVector(150.f, 100.f, 80.f), FLinearColor(0.42f, 0.27f, 0.15f));
	LBox(F, FVector(-60.f, 150.f, 7.f), FVector(380.f, 300.f, 2.f), FLinearColor(0.50f, 0.15f, 0.12f), 0.f, false);
	Cyl(F.TransformPosition(FVector(-480.f, 0.f, 30.f)), FVector(50.f, 50.f, 50.f), FLinearColor(0.4f, 0.2f, 0.1f));
	Ball(F.TransformPosition(FVector(-480.f, 0.f, 95.f)), FVector(90.f, 90.f, 100.f), Leaves[1]);

	// Player start: just outside the door, facing in.
	FActorSpawnParameters Params;
	Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
	const FVector StartLoc = F.TransformPosition(FVector(Spec.Depth * 0.5f + 320.f, 0.f, 110.f));
	PlayerStart = GetWorld()->SpawnActor<ATargetPoint>(ATargetPoint::StaticClass(), StartLoc, FRotator(0.f, F.Rotator().Yaw + 180.f, 0.f), Params);

	// Parking spot on the home side of the street, clear of the traffic lane.
	const FVector Facing = F.GetRotation().GetForwardVector();
	const FVector RoadCentre = F.GetLocation() + Facing * (Spec.Depth * 0.5f + 150.f + S + R * 0.5f);
	FVector Spot = RoadCentre - Facing * (R * 0.5f - 120.f) + F.GetRotation().GetRightVector() * 300.f;
	Spot.Z = 90.f;
	PlayerCarSpot = FTransform(FRotator(0.f, Facing.Y < 0.f ? 0.f : 180.f, 0.f), Spot);
}

void ALifeTownBuilder::BuildDiner(const FTransform& F)
{
	const FRoomSpec Spec = RoomSpecFor(ESpecial::Diner);
	const FLifeJobDef* Job = ULifeSettings::Get()->FindJob(TEXT("Barista"));
	const FLinearColor Accent = Job ? Job->AccentColor : FLinearColor::Red;
	BuildRoom(F, Spec.Depth, Spec.Width, Spec.Height, Spec.Door, FLinearColor(0.85f, 0.80f, 0.70f), FLinearColor(0.75f, 0.75f, 0.72f), FLinearColor(0.65f, 0.08f, 0.06f), TEXT("SUNNY DINER"), TEXT("써니 다이너"));

	LBox(F, FVector(-330.f, 0.f, 50.f), FVector(80.f, 1000.f, 100.f), FLinearColor(0.55f, 0.08f, 0.06f));
	LBox(F, FVector(-330.f, 0.f, 103.f), FVector(90.f, 1010.f, 6.f), FLinearColor(0.85f, 0.85f, 0.85f));
	SpawnStation(F, FVector(-230.f, -330.f, 6.f), 0.f, ELifeStationType::ShopItem, TEXT("Coffee"), FLinearColor(0.3f, 0.15f, 0.05f));
	SpawnStation(F, FVector(-230.f, 0.f, 6.f), 0.f, ELifeStationType::ShopItem, TEXT("Burger"), FLinearColor(0.85f, 0.5f, 0.15f));
	SpawnStation(F, FVector(-230.f, 330.f, 6.f), 0.f, ELifeStationType::ShopItem, TEXT("Salad"), FLinearColor(0.2f, 0.7f, 0.2f));
	SpawnStation(F, FVector(-500.f, -560.f, 6.f), 0.f, ELifeStationType::Workstation, TEXT("Barista"), Accent);

	for (const FVector& T : { FVector(220.f, -450.f, 0.f), FVector(220.f, 450.f, 0.f), FVector(380.f, 0.f, 0.f) })
	{
		Cyl(F.TransformPosition(T + FVector(0, 0, 40.f)), FVector(12.f, 12.f, 74.f), DarkMetal);
		Cyl(F.TransformPosition(T + FVector(0, 0, 78.f)), FVector(120.f, 120.f, 5.f), FLinearColor(0.9f, 0.9f, 0.88f));
		for (float A = 0.f; A < 360.f; A += 120.f)
		{
			const FVector Off = FRotator(0.f, A, 0.f).Vector() * 95.f;
			Cyl(F.TransformPosition(T + Off + FVector(0, 0, 25.f)), FVector(45.f, 45.f, 50.f), FLinearColor(0.55f, 0.08f, 0.06f));
		}
	}
}

void ALifeTownBuilder::BuildOffice(const FTransform& F)
{
	const FRoomSpec Spec = RoomSpecFor(ESpecial::Office);
	const FLifeJobDef* Job = ULifeSettings::Get()->FindJob(TEXT("OfficeClerk"));
	const FLinearColor Accent = Job ? Job->AccentColor : FLinearColor::Blue;
	BuildRoom(F, Spec.Depth, Spec.Width, Spec.Height, Spec.Door, FLinearColor(0.70f, 0.72f, 0.75f), FLinearColor(0.25f, 0.27f, 0.30f), FLinearColor(0.08f, 0.22f, 0.50f), TEXT("NOVA CORP"), TEXT("노바 코퍼레이션"));

	SpawnStation(F, FVector(-300.f, 0.f, 6.f), 0.f, ELifeStationType::Workstation, TEXT("OfficeClerk"), Accent);
	for (const FVector& D : { FVector(-300.f, -420.f, 0.f), FVector(-300.f, 420.f, 0.f), FVector(80.f, -420.f, 0.f), FVector(80.f, 420.f, 0.f) })
	{
		LBox(F, D + FVector(0, 0, 75.f), FVector(90.f, 160.f, 6.f), TownPalette::Wood);
		LBox(F, D + FVector(-20.f, 0, 110.f), FVector(5.f, 70.f, 45.f), DarkMetal, 0.f, false);
		LBox(F, D + FVector(0, 0, 37.f), FVector(80.f, 150.f, 70.f), FLinearColor(0.3f, 0.3f, 0.32f));
	}
	SpawnStation(F, FVector(300.f, 600.f, 6.f), -90.f, ELifeStationType::ShopItem, TEXT("EnergyDrink"), FLinearColor(0.1f, 0.8f, 0.9f));
	for (float Y : { -620.f, 620.f })
	{
		Cyl(F.TransformPosition(FVector(-480.f, Y, 30.f)), FVector(50.f, 50.f, 50.f), FLinearColor(0.85f, 0.85f, 0.85f));
		Ball(F.TransformPosition(FVector(-480.f, Y, 100.f)), FVector(80.f, 80.f, 110.f), Leaves[0]);
	}
}

void ALifeTownBuilder::BuildBank(const FTransform& F)
{
	const FRoomSpec Spec = RoomSpecFor(ESpecial::Bank);
	BuildRoom(F, Spec.Depth, Spec.Width, Spec.Height, Spec.Door, FLinearColor(0.78f, 0.75f, 0.66f), FLinearColor(0.60f, 0.58f, 0.52f), FLinearColor(0.10f, 0.20f, 0.40f), TEXT("CITY BANK"), TEXT("시티 은행"));

	SpawnStation(F, FVector(-430.f, -220.f, 6.f), 0.f, ELifeStationType::ATMDeposit, NAME_None, FLinearColor::White);
	SpawnStation(F, FVector(-430.f, 220.f, 6.f), 0.f, ELifeStationType::ATMWithdraw, NAME_None, FLinearColor::White);
	const FLifeJobDef* Guard = ULifeSettings::Get()->FindJob(TEXT("NightGuard"));
	SpawnStation(F, FVector(150.f, -490.f, 6.f), 90.f, ELifeStationType::Workstation, TEXT("NightGuard"), Guard ? Guard->AccentColor : FLinearColor::Gray);
	for (float Y : { -420.f, 420.f })
	{
		Cyl(F.TransformPosition(FVector(300.f, Y, Spec.Height * 0.5f)), FVector(60.f, 60.f, Spec.Height), FLinearColor(0.85f, 0.83f, 0.78f));
	}
}

void ALifeTownBuilder::BuildGarage(const FTransform& F)
{
	const FRoomSpec Spec = RoomSpecFor(ESpecial::Garage);
	const FLifeJobDef* Job = ULifeSettings::Get()->FindJob(TEXT("Mechanic"));
	const FLinearColor Accent = Job ? Job->AccentColor : FLinearColor(1.f, 0.4f, 0.f);
	BuildRoom(F, Spec.Depth, Spec.Width, Spec.Height, Spec.Door, FLinearColor(0.45f, 0.45f, 0.47f), FLinearColor(0.30f, 0.30f, 0.30f), Accent, TEXT("AUTO GARAGE"), TEXT("오토 정비소"));

	SpawnStation(F, FVector(-480.f, -560.f, 6.f), 0.f, ELifeStationType::Workstation, TEXT("Mechanic"), Accent);
	// Two-post lift and tyre stacks.
	for (float Y : { -200.f, 200.f })
	{
		LBox(F, FVector(-150.f, Y, 140.f), FVector(30.f, 30.f, 280.f), Accent);
	}
	LBox(F, FVector(-150.f, 0.f, 120.f), FVector(380.f, 20.f, 10.f), DarkMetal, 0.f, false);
	for (int32 i = 0; i < 4; ++i)
	{
		Cyl(F.TransformPosition(FVector(-520.f, 520.f, 14.f + i * 26.f)), FVector(68.f, 68.f, 26.f), FLinearColor(0.02f, 0.02f, 0.02f));
	}
	LBox(F, FVector(-560.f, 200.f, 55.f), FVector(60.f, 140.f, 110.f), FLinearColor(0.7f, 0.05f, 0.05f));
}

void ALifeTownBuilder::BuildGym(const FTransform& F)
{
	const FRoomSpec Spec = RoomSpecFor(ESpecial::Gym);
	BuildRoom(F, Spec.Depth, Spec.Width, Spec.Height, Spec.Door, FLinearColor(0.20f, 0.22f, 0.25f), FLinearColor(0.12f, 0.12f, 0.13f), FLinearColor(0.85f, 0.75f, 0.05f), TEXT("IRON GYM"), TEXT("아이언 헬스장"));

	for (float Y : { -400.f, 0.f, 400.f })
	{
		SpawnStation(F, FVector(-300.f, Y, 6.f), 0.f, ELifeStationType::Workout, NAME_None, FLinearColor(0.85f, 0.75f, 0.05f));
	}
	SpawnStation(F, FVector(300.f, -600.f, 6.f), 90.f, ELifeStationType::ShopItem, TEXT("ProteinShake"), FLinearColor(0.9f, 0.85f, 0.75f));
	LBox(F, FVector(-530.f, 0.f, 200.f), FVector(4.f, 1100.f, 220.f), FLinearColor(0.6f, 0.65f, 0.7f), 0.f, false);
	for (int32 i = 0; i < 6; ++i)
	{
		Cyl(F.TransformPosition(FVector(150.f, -300.f + i * 120.f, 20.f)), FVector(30.f, 30.f, 30.f), DarkMetal, FRotator(0.f, 0.f, 90.f));
	}
}

// ------------------------------------------------------------------ Life

void ALifeTownBuilder::BuildWalkGraph()
{
	WalkNodes.Reset();
	WalkLinks.Reset();
	auto NodeIndex = [this](int32 BX, int32 BY, int32 CX, int32 CY) { return ((BY * N + BX) * 4) + CY * 2 + CX; };

	for (int32 BY = 0; BY < N; ++BY)
	{
		for (int32 BX = 0; BX < N; ++BX)
		{
			for (int32 CY = 0; CY < 2; ++CY)
			{
				for (int32 CX = 0; CX < 2; ++CX)
				{
					WalkNodes.Add(FVector(BlockMin(BX) + (CX ? B - S * 0.5f : S * 0.5f), BlockMin(BY) + (CY ? B - S * 0.5f : S * 0.5f), 105.f));
				}
			}
		}
	}
	WalkLinks.SetNum(WalkNodes.Num());
	for (int32 BY = 0; BY < N; ++BY)
	{
		for (int32 BX = 0; BX < N; ++BX)
		{
			for (int32 CY = 0; CY < 2; ++CY)
			{
				for (int32 CX = 0; CX < 2; ++CX)
				{
					TArray<int32>& Links = WalkLinks[NodeIndex(BX, BY, CX, CY)];
					Links.Add(NodeIndex(BX, BY, 1 - CX, CY)); // along the block edge
					Links.Add(NodeIndex(BX, BY, CX, 1 - CY));
					if (CX == 1 && BX + 1 < N) Links.Add(NodeIndex(BX + 1, BY, 0, CY)); // crosswalks
					if (CX == 0 && BX > 0)     Links.Add(NodeIndex(BX - 1, BY, 1, CY));
					if (CY == 1 && BY + 1 < N) Links.Add(NodeIndex(BX, BY + 1, CX, 0));
					if (CY == 0 && BY > 0)     Links.Add(NodeIndex(BX, BY - 1, CX, 1));
				}
			}
		}
	}
}

void ALifeTownBuilder::SpawnCitizens()
{
	const ULifeSettings* Settings = ULifeSettings::Get();
	if (WalkNodes.Num() == 0)
	{
		return;
	}
	FActorSpawnParameters Params;
	Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AdjustIfPossibleButAlwaysSpawn;
	FRandomStream Stream(Settings->TownSeed + 17);
	for (int32 i = 0; i < Settings->CitizenCount; ++i)
	{
		const int32 Node = Stream.RandRange(0, WalkNodes.Num() - 1);
		const FVector Jitter(Stream.FRandRange(-80.f, 80.f), Stream.FRandRange(-80.f, 80.f), 0.f);
		ACitizenNPC* Citizen = GetWorld()->SpawnActor<ACitizenNPC>(ACitizenNPC::StaticClass(), WalkNodes[Node] + Jitter, FRotator(0.f, Stream.FRandRange(0.f, 360.f), 0.f), Params);
		if (!Citizen)
		{
			continue;
		}
		const FString Name = Settings->CitizenNames.IsValidIndex(i) ? Settings->CitizenNames[i] : FString::Printf(TEXT("주민 %d"), i + 1);
		Citizen->InitCitizen(Name, FLifeAppearance::Random(Stream), this, Node, Settings->TownSeed + i * 131);
	}
}

void ALifeTownBuilder::SpawnTraffic()
{
	const ULifeSettings* Settings = ULifeSettings::Get();
	FActorSpawnParameters Params;
	Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;

	// The player's own car.
	if (ALifeVehicle* Car = GetWorld()->SpawnActor<ALifeVehicle>(ALifeVehicle::StaticClass(), PlayerCarSpot, Params))
	{
		Car->SetPaint(FLinearColor(0.55f, 0.02f, 0.03f));
	}

	// Traffic: each car loops the lanes around one block (right-hand traffic).
	FRandomStream Stream(Settings->TownSeed + 99);
	const float LaneOffset = R * 0.25f;
	for (int32 i = 0; i < Settings->TrafficCarCount; ++i)
	{
		const int32 BX = Stream.RandRange(0, N - 1);
		const int32 BY = Stream.RandRange(0, N - 1);
		const FVector Corners[4] = {
			FVector(RoadCenter(BX), RoadCenter(BY), 0.f), FVector(RoadCenter(BX + 1), RoadCenter(BY), 0.f),
			FVector(RoadCenter(BX + 1), RoadCenter(BY + 1), 0.f), FVector(RoadCenter(BX), RoadCenter(BY + 1), 0.f) };
		auto RightOf = [](const FVector& Dir) { return FVector(-Dir.Y, Dir.X, 0.f); };

		TArray<FVector> Route;
		for (int32 c = 0; c < 4; ++c)
		{
			const FVector In = (Corners[c] - Corners[(c + 3) % 4]).GetSafeNormal();
			const FVector Out = (Corners[(c + 1) % 4] - Corners[c]).GetSafeNormal();
			FVector P = Corners[c] + (RightOf(In) + RightOf(Out)) * LaneOffset;
			P.Z = 90.f;
			Route.Add(P);
		}

		const int32 Start = Stream.RandRange(0, 3);
		const FVector From = Route[Start];
		const FVector To = Route[(Start + 1) % 4];
		const FVector Pos = FMath::Lerp(From, To, Stream.FRandRange(0.2f, 0.7f));
		ALifeVehicle* Car = GetWorld()->SpawnActor<ALifeVehicle>(ALifeVehicle::StaticClass(), Pos, (To - From).Rotation(), Params);
		if (Car)
		{
			Car->SetPaint(FLinearColor::MakeFromHSV8(static_cast<uint8>(Stream.RandRange(0, 255)), static_cast<uint8>(Stream.RandRange(40, 230)), static_cast<uint8>(Stream.RandRange(40, 220))));
			Car->SetupAsTraffic(Route, Start);
		}
	}
}
