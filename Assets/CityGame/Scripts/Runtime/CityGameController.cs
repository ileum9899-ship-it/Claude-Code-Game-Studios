using System;
using System.Collections.Generic;
using System.IO;
using CityGame.Core;
using UnityEngine;

namespace CityGame.Runtime
{
    /// <summary>
    /// Composition root: loads the balance file, owns the simulation, builds the
    /// scene (camera, sun, renderer, traffic, HUD) and turns mouse/keyboard input
    /// into build commands. Created automatically by <see cref="GameBootstrap"/>.
    /// </summary>
    public sealed class CityGameController : MonoBehaviour
    {
        /// <summary>Selectable clock speeds (game days per <see cref="CityConfig.SecondsPerDay"/>).</summary>
        public static readonly int[] Speeds = { 0, 1, 2, 4 };

        private const string ConfigResource = "Data/city_balance";
        private const string SaveFile = "city_save.json";
        private const float NotificationSeconds = 4f;

        private readonly List<Vector2Int> dragCells = new List<Vector2Int>(256);
        private CityRenderer cityRenderer;
        private CameraRig rig;
        private Camera cam;
        private TrafficSystem traffic;
        private DayNightCycle dayNight;
        private CityHud hud;
        private Transform preview;
        private MeshRenderer previewRenderer;
        private CityMaterials materials;
        private float dayAccumulator;
        private bool dragging;
        private Vector2Int dragStart, lastDragCell;
        private string screenshotPath;
        private int screenshotFrame = -1;

        /// <summary>The running simulation.</summary>
        public CitySimulation Sim { get; private set; }
        /// <summary>Selected build tool.</summary>
        public ToolType Tool { get; set; } = ToolType.Road;
        /// <summary>Index into <see cref="Speeds"/>.</summary>
        public int SpeedIndex { get; set; } = 1;
        /// <summary>Cell under the mouse, if any.</summary>
        public Vector2Int? HoverCell { get; private set; }
        /// <summary>Recent messages for the HUD (text, time shown).</summary>
        public List<KeyValuePair<string, float>> Notifications { get; } = new List<KeyValuePair<string, float>>();
        /// <summary>Total cost of the drag in progress.</summary>
        public int PendingCost { get; private set; }

        private void Awake()
        {
            CityConfig config = LoadConfig();
            Sim = new CitySimulation(config, new SystemRandomSource(Environment.TickCount));
            materials = new CityMaterials();

            var world = new GameObject("World").transform;
            world.SetParent(transform, false);
            cityRenderer = new CityRenderer(Sim, materials, world);
            Sim.TileChanged += cityRenderer.Rebuild;
            Sim.MilestoneReached += m => Notify(string.Format(CityStrings.Milestone, CityStrings.MilestoneName(m)));
            Sim.MonthEnded += () => Notify(string.Format(CityStrings.MonthReport, Sim.LastMonthIncome, Sim.LastMonthExpenses));

            SetupCamera(config.GridSize);
            Light sun = SetupSun();

            dayNight = gameObject.AddComponent<DayNightCycle>();
            dayNight.Init(sun, cam, materials);
            traffic = new GameObject("Traffic").AddComponent<TrafficSystem>();
            traffic.transform.SetParent(transform, false);
            traffic.Init(Sim, materials);
            hud = gameObject.AddComponent<CityHud>();
            hud.Init(this, dayNight);

            preview = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            Destroy(preview.GetComponent<Collider>());
            preview.name = "Cursor";
            preview.SetParent(transform, false);
            previewRenderer = preview.GetComponent<MeshRenderer>();

            ReadCommandLine();
        }

        private void SetupCamera(int gridSize)
        {
            cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 400f;
            cam.fieldOfView = 45f;
            if (!cam.gameObject.TryGetComponent(out rig)) rig = cam.gameObject.AddComponent<CameraRig>();
            rig.SetBounds(gridSize);
            rig.Focus = new Vector3(gridSize / 2f, 0f, gridSize / 2f);
            rig.Distance = gridSize * 0.6f;
            rig.Apply();
        }

        private static Light SetupSun()
        {
            Light sun = null;
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.7f;
            QualitySettings.shadowDistance = 120f;
            return sun;
        }

        private static CityConfig LoadConfig()
        {
            var asset = Resources.Load<TextAsset>(ConfigResource);
            if (asset == null)
            {
                Debug.LogWarning($"[CityGame] Resources/{ConfigResource}.json not found; using built-in defaults.");
                return new CityConfig();
            }
            try
            {
                return JsonUtility.FromJson<CityConfigDto>(asset.text).ToConfig();
            }
            catch (Exception e)
            {
                Debug.LogError($"[CityGame] city_balance.json is invalid ({e.Message}); using defaults.");
                return new CityConfig();
            }
        }

