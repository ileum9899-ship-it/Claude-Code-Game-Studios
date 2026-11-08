using System;

namespace CityGame.Core
{
    /// <summary>
    /// Engine-free city simulation: placement, daily growth, monthly budget.
    /// The Unity layer drives it by calling <see cref="AdvanceDay"/> on a timer and
    /// listens to <see cref="TileChanged"/> to update visuals.
    /// </summary>
    /// <example>
    /// <code>
    /// var sim = new CitySimulation(new CityConfig(), new SystemRandomSource(1));
    /// sim.TryPlace(ToolType.Road, 5, 5);
    /// sim.TryPlace(ToolType.Residential, 5, 6);
    /// for (int i = 0; i &lt; 30; i++) sim.AdvanceDay();
    /// </code>
    /// </example>
    public sealed class CitySimulation
    {
        private readonly IRandomSource random;

        /// <summary>Active balance values.</summary>
        public CityConfig Config { get; }
        /// <summary>The tile grid.</summary>
        public CityGrid Grid { get; private set; }

        /// <summary>Current funds.</summary>
        public long Money { get; private set; }
        /// <summary>Tax rate in percent.</summary>
        public int TaxRate { get; private set; }
        /// <summary>Days elapsed since founding.</summary>
        public int TotalDays { get; private set; }
        /// <summary>Day of month, 1-based.</summary>
        public int Day => TotalDays % Config.DaysPerMonth + 1;
        /// <summary>Month of year, 1-based.</summary>
        public int Month => TotalDays / Config.DaysPerMonth % 12 + 1;
        /// <summary>Year, 1-based.</summary>
        public int Year => TotalDays / (Config.DaysPerMonth * 12) + 1;

        /// <summary>Total residents.</summary>
        public int Population { get; private set; }
        /// <summary>Total commercial jobs.</summary>
        public int CommercialJobs { get; private set; }
        /// <summary>Total industrial jobs.</summary>
        public int IndustrialJobs { get; private set; }
        /// <summary>Total jobs.</summary>
        public int Jobs => CommercialJobs + IndustrialJobs;
        /// <summary>Resident-weighted city happiness 0..100.</summary>
        public int Happiness { get; private set; }
        /// <summary>Residential demand -1..1.</summary>
        public float ResidentialDemand { get; private set; }
        /// <summary>Commercial demand -1..1.</summary>
        public float CommercialDemand { get; private set; }
        /// <summary>Industrial demand -1..1.</summary>
        public float IndustrialDemand { get; private set; }
        /// <summary>Tax collected at the last month end.</summary>
        public long LastMonthIncome { get; private set; }
        /// <summary>Upkeep paid at the last month end.</summary>
        public long LastMonthExpenses { get; private set; }
        /// <summary>Index into <see cref="CityConfig.MilestonePopulation"/> of the current title.</summary>
        public int MilestoneIndex { get; private set; }

        /// <summary>Raised when a tile's type or level changes.</summary>
        public event Action<int, int> TileChanged;
        /// <summary>Raised at each month end after the budget is applied.</summary>
        public event Action MonthEnded;
        /// <summary>Raised when the city reaches a new population milestone (argument: index).</summary>
        public event Action<int> MilestoneReached;

        /// <summary>Starts a new city.</summary>
        public CitySimulation(CityConfig config, IRandomSource random)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            Grid = new CityGrid(config.GridSize);
            Money = config.StartingMoney;
            TaxRate = config.DefaultTaxRate;
            RecomputeAll();
        }

        /// <summary>Cost of applying a tool at (x, y); 0 when it would do nothing.</summary>
        public int CostAt(ToolType tool, int x, int y)
        {
            return Validate(tool, x, y) == PlaceResult.Placed ? Config.CostOf(tool) : 0;
        }

        /// <summary>Checks a placement without applying it (funds included).</summary>
        public PlaceResult Validate(ToolType tool, int x, int y)
        {
            if (tool == ToolType.None) return PlaceResult.NoTool;
            if (!Grid.InBounds(x, y)) return PlaceResult.OutOfBounds;
            TileType current = Grid.TypeAt(x, y);
            if (tool == ToolType.Bulldoze)
            {
                if (current == TileType.Empty) return PlaceResult.NothingToRemove;
            }
            else if (current != TileType.Empty)
            {
                return PlaceResult.Occupied;
            }
            if (Money < Config.CostOf(tool)) return PlaceResult.InsufficientFunds;
            return PlaceResult.Placed;
        }

