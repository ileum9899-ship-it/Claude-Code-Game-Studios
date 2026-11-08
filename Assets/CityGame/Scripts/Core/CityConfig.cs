using System;

namespace CityGame.Core
{
    /// <summary>
    /// Every tunable gameplay value. Loaded from <c>Resources/Data/city_balance.json</c>
    /// through <see cref="CityConfigDto"/>; the defaults here match that file and are
    /// used when it is missing. See design/gdd/city-builder.md § Tuning Knobs.
    /// </summary>
    public sealed class CityConfig
    {
        /// <summary>Grid width/height in cells.</summary>
        public int GridSize = 48;
        /// <summary>Money at the start of a new city.</summary>
        public long StartingMoney = 20000;

        /// <summary>Build cost per cell, indexed by <see cref="ToolType"/>.</summary>
        public int[] ToolCost = { 0, 10, 100, 150, 200, 80, 5 };
        /// <summary>Monthly upkeep per tile, indexed by <see cref="TileType"/>.</summary>
        public int[] MonthlyUpkeep = { 0, 1, 0, 0, 0, 4, };

        /// <summary>Residents housed per residential level (index = level).</summary>
        public int[] ResidentsPerLevel = { 0, 8, 24, 60 };
        /// <summary>Jobs per commercial level.</summary>
        public int[] CommercialJobsPerLevel = { 0, 6, 16, 40 };
        /// <summary>Jobs per industrial level.</summary>
        public int[] IndustrialJobsPerLevel = { 0, 10, 24, 48 };
        /// <summary>Minimum local happiness needed to grow to each level (index = level).</summary>
        public int[] HappinessForLevel = { 0, 0, 45, 65 };

        /// <summary>Game days per month.</summary>
        public int DaysPerMonth = 30;
        /// <summary>Real seconds per game day at 1x speed.</summary>
        public float SecondsPerDay = 0.5f;

        /// <summary>Daily chance a served zone with positive demand grows one level (scaled by demand).</summary>
        public float GrowthChancePerDay = 0.12f;
        /// <summary>Daily chance a zone under negative demand or unhappy loses a level.</summary>
        public float DecayChancePerDay = 0.04f;
        /// <summary>Demand below this value causes decay.</summary>
        public float DecayDemandThreshold = -0.3f;

        /// <summary>Residential demand from nobody living here yet (bootstraps a new city).</summary>
        public float BaseResidentialDemand = 40f;
        /// <summary>Residents desired per job offered.</summary>
        public float ResidentsPerJob = 1.1f;
        /// <summary>Commercial jobs desired per resident.</summary>
        public float CommercialJobsPerResident = 0.3f;
        /// <summary>Industrial jobs desired per resident.</summary>
        public float IndustrialJobsPerResident = 0.45f;
        /// <summary>Base commercial demand with no residents.</summary>
        public float BaseCommercialDemand = 6f;
        /// <summary>Base industrial demand with no residents.</summary>
        public float BaseIndustrialDemand = 12f;
        /// <summary>Gap (in people/jobs) that maps to full demand of 1.0.</summary>
        public float DemandScale = 60f;

        /// <summary>Happiness with no modifiers.</summary>
        public int BaseHappiness = 55;
        /// <summary>Happiness added by each park within <see cref="ParkRadius"/>.</summary>
        public int ParkBonus = 8;
        /// <summary>Maximum total park bonus per tile.</summary>
        public int ParkBonusCap = 30;
        /// <summary>Chebyshev radius of a park's effect.</summary>
        public int ParkRadius = 3;
        /// <summary>Happiness removed by each industrial level within <see cref="PollutionRadius"/>.</summary>
        public int PollutionPerIndustryLevel = 4;
        /// <summary>Chebyshev radius of industrial pollution.</summary>
        public int PollutionRadius = 3;
        /// <summary>Default tax rate in percent.</summary>
        public int DefaultTaxRate = 9;
        /// <summary>Lowest and highest selectable tax rate.</summary>
        public int MinTaxRate = 0, MaxTaxRate = 20;
        /// <summary>Happiness lost per percentage point above the default tax rate (gained below).</summary>
        public int HappinessPerTaxPoint = 3;

        /// <summary>Monthly tax base per resident (multiplied by tax rate / 100).</summary>
        public float TaxPerResident = 30f;
        /// <summary>Monthly tax base per job.</summary>
        public float TaxPerJob = 25f;

        /// <summary>Population thresholds for city titles (ascending).</summary>
        public int[] MilestonePopulation = { 0, 100, 500, 1500, 4000, 10000 };

        /// <summary>Highest zone level.</summary>
        public int MaxLevel => ResidentsPerLevel.Length - 1;

        /// <summary>Cost to apply a tool to one cell.</summary>
        public int CostOf(ToolType tool)
        {
            int i = (int)tool;
            return i >= 0 && i < ToolCost.Length ? ToolCost[i] : 0;
        }

        /// <summary>Monthly upkeep of a tile type.</summary>
        public int UpkeepOf(TileType type)
        {
            int i = (int)type;
            return i >= 0 && i < MonthlyUpkeep.Length ? MonthlyUpkeep[i] : 0;
        }

        /// <summary>Residents or jobs a zone provides at a level.</summary>
        public int CapacityOf(TileType type, int level)
        {
            int[] table;
            switch (type)
            {
                case TileType.Residential: table = ResidentsPerLevel; break;
                case TileType.Commercial: table = CommercialJobsPerLevel; break;
                case TileType.Industrial: table = IndustrialJobsPerLevel; break;
                default: return 0;
            }
            if (level < 0) return 0;
            return table[Math.Min(level, table.Length - 1)];
        }
    }

