using System.Collections.Generic;
using CityGame.Core;
using UnityEngine;

namespace CityGame.Runtime
{
    /// <summary>
    /// Cosmetic traffic: cars wander the road network, driving on the right.
    /// The car count follows population. Cars are pooled; no per-frame allocation.
    /// </summary>
    public sealed class TrafficSystem : MonoBehaviour
    {
        private sealed class Car
        {
            public Transform Body;
            public Vector2Int From, To;
            public float Progress;
            public float Speed;
        }

        private static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        private static readonly Color[] Paints =
        {
            new Color(0.85f, 0.15f, 0.15f), new Color(0.15f, 0.35f, 0.85f), new Color(0.95f, 0.95f, 0.95f),
            new Color(0.95f, 0.75f, 0.1f), new Color(0.15f, 0.15f, 0.17f), new Color(0.2f, 0.65f, 0.35f),
        };

        private readonly List<Car> cars = new List<Car>();
        private readonly List<Vector2Int> options = new List<Vector2Int>(4);
        private CitySimulation sim;
        private CityMaterials materials;
        private Mesh cube;
        private int seed = 7;

        /// <summary>Speed multiplier from the game clock (0 = paused).</summary>
        public float TimeScale { get; set; } = 1f;

        /// <summary>Wires dependencies; call once after AddComponent.</summary>
        public void Init(CitySimulation simulation, CityMaterials mats)
        {
            sim = simulation;
            materials = mats;
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube = temp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temp);
        }

        private void Update()
        {
            if (sim == null) return;
            int target = TargetCount();
            while (cars.Count < target && TrySpawn()) { }
            while (cars.Count > target) Remove(cars.Count - 1);

            float dt = Time.deltaTime * TimeScale;
            for (int i = cars.Count - 1; i >= 0; i--)
            {
                Car car = cars[i];
                if (!IsRoad(car.From) || !IsRoad(car.To)) { Remove(i); continue; }
                car.Progress += dt * car.Speed;
                while (car.Progress >= 1f)
                {
                    car.Progress -= 1f;
                    Vector2Int next = PickNext(car.To, car.From);
                    car.From = car.To;
                    car.To = next;
                }
                Place(car);
            }
        }

        private int TargetCount()
        {
            int roads = sim.Grid.Count(TileType.Road);
            if (roads < 2) return 0;
            return Mathf.Min(roads / 2, 3 + sim.Population / 25, 120);
        }

        private bool TrySpawn()
        {
            int size = sim.Grid.Size;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                var from = new Vector2Int(NextInt(size), NextInt(size));
                if (!IsRoad(from)) continue;
                Vector2Int to = PickNext(from, new Vector2Int(-1, -1));
                if (to == from) continue;
                var go = new GameObject("Car");
                go.transform.SetParent(transform, false);
                AddPart(go.transform, Paints[NextInt(Paints.Length)], new Vector3(0f, 0.09f, 0f), new Vector3(0.14f, 0.08f, 0.26f));
                AddPart(go.transform, new Color(0.2f, 0.25f, 0.3f), new Vector3(0f, 0.16f, -0.01f), new Vector3(0.12f, 0.06f, 0.14f));
                var light = AddPart(go.transform, Color.white, new Vector3(0f, 0.09f, 0.13f), new Vector3(0.12f, 0.03f, 0.01f));
                light.GetComponent<MeshRenderer>().sharedMaterial = materials.Lamps;
                var car = new Car { Body = go.transform, From = from, To = to, Progress = NextInt(100) / 100f, Speed = 0.9f + NextInt(60) / 100f };
                cars.Add(car);
                Place(car);
                return true;
            }
            return false;
        }

        private GameObject AddPart(Transform parent, Color color, Vector3 pos, Vector3 scale)
        {
            var go = new GameObject("Part");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = cube;
            go.AddComponent<MeshRenderer>().sharedMaterial = materials.Get(color);
            return go;
        }

        private void Place(Car car)
        {
            Vector3 a = new Vector3(car.From.x + 0.5f, 0f, car.From.y + 0.5f);
            Vector3 b = new Vector3(car.To.x + 0.5f, 0f, car.To.y + 0.5f);
            Vector3 dir = b - a;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();
            Vector3 right = new Vector3(dir.z, 0f, -dir.x);
            car.Body.localPosition = Vector3.Lerp(a, b, car.Progress) + right * 0.2f;
            car.Body.localRotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        private Vector2Int PickNext(Vector2Int at, Vector2Int cameFrom)
        {
            options.Clear();
            for (int i = 0; i < Directions.Length; i++)
            {
                Vector2Int n = at + Directions[i];
                if (n != cameFrom && IsRoad(n)) options.Add(n);
            }
            if (options.Count == 0) return IsRoad(cameFrom) ? cameFrom : at; // dead end: U-turn
            return options[NextInt(options.Count)];
        }

        private bool IsRoad(Vector2Int c)
        {
            return sim.Grid.TypeAt(c.x, c.y) == TileType.Road;
        }

        private void Remove(int index)
        {
            Destroy(cars[index].Body.gameObject);
            cars.RemoveAt(index);
        }

        private int NextInt(int max)
        {
            unchecked { seed = seed * 1103515245 + 12345; }
            return (int)((uint)(seed >> 8) % (uint)max);
        }
    }
}
