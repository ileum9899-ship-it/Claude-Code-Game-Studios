using System;
using NeonHarbor.Core;
using UnityEngine;

namespace NeonHarbor.Runtime
{
    /// <summary>
    /// Composition root. Loads harbor_balance.json, generates the city, and wires
    /// player, world AI, wanted level, missions, camera, day/night, post effects,
    /// audio and HUD. Created by <see cref="GameBootstrap"/> in any scene.
    /// Command line: -harborShot &lt;png&gt; (capture after 120 frames and quit),
    /// -harborNight, -harborDrive (start in a car), -harborChase (3 stars).
    /// </summary>
    public sealed class HarborGame : MonoBehaviour
    {
        public HarborConfig Config { get; private set; }
        public CityLayout City { get; private set; }
        public HarborMaterials Materials { get; private set; }
        public WorldDirector World { get; private set; }
        public PlayerController Player { get; private set; }
        public WantedSystem Wanted { get; private set; }
        public MissionDirector Missions { get; private set; }
        public DayNightCycle Day { get; private set; }
        public CameraRig Camera { get; private set; }
        public CombatDirector Combat { get; private set; }
        public OpenWorld Services { get; private set; }

        private HarborHud hud;
        private HarborAudio sound;
        private PostEffects post;
        private Vector3 spawn;
        private float time, deadTimer = -1, fade;
        private bool arrested;
        private string shotPath; private int shotFrame = -1;

        private void Awake()
        {
            Config = LoadConfig();
            City = new CityLayout(Config);
            Materials = new HarborMaterials();
            var worldRoot = new GameObject("City").transform; worldRoot.SetParent(transform, false);
            var builder = new CityBuilder(City, Materials, worldRoot); builder.Build();
            var dynamicRoot = new GameObject("Dynamic").transform; dynamicRoot.SetParent(transform, false);
            World = new WorldDirector(Config, City, Materials, dynamicRoot);
            spawn = new Vector3(3.5f * City.Pitch + 30, 0.18f, 4.5f * City.Pitch - 2);
            Player = new PlayerController(Config, City, Materials, World, dynamicRoot, spawn) { Yaw = Mathf.PI };
            World.Player = Player;
            World.Populate(spawn, new Vector3(spawn.x - 25, 0, 4 * City.Pitch + 5f), Mathf.PI / 2);
            Wanted = new WantedSystem(Config);
            Missions = new MissionDirector(Config, City, Materials, dynamicRoot);
            Day = new DayNightCycle(Materials, builder.NeonSigns, Config.DayLength);
            new TrafficSignalRenderer(World.Lights, City, Materials, worldRoot);

            var cam = UnityEngine.Camera.main;
            if (cam == null) { var go = new GameObject("Main Camera") { tag = "MainCamera" }; cam = go.AddComponent<UnityEngine.Camera>(); go.AddComponent<AudioListener>(); }
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 2500f; cam.clearFlags = CameraClearFlags.Skybox;
            post = cam.gameObject.AddComponent<PostEffects>();
            Camera = new CameraRig(cam, City); Camera.Snap(spawn);
            Combat = new CombatDirector(Config, City, Materials, World, Player, Camera, cam, dynamicRoot, spawn);
            Services = new OpenWorld(Config, City, Materials, World, Combat, Player, Wanted, Missions);
            World.PoliceTarget = v => Services.PoliceTarget(v);
            Combat.OfficerTarget = o => Services.PoliceTarget(o);
            sound = gameObject.AddComponent<HarborAudio>();
            hud = gameObject.AddComponent<HarborHud>(); hud.Init(this);
            Wire();
            ReadCommandLine();
        }

