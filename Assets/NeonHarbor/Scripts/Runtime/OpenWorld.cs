using System;
using System.Collections.Generic;
using NeonHarbor.Core;
using UnityEngine;

namespace NeonHarbor.Runtime
{
    /// <summary>A fixed service location in the city.</summary>
    public sealed class Place
    {
        public string Name, Letter; public Color Color; public Vector3 Pos; public bool OnRoad;
    }

    /// <summary>
    /// Open-world services and the systems around the wanted level:
    /// who can see the player (police sight lines, bystander witnesses), where police
    /// search when they lost sight, the GPS route, the full-screen map / weapon wheel /
    /// gun shop modes, the spray garage, hospital and police-station respawns, the car
    /// radio and health regeneration. Port of the browser build's 10-gta.js.
    /// </summary>
    public sealed class OpenWorld
    {
        /// <summary>Overlay currently open.</summary>
        public enum Mode { None, Map, Wheel, Shop }

        private readonly HarborConfig cfg;
        private readonly CityLayout city;
        private readonly HarborMaterials mats;
        private readonly WorldDirector world;
        private readonly CombatDirector combat;
        private readonly PlayerController player;
        private readonly WantedSystem wanted;
        private readonly MissionDirector missions;
        private readonly System.Random rnd = new System.Random(77);
        private readonly Dictionary<object, Vector3> searchPoints = new Dictionary<object, Vector3>();
        private float routeTimer, sprayLock, lastHurt, healthPrev = 100;
        private bool shopCooldown, wasInCar;
        private int lastStation = -1;
        private Vector2 wheelDelta;

        public readonly Place Hospital, PoliceStation, GunShop, SprayShop;
        public IEnumerable<Place> Places { get { yield return Hospital; yield return PoliceStation; yield return GunShop; yield return SprayShop; } }
        /// <summary>Overlay mode.</summary>
        public Mode Current { get; private set; }
        /// <summary>Player-set map waypoint.</summary>
        public Vector3? Waypoint;
        /// <summary>GPS polyline (world x/z) to the mission target or waypoint.</summary>
        public readonly List<Vector3> Route = new List<Vector3>();
        /// <summary>True when the route leads to a mission target (yellow) rather than a waypoint (purple).</summary>
        public bool RouteIsMission { get; private set; }
        /// <summary>Weapon highlighted on the wheel.</summary>
        public WeaponKind WheelSelection { get; private set; }
        /// <summary>True while a cop can see the player this frame.</summary>
        public bool Seen { get; private set; }
        /// <summary>Radio station index, -1 = off.</summary>
        public int Station { get; private set; } = -1;
        /// <summary>Station names.</summary>
        public static readonly string[] Stations = { "NEON FM · 신스웨이브", "HARBOR BEATS · 힙합", "LOW TIDE · 앰비언트" };

        /// <summary>Message for the HUD.</summary>
        public event Action<string> Toast;
        /// <summary>Big banner (title, subtitle).</summary>
        public event Action<string, string> Banner;
        /// <summary>Radio station changed (index, -1 = off).</summary>
        public event Action<int> RadioChanged;
        /// <summary>Money spent (sound cue).</summary>
        public event Action Purchased;
        /// <summary>Screen fade request (seconds).</summary>
        public event Action<float> Fade;

        /// <summary>Places the service locations and their markers.</summary>
        public OpenWorld(HarborConfig cfg, CityLayout city, HarborMaterials mats, WorldDirector world, CombatDirector combat, PlayerController player, WantedSystem wanted, MissionDirector missions)
        {
            this.cfg = cfg; this.city = city; this.mats = mats; this.world = world; this.combat = combat; this.player = player; this.wanted = wanted; this.missions = missions;
            Block B(int i, int j) => city.Blocks[j * city.N + i];
            Vector3 Side(Block b, float t) { var q = city.SidewalkPoint(b, t); return new Vector3(q.x, 0.18f, q.z); }
            Hospital = new Place { Name = "병원", Letter = "H", Color = HarborMaterials.Hex(0xff5a6a), Pos = Side(B(3, 4), 0.38f) };
            PoliceStation = new Place { Name = "경찰서", Letter = "P", Color = HarborMaterials.Hex(0x4b8cff), Pos = Side(B(5, 5), 0.31f) };
            GunShop = new Place { Name = "총포사", Letter = "G", Color = HarborMaterials.Hex(0xff8a3c), Pos = Side(B(4, 3), 0.56f) };
            SprayShop = new Place { Name = "스프레이 정비소", Letter = "S", Color = HarborMaterials.Hex(0x3cffc8), Pos = new Vector3(2 * city.Pitch + 30, 0, 2 * city.Pitch - 3), OnRoad = true };
            foreach (var p in Places) missions.Marker(p.Color, p.Pos, p.OnRoad ? 2.2f : 1f, 0.35f);
        }

