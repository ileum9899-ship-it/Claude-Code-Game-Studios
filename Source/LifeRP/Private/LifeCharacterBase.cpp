// Life RP - shared humanoid body.

#include "LifeCharacterBase.h"
#include "LifeVisuals.h"
#include "Components/CapsuleComponent.h"
#include "Components/SkeletalMeshComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "UObject/ConstructorHelpers.h"

FLifeAppearance FLifeAppearance::Random(FRandomStream& Stream)
{
	static const FLinearColor Skins[] = {
		FLinearColor(0.93f, 0.75f, 0.62f), FLinearColor(0.85f, 0.63f, 0.48f), FLinearColor(0.70f, 0.48f, 0.33f),
		FLinearColor(0.52f, 0.34f, 0.22f), FLinearColor(0.36f, 0.23f, 0.15f), FLinearColor(0.25f, 0.16f, 0.11f) };
	static const FLinearColor Hairs[] = {
		FLinearColor(0.03f, 0.02f, 0.02f), FLinearColor(0.18f, 0.10f, 0.05f), FLinearColor(0.45f, 0.28f, 0.12f),
		FLinearColor(0.85f, 0.70f, 0.40f), FLinearColor(0.55f, 0.15f, 0.08f), FLinearColor(0.75f, 0.75f, 0.78f) };

	FLifeAppearance A;
	A.Skin = Skins[Stream.RandRange(0, UE_ARRAY_COUNT(Skins) - 1)];
	A.Hair = Hairs[Stream.RandRange(0, UE_ARRAY_COUNT(Hairs) - 1)];
	A.Shirt = FLinearColor::MakeFromHSV8(static_cast<uint8>(Stream.RandRange(0, 255)), static_cast<uint8>(Stream.RandRange(90, 220)), static_cast<uint8>(Stream.RandRange(110, 230)));
	A.Pants = FLinearColor::MakeFromHSV8(static_cast<uint8>(Stream.RandRange(140, 175)), static_cast<uint8>(Stream.RandRange(30, 160)), static_cast<uint8>(Stream.RandRange(40, 120)));
	A.Shoes = Stream.FRand() < 0.5f ? FLinearColor(0.04f, 0.04f, 0.04f) : FLinearColor(0.9f, 0.9f, 0.9f);
	A.HeightScale = Stream.FRandRange(0.92f, 1.08f);
	return A;
}

