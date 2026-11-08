// Life RP - heads-up display.

#include "LifeHUD.h"
#include "LifeCareerComponent.h"
#include "LifeGameState.h"
#include "LifeInteractable.h"
#include "LifeInteractionComponent.h"
#include "LifeNeedsComponent.h"
#include "LifePlayerCharacter.h"
#include "LifePlayerController.h"
#include "CitizenNPC.h"
#include "LifeRules.h"
#include "LifeSettings.h"
#include "LifeStation.h"
#include "LifeTownBuilder.h"
#include "LifeVehicle.h"
#include "LifeWalletComponent.h"
#include "CanvasItem.h"
#include "Engine/Canvas.h"
#include "Engine/Engine.h"
#include "Engine/Font.h"
#include "Engine/World.h"
#include "EngineUtils.h"
#include "Camera/PlayerCameraManager.h"
#include "LifeVisuals.h"
#include "HAL/PlatformMisc.h"

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

UFont* ALifeHUD::Font()
{
	if (!CachedFont)
	{
		const FSoftObjectPath& Path = ULifeSettings::Get()->HUDFont;
		CachedFont = Path.IsValid() ? Cast<UFont>(Path.TryLoad()) : nullptr;
		if (!CachedFont && GEngine)
		{
			CachedFont = GEngine->GetMediumFont();
		}
	}
	return CachedFont;
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
	bTouchUI = LifeVisuals::UsesTouchUI();
	// Phones are small: draw the touch layout larger.
	UIScale = FMath::Max(0.6f, Canvas->ClipY / 1080.f) * (bTouchUI ? 1.15f : 1.f);

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

	DrawWorldLabels(C);
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
	if (bTouchUI)
	{
		ValidateHeldButtons();
		DrawTouchButtons(C);
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
			Clock += FString::Printf(TEXT("   >> x%.0f배속"), GS->GetTimeMultiplier());
		}
		DrawLabel(Clock, X + 16.f * UIScale, Y + 10.f * UIScale, TextMain, 1.25f);
	}
	const ULifeWalletComponent* W = C->GetWallet();
	DrawLabel(FString::Printf(TEXT("현금 $%d"), W->GetCash()), X + 16.f * UIScale, Y + 46.f * UIScale, Positive, 1.1f);
	DrawLabel(FString::Printf(TEXT("은행 $%d"), W->GetBank()), X + 190.f * UIScale, Y + 46.f * UIScale,
		W->GetBank() < 0 ? FLinearColor(1.f, 0.35f, 0.3f) : TextDim, 1.1f);

	const ULifeCareerComponent* Career = C->GetCareer();
	FString Job = Career->HasJob() ? Career->GetJobSummary() : TEXT("무직 - 일자리를 찾아보세요");
	if (Career->IsClockedIn())
	{
		Job = FString::Printf(TEXT("근무 중  +$%.0f   %s"), Career->GetShiftEarnings(), *Career->GetJobSummary());
	}
	DrawLabel(Job, X + 16.f * UIScale, Y + 80.f * UIScale, Career->IsClockedIn() ? Positive : TextDim, 0.85f);
}

