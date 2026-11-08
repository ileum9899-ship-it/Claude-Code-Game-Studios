using System.Collections.Generic;
using CityGame.Core;
using UnityEngine;

namespace CityGame.Runtime
{
    /// <summary>
    /// Immediate-mode HUD (IMGUI) — needs no Canvas, prefab or font asset.
    /// Laid out on a 1280×720 reference canvas scaled to the screen height.
    /// </summary>
    public sealed class CityHud : MonoBehaviour
    {
        private const float RefHeight = 720f;
        private static readonly ToolType[] Tools =
            { ToolType.Road, ToolType.Residential, ToolType.Commercial, ToolType.Industrial, ToolType.Park, ToolType.Bulldoze };
        private static readonly Color[] ToolColors =
        {
            new Color(0.45f, 0.45f, 0.5f), new Color(0.35f, 0.75f, 0.35f), new Color(0.3f, 0.55f, 0.9f),
            new Color(0.9f, 0.7f, 0.25f), new Color(0.25f, 0.65f, 0.3f), new Color(0.85f, 0.3f, 0.25f),
        };

        private readonly List<Rect> uiRects = new List<Rect>(16);
        private CityGameController game;
        private DayNightCycle dayNight;
        private GUIStyle panel, label, big, small, button, buttonOn, center;
        private Texture2D white;
        private float scale = 1f;

        /// <summary>Whether the controls panel is open.</summary>
        public bool ShowHelp { get; set; } = true;

        /// <summary>Wires dependencies.</summary>
        public void Init(CityGameController controller, DayNightCycle cycle)
        {
            game = controller;
            dayNight = cycle;
        }

        /// <summary>True when a screen-space point (origin bottom-left) is over a HUD panel.</summary>
        public bool IsPointerOverUi(Vector3 screenPos)
        {
            var p = new Vector2(screenPos.x, Screen.height - screenPos.y) / scale;
            for (int i = 0; i < uiRects.Count; i++) if (uiRects[i].Contains(p)) return true;
            return false;
        }

