using System.Collections.Generic;
using UnityEngine;

public enum FireMode { Single, Semi, Auto }

// What a collider is made of, for bullets. thickness > 0 overrides the real thickness along the bullet path (use for thin walls and panels).
// Colliders without this component count as 30 MPa concrete (the whole city), or 2 mm of mild steel if they belong to a rigidbody (cars).
public class BulletSurface : MonoBehaviour
{
    public Ammo556.Mat material = Ammo556.Mat.Concrete;
    public float thickness;
}

// Real-ballistics projectiles for 5.56x45 NATO (SS109/M855-type): G7 drag (in the current air: density and sound speed from the temperature, wind relative) + gravity, stepped every 1/480 s (~2 m at muzzle speed),
// each step ray-cast against the world. Walls are penetrated using Ammo556.PenetrationDepth / ExitSpeed (ESTIMATED, see ledger).
public class BulletSim : MonoBehaviour
{
    class Bullet { public Vector3 pos, vel, start; public float age; public int pens; public LineRenderer lr; }
    static BulletSim inst;
    static BulletSim I { get { if (inst == null) inst = new GameObject("BulletSim").AddComponent<BulletSim>(); return inst; } }
    readonly List<Bullet> live = new List<Bullet>();
    readonly Queue<GameObject> decals = new Queue<GameObject>();
    const float SubDt = 1f / 480f, MaxAge = 12f, ZeroRange = 100f;   // zero range is a design choice, not a published value
    const int MaxDecals = 250, MaxPens = 6;
    static float zeroRad = -1f;
    static Material tracerMat;
    // Tracers: a bright red-orange streak (like a real M856 tracer) behind every round, so the shot can be followed. Its width grows with the distance from the eye (about 0.18% of it)
    // so it stays visible at range, and it only lights once it is 6 m from the muzzle.
    static Material TracerMat()
    {
        if (tracerMat != null) return tracerMat;
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit"); if (sh == null) sh = Shader.Find("Sprites/Default");
        tracerMat = new Material(sh); Color c = new Color(1f, 0.36f, 0.10f) * 4f; c.a = 1f;
        tracerMat.color = c; if (tracerMat.HasProperty("_BaseColor")) tracerMat.SetColor("_BaseColor", c);
        return tracerMat;
    }
    static LineRenderer NewTracer()
    {
        var go = new GameObject("tracer"); var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2; lr.useWorldSpace = true; lr.sharedMaterial = TracerMat(); lr.alignment = LineAlignment.View; lr.numCapVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false; lr.enabled = false;
        return lr;
    }

    /// <summary>Launch angle (rad) above the line of sight, sighted in still ICAO standard air (15 C), so the bullet crosses it again at ZeroRange (sight-bore offset ignored).</summary>
    static float ZeroAngle()
    {
        if (zeroRad >= 0f) return zeroRad;
        float lo = 0f, hi = 0.05f;
        for (int k = 0; k < 40; k++)
        {
            float mid = 0.5f * (lo + hi);
            Vector3 p = Vector3.zero, v = new Vector3(Mathf.Cos(mid), Mathf.Sin(mid), 0f) * Ammo556.MuzzleVelocity;
            while (p.x < ZeroRange) Ammo556.Step(ref p, ref v, SubDt);
            if (p.y > 0f) hi = mid; else lo = mid;   // still above the line at the zero range -> angle too big
        }
        return zeroRad = 0.5f * (lo + hi);
    }

    /// <summary>Fire one round along the camera's line of sight.</summary>
    public static void Fire(Transform cam) { Fire(cam, Quaternion.identity); }
    /// <summary>dev: extra rotation of the shot (a hot barrel throws rounds off the line of sight).</summary>
    public static void Fire(Transform cam, Quaternion dev)
    {
        Vector3 dir = dev * (Quaternion.AngleAxis(-ZeroAngle() * Mathf.Rad2Deg, cam.right) * cam.forward);
        Vector3 p0 = cam.position + cam.forward * 0.2f;
        I.live.Add(new Bullet { pos = p0, start = p0, vel = dir.normalized * Ammo556.MuzzleVelocity, lr = NewTracer() });
    }

