// 5.56x45mm NATO SS109/M855-type ball. Units: metres, kilograms, seconds.
// Every constant is sourced (see AMMO_DATA_LEDGER.md). Drag = standard G7 drag function (BRL) scaled by the published G7 BC.
using UnityEngine;
public static class Ammo556
{
    // Cartridge geometry (SAAMI Z299.4 .223 Rem / 5.56 NATO drawing)
    public const float CaseLength = 0.04470f, OverallLength = 0.05740f, RimDiameter = 0.00960f, RimThickness = 0.00114f;
    public const float BodyDiameterHead = 0.009548f, BodyDiameterShoulder = 0.009103f, NeckDiameter = 0.00643f, BulletDiameter = 0.005702f;
    public const float PrimerDiameter = 0.004432f, PrimerHeight = 0.00292f, ShoulderAngleDeg = 23f;
    // Bullet (US 6,085,661 control lot / TM 43-0001-27)
    public const float BulletLength = 0.02304f;
    // Masses (TM 43-0001-27 / M855 data sheet)
    public const float BulletMass = 0.004017f;      // 62.0 gr
    public const float PropellantMass = 0.001691f;  // 26.1 gr WC 844 ball powder
    public const float CaseMass = 0.006156f;        // 95.0 gr brass
    public const float PrimerMass = 0.000259f;      // 4.0 gr
    public const float CartridgeMass = 0.012311f;   // 190 gr
    // Ballistics
    public const float MuzzleVelocity = 945f;       // SAR 21, SS109 (Wikipedia/ST Kinetics)
    public const float MuzzleVelocityTM = 922f;     // TM 43-0001-27, 78 ft from muzzle (20-in barrel)
    public const float BallisticCoefficientG7 = 0.151f; // lb/in^2 (BRL)
    public const float MuzzleEnergyJ = 0.5f * BulletMass * MuzzleVelocity * MuzzleVelocity;
    public const float MaxPressureMPa_CIP = 430f;
    // Range / penetration (see ledger)
    public const float EffectiveRangeSS109 = 800f, EffectiveRangeM193 = 460f;  // SAR 21
    public const float MaxRangePublishedM16A2 = 3600f, MaxRangeComputed = 3339f; // computed: 31 deg, G7, ICAO
    public const float SteelPenM855_mm = 9.525f, SteelPenM855_range_m = 160f;   // 3/8" mild steel at 160 m (US Army)
    public const float SteelPenSS109_mm = 3f, SteelPenSS109_range_m = 600f;     // Wikipedia
    // ESTIMATE (not published): Poncelet depth law calibrated to the sourced steel point; see ledger.
    public enum Mat { MildSteel, Concrete, Brick, Pine, Drywall }
    static readonly float[] MatSigmaMPa = { 400f, 30f, 25f, 8f, 2f };
    static readonly float[] MatRho      = { 7850f, 2400f, 1900f, 500f, 700f };
    const float PonceletC1 = 2.195f;
    /// <summary>Estimated penetration depth (m) into a semi-infinite target at impact speed v (m/s). Rigid-bullet law: high for hard targets.</summary>
    public static float PenetrationDepth(Mat m, float v)
    {
        float area = Mathf.PI * BulletDiameter * BulletDiameter * 0.25f, rho = MatRho[(int)m], c0 = 3.5f * MatSigmaMPa[(int)m] * 1e6f;
        return BulletMass / (2f * area * PonceletC1 * rho) * Mathf.Log(1f + PonceletC1 * rho * v * v / c0);
    }
    /// <summary>Speed after passing a wall of given thickness (0 if stopped); energy-balance approx.</summary>
    public static float ExitSpeed(Mat m, float v, float thickness)
    {
        float full = PenetrationDepth(m, v); if (thickness >= full) return 0f;
        return v * Mathf.Sqrt(1f - thickness / full);
    }
    public const int   MagazineSize = 30;
    public const float SecondsBetweenShots = 60f / 550f; // SAR 21 cyclic 450-650 rpm; 550 used (middle of the range)
    public const float Gravity = 9.80665f, AirDensity = 1.225f, SpeedOfSound = 340.294f; // ICAO standard sea level, 15 C
    const float BC_SI = BallisticCoefficientG7 * 703.0696f; // kg/m^2
    // Standard G7 drag function (Mach, Cd) — BRL/Ingalls table as shipped in py_ballisticcalc
    static readonly float[] Mach = { 0.00f,0.05f,0.10f,0.15f,0.20f,0.25f,0.30f,0.35f,0.40f,0.45f,0.50f,0.55f,0.60f,0.65f,0.70f,0.72f,0.75f,0.78f,0.80f,0.82f,0.85f,0.88f,0.90f,0.93f,0.95f,0.97f,1.00f,1.02f,1.05f,1.07f,1.10f,1.12f,1.15f,1.20f,1.25f,1.30f,1.35f,1.40f,1.50f,1.55f,1.60f,1.65f,1.70f,1.75f,1.80f,1.85f,1.90f,1.95f,2.00f,2.05f,2.10f,2.15f,2.20f,2.25f,2.30f,2.35f,2.40f,2.45f,2.50f,2.55f,2.60f,2.65f,2.70f,2.75f,2.80f,2.85f,2.90f,2.95f,3.00f,3.10f,3.20f,3.30f,3.40f,3.50f,3.60f,3.70f,3.80f,3.90f,4.00f,4.20f,4.40f,4.60f,4.80f,5.00f };
    static readonly float[] Cd   = { 0.1198f,0.1197f,0.1196f,0.1194f,0.1193f,0.1194f,0.1194f,0.1194f,0.1193f,0.1193f,0.1194f,0.1193f,0.1194f,0.1197f,0.1202f,0.1207f,0.1215f,0.1226f,0.1242f,0.1266f,0.1306f,0.1368f,0.1464f,0.1660f,0.2054f,0.2993f,0.3803f,0.4015f,0.4043f,0.4034f,0.4014f,0.3987f,0.3955f,0.3884f,0.3810f,0.3732f,0.3657f,0.3580f,0.3440f,0.3376f,0.3315f,0.3260f,0.3209f,0.3160f,0.3117f,0.3078f,0.3042f,0.3010f,0.2980f,0.2951f,0.2922f,0.2892f,0.2864f,0.2835f,0.2807f,0.2779f,0.2752f,0.2725f,0.2697f,0.2670f,0.2643f,0.2615f,0.2588f,0.2561f,0.2533f,0.2506f,0.2479f,0.2451f,0.2424f,0.2368f,0.2313f,0.2258f,0.2205f,0.2154f,0.2106f,0.2060f,0.2017f,0.1975f,0.1935f,0.1861f,0.1793f,0.1730f,0.1672f,0.1618f };
    public static float G7Cd(float mach)
    {
        if (mach <= Mach[0]) return Cd[0];
        for (int i = 1; i < Mach.Length; i++)
            if (mach <= Mach[i]) { float t = (mach - Mach[i-1]) / (Mach[i] - Mach[i-1]); return Mathf.Lerp(Cd[i-1], Cd[i], t); }
        return Cd[Cd.Length - 1];
    }
    /// <summary>Drag deceleration (m/s^2) magnitude at speed v through air of density rho (kg/m^3) with speed of sound c (m/s).</summary>
    public static float DragDecel(float v, float rho, float c) => Mathf.PI / 8f * rho * v * v * G7Cd(v / c) / BC_SI;
    public static float DragDecel(float v) => DragDecel(v, AirDensity, SpeedOfSound);
    /// <summary>Advance one bullet by dt through still ICAO standard air.</summary>
    public static void Step(ref Vector3 pos, ref Vector3 vel, float dt) { Step(ref pos, ref vel, dt, AirDensity, SpeedOfSound, Vector3.zero); }
    /// <summary>Advance one bullet by dt. Step <= 1/480 s: speed at 500 m within 1.4 m/s of the reference integration (verified in Python).
    /// Drag acts on the bullet's velocity relative to the air (vel - wind), so a crosswind pushes the bullet sideways and a head/tail wind changes the drag.</summary>
    public static void Step(ref Vector3 pos, ref Vector3 vel, float dt, float rho, float c, Vector3 wind)
    {
        Vector3 rel = vel - wind; float v = rel.magnitude;
        if (v > 1e-3f) vel -= rel / v * (DragDecel(v, rho, c) * dt);
        vel += Vector3.down * (Gravity * dt);
        pos += vel * dt;
    }
}