        private void Update()
        {
            int speed = Speeds[SpeedIndex];
            traffic.TimeScale = speed;
            dayNight.TimeScale = speed;

            HandleKeys();
            bool overUi = hud.IsPointerOverUi(Input.mousePosition);
            rig.Tick(Time.unscaledDeltaTime, !overUi);
            HandleMouse(overUi);

            if (speed > 0)
            {
                dayAccumulator += Time.deltaTime * speed;
                float perDay = Mathf.Max(0.01f, Sim.Config.SecondsPerDay);
                int guard = 0;
                while (dayAccumulator >= perDay && guard++ < 16)
                {
                    dayAccumulator -= perDay;
                    Sim.AdvanceDay();
                }
            }

            float now = Time.unscaledTime;
            Notifications.RemoveAll(n => now - n.Value > NotificationSeconds);

            if (screenshotFrame >= 0 && Time.frameCount >= screenshotFrame)
            {
                ScreenCapture.CaptureScreenshot(screenshotPath);
                screenshotFrame = -1;
                Invoke(nameof(Quit), 1f);
            }
        }

        private void HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) Tool = ToolType.Road;
            if (Input.GetKeyDown(KeyCode.Alpha2)) Tool = ToolType.Residential;
            if (Input.GetKeyDown(KeyCode.Alpha3)) Tool = ToolType.Commercial;
            if (Input.GetKeyDown(KeyCode.Alpha4)) Tool = ToolType.Industrial;
            if (Input.GetKeyDown(KeyCode.Alpha5)) Tool = ToolType.Park;
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.X)) Tool = ToolType.Bulldoze;
            if (Input.GetKeyDown(KeyCode.Escape)) { Tool = ToolType.None; dragging = false; }
            if (Input.GetKeyDown(KeyCode.Space)) SpeedIndex = SpeedIndex == 0 ? 1 : 0;
            if (Input.GetKeyDown(KeyCode.RightBracket)) SpeedIndex = Mathf.Min(Speeds.Length - 1, SpeedIndex + 1);
            if (Input.GetKeyDown(KeyCode.LeftBracket)) SpeedIndex = Mathf.Max(0, SpeedIndex - 1);
            if (Input.GetKeyDown(KeyCode.F5)) Save();
            if (Input.GetKeyDown(KeyCode.F9)) Load();
            if (Input.GetKeyDown(KeyCode.H)) hud.ShowHelp = !hud.ShowHelp;
        }

        private void HandleMouse(bool overUi)
        {
            HoverCell = overUi && !dragging ? (Vector2Int?)null : CellUnderMouse();
            PendingCost = 0;

            if (Tool == ToolType.None || HoverCell == null)
            {
                preview.gameObject.SetActive(false);
                if (Input.GetMouseButtonUp(0)) dragging = false;
                return;
            }

            Vector2Int cell = HoverCell.Value;
            if (Input.GetMouseButtonDown(0) && !overUi)
            {
                dragging = true;
                dragStart = lastDragCell = cell;
                if (Tool == ToolType.Road) ApplyCell(cell);
            }

            if (dragging && Input.GetMouseButton(0) && Tool == ToolType.Road && cell != lastDragCell)
            {
                // Paint every cell between the last and current position (axis-stepped, no gaps).
                Vector2Int c = lastDragCell;
                while (c != cell)
                {
                    int dx = cell.x - c.x, dy = cell.y - c.y;
                    if (Mathf.Abs(dx) >= Mathf.Abs(dy)) c.x += Math.Sign(dx); else c.y += Math.Sign(dy);
                    ApplyCell(c);
                }
                lastDragCell = cell;
            }

            bool rectTool = Tool != ToolType.Road;
            Vector2Int min = cell, max = cell;
            if (dragging && rectTool)
            {
                min = Vector2Int.Min(dragStart, cell);
                max = Vector2Int.Max(dragStart, cell);
                CollectRect(min, max);
                foreach (Vector2Int c in dragCells) PendingCost += Sim.CostAt(Tool, c.x, c.y);
            }
            else
            {
                PendingCost = Sim.CostAt(Tool, cell.x, cell.y);
            }

            if (dragging && Input.GetMouseButtonUp(0))
            {
                if (rectTool)
                {
                    CollectRect(min, max);
                    bool ranOut = false;
                    foreach (Vector2Int c in dragCells)
                        if (Sim.TryPlace(Tool, c.x, c.y) == PlaceResult.InsufficientFunds) ranOut = true;
                    if (ranOut) Notify(CityStrings.NoFunds);
                }
                dragging = false;
            }

            UpdatePreview(min, max);
        }

        private void ApplyCell(Vector2Int c)
        {
            if (Sim.TryPlace(Tool, c.x, c.y) == PlaceResult.InsufficientFunds) Notify(CityStrings.NoFunds);
        }

        private void CollectRect(Vector2Int min, Vector2Int max)
        {
            dragCells.Clear();
            for (int y = min.y; y <= max.y; y++)
                for (int x = min.x; x <= max.x; x++)
                    dragCells.Add(new Vector2Int(x, y));
        }

        private void UpdatePreview(Vector2Int min, Vector2Int max)
        {
            preview.gameObject.SetActive(true);
            Vector2 size = max - min + Vector2Int.one;
            preview.localPosition = new Vector3(min.x + size.x / 2f, 0.06f, min.y + size.y / 2f);
            preview.localScale = new Vector3(size.x, 0.08f, size.y);
            bool ok = Sim.Validate(Tool, HoverCell.Value.x, HoverCell.Value.y) == PlaceResult.Placed || (dragging && PendingCost > 0);
            Color c = Tool == ToolType.Bulldoze ? new Color(0.95f, 0.3f, 0.2f) : (ok ? new Color(0.3f, 0.95f, 0.5f) : new Color(0.9f, 0.25f, 0.25f));
            previewRenderer.sharedMaterial = materials.Get(c);
        }

        private Vector2Int? CellUnderMouse()
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Mathf.Abs(ray.direction.y) < 0.0001f) return null;
            float t = -ray.origin.y / ray.direction.y;
            if (t < 0f) return null;
            Vector3 p = ray.origin + ray.direction * t;
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.z);
            return Sim.Grid.InBounds(x, y) ? new Vector2Int(x, y) : (Vector2Int?)null;
        }

        /// <summary>Shows a transient HUD message.</summary>
        public void Notify(string message)
        {
            Notifications.Add(new KeyValuePair<string, float>(message, Time.unscaledTime));
            if (Notifications.Count > 4) Notifications.RemoveAt(0);
        }

        /// <summary>Writes the city to persistentDataPath.</summary>
        public void Save()
        {
            string path = Path.Combine(Application.persistentDataPath, SaveFile);
            File.WriteAllText(path, JsonUtility.ToJson(Sim.CreateSaveData()));
            Notify(CityStrings.Saved);
        }

        /// <summary>Loads the city saved by <see cref="Save"/>.</summary>
        public void Load()
        {
            string path = Path.Combine(Application.persistentDataPath, SaveFile);
            if (!File.Exists(path)) { Notify(CityStrings.NoSave); return; }
            bool ok = Sim.LoadSaveData(JsonUtility.FromJson<CitySaveData>(File.ReadAllText(path)));
            Notify(ok ? CityStrings.Loaded : CityStrings.NoSave);
        }

        /// <summary>Lays out a sample town and fast-forwards it, for a quick look or a screenshot.</summary>
        public void BuildDemoCity(int days)
        {
            Sim.AddFunds(100000);
            int s = Sim.Grid.Size;
            int lo = s / 2 - 12, hi = s / 2 + 12;
            for (int i = lo; i <= hi; i++)
                for (int k = lo; k <= hi; k += 6)
                {
                    Sim.TryPlace(ToolType.Road, i, k);
                    Sim.TryPlace(ToolType.Road, k, i);
                }
            for (int y = lo; y <= hi; y++)
                for (int x = lo; x <= hi; x++)
                {
                    if (Sim.Grid.TypeAt(x, y) != TileType.Empty) continue;
                    int bx = (x - lo) / 6, by = (y - lo) / 6;
                    ToolType z;
                    if (!Sim.Grid.IsAdjacentToRoad(x, y)) z = ToolType.Park;
                    else if ((bx + by) % 4 == 0) z = ToolType.Commercial;
                    else if (bx == 3 && by == 0 || bx == 0 && by == 3) z = ToolType.Industrial;
                    else z = ToolType.Residential;
                    Sim.TryPlace(z, x, y);
                }
            for (int i = 0; i < days; i++) Sim.AdvanceDay();
        }

        private void ReadCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-cityDemo") BuildDemoCity(240);
                if (args[i] == "-cityNight") dayNight.SetTime(0.9f);
                if (args[i] == "-cityShot" && i + 1 < args.Length)
                {
                    screenshotPath = args[i + 1];
                    screenshotFrame = Time.frameCount + 90;
                }
            }
        }

        private void Quit()
        {
            Application.Quit();
        }
    }
}