    void Update()
    {
        float remaining = Mathf.Min(Time.deltaTime, 0.05f);
        for (int i = live.Count - 1; i >= 0; i--)
        {
            var b = live[i]; bool dead = false;
            float t = remaining;
            while (t > 0f && !dead)
            {
                float dt = Mathf.Min(SubDt, t); t -= dt;
                Vector3 p = b.pos, v = b.vel;
                Ammo556.Step(ref p, ref v, dt, Atmosphere.AirDensity, Atmosphere.SpeedOfSound, Atmosphere.WindVec);   // the air as it is now: temperature, density and wind
                Vector3 seg = p - b.pos; float len = seg.magnitude;
                if (len > 1e-5f && Physics.Raycast(b.pos, seg / len, out var hit, len, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    dead = Impact(b, hit, v.magnitude);
                else { b.pos = p; b.vel = v; }
                b.age += dt;
                if (b.age > MaxAge || b.pos.y < -20f) dead = true;
            }
            if (b.lr != null)
            {
                var cm = Camera.main; float far = cm != null ? Vector3.Distance(cm.transform.position, b.pos) : 50f, flown = Vector3.Distance(b.start, b.pos);
                float L = Mathf.Min(b.vel.magnitude * 0.010f, flown);
                b.lr.SetPosition(0, b.pos - b.vel.normalized * L); b.lr.SetPosition(1, b.pos);
                b.lr.widthMultiplier = Mathf.Clamp(far * 0.0018f, 0.004f, 2f);
                b.lr.enabled = !dead && flown > 6f;
            }
            if (dead) { if (b.lr != null) Destroy(b.lr.gameObject); live[i] = live[live.Count - 1]; live.RemoveAt(live.Count - 1); }
        }
    }

    // returns true when the bullet is stopped
    bool Impact(Bullet b, RaycastHit hit, float speed)
    {
        Vector3 dir = (b.vel).normalized;
        var surf = hit.collider.GetComponentInParent<BulletSurface>();
        Ammo556.Mat mat = surf != null ? surf.material : hit.rigidbody != null ? Ammo556.Mat.MildSteel : Ammo556.Mat.Concrete;
        float fixedThick = surf != null ? surf.thickness : hit.rigidbody != null ? 0.002f : 0f;
        float depth = Ammo556.PenetrationDepth(mat, speed);
        float thick = float.PositiveInfinity; Vector3 exit = hit.point;
        if (fixedThick > 0f) { thick = fixedThick; exit = hit.point + dir * fixedThick; }
        else
        {   // real thickness: ray back from just past the maximum depth to find the far face of this collider
            Vector3 far = hit.point + dir * (depth + 0.002f);
            if (hit.collider.Raycast(new Ray(far, -dir), out var back, depth + 0.002f)) { thick = Vector3.Distance(hit.point, back.point); exit = back.point; }
        }
        Decal(hit.point, hit.normal);
        if (hit.rigidbody != null)
        {
            float vOut = thick < depth ? Ammo556.ExitSpeed(mat, speed, thick) : 0f;
            hit.rigidbody.AddForceAtPosition(dir * (Ammo556.BulletMass * (speed - vOut)), hit.point, ForceMode.Impulse);
        }
        if (thick >= depth || ++b.pens > MaxPens) return true;   // stopped inside the target
        float vExit = Ammo556.ExitSpeed(mat, speed, thick);
        if (vExit < 20f) return true;
        b.pos = exit + dir * 0.002f; b.vel = dir * vExit;
        Decal(exit, -hit.normal);
        return false;
    }

    void Decal(Vector3 p, Vector3 n)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(g.GetComponent<Collider>());
        g.transform.position = p + n * 0.003f; g.transform.rotation = Quaternion.LookRotation(-n); g.transform.localScale = Vector3.one * 0.03f;
        g.GetComponent<Renderer>().sharedMaterial = CityBuilder.Mat(new Color(0.05f, 0.05f, 0.05f));
        decals.Enqueue(g);
        if (decals.Count > MaxDecals) Destroy(decals.Dequeue());
    }
}
