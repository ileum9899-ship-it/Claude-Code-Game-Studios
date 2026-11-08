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

	Jobs.Add(MakeJob(TEXT("Barista"), TEXT("Barista"), 16.f, 7, 15,
		FLifeNeedValues(1.f, 2.f, 2.f, 1.f, -2.f), FLinearColor(0.55f, 0.27f, 0.07f),
		{ TEXT("Trainee Barista"), TEXT("Barista"), TEXT("Senior Barista"), TEXT("Shift Lead"), TEXT("Cafe Manager") }));
	Jobs.Add(MakeJob(TEXT("OfficeClerk"), TEXT("Office Clerk"), 22.f, 9, 17,
		FLifeNeedValues(1.f, 3.f, 1.f, 3.f, 0.f), FLinearColor(0.1f, 0.3f, 0.7f),
		{ TEXT("Intern"), TEXT("Clerk"), TEXT("Analyst"), TEXT("Team Lead"), TEXT("Director") }));
	Jobs.Add(MakeJob(TEXT("Mechanic"), TEXT("Mechanic"), 20.f, 12, 20,
		FLifeNeedValues(2.f, 4.f, 6.f, 0.f, 0.f), FLinearColor(0.8f, 0.35f, 0.05f),
		{ TEXT("Apprentice"), TEXT("Mechanic"), TEXT("Senior Mechanic"), TEXT("Master Mechanic"), TEXT("Garage Owner") }));
	Jobs.Add(MakeJob(TEXT("NightGuard"), TEXT("Night Guard"), 19.f, 22, 6,
		FLifeNeedValues(1.f, 3.f, 0.f, 2.f, 1.f), FLinearColor(0.15f, 0.15f, 0.2f),
		{ TEXT("Guard"), TEXT("Senior Guard"), TEXT("Patrol Lead"), TEXT("Security Chief"), TEXT("Head of Security") }));

	ShopItems.Add(MakeItem(TEXT("Coffee"), TEXT("Coffee"), 4, FLifeNeedValues(3.f, 18.f, 0.f, 4.f, 0.f), FLinearColor(0.3f, 0.15f, 0.05f)));
	ShopItems.Add(MakeItem(TEXT("Burger"), TEXT("Burger Meal"), 12, FLifeNeedValues(55.f, 4.f, 0.f, 8.f, 0.f), FLinearColor(0.85f, 0.5f, 0.15f)));
	ShopItems.Add(MakeItem(TEXT("Salad"), TEXT("Fresh Salad"), 9, FLifeNeedValues(35.f, 6.f, 0.f, 2.f, 0.f), FLinearColor(0.2f, 0.7f, 0.2f)));
	ShopItems.Add(MakeItem(TEXT("EnergyDrink"), TEXT("Energy Drink"), 5, FLifeNeedValues(2.f, 25.f, 0.f, 3.f, 0.f), FLinearColor(0.1f, 0.8f, 0.9f)));
	ShopItems.Add(MakeItem(TEXT("ProteinShake"), TEXT("Protein Shake"), 7, FLifeNeedValues(20.f, 10.f, 0.f, 2.f, 0.f), FLinearColor(0.9f, 0.85f, 0.75f)));

	TownSeed = 1337;
	BlocksPerSide = 5;
	BlockSize = 5200.f;
	RoadWidth = 1300.f;
	SidewalkWidth = 380.f;
	CitizenCount = 28;
	TrafficCarCount = 8;
	bStreetLightShadows = false;
	CitizenNames = {
		TEXT("Maya"), TEXT("Leo"), TEXT("Aiko"), TEXT("Sam"), TEXT("Priya"), TEXT("Jonas"), TEXT("Elena"), TEXT("Kwame"),
		TEXT("Noor"), TEXT("Diego"), TEXT("Hana"), TEXT("Theo"), TEXT("Zara"), TEXT("Mateo"), TEXT("Ines"), TEXT("Ravi"),
		TEXT("Lena"), TEXT("Omar"), TEXT("Yuki"), TEXT("Felix"), TEXT("Ada"), TEXT("Bruno"), TEXT("Chloe"), TEXT("Dev"),
		TEXT("Freya"), TEXT("Gus"), TEXT("Iris"), TEXT("Kai"), TEXT("Luz"), TEXT("Nico") };
	SmallTalkLines = {
		TEXT("Lovely day, isn't it?"),
		TEXT("Have you tried the burgers at the diner?"),
		TEXT("The garage is hiring, you know."),
		TEXT("I'm heading to the gym later."),
		TEXT("Rent went up again..."),
		TEXT("Did you see the sunset yesterday?"),
		TEXT("I love walking through the park."),
		TEXT("Work was brutal today."),
		TEXT("Good to see you again!"),
		TEXT("The bank pays a little interest if you save.") };

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