        private void Wire()
        {
            Color warn = new Color(1f, 0.8f, 0.2f), good = new Color(0.5f, 0.9f, 0.56f), bad = new Color(1f, 0.3f, 0.37f);
            World.PedHitByPlayer += () => Services.Crime(Config.HeatPedHit, "보행자 사고");
            World.CopRammed += _ => Services.Crime(Config.HeatCopHit, "경찰차 충돌", true);
            World.Crash += (v, player) => { if (player) { Camera.Shake = Mathf.Max(Camera.Shake, v); sound.Crash(v); } };
            World.Honk += _ => sound.Horn();
            Player.Carjacked += police => Services.Crime(police ? 2 : Config.HeatCarjack, police ? "경찰차 탈취" : "차량 탈취", police);
            Player.CarImpact += v => { Camera.Shake = Mathf.Max(Camera.Shake, v); sound.Crash(v); };
            Player.HornPressed += () => sound.Horn();
            Player.KnockedDown += () => { Camera.Shake = 0.6f; sound.Crash(0.6f); };
            Player.Toast += t => hud.Toast(t);
            Wanted.StarsRaised += (s, why) => { sound.Star(); hud.Toast($"수배 레벨 {new string('★', s)} · {why}", warn); };
            Wanted.Evaded += () => { sound.Win(); hud.Toast("경찰을 따돌렸습니다", good); Missions.NotifyEvaded(); };
            Wanted.Busted += () => { arrested = true; Die("체포됨", $"벌금 -${Mathf.RoundToInt(Player.Money * Config.BustFine):N0} · 경찰서에서 풀려났습니다", Config.BustFine, 0); };
            Wanted.WitnessReported += why => hud.Toast("목격자가 신고했습니다 · " + why, warn);
            Missions.Banner += (t, s, ok) => { hud.Banner(t, s, ok ? good : bad); if (ok) sound.Win(); else sound.Fail(); };
            Missions.Toast += t => hud.Toast(t);
            Missions.Paid += v => { Player.Money += v; sound.Cash(); };
            Missions.Checkpoint += () => sound.Cash();
            Combat.Heat += (amount, why, police) => Services.Crime(amount, why, police);
            Services.Toast += t => hud.Toast(t);
            Services.Banner += (t, s) => hud.Banner(t, s, good);
            Services.Purchased += () => sound.Cash();
            Services.RadioChanged += s => sound.SetStation(s);
            Services.Fade += t => fade = t;
            Combat.Toast += t => hud.Toast(t, good);
            Combat.PlayerHurt += k => hud.Hurt(k);
            Combat.Shot += smg => sound.Shot(smg);
            Combat.Explosion += () => sound.Blast();
            Combat.Click += done => sound.Click(done);
            Combat.Swing += () => sound.Swing();
            Combat.PickedUp += () => sound.Cash();
        }

        private static HarborConfig LoadConfig()
        {
            var asset = Resources.Load<TextAsset>("Data/harbor_balance");
            if (asset == null) { Debug.LogWarning("[NeonHarbor] Resources/Data/harbor_balance.json missing; using defaults."); return new HarborConfig(); }
            try { return JsonUtility.FromJson<HarborConfigDto>(asset.text).ToConfig(); }
            catch (Exception e) { Debug.LogError("[NeonHarbor] harbor_balance.json invalid: " + e.Message); return new HarborConfig(); }
        }

        /// <summary>Leaves the intro screen.</summary>
        public void StartPlaying() { hud.ShowIntro = false; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }

        private void Die(string title, string sub, float fineFraction, int flatFine)
        {
            if (deadTimer >= 0) return;
            int fine = Mathf.RoundToInt(Player.Money * fineFraction) + flatFine; Player.Money = Mathf.Max(0, Player.Money - fine);
            hud.Banner(title, sub, new Color(1f, 0.3f, 0.37f)); sound.Fail(); Missions.Cancel();
            deadTimer = 3.2f;
        }

