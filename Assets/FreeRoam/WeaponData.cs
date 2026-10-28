using System;
using UnityEngine;

public enum SightType { IronSights, RedDot, Scope, Integral }   // Integral: a built-in low-power scope (SAR-21): the world and the rifle stay visible around the tube

// Real sight data. Magnification is the optic's zoom: 1x for iron sights and red dots.
public class WeaponSpec
{
    public string id, name;
    public SightType sight;
    public float magnification = 1f;
    public float adsTime = 0.2f;     // seconds to bring the sight to the eye
    public float swayDeg = 0.2f;     // hand sway while aimed, in degrees
    public Vector3 hipPos, adsPos;   // gun position relative to the eye, at hip and at the sight picture
    public bool loaded;
    public Vector3 bmin, bmax;       // model bounds in gun space
}

[Serializable] class GunPart { public string n; public float[] pv; public float[] c; public float[] v; }
[Serializable] class GunFile
{
    public string name, sight;
    public float[] bmin, bmax;
    public float mag, sightY, sightZ, relief, adsTime, sway, length;
    public GunPart[] parts;
}

public static class WeaponData
{
    // Gun meshes are Assets/Resources/Guns/<id>.json (exported from real dimensions, unshared vertices for flat shading).
    static readonly string[] Ids = { "pistol", "smg", "ak", "sawn", "lmg", "knife", "sar21" };
    static readonly string[] Names = { "C19 Pistol", "SMG", "AK", "Sawn-off", "LMG", "Knife", "SAR-21" };
    public static readonly WeaponSpec[] All = Build();

    static WeaponSpec[] Build()
    {
        var a = new WeaponSpec[Ids.Length];
        for (int i = 0; i < a.Length; i++) a[i] = new WeaponSpec { id = Ids[i], name = Names[i] };
        return a;
    }

    public static void BuildModel(Transform h, WeaponSpec s)
    {
        var ta = Resources.Load<TextAsset>("Guns/" + s.id);
        if (ta == null) { Debug.LogError("Missing gun mesh Guns/" + s.id); return; }
        var g = JsonUtility.FromJson<GunFile>(ta.text);
        if (!s.loaded)
        {
            s.loaded = true;
            s.name = g.name;
            s.sight = g.sight == "Scope" ? SightType.Scope : g.sight == "RedDot" ? SightType.RedDot : SightType.IronSights;
            s.bmin = new Vector3(g.bmin[0], g.bmin[1], g.bmin[2]); s.bmax = new Vector3(g.bmax[0], g.bmax[1], g.bmax[2]);
            s.magnification = g.mag; s.adsTime = g.adsTime; s.swayDeg = g.sway;
            s.hipPos = g.length < 0.3f
                ? new Vector3(0.10f, -0.07f, 0.40f)                                  // pistol: two-handed hold, arms extended in front of the chest
                : new Vector3(0.16f, -0.12f - g.sightY * 0.3f, 0.30f + g.length * 0.2f);
        s.hipPos += new Vector3(0f, 0f, HipDz(s.id));   // pull the gun back toward the shoulder so the support hand can reach the forend
            s.adsPos = new Vector3(0f, -g.sightY, g.relief - g.sightZ);   // rear sight / optic axis on the eye's centre line
        }
        foreach (var p in g.parts)
        {
            int n = p.v.Length / 3;
            var vs = new Vector3[n]; var tr = new int[n];
            for (int k = 0; k < n; k++) { vs[k] = new Vector3(p.v[3 * k], p.v[3 * k + 1], p.v[3 * k + 2]); tr[k] = k; }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = vs; mesh.triangles = tr; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            Transform parent = h;
            if (!string.IsNullOrEmpty(p.n))
            {   // functional part (slide, magazine, pump...): one pivot object per name so it can be moved or rotated as a unit
                parent = h.Find(p.n);
                if (parent == null) { parent = new GameObject(p.n).transform; parent.SetParent(h, false); parent.localPosition = new Vector3(p.pv[0], p.pv[1], p.pv[2]); }
            }
            var go = new GameObject("part");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = CityBuilder.Mat(new Color(p.c[0], p.c[1], p.c[2]));
        }
    }
    static float HipDz(string id)
    {
        switch (id)
        {
            case "mp5": return -0.02f; case "m16a4": return -0.08f; case "r870": return 0f; case "awm": return -0.02f;
            case "ak47": return -0.09f; case "sar21": return -0.12f; case "p90": return 0f; default: return 0f;
        }
    }
}

