// Life RP - data-driven tuning defaults. Rationale for each number: design/game-brief.md.

#include "LifeSettings.h"

namespace
{
	FLifeJobDef MakeJob(FName Id, const TCHAR* Name, float Wage, int32 Start, int32 End, const FLifeNeedValues& Drain,
		const FLinearColor& Color, std::initializer_list<const TCHAR*> Titles)
	{
		FLifeJobDef Job;
		Job.Id = Id;
		Job.DisplayName = Name;
		Job.HourlyWage = Wage;
		Job.ShiftStartHour = Start;
		Job.ShiftEndHour = End;
		Job.DrainPerHour = Drain;
		Job.AccentColor = Color;
		for (const TCHAR* Title : Titles)
		{
			Job.LevelTitles.Add(Title);
		}
		return Job;
	}

	FLifeShopItem MakeItem(FName Id, const TCHAR* Name, int32 Price, const FLifeNeedValues& Restore, const FLinearColor& Color)
	{
		FLifeShopItem Item;
		Item.Id = Id;
		Item.DisplayName = Name;
		Item.Price = Price;
		Item.Restore = Restore;
		Item.PropColor = Color;
		return Item;
	}

	FLifeActivityTuning MakeActivity(float Minutes, const FLifeNeedValues& Rate, float Multiplier)
	{
		FLifeActivityTuning T;
		T.DurationMinutes = Minutes;
		T.RatePerHour = Rate;
		T.TimeMultiplier = Multiplier;
		return T;
	}
}

