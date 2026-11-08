// Life RP - drivable car and traffic.

#include "LifeVehicle.h"
#include "LifeGameState.h"
#include "LifePlayerCharacter.h"
#include "LifePlayerController.h"
#include "LifeSettings.h"
#include "LifeVisuals.h"
#include "Camera/CameraComponent.h"
#include "Components/BoxComponent.h"
#include "Components/PointLightComponent.h"
#include "Components/SpotLightComponent.h"
#include "Components/StaticMeshComponent.h"
#include "EnhancedInputComponent.h"
#include "Engine/StaticMesh.h"
#include "Engine/World.h"
#include "GameFramework/SpringArmComponent.h"
#include "InputActionValue.h"
#include "UObject/ConstructorHelpers.h"

ALifeVehicle::ALifeVehicle()
{
	PrimaryActorTick.bCanEverTick = true;
	AutoPossessAI = EAutoPossessAI::Disabled;

	static ConstructorHelpers::FObjectFinder<UStaticMesh> CubeMesh(TEXT("/Engine/BasicShapes/Cube.Cube"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> SphereMesh(TEXT("/Engine/BasicShapes/Sphere.Sphere"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> CylinderMesh(TEXT("/Engine/BasicShapes/Cylinder.Cylinder"));
	UStaticMesh* Cube = CubeMesh.Object;
	UStaticMesh* Sphere = SphereMesh.Object;
	UStaticMesh* Cyl = CylinderMesh.Object;

	// Origin sits RideHeight (85 cm) above the road; the box starts 25 cm above it so curbs don't block.
	Collision = CreateDefaultSubobject<UBoxComponent>(TEXT("Collision"));
	Collision->InitBoxExtent(FVector(228.f, 96.f, 60.f));
	Collision->SetCollisionProfileName(TEXT("Vehicle"));
	Collision->SetMobility(EComponentMobility::Movable);
	SetRootComponent(Collision);

	BodyPivot = CreateDefaultSubobject<USceneComponent>(TEXT("BodyPivot"));
	BodyPivot->SetupAttachment(Collision);

	// Body: lower shell, hood/trunk slopes, cabin glass, roof, bumpers, mirrors.
	PaintParts.Add(Part(TEXT("Shell"), BodyPivot, Cube, FVector(0, 0, -22), FVector(452, 190, 62)));
	PaintParts.Add(Part(TEXT("Hood"), BodyPivot, Cube, FVector(150, 0, 12), FVector(150, 184, 14), FRotator(-4, 0, 0)));
	PaintParts.Add(Part(TEXT("Trunk"), BodyPivot, Cube, FVector(-175, 0, 14), FVector(100, 184, 16), FRotator(3, 0, 0)));
	GlassParts.Add(Part(TEXT("Cabin"), BodyPivot, Cube, FVector(-25, 0, 42), FVector(225, 172, 58)));
	GlassParts.Add(Part(TEXT("Windshield"), BodyPivot, Cube, FVector(95, 0, 40), FVector(45, 166, 52), FRotator(-38, 0, 0)));
	PaintParts.Add(Part(TEXT("Roof"), BodyPivot, Cube, FVector(-30, 0, 73), FVector(190, 168, 7)));
	PaintParts.Add(Part(TEXT("PillarL"), BodyPivot, Cube, FVector(-25, -84, 42), FVector(20, 5, 58)));
	PaintParts.Add(Part(TEXT("PillarR"), BodyPivot, Cube, FVector(-25, 84, 42), FVector(20, 5, 58)));
	TrimParts.Add(Part(TEXT("BumperF"), BodyPivot, Cube, FVector(228, 0, -38), FVector(14, 194, 26)));
	TrimParts.Add(Part(TEXT("BumperR"), BodyPivot, Cube, FVector(-228, 0, -38), FVector(14, 194, 26)));
	TrimParts.Add(Part(TEXT("Grille"), BodyPivot, Cube, FVector(226, 0, -14), FVector(6, 110, 18)));
	TrimParts.Add(Part(TEXT("SkirtL"), BodyPivot, Cube, FVector(0, -96, -48), FVector(300, 6, 12)));
	TrimParts.Add(Part(TEXT("SkirtR"), BodyPivot, Cube, FVector(0, 96, -48), FVector(300, 6, 12)));
	PaintParts.Add(Part(TEXT("MirrorL"), BodyPivot, Cube, FVector(75, -100, 22), FVector(14, 18, 12)));
	PaintParts.Add(Part(TEXT("MirrorR"), BodyPivot, Cube, FVector(75, 100, 22), FVector(14, 18, 12)));
	HeadlightParts.Add(Part(TEXT("HeadL"), BodyPivot, Sphere, FVector(222, -66, -6), FVector(10, 34, 14)));
	HeadlightParts.Add(Part(TEXT("HeadR"), BodyPivot, Sphere, FVector(222, 66, -6), FVector(10, 34, 14)));
	TaillightParts.Add(Part(TEXT("TailL"), BodyPivot, Cube, FVector(-226, -68, -4), FVector(4, 44, 12)));
	TaillightParts.Add(Part(TEXT("TailR"), BodyPivot, Cube, FVector(-226, 68, -4), FVector(4, 44, 12)));

	const FVector WheelPos[4] = {
		FVector(148, -86, -51), FVector(148, 86, -51), FVector(-142, -86, -51), FVector(-142, 86, -51) };
	for (int32 i = 0; i < 4; ++i)
	{
		USceneComponent* SteerPivot = CreateDefaultSubobject<USceneComponent>(FName(*FString::Printf(TEXT("WheelSteer%d"), i)));
		SteerPivot->SetupAttachment(Collision);
		SteerPivot->SetRelativeLocation(WheelPos[i]);
		USceneComponent* Spin = CreateDefaultSubobject<USceneComponent>(FName(*FString::Printf(TEXT("WheelSpin%d"), i)));
		Spin->SetupAttachment(SteerPivot);
		RubberParts.Add(Part(*FString::Printf(TEXT("Tyre%d"), i), Spin, Cyl, FVector::ZeroVector, FVector(68, 68, 26), FRotator(0, 0, 90)));
		const float Out = WheelPos[i].Y < 0.f ? -14.f : 14.f;
		TrimParts.Add(Part(*FString::Printf(TEXT("Rim%d"), i), Spin, Cyl, FVector(0, Out, 0), FVector(44, 44, 2), FRotator(0, 0, 90)));
		TrimParts.Add(Part(*FString::Printf(TEXT("Spoke%d"), i), Spin, Cube, FVector(0, Out * 1.1f, 0), FVector(40, 2, 7)));
		WheelSteer.Add(SteerPivot);
		WheelSpin.Add(Spin);
	}

	HeadlightL = CreateDefaultSubobject<USpotLightComponent>(TEXT("HeadlightL"));
	HeadlightL->SetupAttachment(BodyPivot);
	HeadlightL->SetRelativeLocation(FVector(232, -66, -6));
	HeadlightR = CreateDefaultSubobject<USpotLightComponent>(TEXT("HeadlightR"));
	HeadlightR->SetupAttachment(BodyPivot);
	HeadlightR->SetRelativeLocation(FVector(232, 66, -6));
	for (USpotLightComponent* Light : { HeadlightL.Get(), HeadlightR.Get() })
	{
		Light->SetMobility(EComponentMobility::Movable);
		Light->SetRelativeRotation(FRotator(-7.f, 0.f, 0.f));
		Light->SetIntensityUnits(ELightUnits::Candelas);
		Light->SetIntensity(0.f);
		Light->SetAttenuationRadius(3500.f);
		Light->SetInnerConeAngle(14.f);
		Light->SetOuterConeAngle(32.f);
		Light->SetLightColor(FLinearColor(1.f, 0.95f, 0.85f));
		Light->SetCastShadows(false);
	}
	TailGlow = CreateDefaultSubobject<UPointLightComponent>(TEXT("TailGlow"));
	TailGlow->SetupAttachment(BodyPivot);
	TailGlow->SetRelativeLocation(FVector(-250, 0, -4));
	TailGlow->SetMobility(EComponentMobility::Movable);
	TailGlow->SetIntensityUnits(ELightUnits::Candelas);
	TailGlow->SetIntensity(0.f);
	TailGlow->SetAttenuationRadius(250.f);
	TailGlow->SetLightColor(FLinearColor(1.f, 0.05f, 0.02f));
	TailGlow->SetCastShadows(false);

	CameraBoom = CreateDefaultSubobject<USpringArmComponent>(TEXT("CameraBoom"));
	CameraBoom->SetupAttachment(Collision);
	CameraBoom->TargetArmLength = 700.f;
	CameraBoom->SocketOffset = FVector(0.f, 0.f, 170.f);
	CameraBoom->bUsePawnControlRotation = true;
	CameraBoom->bEnableCameraLag = true;
	CameraBoom->CameraLagSpeed = 8.f;
	CameraBoom->bEnableCameraRotationLag = true;
	CameraBoom->CameraRotationLagSpeed = 6.f;

	Camera = CreateDefaultSubobject<UCameraComponent>(TEXT("Camera"));
	Camera->SetupAttachment(CameraBoom, USpringArmComponent::SocketName);
	Camera->SetFieldOfView(85.f);
}

UStaticMeshComponent* ALifeVehicle::Part(const TCHAR* Name, USceneComponent* Parent, UStaticMesh* Mesh, const FVector& Loc, const FVector& Size, const FRotator& Rot)
{
	UStaticMeshComponent* C = CreateDefaultSubobject<UStaticMeshComponent>(Name);
	C->SetupAttachment(Parent);
	C->SetStaticMesh(Mesh);
	C->SetRelativeLocation(Loc);
	C->SetRelativeRotation(Rot);
	C->SetRelativeScale3D(Size / 100.f);
	C->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	C->SetCanEverAffectNavigation(false);
	return C;
}

void ALifeVehicle::BeginPlay()
{
	Super::BeginPlay();
	SetPaint(Paint);
	for (UStaticMeshComponent* C : GlassParts)     LifeVisuals::Tint(C, FLinearColor(0.02f, 0.03f, 0.05f));
	for (UStaticMeshComponent* C : TrimParts)      LifeVisuals::Tint(C, FLinearColor(0.05f, 0.05f, 0.055f));
	for (UStaticMeshComponent* C : RubberParts)    LifeVisuals::Tint(C, FLinearColor(0.015f, 0.015f, 0.015f));
	for (UStaticMeshComponent* C : HeadlightParts) LifeVisuals::Tint(C, FLinearColor(0.95f, 0.95f, 0.9f));
	for (UStaticMeshComponent* C : TaillightParts) LifeVisuals::Tint(C, FLinearColor(0.6f, 0.02f, 0.02f));
}

void ALifeVehicle::SetPaint(const FLinearColor& Color)
{
	Paint = Color;
	for (UStaticMeshComponent* C : PaintParts)
	{
		LifeVisuals::Tint(C, Paint);
	}
}

void ALifeVehicle::SetupAsTraffic(const TArray<FVector>& Route, int32 StartIndex)
{
	bTraffic = Route.Num() >= 2;
	TrafficRoute = Route;
	RouteIndex = Route.Num() > 0 ? (StartIndex + 1) % Route.Num() : 0;
	if (bTraffic)
	{
		// Traffic cars are WorldDynamic and ignore each other, so crossing loops can never
		// gridlock; they still block (and brake for) pedestrians and the player's car.
		Collision->SetCollisionObjectType(ECC_WorldDynamic);
		Collision->SetCollisionResponseToChannel(ECC_WorldDynamic, ECR_Ignore);
	}
}

// ------------------------------------------------------------------ Interaction

FString ALifeVehicle::GetInteractionPrompt(const ALifePlayerCharacter* Player) const
{
	return bTraffic ? TEXT("Someone else's car") : TEXT("Drive");
}

bool ALifeVehicle::CanInteract(const ALifePlayerCharacter* Player) const
{
	return !bTraffic && !Driver.IsValid();
}

void ALifeVehicle::Interact(ALifePlayerCharacter* Player)
{
	if (Player && CanInteract(Player))
	{
		Player->EnterVehicle(this);
	}
}

// ------------------------------------------------------------------ Input

void ALifeVehicle::SetupPlayerInputComponent(UInputComponent* PlayerInputComponent)
{
	Super::SetupPlayerInputComponent(PlayerInputComponent);
	ALifePlayerController* PC = Cast<ALifePlayerController>(GetController());
	UEnhancedInputComponent* EIC = Cast<UEnhancedInputComponent>(PlayerInputComponent);
	if (!PC || !EIC)
	{
		return;
	}
	const ULifeInputSet* In = PC->GetInputSet();
	EIC->BindAction(In->Throttle, ETriggerEvent::Triggered, this, &ALifeVehicle::ThrottleInput);
	EIC->BindAction(In->Throttle, ETriggerEvent::Completed, this, &ALifeVehicle::ThrottleInput);
	EIC->BindAction(In->Steer, ETriggerEvent::Triggered, this, &ALifeVehicle::SteerInput);
	EIC->BindAction(In->Steer, ETriggerEvent::Completed, this, &ALifeVehicle::SteerInput);
	EIC->BindAction(In->Handbrake, ETriggerEvent::Started, this, &ALifeVehicle::HandbrakeStart);
	EIC->BindAction(In->Handbrake, ETriggerEvent::Completed, this, &ALifeVehicle::HandbrakeStop);
	EIC->BindAction(In->Look, ETriggerEvent::Triggered, this, &ALifeVehicle::Look);
	EIC->BindAction(In->ExitVehicle, ETriggerEvent::Started, this, &ALifeVehicle::ExitPressed);
}

void ALifeVehicle::ThrottleInput(const FInputActionValue& Value) { Throttle = FMath::Clamp(Value.Get<float>(), -1.f, 1.f); }
void ALifeVehicle::SteerInput(const FInputActionValue& Value)    { Steer = FMath::Clamp(Value.Get<float>(), -1.f, 1.f); }

void ALifeVehicle::Look(const FInputActionValue& Value)
{
	const FVector2D Axis = Value.Get<FVector2D>();
	AddControllerYawInput(Axis.X);
	AddControllerPitchInput(Axis.Y);
}

void ALifeVehicle::ExitPressed()
{
	ALifePlayerCharacter* D = Driver.Get();
	if (!D)
	{
		return;
	}
	if (FMath::Abs(Speed) > 350.f)
	{
		LifeNotify(this, TEXT("Slow down before getting out."), FLinearColor(1.f, 0.85f, 0.4f));
		return;
	}
	Speed = 0.f;
	Throttle = 0.f;
	Steer = 0.f;

	const FRotator Yaw(0.f, GetActorRotation().Yaw, 0.f);
	const FVector Left = -FRotationMatrix(Yaw).GetUnitAxis(EAxis::Y);
	FVector Candidates[3] = {
		GetActorLocation() + Left * 175.f + FVector(0, 0, 20.f),
		GetActorLocation() - Left * 175.f + FVector(0, 0, 20.f),
		GetActorLocation() + FVector(0, 0, 170.f) };
	for (FVector& Spot : Candidates)
	{
		if (GetWorld()->FindTeleportSpot(D, Spot, Yaw))
		{
			D->ExitVehicle(this, Spot);
			return;
		}
	}
	D->ExitVehicle(this, Candidates[2]);
}

// ------------------------------------------------------------------ Simulation

void ALifeVehicle::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (bTraffic)
	{
		DriveTraffic(DeltaSeconds);
	}
	else if (!Driver.IsValid())
	{
		Throttle = 0.f;
		Steer = 0.f;
		bHandbrake = true;
	}
	Simulate(DeltaSeconds);
	FollowGround(DeltaSeconds);
	UpdateVisuals(DeltaSeconds);
}

void ALifeVehicle::DriveTraffic(float DeltaSeconds)
{
	if (TrafficRoute.Num() < 2)
	{
		return;
	}
	const FVector Target = TrafficRoute[RouteIndex];
	FVector ToTarget = Target - GetActorLocation();
	ToTarget.Z = 0.f;
	if (ToTarget.Size() < 380.f)
	{
		RouteIndex = (RouteIndex + 1) % TrafficRoute.Num();
		return;
	}

	const float DesiredYaw = ToTarget.Rotation().Yaw;
	const float DeltaYaw = FMath::FindDeltaAngleDegrees(GetActorRotation().Yaw, DesiredYaw);
	Steer = FMath::Clamp(DeltaYaw / 25.f, -1.f, 1.f);

	float TargetSpeed = ULifeSettings::Get()->TrafficCruiseSpeed;
	if (FMath::Abs(DeltaYaw) > 30.f)
	{
		TargetSpeed *= 0.45f; // slow for corners
	}

	// Brake for pedestrians and cars ahead.
	const FVector Forward = FRotator(0.f, GetActorRotation().Yaw, 0.f).Vector();
	const FVector Start = GetActorLocation() + Forward * 240.f;
	const FVector End = Start + Forward * (350.f + FMath::Max(0.f, Speed) * 0.7f);
	FCollisionObjectQueryParams Objects;
	Objects.AddObjectTypesToQuery(ECC_Pawn);
	Objects.AddObjectTypesToQuery(ECC_Vehicle);
	Objects.AddObjectTypesToQuery(ECC_WorldDynamic);
	FCollisionQueryParams Params(SCENE_QUERY_STAT(LifeTrafficLook), false, this);
	TArray<FHitResult> Hits;
	GetWorld()->SweepMultiByObjectType(Hits, Start, End, FQuat::Identity, Objects, FCollisionShape::MakeSphere(70.f), Params);
	for (const FHitResult& Hit : Hits)
	{
		const AActor* Other = Hit.GetActor();
		if (!Other || Other->IsHidden())
		{
			continue;
		}
		const ALifeVehicle* OtherCar = Cast<ALifeVehicle>(Other);
		if (OtherCar && OtherCar->IsTraffic())
		{
			// Only queue behind traffic heading the same way; cross traffic passes through.
			if (FMath::Abs(FMath::FindDeltaAngleDegrees(OtherCar->GetActorRotation().Yaw, GetActorRotation().Yaw)) > 50.f)
			{
				continue;
			}
		}
		else if (!OtherCar && !Cast<APawn>(Other))
		{
			continue;
		}
		TargetSpeed = 0.f;
		break;
	}

	Throttle = FMath::Clamp((TargetSpeed - Speed) / 300.f, -1.f, 1.f);
	bHandbrake = false;
}

void ALifeVehicle::Simulate(float DeltaSeconds)
{
	const ULifeSettings* S = ULifeSettings::Get();

	if (bHandbrake)
	{
		Speed = FMath::FInterpConstantTo(Speed, 0.f, DeltaSeconds, S->CarBrakeDeceleration * 1.2f);
	}
	else if (Throttle * Speed < 0.f)
	{
		Speed = FMath::FInterpConstantTo(Speed, 0.f, DeltaSeconds, S->CarBrakeDeceleration * FMath::Abs(Throttle));
	}
	else if (FMath::Abs(Throttle) > 0.05f)
	{
		Speed += Throttle * S->CarAcceleration * DeltaSeconds;
	}
	else
	{
		Speed = FMath::FInterpConstantTo(Speed, 0.f, DeltaSeconds, 250.f); // rolling resistance
	}
	Speed -= Speed * S->CarDrag * DeltaSeconds * FMath::Abs(Speed) / S->CarMaxSpeed;
	Speed = FMath::Clamp(Speed, -S->CarMaxReverseSpeed, S->CarMaxSpeed);

	// Steering authority grows with speed; reversing steers the other way.
	const float Authority = FMath::Clamp(FMath::Abs(Speed) / 500.f, 0.f, 1.f) * (Speed >= 0.f ? 1.f : -1.f);
	const float HighSpeedDamp = FMath::Lerp(1.f, 0.55f, FMath::Clamp(FMath::Abs(Speed) / S->CarMaxSpeed, 0.f, 1.f));
	const float YawRate = Steer * S->CarSteerDegreesPerSecond * Authority * HighSpeedDamp;
	AddActorWorldRotation(FRotator(0.f, YawRate * DeltaSeconds, 0.f));

	const FVector Forward = FRotator(0.f, GetActorRotation().Yaw, 0.f).Vector();
	FHitResult Hit;
	AddActorWorldOffset(Forward * Speed * DeltaSeconds, true, &Hit);
	if (Hit.bBlockingHit)
	{
		if (Driver.IsValid() && FMath::Abs(Speed) > 900.f)
		{
			LifeNotify(this, TEXT("Crash! Drive carefully."), FLinearColor(1.f, 0.4f, 0.3f));
		}
		// Slide along the obstacle instead of sticking to it.
		const FVector Slide = FVector::VectorPlaneProject(Forward * Speed * DeltaSeconds * (1.f - Hit.Time), Hit.Normal);
		AddActorWorldOffset(Slide, true);
		Speed *= -0.2f;
	}
}

void ALifeVehicle::FollowGround(float DeltaSeconds)
{
	const FRotator Yaw(0.f, GetActorRotation().Yaw, 0.f);
	const FVector Forward = Yaw.Vector();
	FCollisionObjectQueryParams Ground(ECC_WorldStatic);
	FCollisionQueryParams Params(SCENE_QUERY_STAT(LifeCarGround), false, this);
	if (Driver.IsValid())
	{
		Params.AddIgnoredActor(Driver.Get());
	}

	auto SampleZ = [&](const FVector& Offset, float& OutZ) -> bool
	{
		const FVector P = GetActorLocation() + Offset;
		FHitResult Hit;
		if (GetWorld()->LineTraceSingleByObjectType(Hit, P + FVector(0, 0, 150.f), P - FVector(0, 0, 600.f), Ground, Params))
		{
			OutZ = Hit.ImpactPoint.Z;
			return true;
		}
		return false;
	};

	float FrontZ = 0.f, RearZ = 0.f;
	const bool bFront = SampleZ(Forward * 145.f, FrontZ);
	const bool bRear = SampleZ(-Forward * 145.f, RearZ);
	if (!bFront && !bRear)
	{
		return;
	}
	if (!bFront) FrontZ = RearZ;
	if (!bRear) RearZ = FrontZ;

	const float TargetZ = (FrontZ + RearZ) * 0.5f + RideHeight;
	const float TargetPitch = FMath::RadiansToDegrees(FMath::Atan2(FrontZ - RearZ, 290.f));
	FVector Loc = GetActorLocation();
	Loc.Z = FMath::FInterpTo(Loc.Z, TargetZ, DeltaSeconds, 14.f);
	const float Pitch = FMath::FInterpTo(GetActorRotation().Pitch, TargetPitch, DeltaSeconds, 10.f);
	SetActorLocationAndRotation(Loc, FRotator(Pitch, Yaw.Yaw, 0.f));
}

void ALifeVehicle::UpdateVisuals(float DeltaSeconds)
{
	SteerVisual = FMath::FInterpTo(SteerVisual, Steer * 28.f, DeltaSeconds, 10.f);
	WheelRoll = FMath::Fmod(WheelRoll - FMath::RadiansToDegrees(Speed * DeltaSeconds / WheelRadius), 360.f);
	for (int32 i = 0; i < WheelSteer.Num(); ++i)
	{
		WheelSteer[i]->SetRelativeRotation(FRotator(0.f, i < 2 ? SteerVisual : 0.f, 0.f));
		WheelSpin[i]->SetRelativeRotation(FRotator(WheelRoll, 0.f, 0.f));
	}

	// Body roll / squat for weight.
	const float Roll = -Steer * FMath::Clamp(FMath::Abs(Speed) / 2000.f, 0.f, 1.f) * 3.f;
	const float Squat = FMath::Clamp(Throttle, -1.f, 1.f) * 1.2f;
	BodyPivot->SetRelativeRotation(FMath::RInterpTo(BodyPivot->GetRelativeRotation(), FRotator(Squat, 0.f, Roll), DeltaSeconds, 5.f));

	const ALifeGameState* GS = GetWorld()->GetGameState<ALifeGameState>();
	const bool bLightsOn = GS && GS->IsNight() && (bTraffic || Driver.IsValid());
	HeadlightL->SetIntensity(bLightsOn ? 9000.f : 0.f);
	HeadlightR->SetIntensity(bLightsOn ? 9000.f : 0.f);
	const bool bBraking = bHandbrake || Throttle * Speed < 0.f;
	TailGlow->SetIntensity((bLightsOn ? 40.f : 0.f) + (bBraking && (bTraffic || Driver.IsValid()) ? 120.f : 0.f));
}