        // ------------------------------------------------------------------ witnesses & sight
        private bool CopSees()
        {
            Vector3 eye = player.Car != null ? player.Car.Position + Vector3.up : player.Position + Vector3.up * 1.4f;
            foreach (var v in world.Vehicles)
            {
                if (v.Spec.Type != CarType.Police || v.Exploded || v.Driver == null && v.Mode != VehicleMode.Police) continue;
                if (Vector3.Distance(v.Position, eye) < cfg.CopSightCar && city.Raycast(v.Position.x, 1.4f, v.Position.z, eye.x, eye.y, eye.z) >= 0.999f) return true;
            }
            foreach (var o in combat.Officers)
            {
                if (o.Down) continue;
                if (Vector3.Distance(o.Pos, eye) < cfg.CopSightFoot && city.Raycast(o.Pos.x, o.Pos.y + 1.5f, o.Pos.z, eye.x, eye.y, eye.z) >= 0.999f) return true;
            }
            return false;
        }

        private bool Witnessed()
        {
            foreach (var p in world.Peds)
                if ((p.Mode == Pedestrian.State.Walk || p.Mode == Pedestrian.State.Flee) && Vector3.Distance(p.Pos, player.Position) < cfg.WitnessRadius) return true;
            return false;
        }

        /// <summary>A crime: police see it → stars now; bystanders → a phone call in a few seconds; nobody → nothing.</summary>
        public void Crime(float amount, string why, bool againstPolice = false)
        {
            bool sees = againstPolice || CopSees(), witness = !sees && Witnessed();
            int pending = wanted.PendingReports;
            if (wanted.ReportCrime(amount, why, sees, witness) && !sees && pending == 0) Toast?.Invoke("누군가 휴대폰을 꺼냈습니다…");
        }

        /// <summary>Where a police unit should head: the player when seen, else a point inside the search area.</summary>
        public Vector3 PoliceTarget(object unit)
        {
            if (!wanted.Searching) return player.Car != null ? player.Car.Position : player.Position;
            var lk = wanted.LastKnown;
            if (!searchPoints.TryGetValue(unit, out var p) || Vector2.Distance(new Vector2(p.x, p.z), UnitPos(unit)) < 12 || Vector2.Distance(new Vector2(p.x, p.z), new Vector2(lk.X, lk.Y)) > wanted.SearchRadius)
            {
                float a = (float)rnd.NextDouble() * Mathx.Tau, r = (float)rnd.NextDouble() * wanted.SearchRadius * 0.8f;
                p = new Vector3(lk.X + Mathf.Cos(a) * r, 0, lk.Y + Mathf.Sin(a) * r); searchPoints[unit] = p;
            }
            return p;
        }

        private static Vector2 UnitPos(object unit)
        {
            if (unit is Vehicle v) return new Vector2(v.Position.x, v.Position.z);
            if (unit is CombatDirector.Officer o) return new Vector2(o.Pos.x, o.Pos.z);
            return Vector2.zero;
        }

