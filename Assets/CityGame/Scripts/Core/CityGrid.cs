using System;

namespace CityGame.Core
{
    /// <summary>Square grid of tiles addressed by (x, y).</summary>
    public sealed class CityGrid
    {
        private readonly Tile[] tiles;

        /// <summary>Width and height in cells.</summary>
        public int Size { get; }

        /// <summary>Creates an empty grid.</summary>
        public CityGrid(int size)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            Size = size;
            tiles = new Tile[size * size];
        }

        /// <summary>True when (x, y) is on the grid.</summary>
        public bool InBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Size && y < Size;
        }

        /// <summary>Reads a tile. Out-of-bounds reads return an empty tile.</summary>
        public Tile Get(int x, int y)
        {
            return InBounds(x, y) ? tiles[y * Size + x] : default(Tile);
        }

        /// <summary>Writes a tile. Out-of-bounds writes are ignored.</summary>
        public void Set(int x, int y, Tile tile)
        {
            if (InBounds(x, y)) tiles[y * Size + x] = tile;
        }

        /// <summary>Type of the tile at (x, y), Empty when out of bounds.</summary>
        public TileType TypeAt(int x, int y)
        {
            return Get(x, y).Type;
        }

        /// <summary>True when any of the four orthogonal neighbours is a road.</summary>
        public bool IsAdjacentToRoad(int x, int y)
        {
            return TypeAt(x + 1, y) == TileType.Road || TypeAt(x - 1, y) == TileType.Road
                || TypeAt(x, y + 1) == TileType.Road || TypeAt(x, y - 1) == TileType.Road;
        }

        /// <summary>Counts tiles of a type.</summary>
        public int Count(TileType type)
        {
            int n = 0;
            for (int i = 0; i < tiles.Length; i++) if (tiles[i].Type == type) n++;
            return n;
        }
    }
}