ULifeSettings::ULifeSettings()
{
	// Time: 1 real second = 1 game minute -> a 24 minute day.
	RealSecondsPerGameMinute = 1.0f;
	StartHour = 8.0f;
	WakeHour = 7.0f;
	SleepTimeMultiplier = 60.f;
	MaxSunElevation = 62.f;

	// Needs: decay sized so a well-run day needs ~2 meals, 1 sleep, 1 shower, some fun and a chat.
	StartingNeeds = FLifeNeedValues(80.f, 85.f, 75.f, 70.f, 60.f);
	DecayPerHour = FLifeNeedValues(5.5f, 4.0f, 3.0f, 4.5f, 3.0f);
	CriticalThreshold = 15.f;
	SprintEnergyMultiplier = 2.5f;
	ConversationSocialGain = 12.f;
	ConversationRelationshipGain = 6;

	Sleep = MakeActivity(0.f, FLifeNeedValues(0.f, 14.f, 0.f, 0.f, 0.f), 60.f);
	Shower = MakeActivity(20.f, FLifeNeedValues(0.f, 0.f, 240.f, 6.f, 0.f), 10.f);
	WatchTV = MakeActivity(60.f, FLifeNeedValues(0.f, 2.f, 0.f, 35.f, 0.f), 12.f);
	Workout = MakeActivity(60.f, FLifeNeedValues(-4.f, -12.f, -18.f, 18.f, 0.f), 12.f);
	FitnessPerWorkoutHour = 0.5f;
	CharismaPerConversation = 0.1f;
	FridgeMealCost = 6;
	FridgeMealRestore = FLifeNeedValues(35.f, 2.f, 0.f, 3.f, 0.f);

	WalkSpeed = 220.f;
	RunSpeed = 520.f;
	RunSpeedPerFitness = 0.03f;
	LowEnergyThreshold = 20.f;
	LowEnergySpeedMultiplier = 0.65f;

	StartingCash = 120;
	StartingBank = 600;
	DailyRent = 45;
	DailyBankInterest = 0.002f;
	ATMWithdrawAmount = 100;
	CollapseFee = 80;

	Jobs.Add(MakeJob(TEXT("Barista"), TEXT("바리스타"), 16.f, 7, 15,
		FLifeNeedValues(1.f, 2.f, 2.f, 1.f, -2.f), FLinearColor(0.55f, 0.27f, 0.07f),
		{ TEXT("수습 바리스타"), TEXT("바리스타"), TEXT("시니어 바리스타"), TEXT("매장 리더"), TEXT("카페 매니저") }));
	Jobs.Add(MakeJob(TEXT("OfficeClerk"), TEXT("사무원"), 22.f, 9, 17,
		FLifeNeedValues(1.f, 3.f, 1.f, 3.f, 0.f), FLinearColor(0.1f, 0.3f, 0.7f),
		{ TEXT("인턴"), TEXT("사원"), TEXT("대리"), TEXT("팀장"), TEXT("이사") }));
	Jobs.Add(MakeJob(TEXT("Mechanic"), TEXT("정비공"), 20.f, 12, 20,
		FLifeNeedValues(2.f, 4.f, 6.f, 0.f, 0.f), FLinearColor(0.8f, 0.35f, 0.05f),
		{ TEXT("견습 정비공"), TEXT("정비공"), TEXT("선임 정비공"), TEXT("정비 마스터"), TEXT("정비소 사장") }));
	Jobs.Add(MakeJob(TEXT("NightGuard"), TEXT("야간 경비원"), 19.f, 22, 6,
		FLifeNeedValues(1.f, 3.f, 0.f, 2.f, 1.f), FLinearColor(0.15f, 0.15f, 0.2f),
		{ TEXT("경비원"), TEXT("선임 경비원"), TEXT("순찰 팀장"), TEXT("보안 실장"), TEXT("보안 총괄") }));

	ShopItems.Add(MakeItem(TEXT("Coffee"), TEXT("커피"), 4, FLifeNeedValues(3.f, 18.f, 0.f, 4.f, 0.f), FLinearColor(0.3f, 0.15f, 0.05f)));
	ShopItems.Add(MakeItem(TEXT("Burger"), TEXT("버거 세트"), 12, FLifeNeedValues(55.f, 4.f, 0.f, 8.f, 0.f), FLinearColor(0.85f, 0.5f, 0.15f)));
	ShopItems.Add(MakeItem(TEXT("Salad"), TEXT("신선한 샐러드"), 9, FLifeNeedValues(35.f, 6.f, 0.f, 2.f, 0.f), FLinearColor(0.2f, 0.7f, 0.2f)));
	ShopItems.Add(MakeItem(TEXT("EnergyDrink"), TEXT("에너지 드링크"), 5, FLifeNeedValues(2.f, 25.f, 0.f, 3.f, 0.f), FLinearColor(0.1f, 0.8f, 0.9f)));
	ShopItems.Add(MakeItem(TEXT("ProteinShake"), TEXT("프로틴 셰이크"), 7, FLifeNeedValues(20.f, 10.f, 0.f, 2.f, 0.f), FLinearColor(0.9f, 0.85f, 0.75f)));

	TownSeed = 1337;
	BlocksPerSide = 5;
	BlockSize = 5200.f;
	RoadWidth = 1300.f;
	SidewalkWidth = 380.f;
	CitizenCount = 28;
	TrafficCarCount = 8;
	bStreetLightShadows = false;
	CitizenNames = {
		TEXT("민준"), TEXT("서연"), TEXT("지호"), TEXT("하은"), TEXT("도윤"), TEXT("수아"), TEXT("시우"), TEXT("지유"),
		TEXT("예준"), TEXT("서윤"), TEXT("주원"), TEXT("하린"), TEXT("건우"), TEXT("지아"), TEXT("우진"), TEXT("채원"),
		TEXT("현우"), TEXT("다은"), TEXT("선우"), TEXT("예린"), TEXT("유준"), TEXT("소율"), TEXT("정우"), TEXT("윤서"),
		TEXT("승민"), TEXT("가은"), TEXT("태윤"), TEXT("나은"), TEXT("은호"), TEXT("지민") };
	SmallTalkLines = {
		TEXT("날씨 정말 좋네요, 그렇죠?"),
		TEXT("다이너 버거 먹어 봤어요?"),
		TEXT("정비소에서 사람 구한대요."),
		TEXT("이따가 헬스장 갈 거예요."),
		TEXT("월세가 또 올랐어요..."),
		TEXT("어제 노을 봤어요?"),
		TEXT("공원 산책하는 게 너무 좋아요."),
		TEXT("오늘 일 진짜 힘들었어요."),
		TEXT("또 만나서 반가워요!"),
		TEXT("은행에 저축하면 이자가 조금 붙어요.") };

	// Speeds in cm/s: 2800 cm/s ~= 100 km/h.
	CarMaxSpeed = 2800.f;
	CarMaxReverseSpeed = 700.f;
	CarAcceleration = 900.f;
	CarBrakeDeceleration = 2400.f;
	CarDrag = 0.25f;
	CarSteerDegreesPerSecond = 75.f;
	TrafficCruiseSpeed = 1000.f;

	SaveSlotName = TEXT("LifeRP_Save0");
	bAutoLoadOnStart = true;
	bAutoSaveOnWake = true;
}

const FLifeJobDef* ULifeSettings::FindJob(FName JobId) const
{
	return Jobs.FindByPredicate([JobId](const FLifeJobDef& J) { return J.Id == JobId; });
}

const FLifeShopItem* ULifeSettings::FindShopItem(FName ItemId) const
{
	return ShopItems.FindByPredicate([ItemId](const FLifeShopItem& I) { return I.Id == ItemId; });
}
