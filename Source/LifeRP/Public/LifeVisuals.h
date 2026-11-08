// Life RP - runtime geometry helpers.
//
// The game ships with no binary assets: every mesh is an engine basic shape
// (/Engine/BasicShapes) tinted through a dynamic instance of BasicShapeMaterial.
// That keeps the repository text-only and lets art replace any piece later by
// swapping the mesh/material on the component (or subclassing in Blueprint).

#pragma once

#include "CoreMinimal.h"

class UStaticMesh;
class UMaterialInterface;
class UMaterialInstanceDynamic;
class UStaticMeshComponent;
class USceneComponent;
class UTextRenderComponent;
class AActor;

namespace LifeVisuals
{
	LIFERP_API UStaticMesh* Cube();
	LIFERP_API UStaticMesh* Sphere();
	LIFERP_API UStaticMesh* Cylinder();

	/** The tintable engine base material ("Color" vector parameter). */
	LIFERP_API UMaterialInterface* BaseMaterial();

	/** Dynamic material tinted to Color. */
	LIFERP_API UMaterialInstanceDynamic* MakeColor(UObject* Outer, const FLinearColor& Color);

	/** Re-tints every material slot of a mesh component. */
	LIFERP_API void Tint(UStaticMeshComponent* Component, const FLinearColor& Color);

	/**
	 * Creates, attaches and registers a mesh part at runtime.
	 * Size is in centimetres (basic shapes are 100 cm), Location/Rotation are relative to Parent.
	 */
	LIFERP_API UStaticMeshComponent* AddPart(AActor* Owner, USceneComponent* Parent, UStaticMesh* Mesh,
		const FVector& Location, const FVector& Size, const FLinearColor& Color,
		const FRotator& Rotation = FRotator::ZeroRotator, bool bCollision = true);

	/** Creates, attaches and registers a 3D text label facing +X of Parent. */
	LIFERP_API UTextRenderComponent* AddLabel(AActor* Owner, USceneComponent* Parent, const FString& Text,
		const FVector& Location, float WorldSize, const FColor& Color, const FRotator& Rotation = FRotator::ZeroRotator);

	/** True when the world renders with the mobile renderer (Android, or Mobile Preview in the editor). */
	LIFERP_API bool IsMobileRenderer(const UObject* WorldContext);

	/** True when the on-screen touch controls should be shown. */
	LIFERP_API bool UsesTouchUI();

	/** Hex-ish helper: 0-255 RGB to linear colour. */
	LIFERP_API FLinearColor RGB(uint8 R, uint8 G, uint8 B);
}
