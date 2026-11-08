// Life RP - heads-up display.

#include "LifeHUD.h"
#include "LifeCareerComponent.h"
#include "LifeGameState.h"
#include "LifeInteractable.h"
#include "LifeInteractionComponent.h"
#include "LifeNeedsComponent.h"
#include "LifePlayerCharacter.h"
#include "LifePlayerController.h"
#include "LifeRules.h"
#include "LifeVehicle.h"
#include "LifeWalletComponent.h"
#include "CanvasItem.h"
#include "Engine/Canvas.h"
#include "Engine/Engine.h"
#include "Engine/Font.h"
#include "Engine/World.h"

namespace
{
	const FLinearColor TextMain(0.96f, 0.96f, 0.94f);
	const FLinearColor TextDim(0.70f, 0.72f, 0.76f);
	const FLinearColor Positive(0.45f, 1.f, 0.55f);

	FLinearColor NeedColor(float Value01)
	{
		// Red -> amber -> green.
		return Value01 < 0.5f
			? FMath::Lerp(FLinearColor(0.95f, 0.18f, 0.15f), FLinearColor(0.98f, 0.70f, 0.15f), Value01 * 2.f)
			: FMath::Lerp(FLinearColor(0.98f, 0.70f, 0.15f), FLinearColor(0.30f, 0.85f, 0.40f), (Value01 - 0.5f) * 2.f);
	}
}

UFont* ALifeHUD::Font() const
{
	return GEngine ? GEngine->GetMediumFont() : nullptr;
}

void ALifeHUD::PushNotification(const FString& Message, const FLinearColor& Color)
{
	FToast Toast;
	Toast.Text = Message;
	Toast.Color = Color;
	Toasts.Insert(Toast, 0);
	if (Toasts.Num() > MaxToasts)
	{
		Toasts.SetNum(MaxToasts);
	}
}

void ALifeHUD::DrawPanel(float X, float Y, float W, float H, float Alpha)
{
	FCanvasTileItem Tile(FVector2D(X, Y), FVector2D(W, H), FLinearColor(0.02f, 0.025f, 0.035f, Alpha));
	Tile.BlendMode = SE_BLEND_Translucent;
	Canvas->DrawItem(Tile);
	FCanvasTileItem Edge(FVector2D(X, Y), FVector2D(3.f * UIScale, H), FLinearColor(1.f, 1.f, 1.f, 0.15f));
	Edge.BlendMode = SE_BLEND_Translucent;
	Canvas->DrawItem(Edge);
}

void ALifeHUD::DrawLabel(const FString& Text, float X, float Y, const FLinearColor& Color, float Size, bool bCentered)
{
	FCanvasTextItem Item(FVector2D(X, Y), FText::FromString(Text), Font(), Color);
	Item.Scale = FVector2D(Size * UIScale, Size * UIScale);
	Item.bCentreX = bCentered;
	Item.EnableShadow(FLinearColor(0.f, 0.f, 0.f, 0.7f));
	Canvas->DrawItem(Item);
}

void ALifeHUD::DrawBar(const FString& Label, float Value01, float X, float Y, float W)
{
	const float H = 10.f * UIScale;
	DrawLabel(Label, X, Y, TextDim, 0.85f);
	const float BarY = Y + 20.f * UIScale;
	FCanvasTileItem Back(FVector2D(X, BarY), FVector2D(W, H), FLinearColor(1.f, 1.f, 1.f, 0.1f));
	Back.BlendMode = SE_BLEND_Translucent;
	Canvas->DrawItem(Back);
	FCanvasTileItem Fill(FVector2D(X, BarY), FVector2D(W * FMath::Clamp(Value01, 0.f, 1.f), H), NeedColor(Value01));
	Canvas->DrawItem(Fill);
}

void ALifeHUD::DrawHUD()
{
	Super::DrawHUD();
	if (!Canvas)
	{
		return;
	}
	UIScale = FMath::Max(0.6f, Canvas->ClipY / 1080.f);

	const float Now = GetWorld()->GetRealTimeSeconds();
	const float Delta = LastDrawTime < 0.f ? 0.f : FMath::Min(Now - LastDrawTime, 0.25f);
	LastDrawTime = Now;

	const ALifePlayerController* PC = Cast<ALifePlayerController>(PlayerOwner);
	const ALifePlayerCharacter* C = PC ? PC->GetLifeCharacter() : nullptr;
	if (!C)
	{
		return;
	}

	// Sleep fade to dark.
	const float TargetFade = C->GetActivity() == ELifeActivity::Sleep ? 0.82f : 0.f;
	SleepFade = FMath::FInterpTo(SleepFade, TargetFade, Delta, 2.f);
	if (SleepFade > 0.01f)
	{
		FCanvasTileItem Dim(FVector2D::ZeroVector, FVector2D(Canvas->ClipX, Canvas->ClipY), FLinearColor(0.f, 0.f, 0.02f, SleepFade));
		Dim.BlendMode = SE_BLEND_Translucent;
		Canvas->DrawItem(Dim);
	}

	DrawTopLeft(C);
	DrawNeeds(C);
	DrawPrompt(C);
	DrawActivity(C);
	DrawSpeedometer(C);
	DrawToasts(Delta);
	if (PC->IsStatusOpen())
	{
		DrawStatusPanel(C);
	}
}