/// <summary>Meshes of the real 5.56x45 NATO cartridge parts, built from the SAAMI / drawing dimensions in Ammo556 (lathe profiles, metres, long axis +Y, base at y = 0).
/// Dimensions marked ASSUMED are not in the sources: extractor-groove depth/width, neck length, boat-tail length and base diameter, tracer tip length.</summary>
public static class Cartridge
{
    static Mesh Lathe(System.Collections.Generic.List<Vector2> pr, int seg, int splitRing, float yShift)
    {
        int rings = pr.Count; var vs = new Vector3[rings * seg]; var tris = new System.Collections.Generic.List<int>(); var tris2 = new System.Collections.Generic.List<int>();
        for (int k = 0; k < rings; k++) for (int i = 0; i < seg; i++) { float a = i * Mathf.PI * 2f / seg; vs[k * seg + i] = new Vector3(Mathf.Cos(a) * pr[k].y, pr[k].x + yShift, Mathf.Sin(a) * pr[k].y); }
        for (int k = 0; k < rings - 1; k++) for (int i = 0; i < seg; i++)
        {
            int j = (i + 1) % seg, a = k * seg + i, b = (k + 1) * seg + i, c = k * seg + j, d = (k + 1) * seg + j;
            var t = (splitRing >= 0 && k >= splitRing) ? tris2 : tris; t.Add(a); t.Add(b); t.Add(c); t.Add(c); t.Add(b); t.Add(d);
        }
        var m = new Mesh { name = "cartridge" }; m.vertices = vs;
        if (tris2.Count > 0) { m.subMeshCount = 2; m.SetTriangles(tris, 0); m.SetTriangles(tris2, 1); } else m.triangles = tris.ToArray();
        m.RecalculateNormals(); m.RecalculateBounds(); return m;
    }
    static Mesh caseMesh, bulletMesh;
    /// <summary>The empty brass case, centred on its middle. Rim 9.60 x 1.14 mm, groove, body 9.548 mm tapering to 9.103 mm, 23 deg shoulder, 6.43 mm neck, open mouth, 44.70 mm long.</summary>
    public static Mesh CaseMesh()
    {
        if (caseMesh != null) return caseMesh;
        float L = Ammo556.CaseLength, rim = Ammo556.RimDiameter * 0.5f, head = Ammo556.BodyDiameterHead * 0.5f, sh = Ammo556.BodyDiameterShoulder * 0.5f, neck = Ammo556.NeckDiameter * 0.5f;
        float neckLen = 0.0055f /*ASSUMED*/, shLen = (sh - neck) / Mathf.Tan(Ammo556.ShoulderAngleDeg * Mathf.Deg2Rad), shStart = L - neckLen - shLen, wall = 0.00035f /*ASSUMED*/;
        var p = new System.Collections.Generic.List<Vector2> {
            new Vector2(0f, 0f), new Vector2(0f, rim * 0.96f), new Vector2(0.0001f, rim), new Vector2(Ammo556.RimThickness, rim), new Vector2(Ammo556.RimThickness, 0.0040f /*groove, ASSUMED*/),
            new Vector2(0.0021f /*ASSUMED*/, 0.0040f), new Vector2(0.0021f, head), new Vector2(shStart, sh), new Vector2(shStart + shLen, neck), new Vector2(L, neck),
            new Vector2(L, neck - wall), new Vector2(L - 0.008f, neck - wall), new Vector2(L - 0.008f, 0f) };
        return caseMesh = Lathe(p, 20, -1, -L * 0.5f);
    }
    /// <summary>The 62 gr SS109/M855-type projectile, 5.70 mm x 23.04 mm: boat tail, cylinder, tangent ogive. Submesh 0 is the copper jacket, submesh 1 the tip (dark red on an M856 tracer). Centred on its middle.</summary>
    public static Mesh BulletMesh()
    {
        if (bulletMesh != null) return bulletMesh;
        float L = Ammo556.BulletLength, R = Ammo556.BulletDiameter * 0.5f, bt = 0.0025f /*ASSUMED boat-tail length*/, rb = R * 0.74f /*ASSUMED base*/, og = 0.0120f /*ASSUMED ogive length*/;
        float rho = (R * R + og * og) / (2f * R), cyl = L - og;
        var p = new System.Collections.Generic.List<Vector2> { new Vector2(0f, 0f), new Vector2(0f, rb), new Vector2(bt, R), new Vector2(cyl, R) };
        int n = 10, split = 0;
        for (int i = 1; i <= n; i++) { float x = og * i / n; float r = Mathf.Sqrt(Mathf.Max(0f, rho * rho - x * x)) + R - rho; p.Add(new Vector2(cyl + x, Mathf.Max(0f, r))); if (x < og - 0.0045f /*ASSUMED tip paint length*/) split = p.Count - 1; }
        return bulletMesh = Lathe(p, 16, split, -L * 0.5f);
    }
}