ALifeCharacterBase::ALifeCharacterBase()
{
	PrimaryActorTick.bCanEverTick = true;

	GetCapsuleComponent()->InitCapsuleSize(34.f, 90.f);
	GetMesh()->SetVisibility(false);
	GetMesh()->SetCollisionEnabled(ECollisionEnabled::NoCollision);

	static ConstructorHelpers::FObjectFinder<UStaticMesh> CubeMesh(TEXT("/Engine/BasicShapes/Cube.Cube"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> SphereMesh(TEXT("/Engine/BasicShapes/Sphere.Sphere"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> CylinderMesh(TEXT("/Engine/BasicShapes/Cylinder.Cylinder"));
	UStaticMesh* Cube = CubeMesh.Object;
	UStaticMesh* Sphere = SphereMesh.Object;
	UStaticMesh* Cyl = CylinderMesh.Object;

	// BodyRoot sits at the feet. Everything below is in centimetres from the feet.
	BodyRoot = CreateDefaultSubobject<USceneComponent>(TEXT("BodyRoot"));
	BodyRoot->SetupAttachment(GetCapsuleComponent());
	BodyRoot->SetRelativeLocation(FVector(0, 0, -90.f));

	HipL = CreateDefaultSubobject<USceneComponent>(TEXT("HipL"));
	HipL->SetupAttachment(BodyRoot);
	HipL->SetRelativeLocation(FVector(0, -10.f, 92.f));
	HipR = CreateDefaultSubobject<USceneComponent>(TEXT("HipR"));
	HipR->SetupAttachment(BodyRoot);
	HipR->SetRelativeLocation(FVector(0, 10.f, 92.f));

	PantsParts.Add(MakePart(TEXT("LegL"), HipL, Cyl, FVector(0, 0, -43.f), FVector(15, 15, 86)));
	PantsParts.Add(MakePart(TEXT("LegR"), HipR, Cyl, FVector(0, 0, -43.f), FVector(15, 15, 86)));
	ShoeParts.Add(MakePart(TEXT("ShoeL"), HipL, Cube, FVector(5, 0, -87.f), FVector(26, 12, 9)));
	ShoeParts.Add(MakePart(TEXT("ShoeR"), HipR, Cube, FVector(5, 0, -87.f), FVector(26, 12, 9)));
	PantsParts.Add(MakePart(TEXT("Pelvis"), BodyRoot, Cyl, FVector(0, 0, 96.f), FVector(24, 36, 18)));

	Torso = CreateDefaultSubobject<USceneComponent>(TEXT("Torso"));
	Torso->SetupAttachment(BodyRoot);
	Torso->SetRelativeLocation(FVector(0, 0, 104.f));
	ShirtParts.Add(MakePart(TEXT("Chest"), Torso, Cyl, FVector(0, 0, 21.f), FVector(25, 38, 44)));
	ShirtParts.Add(MakePart(TEXT("Shoulders"), Torso, Sphere, FVector(0, 0, 41.f), FVector(26, 46, 16)));
	SkinParts.Add(MakePart(TEXT("Neck"), Torso, Cyl, FVector(0, 0, 50.f), FVector(10, 10, 10)));
	SkinParts.Add(MakePart(TEXT("Head"), Torso, Sphere, FVector(1, 0, 66.f), FVector(23, 21, 26)));
	HairParts.Add(MakePart(TEXT("Hair"), Torso, Sphere, FVector(-2, 0, 72.f), FVector(24, 22, 20)));
	DetailParts.Add(MakePart(TEXT("EyeL"), Torso, Sphere, FVector(11, -4.5f, 68.f), FVector(3, 3, 3.5f)));
	DetailParts.Add(MakePart(TEXT("EyeR"), Torso, Sphere, FVector(11, 4.5f, 68.f), FVector(3, 3, 3.5f)));
	SkinParts.Add(MakePart(TEXT("Nose"), Torso, Sphere, FVector(12, 0, 64.f), FVector(4, 3, 4)));

	ShoulderL = CreateDefaultSubobject<USceneComponent>(TEXT("ShoulderL"));
	ShoulderL->SetupAttachment(Torso);
	ShoulderL->SetRelativeLocation(FVector(0, -24.f, 40.f));
	ShoulderR = CreateDefaultSubobject<USceneComponent>(TEXT("ShoulderR"));
	ShoulderR->SetupAttachment(Torso);
	ShoulderR->SetRelativeLocation(FVector(0, 24.f, 40.f));

	ShirtParts.Add(MakePart(TEXT("UpperArmL"), ShoulderL, Cyl, FVector(0, 0, -15.f), FVector(11, 11, 30)));
	ShirtParts.Add(MakePart(TEXT("UpperArmR"), ShoulderR, Cyl, FVector(0, 0, -15.f), FVector(11, 11, 30)));
	SkinParts.Add(MakePart(TEXT("ForearmL"), ShoulderL, Cyl, FVector(0, 0, -44.f), FVector(9, 9, 30)));
	SkinParts.Add(MakePart(TEXT("ForearmR"), ShoulderR, Cyl, FVector(0, 0, -44.f), FVector(9, 9, 30)));
	SkinParts.Add(MakePart(TEXT("HandL"), ShoulderL, Sphere, FVector(0, 0, -62.f), FVector(10, 8, 11)));
	SkinParts.Add(MakePart(TEXT("HandR"), ShoulderR, Sphere, FVector(0, 0, -62.f), FVector(10, 8, 11)));

	UCharacterMovementComponent* Move = GetCharacterMovement();
	Move->bOrientRotationToMovement = true;
	Move->RotationRate = FRotator(0.f, 540.f, 0.f);
	Move->MaxWalkSpeed = 220.f;
	Move->BrakingDecelerationWalking = 1600.f;
	Move->JumpZVelocity = 460.f;
	Move->AirControl = 0.3f;
	bUseControllerRotationYaw = false;
}

UStaticMeshComponent* ALifeCharacterBase::MakePart(const TCHAR* Name, USceneComponent* Parent, UStaticMesh* Mesh,
	const FVector& Location, const FVector& Size, const FRotator& Rotation)
{
	UStaticMeshComponent* Part = CreateDefaultSubobject<UStaticMeshComponent>(Name);
	Part->SetupAttachment(Parent);
	Part->SetStaticMesh(Mesh);
	Part->SetRelativeLocation(Location);
	Part->SetRelativeRotation(Rotation);
	Part->SetRelativeScale3D(Size / 100.f);
	Part->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	Part->SetGenerateOverlapEvents(false);
	Part->SetCanEverAffectNavigation(false);
	Part->bReceivesDecals = false;
	return Part;
}

void ALifeCharacterBase::BeginPlay()
{
	Super::BeginPlay();
	ApplyAppearance(Appearance);
}

void ALifeCharacterBase::ApplyAppearance(const FLifeAppearance& InAppearance)
{
	Appearance = InAppearance;
	auto TintAll = [](const TArray<TObjectPtr<UStaticMeshComponent>>& Parts, const FLinearColor& Color)
	{
		for (UStaticMeshComponent* Part : Parts)
		{
			LifeVisuals::Tint(Part, Color);
		}
	};
	TintAll(SkinParts, Appearance.Skin);
	TintAll(HairParts, Appearance.Hair);
	TintAll(ShirtParts, Appearance.Shirt);
	TintAll(PantsParts, Appearance.Pants);
	TintAll(ShoeParts, Appearance.Shoes);
	TintAll(DetailParts, FLinearColor(0.02f, 0.02f, 0.03f));
	BodyRoot->SetRelativeScale3D(FVector(1.f, 1.f, Appearance.HeightScale));
}

void ALifeCharacterBase::SetProceduralBodyVisible(bool bVisible)
{
	BodyRoot->SetVisibility(bVisible, true);
}

void ALifeCharacterBase::SetLyingDown(bool bLying)
{
	bLyingDown = bLying;
	// Pitch +90 tips the body onto its back with the head toward local -X.
	BodyRoot->SetRelativeRotation(bLying ? FRotator(90.f, 0.f, 0.f) : FRotator::ZeroRotator);
	// Raise by the torso half-depth so the back rests on the surface instead of sinking into it.
	BodyRoot->SetRelativeLocation(FVector(0.f, 0.f, bLying ? -77.f : -90.f));
}

void ALifeCharacterBase::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);

	IdleTime += DeltaSeconds;
	const float Speed = GetVelocity().Size2D();
	const bool bFalling = GetCharacterMovement()->IsFalling();
	const float Stride = FMath::Clamp(Speed / 450.f, 0.f, 1.f);

	// Phase advances with distance travelled so feet don't skate.
	WalkPhase += DeltaSeconds * (Speed / 38.f);
	const float Swing = bLyingDown ? 0.f : FMath::Sin(WalkPhase) * (18.f + 30.f * Stride) * FMath::Min(1.f, Speed / 60.f);

	float LegL = Swing, LegR = -Swing;
	float ArmL = -Swing * 0.8f, ArmR = Swing * 0.8f;
	if (bFalling && !bLyingDown)
	{
		LegL = 25.f; LegR = -10.f; ArmL = 120.f; ArmR = 120.f;
	}
	else if (bBusyAnim && Speed < 20.f)
	{
		const float Work = FMath::Sin(IdleTime * 6.f) * 12.f;
		ArmL = 60.f + Work; ArmR = 60.f - Work;
	}

	const float Blend = FMath::Clamp(DeltaSeconds * 12.f, 0.f, 1.f);
	auto Drive = [Blend](USceneComponent* Pivot, float TargetPitch, float Roll)
	{
		const FRotator Current = Pivot->GetRelativeRotation();
		Pivot->SetRelativeRotation(FRotator(FMath::Lerp(Current.Pitch, TargetPitch, Blend), 0.f, Roll));
	};
	Drive(HipL, LegL, 0.f);
	Drive(HipR, LegR, 0.f);
	Drive(ShoulderL, ArmL, -6.f);
	Drive(ShoulderR, ArmR, 6.f);

	// Breathing + walk bob.
	const float Bob = FMath::Abs(FMath::Cos(WalkPhase)) * 2.5f * Stride;
	const float Breath = FMath::Sin(IdleTime * 1.8f) * 0.6f;
	Torso->SetRelativeLocation(FVector(Stride * 3.f, 0.f, 104.f + Bob + Breath));
}
