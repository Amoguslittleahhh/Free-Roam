using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// GRIP LAB (F10): closed-loop grip solver that runs inside Unity against the real gun meshes and the real hand rig.
// Every candidate grip is applied for one frame, the actual finger/palm bones are read back, and the grip is scored by
// "touch" (distance from every finger segment to the gun surface), penetration, hand-to-hand collision, arm reach and
// human-grip rules (fingers wrap, relaxed curl, thumb along the handguard). A (1+1) evolution strategy keeps the best.
public partial class ViewmodelRig
{
    public static string LabStatus = "";
    static readonly float[] LabTip = { 0.02422f, 0.02733f, 0.02367f, 0.02157f };
    static readonly float[] LabRad = { 0.01225f, 0.0135f, 0.0135f, 0.011f };
    const float LabCell = 0.012f;
    Dictionary<Vector3Int, List<Vector3>> labGrid;

    struct LP { public Vector3 p; public float r; public int fi; public int li; }

    void LabAdd(Vector3 p)
    {
        var k = new Vector3Int(Mathf.FloorToInt(p.x / LabCell), Mathf.FloorToInt(p.y / LabCell), Mathf.FloorToInt(p.z / LabCell));
        List<Vector3> l;
        if (!labGrid.TryGetValue(k, out l)) { l = new List<Vector3>(); labGrid[k] = l; }
        l.Add(p);
    }