    /// <summary>
    /// Wire format of <c>city_balance.json</c>. Keys are snake_case (the cross-engine
    /// data contract, see .claude/rules/data-files.md); this DTO maps them once into
    /// <see cref="CityConfig"/>. Any field absent from the file keeps the default.
    /// </summary>
    [Serializable]
    public sealed class CityConfigDto
    {
#pragma warning disable 0649 // assigned by JsonUtility / System.Text.Json
        public int grid_size;
        public long starting_money;
        public int cost_road, cost_residential, cost_commercial, cost_industrial, cost_park, cost_bulldoze;
        public int upkeep_road, upkeep_park;
        public int[] residents_per_level;
        public int[] commercial_jobs_per_level;
        public int[] industrial_jobs_per_level;
        public int[] happiness_for_level;
        public int days_per_month;
        public float seconds_per_day;
        public float growth_chance_per_day;
        public float decay_chance_per_day;
        public float decay_demand_threshold;
        public float base_residential_demand;
        public float residents_per_job;
        public float commercial_jobs_per_resident;
        public float industrial_jobs_per_resident;
        public float base_commercial_demand;
        public float base_industrial_demand;
        public float demand_scale;
        public int base_happiness;
        public int park_bonus, park_bonus_cap, park_radius;
        public int pollution_per_industry_level, pollution_radius;
        public int default_tax_rate, min_tax_rate, max_tax_rate, happiness_per_tax_point;
        public float tax_per_resident, tax_per_job;
        public int[] milestone_population;
#pragma warning restore 0649

        /// <summary>Maps the wire values onto defaults. Zero / null means "not set".</summary>
        public CityConfig ToConfig()
        {
            var c = new CityConfig();
            if (grid_size > 0) c.GridSize = grid_size;
            if (starting_money > 0) c.StartingMoney = starting_money;
            SetCost(c, ToolType.Road, cost_road);
            SetCost(c, ToolType.Residential, cost_residential);
            SetCost(c, ToolType.Commercial, cost_commercial);
            SetCost(c, ToolType.Industrial, cost_industrial);
            SetCost(c, ToolType.Park, cost_park);
            SetCost(c, ToolType.Bulldoze, cost_bulldoze);
            if (upkeep_road > 0) c.MonthlyUpkeep[(int)TileType.Road] = upkeep_road;
            if (upkeep_park > 0) c.MonthlyUpkeep[(int)TileType.Park] = upkeep_park;
            if (IsTable(residents_per_level)) c.ResidentsPerLevel = residents_per_level;
            if (IsTable(commercial_jobs_per_level)) c.CommercialJobsPerLevel = commercial_jobs_per_level;
            if (IsTable(industrial_jobs_per_level)) c.IndustrialJobsPerLevel = industrial_jobs_per_level;
            if (IsTable(happiness_for_level)) c.HappinessForLevel = happiness_for_level;
            if (days_per_month > 0) c.DaysPerMonth = days_per_month;
            if (seconds_per_day > 0) c.SecondsPerDay = seconds_per_day;
            if (growth_chance_per_day > 0) c.GrowthChancePerDay = growth_chance_per_day;
            if (decay_chance_per_day > 0) c.DecayChancePerDay = decay_chance_per_day;
            if (decay_demand_threshold < 0) c.DecayDemandThreshold = decay_demand_threshold;
            if (base_residential_demand > 0) c.BaseResidentialDemand = base_residential_demand;
            if (residents_per_job > 0) c.ResidentsPerJob = residents_per_job;
            if (commercial_jobs_per_resident > 0) c.CommercialJobsPerResident = commercial_jobs_per_resident;
            if (industrial_jobs_per_resident > 0) c.IndustrialJobsPerResident = industrial_jobs_per_resident;
            if (base_commercial_demand > 0) c.BaseCommercialDemand = base_commercial_demand;
            if (base_industrial_demand > 0) c.BaseIndustrialDemand = base_industrial_demand;
            if (demand_scale > 0) c.DemandScale = demand_scale;
            if (base_happiness > 0) c.BaseHappiness = base_happiness;
            if (park_bonus > 0) c.ParkBonus = park_bonus;
            if (park_bonus_cap > 0) c.ParkBonusCap = park_bonus_cap;
            if (park_radius > 0) c.ParkRadius = park_radius;
            if (pollution_per_industry_level > 0) c.PollutionPerIndustryLevel = pollution_per_industry_level;
            if (pollution_radius > 0) c.PollutionRadius = pollution_radius;
            if (default_tax_rate > 0) c.DefaultTaxRate = default_tax_rate;
            if (min_tax_rate > 0) c.MinTaxRate = min_tax_rate;
            if (max_tax_rate > 0) c.MaxTaxRate = max_tax_rate;
            if (happiness_per_tax_point > 0) c.HappinessPerTaxPoint = happiness_per_tax_point;
            if (tax_per_resident > 0) c.TaxPerResident = tax_per_resident;
            if (tax_per_job > 0) c.TaxPerJob = tax_per_job;
            if (milestone_population != null && milestone_population.Length > 0) c.MilestonePopulation = milestone_population;
            return c;
        }

        private static bool IsTable(int[] t)
        {
            return t != null && t.Length >= 2;
        }

        private static void SetCost(CityConfig c, ToolType tool, int value)
        {
            if (value > 0) c.ToolCost[(int)tool] = value;
        }
    }
}
