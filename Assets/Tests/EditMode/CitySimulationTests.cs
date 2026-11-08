using CityGame.Core;
using NUnit.Framework;

namespace CityGame.Tests
{
    /// <summary>Unit tests for the engine-free city simulation (design/gdd/city-builder.md § Acceptance Criteria).</summary>
    public class CitySimulationTests
    {
        private const int Seed = 1234;

        /// <summary>Always returns the same roll, so chance checks are deterministic.</summary>
        private sealed class FixedRandom : IRandomSource
        {
            private readonly double value;
            public FixedRandom(double value) { this.value = value; }
            public double NextDouble() { return value; }
        }

        private static CityConfig SmallConfig()
        {
            return new CityConfig { GridSize = 16, StartingMoney = 10000 };
        }

        private static CitySimulation NewSim(double roll = 0.0)
        {
            return new CitySimulation(SmallConfig(), new FixedRandom(roll));
        }

        [Test]
        public void PlaceRoad_DeductsCost()
        {
            var sim = NewSim();
            long before = sim.Money;

            PlaceResult r = sim.TryPlace(ToolType.Road, 2, 2);

            Assert.AreEqual(PlaceResult.Placed, r);
            Assert.AreEqual(before - sim.Config.CostOf(ToolType.Road), sim.Money);
            Assert.AreEqual(TileType.Road, sim.Grid.TypeAt(2, 2));
        }

        [Test]
        public void PlaceOnOccupiedTile_IsRejected()
        {
            var sim = NewSim();
            sim.TryPlace(ToolType.Road, 2, 2);
            long before = sim.Money;

            PlaceResult r = sim.TryPlace(ToolType.Residential, 2, 2);

            Assert.AreEqual(PlaceResult.Occupied, r);
            Assert.AreEqual(before, sim.Money);
        }

        [Test]
        public void PlaceWithoutFunds_IsRejected()
        {
            var config = SmallConfig();
            config.StartingMoney = 5;
            var sim = new CitySimulation(config, new FixedRandom(0));

            Assert.AreEqual(PlaceResult.InsufficientFunds, sim.TryPlace(ToolType.Residential, 1, 1));
            Assert.AreEqual(TileType.Empty, sim.Grid.TypeAt(1, 1));
        }

        [Test]
        public void PlaceOutOfBounds_IsRejected()
        {
            var sim = NewSim();

            Assert.AreEqual(PlaceResult.OutOfBounds, sim.TryPlace(ToolType.Road, -1, 0));
            Assert.AreEqual(PlaceResult.OutOfBounds, sim.TryPlace(ToolType.Road, 16, 0));
        }

        [Test]
        public void Bulldoze_ClearsTile_AndEmptyIsRejected()
        {
            var sim = NewSim();
            sim.TryPlace(ToolType.Park, 3, 3);

            Assert.AreEqual(PlaceResult.Placed, sim.TryPlace(ToolType.Bulldoze, 3, 3));
            Assert.AreEqual(TileType.Empty, sim.Grid.TypeAt(3, 3));
            Assert.AreEqual(PlaceResult.NothingToRemove, sim.TryPlace(ToolType.Bulldoze, 3, 3));
        }

        [Test]
        public void ZoneWithRoad_GrowsWhenDemandPositive()
        {
            var sim = NewSim(roll: 0.0);
            sim.TryPlace(ToolType.Road, 4, 4);
            sim.TryPlace(ToolType.Residential, 4, 5);

            sim.AdvanceDay();

            Assert.AreEqual(1, sim.Grid.Get(4, 5).Level);
            Assert.AreEqual(sim.Config.ResidentsPerLevel[1], sim.Population);
        }

        [Test]
        public void ZoneWithoutRoad_NeverGrows()
        {
            var sim = NewSim(roll: 0.0);
            sim.TryPlace(ToolType.Residential, 8, 8);

            for (int i = 0; i < 60; i++) sim.AdvanceDay();

            Assert.AreEqual(0, sim.Grid.Get(8, 8).Level);
            Assert.AreEqual(0, sim.Population);
        }

        [Test]
        public void HighRoll_PreventsGrowth()
        {
            var sim = NewSim(roll: 0.999);
            sim.TryPlace(ToolType.Road, 4, 4);
            sim.TryPlace(ToolType.Residential, 4, 5);

            for (int i = 0; i < 10; i++) sim.AdvanceDay();

            Assert.AreEqual(0, sim.Grid.Get(4, 5).Level);
        }