void ALifeHUD::DrawTopLeft(const ALifePlayerCharacter* C)
{
	const ALifeGameState* GS = GetWorld()->GetGameState<ALifeGameState>();
	const float X = 24.f * UIScale, Y = 24.f * UIScale;
	DrawPanel(X, Y, 420.f * UIScale, 118.f * UIScale);
	if (GS)
	{
		FString Clock = GS->GetClockText();
		if (GS->GetTimeMultiplier() > 1.5f)
		{
			Clock += FString::Printf(TEXT("   >> x%.0f"), GS->GetTimeMultiplier());
		}
		DrawLabel(Clock, X + 16.f * UIScale, Y + 10.f * UIScale, TextMain, 1.25f);
	}
	const ULifeWalletComponent* W = C->GetWallet();
	DrawLabel(FString::Printf(TEXT("$%d cash"), W->GetCash()), X + 16.f * UIScale, Y + 46.f * UIScale, Positive, 1.1f);
	DrawLabel(FString::Printf(TEXT("$%d bank"), W->GetBank()), X + 190.f * UIScale, Y + 46.f * UIScale,
		W->GetBank() < 0 ? FLinearColor(1.f, 0.35f, 0.3f) : TextDim, 1.1f);

	const ULifeCareerComponent* Career = C->GetCareer();
	FString Job = Career->HasJob() ? Career->GetJobSummary() : TEXT("Unemployed - find a job board");
	if (Career->IsClockedIn())
	{
		Job = FString::Printf(TEXT("AT WORK  +$%.0f   %s"), Career->GetShiftEarnings(), *Career->GetJobSummary());
	}
	DrawLabel(Job, X + 16.f * UIScale, Y + 80.f * UIScale, Career->IsClockedIn() ? Positive : TextDim, 0.85f);
}

void ALifeHUD::DrawNeeds(const ALifePlayerCharacter* C)
{
	static const TCHAR* Names[] = { TEXT("Hunger"), TEXT("Energy"), TEXT("Hygiene"), TEXT("Fun"), TEXT("Social") };
	const ULifeNeedsComponent* Needs = C->GetNeeds();
	const float W = 300.f * UIScale;
	const float X = 24.f * UIScale;
	const float Rows = 6.f;
	const float RowH = 38.f * UIScale;
	const float Y = Canvas->ClipY - (Rows * RowH + 40.f * UIScale);
	DrawPanel(X, Y, W + 32.f * UIScale, Rows * RowH + 20.f * UIScale);

	const float Mood = Needs->GetMood();
	DrawBar(FString::Printf(TEXT("MOOD  %.0f"), Mood), Mood / 100.f, X + 16.f * UIScale, Y + 8.f * UIScale, W);
	int32 Row = 1;
	for (ELifeNeed Need : TEnumRange<ELifeNeed>())
	{
		const float V = Needs->GetNeed(Need);
		DrawBar(FString::Printf(TEXT("%s  %.0f"), Names[static_cast<int32>(Need)], V), V / 100.f, X + 16.f * UIScale, Y + 8.f * UIScale + Row * RowH, W);
		++Row;
	}
}

void ALifeHUD::DrawPrompt(const ALifePlayerCharacter* C)
{
	if (C->IsBusy() || C->IsDriving())
	{
		return;
	}
	AActor* Focus = C->GetInteraction()->GetFocus();
	const ILifeInteractable* Target = Cast<ILifeInteractable>(Focus);
	if (!Target)
	{
		return;
	}
	const FString Prompt = FString::Printf(TEXT("[E]  %s"), *Target->GetInteractionPrompt(C));
	const bool bUsable = Target->CanInteract(C);
	const float W = FMath::Max(260.f, Prompt.Len() * 13.f) * UIScale;
	const float X = Canvas->ClipX * 0.5f - W * 0.5f;
	const float Y = Canvas->ClipY * 0.72f;
	DrawPanel(X, Y, W, 44.f * UIScale, 0.65f);
	DrawLabel(Prompt, Canvas->ClipX * 0.5f, Y + 10.f * UIScale, bUsable ? TextMain : FLinearColor(1.f, 0.5f, 0.45f), 1.1f, true);
}

