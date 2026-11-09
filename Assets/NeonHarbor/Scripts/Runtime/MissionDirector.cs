using System;
using System.Collections.Generic;
using NeonHarbor.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace NeonHarbor.Runtime
{
    /// <summary>
    /// Job markers (yellow light beams), the active <see cref="MissionRun"/> and its
    /// checkpoint beam, and the cash pickups scattered on sidewalks.
    /// </summary>
    public sealed class MissionDirector
    {
        /// <summary>A job offered at a fixed street location.</summary>
        public sealed class Job { public string Name; public MissionKind Kind; public Vector3 Pos; public Transform Beam; }

        private readonly HarborConfig cfg;
        private readonly CityLayout city;
        private readonly HarborMaterials mats;
        private readonly Transform root;
        private readonly System.Random rnd = new System.Random(7);
        private readonly List<Transform> cash = new List<Transform>();
        private Transform targetBeam;
        private float cooldown;

        public readonly List<Job> Jobs = new List<Job>();
        /// <summary>Running job, or null.</summary>
        public MissionRun Active { get; private set; }
        /// <summary>Running job's definition.</summary>
        public Job ActiveJob { get; private set; }
        /// <summary>Current checkpoint position (for the minimap), or null.</summary>
        public Vector3? Target => targetBeam != null ? targetBeam.position : (Vector3?)null;
        /// <summary>Cash pickup positions (for the minimap).</summary>
        public IEnumerable<Transform> Cash => cash;

        /// <summary>(title, subtitle, good?) big message.</summary>
        public event Action<string, string, bool> Banner;
        /// <summary>Small message.</summary>
        public event Action<string> Toast;
        /// <summary>Money gained (amount).</summary>
        public event Action<int> Paid;
        /// <summary>Checkpoint reached.</summary>
        public event Action Checkpoint;

        /// <summary>Places job markers and cash.</summary>
        public MissionDirector(HarborConfig cfg, CityLayout city, HarborMaterials mats, Transform root)
        {
            this.cfg = cfg; this.city = city; this.mats = mats; this.root = root;
            float P = city.Pitch;
            Jobs.Add(new Job { Name = "택배 배달", Kind = MissionKind.Courier, Pos = new Vector3(3 * P + 14, 0.18f, 4 * P + 9.5f) });
            Jobs.Add(new Job { Name = "스트리트 레이스", Kind = MissionKind.Race, Pos = new Vector3(5 * P - 9.5f, 0.18f, 2 * P + 20) });
            Jobs.Add(new Job { Name = "추격 따돌리기", Kind = MissionKind.Evade, Pos = new Vector3(2 * P + 30, 0.18f, 6 * P - 9.5f) });
            foreach (var j in Jobs) j.Beam = MakeBeam(new Color(1f, 0.75f, 0.15f), j.Pos);
            var cube = MeshBuilder.Cube(); var glow = mats.Glow(new Color(0.2f, 1f, 0.4f), 3f);
            for (int k = 0; k < cfg.CashPickups; k++)
            {
                var go = new GameObject("Cash"); go.transform.SetParent(root, false); go.transform.localScale = new Vector3(0.5f, 0.26f, 0.26f);
                go.AddComponent<MeshFilter>().sharedMesh = cube; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = glow; r.shadowCastingMode = ShadowCastingMode.Off;
                PlaceCash(go.transform); cash.Add(go.transform);
            }
        }

        private Transform MakeBeam(Color c, Vector3 at)
        {
            var t = new GameObject("Beam").transform; t.SetParent(root, false); t.position = at;
            var cyl = new MeshBuilder(); cyl.Add(MeshBuilder.Cylinder(32), Matrix4x4.TRS(new Vector3(0, 20, 0), Quaternion.identity, new Vector3(3.2f, 40, 3.2f)));
            var g = new GameObject("Light"); g.transform.SetParent(t, false);
            g.AddComponent<MeshFilter>().sharedMesh = cyl.ToMesh("beam");
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = BeamMaterial(c); r.shadowCastingMode = ShadowCastingMode.Off;
            var ring = new MeshBuilder(); ring.Add(MeshBuilder.Cylinder(32), Matrix4x4.TRS(new Vector3(0, 0.3f, 0), Quaternion.identity, new Vector3(3.8f, 0.08f, 3.8f)));
            var rg = new GameObject("Ring"); rg.transform.SetParent(t, false);
            rg.AddComponent<MeshFilter>().sharedMesh = ring.ToMesh("ring");
            var rr = rg.AddComponent<MeshRenderer>(); rr.sharedMaterial = mats.Glow(c, 4f); rr.shadowCastingMode = ShadowCastingMode.Off;
            return t;
        }

        private Material BeamMaterial(Color c)
        {
            var m = new Material(mats.Pool) { name = "BeamColumn", mainTexture = Texture2D.whiteTexture };
            m.SetColor("_TintColor", new Color(c.r, c.g, c.b, 0.22f)); m.SetColor("_Color", new Color(c.r, c.g, c.b, 0.22f));
            return m;
        }

        private Vector3 SidewalkPoint(Vector3 from, float minD, float maxD)
        {
            for (int a = 0; a < 60; a++)
            {
                var blk = city.Blocks[rnd.Next(city.Blocks.Count)]; var q = city.SidewalkPoint(blk, (float)rnd.NextDouble());
                float d = Vector2.Distance(new Vector2(q.x, q.z), new Vector2(from.x, from.z));
                if (d > minD && d < maxD) return new Vector3(q.x, 0.18f, q.z);
            }
            return from;
        }

        private void PlaceCash(Transform t)
        {
            for (int a = 0; a < 30; a++)
            {
                var blk = city.Blocks[rnd.Next(city.Blocks.Count)]; if (blk.Type == BlockType.Harbor) continue;
                var q = city.SidewalkPoint(blk, (float)rnd.NextDouble()); t.position = new Vector3(q.x, 0.9f, q.z); return;
            }
        }

        private void SetTarget(Vector2? p)
        {
            if (targetBeam != null) UnityEngine.Object.Destroy(targetBeam.gameObject);
            targetBeam = p.HasValue ? MakeBeam(new Color(0.2f, 0.8f, 1f), new Vector3(p.Value.x, 0.18f, p.Value.y)) : null;
        }

        private void Start(Job job, PlayerController player)
        {
            var from = new Vec2(player.Position.x, player.Position.z);
            if (job.Kind == MissionKind.Race && player.Car == null) { Toast?.Invoke("레이스는 차량이 필요합니다"); cooldown = 3; return; }
            if (job.Kind == MissionKind.Courier)
            {
                var p = SidewalkPoint(player.Position, 160, 330);
                Active = MissionRun.Courier(cfg, from, new Vec2(p.x, p.z));
            }
            else if (job.Kind == MissionKind.Race)
            {
                var pts = new List<Vec2>(); var cur = city.NearestNode(player.Position.x, player.Position.z); var prev = cur;
                for (int k = 0; k < 7; k++)
                {
                    var nx = cur;
                    for (int s = 0; s < 2; s++) { var opts = city.Neighbours(nx.Item1, nx.Item2); opts.RemoveAll(o => o.Equals(prev)); var n = opts[rnd.Next(opts.Count)]; prev = nx; nx = n; }
                    cur = nx; pts.Add(city.NodePos(cur.Item1, cur.Item2));
                }
                Active = MissionRun.Race(cfg, from, pts);
            }
            else { Active = MissionRun.Evade(cfg); Toast?.Invoke("수배 ★★ 를 얻은 뒤 경찰을 따돌리세요"); }
            ActiveJob = job;
            if (Active.Points.Count > 0) SetTarget(new Vector2(Active.Points[0].X, Active.Points[0].Y));
            Banner?.Invoke(job.Name, "미션 시작", true);
        }

        private void Finish(bool ok)
        {
            if (ok) { Paid?.Invoke(Active.Reward); Banner?.Invoke("미션 성공", "+$" + Active.Reward.ToString("N0"), true); }
            else Banner?.Invoke("미션 실패", ActiveJob.Name, false);
            SetTarget(null); Active = null; ActiveJob = null; cooldown = 4;
        }

        /// <summary>Cancels the running job (arrest / knockout).</summary>
        public void Cancel() { if (Active != null) Finish(false); }

        /// <summary>Call when the wanted level was shaken off.</summary>
        public void NotifyEvaded() { Active?.NotifyEvaded(); }

        /// <summary>Per-frame update.</summary>
        public void Tick(float dt, float time, PlayerController player, int stars)
        {
            foreach (var j in Jobs) { j.Beam.gameObject.SetActive(Active == null); j.Beam.GetChild(1).localPosition = new Vector3(0, Mathf.Sin(time * 2) * 0.15f, 0); }
            foreach (var c in cash)
            {
                c.Rotate(0, dt * 120, 0, Space.World);
                c.position = new Vector3(c.position.x, 0.9f + Mathf.Sin(time * 3 + c.GetInstanceID()) * 0.12f, c.position.z);
                if (Vector2.Distance(new Vector2(c.position.x, c.position.z), new Vector2(player.Position.x, player.Position.z)) < 1.6f)
                {
                    int v = rnd.Next(cfg.CashMin, cfg.CashMax + 1); Paid?.Invoke(v); Toast?.Invoke("+$" + v); PlaceCash(c);
                }
            }
            cooldown -= dt;
            if (Active == null)
            {
                if (cooldown <= 0) foreach (var j in Jobs) if (Vector2.Distance(new Vector2(j.Pos.x, j.Pos.z), new Vector2(player.Position.x, player.Position.z)) < 3) { Start(j, player); break; }
                return;
            }
            var status = Active.Tick(dt, new Vec2(player.Position.x, player.Position.z), stars);
            if (status == MissionStatus.Failed) Finish(false);
            else if (status == MissionStatus.Succeeded) Finish(true);
            else if (status == MissionStatus.CheckpointReached) { Checkpoint?.Invoke(); var p = Active.Points[Active.Index]; SetTarget(new Vector2(p.X, p.Y)); }
        }
    }
}