        private void Update()
        {
            bool paused = Services.TickInput(Time.unscaledDeltaTime, deadTimer < 0 && !hud.ShowIntro);
            if (fade > 0) { fade -= Time.unscaledDeltaTime; post.Flash = new Color(0, 0, 0, Mathf.Clamp01(fade)); if (fade <= 0) post.Flash = Color.clear; }
            if (paused) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f); time += dt;
            if (Input.GetKeyDown(KeyCode.Escape) && Services.Current == OpenWorld.Mode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (Input.GetMouseButtonDown(0) && !hud.ShowIntro && Services.Current == OpenWorld.Mode.None) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (Input.GetKeyDown(KeyCode.T)) Day.SetTime(Day.T + 0.08f);

            if (deadTimer >= 0)
            {
                deadTimer -= dt; post.Flash = new Color(0.25f, 0.02f, 0.02f, Mathf.Clamp01(deadTimer / 3f) * 0.6f);
                if (deadTimer < 0) { var rp = Services.RespawnPoint(arrested); Player.Respawn(rp); Wanted.Clear(); foreach (var v in World.Vehicles.ToArray()) if (v.Spec.Type == CarType.Police) World.Remove(v); Camera.Snap(rp); Combat.Reset(); post.Flash = Color.clear; }
            }
            else if (!hud.ShowIntro) Player.Tick(dt, Camera.Yaw);

            Combat.Tick(dt, time, Wanted.Stars, deadTimer < 0 && !hud.ShowIntro);
            Camera.Aim = Combat.Aiming && Player.Car == null;
            float nearestCop = Mathf.Min(World.Tick(dt, time, Wanted.Stars, Wanted.DesiredCops), Combat.NearestOfficer());
            Services.Tick(dt);
            Wanted.Tick(dt, Services.Seen, new Vec2(Player.Position.x, Player.Position.z), Player.Car != null ? Player.Car.Speed : new Vector2(Player.Velocity.x, Player.Velocity.z).magnitude, nearestCop);
            if (Player.Health <= 0 && deadTimer < 0) { arrested = false; Die("의식을 잃었습니다", $"병원비 -${Config.HospitalBill} · 병원에서 깨어났습니다", 0, Config.HospitalBill); }
            Missions.Tick(dt, time, Player, Wanted.Stars);
            Day.Tick(dt, Player.Position);
            Materials.Water.SetTextureOffset("_BumpMap", new Vector2(time * 0.021f, time * 0.013f));
            post.Exposure = Mathf.Lerp(1.05f, 1.35f, Day.Night); post.Intensity = Mathf.Lerp(0.35f, 0.9f, Day.Night); post.Threshold = Mathf.Lerp(1.1f, 0.85f, Day.Night);
            sound.Tick(Player.Car, Wanted.Stars > 0 ? Mathf.Clamp01(1 - nearestCop / 140f) : 0);

            if (shotFrame >= 0 && Time.frameCount >= shotFrame) { ScreenCapture.CaptureScreenshot(shotPath); shotFrame = -1; Invoke(nameof(Quit), 1.5f); }
        }

        private void LateUpdate()
        {
            if (Services.Current == OpenWorld.Mode.Map || Services.Current == OpenWorld.Mode.Shop) return;
            Camera.Tick(Mathf.Min(Time.unscaledDeltaTime, 0.05f), Player, !hud.ShowIntro && Services.Current != OpenWorld.Mode.Wheel && (Cursor.lockState == CursorLockMode.Locked || Input.GetMouseButton(1)));
        }

        private void ReadCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-harborNight") Day.SetTime(0.93f);
                if (args[i] == "-harborDrive") { hud.ShowIntro = false; Vehicle best = null; foreach (var v in World.Vehicles) if (v.Spec.Type == CarType.Sports) { best = v; break; } if (best != null) Player.Enter(best); }
                if (args[i] == "-harborChase") { hud.ShowIntro = false; Wanted.AddHeat(3, "test"); }
                if (args[i] == "-harborShot" && i + 1 < args.Length) { shotPath = args[i + 1]; shotFrame = Time.frameCount + 120; }
            }
        }

        private void Quit() { Application.Quit(); }
    }
}
