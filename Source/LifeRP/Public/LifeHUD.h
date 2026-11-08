// Life RP - heads-up display.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/HUD.h"
#include "LifeHUD.generated.h"

class UFont;
class ALifePlayerCharacter;

/**
 * Canvas HUD: clock and money, need bars with mood, job status, interaction
 * prompt, toast notifications, activity overlay, speedometer and a TAB status
 * panel. Drawn in code so it needs no widget assets; resolution-independent
 * (designed at 1080p, scaled to the viewport).
 */
UCLASS()
class LIFERP_API ALifeHUD : public AHUD
{
	GENERATED_BODY()

public:
	virtual void DrawHUD() override;

	/** Adds a toast; newest on top, at most MaxToasts visible. */
	void PushNotification(const FString& Message, const FLinearColor& Color);

private:
	struct FToast
	{
		FString Text;
		FLinearColor Color;
		float Age = 0.f;
	};

	void DrawPanel(float X, float Y, float W, float H, float Alpha = 0.55f);
	void DrawLabel(const FString& Text, float X, float Y, const FLinearColor& Color, float Size = 1.f, bool bCentered = false);
	void DrawBar(const FString& Label, float Value01, float X, float Y, float W);
	void DrawTopLeft(const ALifePlayerCharacter* C);
	void DrawNeeds(const ALifePlayerCharacter* C);
	void DrawPrompt(const ALifePlayerCharacter* C);
	void DrawToasts(float DeltaSeconds);
	void DrawActivity(const ALifePlayerCharacter* C);
	void DrawStatusPanel(const ALifePlayerCharacter* C);
	void DrawSpeedometer(const ALifePlayerCharacter* C);

	UFont* Font() const;

	TArray<FToast> Toasts;
	float UIScale = 1.f;
	float LastDrawTime = -1.f;
	float SleepFade = 0.f;
	static constexpr int32 MaxToasts = 6;
	static constexpr float ToastLifetime = 6.f;
};