        private void EnsureStyles()
        {
            if (panel != null) return;
            white = Texture2D.whiteTexture;
            panel = new GUIStyle(GUI.skin.box) { normal = { background = MakeTex(new Color(0.08f, 0.1f, 0.14f, 0.82f)) }, padding = new RectOffset(10, 10, 6, 6) };
            label = new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = Color.white }, alignment = TextAnchor.MiddleLeft };
            big = new GUIStyle(label) { fontSize = 19, fontStyle = FontStyle.Bold };
            small = new GUIStyle(label) { fontSize = 12, normal = { textColor = new Color(0.8f, 0.85f, 0.9f) } };
            center = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            button = new GUIStyle(GUI.skin.button) { fontSize = 14, normal = { textColor = Color.white } };
            buttonOn = new GUIStyle(button) { normal = { background = MakeTex(new Color(0.95f, 0.75f, 0.2f)), textColor = Color.black }, fontStyle = FontStyle.Bold };
        }

        private static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private void OnGUI()
        {
            if (game == null || game.Sim == null) return;
            EnsureStyles();
            scale = Screen.height / RefHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale;
            uiRects.Clear();

            CitySimulation sim = game.Sim;
            DrawTopBar(sim, w);
            DrawDemand(sim, w);
            DrawToolbar(sim, w);
            DrawHover(sim);
            DrawNotifications(w);
            if (ShowHelp) DrawHelp(w);
        }

        private Rect Panel(Rect r)
        {
            GUI.Box(r, GUIContent.none, panel);
            uiRects.Add(r);
            return r;
        }

        private void DrawTopBar(CitySimulation sim, float w)
        {
            Panel(new Rect(0, 0, w, 46));
            float x = 12;
            GUI.Label(new Rect(x, 4, 220, 38), $"{CityStrings.GameTitle} · {CityStrings.MilestoneName(sim.MilestoneIndex)}", big); x += 230;
            Color moneyColor = sim.Money < 0 ? new Color(1f, 0.4f, 0.4f) : new Color(0.6f, 1f, 0.6f);
            Stat(ref x, CityStrings.Money, $"₩{sim.Money:N0}", moneyColor, 170);
            Stat(ref x, CityStrings.Population, sim.Population.ToString("N0"), Color.white, 110);
            Stat(ref x, CityStrings.Jobs, sim.Jobs.ToString("N0"), Color.white, 110);
            Color hc = Color.Lerp(new Color(1f, 0.4f, 0.3f), new Color(0.5f, 1f, 0.5f), sim.Happiness / 100f);
            Stat(ref x, CityStrings.Happiness, sim.Happiness + "%", hc, 100);
            string clock = dayNight.IsNight ? "(밤)" : "(낮)";
            GUI.Label(new Rect(x, 4, 170, 38), string.Format(CityStrings.Date, sim.Year, sim.Month, sim.Day) + " " + clock, label);

            float bx = w - 4 * 46 - 10;
            string[] names = { "II", "▶", "▶▶", "▶▶▶" };
            for (int i = 0; i < CityGameController.Speeds.Length; i++)
            {
                if (GUI.Button(new Rect(bx + i * 46, 7, 42, 32), names[i], game.SpeedIndex == i ? buttonOn : button)) game.SpeedIndex = i;
            }
        }

        private void Stat(ref float x, string name, string value, Color color, float width)
        {
            GUI.Label(new Rect(x, 2, width, 18), name, small);
            Color old = GUI.color;
            GUI.color = color;
            GUI.Label(new Rect(x, 18, width, 24), value, big);
            GUI.color = old;
            x += width;
        }

        private void DrawDemand(CitySimulation sim, float w)
        {
            Rect r = Panel(new Rect(w - 170, 56, 160, 232));
            GUI.Label(new Rect(r.x + 10, r.y + 4, 140, 20), CityStrings.Demand, label);
            Bar(r.x + 20, r.y + 30, sim.ResidentialDemand, ToolColors[1], "R");
            Bar(r.x + 65, r.y + 30, sim.CommercialDemand, ToolColors[2], "C");
            Bar(r.x + 110, r.y + 30, sim.IndustrialDemand, ToolColors[3], "I");

            float ty = r.y + 150;
            GUI.Label(new Rect(r.x + 10, ty, 70, 26), $"{CityStrings.Tax} {sim.TaxRate}%", label);
            if (GUI.Button(new Rect(r.x + 90, ty, 28, 26), "-", button)) sim.SetTaxRate(sim.TaxRate - 1);
            if (GUI.Button(new Rect(r.x + 122, ty, 28, 26), "+", button)) sim.SetTaxRate(sim.TaxRate + 1);
            GUI.Label(new Rect(r.x + 10, ty + 30, 145, 20), $"수입 +{sim.LastMonthIncome:N0}", small);
            GUI.Label(new Rect(r.x + 10, ty + 48, 145, 20), $"지출 -{sim.LastMonthExpenses:N0}", small);
        }

        private void Bar(float x, float y, float demand, Color color, string tag)
        {
            const float h = 100f;
            GUI.color = new Color(1f, 1f, 1f, 0.15f);
            GUI.DrawTexture(new Rect(x, y, 28, h), white);
            GUI.color = color;
            float mid = y + h / 2f;
            float len = Mathf.Abs(demand) * h / 2f;
            if (demand >= 0) GUI.DrawTexture(new Rect(x, mid - len, 28, len), white);
            else GUI.DrawTexture(new Rect(x, mid, 28, len), white);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x - 2, mid, 32, 1), white);
            GUI.Label(new Rect(x, y + h + 2, 28, 18), tag, center);
        }

        private void DrawToolbar(CitySimulation sim, float w)
        {
            const float bw = 92, bh = 58, gap = 6;
            float total = Tools.Length * (bw + gap) - gap;
            Rect r = Panel(new Rect((w - total) / 2f - 10, RefHeight - bh - 22, total + 20, bh + 16));
            for (int i = 0; i < Tools.Length; i++)
            {
                ToolType t = Tools[i];
                var br = new Rect(r.x + 10 + i * (bw + gap), r.y + 8, bw, bh);
                GUI.color = game.Tool == t ? Color.white : ToolColors[i];
                if (GUI.Button(br, $"{i + 1}  {CityStrings.ToolName(t)}\n₩{sim.Config.CostOf(t)}", game.Tool == t ? buttonOn : button))
                    game.Tool = game.Tool == t ? ToolType.None : t;
                GUI.color = Color.white;
            }
            if (game.PendingCost > 0)
                GUI.Label(new Rect(r.x, r.y - 26, r.width, 24), $"비용 ₩{game.PendingCost:N0}", center);
        }

        private void DrawHover(CitySimulation sim)
        {
            if (game.HoverCell == null) return;
            Vector2Int c = game.HoverCell.Value;
            Tile t = sim.Grid.Get(c.x, c.y);
            Rect r = Panel(new Rect(10, RefHeight - 130, 230, 120));
            GUI.Label(new Rect(r.x + 10, r.y + 6, 210, 22), $"{CityStrings.TileName(t.Type)}  ({c.x}, {c.y})", label);
            if (TileTypes.IsZone(t.Type))
            {
                int cap = sim.Config.CapacityOf(t.Type, t.Level);
                string unit = t.Type == TileType.Residential ? "주민" : "일자리";
                GUI.Label(new Rect(r.x + 10, r.y + 30, 210, 20), $"레벨 {t.Level}/{sim.Config.MaxLevel}   {unit} {cap}", small);
                GUI.Label(new Rect(r.x + 10, r.y + 50, 210, 20), $"{CityStrings.Happiness} {t.Happiness}%", small);
                if (!t.HasRoadAccess)
                {
                    GUI.color = new Color(1f, 0.5f, 0.4f);
                    GUI.Label(new Rect(r.x + 10, r.y + 72, 210, 20), CityStrings.NoRoad, small);
                    GUI.color = Color.white;
                }
                else if (t.Level < sim.Config.MaxLevel)
                {
                    int need = sim.Config.HappinessForLevel[Mathf.Min(t.Level + 1, sim.Config.HappinessForLevel.Length - 1)];
                    GUI.Label(new Rect(r.x + 10, r.y + 72, 210, 20), $"다음 레벨: 행복도 {need}% 이상 + 수요", small);
                }
            }
            else
            {
                GUI.Label(new Rect(r.x + 10, r.y + 30, 210, 20), $"선택 도구: {CityStrings.ToolName(game.Tool)}", small);
            }
        }

        private void DrawNotifications(float w)
        {
            float y = 56;
            foreach (KeyValuePair<string, float> n in game.Notifications)
            {
                var r = new Rect(w / 2f - 200, y, 400, 28);
                GUI.Box(r, GUIContent.none, panel);
                GUI.Label(r, n.Key, center);
                y += 32;
            }
        }

        private void DrawHelp(float w)
        {
            float h = 30 + CityStrings.HelpLines.Length * 20;
            Rect r = Panel(new Rect(10, 56, 440, h));
            GUI.Label(new Rect(r.x + 10, r.y + 4, 300, 22), CityStrings.Help, label);
            if (GUI.Button(new Rect(r.xMax - 30, r.y + 4, 22, 20), "×", button)) ShowHelp = false;
            for (int i = 0; i < CityStrings.HelpLines.Length; i++)
                GUI.Label(new Rect(r.x + 10, r.y + 26 + i * 20, 420, 20), CityStrings.HelpLines[i], small);
        }
    }
}
