using System.Collections.Generic;
using NeonHarbor.Core;
using UnityEngine;

namespace NeonHarbor.Runtime
{
    /// <summary>
    /// IMGUI HUD on a 1280×720 reference canvas: rotating minimap with blips,
    /// health, clock / money / wanted stars, speedometer, mission panel, context
    /// prompt, toasts, big banners and the intro screen. Needs no Canvas or font asset.
    /// </summary>
    public sealed class HarborHud : MonoBehaviour
    {
        private const float RefH = 720f, MapSize = 200f, MapScale = 1.15f, MapOffset = 50f;
        private HarborGame game;
        private Texture2D map, white;
        private GUIStyle money, clock, starOn, starOff, label, small, big, bigSub, prompt, speed, unit, title, tagline, panelTitle, timer, button;
        private readonly List<(string text, float time, Color color)> toasts = new List<(string, float, Color)>();
        private string bannerTitle, bannerSub; private float bannerTime; private Color bannerColor;
        private float scale = 1, hurt;
        private Texture2D disc;

        /// <summary>Red screen-edge flash when the player is hurt (0..1).</summary>
        public void Hurt(float k) { hurt = Mathf.Min(1, hurt + k); }

        /// <summary>Intro screen visible.</summary>
        public bool ShowIntro = true;