void ALifeHUD::DrawToasts(float DeltaSeconds)
{
	for (FToast& Toast : Toasts)
	{
		Toast.Age += DeltaSeconds;
	}
	Toasts.RemoveAll([](const FToast& T) { return T.Age > ToastLifetime; });

	const float W = 520.f * UIScale;
	const float X = Canvas->ClipX - W - 24.f * UIScale;
	float Y = 24.f * UIScale;
	for (const FToast& Toast : Toasts)
	{
		const float Alpha = FMath::Clamp((ToastLifetime - Toast.Age) / 1.f, 0.f, 1.f) * FMath::Clamp(Toast.Age / 0.15f, 0.f, 1.f);
		DrawPanel(X, Y, W, 36.f * UIScale, 0.55f * Alpha);
		FLinearColor Color = Toast.Color;
		Color.A = Alpha;
		DrawLabel(Toast.Text, X + 14.f * UIScale, Y + 7.f * UIScale, Color, 0.95f);
		Y += 42.f * UIScale;
	}
}

void ALifeHUD::DrawActivity(const ALifePlayerCharacter* C)
{
	if (!C->IsBusy())
	{
		return;
	}
	const int32 Minutes = FMath::CeilToInt(C->GetActivityRemainingMinutes());
	const FString Text = FString::Printf(TEXT("%s   %dh %02dm left      [E] stop"), *C->GetActivityLabel(), Minutes / 60, Minutes % 60);
	DrawLabel(Text, Canvas->ClipX * 0.5f, Canvas->ClipY * 0.42f, TextMain, 1.5f, true);
}

void ALifeHUD::DrawSpeedometer(const ALifePlayerCharacter* C)
{
	const ALifeVehicle* Car = C->GetVehicle();
	if (!Car)
	{
		return;
	}
	const float X = Canvas->ClipX - 250.f * UIScale;
	const float Y = Canvas->ClipY - 130.f * UIScale;
	DrawPanel(X, Y, 226.f * UIScale, 106.f * UIScale);
	DrawLabel(FString::Printf(TEXT("%3.0f"), Car->GetSpeedKmh()), X + 113.f * UIScale, Y + 8.f * UIScale, TextMain, 3.f, true);
	DrawLabel(TEXT("km/h      [E] exit"), X + 113.f * UIScale, Y + 74.f * UIScale, TextDim, 0.85f, true);
}

void ALifeHUD::DrawStatusPanel(const ALifePlayerCharacter* C)
{
	const float W = 620.f * UIScale, H = 640.f * UIScale;
	const float X = Canvas->ClipX * 0.5f - W * 0.5f;
	const float Y = Canvas->ClipY * 0.5f - H * 0.5f;
	DrawPanel(X, Y, W, H, 0.82f);

	float Line = Y + 18.f * UIScale;
	auto Row = [&](const FString& Text, const FLinearColor& Color, float Size = 1.f)
	{
		DrawLabel(Text, X + 24.f * UIScale, Line, Color, Size);
		Line += 30.f * UIScale * Size;
	};

	Row(TEXT("LIFE STATUS"), TextMain, 1.4f);
	const ULifeCareerComponent* Career = C->GetCareer();
	Row(Career->HasJob() ? FString::Printf(TEXT("Career: %s   (%.1f h worked)"), *Career->GetJobSummary(), Career->GetTotalHours()) : TEXT("Career: unemployed"), TextDim);
	Row(FString::Printf(TEXT("Skills: Fitness %.1f / 10    Charisma %.1f / 10"), C->GetFitness(), C->GetCharisma()), TextDim);
	Row(FString::Printf(TEXT("Mood: %.0f    Work performance x%.2f"), C->GetNeeds()->GetMood(), LifeRules::ComputePerformance(C->GetNeeds()->GetMood())), TextDim);
	Line += 8.f * UIScale;

	Row(TEXT("RELATIONSHIPS"), TextMain, 1.1f);
	TArray<TPair<FString, int32>> Sorted;
	for (const TPair<FString, int32>& Pair : C->GetRelationships())
	{
		Sorted.Add(Pair);
	}
	Sorted.Sort([](const TPair<FString, int32>& A, const TPair<FString, int32>& B) { return A.Value > B.Value; });
	if (Sorted.Num() == 0)
	{
		Row(TEXT("Nobody yet - go say hi to people around town."), TextDim);
	}
	for (int32 i = 0; i < FMath::Min(6, Sorted.Num()); ++i)
	{
		Row(FString::Printf(TEXT("%-10s %4d  %s"), *Sorted[i].Key, Sorted[i].Value, *LifeRules::RelationshipLabel(Sorted[i].Value)), TextDim);
	}
	Line += 8.f * UIScale;

	Row(TEXT("CONTROLS"), TextMain, 1.1f);
	Row(TEXT("WASD move   Mouse look   Wheel zoom   Shift run   Space jump"), TextDim, 0.85f);
	Row(TEXT("E interact / enter car / stop activity   TAB this panel"), TextDim, 0.85f);
	Row(TEXT("Car: W/S drive   A/D steer   Space handbrake   E exit"), TextDim, 0.85f);
	Row(TEXT("F5 save   F9 load   (autosaves when you wake up)"), TextDim, 0.85f);
}