void ALifeHUD::DrawNeeds(const ALifePlayerCharacter* C)
{
	static const TCHAR* Names[] = { TEXT("배고픔"), TEXT("에너지"), TEXT("위생"), TEXT("재미"), TEXT("사교") };
	const ULifeNeedsComponent* Needs = C->GetNeeds();
	const float W = 300.f * UIScale;
	const float X = 24.f * UIScale;
	const float Rows = 6.f;
	const float RowH = 38.f * UIScale;
	const float Y = bTouchUI ? 150.f * UIScale : Canvas->ClipY - (Rows * RowH + 40.f * UIScale);
	DrawPanel(X, Y, W + 32.f * UIScale, Rows * RowH + 20.f * UIScale);

	const float Mood = Needs->GetMood();
	DrawBar(FString::Printf(TEXT("기분  %.0f"), Mood), Mood / 100.f, X + 16.f * UIScale, Y + 8.f * UIScale, W);
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
	const FString Prompt = FString::Printf(bTouchUI ? TEXT("[행동]  %s") : TEXT("[E]  %s"), *Target->GetInteractionPrompt(C));
	const bool bUsable = Target->CanInteract(C);
	float TextW = 0.f, TextH = 0.f;
	Canvas->TextSize(Font(), Prompt, TextW, TextH, 1.1f * UIScale, 1.1f * UIScale);
	const float W = FMath::Max(260.f * UIScale, TextW + 48.f * UIScale);
	const float X = Canvas->ClipX * 0.5f - W * 0.5f;
	const float Y = Canvas->ClipY * (bTouchUI ? 0.62f : 0.72f);
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
	int32 Shown = 0;
	for (const FToast& Toast : Toasts)
	{
		if (bTouchUI && ++Shown > 3)
		{
			break; // keep the right side free for the touch buttons
		}
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
	const FString Text = FString::Printf(bTouchUI ? TEXT("%s   남은 시간 %d시간 %02d분      [행동] 중단") : TEXT("%s   남은 시간 %d시간 %02d분      [E] 중단"), *C->GetActivityLabel(), Minutes / 60, Minutes % 60);
	DrawLabel(Text, Canvas->ClipX * 0.5f, Canvas->ClipY * 0.42f, TextMain, 1.5f, true);
}

void ALifeHUD::DrawSpeedometer(const ALifePlayerCharacter* C)
{
	const ALifeVehicle* Car = C->GetVehicle();
	if (!Car)
	{
		return;
	}
	const float X = bTouchUI ? Canvas->ClipX * 0.5f - 113.f * UIScale : Canvas->ClipX - 250.f * UIScale;
	const float Y = bTouchUI ? 24.f * UIScale : Canvas->ClipY - 130.f * UIScale;
	DrawPanel(X, Y, 226.f * UIScale, 106.f * UIScale);
	DrawLabel(FString::Printf(TEXT("%3.0f"), Car->GetSpeedKmh()), X + 113.f * UIScale, Y + 8.f * UIScale, TextMain, 3.f, true);
	DrawLabel(bTouchUI ? TEXT("km/h") : TEXT("km/h      [E] 내리기"), X + 113.f * UIScale, Y + 74.f * UIScale, TextDim, 0.85f, true);
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

	Row(TEXT("생활 현황"), TextMain, 1.4f);
	const ULifeCareerComponent* Career = C->GetCareer();
	Row(Career->HasJob() ? FString::Printf(TEXT("직업: %s   (%.1f시간 근무)"), *Career->GetJobSummary(), Career->GetTotalHours()) : TEXT("직업: 무직"), TextDim);
	Row(FString::Printf(TEXT("스킬: 체력 %.1f / 10    매력 %.1f / 10"), C->GetFitness(), C->GetCharisma()), TextDim);
	Row(FString::Printf(TEXT("기분: %.0f    업무 효율 x%.2f"), C->GetNeeds()->GetMood(), LifeRules::ComputePerformance(C->GetNeeds()->GetMood())), TextDim);
	Line += 8.f * UIScale;

	Row(TEXT("인간관계"), TextMain, 1.1f);
	TArray<TPair<FString, int32>> Sorted;
	for (const TPair<FString, int32>& Pair : C->GetRelationships())
	{
		Sorted.Add(Pair);
	}
	Sorted.Sort([](const TPair<FString, int32>& A, const TPair<FString, int32>& B) { return A.Value > B.Value; });
	if (Sorted.Num() == 0)
	{
		Row(TEXT("아직 아무도 없습니다 - 마을 사람들에게 인사해 보세요."), TextDim);
	}
	for (int32 i = 0; i < FMath::Min(6, Sorted.Num()); ++i)
	{
		Row(FString::Printf(TEXT("%-10s %4d  %s"), *Sorted[i].Key, Sorted[i].Value, *LifeRules::RelationshipLabel(Sorted[i].Value)), TextDim);
	}
	Line += 8.f * UIScale;

	Row(TEXT("조작법"), TextMain, 1.1f);
	Row(TEXT("WASD 이동   마우스 시점   휠 줌   Shift 달리기   Space 점프"), TextDim, 0.85f);
	Row(TEXT("E 상호작용 / 차 타기 / 활동 중단   TAB 이 화면"), TextDim, 0.85f);
	Row(TEXT("자동차: W/S 가속·후진   A/D 조향   Space 핸드브레이크   E 내리기"), TextDim, 0.85f);
	Row(TEXT("F5 저장   F9 불러오기   (잠에서 깨면 자동 저장)"), TextDim, 0.85f);
}

// ------------------------------------------------------------------ In-world text

void ALifeHUD::DrawWorldLabel(const ALifePlayerCharacter* C, const FVector& World, const FString& Text, const FLinearColor& Color,
	float Size, float MaxDistance, const AActor* Owner)
{
	if (Text.IsEmpty() || !PlayerOwner || !PlayerOwner->PlayerCameraManager)
	{
		return;
	}
	const FVector CamLoc = PlayerOwner->PlayerCameraManager->GetCameraLocation();
	const FVector CamFwd = PlayerOwner->PlayerCameraManager->GetCameraRotation().Vector();
	const AActor* Viewer = C->GetVehicle() ? static_cast<const AActor*>(C->GetVehicle()) : static_cast<const AActor*>(C);
	const float Distance = FVector::Dist(Viewer->GetActorLocation(), World);
	if (Distance > MaxDistance || FVector::DotProduct(World - CamLoc, CamFwd) <= 0.f)
	{
		return;
	}

	// Hide labels behind walls.
	FCollisionQueryParams Params(SCENE_QUERY_STAT(LifeHUDLabel), false);
	Params.AddIgnoredActor(C);
	Params.AddIgnoredActor(Viewer);
	if (Owner)
	{
		Params.AddIgnoredActor(Owner);
	}
	FHitResult Hit;
	if (GetWorld()->LineTraceSingleByChannel(Hit, CamLoc, World, ECC_Visibility, Params))
	{
		return;
	}

	const FVector Screen = Project(World);
	const float Alpha = FMath::Clamp((MaxDistance - Distance) / (MaxDistance * 0.3f), 0.f, 1.f);
	const float Scale = Size * UIScale;
	float TextW = 0.f, TextH = 0.f;
	Canvas->TextSize(Font(), Text, TextW, TextH, Scale, Scale);
	const float Pad = 10.f * UIScale;
	DrawPanel(Screen.X - TextW * 0.5f - Pad, Screen.Y - TextH - Pad, TextW + Pad * 2.f, TextH + Pad * 1.4f, 0.6f * Alpha);
	FLinearColor Faded = Color;
	Faded.A = Alpha;
	DrawLabel(Text, Screen.X, Screen.Y - TextH - Pad * 0.4f, Faded, Size, true);
}

void ALifeHUD::DrawWorldLabels(const ALifePlayerCharacter* C)
{
	UWorld* World = GetWorld();

	if (!Town.IsValid())
	{
		TActorIterator<ALifeTownBuilder> It(World);
		Town = It ? *It : nullptr;
	}
	if (const ALifeTownBuilder* T = Town.Get())
	{
		for (const FLifeSignLabel& Sign : T->GetSignLabels())
		{
			DrawWorldLabel(C, Sign.Location, Sign.Text, FLinearColor(1.f, 0.92f, 0.75f), 1.15f, 3500.f, T);
		}
	}

	if (C->IsDriving())
	{
		return;
	}
	for (TActorIterator<ALifeStation> It(World); It; ++It)
	{
		DrawWorldLabel(C, It->GetWorldLabelLocation(), It->GetWorldLabel(), TextMain, 0.85f, 900.f, *It);
	}
	for (TActorIterator<ACitizenNPC> It(World); It; ++It)
	{
		if (It->IsAtHome())
		{
			continue;
		}
		const FString Line = It->GetSpeechLine();
		if (!Line.IsEmpty())
		{
			DrawWorldLabel(C, It->GetActorLocation() + FVector(0.f, 0.f, 120.f),
				FString::Printf(TEXT("%s: %s"), *It->GetCitizenName(), *Line), TextMain, 0.95f, 1500.f, *It);
		}
	}
}

// ------------------------------------------------------------------ Touch controls

namespace LifeTouch
{
	const FName Interact(TEXT("Touch.Interact"));
	const FName Jump(TEXT("Touch.Jump"));
	const FName Sprint(TEXT("Touch.Sprint"));
	const FName Status(TEXT("Touch.Status"));
	const FName Save(TEXT("Touch.Save"));
	const FName Gas(TEXT("Touch.Gas"));
	const FName Brake(TEXT("Touch.Brake"));
	const FName Handbrake(TEXT("Touch.Handbrake"));
	const FName Exit(TEXT("Touch.Exit"));
}

void ALifeHUD::TouchButton(FName Name, const FString& Label, float CX, float CY, float Size, bool bActive)
{
	const FVector2D Pos(CX - Size * 0.5f, CY - Size * 0.5f);
	const bool bHeld = HeldButtons.Contains(Name) || bActive;
	FCanvasTileItem Tile(Pos, FVector2D(Size, Size), bHeld ? FLinearColor(0.35f, 0.75f, 1.f, 0.55f) : FLinearColor(0.02f, 0.025f, 0.035f, 0.45f));
	Tile.BlendMode = SE_BLEND_Translucent;
	Canvas->DrawItem(Tile);
	FCanvasBoxItem Border(Pos, FVector2D(Size, Size));
	Border.SetColor(FLinearColor(1.f, 1.f, 1.f, 0.35f));
	Border.LineThickness = 2.f;
	Canvas->DrawItem(Border);
	float TextW = 0.f, TextH = 0.f;
	Canvas->TextSize(Font(), Label, TextW, TextH, UIScale, UIScale);
	DrawLabel(Label, CX, CY - TextH * 0.5f, TextMain, 1.f, true);

	AddHitBox(Pos, FVector2D(Size, Size), Name, true);
	ButtonRects.Add(Name, FBox2D(Pos, Pos + FVector2D(Size, Size)));
}

void ALifeHUD::DrawTouchButtons(const ALifePlayerCharacter* C)
{
	ButtonRects.Reset();
	const float W = Canvas->ClipX, H = Canvas->ClipY;
	const float Big = 150.f * UIScale, Small = 96.f * UIScale, Gap = 18.f * UIScale;
	const float RightX = W - Big * 0.5f - 40.f * UIScale;

	// Top centre: status + save.
	TouchButton(LifeTouch::Status, TEXT("상태"), W * 0.5f - Small * 0.6f - (C->IsDriving() ? 160.f * UIScale : 0.f), Small * 0.5f + 20.f * UIScale, Small);
	TouchButton(LifeTouch::Save, TEXT("저장"), W * 0.5f + Small * 0.6f - (C->IsDriving() ? 160.f * UIScale : 0.f), Small * 0.5f + 20.f * UIScale, Small);

	// Right column, above the look joystick.
	if (C->IsDriving())
	{
		TouchButton(LifeTouch::Gas, TEXT("가속"), RightX, H * 0.26f, Big);
		TouchButton(LifeTouch::Brake, TEXT("브레이크"), RightX - Big - Gap, H * 0.26f + Big * 0.35f, Big);
		TouchButton(LifeTouch::Handbrake, TEXT("핸드브레이크"), RightX - Big - Gap, H * 0.26f + Big * 1.4f, Small);
		TouchButton(LifeTouch::Exit, TEXT("내리기"), RightX, H * 0.26f + Big + Gap, Small);
		return;
	}
	TouchButton(LifeTouch::Interact, C->IsBusy() ? TEXT("중단") : TEXT("행동"), RightX, H * 0.30f, Big);
	TouchButton(LifeTouch::Jump, TEXT("점프"), RightX - Big - Gap, H * 0.30f + Big * 0.5f, Small);
	TouchButton(LifeTouch::Sprint, TEXT("달리기"), RightX, H * 0.30f + Big * 0.5f + Gap + Small * 0.5f, Small, C->IsSprintHeld());
}

void ALifeHUD::NotifyHitBoxClick_Implementation(FName BoxName)
{
	Super::NotifyHitBoxClick_Implementation(BoxName);
	if (!HeldButtons.Contains(BoxName))
	{
		HeldButtons.Add(BoxName);
		PressButton(BoxName);
	}
}

void ALifeHUD::NotifyHitBoxRelease_Implementation(FName BoxName)
{
	Super::NotifyHitBoxRelease_Implementation(BoxName);
	ReleaseButton(BoxName);
}

void ALifeHUD::PressButton(FName Name)
{
	ALifePlayerController* PC = Cast<ALifePlayerController>(PlayerOwner);
	ALifePlayerCharacter* C = PC ? PC->GetLifeCharacter() : nullptr;
	if (!C)
	{
		return;
	}
	ALifeVehicle* Car = C->GetVehicle();

	if (Name == LifeTouch::Status)          { PC->ToggleStatus(); }
	else if (Name == LifeTouch::Save)       { PC->SaveToSlot(); }
	else if (Name == LifeTouch::Interact)   { C->InteractPressed(); }
	else if (Name == LifeTouch::Jump)       { C->JumpPressed(); }
	else if (Name == LifeTouch::Sprint)     { C->SetSprintHeld(!C->IsSprintHeld()); }
	else if (Car && Name == LifeTouch::Gas)       { Car->SetTouchThrottle(1.f); }
	else if (Car && Name == LifeTouch::Brake)     { Car->SetTouchThrottle(-1.f); }
	else if (Car && Name == LifeTouch::Handbrake) { Car->SetTouchHandbrake(true); }
	else if (Car && Name == LifeTouch::Exit)      { Car->RequestExit(); }
}

void ALifeHUD::ReleaseButton(FName Name)
{
	if (!HeldButtons.Remove(Name))
	{
		return;
	}
	const ALifePlayerController* PC = Cast<ALifePlayerController>(PlayerOwner);
	ALifePlayerCharacter* C = PC ? PC->GetLifeCharacter() : nullptr;
	if (!C)
	{
		return;
	}
	if (Name == LifeTouch::Jump)
	{
		C->StopJumping();
	}
	if (ALifeVehicle* Car = C->GetVehicle())
	{
		if (Name == LifeTouch::Gas || Name == LifeTouch::Brake)
		{
			Car->SetTouchThrottle(0.f);
		}
		else if (Name == LifeTouch::Handbrake)
		{
			Car->SetTouchHandbrake(false);
		}
	}
}

void ALifeHUD::ValidateHeldButtons()
{
	if (HeldButtons.Num() == 0 || !PlayerOwner)
	{
		return;
	}
	// Fingers currently down (mouse clicks on PC are released through the hit box itself).
	TArray<FVector2D> Fingers;
	for (int32 Index = 0; Index < ETouchIndex::MAX_TOUCHES; ++Index)
	{
		float X = 0.f, Y = 0.f;
		bool bPressed = false;
		PlayerOwner->GetInputTouchState(static_cast<ETouchIndex::Type>(Index), X, Y, bPressed);
		if (bPressed)
		{
			Fingers.Add(FVector2D(X, Y));
		}
	}
	if (!FPlatformMisc::GetUseVirtualJoysticks())
	{
		return; // PC test mode
	}
	TArray<FName> ToRelease;
	for (const FName& Name : HeldButtons)
	{
		const FBox2D* Rect = ButtonRects.Find(Name);
		const bool bStillHeld = Rect && Fingers.ContainsByPredicate([Rect](const FVector2D& F) { return Rect->IsInside(F); });
		if (!bStillHeld)
		{
			ToRelease.Add(Name);
		}
	}
	for (const FName& Name : ToRelease)
	{
		ReleaseButton(Name);
	}
}