        /// <summary>Wires the game and renders the minimap base.</summary>
        public void Init(HarborGame g)
        {
            game = g; white = Texture2D.whiteTexture;
            var city = g.City; int W = Mathf.CeilToInt((city.Size + 120) * MapScale);
            map = new Texture2D(W, W, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Minimap" };
            var px = new Color32[W * W];
            Color32 water = new Color32(28, 58, 82, 255), road = new Color32(59, 63, 70, 255);
            for (int k = 0; k < px.Length; k++) px[k] = water;
            void Rect(float x0, float z0, float x1, float z1, Color32 c)
            {
                int a0 = Mathf.Clamp((int)((x0 + MapOffset) * MapScale), 0, W - 1), a1 = Mathf.Clamp((int)((x1 + MapOffset) * MapScale), 0, W - 1);
                int b0 = Mathf.Clamp((int)((z0 + MapOffset) * MapScale), 0, W - 1), b1 = Mathf.Clamp((int)((z1 + MapOffset) * MapScale), 0, W - 1);
                for (int b = b0; b <= b1; b++) for (int a = a0; a <= a1; a++) px[b * W + a] = c;
            }
            Rect(-city.HalfRoad, -city.HalfRoad, city.Size + city.HalfRoad + CityLayout.Quay, city.Size + city.HalfRoad + CityLayout.Quay, road);
            foreach (var blk in city.Blocks)
            {
                Rect(blk.X0, blk.Z0, blk.X1, blk.Z1, blk.Type == BlockType.Park ? new Color32(63, 107, 58, 255) : blk.Type == BlockType.Harbor ? new Color32(90, 85, 80, 255) : new Color32(106, 110, 117, 255));
                foreach (var b in blk.Buildings) Rect(b.X0, b.Z0, b.X1, b.Z1, blk.Type == BlockType.Downtown ? new Color32(140, 146, 155, 255) : new Color32(123, 128, 136, 255));
            }
            map.SetPixels32(px); map.Apply();
            disc = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var dp = new Color32[64 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) dp[y * 64 + x] = (x - 31.5f) * (x - 31.5f) + (y - 31.5f) * (y - 31.5f) < 31.5f * 31.5f ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            disc.SetPixels32(dp); disc.Apply();
        }

        /// <summary>Queues a small message.</summary>
        public void Toast(string text, Color? color = null) { toasts.Add((text, Time.unscaledTime, color ?? Color.white)); if (toasts.Count > 3) toasts.RemoveAt(0); }

        /// <summary>Shows a big centre banner for 3 s.</summary>
        public void Banner(string titleText, string sub, Color color) { bannerTitle = titleText; bannerSub = sub; bannerColor = color; bannerTime = Time.unscaledTime; }

        private void Styles()
        {
            if (money != null) return;
            GUIStyle S(int size, Color c, FontStyle fs = FontStyle.Normal, TextAnchor a = TextAnchor.UpperLeft) => new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fs, alignment = a, normal = { textColor = c }, wordWrap = false, richText = true };
            money = S(40, new Color(0.49f, 0.88f, 0.56f), FontStyle.Bold, TextAnchor.UpperRight);
            clock = S(20, new Color(0.75f, 0.78f, 0.82f), FontStyle.Normal, TextAnchor.UpperRight);
            starOn = S(26, new Color(1f, 0.8f, 0.2f), FontStyle.Bold, TextAnchor.MiddleCenter);
            starOff = S(26, new Color(1, 1, 1, 0.2f), FontStyle.Bold, TextAnchor.MiddleCenter);
            label = S(16, Color.white); small = S(13, new Color(0.78f, 0.82f, 0.86f));
            big = S(76, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter); bigSub = S(22, Color.white, FontStyle.Normal, TextAnchor.MiddleCenter);
            prompt = S(16, Color.white, FontStyle.Normal, TextAnchor.MiddleCenter);
            speed = S(64, Color.white, FontStyle.Bold, TextAnchor.LowerRight); unit = S(18, new Color(0.75f, 0.78f, 0.82f), FontStyle.Normal, TextAnchor.LowerLeft);
            title = S(96, new Color(1f, 0.42f, 0.76f), FontStyle.Bold); tagline = S(16, new Color(0.8f, 0.84f, 0.88f)); tagline.wordWrap = true;
            panelTitle = S(19, new Color(0.24f, 0.83f, 1f), FontStyle.Bold); timer = S(28, Color.white, FontStyle.Bold);
            button = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = Color.black, background = Solid(new Color(1f, 0.75f, 0.3f)) }, hover = { textColor = Color.black, background = Solid(new Color(1f, 0.85f, 0.45f)) } };
        }

        private static Texture2D Solid(Color c) { var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave }; t.SetPixel(0, 0, c); t.Apply(); return t; }

        private void Box(Rect r, Color c) { var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = old; }

        private void Shadowed(Rect r, string text, GUIStyle st)
        {
            var c = st.normal.textColor; st.normal.textColor = new Color(0, 0, 0, 0.7f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, st); st.normal.textColor = c; GUI.Label(r, text, st);
        }

        private void OnGUI()
        {
            if (game == null || game.Player == null) return;
            Styles();
            scale = Screen.height / RefH;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float W = Screen.width / scale;
            var p = game.Player;

            if (ShowIntro) { DrawIntro(W); return; }
            var svc = game.Services;
            if (svc.Current == OpenWorld.Mode.Map) { DrawFullMap(W); return; }

            // top right: clock, money, stars
            int hours = (int)(game.Day.T * 24), mins = (int)(game.Day.T * 24 * 60 % 60);
            Shadowed(new Rect(W - 260, 12, 240, 26), $"{hours:00}:{mins:00}", clock);
            Shadowed(new Rect(W - 360, 34, 340, 48), "$" + p.Money.ToString("N0"), money);
            bool flash = game.Wanted.Searching && Mathf.FloorToInt(Time.unscaledTime * 3) % 2 == 0;
            for (int k = 0; k < 5; k++) Shadowed(new Rect(W - 180 + k * 32, 82, 30, 30), "★", k < game.Wanted.Stars && !flash ? starOn : starOff);
            var arms = p.Arsenal;
            string ammo = arms.Spec.Melee ? "" : arms.Reloading ? "재장전…" : $"{arms.Clip} / {arms.Reserve}";
            Shadowed(new Rect(W - 360, 114, 340, 30), $"<size=14>{arms.Spec.Name}</size>   <size=24><b>{ammo}</b></size>", new GUIStyle(clock) { normal = { textColor = Color.white } });

            // hurt vignette + crosshair
            hurt = Mathf.Max(0, hurt - Time.deltaTime * 1.6f);
            if (hurt > 0.01f) { Box(new Rect(0, 0, W, 40), new Color(0.75f, 0.04f, 0.08f, hurt * 0.5f)); Box(new Rect(0, RefH - 40, W, 40), new Color(0.75f, 0.04f, 0.08f, hurt * 0.5f)); Box(new Rect(0, 0, 40, RefH), new Color(0.75f, 0.04f, 0.08f, hurt * 0.5f)); Box(new Rect(W - 40, 0, 40, RefH), new Color(0.75f, 0.04f, 0.08f, hurt * 0.5f)); }
            if (!arms.Spec.Melee && (p.Car == null || game.Combat.Aiming))
            {
                float g = game.Combat.Aiming ? 4 : 8; var cc = game.Combat.HitMarker > 0 ? new Color(1f, 0.3f, 0.37f) : Color.white; float cx = W / 2, cy = RefH / 2;
                Box(new Rect(cx - 1, cy - g - 8, 2, 8), cc); Box(new Rect(cx - 1, cy + g, 2, 8), cc); Box(new Rect(cx - g - 8, cy - 1, 8, 2), cc); Box(new Rect(cx + g, cy - 1, 8, 2), cc); Box(new Rect(cx - 1.5f, cy - 1.5f, 3, 3), cc);
            }

            // bottom left: minimap + health
            DrawMinimap(new Rect(18, RefH - MapSize - 34, MapSize, MapSize));
            Box(new Rect(18, RefH - 26, MapSize, 8), new Color(0, 0, 0, 0.55f));
            Box(new Rect(18, RefH - 26, MapSize * Mathf.Clamp01(p.Health / 100f), 8), new Color(0.31f, 0.83f, 0.42f));
            if (p.Armor > 0) { Box(new Rect(18, RefH - 14, MapSize, 6), new Color(0, 0, 0, 0.55f)); Box(new Rect(18, RefH - 14, MapSize * Mathf.Clamp01(p.Armor / 100f), 6), new Color(0.24f, 0.55f, 1f)); }

            // bottom right: speedometer
            if (p.Car != null)
            {
                float vF = p.Car.Model.ForwardSpeed; int kmh = Mathf.RoundToInt(Mathf.Abs(vF) * 3.6f);
                string gear = vF < -0.5f ? "R" : kmh < 2 ? "N" : (1 + Mathf.Min(5, kmh / 40)).ToString();
                Shadowed(new Rect(W - 300, RefH - 130, 200, 24), p.Car.Spec.Name, new GUIStyle(small) { alignment = TextAnchor.UpperRight });
                Shadowed(new Rect(W - 300, RefH - 110, 210, 80), $"<color=#ffcc33><size=30>{gear}</size></color> {kmh}", speed);
                Shadowed(new Rect(W - 88, RefH - 62, 80, 30), "km/h", unit);
                Box(new Rect(W - 190, RefH - 24, 170, 5), new Color(0, 0, 0, 0.55f));
                Box(new Rect(W - 190, RefH - 24, 170 * Mathf.Clamp01(p.Car.Health / 100f), 5), new Color(1f, 0.6f, 0.25f));
                GUI.Label(new Rect(W - 330, RefH - 18, 310, 18), svc.Station >= 0 ? "<color=#3cd4ff>♪ " + OpenWorld.Stations[svc.Station] + "</color>" : "라디오 꺼짐 ( , . )", new GUIStyle(small) { alignment = TextAnchor.UpperRight });
            }

            // mission panel (top left)
            var m = game.Missions;
            if (m.Active != null)
            {
                var r = new Rect(18, 16, 360, 96); Box(r, new Color(0.03f, 0.05f, 0.08f, 0.62f)); Box(new Rect(18, 16, 3, 96), new Color(0.24f, 0.83f, 1f));
                GUI.Label(new Rect(30, 22, 340, 26), m.ActiveJob.Name, panelTitle);
                string goal = m.Active.Kind == MissionKind.Courier ? "표시된 지점으로 소포를 배달하세요"
                    : m.Active.Kind == MissionKind.Race ? $"체크포인트 {m.Active.Index + 1} / {m.Active.Points.Count}"
                    : m.Active.Armed ? "경찰을 따돌리세요 (지도의 경찰에게서 멀어지기)" : $"수배 ★★ 이상 얻기 (현재 {game.Wanted.Stars})";
                GUI.Label(new Rect(30, 48, 340, 22), goal, label);
                float t = Mathf.Max(0, m.Active.TimeLeft);
                var ts = new GUIStyle(timer) { normal = { textColor = t < 10 ? new Color(1f, 0.3f, 0.37f) : Color.white } };
                GUI.Label(new Rect(30, 70, 120, 34), $"{(int)(t / 60)}:{(int)(t % 60):00}", ts);
                GUI.Label(new Rect(200, 78, 170, 22), $"<color=#7ee08f>보상 ${m.Active.Reward:N0}</color>", small);
            }

            // context prompt
            string pr = null;
            if (p.Car == null) { var c = p.NearestEnterable(); if (c != null) pr = $"[F] {c.Spec.Name} {(c.Mode == VehicleMode.Traffic ? "탈취" : "탑승")}"; }
            else if (p.Car.Speed < 4) pr = "[F] 내리기";
            if (m.Active == null) foreach (var j in m.Jobs) if (Vector2.Distance(new Vector2(j.Pos.x, j.Pos.z), new Vector2(p.Position.x, p.Position.z)) < 12) pr = $"노란 빛기둥에 들어가면 {j.Name} 시작";
            if (pr != null) { var r = new Rect(W / 2 - 200, RefH * 0.72f, 400, 34); Box(r, new Color(0.03f, 0.05f, 0.08f, 0.62f)); GUI.Label(r, pr, prompt); }

            // toasts
            float y = RefH * 0.22f;
            for (int k = toasts.Count - 1; k >= 0; k--) if (Time.unscaledTime - toasts[k].time > 3.2f) toasts.RemoveAt(k);
            foreach (var (text, _, color) in toasts)
            {
                var r = new Rect(W / 2 - 220, y, 440, 32); Box(r, new Color(0.03f, 0.05f, 0.08f, 0.62f));
                var st = new GUIStyle(prompt) { normal = { textColor = color } }; GUI.Label(r, text, st); y += 38;
            }

            if (svc.Current == OpenWorld.Mode.Wheel) DrawWheel(W);
            if (svc.Current == OpenWorld.Mode.Shop) DrawShop(W);
            if (GUI.Button(new Rect(W - 80, 150, 62, 26), "지도 M")) svc.ToggleMap();

            // banner
            if (bannerTitle != null && Time.unscaledTime - bannerTime < 3f)
            {
                var st = new GUIStyle(big) { normal = { textColor = bannerColor } };
                Shadowed(new Rect(0, RefH / 2 - 70, W, 100), bannerTitle, st);
                Shadowed(new Rect(0, RefH / 2 + 26, W, 30), bannerSub, bigSub);
            }
        }

        private void DrawMinimap(Rect r)
        {
            var p = game.Player; var city = game.City;
            Box(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), new Color(1, 1, 1, 0.85f));
            GUI.BeginGroup(r);
            Vector2 c = new Vector2(r.width / 2, r.height / 2);
            float yawDeg = game.Camera.Yaw * Mathf.Rad2Deg;
            var saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(-yawDeg, c);
            float px = (p.Position.x + MapOffset) * MapScale, pz = (p.Position.z + MapOffset) * MapScale;
            // texture v=1 is the top; world +z maps to up
            GUI.DrawTexture(new Rect(c.x - px, c.y - (map.height - pz), map.width, map.height), map);
            GUI.matrix = saved;
            Vector3 fwd = new Vector3(Mathf.Sin(game.Camera.Yaw), 0, Mathf.Cos(game.Camera.Yaw)), right = new Vector3(Mathf.Cos(game.Camera.Yaw), 0, -Mathf.Sin(game.Camera.Yaw));
            void Blip(Vector3 w, Color col, float size, bool clampEdge)
            {
                Vector3 d = w - p.Position; float sx = Vector3.Dot(d, right) * MapScale, sy = -Vector3.Dot(d, fwd) * MapScale;
                float lim = r.width / 2 - 6;
                if (Mathf.Abs(sx) > lim || Mathf.Abs(sy) > lim) { if (!clampEdge) return; float k = lim / Mathf.Max(Mathf.Abs(sx), Mathf.Abs(sy)); sx *= k; sy *= k; }
                Box(new Rect(c.x + sx - size / 2 - 1, c.y + sy - size / 2 - 1, size + 2, size + 2), Color.black);
                Box(new Rect(c.x + sx - size / 2, c.y + sy - size / 2, size, size), col);
            }
            // police search area (flashing red / blue disc)
            if (game.Wanted.Stars > 0 && game.Wanted.Searching)
            {
                var lk = new Vector3(game.Wanted.LastKnown.X, 0, game.Wanted.LastKnown.Y) - p.Position; float rr = game.Wanted.SearchRadius * MapScale;
                float sx = Vector3.Dot(lk, right) * MapScale, sy = -Vector3.Dot(lk, fwd) * MapScale;
                var old = GUI.color; GUI.color = Mathf.FloorToInt(Time.unscaledTime * 2) % 2 == 0 ? new Color(1f, 0.24f, 0.31f, 0.25f) : new Color(0.24f, 0.43f, 1f, 0.25f);
                GUI.DrawTexture(new Rect(c.x + sx - rr, c.y + sy - rr, rr * 2, rr * 2), disc); GUI.color = old;
            }
            // GPS route
            var route = game.Services.Route;
            for (int k = 0; k + 1 < route.Count; k++)
            {
                Vector3 a = route[k] - p.Position, b = route[k + 1] - p.Position;
                Vector2 A = new Vector2(Vector3.Dot(a, right), -Vector3.Dot(a, fwd)) * MapScale + c, Bv = new Vector2(Vector3.Dot(b, right), -Vector3.Dot(b, fwd)) * MapScale + c;
                Vector2 d = Bv - A; float len = d.magnitude; if (len < 0.5f) continue;
                var m0 = GUI.matrix; GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, A);
                Box(new Rect(A.x, A.y - 2.5f, len, 5), game.Services.RouteIsMission ? new Color(1f, 0.82f, 0.24f) : new Color(0.78f, 0.42f, 1f));
                GUI.matrix = m0;
            }
            foreach (var pl in game.Services.Places) Blip(pl.Pos, pl.Color, 8, false);
            if (game.Services.Waypoint.HasValue) Blip(game.Services.Waypoint.Value, new Color(0.78f, 0.42f, 1f), 9, true);
            foreach (var cash in game.Missions.Cash) Blip(cash.position, new Color(0.36f, 1f, 0.54f), 4, false);
            if (game.Missions.Active == null) foreach (var j in game.Missions.Jobs) Blip(j.Pos, new Color(1f, 0.76f, 0.23f), 9, true);
            if (game.Missions.Target.HasValue) Blip(game.Missions.Target.Value, new Color(0.24f, 0.83f, 1f), 10, true);
            bool redPhase = Mathf.FloorToInt(Time.time * 4) % 2 == 0;
            foreach (var v in game.World.Vehicles) if (v.Mode == VehicleMode.Police) Blip(v.Position, redPhase ? new Color(1f, 0.23f, 0.23f) : new Color(0.23f, 0.42f, 1f), 7, true);
            // player arrow
            float heading = (p.Car != null ? p.Car.Model.Yaw : p.Yaw) - game.Camera.Yaw;
            var m2 = GUI.matrix; GUIUtility.RotateAroundPivot(heading * Mathf.Rad2Deg, c);
            Box(new Rect(c.x - 5, c.y - 5, 10, 10), Color.black); Box(new Rect(c.x - 4, c.y - 4, 8, 8), Color.white); Box(new Rect(c.x - 2, c.y - 11, 4, 8), Color.white);
            GUI.matrix = m2;
            GUI.EndGroup();
        }

        private void DrawFullMap(float W)
        {
            var svc = game.Services; var city = game.City;
            Box(new Rect(0, 0, W, RefH), new Color(0.02f, 0.03f, 0.06f, 0.95f));
            float size = RefH - 110, ox = (W - size) / 2, oy = 60;
            GUI.DrawTexture(new Rect(ox, oy, size, size), map);
            float k = size / map.width;
            Vector2 W2S(Vector3 w) => new Vector2(ox + (w.x + MapOffset) * MapScale * k, oy + size - (w.z + MapOffset) * MapScale * k);
            for (int i = 0; i + 1 < svc.Route.Count; i++)
            {
                Vector2 A = W2S(svc.Route[i]), B = W2S(svc.Route[i + 1]), d = B - A; if (d.magnitude < 0.5f) continue;
                var m0 = GUI.matrix; GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, A);
                Box(new Rect(A.x, A.y - 2, d.magnitude, 4), svc.RouteIsMission ? new Color(1f, 0.82f, 0.24f) : new Color(0.78f, 0.42f, 1f)); GUI.matrix = m0;
            }
            void Icon(Vector3 w, Color col, string letter)
            {
                var s = W2S(w); Box(new Rect(s.x - 10, s.y - 10, 20, 20), col);
                GUI.Label(new Rect(s.x - 10, s.y - 11, 20, 20), "<color=#0b0f16><b>" + letter + "</b></color>", new GUIStyle(prompt) { fontSize = 13 });
            }
            foreach (var pl in svc.Places) Icon(pl.Pos, pl.Color, pl.Letter);
            foreach (var j in game.Missions.Jobs) Icon(j.Pos, new Color(1f, 0.76f, 0.23f), "!");
            if (svc.Waypoint.HasValue) Icon(svc.Waypoint.Value, new Color(0.78f, 0.42f, 1f), "W");
            var me = W2S(game.Player.Position); Box(new Rect(me.x - 6, me.y - 6, 12, 12), Color.black); Box(new Rect(me.x - 5, me.y - 5, 10, 10), Color.white);
            GUI.Label(new Rect(20, 14, 900, 34), "<b>네온 하버 지도</b>   <size=13>클릭: 웨이포인트 설정 · 다시 클릭: 해제 · M / Esc: 닫기</size>", new GUIStyle(label) { fontSize = 22 });
            GUI.Label(new Rect(20, RefH - 34, W - 40, 24), "<color=#ff5a6a>■</color> H 병원   <color=#4b8cff>■</color> P 경찰서   <color=#ff8a3c>■</color> G 총포사   <color=#3cffc8>■</color> S 스프레이 정비소   <color=#ffc23a>■</color> ! 미션   <color=#c86bff>■</color> W 웨이포인트", small);
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                Vector2 mp = e.mousePosition; // already in the scaled GUI space
                if (new Rect(ox, oy, size, size).Contains(mp))
                {
                    float wx = (mp.x - ox) / (MapScale * k) - MapOffset, wz = (oy + size - mp.y) / (MapScale * k) - MapOffset;
                    svc.ClickMap(wx, wz); e.Use();
                }
            }
            if (GUI.Button(new Rect(W - 100, 14, 80, 30), "닫기")) svc.Close();
        }

        private void DrawWheel(float W)
        {
            Box(new Rect(0, 0, W, RefH), new Color(0, 0, 0, 0.45f));
            var a = game.Player.Arsenal; Vector2 c = new Vector2(W / 2, RefH / 2);
            var slots = new[] { (WeaponKind.Fist, new Vector2(0, -110)), (WeaponKind.Pistol, new Vector2(110, 60)), (WeaponKind.Smg, new Vector2(-110, 60)) };
            foreach (var (kind, off) in slots)
            {
                var spec = WeaponSpec.All[(int)kind]; bool sel = game.Services.WheelSelection == kind, owned = a.Owns(kind);
                var r = new Rect(c.x + off.x - 60, c.y + off.y - 30, 120, 60);
                Box(r, sel ? new Color(1f, 0.8f, 0.2f, 0.35f) : new Color(0.04f, 0.06f, 0.1f, 0.85f));
                if (sel) { Box(new Rect(r.x, r.y, r.width, 2), new Color(1f, 0.8f, 0.2f)); Box(new Rect(r.x, r.yMax - 2, r.width, 2), new Color(1f, 0.8f, 0.2f)); }
                string ammo = spec.Melee ? "" : owned ? $"{a.ClipOf(kind)} / {a.ReserveOf(kind)}" : "없음";
                GUI.Label(r, $"<b>{spec.Name}</b>\n<size=14>{ammo}</size>", new GUIStyle(prompt) { normal = { textColor = owned ? Color.white : new Color(1, 1, 1, 0.4f) } });
            }
            GUI.Label(new Rect(c.x - 150, c.y - 12, 300, 24), "마우스로 고르고 Tab을 놓으세요", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
        }

        private void DrawShop(float W)
        {
            var r = new Rect(W / 2 - 220, 120, 440, 400);
            Box(new Rect(0, 0, W, RefH), new Color(0, 0, 0, 0.45f)); Box(r, new Color(0.05f, 0.07f, 0.1f, 0.96f)); Box(new Rect(r.x, r.y, r.width, 3), new Color(1f, 0.54f, 0.24f));
            GUI.Label(new Rect(r.x + 20, r.y + 14, 300, 40), "<color=#ff8a3c><size=30><b>총포사</b></size></color>", label);
            GUI.Label(new Rect(r.x + 20, r.y + 56, 300, 22), $"보유 자금 <b>${game.Player.Money:N0}</b>", small);
            for (int k = 0; k < 5; k++)
            {
                var item = (ShopItem)k; var br = new Rect(r.x + 20, r.y + 90 + k * 54, r.width - 40, 46);
                if (GUI.Button(br, GUIContent.none)) game.Services.Buy(item);
                GUI.Label(new Rect(br.x + 12, br.y, 300, br.height), $"[{k + 1}]  <b>{Shop.Name(item)}</b>", new GUIStyle(label) { alignment = TextAnchor.MiddleLeft });
                GUI.Label(new Rect(br.x, br.y, br.width - 12, br.height), $"<color=#7ee08f><b>${Shop.Price(game.Config, item):N0}</b></color>", new GUIStyle(label) { alignment = TextAnchor.MiddleRight, fontSize = 20 });
            }
            if (GUI.Button(new Rect(r.x + 20, r.yMax - 46, r.width - 40, 32), "나가기 (Esc)")) game.Services.Close();
        }

        private void DrawIntro(float W)
        {
            Box(new Rect(0, 0, W * 0.55f, RefH), new Color(0.02f, 0.03f, 0.06f, 0.88f));
            Shadowed(new Rect(36, 150, 600, 120), "네온 하버", title);
            GUI.Label(new Rect(40, 270, 520, 60), "항구 도시의 밤거리를 걷고, 아무 차나 잡아타고, 일을 받아 돈을 버세요. 무기를 주워 싸울 수도 있지만, 소란을 피울수록 더 많은 경찰이 총을 들고 쫓아옵니다.", tagline);
            string[] rows = { "Tab (누르고 있기)   무기 휠 · M 지도/웨이포인트 · , . 라디오", "WASD · 방향키   걷기 / 운전", "마우스   카메라 (우클릭을 누른 채 움직여도 됨)", "Shift · Space   달리기 · 점프 / 핸드브레이크", "F   차량 타기 · 내리기 (운전 중인 차는 탈취)", "좌클릭 · 우클릭   사격·주먹 · 조준 (1·2·3 / Q 무기, R 재장전)", "H · T · 휠   경적 · 시간 빨리감기 · 카메라 거리", "노란 빛기둥   택배 배달 · 스트리트 레이스 · 추격 따돌리기", "Esc   마우스 커서 풀기" };
            for (int k = 0; k < rows.Length; k++) GUI.Label(new Rect(40, 340 + k * 24, 560, 24), rows[k], small);
            if (GUI.Button(new Rect(40, 530, 240, 56), "도시로 나가기", button) || Event.current.keyCode == KeyCode.Return) game.StartPlaying();
        }
    }
}
