// Life RP - finds what the player is looking at.

#include "LifeInteractionComponent.h"
#include "LifeInteractable.h"
#include "Engine/World.h"
#include "Engine/OverlapResult.h"
#include "GameFramework/Pawn.h"
#include "GameFramework/Controller.h"

ULifeInteractionComponent::ULifeInteractionComponent()
{
	PrimaryComponentTick.bCanEverTick = true;
}

void ULifeInteractionComponent::TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction)
{
	Super::TickComponent(DeltaTime, TickType, ThisTickFunction);
	ScanTimer -= DeltaTime;
	if (ScanTimer <= 0.f)
	{
		ScanTimer = ScanInterval;
		Scan();
	}
}

void ULifeInteractionComponent::Scan()
{
	const APawn* Owner = Cast<APawn>(GetOwner());
	if (!Owner || !Owner->GetController() || Owner->IsHidden())
	{
		Focus.Reset();
		return;
	}

	const FVector Origin = Owner->GetActorLocation();
	FVector ViewDir = Owner->GetController()->GetControlRotation().Vector();
	ViewDir.Z = 0.f;
	ViewDir = ViewDir.GetSafeNormal();

	TArray<FOverlapResult> Overlaps;
	FCollisionQueryParams Params(SCENE_QUERY_STAT(LifeInteractScan), false, Owner);
	GetWorld()->OverlapMultiByObjectType(Overlaps, Origin, FQuat::Identity,
		FCollisionObjectQueryParams(FCollisionObjectQueryParams::InitType::AllObjects),
		FCollisionShape::MakeSphere(Radius), Params);

	AActor* Best = nullptr;
	float BestScore = -1.f;
	for (const FOverlapResult& Overlap : Overlaps)
	{
		AActor* Candidate = Overlap.GetActor();
		if (!Candidate || Candidate == Owner || !Candidate->Implements<ULifeInteractable>())
		{
			continue;
		}
		FVector ToCandidate = Candidate->GetActorLocation() - Origin;
		ToCandidate.Z = 0.f;
		const float Distance = ToCandidate.Size();
		const float Facing = Distance > 1.f ? FVector::DotProduct(ViewDir, ToCandidate / Distance) : 1.f;
		if (Facing < -0.2f && Distance > 120.f)
		{
			continue; // behind us
		}
		const float Score = Facing * 2.f - Distance / Radius;
		if (!Best || Score > BestScore)
		{
			Best = Candidate;
			BestScore = Score;
		}
	}
	Focus = Best;
}