        [Test]
        public void Park_RaisesNearbyHappiness_IndustryLowersIt()
        {
            var sim = NewSim();
            int baseline = sim.LocalHappiness(5, 5, 0);
            sim.TryPlace(ToolType.Park, 6, 5);
            int withPark = sim.LocalHappiness(5, 5, 0);

            Assert.AreEqual(sim.Config.BaseHappiness, baseline);
            Assert.AreEqual(baseline + sim.Config.ParkBonus, withPark);

            var data = sim.CreateSaveData();
            data.types[5 * 16 + 4] = (int)TileType.Industrial;
            data.levels[5 * 16 + 4] = 2;
            sim.LoadSaveData(data);

            Assert.AreEqual(withPark - 2 * sim.Config.PollutionPerIndustryLevel, sim.LocalHappiness(5, 5, 0));
        }

        [Test]
        public void MonthEnd_CollectsTaxAndPaysUpkeep()
        {
            var sim = NewSim(roll: 0.0);
            sim.TryPlace(ToolType.Road, 4, 4);
            sim.TryPlace(ToolType.Residential, 4, 5);
            bool ended = false;
            sim.MonthEnded += () => ended = true;

            for (int i = 0; i < sim.Config.DaysPerMonth; i++) sim.AdvanceDay();

            Assert.IsTrue(ended);
            Assert.Greater(sim.LastMonthIncome, 0);
            Assert.AreEqual(sim.Config.UpkeepOf(TileType.Road), sim.LastMonthExpenses);
        }

        [Test]
        public void TaxAboveDefault_ReducesHappiness()
        {
            var sim = NewSim();
            int before = sim.Happiness;

            sim.SetTaxRate(sim.Config.DefaultTaxRate + 5);

            Assert.AreEqual(before - 5 * sim.Config.HappinessPerTaxPoint, sim.Happiness);
        }

        [Test]
        public void TaxRate_IsClamped()
        {
            var sim = NewSim();

            sim.SetTaxRate(999);
            Assert.AreEqual(sim.Config.MaxTaxRate, sim.TaxRate);
            sim.SetTaxRate(-5);
            Assert.AreEqual(sim.Config.MinTaxRate, sim.TaxRate);
        }

        [Test]
        public void Jobs_CreateResidentialDemand()
        {
            var sim = NewSim();
            float before = sim.ResidentialDemand;
            var data = sim.CreateSaveData();
            data.types[0] = (int)TileType.Industrial;
            data.levels[0] = 3;

            sim.LoadSaveData(data);

            Assert.Greater(sim.ResidentialDemand, before);
        }

        [Test]
        public void SaveLoad_RoundTripsState()
        {
            var sim = new CitySimulation(SmallConfig(), new SystemRandomSource(Seed));
            sim.TryPlace(ToolType.Road, 1, 1);
            sim.TryPlace(ToolType.Commercial, 1, 2);
            for (int i = 0; i < 45; i++) sim.AdvanceDay();
            CitySaveData data = sim.CreateSaveData();

            var restored = new CitySimulation(SmallConfig(), new SystemRandomSource(Seed));
            Assert.IsTrue(restored.LoadSaveData(data));

            Assert.AreEqual(sim.Money, restored.Money);
            Assert.AreEqual(sim.TotalDays, restored.TotalDays);
            Assert.AreEqual(sim.Grid.Get(1, 2).Level, restored.Grid.Get(1, 2).Level);
            Assert.AreEqual(TileType.Commercial, restored.Grid.TypeAt(1, 2));
        }

        [Test]
        public void LoadSaveData_WrongSize_IsRejected()
        {
            var sim = NewSim();
            var data = new CitySaveData { grid_size = 8, types = new int[64], levels = new int[64] };

            Assert.IsFalse(sim.LoadSaveData(data));
        }

        [Test]
        public void Milestone_FiresWhenPopulationCrossesThreshold()
        {
            var config = SmallConfig();
            config.MilestonePopulation = new[] { 0, 8 };
            var sim = new CitySimulation(config, new FixedRandom(0));
            int reached = -1;
            sim.MilestoneReached += m => reached = m;
            sim.TryPlace(ToolType.Road, 4, 4);
            sim.TryPlace(ToolType.Residential, 4, 5);

            sim.AdvanceDay();

            Assert.AreEqual(1, reached);
            Assert.AreEqual(1, sim.MilestoneIndex);
        }

        [Test]
        public void ConfigDto_OverridesOnlyProvidedValues()
        {
            var dto = new CityConfigDto { grid_size = 20, cost_road = 33 };

            CityConfig c = dto.ToConfig();

            Assert.AreEqual(20, c.GridSize);
            Assert.AreEqual(33, c.CostOf(ToolType.Road));
            Assert.AreEqual(new CityConfig().CostOf(ToolType.Park), c.CostOf(ToolType.Park));
        }
    }
}
