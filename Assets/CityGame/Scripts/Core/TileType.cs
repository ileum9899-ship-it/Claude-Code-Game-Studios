namespace CityGame.Core
{
    /// <summary>What occupies a single grid cell.</summary>
    public enum TileType
    {
        Empty = 0,
        Road = 1,
        Residential = 2,
        Commercial = 3,
        Industrial = 4,
        Park = 5,
    }

    /// <summary>A build tool the player can select.</summary>
    public enum ToolType
    {
        None = 0,
        Road = 1,
        Residential = 2,
        Commercial = 3,
        Industrial = 4,
        Park = 5,
        Bulldoze = 6,
    }

    /// <summary>Outcome of a placement attempt.</summary>
    public enum PlaceResult
    {
        Placed,
        OutOfBounds,
        Occupied,
        NothingToRemove,
        InsufficientFunds,
        NoTool,
    }

    /// <summary>Helpers for mapping tools to tiles.</summary>
    public static class TileTypes
    {
        /// <summary>True for the three growable zone types.</summary>
        public static bool IsZone(TileType t)
        {
            return t == TileType.Residential || t == TileType.Commercial || t == TileType.Industrial;
        }

        /// <summary>The tile a build tool produces, or Empty for None/Bulldoze.</summary>
        public static TileType TileFor(ToolType tool)
        {
            switch (tool)
            {
                case ToolType.Road: return TileType.Road;
                case ToolType.Residential: return TileType.Residential;
                case ToolType.Commercial: return TileType.Commercial;
                case ToolType.Industrial: return TileType.Industrial;
                case ToolType.Park: return TileType.Park;
                default: return TileType.Empty;
            }
        }
    }
}