        // ------------------------------------------------------------------ per frame
        /// <summary>Runs before the simulation: overlays, shop, spray, radio, regen. Returns true when the game is paused.</summary>
        public bool TickInput(float unscaledDt, bool playing)
        {
            if (Input.GetKeyDown(KeyCode.M) && Current != Mode.Shop) SetMode(Current == Mode.Map ? Mode.None : Mode.Map);
            if (Current == Mode.Map) { if (Input.GetKeyDown(KeyCode.Escape)) SetMode(Mode.None); return true; }
            if (Current == Mode.Shop)
            {
                for (int k = 0; k < 5; k++) if (Input.GetKeyDown(KeyCode.Alpha1 + k)) Buy((ShopItem)k);
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F) || !Near(GunShop, 4)) SetMode(Mode.None);
                return true;
            }
            // weapon wheel: hold Tab, slow time, pick with the mouse
            if (Input.GetKey(KeyCode.Tab) && player.Car == null && playing)
            {
                if (Current != Mode.Wheel) { SetMode(Mode.Wheel); wheelDelta = Vector2.zero; WheelSelection = player.Arsenal.Current; }
                wheelDelta += new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
                if (wheelDelta.magnitude > 30)
                {
                    float a = Mathf.Atan2(wheelDelta.x, wheelDelta.y);
                    WheelSelection = a < -Mathf.PI / 3 ? WeaponKind.Smg : a > Mathf.PI / 3 ? WeaponKind.Pistol : WeaponKind.Fist;
                }
            }
            else if (Current == Mode.Wheel) { player.Arsenal.Select(WheelSelection); SetMode(Mode.None); }

