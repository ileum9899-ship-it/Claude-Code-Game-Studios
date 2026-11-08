namespace CityGame.Core
{
    /// <summary>State of one grid cell. Mutable struct stored in <see cref="CityGrid"/>.</summary>
    public struct Tile
    {
        /// <summary>What is built here.</summary>
        public TileType Type;

        /// <summary>Development level for zones: 0 = zoned but empty, up to the configured max.</summary>
        public int Level;

        /// <summary>True when an orthogonal neighbour is a road (recomputed every day).</summary>
        public bool HasRoadAccess;

        /// <summary>Local happiness 0..100 (recomputed every day).</summary>
        public int Happiness;
    }
}