        /// <summary>Applies a tool to one cell, charging its cost.</summary>
        public PlaceResult TryPlace(ToolType tool, int x, int y)
        {
            PlaceResult result = Validate(tool, x, y);
            if (result != PlaceResult.Placed) return result;

            Money -= Config.CostOf(tool);
            var tile = new Tile { Type = TileTypes.TileFor(tool), Level = 0 };
            Grid.Set(x, y, tile);
            RecomputeAll();
            TileChanged?.Invoke(x, y);
            // Road access of neighbours may have changed; visuals for roads depend on neighbours too.
            NotifyNeighbours(x, y);
            return result;
        }

        /// <summary>Adds (or with a negative value removes) funds — grants, scenarios, debug.</summary>
        public void AddFunds(long amount)
        {
            Money += amount;
        }

        /// <summary>Sets the tax rate, clamped to the configured range.</summary>
        public void SetTaxRate(int percent)
        {
            TaxRate = Math.Max(Config.MinTaxRate, Math.Min(Config.MaxTaxRate, percent));
            RecomputeAll();
        }

        /// <summary>Advances one game day: growth/decay, then the month-end budget when due.</summary>
        public void AdvanceDay()
        {
            RecomputeAll();
            int size = Grid.Size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Tile t = Grid.Get(x, y);
                    if (!TileTypes.IsZone(t.Type)) continue;
                    int newLevel = NextLevel(t, DemandFor(t.Type));
                    if (newLevel != t.Level)
                    {
                        t.Level = newLevel;
                        Grid.Set(x, y, t);
                        TileChanged?.Invoke(x, y);
                    }
                }
            }

            TotalDays++;
            RecomputeAll();
            if (TotalDays % Config.DaysPerMonth == 0) EndMonth();
            CheckMilestone();
        }

        /// <summary>Demand for a zone type, 0 for non-zones.</summary>
        public float DemandFor(TileType type)
        {
            switch (type)
            {
                case TileType.Residential: return ResidentialDemand;
                case TileType.Commercial: return CommercialDemand;
                case TileType.Industrial: return IndustrialDemand;
                default: return 0f;
            }
        }

        /// <summary>Snapshot for saving.</summary>
        public CitySaveData CreateSaveData()
        {
            int size = Grid.Size;
            var data = new CitySaveData
            {
                grid_size = size,
                money = Money,
                tax_rate = TaxRate,
                total_days = TotalDays,
                types = new int[size * size],
                levels = new int[size * size],
            };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Tile t = Grid.Get(x, y);
                    data.types[y * size + x] = (int)t.Type;
                    data.levels[y * size + x] = t.Level;
                }
            return data;
        }

        /// <summary>Restores a snapshot. Returns false (and changes nothing) if it does not fit this config.</summary>
        public bool LoadSaveData(CitySaveData data)
        {
            if (data == null || data.types == null || data.levels == null) return false;
            int size = data.grid_size;
            if (size != Config.GridSize || data.types.Length != size * size || data.levels.Length != size * size) return false;

            var grid = new CityGrid(size);
            for (int i = 0; i < size * size; i++)
            {
                var type = (TileType)data.types[i];
                if (!Enum.IsDefined(typeof(TileType), type)) type = TileType.Empty;
                int level = TileTypes.IsZone(type) ? Math.Max(0, Math.Min(Config.MaxLevel, data.levels[i])) : 0;
                grid.Set(i % size, i / size, new Tile { Type = type, Level = level });
            }
            Grid = grid;
            Money = data.money;
            TaxRate = Math.Max(Config.MinTaxRate, Math.Min(Config.MaxTaxRate, data.tax_rate));
            TotalDays = Math.Max(0, data.total_days);
            RecomputeAll();
            MilestoneIndex = MilestoneFor(Population);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    TileChanged?.Invoke(x, y);
            return true;
        }

        private int NextLevel(Tile t, float demand)
        {
            if (!t.HasRoadAccess)
                return t.Level > 0 && Roll(Config.DecayChancePerDay * 2f) ? t.Level - 1 : t.Level;

            bool tooUnhappy = t.Level > 0 && t.Happiness < Config.HappinessForLevel[Math.Min(t.Level, Config.HappinessForLevel.Length - 1)];
            if ((demand < Config.DecayDemandThreshold || tooUnhappy) && t.Level > 0)
                return Roll(Config.DecayChancePerDay) ? t.Level - 1 : t.Level;

            int next = t.Level + 1;
            if (next > Config.MaxLevel || demand <= 0f) return t.Level;
            if (t.Happiness < Config.HappinessForLevel[Math.Min(next, Config.HappinessForLevel.Length - 1)]) return t.Level;
            return Roll(Config.GrowthChancePerDay * demand) ? next : t.Level;
        }

        private bool Roll(float chance)
        {
            return random.NextDouble() < chance;
        }

        private void EndMonth()
        {
            long income = (long)Math.Round((Population * Config.TaxPerResident + Jobs * Config.TaxPerJob) * TaxRate / 100.0);
            long expenses = 0;
            int size = Grid.Size;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    expenses += Config.UpkeepOf(Grid.TypeAt(x, y));
            Money += income - expenses;
            LastMonthIncome = income;
            LastMonthExpenses = expenses;
            MonthEnded?.Invoke();
        }

        private void CheckMilestone()
        {
            int m = MilestoneFor(Population);
            if (m > MilestoneIndex)
            {
                MilestoneIndex = m;
                MilestoneReached?.Invoke(m);
            }
        }

        private int MilestoneFor(int population)
        {
            int[] steps = Config.MilestonePopulation;
            int index = 0;
            for (int i = 0; i < steps.Length; i++) if (population >= steps[i]) index = i;
            return index;
        }

        private void NotifyNeighbours(int x, int y)
        {
            if (TileChanged == null) return;
            if (Grid.InBounds(x + 1, y)) TileChanged(x + 1, y);
            if (Grid.InBounds(x - 1, y)) TileChanged(x - 1, y);
            if (Grid.InBounds(x, y + 1)) TileChanged(x, y + 1);
            if (Grid.InBounds(x, y - 1)) TileChanged(x, y - 1);
        }

        /// <summary>Recomputes road access, local happiness, totals and demand.</summary>
        private void RecomputeAll()
        {
            int size = Grid.Size;
            int pop = 0, com = 0, ind = 0;
            long happinessWeighted = 0;
            int taxModifier = (Config.DefaultTaxRate - TaxRate) * Config.HappinessPerTaxPoint;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Tile t = Grid.Get(x, y);
                    t.HasRoadAccess = Grid.IsAdjacentToRoad(x, y);
                    t.Happiness = TileTypes.IsZone(t.Type) ? LocalHappiness(x, y, taxModifier) : 0;
                    Grid.Set(x, y, t);

                    int cap = Config.CapacityOf(t.Type, t.Level);
                    if (t.Type == TileType.Residential) { pop += cap; happinessWeighted += (long)cap * t.Happiness; }
                    else if (t.Type == TileType.Commercial) com += cap;
                    else if (t.Type == TileType.Industrial) ind += cap;
                }
            }

            Population = pop;
            CommercialJobs = com;
            IndustrialJobs = ind;
            Happiness = pop > 0
                ? (int)(happinessWeighted / pop)
                : Clamp(Config.BaseHappiness + taxModifier, 0, 100);

            float scale = Math.Max(1f, Config.DemandScale);
            ResidentialDemand = ClampDemand((Config.BaseResidentialDemand + (com + ind) * Config.ResidentsPerJob - pop) / scale);
            CommercialDemand = ClampDemand((Config.BaseCommercialDemand + pop * Config.CommercialJobsPerResident - com) / scale);
            IndustrialDemand = ClampDemand((Config.BaseIndustrialDemand + pop * Config.IndustrialJobsPerResident - ind) / scale);
        }

        /// <summary>Happiness at a cell: base + parks − pollution ± tax, clamped 0..100.</summary>
        public int LocalHappiness(int x, int y, int taxModifier)
        {
            int park = 0, pollution = 0;
            int r = Math.Max(Config.ParkRadius, Config.PollutionRadius);
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    Tile n = Grid.Get(x + dx, y + dy);
                    int d = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    if (n.Type == TileType.Park && d <= Config.ParkRadius) park += Config.ParkBonus;
                    else if (n.Type == TileType.Industrial && d <= Config.PollutionRadius) pollution += n.Level * Config.PollutionPerIndustryLevel;
                }
            }
            if (Grid.TypeAt(x, y) == TileType.Industrial) pollution = 0; // factories don't mind factories
            return Clamp(Config.BaseHappiness + Math.Min(park, Config.ParkBonusCap) - pollution + taxModifier, 0, 100);
        }

        private static float ClampDemand(float v)
        {
            return v < -1f ? -1f : (v > 1f ? 1f : v);
        }

        private static int Clamp(int v, int min, int max)
        {
            return v < min ? min : (v > max ? max : v);
        }
    }

    /// <summary>Serializable save snapshot (snake_case wire format, JsonUtility-compatible).</summary>
    [Serializable]
    public sealed class CitySaveData
    {
        public int grid_size;
        public long money;
        public int tax_rate;
        public int total_days;
        public int[] types;
        public int[] levels;
    }
}