            if (!playing) return false;
            if (player.Car == null && Near(GunShop, 2.5f) && !shopCooldown) { SetMode(Mode.Shop); shopCooldown = true; }
            if (!Near(GunShop, 4)) shopCooldown = false;
            sprayLock -= unscaledDt;
            if (player.Car != null && Near(SprayShop, 5) && sprayLock <= 0) { sprayLock = 4; Spray(); }
            // radio follows the car
            if (player.Car != null)
            {
                if (!wasInCar) { wasInCar = true; SetStation(lastStation >= 0 ? lastStation : rnd.Next(Stations.Length)); }
                if (Input.GetKeyDown(KeyCode.Period)) SetStation((Station + 2) % (Stations.Length + 1) - 1);
                if (Input.GetKeyDown(KeyCode.Comma)) SetStation((Station + Stations.Length + 1) % (Stations.Length + 1) - 1);
            }
            else if (wasInCar) { wasInCar = false; lastStation = Station; Station = -1; RadioChanged?.Invoke(-1); }
            return false;
        }

        /// <summary>Runs after the simulation: sight, search, GPS, regeneration.</summary>
        public void Tick(float dt)
        {
            Seen = wanted.Stars > 0 && CopSees();
            routeTimer -= Time.unscaledDeltaTime;
            if (routeTimer <= 0) { routeTimer = 0.5f; RebuildRoute(); }
            if (player.Health < healthPrev - 0.01f) lastHurt = 0; else lastHurt += dt;
            player.Health = Shop.Regenerate(cfg, player.Health, lastHurt, dt);
            healthPrev = player.Health;
        }

        private void RebuildRoute()
        {
            Route.Clear();
            Vector3? target = missions.Target ?? Waypoint;
            RouteIsMission = missions.Target.HasValue;
            if (!target.HasValue) return;
            if (!RouteIsMission && Waypoint.HasValue && Vector2.Distance(new Vector2(player.Position.x, player.Position.z), new Vector2(Waypoint.Value.x, Waypoint.Value.z)) < 8)
            { Waypoint = null; Toast?.Invoke("목적지 도착"); return; }
            foreach (var p in city.Route(new Vec2(player.Position.x, player.Position.z), new Vec2(target.Value.x, target.Value.z))) Route.Add(new Vector3(p.X, 0, p.Y));
        }

        private void SetMode(Mode m)
        {
            Current = m;
            Time.timeScale = m == Mode.Wheel ? 0.25f : m == Mode.None ? 1f : 0f;
            if (m == Mode.Map || m == Mode.Shop) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (m == Mode.Map) RebuildRoute();
        }

        /// <summary>Closes any overlay.</summary>
        public void Close() { if (Current != Mode.None) SetMode(Mode.None); }

        /// <summary>Opens / closes the full map (HUD button).</summary>
        public void ToggleMap() { SetMode(Current == Mode.Map ? Mode.None : Mode.Map); }

        /// <summary>Sets or clears the waypoint from a map click (world x/z).</summary>
        public void ClickMap(float x, float z)
        {
            if (Waypoint.HasValue && Vector2.Distance(new Vector2(Waypoint.Value.x, Waypoint.Value.z), new Vector2(x, z)) < 25) { Waypoint = null; Toast?.Invoke("웨이포인트 해제"); }
            else { Waypoint = new Vector3(Mathf.Clamp(x, 0, city.Size), 0, Mathf.Clamp(z, 0, city.Size)); Toast?.Invoke("웨이포인트 설정 — 미니맵의 보라색 경로를 따라가세요"); }
            RebuildRoute();
        }

        private bool Near(Place p, float r) { return Vector2.Distance(new Vector2(player.Position.x, player.Position.z), new Vector2(p.Pos.x, p.Pos.z)) < r; }

        // ------------------------------------------------------------------ shops
        /// <summary>Buys a gun-shop item (HUD button or number key).</summary>
        public void Buy(ShopItem item)
        {
            int money = player.Money; float armor = player.Armor;
            var r = Shop.Buy(cfg, item, ref money, player.Arsenal, ref armor);
            player.Money = money; player.Armor = armor;
            switch (r)
            {
                case PurchaseResult.Bought: Purchased?.Invoke(); Toast?.Invoke("구매: " + Shop.Name(item)); break;
                case PurchaseResult.NotEnoughMoney: Toast?.Invoke("돈이 부족합니다"); break;
                case PurchaseResult.AlreadyOwned: Toast?.Invoke("이미 가지고 있습니다"); break;
                case PurchaseResult.NeedsWeapon: Toast?.Invoke("해당 무기가 필요합니다"); break;
                default: Toast?.Invoke("방탄복이 이미 가득합니다"); break;
            }
        }

        private void Spray()
        {
            int money = player.Money;
            var r = Shop.Spray(cfg, player.Car != null, Seen, ref money);
            if (r == Shop.SprayResult.PoliceWatching) { Toast?.Invoke("경찰이 보고 있어 정비소에 들어갈 수 없습니다"); return; }
            if (r == Shop.SprayResult.NotEnoughMoney) { Toast?.Invoke($"${cfg.SprayCost}가 필요합니다"); return; }
            if (r != Shop.SprayResult.Resprayed) return;
            player.Money = money;
            var car = player.Car; var spec = car.Spec;
            var paint = mats.Paint(HarborMaterials.Hex(spec.Paints.Length > 1 ? spec.Paints[rnd.Next(spec.Paints.Length)] : new[] { 0x1d3b6e, 0xb8b8bc, 0x8a1d1d, 0xf2c31b, 0x1f8fd6 }[rnd.Next(5)]));
            var white = mats.Paint(new Color(0.95f, 0.95f, 0.95f));
            foreach (var mr in car.Parts.Body.GetComponentsInChildren<MeshRenderer>())
                if (mr.sharedMaterial != null && mr.sharedMaterial.name == "Paint" && mr.sharedMaterial != white && mr.gameObject.name.StartsWith("CarBody")) mr.sharedMaterial = paint;
            car.Health = 100; car.Burning = 0;
            bool had = wanted.Stars > 0; wanted.Clear();
            Fade?.Invoke(1.2f); Purchased?.Invoke();
            Banner?.Invoke("새 차 같네요", had ? "도색 완료 · 수배 해제" : $"도색·수리 완료 -${cfg.SprayCost}");
        }

        /// <summary>Respawn point after an arrest (police station) or knockout (hospital).</summary>
        public Vector3 RespawnPoint(bool arrested) { return arrested ? PoliceStation.Pos : Hospital.Pos; }

        private void SetStation(int s)
        {
            Station = s; RadioChanged?.Invoke(s);
            Toast?.Invoke(s >= 0 ? "라디오: " + Stations[s] : "라디오 끔");
        }
    }
}