    void LabBuildSurface()
    {
        labGrid = new Dictionary<Vector3Int, List<Vector3>>();
        foreach (var mf in holder.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = mf.sharedMesh; if (mesh == null) continue;
            Vector3[] v = mesh.vertices; int[] t = mesh.triangles;
            Matrix4x4 m = holder.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            for (int k = 0; k + 2 < t.Length; k += 3)
            {
                Vector3 a = m.MultiplyPoint3x4(v[t[k]]), b = m.MultiplyPoint3x4(v[t[k + 1]]), c = m.MultiplyPoint3x4(v[t[k + 2]]);
                int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max((b - a).magnitude, (c - a).magnitude) / 0.006f), 1, 50);
                for (int i = 0; i <= n; i++)
                    for (int j = 0; j <= n - i; j++)
                        LabAdd(a + (b - a) * (i / (float)n) + (c - a) * (j / (float)n));
            }
        }
    }

    float LabDist(Vector3 p)
    {
        int cx = Mathf.FloorToInt(p.x / LabCell), cy = Mathf.FloorToInt(p.y / LabCell), cz = Mathf.FloorToInt(p.z / LabCell);
        float best = 0.03f * 0.03f;
        for (int dx = -2; dx <= 2; dx++)
            for (int dy = -2; dy <= 2; dy++)
                for (int dz = -2; dz <= 2; dz++)
                {
                    List<Vector3> l;
                    if (!labGrid.TryGetValue(new Vector3Int(cx + dx, cy + dy, cz + dz), out l)) continue;
                    for (int q = 0; q < l.Count; q++) { float d = (l[q] - p).sqrMagnitude; if (d < best) best = d; }
                }
        return Mathf.Sqrt(best);
    }

    // hand sample points in holder-local space
    void LabPoints(Hand h, List<LP> L)
    {
        L.Clear();
        for (int i = 0; i < 4; i++)
        {
            Vector3[] j = new Vector3[4];
            j[0] = holder.InverseTransformPoint(h.f1[i].position);
            j[1] = holder.InverseTransformPoint(h.f2[i].position);
            j[2] = holder.InverseTransformPoint(h.f3[i].position);
            j[3] = holder.InverseTransformPoint(h.f3[i].position + h.f3[i].forward * LabTip[i]);
            for (int s = 0; s < 3; s++)
                for (int q = 0; q < 3; q++)
                    L.Add(new LP { p = Vector3.Lerp(j[s], j[s + 1], q / 2f), r = LabRad[i], fi = i, li = s });
        }
        Vector3 t0 = holder.InverseTransformPoint(h.thumb.position), t1 = holder.InverseTransformPoint(h.thumb2.position);
        Vector3 t2 = holder.InverseTransformPoint(h.thumb2.position + h.thumb2.forward * 0.04875f);
        for (int q = 0; q < 3; q++)
        {
            L.Add(new LP { p = Vector3.Lerp(t0, t1, q / 2f), r = 0.011f, fi = 4, li = 0 });
            L.Add(new LP { p = Vector3.Lerp(t1, t2, q / 2f), r = 0.0105f, fi = 4, li = 1 });
        }
        for (int x = -1; x <= 1; x++)
            for (int z = -1; z <= 1; z++)
            {
                Vector3 w = h.palm.position - h.palm.up * 0.034f + h.palm.right * (x * 0.03f) + h.palm.forward * (z * 0.03f);
                L.Add(new LP { p = holder.InverseTransformPoint(w), r = 0f, fi = 5, li = 0 });
            }
    }

    static float LabSq(float v) { return v * v; }

    float LabCost(Hand h, GripPose gp, GripPose start, bool strong, int gun, List<LP> mine, List<LP> other)
    {
        float c = 0f;
        float[,] dmin = new float[6, 3];
        for (int a = 0; a < 6; a++) for (int b = 0; b < 3; b++) dmin[a, b] = 1f;
        for (int k = 0; k < mine.Count; k++)
        {
            LP p = mine[k]; float d = LabDist(p.p) - p.r;
            if (d < dmin[p.fi, p.li]) dmin[p.fi, p.li] = d;
        }
        for (int fi = 0; fi < 4; fi++)
            for (int li = 0; li < 3; li++)
            {
                float d = dmin[fi, li];
                if (!(strong && fi == 0)) c += LabSq(Mathf.Max(0f, d - 0.003f) * 1000f) * (strong ? 0.6f : 1.0f);
                if (d < -0.002f) c += LabSq((-d - 0.002f) * 1000f) * 8f;
            }
        for (int li = 0; li < 2; li++)
        {
            float d = dmin[4, li];
            if (!strong && li == 1) c += LabSq(Mathf.Max(0f, d - 0.004f) * 1000f) * 0.25f;
            if (d < -0.002f) c += LabSq((-d - 0.002f) * 1000f) * 8f;
        }
        c += LabSq(Mathf.Max(0f, dmin[5, 0] - 0.006f) * 1000f) * (strong ? 1.5f : 1.0f);
        float dc = LabDist(holder.InverseTransformPoint(h.palm.position)) - 0.034f;
        if (dc < -0.002f) c += LabSq((-dc - 0.002f) * 1000f) * 6f;

        // the two hands must not occupy the same space
        for (int a = 0; a < mine.Count; a++)
            for (int b = 0; b < other.Count; b++)
            {
                float d = Vector3.Distance(mine[a].p, other[b].p) - mine[a].r - other[b].r;
                if (d < -0.002f) c += LabSq((-d - 0.002f) * 1000f) * 0.5f;
            }

        // arm reach: the palm must really be where the grip asks for it
        Vector3 want = holder.TransformPoint(gp.wrist + gp.fwd * 0.05f);
        c += LabSq(Vector3.Distance(want, h.palm.position) * 1000f) * 0.5f;

        // human grip rules
        Vector3 tdir = Vector3.zero;
        {
            Vector3 t1 = holder.InverseTransformPoint(h.thumb2.position);
            Vector3 t2 = holder.InverseTransformPoint(h.thumb2.position + h.thumb2.forward * 0.04875f);
            tdir = (t2 - t1).normalized;
        }
        if (gun != 0)
        {
            if (!strong)
            {
                c += 4000f * LabSq(Mathf.Max(0f, 0.75f - gp.fwd.z)) + 4000f * LabSq(Mathf.Max(0f, -0.3f - gp.fwd.y));
                c += 600f * LabSq(Mathf.Max(0f, 0.5f - tdir.z)) + 600f * LabSq(Mathf.Max(0f, tdir.y - 0.4f));
            }
            else
            {
                c += 2000f * LabSq(Mathf.Max(0f, 0.2f - gp.fwd.z));
                c += 400f * LabSq(Mathf.Max(0f, 0.1f - tdir.z));
            }
        }
        for (int i = 0; i < 4; i++)
        {
            if (strong && i == 0) continue;
            c += 0.3f * LabSq(Mathf.Max(0f, 30f - gp.mcp[i])) + 0.3f * LabSq(Mathf.Max(0f, 35f - gp.pip[i]));
            c += 0.02f * LabSq(gp.dip[i] - 0.66f * gp.pip[i]);
            if (i > 0 && !(strong && i == 1)) c += 0.01f * LabSq(Mathf.Max(0f, Mathf.Abs(gp.pip[i] - gp.pip[i - 1]) - 35f));
        }
        // stay near the offline starting point
        c += LabSq((gp.wrist - start.wrist).magnitude * 1000f) * 0.25f;
        c += LabSq(Vector3.Angle(gp.fwd, start.fwd) * 0.5f);
        if ((gp.wrist - start.wrist).magnitude > 0.035f || Vector3.Angle(gp.fwd, start.fwd) > 25f) c += 5000f;
        return c;
    }

    static float LabGauss()
    {
        float u = Random.value + 1e-6f, v = Random.value;
        return Mathf.Sqrt(-2f * Mathf.Log(u)) * Mathf.Cos(6.2831853f * v);
    }

    static GripPose LabClone(GripPose g)
    {
        return new GripPose
        {
            wrist = g.wrist, fwd = g.fwd, dorsal = g.dorsal,
            mcp = (float[])g.mcp.Clone(), pip = (float[])g.pip.Clone(), dip = (float[])g.dip.Clone(),
            fy = (float[])g.fy.Clone(), th = (float[])g.th.Clone()
        };
    }

    static GripPose LabMutate(GripPose g, float s, bool strong)
    {
        GripPose c = LabClone(g);
        float big = Random.value < 0.05f ? 2f : 1f; float ws = strong ? 0.003f : 0.006f, os = strong ? 3f : 5f;
        c.wrist += new Vector3(LabGauss(), LabGauss(), LabGauss()) * ws * s * big;
        Quaternion q = Quaternion.Euler(LabGauss() * os * s * big, LabGauss() * os * s * big, LabGauss() * os * 1.4f * s * big);
        c.fwd = (q * c.fwd).normalized;
        c.dorsal = Vector3.ProjectOnPlane(q * c.dorsal, c.fwd).normalized;
        if (strong && Random.value < 0.5f)
        {   // trigger finger: free angles, kept fairly straight
            c.mcp[0] = Mathf.Clamp(c.mcp[0] + LabGauss() * 8f * s, -15f, 40f);
            c.pip[0] = Mathf.Clamp(c.pip[0] + LabGauss() * 8f * s, 0f, 60f);
            c.dip[0] = Mathf.Clamp(c.dip[0] + LabGauss() * 8f * s, 0f, 40f);
        }
        if (Random.value < 0.6f)
        {
            c.th[0] = Mathf.Clamp(c.th[0] + LabGauss() * 8f * s, -40f, 40f);
            c.th[1] = Mathf.Clamp(c.th[1] + LabGauss() * 8f * s, -40f, 80f);
            c.th[2] = Mathf.Clamp(c.th[2] + LabGauss() * 8f * s, -10f, 70f);
        }
        return c;
    }

    // human finger curl: one number per finger, joints coupled the way a real finger bends
    static void LabSetCurl(GripPose c, int i, float k)
    {
        c.mcp[i] = Mathf.Min(95f, 80f * k); c.pip[i] = Mathf.Min(105f, 100f * k); c.dip[i] = Mathf.Min(70f, 60f * k);
    }

    void LabApply(int gun, bool right, GripPose p)
    {
        if (right) { GripData.R[gun] = p; gripL = p.wrist + p.fwd * HAND; }
        else { GripData.L[gun] = p; foreLoc = p.wrist + p.fwd * HAND; }
    }

    static string LabF(float v) { return v.ToString("0.0000", CultureInfo.InvariantCulture); }
    static string LabV(Vector3 v) { return "[" + LabF(v.x) + "," + LabF(v.y) + "," + LabF(v.z) + "]"; }
    static string LabA(float[] a) { var sb = new StringBuilder("["); for (int i = 0; i < a.Length; i++) { if (i > 0) sb.Append(","); sb.Append(LabF(a[i])); } return sb.Append("]").ToString(); }

    // runs the search for both hands of one gun; the gun must already be equipped
    public IEnumerator LabRun(int gun, int iters, StringBuilder json)
    {
        LabBuildSurface();
        var mine = new List<LP>(); var theirs = new List<LP>();
        bool first = true;
        for (int pass = 0; pass < 2; pass++)
        {
            bool right = pass == 0;
            Hand h = right ? hR : hL; Hand o = right ? hL : hR;
            GripPose[] arr = right ? GripData.R : GripData.L;
            if (arr[gun] == null) continue;
            GripPose start = LabClone(arr[gun]); GripPose cur = LabClone(start);
            float best = float.MaxValue, sigma = 1f;
            for (int it = 0; it < iters; it++)
            {
                GripPose cand = it == 0 ? LabClone(cur) : LabMutate(cur, sigma, right);
                float[] lo = { 0f, 0f, 0f, 0f }, hi = { 1.15f, 1.15f, 1.15f, 1.15f };
                for (int bs = 0; bs < 7; bs++)
                {
                    for (int i = (right ? 1 : 0); i < 4; i++) LabSetCurl(cand, i, (lo[i] + hi[i]) * 0.5f);
                    LabApply(gun, right, cand);
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    LabPoints(h, mine);
                    float[] fmin = { 1f, 1f, 1f, 1f };
                    for (int k = 0; k < mine.Count; k++)
                    {
                        if (mine[k].fi > 3) continue;
                        float d = LabDist(mine[k].p) - mine[k].r;
                        if (d < fmin[mine[k].fi]) fmin[mine[k].fi] = d;
                    }
                    for (int i = (right ? 1 : 0); i < 4; i++)
                    {
                        float mid = (lo[i] + hi[i]) * 0.5f;
                        if (fmin[i] < -0.0015f) hi[i] = mid; else lo[i] = mid;
                    }
                }
                for (int i = (right ? 1 : 0); i < 4; i++) LabSetCurl(cand, i, lo[i]);
                LabApply(gun, right, cand);
                yield return null;
                yield return new WaitForEndOfFrame();
                LabPoints(h, mine); LabPoints(o, theirs);
                float c = LabCost(h, cand, start, right, gun, mine, theirs);
                if (c < best) { best = c; cur = cand; sigma = Mathf.Min(2.5f, sigma * 1.2f); }
                else sigma = Mathf.Max(0.2f, sigma * 0.95f);
                if ((it & 3) == 0) LabStatus = "GRIP LAB  gun " + gun + (right ? " strong" : " support") + "  " + it + "/" + iters + "  cost " + best.ToString("0.0", CultureInfo.InvariantCulture);
            }
            LabApply(gun, right, cur);
            if (!first) json.Append(",");
            first = false;
            json.Append("{\"gun\":" + gun + ",\"hand\":\"" + (right ? "R" : "L") + "\",\"cost\":" + LabF(best) +
                ",\"wrist\":" + LabV(cur.wrist) + ",\"fwd\":" + LabV(cur.fwd) + ",\"dorsal\":" + LabV(cur.dorsal) +
                ",\"mcp\":" + LabA(cur.mcp) + ",\"pip\":" + LabA(cur.pip) + ",\"dip\":" + LabA(cur.dip) +
                ",\"fy\":" + LabA(cur.fy) + ",\"th\":" + LabA(cur.th) + "}");
        }
    }
}
