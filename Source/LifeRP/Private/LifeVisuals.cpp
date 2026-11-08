// Life RP - runtime geometry helpers.

#include "LifeVisuals.h"
#include "Engine/StaticMesh.h"
#include "Components/StaticMeshComponent.h"
#include "Components/TextRenderComponent.h"
#include "Materials/MaterialInterface.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "GameFramework/Actor.h"

namespace LifeVisuals
{
	static const FName ColorParam(TEXT("Color"));

	UStaticMesh* Cube()     { return LoadObject<UStaticMesh>(nullptr, TEXT("/Engine/BasicShapes/Cube.Cube")); }
	UStaticMesh* Sphere()   { return LoadObject<UStaticMesh>(nullptr, TEXT("/Engine/BasicShapes/Sphere.Sphere")); }
	UStaticMesh* Cylinder() { return LoadObject<UStaticMesh>(nullptr, TEXT("/Engine/BasicShapes/Cylinder.Cylinder")); }

	UMaterialInterface* BaseMaterial()
	{
		return LoadObject<UMaterialInterface>(nullptr, TEXT("/Engine/BasicShapes/BasicShapeMaterial.BasicShapeMaterial"));
	}

	UMaterialInstanceDynamic* MakeColor(UObject* Outer, const FLinearColor& Color)
	{
		UMaterialInterface* Base = BaseMaterial();
		if (!Base)
		{
			return nullptr;
		}
		UMaterialInstanceDynamic* MID = UMaterialInstanceDynamic::Create(Base, Outer);
		MID->SetVectorParameterValue(ColorParam, Color);
		return MID;
	}

	void Tint(UStaticMeshComponent* Component, const FLinearColor& Color)
	{
		if (!Component)
		{
			return;
		}
		if (UMaterialInstanceDynamic* MID = MakeColor(Component, Color))
		{
			for (int32 i = 0; i < FMath::Max(1, Component->GetNumMaterials()); ++i)
			{
				Component->SetMaterial(i, MID);
			}
		}
	}

	UStaticMeshComponent* AddPart(AActor* Owner, USceneComponent* Parent, UStaticMesh* Mesh,
		const FVector& Location, const FVector& Size, const FLinearColor& Color, const FRotator& Rotation, bool bCollision)
	{
		check(Owner);
		UStaticMeshComponent* Part = NewObject<UStaticMeshComponent>(Owner);
		Part->SetMobility(Parent ? Parent->Mobility.GetValue() : EComponentMobility::Movable);
		Part->SetStaticMesh(Mesh);
		Part->SetRelativeLocation(Location);
		Part->SetRelativeRotation(Rotation);
		Part->SetRelativeScale3D(Size / 100.f);
		Part->SetCollisionEnabled(bCollision ? ECollisionEnabled::QueryAndPhysics : ECollisionEnabled::NoCollision);
		if (!bCollision)
		{
			Part->SetCanEverAffectNavigation(false);
		}
		if (Parent)
		{
			Part->SetupAttachment(Parent);
		}
		Part->RegisterComponent();
		Owner->AddInstanceComponent(Part);
		Tint(Part, Color);
		return Part;
	}

	UTextRenderComponent* AddLabel(AActor* Owner, USceneComponent* Parent, const FString& Text,
		const FVector& Location, float WorldSize, const FColor& Color, const FRotator& Rotation)
	{
		check(Owner);
		UTextRenderComponent* Label = NewObject<UTextRenderComponent>(Owner);
		Label->SetMobility(EComponentMobility::Movable);
		Label->SetText(FText::FromString(Text));
		Label->SetTextRenderColor(Color);
		Label->SetWorldSize(WorldSize);
		Label->SetHorizontalAlignment(EHTA_Center);
		Label->SetVerticalAlignment(EVRTA_TextCenter);
		Label->SetRelativeLocation(Location);
		Label->SetRelativeRotation(Rotation);
		Label->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		if (Parent)
		{
			Label->SetupAttachment(Parent);
		}
		Label->RegisterComponent();
		Owner->AddInstanceComponent(Label);
		return Label;
	}

	FLinearColor RGB(uint8 R, uint8 G, uint8 B)
	{
		return FLinearColor(FColor(R, G, B));
	}
}
