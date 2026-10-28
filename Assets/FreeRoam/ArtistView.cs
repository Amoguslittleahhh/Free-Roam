using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using GLTFast;

// The artist's complete first-person weapons (models, arms/hands and all animations) from "Molten Aim 25"
// by goblimos (MIT licence, https://github.com/goblimos/Molten-Aim-25). Weapon models are from Sketchfab.
// The .glb files live in Resources/MoltenAim/*.bytes and are loaded at runtime with glTFast; every animation
// is one long timeline, sliced here by frame range (30 fps) exactly as the original Godot project does.
public class ArtistView : MonoBehaviour
{
    public struct Rng
    {
        public int a, b; public bool loop;
        public Rng(int a, int b, bool loop = false) { this.a = a; this.b = b; this.loop = loop; }
        public bool Has => b > a;
    }
    public class Def
    {
        public string file, name;
        public Vector3 hip, ads;        // camera space (x right, y up, z forward)
        public float adsSpeed = 1.2f;   // metres per second towards the sight picture
        public int cap; public bool auto, melee, sar, ballistic; public float kg;   // kg > 0: the rifle follows the player's movement through the carry model (StepHeavy)
        public SightType sight = SightType.IronSights; public float mag = 1f;
        public float damage = 30f, range = 100f, recoil = 0.05f;
        public Rng take, idle, fire, reload, reloadFull, hide, bash;
    }

    // slot order = weapon wheel order
    public static readonly Def[] Defs =
    {
        new Def { file = "pistol", name = "C19 Pistol",  hip = new Vector3(0.1f, -0.4f, 0.3f), ads = new Vector3(0, -0.25f, 0.2f),  adsSpeed = 2f,   cap = 19,  auto = false, damage = 20,  range = 50,  recoil = 0.08f,
            take = new Rng(142,177), idle = new Rng(179,204,true), fire = new Rng(225,234), reload = new Rng(65,130), hide = new Rng(131,142), bash = new Rng(205,224) },
        new Def { file = "smg",    name = "SMG",         hip = new Vector3(0.1f, -0.4f, 0.2f), ads = new Vector3(0, -0.31f, 0.1f),  adsSpeed = 1.2f, cap = 25,  auto = true,  damage = 30,  range = 50,  recoil = 0.03f,
            take = new Rng(141,176), idle = new Rng(186,215,true), fire = new Rng(0,6), reload = new Rng(8,81), reloadFull = new Rng(81,129), hide = new Rng(130,140), bash = new Rng(215,234) },
        new Def { file = "ak",     name = "AK",          hip = new Vector3(0.1f, -0.4f, 0.3f), ads = new Vector3(0, -0.285f, 0f),   adsSpeed = 1.2f, cap = 30,  auto = true,  damage = 45,  range = 100, recoil = 0.05f,
            take = new Rng(164,198), idle = new Rng(199,221,true), fire = new Rng(0,7), reload = new Rng(8,97), reloadFull = new Rng(98,152), hide = new Rng(153,163), bash = new Rng(222,243) },
        new Def { file = "sawn",   name = "Sawn-off",    hip = new Vector3(0.1f, -0.3f, 0.3f), ads = new Vector3(0, -0.2f, 0.15f),  adsSpeed = 2f,   cap = 2,   auto = false, damage = 100, range = 10,  recoil = 0.15f,
            take = new Rng(205,219), idle = new Rng(220,250,true), fire = new Rng(0,13), reload = new Rng(106,193), reloadFull = new Rng(14,105), hide = new Rng(194,205), bash = new Rng(251,270) },
        new Def { file = "lmg",    name = "LMG",         hip = new Vector3(0.1f, -0.4f, 0.2f), ads = new Vector3(0, -0.35f, 0.15f), adsSpeed = 0.3f, cap = 100, auto = true,  damage = 40,  range = 80,  recoil = 0.1f,
            take = new Rng(196,217), idle = new Rng(218,250,true), fire = new Rng(0,10), reload = new Rng(11,183), hide = new Rng(184,195), bash = new Rng(251,275) },
        new Def { file = "knife",  name = "Knife",       hip = new Vector3(0f, -0.3f, 0.3f),   ads = new Vector3(0f, -0.3f, 0.3f),  adsSpeed = 1f,   cap = 0,   auto = false, melee = true, damage = 100, range = 1.6f, recoil = 0f,
            take = new Rng(135,145), idle = new Rng(0,40,true), fire = new Rng(40,60), hide = new Rng(125,135), bash = new Rng(103,124) },
        // SAR-21: the artist's AK hands and animations, with the SAR-21 mesh fitted onto them (see FitSar). Real 5.56x45 ballistics.
        new Def { file = "ak",     name = "SAR-21",      hip = new Vector3(0.1f, -0.48f, 0.65f), ads = new Vector3(0, -0.285f, 0f),   adsSpeed = 1.2f, cap = 30,  auto = true,  damage = 45,  range = 100, recoil = 0.04f, sar = true, ballistic = true, kg = 4.5f, sight = SightType.Integral, mag = 1.5f,
            take = new Rng(164,198), idle = new Rng(199,221,true), fire = new Rng(0,7), reload = new Rng(8,97), reloadFull = new Rng(98,152), hide = new Rng(153,163), bash = new Rng(222,243) },
    };

    enum St { Take, Idle, Fire, Reload, Bash }

    // B cycles the selector. Single: one round per pull with a lock-out (design value). Semi-auto: one round per pull. Auto: fires while held.
    static readonly FireMode[][] Modes =
    {
        new[] { FireMode.Single, FireMode.Semi },                     // pistol
        new[] { FireMode.Single, FireMode.Semi, FireMode.Auto },      // SMG
        new[] { FireMode.Single, FireMode.Semi, FireMode.Auto },      // AK
        new[] { FireMode.Single },                                    // sawn-off
        new[] { FireMode.Auto },                                      // LMG (belt-fed, full-auto only)
        new[] { FireMode.Single },                                    // knife
        new[] { FireMode.Single, FireMode.Semi, FireMode.Auto },      // SAR-21
    };
    const float SingleDelay = 0.30f;
    const float FrontSightShift = 0f;   // the CAD's knob/post on top of the tube stays where the CAD has it (it is the elevation knob in the photo, not the front sight)
    // SAR-21 cyclic rate: 450-650 rounds/min (ST Kinetics / Wikipedia); the middle of the published range, 550 rpm
    const float SarShotInterval = 60f / 550f; float nextShotT;
    readonly int[] modeIdx = new int[Defs.Length];
    float nextSingle;
    FireMode Mode => Modes[cur][modeIdx[cur]];

    // ---- barrel heat ---------------------------------------------------------------------------------------------------------------------------------------
    // Only the real-ballistics rifle heats up. Physics, calibration and sources: see BarrelThermal. The shot is thrown off by what the measured hot-barrel test showed:
    // the group opens up and the point of impact walks down and to the left, both in proportion to the temperature rise of the hot section.
    readonly BarrelThermal barrel = new BarrelThermal { T = Atmosphere.AirTempC };
    const float GlowC = 525f, TopC = 800f;   // a steel barrel starts to glow dull red at about 525 C (Draper point); the gauge ends at 800 C (burst point 738 C)
    float lastShotT = -10f, crossGap = 8f; Transform sarBasePivot, icePack; float iceT, iceAnimT = -1f; bool iceApplying, iceApplied; Vector3 iceRest;
    float Rise => (float)BarrelThermal.Rise(barrel.T, Atmosphere.AirTempC);
    float GroupMoa => Rise * (float)BarrelThermal.GrowthMoaPerK;
    float Gauss() { float u = Mathf.Max(1e-6f, UnityEngine.Random.value), v = UnityEngine.Random.value; return Mathf.Sqrt(-2f * Mathf.Log(u)) * Mathf.Cos(6.2831853f * v); }
    readonly System.Collections.Generic.List<Material> hotM = new System.Collections.Generic.List<Material>(); readonly System.Collections.Generic.List<Color> hotBase = new System.Collections.Generic.List<Color>();
    // Barrel colour from its temperature (a black-body ramp): nothing below the Draper point, dull red at ~525 C, red-orange ~700 C, orange ~900 C, yellow-white ~1100 C
    static readonly float[] GlowT = { 525f, 700f, 900f, 1100f };
    static readonly Color[] GlowCol = { new Color(0.35f, 0.02f, 0f), new Color(1f, 0.22f, 0.02f), new Color(1f, 0.5f, 0.1f), new Color(1f, 0.8f, 0.4f) };
    static Color BlackBody(float T)
    {
        if (T <= GlowT[0]) return Color.black;
        for (int k = 1; k < GlowT.Length; k++) if (T < GlowT[k]) return Color.Lerp(GlowCol[k - 1], GlowCol[k], (T - GlowT[k - 1]) / (GlowT[k] - GlowT[k - 1]));
        return GlowCol[GlowT.Length - 1];
    }
    void ApplyBarrelGlow()
    {
        if (hotM.Count == 0) return;
        float T = (float)barrel.T; Color em = BlackBody(T) * Mathf.Clamp01((T - 525f) / 150f) * 0.9f;   // kept below 1: no over-bright bloom
        for (int i = 0; i < hotM.Count; i++) { var m = hotM[i]; if (m != null) m.SetColor("_EmissionColor", em); }
        if (smoke != null)
        {   // oil and residue smoke from the barrel: starts at about 150 C and thickens with temperature
            var emis = smoke.emission; emis.rateOverTime = aim.AimAmount > 0.3f ? 0f : Mathf.Clamp01((T - 150f) / 450f) * 14f;
        }
    }
    ParticleSystem smoke;
    readonly System.Collections.Generic.List<Renderer> adsHide = new System.Collections.Generic.List<Renderer>(); bool adsHidden;
    static bool InScope(Vector3 v) { return Mathf.Sqrt(v.x * v.x + (v.y - ScopeY) * (v.y - ScopeY)) < 0.0105f && v.z > ScopeRearZ && v.z < ScopeFrontZ; }
    void BuildSmoke(Vector3 localPos)
    {
        var go = new GameObject("barrel_smoke"); go.transform.SetParent(holder, false); go.transform.localPosition = localPos;
        smoke = go.AddComponent<ParticleSystem>();
        var main = smoke.main; main.simulationSpace = ParticleSystemSimulationSpace.World; main.startLifetime = 1.8f; main.startSpeed = 0.03f; main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.045f);
        main.startColor = new Color(0.6f, 0.6f, 0.6f, 0.10f); main.maxParticles = 80; main.loop = true;
        var emis = smoke.emission; emis.rateOverTime = 0f;
        var sh = smoke.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.006f;
        var vel = smoke.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World; vel.y = 0.12f;
        var sz = smoke.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2.6f));
        var col = smoke.colorOverLifetime; col.enabled = true; var gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
        var r = go.GetComponent<ParticleSystemRenderer>(); var sd = Shader.Find("Sprites/Default");
        if (sd != null)
        {
            var t = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) { float rr = Mathf.Sqrt((x - 15.5f) * (x - 15.5f) + (y - 15.5f) * (y - 15.5f)) / 15.5f; t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - rr) * Mathf.Clamp01(1f - rr))); }
            t.Apply(); var m = new Material(sd) { mainTexture = t }; r.sharedMaterial = m;
        }
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
    }
    public void Hide()
    {
        d = null; cur = -1; flash.enabled = false;
        for (int k = 0; k < models.Length; k++) if (models[k] != null) models[k].SetActive(false);
    }

    Camera cam; Transform holder; WeaponAim aim;
    readonly GameObject[] models = new GameObject[Defs.Length];
    readonly GameObject[] animRoots = new GameObject[Defs.Length];
    readonly AnimationClip[] clips = new AnimationClip[Defs.Length];
    readonly bool[] loading = new bool[Defs.Length];
    readonly int[] ammo = new int[Defs.Length];
    int cur = -1; Def d; St st; Rng play; float frame; bool reloadFull;
    float kick; Light flash; float flashT; GUIStyle hud, small, heatTxt;
    public bool CanAds => st == St.Idle || st == St.Fire;
    public bool Busy => st == St.Reload;

    public void Init(Camera c, Transform h, WeaponAim w)
    {
        cam = c; holder = h; aim = w;
        for (int i = 0; i < Defs.Length; i++) { ammo[i] = Defs[i].cap; modeIdx[i] = Modes[i].Length - 1; }
        var fl = new GameObject("MuzzleFlash"); fl.transform.SetParent(c.transform, false); fl.transform.localPosition = new Vector3(0.12f, -0.08f, 1.1f);
        flash = fl.AddComponent<Light>(); flash.type = LightType.Point; flash.color = new Color(1f, 0.82f, 0.55f); flash.range = 6f; flash.intensity = 1.1f; flash.enabled = false;
    }

    public Vector3 Pos(float e) { StepHeavy(e); return Vector3.Lerp(d.hip, d.ads, e) + new Vector3(0, 0.004f * kick, -0.02f * kick) + rlPos + (sarReload ? RotPivot - Rot * RotPivot : Vector3.zero) + hvPos; }   // the rifle turns about its magazine well, not about the holder origin
    public float Kick => kick * d.recoil * 28f;   // degrees of view kick
    // SAR-21 reload: the gun is canted to the left so the magazine being changed is in view
    static readonly Vector3 RotPivot = new Vector3(0f, 0.1f, -0.12f); bool sarReload; Transform sarMagB; float tiltB; const float ChargeRollDeg = -30f;
    static readonly Vector3 Pouch = new Vector3(-0.15f, -0.85f, -0.15f);   // where the fresh magazine comes from (the belt/pouch below the view), in camera space relative to the magazine well
    float tilt; Vector3 rlPos; Quaternion rlRot = Quaternion.identity;   // SAR-21 reload: the rifle is lifted and turned a little so the magazine is in view, plus small knocks
    const float ReloadTiltDeg = -65f;   // negative = top of the rifle toward the right (underside, with the magazine, turned toward the left hand)
    public Quaternion Rot => Quaternion.Euler(0f, 0f, ReloadTiltDeg * Mathf.SmoothStep(0f, 1f, tilt) + ChargeRollDeg * Mathf.SmoothStep(0f, 1f, tiltB) - sarRoll * sarCancel) * rlRot * hvRot;


    // ---- carry model for a heavy rifle -------------------------------------------------------------------------------------------------------------------------
    // The rifle (mass d.kg) hangs from the hands, which are modelled as a spring and damper with fixed muscle stiffness, so a heavier rifle has a lower natural
    // frequency (w = sqrt(k / m)) and lags further behind the body:   x'' = -2*zeta*w*x' - w^2*x - a_hands
    // a_hands is the camera's own acceleration (starting, stopping, jumping, landing) plus the body's footstep motion (vertical, side to side and fore-aft at the step
    // frequency, and a heel-strike jolt). The same spring runs on the rifle's pitch, yaw and roll, driven by the camera's turning acceleration and by the sideways
    // and fore-aft acceleration. At rest every term decays to zero, so the rifle is still when the player stands still.
    const float SprintPitch = 12f, SprintYaw = -30f, SprintRoll = 8f; static readonly Vector3 SprintOffset = new Vector3(-0.06f, 0.03f, 0.08f);   // sling-carry across the chest: muzzle down and to the left, stock tucked in
    const float ArmK = 900f, ArmZeta = 0.42f, RotW = 0.85f, PitchGain = 50f, RollGain = 50f;
    Vector3 hvP, hvV, hvA, hvAV, hvVel, hvAcc, hvPos, prevCamPos; Quaternion hvRot = Quaternion.identity;
    float prevYaw, prevPitch, yawRate, pitchRate, stepPh, moveW, sprintW; bool hvInit; int hvFrame = -1; CharacterController hvCc;
    void StepHeavy(float e)
    {
        if (d == null || d.kg <= 0f || cam == null) { hvPos = Vector3.zero; hvRot = Quaternion.identity; return; }
        if (hvFrame == Time.frameCount) return; hvFrame = Time.frameCount;
        float dt = Mathf.Min(Time.deltaTime, 0.05f); if (dt <= 1e-5f) return;
        var ct = cam.transform;
        if (!hvInit)
        {
            prevCamPos = ct.position; prevYaw = ct.eulerAngles.y; prevPitch = ct.eulerAngles.x; hvVel = hvAcc = Vector3.zero; yawRate = pitchRate = 0f; hvInit = true;
            hvCc = cam.GetComponentInParent<CharacterController>(); return;
        }
        // the player's motion, from the camera itself: velocity, acceleration (smoothed) and turning rates
        Vector3 raw = (ct.position - prevCamPos) / dt; prevCamPos = ct.position;
        if (raw.magnitude > 40f) raw = hvVel;   // a teleport or respawn is not a movement
        Vector3 vNew = Vector3.Lerp(hvVel, raw, 1f - Mathf.Exp(-dt / 0.04f));
        Vector3 aW = Vector3.ClampMagnitude((vNew - hvVel) / dt, 40f); hvVel = vNew;
        hvAcc = Vector3.Lerp(hvAcc, aW, 1f - Mathf.Exp(-dt / 0.05f));
        Vector3 aL = ct.InverseTransformDirection(hvAcc);
        float yaw = ct.eulerAngles.y, pit = ct.eulerAngles.x;
        float yr = Mathf.Clamp(Mathf.DeltaAngle(prevYaw, yaw) / dt, -1500f, 1500f), pr = Mathf.Clamp(Mathf.DeltaAngle(prevPitch, pit) / dt, -1500f, 1500f); prevYaw = yaw; prevPitch = pit;
        float k3 = 1f - Mathf.Exp(-dt / 0.03f);
        float prevYr = yawRate, prevPr = pitchRate;
        yawRate = Mathf.Lerp(yawRate, yr, k3); pitchRate = Mathf.Lerp(pitchRate, pr, k3);
        float aYaw = Mathf.Clamp((yawRate - prevYr) / dt, -4000f, 4000f), aPit = Mathf.Clamp((pitchRate - prevPr) / dt, -4000f, 4000f);
        // walking: step frequency follows speed, only on the ground
        float sp = new Vector3(hvVel.x, 0f, hvVel.z).magnitude;
        bool firing = Time.time - lastShotT < 0.4f || st == St.Fire;   // shooting on the run: no sprint carry, just the walking animation (hands at walking pace)
        float spLim = firing ? Mathf.Min(sp, 4.5f) : sp;
        bool grounded = hvCc != null ? hvCc.isGrounded : Mathf.Abs(hvVel.y) < 1.5f;
        moveW = Mathf.MoveTowards(moveW, (grounded && sp > 0.6f) ? 1f : 0f, dt / 0.15f);
        float f = Mathf.Clamp(0.5f * spLim, 1.2f, 3.6f);   // steps per second
        float spd = Mathf.InverseLerp(0.6f, 7.5f, spLim);
        float ab = Mathf.Lerp(0.008f, 0.030f, spd) * moveW, al = Mathf.Lerp(0.008f, 0.02f, spd) * moveW;   // body bounce and sway amplitudes (metres)
        float prevPh = stepPh; if (moveW > 0.01f) stepPh += f * dt;
        if (Mathf.Floor(stepPh) != Mathf.Floor(prevPh)) hvV.y -= 0.18f * Mathf.Clamp(spLim / 4.5f, 0.3f, 1.6f) * moveW;   // heel strike
        float w = Mathf.Sqrt(ArmK / d.kg), c = 2f * ArmZeta * w, wr = w * RotW, cr = 2f * ArmZeta * wr;
        float W2 = 2f * Mathf.PI * f;
        int n = Mathf.Max(1, Mathf.CeilToInt(dt / (1f / 240f))); float h = dt / n;
        for (int i = 0; i < n; i++)
        {
            float ph = stepPh + f * h * i;
            Vector3 aStep = new Vector3(-al * (W2 * 0.5f) * (W2 * 0.5f) * Mathf.Sin(Mathf.PI * ph),
                                        ab * W2 * W2 * Mathf.Cos(2f * Mathf.PI * ph),
                                        0.25f * ab * W2 * W2 * Mathf.Sin(2f * Mathf.PI * ph));
            Vector3 aT = aL * 0.6f + aStep;   // the arms brace against the player's own acceleration
            hvV += (-c * hvV - w * w * hvP - aT) * h; hvP += hvV * h;
            Vector3 drive = new Vector3(-aPit - PitchGain * aT.z, -aYaw, RollGain * aT.x);   // deg/s^2 on pitch (x), yaw (y), roll (z)
            hvAV += (-cr * hvAV - wr * wr * hvA + drive) * h; hvA += hvAV * h;
        }
        hvP = new Vector3(Mathf.Clamp(hvP.x, -0.08f, 0.08f), Mathf.Clamp(hvP.y, -0.08f, 0.08f), Mathf.Clamp(hvP.z, -0.10f, 0.10f));
        hvA = new Vector3(Mathf.Clamp(hvA.x, -12f, 12f), Mathf.Clamp(hvA.y, -12f, 12f), Mathf.Clamp(hvA.z, -12f, 12f));
        float g = (1f - 0.85f * e) * (st == St.Reload ? 0.35f : 1f);   // aiming steadies it, and the reload has its own choreography
        // sprint carry: like a bullpup carried on a sling across the chest (reference photo from the user): the rifle lies diagonally against the body, stock tucked in
        // at the right shoulder, muzzle down and across to the left, grip hand on the pistol grip and support hand on the forearm. Rotated about the grip hand.
        bool wantSprint = grounded && sp > 6f && e < 0.05f && !firing && st != St.Reload && st != St.Bash;
        sprintW = Mathf.MoveTowards(sprintW, wantSprint ? 1f : 0f, dt / 0.25f);
        float sw = sprintW * sprintW * (3f - 2f * sprintW);
        Quaternion rs = Quaternion.Euler(SprintPitch * sw, SprintYaw * sw, SprintRoll * sw);
        Vector3 gripPivot = SarWristR * sarS;   // rotate about the grip hand so it stays on the grip
        hvPos = hvP * g + gripPivot - rs * gripPivot + SprintOffset * sw; hvRot = rs * Quaternion.Euler(hvA * g);
    }

    public void Equip(int i, WeaponSpec spec)
    {
        cur = i; d = Defs[i]; tilt = 0f; hvInit = false; hvP = hvV = hvA = hvAV = Vector3.zero; hvPos = Vector3.zero; hvRot = Quaternion.identity;
        spec.name = d.name; spec.sight = d.sight; spec.magnification = d.mag; spec.swayDeg = d.sar ? 0f : 0.15f;
        spec.adsTime = Mathf.Clamp(Vector3.Distance(d.hip, d.ads) / d.adsSpeed, 0.15f, 0.8f);
        spec.hipPos = d.hip; spec.adsPos = d.ads;
        for (int k = 0; k < models.Length; k++) if (models[k] != null) models[k].SetActive(k == i);
        if (models[i] == null && !loading[i]) _ = LoadAsync(i);
        Begin(St.Take, d.take);
    }

    void Begin(St s, Rng r) { st = s; play = r; frame = r.a; Sample(); }

    async Task LoadAsync(int i)
    {
        loading[i] = true;
        try
        {
            var def = Defs[i];
            var ta = Resources.Load<TextAsset>("MoltenAim/" + def.file);
            if (ta == null) { Debug.LogError("Missing MoltenAim/" + def.file); return; }
            var g = new GltfImport();
            if (!await g.Load(ta.bytes)) { Debug.LogError("glTF load failed: " + def.file); return; }
            var root = new GameObject("artist_" + def.file);
            root.transform.SetParent(holder, false); root.transform.localScale = Vector3.one * 0.015f;
            root.SetActive(i == cur);
            if (!await g.InstantiateMainSceneAsync(root.transform)) { Debug.LogError("glTF instantiate failed: " + def.file); return; }
            var anim = root.GetComponentInChildren<Animation>(true);
            AnimationClip clip = null; GameObject target = null;
            if (anim != null)
            {
                foreach (AnimationState s in anim) { clip = s.clip; break; }
                target = anim.gameObject; anim.playAutomatically = false; anim.Stop(); anim.enabled = false;
            }
            if (clip == null) { var all = g.GetAnimationClips(); if (all != null && all.Length > 0) clip = all[0]; }
            if (target == null) target = root.transform.childCount > 0 ? root.transform.GetChild(0).gameObject : root;
            foreach (var sm in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) sm.updateWhenOffscreen = true;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            clips[i] = clip; animRoots[i] = target; models[i] = root;
            if (def.sar) FitSar(i, root);
            if (clip == null) Debug.LogError("No animation in " + def.file);
            if (i == cur) Sample();
            // quietly load the others afterwards, one at a time
            for (int k = 0; k < Defs.Length; k++) if (models[k] == null && !loading[k]) { _ = LoadAsync(k); break; }
        }
        catch (Exception ex) { Debug.LogError("ArtistView load " + Defs[i].file + ": " + ex); }
        finally { loading[i] = false; }
    }

    // ---- SAR-21 on the artist's hands ----
    // The artist's AK animation drives hands, bolt and magazine. The AK meshes are hidden and the SAR-21 parts are parented to the matching bones:
    // mag -> mag, magcatch -> release, bolt/charge/charge_side -> bolt, everything else -> base. The SAR-21 is scaled to the AK's scale
    // (AK-74, 943 mm) and placed so the artist's wrists sit where the SAR-21 hold puts them (wrist positions from the solved hold).
    static readonly Vector3 SarWristR = new Vector3(0.04865f, -0.04890f, -0.17306f), SarWristL = new Vector3(-0.06396f, -0.05156f, 0.12336f);
    Transform sarMagPivot, sarLArm, sarLWrist, sarMagNode, sarAk, sarCharge, sarBoltNode, sarBaseNode; Vector3 sarD, sarD2, sarBoltRestInAk; float sarW2; Vector3 sarMagRestInAk; bool sarChargeOn = true; Quaternion sarAkRest = Quaternion.identity; float sarRoll, sarCancel;
    // procedural SAR-21 reload: left-arm bone poses (idle / holding the magazine / holding the charging handle, taken from the artist's clip) and anchor points, all in holder space
    Transform[] lcT; Vector3[] pI, pM, pB; Quaternion[] rI, rM, rB; Vector3 vMag, vBolt, lwIdleH, magRestH, chargeRestH; float sarS = 1f;
    const float MagDrop = 0.20f, ChargePull = 0.06f;   // metres (real size): how far the magazine is taken down, how far the charging handle is pulled back
    //    // sarD: SAR-21 magazine rest position minus the AK magazine's, in holder space

    static Transform FindT(Transform root, string name, bool prefix = false)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (prefix ? t.name.StartsWith(name) : t.name == name) return t;
        return null;
    }

    // ---- the SAR-21's own scope (the tube in the CAD model): open tube, clear glass, a reticle wire in the rear lens plane, and the sight picture taken from the real optic ----
    // The model's tube (axis y = ScopeY, z from ScopeRearZ to ScopeFrontZ, clear aperture radius ScopeR, all in model units) was closed by a solid block; that block is not built.
    // The reticle follows the reference photo: two fine crossing lines out to the edge of the aperture, an open ring and a dot at the centre.
    const float ScopeBore = 0.0107f;
    const float ScopeY = 0.1214f, ScopeR = 0.0107f, ScopeRearZ = -0.1418f, ScopeFrontZ = 0.068f, ScopeOnScreen = 0.30f, EyecupOutR = 0.0160f, EyecupLen = 0.014f, EyecupIn = 0.0117f, EyecupOut = 0.0162f;
    static void Quad(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d2)
    {
        int k = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d2);
        t.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3, k, k + 2, k + 1, k, k + 3, k + 2 });   // both faces: seen from the eye and from the front
    }
    GameObject AddMesh(Transform parent, string name, System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<int> t, Material m)
    {
        var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        {   // every scope piece is drawn from both sides, so no wall (eyecup bore, bands) is see-through when looking out from inside the scope
            int nv = v.Count, nt = t.Count; var vv = new System.Collections.Generic.List<Vector3>(v); vv.AddRange(v); var tt = new System.Collections.Generic.List<int>(t);
            for (int k = 0; k < nt; k += 3) { tt.Add(t[k] + nv); tt.Add(t[k + 2] + nv); tt.Add(t[k + 1] + nv); }
            mesh.SetVertices(vv); mesh.SetTriangles(tt, 0); mesh.RecalculateNormals(); var nm = mesh.normals; for (int k = nv; k < nm.Length; k++) nm[k] = -nm[k]; mesh.normals = nm; mesh.RecalculateBounds();
        }
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; return go;
    }
    // an octagonal ring (vertices on the axes, like the CAD tube) from z0 to z1, inner and outer circumradius; both faces of every quad
    static System.Collections.Generic.List<Vector3> OctRing(float z0, float z1, float ri, float ro, out System.Collections.Generic.List<int> t)
    {
        var v = new System.Collections.Generic.List<Vector3>(); t = new System.Collections.Generic.List<int>();
        for (int k = 0; k < 8; k++)
        {
            float a0 = k * Mathf.PI / 4f, a1 = (k + 1) * Mathf.PI / 4f; Vector3 d0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f), d1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f), o = new Vector3(0f, ScopeY, 0f);
            Quad(v, t, o + d0 * ro + Vector3.forward * z0, o + d1 * ro + Vector3.forward * z0, o + d1 * ro + Vector3.forward * z1, o + d0 * ro + Vector3.forward * z1);   // outer wall
            Quad(v, t, o + d0 * ri + Vector3.forward * z0, o + d1 * ri + Vector3.forward * z0, o + d1 * ri + Vector3.forward * z1, o + d0 * ri + Vector3.forward * z1);   // inner wall
            Quad(v, t, o + d0 * ri + Vector3.forward * z0, o + d1 * ri + Vector3.forward * z0, o + d1 * ro + Vector3.forward * z0, o + d0 * ro + Vector3.forward * z0);   // rear face
            Quad(v, t, o + d0 * ri + Vector3.forward * z1, o + d1 * ri + Vector3.forward * z1, o + d1 * ro + Vector3.forward * z1, o + d0 * ro + Vector3.forward * z1);   // front face
        }
        return v;
    }
    static void Tri(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<int> t, Vector3 a, Vector3 b, Vector3 c) { int k = v.Count; v.Add(a); v.Add(b); v.Add(c); t.Add(k); t.Add(k + 1); t.Add(k + 2); }
    // A prism / frustum along local z (z0..z1, radius r0 at z0 and r1 at z1, "sides" corners, first corner on the x axis), moved by m. Single-sided: only the faces seen from OUTSIDE exist
    // (so the inside of the chassis is open and clear from the eye). Caps are rings when ri > 0, discs otherwise.
    static void Prism(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<int> t, int sides, float z0, float z1, float r0, float r1, Matrix4x4 m, bool capLo, bool capHi, float ri = 0f)
    {
        for (int k = 0; k < sides; k++)
        {
            float a0 = k * 2f * Mathf.PI / sides, a1 = (k + 1) * 2f * Mathf.PI / sides; Vector3 d0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f), d1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);
            Vector3 p0 = m.MultiplyPoint3x4(d0 * r0 + Vector3.forward * z0), p1 = m.MultiplyPoint3x4(d1 * r0 + Vector3.forward * z0), p2 = m.MultiplyPoint3x4(d1 * r1 + Vector3.forward * z1), p3 = m.MultiplyPoint3x4(d0 * r1 + Vector3.forward * z1);
            Tri(v, t, p0, p1, p2); Tri(v, t, p0, p2, p3);   // outer wall, normal pointing away from the axis
            if (ri > 0f)
            {   // inner wall (the bore), drawn from both sides by AddMesh, so looking out of the eyecup the bore closes the view
                Vector3 q0 = m.MultiplyPoint3x4(d0 * ri + Vector3.forward * z0), q1 = m.MultiplyPoint3x4(d1 * ri + Vector3.forward * z0), q2 = m.MultiplyPoint3x4(d1 * ri + Vector3.forward * z1), q3 = m.MultiplyPoint3x4(d0 * ri + Vector3.forward * z1);
                Tri(v, t, q0, q2, q1); Tri(v, t, q0, q3, q2);
            }
            for (int cap = 0; cap < 2; cap++)
            {
                if ((cap == 0 && !capLo) || (cap == 1 && !capHi)) continue;
                float z = cap == 0 ? z0 : z1, ro = cap == 0 ? r0 : r1;
                Vector3 o0 = m.MultiplyPoint3x4(d0 * ro + Vector3.forward * z), o1 = m.MultiplyPoint3x4(d1 * ro + Vector3.forward * z);
                Vector3 i0 = m.MultiplyPoint3x4(d0 * ri + Vector3.forward * z), i1 = m.MultiplyPoint3x4(d1 * ri + Vector3.forward * z);   // ri = 0: both collapse to the centre = a fan
                if (ri <= 0f) { Vector3 c = m.MultiplyPoint3x4(Vector3.forward * z); if (cap == 0) Tri(v, t, c, o1, o0); else Tri(v, t, c, o0, o1); continue; }   // disc: a fan from the centre
                if (cap == 0) { Tri(v, t, i0, i1, o1); Tri(v, t, i0, o1, o0); }   // rear face (-z)
                else { Tri(v, t, o1, i1, i0); Tri(v, t, o0, o1, i0); }            // front face (+z)
            }
        }
    }
    // The scope's chassis, built around the CAD tube: eyepiece bell, two mounting bands, objective bell, elevation and windage turrets. All of it sits outside the light path,
    // and it stays visible while aiming.
    void BuildChassis(Transform basePivot)
    {
        var dark = Matte(new Color(0.105f, 0.108f, 0.114f)); var mid = Matte(new Color(0.14f, 0.143f, 0.15f));
        Matrix4x4 tube = Matrix4x4.Translate(new Vector3(0f, ScopeY, 0f));
        var v = new System.Collections.Generic.List<Vector3>(); var t = new System.Collections.Generic.List<int>();
        Prism(v, t, 48, ScopeRearZ - EyecupLen, ScopeRearZ, 0.0160f, 0.0156f, tube, true, false, EyecupIn);    // eyecup: a plain short collar at the rear, like the standard SAR-21 scope tube
        {   // the optic tube, built round and hollow from the CAD's own radii (the CAD's octagon and its two solid end discs are not used)
            var tv = new System.Collections.Generic.List<Vector3>(); var tt = new System.Collections.Generic.List<int>();
            const int SD = 64; const float ri = ScopeBore;
            Prism(tv, tt, SD, ScopeRearZ, -0.1139f, 0.0140f, 0.0140f, tube, false, false, ri);
            Prism(tv, tt, SD, -0.1139f, -0.0125f, 0.0153f, 0.0153f, tube, true, false, ri);
            Prism(tv, tt, SD, -0.0125f, -0.0099f, 0.0153f, 0.0131f, tube, false, false, ri);
            Prism(tv, tt, SD, -0.0099f, ScopeFrontZ, 0.0131f, 0.0131f, tube, false, true, ri);
            var tubeMat = Matte(new Color(0.105f, 0.108f, 0.114f)); var body = AddMesh(basePivot, "scope_tube", tv, tt, tubeMat);
            adsHide.Add(body.GetComponent<MeshRenderer>());
            var rv = new System.Collections.Generic.List<Vector3>(); var rt = new System.Collections.Generic.List<int>();
            Prism(rv, rt, SD, ScopeRearZ, ScopeRearZ + 0.0002f, 0.0140f, 0.0140f, tube, true, false, ri);
            AddMesh(basePivot, "scope_rim", rv, rt, tubeMat);   // the eye-end rim: the only part of the tube in the aim view
        }
        AddMesh(basePivot, "scope_chassis", v, t, dark);
        {   // front sight blade on top of the front end of the tube (standard SAR-21 photo); hidden while aiming
            var bv = new System.Collections.Generic.List<Vector3>(); var bt = new System.Collections.Generic.List<int>();
            Matrix4x4 blade = Matrix4x4.TRS(new Vector3(0f, ScopeY + 0.0125f, ScopeFrontZ - 0.006f), Quaternion.FromToRotation(Vector3.forward, Vector3.up), Vector3.one);
            Prism(bv, bt, 4, 0f, 0.0050f, 0.0022f, 0.0015f, blade, false, true);
            adsHide.Add(AddMesh(basePivot, "scope_fsight", bv, bt, dark).GetComponent<MeshRenderer>());
        }
    }
    // Turns the CAD's octagonal tube into a round one: every triangle is split into 64 and each point is moved radially onto the circle through the octagon's corners at that point
    // (an octagon with corners on the axes, circumradius Rc, has radius r = Rc cos(22.5 deg) / cos(angle from the nearest edge middle) at the angle, which is inverted here).
    static void RoundTube(ref Vector3[] vs, ref int[] tr)
    {
        var list = new System.Collections.Generic.List<Vector3>(vs);
        for (int pass = 0; pass < 4; pass++)
        {
            var o = new System.Collections.Generic.List<Vector3>(list.Count * 4);
            for (int k = 0; k + 2 < list.Count; k += 3)
            {
                Vector3 a = list[k], b = list[k + 1], c = list[k + 2], ab = (a + b) * 0.5f, bc = (b + c) * 0.5f, ca = (c + a) * 0.5f;
                o.Add(a); o.Add(ab); o.Add(ca); o.Add(ab); o.Add(b); o.Add(bc); o.Add(ca); o.Add(bc); o.Add(c); o.Add(ab); o.Add(bc); o.Add(ca);
            }
            list = o;
        }
        float c225 = Mathf.Cos(22.5f * Mathf.Deg2Rad);
        for (int k = 0; k < list.Count; k++)
        {
            Vector3 q = list[k]; float x = q.x, y = q.y - ScopeY, r = Mathf.Sqrt(x * x + y * y);
            if (r > 1e-6f)
            {
                float ang = Mathf.Atan2(y, x) * Mathf.Rad2Deg; ang = ((ang % 45f) + 45f) % 45f; float rel = (ang - 22.5f) * Mathf.Deg2Rad;
                float rc = r * Mathf.Cos(rel) / c225, f = rc / r; q.x = x * f; q.y = ScopeY + y * f;
            }
            list[k] = q;
        }
        vs = list.ToArray(); tr = new int[vs.Length]; for (int k = 0; k < tr.Length; k++) tr[k] = k;
    }
    // both sides of every triangle (the tube is seen from outside at the hip and from inside through the eyecup): returns the doubled mesh
    static Mesh DoubleSidedMesh(Vector3[] v)
    {
        int n = v.Length; var vv = new Vector3[n * 2]; var tt = new int[n * 2];
        System.Array.Copy(v, vv, n); System.Array.Copy(v, 0, vv, n, n);
        for (int k = 0; k < n; k++) tt[k] = k;
        for (int k = 0; k + 2 < n; k += 3) { tt[n + k] = n + k; tt[n + k + 1] = n + k + 2; tt[n + k + 2] = n + k + 1; }
        var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.vertices = vv; m.triangles = tt; m.RecalculateNormals(); var nm = m.normals; for (int k = n; k < nm.Length; k++) nm[k] = -nm[k]; m.normals = nm; m.RecalculateBounds(); return m;
    }
    // the standard SAR-21 is black (polymer body, black steel, smoke-grey magazine): the CAD colours only decide how dark each part is (lighter CAD parts = metal, a little lighter)
    static Color SarBlack(float[] c) { float lum = (c[0] + c[1] + c[2]) / 3f, g = Mathf.Min(0.30f, 0.115f + 0.26f * lum); return new Color(g, g, g * 1.06f); }
    static float RadiusOf(Vector3 q) { return Mathf.Sqrt(q.x * q.x + (q.y - ScopeY) * (q.y - ScopeY)); }
    static float ZSpan(Vector3[] v) { float a = 9f, b = -9f; foreach (var q in v) { a = Mathf.Min(a, q.z); b = Mathf.Max(b, q.z); } return b - a; }
    // The reticle, part of the scope itself: fine wires, a small open ring and a dot, in one plane inside the CAD tube just ahead of its rear collar, black, the wires ending on the tube's bore.
    // It moves with the scope, is seen in perspective through the eyecup and cannot be seen from the front or the hip.
    // pure black that no light or reflection can lift: an unlit shader (URP Unlit first, then the built-in ones), the lit material only as the last resort
    // a matte, non-reflective dark material: no sky reflection or specular, so black plastic stays black instead of picking up the sky's colour
    static Material Matte(Color c)
    {
        var m = CityBuilder.Mat(c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
        if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0f);
        if (m.HasProperty("_GlossyReflections")) m.SetFloat("_GlossyReflections", 0f);
        m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        return m;
    }
    static Material BlackMat()
    {
        foreach (var n in new[] { "Universal Render Pipeline/Unlit", "Unlit/Color", "Sprites/Default" })
        {
            var sh = Shader.Find(n); if (sh == null) continue;
            var m = new Material(sh) { color = Color.black };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.black);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);   // both sides
            return m;
        }
        var l = CityBuilder.Mat(Color.black);
        if (l.HasProperty("_Smoothness")) l.SetFloat("_Smoothness", 0f);
        if (l.HasProperty("_Metallic")) l.SetFloat("_Metallic", 0f);
        if (l.HasProperty("_SpecularHighlights")) l.SetFloat("_SpecularHighlights", 0f);
        if (l.HasProperty("_EnvironmentReflections")) l.SetFloat("_EnvironmentReflections", 0f);
        return l;
    }

    // ---- the optic's window: a second camera with the field of view narrowed by the magnification draws into a texture, shown on a disc at the eye end of the tube ----
    Camera scopeCam; RenderTexture scopeRT; GameObject lensGo; Mesh lensMesh; Material lensMat; Vector3[] lensV; Vector2[] lensUv; bool camHooked, layersSet, glassOn; float lensK;
    const int HideLayer = 30;
    void BuildLens(Transform basePivot)
    {
        const int RN = 12, SN = 64; float z = ScopeRearZ + 0.0004f;
        lensV = new Vector3[1 + RN * SN]; lensUv = new Vector2[lensV.Length];
        lensV[0] = new Vector3(0f, ScopeY, z);
        for (int r = 1; r <= RN; r++) for (int k = 0; k < SN; k++) { float a = k * 2f * Mathf.PI / SN, rr = ScopeBore * r / RN; lensV[1 + (r - 1) * SN + k] = new Vector3(Mathf.Cos(a) * rr, ScopeY + Mathf.Sin(a) * rr, z); }
        var tri = new System.Collections.Generic.List<int>();
        System.Action<int, int, int> add = (a, b, c) => { tri.Add(a); tri.Add(b); tri.Add(c); tri.Add(a); tri.Add(c); tri.Add(b); };   // both sides
        for (int k = 0; k < SN; k++) add(0, 1 + k, 1 + (k + 1) % SN);
        for (int r = 1; r < RN; r++) for (int k = 0; k < SN; k++)
        {
            int a = 1 + (r - 1) * SN + k, b = 1 + (r - 1) * SN + (k + 1) % SN, c = 1 + r * SN + k, e = 1 + r * SN + (k + 1) % SN;
            add(a, c, b); add(b, c, e);
        }
        lensMesh = new Mesh(); lensMesh.vertices = lensV; lensMesh.triangles = tri.ToArray(); lensMesh.uv = lensUv; lensMesh.bounds = new Bounds(new Vector3(0f, ScopeY, z), Vector3.one * 0.1f);
        var uv2 = new Vector2[lensV.Length]; var nrm = new Vector3[lensV.Length];
        for (int k = 0; k < lensV.Length; k++) { uv2[k] = new Vector2(lensV[k].x / ScopeBore, (lensV[k].y - ScopeY) / ScopeBore); nrm[k] = new Vector3(0f, 0f, -1f); }
        lensMesh.uv2 = uv2; lensMesh.normals = nrm;
        Shader sh = Shader.Find("FreeRoam/ScopeGlass"); glassOn = sh != null;
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit"); if (sh == null) sh = Shader.Find("Unlit/Texture");
        lensMat = new Material(sh); lensMat.color = Color.white;
        if (glassOn)
        {   // the objective glass at the front end: the same coated glass with the same etched reticle, seen from the front and the side
            var fm = new Mesh(); var fv = new Vector3[lensV.Length]; for (int k = 0; k < fv.Length; k++) fv[k] = new Vector3(lensV[k].x, lensV[k].y, ScopeFrontZ - 0.0015f);
            fm.vertices = fv; fm.triangles = tri.ToArray(); fm.uv = new Vector2[fv.Length]; fm.uv2 = uv2; var fn = new Vector3[fv.Length]; for (int k = 0; k < fn.Length; k++) fn[k] = new Vector3(0f, 0f, 1f); fm.normals = fn;
            fm.bounds = new Bounds(new Vector3(0f, ScopeY, ScopeFrontZ), Vector3.one * 0.1f);
            var fmat = new Material(sh); fmat.SetFloat("_Clarity", 0f);
            var fg = new GameObject("scope_front_glass"); fg.transform.SetParent(basePivot, false); fg.AddComponent<MeshFilter>().sharedMesh = fm;
            var fr = fg.AddComponent<MeshRenderer>(); fr.sharedMaterial = fmat; fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; fr.receiveShadows = false;
        }

        lensGo = new GameObject("scope_lens"); lensGo.transform.SetParent(basePivot, false);
        lensGo.AddComponent<MeshFilter>().sharedMesh = lensMesh;
        var mr = lensGo.AddComponent<MeshRenderer>(); mr.sharedMaterial = lensMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        var cg = new GameObject("scope_cam"); cg.transform.SetParent(cam.transform, false);
        scopeCam = cg.AddComponent<Camera>(); scopeCam.CopyFrom(cam); scopeCam.cullingMask = cam.cullingMask & ~(1 << HideLayer); scopeCam.depth = cam.depth - 1f; scopeCam.enabled = false; scopeCam.allowHDR = true;
        lensGo.SetActive(false);
        if (!camHooked) { UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += OnBeginCam; camHooked = true; }
    }
    void OnDestroy() { if (camHooked) UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= OnBeginCam; if (scopeRT != null) scopeRT.Release(); }
    static void SetLayer(Transform t, int layer) { foreach (var x in t.GetComponentsInChildren<Transform>(true)) x.gameObject.layer = layer; }
    // The glass at the eye end is always there. Like a real tube, it shows the scene only while the eye looks straight down the bore (the rear opening then frames the front opening);
    // off-axis the eye sees the dark inside, so nothing turns see-through suddenly: the picture fades in smoothly as the aiming animation lines the eye up with the tube.
    float LensClarity()
    {
        Vector3 eye = cam.transform.position, pR = scopeAp.position, pF = scopeFront.position;
        Vector3 dR = pR - eye, dF = pF - eye; float lr = dR.magnitude, lf = dF.magnitude; if (lr < 1e-4f || lf < 1e-4f) return 0f;
        float sep = Mathf.Deg2Rad * Vector3.Angle(dR, dF);                                  // how far the far opening sits off the near opening, seen from the eye
        float rw = ScopeBore * Mathf.Abs(scopeAp.lossyScale.y);                                // the bore radius in the world
        float room = Mathf.Max(1e-4f, rw / lr - rw / lf);                                     // the clear room: apparent near radius minus apparent far radius
        float x = Mathf.Clamp01((sep / room - 0.15f) / 3.2f);   // a wide band: the picture starts to come in well before the eye is lined up
        return 1f - x * x * (3f - 2f * x);
    }
    void UpdateScopeCam()
    {
        if (lensGo == null || scopeCam == null) return;
        bool on = d != null && d.sar && cur >= 0 && models[cur] != null && scopeAp != null && scopeFront != null;
        if (on != lensGo.activeSelf) lensGo.SetActive(on);
        if (!on) { scopeCam.enabled = false; return; }
        float kT = LensClarity();
        lensK = Mathf.MoveTowards(lensK, kT, Time.deltaTime * (kT > lensK ? 2.6f : 5f));   // and the picture eases in over about 0.4 s (out over 0.2 s), never snaps
        float k = lensK * lensK * (3f - 2f * lensK);
        if (glassOn) lensMat.SetFloat("_Clarity", k);
        else { Color glass = Color.Lerp(new Color(0.035f, 0.04f, 0.045f), Color.white, k); lensMat.color = glass; if (lensMat.HasProperty("_BaseColor")) lensMat.SetColor("_BaseColor", glass); }
        scopeCam.enabled = k > 0.01f;
        if (!scopeCam.enabled) return;
        if (!layersSet) { SetLayer(holder, HideLayer); layersSet = true; }
        if (scopeRT == null || scopeRT.width != Screen.width || scopeRT.height != Screen.height)
        {
            if (scopeRT != null) { scopeCam.targetTexture = null; scopeRT.Release(); }
            scopeRT = new RenderTexture(Mathf.Max(16, Screen.width), Mathf.Max(16, Screen.height), 24, RenderTextureFormat.ARGBHalf); scopeRT.Create();
            scopeCam.targetTexture = scopeRT; lensMat.mainTexture = scopeRT; if (lensMat.HasProperty("_BaseMap")) lensMat.SetTexture("_BaseMap", scopeRT);
        }
        scopeCam.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1f, aim.Zoom)) * Mathf.Rad2Deg;
        scopeCam.aspect = cam.aspect; scopeCam.nearClipPlane = cam.nearClipPlane; scopeCam.farClipPlane = cam.farClipPlane;
    }
    void OnBeginCam(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera c)
    {
        if (c != cam || lensGo == null || !lensGo.activeInHierarchy) return;
        var tr = lensGo.transform;
        for (int i = 0; i < lensV.Length; i++) { Vector3 p = cam.WorldToViewportPoint(tr.TransformPoint(lensV[i])); lensUv[i] = new Vector2(p.x, p.y); }   // the lens shows the screen position it covers
        lensMesh.uv = lensUv;
    }
    // The ice pack: a flexible gel pack wrapped round the bare barrel between the handguard (z 0.272) and the muzzle device (z 0.346), held by two straps. Opaque, frosted pale blue.
    void BuildIcePack(Transform basePivot)
    {
        var v = new System.Collections.Generic.List<Vector3>(); var t = new System.Collections.Generic.List<int>();
        var vs = new System.Collections.Generic.List<Vector3>(); var ts = new System.Collections.Generic.List<int>();
        Matrix4x4 ax = Matrix4x4.Translate(new Vector3(0f, 0.0325f, 0f));
        Prism(v, t, 28, 0.274f, 0.344f, 0.0235f, 0.0235f, ax, true, true, 0.0078f);
        Prism(v, t, 28, 0.272f, 0.274f, 0.0150f, 0.0235f, ax, false, false, 0.0078f);
        Prism(v, t, 28, 0.344f, 0.346f, 0.0235f, 0.0150f, ax, false, false, 0.0078f);
        Prism(vs, ts, 28, 0.287f, 0.292f, 0.0243f, 0.0243f, ax, true, true, 0.0078f);
        Prism(vs, ts, 28, 0.326f, 0.331f, 0.0243f, 0.0243f, ax, true, true, 0.0078f);
        var mat = CityBuilder.Mat(new Color(0.62f, 0.80f, 0.93f)); if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);
        var go = AddMesh(basePivot, "ice_pack", v, t, mat);
        AddMesh(go.transform, "ice_straps", vs, ts, Matte(new Color(0.12f, 0.13f, 0.14f)));
        icePack = go.transform; iceRest = new Vector3(0f, 0.0325f, 0.309f); icePack.localPosition = iceRest; icePack.localScale = Vector3.one * 0.01f;
        // the meshes were built around the barrel axis; recentre them on the pack's own pivot so it can grow from its middle
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) { var m = mf.sharedMesh; var vv = m.vertices; for (int k = 0; k < vv.Length; k++) vv[k] -= new Vector3(0f, 0.0325f, 0.309f); m.vertices = vv; m.RecalculateBounds(); }
        go.SetActive(false);
    }
    // The real SAR-21 reticle (from the owner's photo through the scope): thin black wires across the full window, a thick ring at the crossing with an open centre, and a tiny dot in it.
    void ReticleMesh(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<int> t, float z, float R)
    {
        float e = R * 0.985f, w = R * 0.0065f, r0 = R * 0.058f, r1 = R * 0.112f, dot = R * 0.011f;
        System.Action<float, float, float, float> box = (x0, y0, x1, y1) =>
        {
            Vector3 a = new Vector3(x0, ScopeY + y0, z), b = new Vector3(x1, ScopeY + y0, z), c = new Vector3(x1, ScopeY + y1, z), d0 = new Vector3(x0, ScopeY + y1, z);
            Tri(v, t, a, b, c); Tri(v, t, a, c, d0);
        };
        box(-e, -w, -r1, w); box(r1, -w, e, w); box(-w, -e, w, -r1); box(-w, r1, w, e);
        const int N = 64;
        for (int k = 0; k < N; k++)
        {
            float a0 = k * 2f * Mathf.PI / N, a1 = (k + 1) * 2f * Mathf.PI / N; Vector3 d0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f), d1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f), o = new Vector3(0f, ScopeY, z);
            Tri(v, t, o + d0 * r0, o + d0 * r1, o + d1 * r1); Tri(v, t, o + d0 * r0, o + d1 * r1, o + d1 * r0);
            Tri(v, t, o, o + d0 * dot, o + d1 * dot);
        }
    }
    void BuildReticle(Transform basePivot)
    {
        if (glassOn) return;   // the reticle is etched into the glass by its shader
        var v = new System.Collections.Generic.List<Vector3>(); var t = new System.Collections.Generic.List<int>();
        ReticleMesh(v, t, ScopeFrontZ - 0.0020f, ScopeBore);   // at the front end of the tube, seen from the front and the side
        AddMesh(basePivot, "scope_reticle", v, t, BlackMat());
        var lv = new System.Collections.Generic.List<Vector3>(); var lt = new System.Collections.Generic.List<int>();
        ReticleMesh(lv, lt, ScopeRearZ + 0.0001f, ScopeBore);  // the same on the glass, which is what the eye sees
        AddMesh(lensGo.transform, "scope_lens_reticle", lv, lt, BlackMat());
    }
    void BuildScope(Transform basePivot, float modelScale, Def def)
    {
        BuildLens(basePivot);
        BuildReticle(basePivot);
        BuildChassis(basePivot);
        scopeRear = new GameObject("scope_rear").transform; scopeRear.SetParent(basePivot, false); scopeRear.localPosition = new Vector3(0f, ScopeY, ScopeRearZ - EyecupLen);
        scopeAp = new GameObject("scope_aperture").transform; scopeAp.SetParent(basePivot, false); scopeAp.localPosition = new Vector3(0f, ScopeY, ScopeRearZ);
        scopeFront = new GameObject("scope_front").transform; scopeFront.SetParent(basePivot, false); scopeFront.localPosition = new Vector3(0f, ScopeY, ScopeFrontZ);
        // sight picture: the rear end of the optic goes on the eye's axis, at the distance where the scope's outer width fills ScopeOnScreen of the screen height at the aimed zoom
        Vector3 rear = holder.InverseTransformPoint(basePivot.TransformPoint(new Vector3(0f, ScopeY, ScopeRearZ - EyecupLen)));
        float tanHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad), relief = EyecupOutR * modelScale / (ScopeOnScreen * tanHalf);   // the scope's real outer width (eyecup collar) takes ScopeOnScreen of the screen height, not more
        def.ads = new Vector3(-rear.x, -rear.y, relief - rear.z);
        BuildSmoke(holder.InverseTransformPoint(basePivot.TransformPoint(new Vector3(0f, 0.033f, 0.33f))));
    }
    void FitSar(int i, GameObject root)
    {
        var def = Defs[i];
        var rw = FindT(root.transform, "R_wrist", true); var lw = FindT(root.transform, "L_wrist", true); var ak = FindT(root.transform, "ak");
        var nBase = FindT(root.transform, "base"); var nBolt = FindT(root.transform, "bolt"); var nMag = FindT(root.transform, "mag"); var nRel = FindT(root.transform, "release");
        var ta = Resources.Load<TextAsset>("Guns/sar21_split");
        if (rw == null || lw == null || ak == null || nBase == null || nBolt == null || nMag == null || nRel == null || ta == null || clips[i] == null)
        { Debug.LogError("SAR-21 fit failed: missing bone, model or animation"); return; }
        clips[i].SampleAnimation(animRoots[i], def.idle.a / 30f);   // idle pose
        var meshT = FindT(root.transform, "base_ak74_0"); var mf = meshT != null ? meshT.GetComponent<MeshFilter>() : null;
        float akLen = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size.z * Mathf.Abs(mf.transform.lossyScale.z) : 110.4f * 0.015f;
        float s = akLen / 0.943f;
        Vector3 Rw = holder.InverseTransformPoint(rw.position), Lw = holder.InverseTransformPoint(lw.position);
        Vector3 t = ((Rw - s * SarWristR) + (Lw - s * SarWristL)) * 0.5f;
        sarAkRest = Quaternion.Inverse(holder.rotation) * ak.rotation;   // the AK's idle orientation in holder space, to measure the animation's roll against
        // the artist's left-hand poses are kept (gloves and arm only): idle, holding the magazine (frame 30), holding the charge handle (frame 72)
        var lArm0 = FindT(root.transform, "L_arm", true);
        if (lArm0 != null)
        {
            lcT = lArm0.GetComponentsInChildren<Transform>(true); int nb = lcT.Length;
            pI = new Vector3[nb]; pM = new Vector3[nb]; pB = new Vector3[nb]; rI = new Quaternion[nb]; rM = new Quaternion[nb]; rB = new Quaternion[nb];
            for (int k = 0; k < nb; k++) { pI[k] = lcT[k].localPosition; rI[k] = lcT[k].localRotation; }
            clips[i].SampleAnimation(animRoots[i], 30f / 30f);
            for (int k = 0; k < nb; k++) { pM[k] = lcT[k].localPosition; rM[k] = lcT[k].localRotation; }
            vMag = holder.InverseTransformPoint(lw.position) - holder.InverseTransformPoint(nMag.position);
            clips[i].SampleAnimation(animRoots[i], 72f / 30f);
            for (int k = 0; k < nb; k++) { pB[k] = lcT[k].localPosition; rB[k] = lcT[k].localRotation; }
            vBolt = holder.InverseTransformPoint(lw.position) - holder.InverseTransformPoint(nBolt.position);
            clips[i].SampleAnimation(animRoots[i], def.idle.a / 30f);
            lwIdleH = holder.InverseTransformPoint(lw.position); sarS = s;
        }
        foreach (var r in ak.GetComponentsInChildren<Renderer>(true)) r.enabled = false;   // the AK itself

        var g = JsonUtility.FromJson<GunFile>(ta.text);
        var sarRoot = new GameObject("sar_root").transform; sarRoot.SetParent(holder, false);
        sarRoot.localPosition = t; sarRoot.localRotation = Quaternion.identity; sarRoot.localScale = Vector3.one * s;
        var piv = new Dictionary<string, Transform>();
        foreach (var p in g.parts)
        {
            string key = string.IsNullOrEmpty(p.n) ? "base" : p.n;
            if (!piv.TryGetValue(key, out var pt))
            {
                pt = new GameObject("sar_" + key).transform; pt.SetParent(sarRoot, false);
                if (!string.IsNullOrEmpty(p.n) && p.pv != null && p.pv.Length >= 3) pt.localPosition = new Vector3(p.pv[0], p.pv[1], p.pv[2]);
                piv[key] = pt;
            }
            int n = p.v.Length / 3; var vs = new Vector3[n]; var tr = new int[n];
            for (int k = 0; k < n; k++) { vs[k] = new Vector3(p.v[3 * k], p.v[3 * k + 1], p.v[3 * k + 2]); tr[k] = k; }
            if (key == "base" && ((n == 432 && ZSpan(vs) > 0.2f) || (n == 36 && ZSpan(vs) > 0.19f && ZSpan(vs) < 0.215f))) continue;   // replaced by the round hollow tube from BuildChassis
            if (key == "base" && n == 60)
            {   // the front sight: in the standard SAR-21 it sits on top of the very front end of the scope tube; the CAD has it half way along, so it is moved forward
                float zlo = 9f, zhi = -9f; foreach (var q in vs) { zlo = Mathf.Min(zlo, q.z); zhi = Mathf.Max(zhi, q.z); }
                if (zlo > -0.01f && zhi < 0.02f) for (int k = 0; k < n; k++) vs[k].z += FrontSightShift;
            }
            if (key == "base" && n == 60)
            {   // the rear sight post: the CAD's block stands about twice as tall above the tube as the real low rear sight tab, so only its top is lowered
                float zlo = 9f, zhi = -9f; foreach (var q in vs) { zlo = Mathf.Min(zlo, q.z); zhi = Mathf.Max(zhi, q.z); }
                if (zlo > -0.14f && zhi < -0.12f) for (int k = 0; k < n; k++) { float top = ScopeY + 0.0140f; if (vs[k].y > top) vs[k].y = top + (vs[k].y - top) * 0.40f; vs[k].x *= 0.85f; }
            }
            if (key == "base" && n != 432)
                for (int k = 0; k < n; k++)
                {   // nothing may reach into the tube's clear bore (the CAD's handle legs and sight posts do): such vertices are pushed out into the tube wall, so the bore stays empty like the real optic
                    Vector3 q = vs[k]; if (q.z <= ScopeRearZ || q.z >= ScopeFrontZ) continue;
                    float dx = q.x, dy = q.y - ScopeY, r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r >= 0.0111f) continue;
                    if (r < 1e-5f) { dx = 0f; dy = -1f; r = 1f; }
                    float f = 0.0111f / r; vs[k].x = dx * f; vs[k].y = ScopeY + dy * f;
                }
            int ins = 0;   // vertices inside the scope's light path: parts that reach into the tube (sight posts, handle legs) are hidden while aiming; elsewhere the tube's own inner collars are cut
            if (key == "base") foreach (var q in vs) if (InScope(q)) ins++;
            bool block = false;
            if (key == "base" && n == 36) { float z0 = 9f, z1 = -9f; foreach (var q in vs) { z0 = Mathf.Min(z0, q.z); z1 = Mathf.Max(z1, q.z); } block = z1 - z0 > 0.2f; }   // the CAD's solid scope body: kept at the hip, not drawn while aiming (clear view)
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            bool tube = key == "base" && n == 432 && (ZSpan(vs)) > 0.2f;
            if (tube) { RoundTube(ref vs, ref tr); n = vs.Length; }   // the CAD models the tube as an octagon; the real optic tube is round
            Vector3[] stepV = null;
            if (tube)
            {   // the tube walls and steps inside are split off: seen from inside they would show as thin rings in the clear view, so only the rear rim ring is drawn while aiming (the real optic shows no tunnel)
                var keep = new System.Collections.Generic.List<Vector3>(vs.Length); var step = new System.Collections.Generic.List<Vector3>();
                for (int k = 0; k + 2 < vs.Length; k += 3)
                {
                    float z0 = vs[k].z; bool flat = Mathf.Abs(z0 - vs[k + 1].z) < 1e-4f && Mathf.Abs(z0 - vs[k + 2].z) < 1e-4f, ends = Mathf.Abs(z0 - ScopeRearZ) < 5e-4f;   // only the rear ring (the eye-end rim) stays in the aim view
                    var dst = flat && ends ? keep : step;   // only the rear rim ring stays in the aim view; the walls and steps are split off (hidden while aiming)
                    dst.Add(vs[k]); dst.Add(vs[k + 1]); dst.Add(vs[k + 2]);
                }
                vs = keep.ToArray(); tr = new int[vs.Length]; for (int k = 0; k < tr.Length; k++) tr[k] = k; n = vs.Length; stepV = step.ToArray();
            }
            if (tube) mesh = DoubleSidedMesh(vs); else { mesh.vertices = vs; mesh.triangles = tr; mesh.RecalculateNormals(); mesh.RecalculateBounds(); }
                        var go = new GameObject("part"); go.transform.SetParent(pt, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = Matte(tube ? new Color(0.105f, 0.108f, 0.114f) : key == "mag" ? new Color(0.30f, 0.275f, 0.255f) : SarBlack(p.c));   // the tube is black like the real optic housing (and its rear rim frames the picture in black)
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            if (stepV != null && stepV.Length > 0)
            {
                var stepMesh = DoubleSidedMesh(stepV);
                var g2 = new GameObject("part_steps"); g2.transform.SetParent(pt, false); g2.AddComponent<MeshFilter>().sharedMesh = stepMesh;
                var r2 = g2.AddComponent<MeshRenderer>(); r2.sharedMaterial = mr.sharedMaterial; r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r2.receiveShadows = false; adsHide.Add(r2);
            }
            if (ins > 0 || block) adsHide.Add(mr);   // parts intruding into the light path and the solid body: not drawn while aiming; the CAD tube is drawn from outside only, so looking through it the world is seen clear (the scope housing, eyecup and turrets stay visible while aiming)
            if (string.IsNullOrEmpty(p.n) && mesh.bounds.max.z > 0.33f)   // the barrel and the muzzle device (the only parts reaching beyond the handguard): they glow when hot
            {
                var hm = new Material(mr.sharedMaterial); mr.sharedMaterial = hm; hm.EnableKeyword("_EMISSION");
                hotM.Add(hm); hotBase.Add(hm.HasProperty("_BaseColor") ? hm.GetColor("_BaseColor") : hm.HasProperty("_Color") ? hm.GetColor("_Color") : Color.gray);
            }
        }
        BuildScope(piv["base"], s, def);
        BuildIcePack(piv["base"]); sarBasePivot = piv["base"];
        foreach (var kv in piv)
        {
            Transform target = kv.Key == "mag" ? nMag : kv.Key == "magcatch" ? nRel : kv.Key == "charge" ? nBolt : nBase;   // only the top charging handle moves; the AK's side handle and bolt motion is not shown on the SAR-21
            kv.Value.SetParent(target, true);   // keeps the fitted world pose; the bone's animation carries it from here
        }
        // The SAR-21 magazine sits at the grip, the artist's AK magazine well is further forward. The magazine follows the AK magazine's motion from its own well,
        // and while the support hand is at the magazine the whole left arm is moved by the same offset, so the hand really holds the SAR-21 magazine.
        var lArm = FindT(root.transform, "L_arm", true);
        if (piv.TryGetValue("mag", out var sm) && lArm != null)
        {
            sarMagNode = nMag; sarLArm = lArm; sarLWrist = lw; sarMagPivot = sm;
            sarD = holder.InverseTransformVector(sm.position - nMag.position);
            sarAk = ak; sarBoltNode = nBolt; sarBaseNode = nBase; sarBoltRestInAk = ak.InverseTransformPoint(nBolt.position);
            if (piv.TryGetValue("charge", out sarCharge)) sarD2 = holder.InverseTransformVector(sarCharge.position - nBolt.position);
            sarMagRestInAk = ak.InverseTransformPoint(nMag.position);
            magRestH = holder.InverseTransformPoint(sm.position);
            if (sarCharge != null) chargeRestH = holder.InverseTransformPoint(sarCharge.position);
        }
        // The artist's sleeve stops at the elbow, so a lowered hand showed the cut edge. A dark sleeve carries on from the cut up the arm, off the bottom of the screen.
        var elb = FindT(root.transform, "L_elbow_02", true);
        if (elb != null)
        {
            Vector3 cH = new Vector3(-0.2189f, -0.0311f, 0.2277f), dH = new Vector3(-0.57610f, -0.56116f, -0.59431f).normalized;   // measured on the idle pose, in holder space
            // the sleeve's ragged loose end (the cloth beyond 0.28 from the wrist) is cut away, the new sleeve takes over from there
            try { foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var sm0 = smr.sharedMesh; if (sm0 == null || sm0.boneWeights.Length != sm0.vertexCount) continue;
                var bn = smr.bones; var bp = sm0.bindposes; var bw = sm0.boneWeights; var vv = sm0.vertices; var mats = new Matrix4x4[bn.Length];
                for (int b = 0; b < bn.Length; b++) mats[b] = bn[b] != null ? bn[b].localToWorldMatrix * bp[b] : Matrix4x4.identity;
                var cutV = new bool[vv.Length]; bool any = false; Vector3 wp = lw.position;
                for (int v = 0; v < vv.Length; v++)
                {
                    var w = bw[v]; int bi = w.boneIndex0; float bwt = w.weight0;
                    if (w.weight1 > bwt) { bi = w.boneIndex1; bwt = w.weight1; } if (w.weight2 > bwt) { bi = w.boneIndex2; bwt = w.weight2; } if (w.weight3 > bwt) { bi = w.boneIndex3; }
                    if (bi < 0 || bi >= bn.Length || bn[bi] == null || !bn[bi].name.StartsWith("L_")) continue;
                    Vector3 pw = mats[w.boneIndex0].MultiplyPoint3x4(vv[v]) * w.weight0;
                    if (w.weight1 > 0f) pw += mats[w.boneIndex1].MultiplyPoint3x4(vv[v]) * w.weight1;
                    if (w.weight2 > 0f) pw += mats[w.boneIndex2].MultiplyPoint3x4(vv[v]) * w.weight2;
                    if (w.weight3 > 0f) pw += mats[w.boneIndex3].MultiplyPoint3x4(vv[v]) * w.weight3;
                    if ((pw - wp).magnitude > 0.28f * holder.lossyScale.x) { cutV[v] = true; any = true; }
                }
                if (!any) continue;
                var m2 = Instantiate(sm0);
                for (int sb = 0; sb < m2.subMeshCount; sb++)
                {
                    var tri = m2.GetTriangles(sb); var keep = new List<int>(tri.Length);
                    for (int tq = 0; tq < tri.Length; tq += 3) if (!(cutV[tri[tq]] || cutV[tri[tq + 1]] || cutV[tri[tq + 2]])) { keep.Add(tri[tq]); keep.Add(tri[tq + 1]); keep.Add(tri[tq + 2]); }
                    m2.SetTriangles(keep, sb);
                }
                smr.sharedMesh = m2;
            } } catch (System.Exception ex) { Debug.LogWarning("sleeve trim skipped: " + ex.Message); }
            // one continuous sleeve from inside the trimmed cloth, through a rounded elbow, to a fixed shoulder out of view (rebuilt every frame, see UpdateSleeve)
            Material armMat = Matte(new Color(0.14f, 0.15f, 0.16f));
            var sg = new GameObject("sar_sleeve"); sg.layer = HideLayer;
            sleeveV = new Vector3[2 * SlN * SlM]; sleeveNr = new Vector3[2 * SlN * SlM]; var tr = new List<int>(); int half = SlN * SlM;
            for (int si = 0; si < SlN - 1; si++) for (int k = 0; k < SlM; k++)
            {
                int k2 = (k + 1) % SlM, A = si * SlM + k, A2 = si * SlM + k2, B = (si + 1) * SlM + k, B2 = (si + 1) * SlM + k2;
                tr.Add(A); tr.Add(B); tr.Add(A2); tr.Add(A2); tr.Add(B); tr.Add(B2);                                                       // inner side (faces the axis)
                tr.Add(half + A2); tr.Add(half + B); tr.Add(half + A); tr.Add(half + B2); tr.Add(half + B); tr.Add(half + A2);             // outer side
            }
            sleeveMesh = new Mesh { name = "sleeve" }; sleeveMesh.MarkDynamic(); sleeveMesh.vertices = sleeveV; sleeveMesh.normals = sleeveNr; sleeveMesh.triangles = tr.ToArray();
            sg.AddComponent<MeshFilter>().sharedMesh = sleeveMesh; var r1 = sg.AddComponent<MeshRenderer>(); r1.sharedMaterial = armMat; r1.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r1.receiveShadows = false;
            sarElbow = elb; armDn = dH;
            Vector3 w0 = lwIdleH, e0 = w0 + dH * ArmLf; Vector3 dup = new Vector3(-0.20f, 0.25f, -0.946f).normalized;   // the upper arm runs from the elbow back and up to the shoulder armS0 = e0 + dup * ArmLu;
            Vector3 u0 = (w0 - armS0).normalized, v0 = e0 - armS0; armPole = (v0 - u0 * Vector3.Dot(v0, u0)).normalized;   // the elbow bends the way it does in the idle pose
        }
        Destroy(sarRoot.gameObject);
        // sight picture: the eye sits on the optic axis, 0.15 behind the optic (SAR-21 sightY 0.129 and sightZ -0.127 in gun space)
        def.ads = -new Vector3(t.x, s * 0.129f + t.y, s * -0.127f + t.z - 0.15f);
        Debug.Log("SAR-21 fitted: scale " + s.ToString("F3") + " offset " + t + " ads " + def.ads);
        BuildWaist();
    }

    // an open tube along +Z (length len, radius r0 at the start and r1 at the end), drawn from both sides
    static Mesh TubeMesh(float len, float r0, float r1)
    {
        const int N = 24; var vsl = new Vector3[N * 4]; var nl = new Vector3[N * 4]; var tl = new List<int>();
        for (int q = 0; q < N; q++)
        {
            float a2 = q * Mathf.PI * 2f / N; var rad = new Vector3(Mathf.Cos(a2), Mathf.Sin(a2), 0f);
            vsl[q] = rad * r0; vsl[N + q] = rad * r1 + Vector3.forward * len; nl[q] = nl[N + q] = -rad;
            vsl[2 * N + q] = vsl[q]; vsl[3 * N + q] = vsl[N + q]; nl[2 * N + q] = nl[3 * N + q] = rad;
        }
        for (int q = 0; q < N; q++)
        {
            int j = (q + 1) % N;
            tl.Add(q); tl.Add(N + q); tl.Add(j); tl.Add(j); tl.Add(N + q); tl.Add(N + j);
            tl.Add(2 * N + q); tl.Add(2 * N + j); tl.Add(3 * N + q); tl.Add(2 * N + j); tl.Add(3 * N + j); tl.Add(3 * N + q);
        }
        var m = new Mesh { name = "tube" }; m.vertices = vsl; m.normals = nl; m.triangles = tl.ToArray(); m.RecalculateBounds(); return m;
    }
    // Two-bone left arm: forearm ArmLf (wrist to elbow) and upper arm ArmLu (elbow to the fixed shoulder armS0, out of view). Measured in the model's own units (the artist's arms are larger than life).
    const float ArmLf = 0.52f, ArmLu = 0.57f;
    Transform sarElbow; Vector3 armS0, armPole, armDn;
    const int SlN = 18, SlM = 24; Mesh sleeveMesh; Vector3[] sleeveV, sleeveNr; readonly Vector3[] slP = new Vector3[SlN]; readonly float[] slL = new float[SlN];
    Vector3 ElbowFor(Vector3 W)   // holder space: the elbow for a wrist at W, bent towards the idle pole
    {
        Vector3 d0 = W - armS0; float d = Mathf.Clamp(d0.magnitude, Mathf.Abs(ArmLu - ArmLf) + 0.01f, ArmLu + ArmLf - 0.01f); Vector3 u = d0.normalized;
        float a = (ArmLu * ArmLu - ArmLf * ArmLf + d * d) / (2f * d), h = Mathf.Sqrt(Mathf.Max(0f, ArmLu * ArmLu - a * a));
        Vector3 p = (armPole - u * Vector3.Dot(armPole, u)).normalized; return armS0 + u * a + p * h;
    }
    // The sleeve: forearm (from inside the trimmed cloth to the elbow end), a rounded elbow (quadratic Bezier, the radius of curvature stays above the sleeve radius) and the upper arm to the shoulder, running on past it.
    void UpdateSleeve()
    {
        if (sleeveMesh == null || sarElbow == null || sarLWrist == null) return;
        float hs = holder.lossyScale.x; Vector3 wp = sarLWrist.position, dir = (sarElbow.position - wp).normalized;
        Vector3 A = wp + dir * (0.19f * hs), E = wp + dir * (ArmLf * hs), S = holder.TransformPoint(armS0);
        Vector3 d1 = E - A; float lenA = d1.magnitude; d1 /= Mathf.Max(lenA, 1e-6f); Vector3 d3 = S - E; float lenS = d3.magnitude; d3 /= Mathf.Max(lenS, 1e-6f);
        float tl = Mathf.Min(0.29f * hs, 0.88f * lenA, 0.5f * lenS);
        Vector3 P1 = E - d1 * tl, P2 = E + d3 * tl, PE = S + d3 * (0.35f * hs);
        int n = 0; slP[n++] = A; slP[n++] = Vector3.Lerp(A, P1, 0.5f); slP[n++] = P1; int eIdx = 0;
        for (int q = 1; q <= 11; q++) { float t = q / 12f, w = 1f - t; slP[n++] = P1 * (w * w) + E * (2f * w * t) + P2 * (t * t); if (q == 6) eIdx = n - 1; }
        slP[n++] = P2; slP[n++] = Vector3.Lerp(P2, PE, 0.33f); slP[n++] = Vector3.Lerp(P2, PE, 0.66f); slP[n++] = PE;
        slL[0] = 0f; for (int i = 1; i < SlN; i++) slL[i] = slL[i - 1] + (slP[i] - slP[i - 1]).magnitude;
        float sE = Mathf.Max(slL[eIdx], 1e-4f);
        Vector3 nn = Vector3.ProjectOnPlane(Vector3.up, (slP[1] - slP[0]).normalized); if (nn.sqrMagnitude < 1e-4f) nn = Vector3.right; nn.Normalize();
        int half = SlN * SlM;
        for (int i = 0; i < SlN; i++)
        {
            Vector3 ti = (slP[Mathf.Min(SlN - 1, i + 1)] - slP[Mathf.Max(0, i - 1)]).normalized; nn = Vector3.ProjectOnPlane(nn, ti).normalized; Vector3 bb = Vector3.Cross(ti, nn);
            float l = slL[i] / hs, bs = l <= sE / hs ? 0.082f + 0.012f * Smooth(Mathf.Min(1f, l / (sE / hs))) : 0.094f + 0.010f * Mathf.Min(1f, (l - sE / hs) / 0.6f);
            float r = (bs + 0.004f * Mathf.Exp(-((l - sE / hs) / 0.07f) * ((l - sE / hs) / 0.07f))) * hs;
            for (int k = 0; k < SlM; k++)
            {
                float al = k * 2f * Mathf.PI / SlM; Vector3 rad = nn * Mathf.Cos(al) + bb * Mathf.Sin(al);
                sleeveV[i * SlM + k] = sleeveV[half + i * SlM + k] = slP[i] + rad * r; sleeveNr[i * SlM + k] = -rad; sleeveNr[half + i * SlM + k] = rad;
            }
        }
        sleeveMesh.vertices = sleeveV; sleeveMesh.normals = sleeveNr; sleeveMesh.RecalculateBounds();
    }
    static float Smooth(float q) { return q * q * (3f - 2f * q); }
    void LateUpdate() { LateUpdateMain(); AlignScope(); UpdateScopeCam(); UpdateSleeve(); }
    // While aiming, the scope looks exactly where the camera looks: the tube axis (rear ring centre to front end, measured on the animated rifle) is turned onto the camera's
    // forward axis, about the rear ring, and the rear ring is centred on the view axis. Blended in with the aim, so the hip pose is untouched.
    Transform scopeRear, scopeFront, scopeAp; Texture2D haloTex, edgeTex;
    void AlignScope()
    {
        if (scopeRear == null || d == null || !d.sar) return;
        float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((aim.AimAmount - 0.3f) / 0.7f)); if (w <= 0f) return;
        var ct = cam.transform; Vector3 pr = ct.InverseTransformPoint(scopeRear.position), pf = ct.InverseTransformPoint(scopeFront.position);
        if ((pf - pr).sqrMagnitude < 1e-8f) return;
        Quaternion q = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(pf - pr, Vector3.forward), w);
        holder.localRotation = q * holder.localRotation;
        holder.localPosition = pr + q * (holder.localPosition - pr) + new Vector3(-pr.x, -pr.y, 0f) * w;
    }
    void LateUpdateMain()
    {
        if (waistRoot != null && holder != null) { bool on = holder.gameObject.activeInHierarchy; if (waistRoot.gameObject.activeSelf != on) waistRoot.gameObject.SetActive(on); if (waistRoot.parent != cam.transform) waistRoot.localPosition = cam.transform.localPosition;
            if (iceSpares != null && barrel != null) for (int q = 0; q < iceSpares.Length; q++) { bool v = on && (q < 2 ? barrel.PackStock > q : !(iceAnimT >= 0f) && !barrel.PackOn && (barrel.PackStock > 2 || barrel.PackE > 0.0)); if (iceSpares[q].gameObject.activeSelf != v) iceSpares[q].gameObject.SetActive(v); } }
        if (sarLArm == null || cur < 0 || !Defs[cur].sar) { sarCancel = 0f; sarReload = false; return; }
        float rel = frame - play.a, len = Mathf.Max(1f, play.b - play.a);
        if (st == St.Reload)
        {
            // The SAR-21 reload is its own animation: the artist's clip only supplies the idle pose (see Sample) and the glove/arm poses for holding the magazine and the handle.
            float u = reloadFull ? rel * 60f / len : rel;   // 0..60 magazine swap, 60..89 charging handle (only when the rifle was empty)
            float wm = Mathf.Clamp01(Ramp(u, 0f, 10f) - Ramp(u, 50f, 58f));
            float wb = reloadFull ? 0f : Mathf.Clamp01(Ramp(u, 60f, 67f) - Ramp(u, 81f, 89f));
            float pull = reloadFull ? 0f : Ramp(u, 68f, 74f) - Ramp(u, 76f, 82f);   // charging handle back and forward
            tilt = Mathf.Clamp01(Ramp(u, 2f, 12f) - Ramp(u, 50f, 58f));
            tiltB = wb;
            sarRoll = 0f; sarCancel = 0f;
            // lift and turn the rifle so the magazine well is in view, and add the knocks that make the swap feel physical
            sarReload = true;
            float L = Mathf.SmoothStep(0f, 1f, tilt);
            float release = Pulse(u, 9f, 2.5f), seat = Pulse(u, 47f, 2.5f), tug = Pulse(u, 71f, 3f), snap = Pulse(u, 79f, 2f);
            rlPos = (new Vector3(0f, 0.16f, 0f) - sarD) * L + new Vector3(0f, -0.014f * release - 0.02f * seat, 0f) + new Vector3(0f, 0f, -0.025f * tug + 0.02f * snap);
            rlRot = Quaternion.Euler(-4f * seat + 3f * release, 0f, 0f);
            // Magazine change: the magazine is pulled out to the left, the hand takes it out of view to the pouch (swaps it there) and brings the fresh one back and seats it.
            Vector3 outP = new Vector3(-0.05f, -MagDrop, 0f) * sarS, pouch = holder.InverseTransformVector(holder.parent.TransformVector(Pouch));
            Vector3 mo;   // where the magazine in the hand is, in holder space, relative to the well
            if (u < 26f) mo = outP * Ramp(u, 5f, 20f);   // it is released a little before the hand closes on it
            else if (u < 34f) mo = Vector3.Lerp(outP, pouch, Ramp(u, 26f, 34f));
            else if (u < 44f) mo = Vector3.Lerp(pouch, outP, Ramp(u, 34f, 44f));
            else mo = outP * (1f - Ramp(u, 44f, 48f));
            // magazine A (the old one) is in the hand until it is let go at u=26 and falls away; magazine B (the fresh one) comes back with the hand from off-screen, so nothing pops in or out
            if (sarMagPivot != null)
            {
                if (sarMagB == null) { sarMagB = Instantiate(sarMagPivot.gameObject, sarMagPivot.parent).transform; sarMagB.gameObject.SetActive(false); }
                float ts = Mathf.Max(0f, (u - 26f) / 30f);
                bool aOn = u < 42f;
                if (sarMagPivot.gameObject.activeSelf != aOn) sarMagPivot.gameObject.SetActive(aOn);
                if (u < 26f) sarMagPivot.position = holder.TransformPoint(magRestH + mo);   // position only: setting a world rotation under the glb's scaled/mirrored bones shears the mesh
                else if (aOn) { Vector3 w = holder.TransformPoint(magRestH + outP); w.y -= 6f * ts * ts + ts; w.x -= 0.3f * ts; sarMagPivot.position = w; }
                bool bOn = u >= 34f;
                if (sarMagB.gameObject.activeSelf != bOn) sarMagB.gameObject.SetActive(bOn);
                if (bOn) sarMagB.position = holder.TransformPoint(magRestH + mo);
            }
            Vector3 reachX = outP * (0.8f * ((1f - Ramp(u, 6f, 12f)) + Ramp(u, 47f, 54f)));   // the hand comes in from, and leaves to, the side, so the forearm is not seen end-on
            if (sarCharge != null)
            {
                if (sarChargeOn) { sarCharge.SetParent(sarBaseNode, true); sarChargeOn = false; }
                sarCharge.position = holder.TransformPoint(chargeRestH + new Vector3(0f, 0f, -ChargePull * sarS * pull));
            }
            if (lcT != null)
            {
                for (int k = 0; k < lcT.Length; k++)
                {
                    Vector3 p = pI[k]; Quaternion r = rI[k];
                    if (wm > 0f) { p = Vector3.Lerp(p, pM[k], wm); r = Quaternion.Slerp(r, rM[k], wm); }
                    if (wb > 0f) { p = Vector3.Lerp(p, pB[k], wb); r = Quaternion.Slerp(r, rB[k], wb); }
                    lcT[k].localPosition = p; lcT[k].localRotation = r;
                }
                Vector3 tgt = lwIdleH;
                tgt += wm * ((magRestH + mo + reachX + vMag) - lwIdleH);
                tgt += wb * ((chargeRestH + new Vector3(0f, 0f, -ChargePull * sarS * pull) + vBolt) - lwIdleH);
                sarLArm.position += holder.TransformPoint(tgt) - sarLWrist.position;
            }
            return;
        }
        if (sarMagPivot != null && !sarMagPivot.gameObject.activeSelf) sarMagPivot.gameObject.SetActive(true);
        if (sarMagB != null && sarMagB.gameObject.activeSelf) sarMagB.gameObject.SetActive(false);
        if (st == St.Take)
        {   // draw: the rifle swings up from low and muzzle-down into the carry pose with both hands on it, no handle-pulling (the artist's AK clip is not used, see Sample)
            if (sarChargeOn && sarCharge != null) { sarCharge.SetParent(sarBaseNode, true); sarChargeOn = false; }
            float u = Mathf.Clamp01(rel / (len * 0.7f)), k = (1f - u) * (1f - u) * (1f - u);   // fast start, soft landing
            sarReload = true; tilt = 0f; tiltB = 0f; sarRoll = 0f; sarCancel = 0f; sarW2 = 0f;
            rlPos = new Vector3(0.06f, -0.30f, -0.05f) * k; rlRot = Quaternion.Euler(38f * k, -9f * k, 6f * k);
            return;
        }
        sarReload = false; tilt = 0f; tiltB = 0f; rlPos = Vector3.zero; rlRot = Quaternion.identity;
        // draw: the artist's clip pulls the AK's side handle; on the SAR-21 only the top handle follows that, and the hand goes to it while it is pulled
        float wb2 = st == St.Take ? Ramp(rel, 8f, 12f) - Ramp(rel, 19f, 23f) : 0f;
        sarW2 = wb2;
        var dq = Quaternion.Inverse(holder.rotation) * sarAk.rotation * Quaternion.Inverse(sarAkRest);
        float tw = 2f * Mathf.Atan2(dq.z, dq.w) * Mathf.Rad2Deg; tw = Mathf.Repeat(tw + 180f, 360f) - 180f;
        sarRoll = tw;
        sarCancel = st == St.Take ? Mathf.Clamp01(Ramp(rel, 0f, 6f) - Ramp(rel, len - 6f, len)) : 0f;   // keep the rifle upright during the draw
        bool charging = st == St.Take;
        if (sarCharge != null && charging != sarChargeOn) { sarCharge.SetParent(charging ? sarBoltNode : sarBaseNode, true); sarChargeOn = charging; }
        if (sarW2 > 0.001f) sarLArm.position += holder.TransformVector(sarD2) * sarW2;
        if (iceAnimT >= 0f) IceAnim();
    }

    // ---- spent cases: a 5.56x45 case (44.7 mm long, about 9.6 mm at the rim) is thrown out of the ejection port to the right, tumbles, falls under gravity and bounces on what it lands on ----
    class Casing { public Transform t; public Vector3 v, spinAxis; public float spin, age, rest; public int bounces; public bool atRest; }
    readonly System.Collections.Generic.List<Casing> casings = new System.Collections.Generic.List<Casing>();
    Material brassMat; Mesh casingMesh;
    void EjectCasing()
    {
        if (cam == null) return;
        if (brassMat == null)
        {
            brassMat = CityBuilder.Mat(new Color(0.62f, 0.45f, 0.19f));   // brass, a little duller so the muzzle light does not blow it out
            if (brassMat.HasProperty("_Metallic")) brassMat.SetFloat("_Metallic", 0.5f);
            if (brassMat.HasProperty("_Smoothness")) brassMat.SetFloat("_Smoothness", 0.35f);
            casingMesh = Cartridge.CaseMesh();
        }
        var go = new GameObject("casing"); var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = casingMesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = brassMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Transform ct = cam.transform; Vector3 port = sarBasePivot != null ? sarBasePivot.TransformPoint(new Vector3(0f, 0.07f, -0.07f)) : ct.position + ct.forward * 0.35f - ct.up * 0.09f;
        port += ct.right * 0.04f;   // out at the right-hand side of the receiver
        go.transform.position = port; go.transform.rotation = Quaternion.LookRotation(ct.forward, ct.up) * Quaternion.Euler(90f, 0f, 0f);
        float r1 = UnityEngine.Random.value - 0.5f, r2 = UnityEngine.Random.value - 0.5f, r3 = UnityEngine.Random.value - 0.5f;
        var c = new Casing { t = go.transform };
        c.v = ct.right * (2.6f + r1 * 0.9f) + ct.up * (1.5f + r2 * 0.8f) - ct.forward * (0.5f + r3 * 0.6f) + hvVel * 0.5f;   // ejected up and to the right, a little back
        c.spinAxis = new Vector3(UnityEngine.Random.value - 0.5f, UnityEngine.Random.value - 0.5f, UnityEngine.Random.value - 0.5f).normalized; c.spin = (18f + UnityEngine.Random.value * 22f) * Mathf.Rad2Deg;
        casings.Add(c);
        if (casings.Count > 60) { var o = casings[0]; if (o.t != null) Destroy(o.t.gameObject); casings.RemoveAt(0); }
    }
    void UpdateCasings(float dt)
    {
        for (int i = casings.Count - 1; i >= 0; i--)
        {
            var c = casings[i]; if (c.t == null) { casings.RemoveAt(i); continue; }
            c.age += dt;
            if (c.atRest) { c.rest += dt; if (c.rest > 20f) { Destroy(c.t.gameObject); casings.RemoveAt(i); } continue; }
            Vector3 p = c.t.position; c.v += Physics.gravity * dt; c.v *= 1f - 0.4f * dt;   // gravity and a little drag
            Vector3 np = p + c.v * dt, seg = np - p; float len = seg.magnitude;
            if (len > 1e-6f && Physics.Raycast(p, seg / len, out var hit, len + 0.006f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && !(hit.collider is CharacterController) && c.age > 0.08f)
            {
                Vector3 n = hit.normal; float vn = Vector3.Dot(c.v, n);
                Vector3 vt = c.v - n * vn;
                c.v = vt * 0.65f - n * vn * 0.38f;                       // brass on concrete: loses much of its energy and bounces about a third as hard
                c.spin *= 0.6f; c.bounces++;
                np = hit.point + n * 0.005f;
                if (c.v.magnitude < 0.5f || c.bounces >= 5)
                {   // comes to rest lying down on its side
                    c.atRest = true; Vector3 ax = Vector3.Cross(n, UnityEngine.Random.insideUnitSphere).normalized; if (ax.sqrMagnitude < 0.01f) ax = Vector3.right;
                    c.t.rotation = Quaternion.FromToRotation(Vector3.up, ax); np = hit.point + n * 0.0048f;
                }
            }
            c.t.position = np;
            if (!c.atRest) c.t.Rotate(c.spinAxis, c.spin * dt, Space.World);
            if (c.age > 30f) { Destroy(c.t.gameObject); casings.RemoveAt(i); }
        }
    }

    // ---- The waist gear -------------------------------------------------------------------------------------------------------------------------------
    // One uniform system: the tunic, trousers, load-bearing vest, belt and pouches are all the same camouflage fabric at one world scale (5 mm pattern cells with a woven texture), joined by the same olive webbing,
    // tan hardware and thread. The vest has PALS webbing ladders; the mag pouches attach to them with snap straps, the utility pouch hangs on the belt. Pouch construction follows MOLLE/PALS flap pouches (soft body that swells over
    // its contents, dark lining, back-hinged flap with a stitched edge, centre strap with a side-release buckle; female half on the flap, male tab on the body). The pattern is an approximation of the SAF digital camouflage from photos.
    // Everything hangs on the Player object (yaw only), placed relative to the eye (real human size, for a third-person view; the first-person camera does not see it).
    Transform icePouch, iceFlap, waistRoot; Vector3 palmOffH; Texture2D camoTex; Transform[] iceSpares;
    const float BeA = 0.165f, BeB = 0.115f, BeCz = -0.15f, BeH = 0.05f, BeT = 0.012f, BeY = -0.64f, PouchK = 1.0f;
    static readonly float BeTh = 38f * Mathf.Deg2Rad;
    const float UVS = 3.125f;   // texture tiles per metre: one 32 x 32 cell tile of the camouflage (10 mm cells) is 0.32 m
    struct PouchSpec { public float W, D, H, dropH, bB, bR; public int nR; public bool pals; }
    static readonly PouchSpec IceSpec = new PouchSpec { W = 0.17f, D = 0.075f, H = 0.08f, dropH = 0.050f, nR = 3, bB = 0.004f, bR = 0.006f, pals = false };   // three standing packs side by side
    static readonly PouchSpec MagL = new PouchSpec { W = 0.105f, D = 0.07f, H = 0.135f, dropH = 0.10f, nR = 1, bB = 0.005f, bR = 0.006f, pals = true };   // tall dome-flap pouch
    static readonly PouchSpec MagR = new PouchSpec { W = 0.16f, D = 0.075f, H = 0.10f, dropH = 0.07f, nR = 2, bB = 0.005f, bR = 0.006f, pals = true };    // wide general-purpose pouch

    static Mesh MeshOf(List<Vector3> V, List<Vector2> UV, List<int> T)
    {
        var m = new Mesh(); m.SetVertices(V); m.SetUVs(0, UV); m.SetTriangles(T, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
    }
    static List<Vector2> RoundRect(float w, float d, float r, int seg)   // a rounded rectangle, counter-clockwise from the first corner
    {
        var pts = new List<Vector2>(); float[,] cs = { { w / 2 - r, d / 2 - r, 0 }, { -(w / 2 - r), d / 2 - r, 90 }, { -(w / 2 - r), -(d / 2 - r), 180 }, { w / 2 - r, -(d / 2 - r), 270 } };
        for (int c = 0; c < 4; c++) for (int k = 0; k <= seg; k++) { float a = (cs[c, 2] + 90f * k / seg) * Mathf.Deg2Rad; pts.Add(new Vector2(cs[c, 0] + r * Mathf.Cos(a), cs[c, 1] + r * Mathf.Sin(a))); }
        return pts;
    }
    static List<Vector4> RoundRect2(float w, float d, float r, int seg)   // as RoundRect, with extra points along the straight edges and an outward normal per point: (x, z, nx, nz)
    {
        var pts = new List<Vector4>(); float[,] cs = { { w / 2 - r, d / 2 - r, 0 }, { -(w / 2 - r), d / 2 - r, 90 }, { -(w / 2 - r), -(d / 2 - r), 180 }, { w / 2 - r, -(d / 2 - r), 270 } };
        for (int c = 0; c < 4; c++)
        {
            float cx = cs[c, 0], cz = cs[c, 1], a0 = cs[c, 2];
            for (int k = 0; k <= seg; k++) { float a = (a0 + 90f * k / seg) * Mathf.Deg2Rad; pts.Add(new Vector4(cx + r * Mathf.Cos(a), cz + r * Mathf.Sin(a), Mathf.Cos(a), Mathf.Sin(a))); }
            float a1 = (a0 + 90f) * Mathf.Deg2Rad, nx = Mathf.Cos(a1), nz = Mathf.Sin(a1); int c2 = (c + 1) % 4; float cx2 = cs[c2, 0], cz2 = cs[c2, 1];
            float e0x = cx + r * nx, e0z = cz + r * nz, e1x = cx2 + r * nx, e1z = cz2 + r * nz; float len = Mathf.Sqrt((e1x - e0x) * (e1x - e0x) + (e1z - e0z) * (e1z - e0z)); int sub = Mathf.Max(0, Mathf.CeilToInt(len / 0.01f) - 1);
            for (int q = 1; q <= sub; q++) { float t = (float)q / (sub + 1); pts.Add(new Vector4(e0x + (e1x - e0x) * t, e0z + (e1z - e0z) * t, nx, nz)); }
        }
        return pts;
    }
    // the soft pouch body: the fabric swells over the contents (a ridge over each standing item on the front), tapers at the sewn bottom, with small wrinkles; liner = the dark lining inside (facing inward)
    static Mesh SoftMesh(float W, float D, float H, float r, int nR, float bB, float bR, bool liner)
    {
        var pts = RoundRect2(W, D, r, 4); int N = pts.Count; float pitch = (W - 0.016f) / nR;
        var V = new List<Vector3>(); var UV = new List<Vector2>(); var T = new List<int>();
        var cum = new float[N + 1]; for (int i = 1; i <= N; i++) { var a = pts[i - 1]; var b = pts[i % N]; cum[i] = cum[i - 1] + Mathf.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y)); }
        var ys = new List<float>(); var ts = new List<float>(); var ks = new List<float>();
        if (liner) { ys.Add(0f); ts.Add(-1f); ks.Add(0f); ys.Add(-0.0015f); ts.Add(-1f); ks.Add(1f); }
        const int LV = 10; for (int l = 0; l < LV; l++) { float t = (float)l / (LV - 1); ys.Add(-H * t); ts.Add(t); ks.Add(0f); }
        int L = ys.Count;
        for (int l = 0; l < L; l++)
        {
            float t = ts[l], y = ys[l], f = 1f; if (t > 0.82f) { float q = (t - 0.82f) / 0.18f; f = 1f - 0.13f * q * q; }
            for (int i = 0; i < N; i++)
            {
                var p = pts[i]; float o;
                if (t < 0f) o = -0.006f * ks[l]; else if (liner) o = -0.006f;
                else { float w = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(t, 1.25f)), 0.6f); float fw = Mathf.Max(0f, -p.w); o = w * (bB + fw * bR * (0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * p.x / pitch)) + 0.0013f * Mathf.Sin(p.x * 83f + y * 57f + p.y * 40f) * Mathf.Sin(y * 131f + 1.7f * p.x * 30f)) + 0.0009f * Mathf.Sin(2f * Mathf.PI * cum[i] / 0.025f) * (Mathf.Exp(-Mathf.Pow(t / 0.08f, 2f)) + Mathf.Exp(-Mathf.Pow((1f - t) / 0.08f, 2f))); }
                V.Add(new Vector3(p.x * f + p.z * o, y, p.y * f + p.w * o)); UV.Add(new Vector2(cum[i] * UVS, y * UVS));
            }
        }
        for (int k = 0; k < L - 1; k++) for (int i = 0; i < N; i++)
        {
            int j = (i + 1) % N, A = k * N + i, A2 = k * N + j, B = (k + 1) * N + i, B2 = (k + 1) * N + j;
            if (liner) { T.Add(A); T.Add(B); T.Add(A2); T.Add(A2); T.Add(B); T.Add(B2); } else { T.Add(A); T.Add(A2); T.Add(B); T.Add(A2); T.Add(B2); T.Add(B); }
        }
        float yb = ys[L - 1]; int b0 = (L - 1) * N, c = V.Count; V.Add(new Vector3(0f, yb, 0f)); UV.Add(Vector2.zero);
        for (int i = 0; i < N; i++) { V.Add(new Vector3(V[b0 + i].x, yb, V[b0 + i].z)); UV.Add(new Vector2(V[b0 + i].x * UVS, V[b0 + i].z * UVS)); }
        for (int i = 0; i < N; i++) { int j = (i + 1) % N; if (liner) { T.Add(c); T.Add(c + 1 + j); T.Add(c + 1 + i); } else { T.Add(c); T.Add(c + 1 + i); T.Add(c + 1 + j); } }
        return MeshOf(V, UV, T);
    }
    // a rounded-rectangle ring (the binding round the pouch mouth): rings at heights y with a scale f each, no cap
    static Mesh PrismMesh(float w, float d, float r, float[,] lv)
    {
        var pts = RoundRect(w, d, r, 4); int N = pts.Count, L = lv.GetLength(0); var V = new List<Vector3>(); var UV = new List<Vector2>(); var T = new List<int>();
        var cum = new float[N + 1]; for (int i = 1; i <= N; i++) cum[i] = cum[i - 1] + (pts[i - 1] - pts[i % N]).magnitude;
        for (int l = 0; l < L; l++) for (int i = 0; i < N; i++) { V.Add(new Vector3(pts[i].x * lv[l, 1], lv[l, 0], pts[i].y * lv[l, 1])); UV.Add(new Vector2(cum[i] * UVS, lv[l, 0] * UVS)); }
        for (int k = 0; k < L - 1; k++) for (int i = 0; i < N; i++) { int j = (i + 1) % N, A = k * N + i, A2 = k * N + j, B = (k + 1) * N + i, B2 = (k + 1) * N + j; T.Add(A); T.Add(A2); T.Add(B); T.Add(A2); T.Add(B2); T.Add(B); }
        return MeshOf(V, UV, T);
    }
    // a rounded plate in the XY plane, thickness th along Z (the edge has its own texture coordinates, so the fabric does not streak)
    static Mesh PlateMesh(float w, float h, float r, float th)
    {
        var rr = RoundRect(w, h, r, 4); int N = rr.Count; var V = new List<Vector3>(); var UV = new List<Vector2>(); var T = new List<int>();
        foreach (var p in rr) { V.Add(new Vector3(p.x, p.y, -th / 2)); UV.Add(p * UVS); }
        foreach (var p in rr) { V.Add(new Vector3(p.x, p.y, th / 2)); UV.Add(p * UVS); }
        int cf = V.Count; V.Add(new Vector3(0, 0, -th / 2)); UV.Add(Vector2.zero); int cb = V.Count; V.Add(new Vector3(0, 0, th / 2)); UV.Add(Vector2.zero);
        for (int i = 0; i < N; i++) { int j = (i + 1) % N; T.Add(cf); T.Add(j); T.Add(i); T.Add(cb); T.Add(N + i); T.Add(N + j); }
        int w0 = V.Count; float cum = 0f;
        for (int i = 0; i <= N; i++) { var a = rr[i % N]; if (i > 0) cum += (a - rr[i - 1]).magnitude; V.Add(new Vector3(a.x, a.y, -th / 2)); UV.Add(new Vector2(cum * UVS, 0f)); V.Add(new Vector3(a.x, a.y, th / 2)); UV.Add(new Vector2(cum * UVS, th * UVS)); }
        for (int i = 0; i < N; i++) { int A = w0 + 2 * i, B = A + 1, A2 = A + 2, B2 = A + 3; T.Add(A2); T.Add(B2); T.Add(A); T.Add(B2); T.Add(B); T.Add(A); }
        return MeshOf(V, UV, T);
    }
    static Mesh RotMesh(Mesh m, Quaternion q)
    {
        var v = m.vertices; for (int i = 0; i < v.Length; i++) v[i] = q * v[i]; m.vertices = v; m.RecalculateNormals(); m.RecalculateBounds(); return m;
    }
    // a two-sided mesh with proper shading: the first copy faces along the given normals, the second is its mirror image (reversed winding, negated normals)
    static Mesh TwoSided(List<Vector3> V, List<Vector3> Nr, List<Vector2> UV, List<int> T)
    {
        int n = V.Count; var V2 = new List<Vector3>(V); V2.AddRange(V); var N2 = new List<Vector3>(Nr); foreach (var q in Nr) N2.Add(-q); var UV2 = new List<Vector2>(UV); UV2.AddRange(UV); var T2 = new List<int>(T.Count * 2);
        for (int t = 0; t < T.Count; t += 3)
        {
            int a = T[t], b = T[t + 1], c = T[t + 2];
            if (Vector3.Dot(Vector3.Cross(V[b] - V[a], V[c] - V[a]), Nr[a]) >= 0f) { T2.Add(a); T2.Add(b); T2.Add(c); T2.Add(n + c); T2.Add(n + b); T2.Add(n + a); }
            else { T2.Add(a); T2.Add(c); T2.Add(b); T2.Add(n + a); T2.Add(n + b); T2.Add(n + c); }
        }
        var m = new Mesh(); m.SetVertices(V2); m.SetNormals(N2); m.SetUVs(0, UV2); m.SetTriangles(T2, 0); m.RecalculateBounds(); return m;
    }
    // stitching: dashed thread lines (flat ribbons) along line segments, in the local XY plane at z = 0
    static Mesh DashMesh(List<Vector2[]> lines, float len, float gap, float wd)
    {
        var V = new List<Vector3>(); var Nr = new List<Vector3>(); var UV = new List<Vector2>(); var T = new List<int>();
        foreach (var ln in lines)
        {
            Vector2 a = ln[0], b = ln[1]; float L = (b - a).magnitude; if (L < 1e-5f) continue; Vector2 u = (b - a) / L, nn = new Vector2(-u.y, u.x) * wd * 0.5f;
            for (float t = 0f; t + len <= L; t += len + gap)
            {
                Vector2 p0 = a + u * t, p1 = p0 + u * len; int n = V.Count;
                V.Add(new Vector3(p0.x + nn.x, p0.y + nn.y, 0f)); V.Add(new Vector3(p0.x - nn.x, p0.y - nn.y, 0f)); V.Add(new Vector3(p1.x - nn.x, p1.y - nn.y, 0f)); V.Add(new Vector3(p1.x + nn.x, p1.y + nn.y, 0f));
                for (int q = 0; q < 4; q++) { Nr.Add(Vector3.forward); UV.Add(Vector2.zero); }
                T.Add(n); T.Add(n + 1); T.Add(n + 2); T.Add(n); T.Add(n + 2); T.Add(n + 3);
            }
        }
        return TwoSided(V, Nr, UV, T);
    }
    static List<Vector2[]> RRLines(float w, float h, float r)
    {
        var pts = RoundRect(w, h, r, 4); var L = new List<Vector2[]>(); for (int i = 0; i < pts.Count; i++) L.Add(new[] { pts[i], pts[(i + 1) % pts.Count] }); return L;
    }
    static Mesh FrusMesh(float yT, float czT, float rxT, float rzT, float yB, float czB, float rxB, float rzB, int seg)   // an elliptic frustum (open), seen from both sides; the fabric pattern keeps one world scale
    {
        var V = new List<Vector3>(); var Nr = new List<Vector3>(); var UV = new List<Vector2>(); var T = new List<int>();
        float tilesT = Mathf.Max(1f, Mathf.Round(UVS * 2f * Mathf.PI * Mathf.Sqrt((rxT * rxT + rzT * rzT) / 2f))), tilesB = Mathf.Max(1f, Mathf.Round(UVS * 2f * Mathf.PI * Mathf.Sqrt((rxB * rxB + rzB * rzB) / 2f)));
        for (int i = 0; i <= seg; i++)
        {
            float a = i * 2f * Mathf.PI / seg; var nn = new Vector2(Mathf.Sin(a) / Mathf.Max(rxT, rxB), Mathf.Cos(a) / Mathf.Max(rzT, rzB)).normalized; var nv = new Vector3(nn.x, 0f, nn.y);
            V.Add(new Vector3(rxT * Mathf.Sin(a), yT, czT + rzT * Mathf.Cos(a))); Nr.Add(nv); UV.Add(new Vector2((float)i / seg * tilesT, yT * UVS));
            V.Add(new Vector3(rxB * Mathf.Sin(a), yB, czB + rzB * Mathf.Cos(a))); Nr.Add(nv); UV.Add(new Vector2((float)i / seg * tilesB, yB * UVS));
        }
        for (int i = 0; i < seg; i++) { int A = 2 * i, B = 2 * i + 1, A2 = 2 * i + 2, B2 = 2 * i + 3; T.Add(A); T.Add(B); T.Add(A2); T.Add(A2); T.Add(B); T.Add(B2); }
        return TwoSided(V, Nr, UV, T);
    }
    // a lofted body part: rings [y, cx, cz, rx, rz] smoothed with Catmull-Rom; fold(a, y) wobbles the radius (creases, blousing). Used for the tunic, collar, neck, head, sleeves and hands.
    static Mesh LoftMesh(float[][] R, int m, int seg, System.Func<float, float, float> fold, bool single = false)
    {
        float CR(float p0, float p1, float p2, float p3, float t) => 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
        var D = new List<float[]>();
        for (int k = 0; k < R.Length - 1; k++) for (int q = 0; q < m; q++)
        {
            float t = (float)q / m; var A = R[Mathf.Max(0, k - 1)]; var B = R[k]; var C = R[k + 1]; var E = R[Mathf.Min(R.Length - 1, k + 2)]; var d = new float[5];
            for (int j = 0; j < 5; j++) d[j] = CR(A[j], B[j], C[j], E[j], t); D.Add(d);
        }
        D.Add((float[])R[R.Length - 1].Clone());
        int NR = D.Count; var V = new List<Vector3>(); var UV = new List<Vector2>(); var T = new List<int>(); var Nr = new List<Vector3>();
        for (int r = 0; r < NR; r++)
        {
            var c = D[r]; float tiles = Mathf.Max(1f, Mathf.Round(UVS * 2f * Mathf.PI * Mathf.Sqrt((c[3] * c[3] + c[4] * c[4]) / 2f)));
            for (int i = 0; i <= seg; i++) { float a = i * 2f * Mathf.PI / seg; float f = fold != null ? fold(a, c[0]) : 1f; V.Add(new Vector3(c[1] + c[3] * f * Mathf.Sin(a), c[0], c[2] + c[4] * f * Mathf.Cos(a))); UV.Add(new Vector2((float)i / seg * tiles, c[0] * UVS)); }
        }
        for (int r = 0; r < NR; r++) for (int i = 0; i <= seg; i++)
        {
            int ii = i % seg; Vector3 du = V[r * (seg + 1) + (ii + 1) % seg] - V[r * (seg + 1) + (ii - 1 + seg) % seg], dy = V[Mathf.Min(r + 1, NR - 1) * (seg + 1) + i] - V[Mathf.Max(r - 1, 0) * (seg + 1) + i];
            Vector3 n = Vector3.Cross(du, dy).normalized; var o = V[r * (seg + 1) + i]; if (n.x * (o.x - D[r][1]) + n.z * (o.z - D[r][2]) < 0f) n = -n; Nr.Add(n);
        }
        for (int r = 0; r < NR - 1; r++) for (int i = 0; i < seg; i++) { int A = r * (seg + 1) + i, B = A + seg + 1; T.Add(A); T.Add(B); T.Add(A + 1); T.Add(A + 1); T.Add(B); T.Add(B + 1); }
        if (!single) return TwoSided(V, Nr, UV, T);
        var T1 = new List<int>(T.Count);   // one side only (outward): the head must not be seen from inside by the first-person camera
        for (int t = 0; t < T.Count; t += 3) { int a = T[t], b = T[t + 1], c = T[t + 2]; if (Vector3.Dot(Vector3.Cross(V[b] - V[a], V[c] - V[a]), Nr[a]) >= 0f) { T1.Add(a); T1.Add(b); T1.Add(c); } else { T1.Add(a); T1.Add(c); T1.Add(b); } }
        var m1 = new Mesh(); m1.SetVertices(V); m1.SetNormals(Nr); m1.SetUVs(0, UV); m1.SetTriangles(T1, 0); m1.RecalculateBounds(); return m1;
    }
    static Mesh BeltMesh()   // a webbing belt: an elliptic ring round the waist (outer, top, inner and bottom faces)
    {
        const int N = 120; var V = new List<Vector3>(); var Nr = new List<Vector3>(); var UV = new List<Vector2>(); var T = new List<int>(); float t = BeT, h = BeH;
        for (int i = 0; i < N; i++)
        {
            float th = i * 2f * Mathf.PI / N, px = BeA * Mathf.Sin(th), pz = BeCz + BeB * Mathf.Cos(th); var n = new Vector2(Mathf.Sin(th) / BeA, Mathf.Cos(th) / BeB).normalized;
            float[,] f = { { t / 2, h / 2, t / 2, -h / 2 }, { t / 2, h / 2, -t / 2, h / 2 }, { -t / 2, h / 2, -t / 2, -h / 2 }, { t / 2, -h / 2, -t / 2, -h / 2 } };
            Vector3[] nr = { new Vector3(n.x, 0f, n.y), Vector3.up, new Vector3(-n.x, 0f, -n.y), Vector3.down };
            for (int q = 0; q < 4; q++) for (int e = 0; e < 2; e++) { float dn = f[q, e * 2], dy = f[q, e * 2 + 1]; V.Add(new Vector3(px + n.x * dn, BeY + dy, pz + n.y * dn)); Nr.Add(nr[q]); UV.Add(new Vector2((float)i / N * 20f, dy > 0 ? 1f : 0f)); }
        }
        for (int i = 0; i < N; i++) { int j = (i + 1) % N; for (int q = 0; q < 4; q++) { int A = i * 8 + q * 2, A1 = A + 1, B = j * 8 + q * 2, B1 = B + 1; T.Add(A); T.Add(B); T.Add(A1); T.Add(A1); T.Add(B); T.Add(B1); } }
        return TwoSided(V, Nr, UV, T);
    }
    void BuildWaist()
    {
        if (waistRoot != null || holder == null || cam == null) return;
        Transform par = cam.transform.parent != null ? cam.transform.parent : cam.transform;
        waistRoot = new GameObject("waist_gear").transform; waistRoot.SetParent(par, false); waistRoot.localPosition = par == cam.transform ? Vector3.zero : cam.transform.localPosition; waistRoot.localRotation = Quaternion.identity;
        if (camoTex == null)
        {
            // SAF-style digital camouflage (the palette was measured from a reference photo): 32 x 32 cells of 10 mm (8 x 8 texels each) clustered into blobs by three octaves of tileable value noise, with a plain-weave pattern and fine noise
            const int G = 32; long seed = 7; float Rnd() { seed = (seed * 16807L) % 2147483647L; return (float)seed / 2147483647f; }
            camoTex = new Texture2D(256, 256, TextureFormat.RGBA32, true) { filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            float[,] pal = { { 14, 26, 14 }, { 34, 52, 30 }, { 62, 88, 48 }, { 104, 122, 76 }, { 100, 92, 58 }, { 62, 48, 28 } };
            float[] MakeLat(int n) { var l = new float[n * n]; for (int k = 0; k < l.Length; k++) l[k] = Rnd(); return l; }
            float Oct(float[] lat, int n, float x, float y)
            {
                float fx = x / G * n, fy = y / G * n; int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy); float tx = fx - x0, ty = fy - y0; tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty);
                float L(int a, int b2) => lat[(((b2 % n) + n) % n) * n + (((a % n) + n) % n)];
                return (L(x0, y0) * (1f - tx) + L(x0 + 1, y0) * tx) * (1f - ty) + (L(x0, y0 + 1) * (1f - tx) + L(x0 + 1, y0 + 1) * tx) * ty;
            }
            var l4 = MakeLat(4); var l8 = MakeLat(8); var l16 = MakeLat(16); var w8l = MakeLat(8); var grl = MakeLat(4);
            var val = new float[G * G]; for (int y = 0; y < G; y++) for (int x = 0; x < G; x++) val[val.Length > 0 ? y * G + x : 0] = 0.45f * Oct(l4, 4, x, y) + 0.3f * Oct(l8, 8, x, y) + 0.25f * Oct(l16, 16, x, y) + (Rnd() - 0.5f) * 0.18f;
            var order = new List<int>(); for (int k = 0; k < G * G; k++) order.Add(k); order.Sort((a2, b2) => { int c2 = val[a2].CompareTo(val[b2]); return c2 != 0 ? c2 : a2.CompareTo(b2); });
            float[] fr = { 0.18f, 0.20f, 0.25f, 0.22f }; var cell = new int[G * G]; { int c = 0; float acc = 0f; for (int r = 0; r < order.Count; r++) { while (c < 3 && r >= Mathf.Round((acc + fr[c]) * G * G)) { acc += fr[c]; c++; } cell[order[r]] = c; } }
            for (int y = 0; y < G; y++) for (int x = 0; x < G; x++) { float wv = Oct(w8l, 8, x, y) + (Rnd() - 0.5f) * 0.25f; if (wv > 0.78f) cell[y * G + x] = 4; else if (wv < 0.2f && cell[y * G + x] < 2) cell[y * G + x] = 5; }
            var px = new Color32[256 * 256];
            for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
            {
                int ci = cell[(y >> 3) * G + (x >> 3)]; float gm = 0.84f + 0.22f * Oct(grl, 4, (x >> 3) + 5, (y >> 3) + 11); float wv = gm * (0.90f + 0.10f * ((x & 1) ^ (y & 1)) + 0.04f * (((x >> 1) & 1) ^ ((y >> 1) & 1)) + (Rnd() - 0.5f) * 0.05f);
                px[y * 256 + x] = new Color32((byte)Mathf.Min(255f, pal[ci, 0] * wv), (byte)Mathf.Min(255f, pal[ci, 1] * wv), (byte)Mathf.Min(255f, pal[ci, 2] * wv), 255);
            }
            camoTex.SetPixels32(px); camoTex.Apply(true);
        }
        Material web = Matte(new Color(0.227f, 0.282f, 0.2f)), buck = Matte(new Color(0.251f, 0.278f, 0.184f)), dark = Matte(new Color(0.059f, 0.067f, 0.043f)), lin = Matte(new Color(0.169f, 0.2f, 0.141f)), thread = Matte(new Color(0.42f, 0.451f, 0.345f)), slotM = Matte(new Color(0.29f, 0.329f, 0.22f)), pipe = Matte(new Color(0.353f, 0.424f, 0.286f)), bootM = Matte(new Color(0.078f, 0.067f, 0.051f));
        Material CamoMat(Color tint) { var mm = Matte(tint); mm.mainTexture = camoTex; if (mm.HasProperty("_BaseMap")) mm.SetTexture("_BaseMap", camoTex); return mm; }
        Transform Mk(Transform p, string n, Mesh m, Material mat, Vector3 pos)
        {
            var g = new GameObject(n); g.AddComponent<MeshFilter>().sharedMesh = m; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            g.transform.SetParent(p, false); g.transform.localPosition = pos; return g.transform;
        }
        Transform Bx(Transform p, string n, Vector3 size, Vector3 pos, Material mat)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = n; var col = g.GetComponent<Collider>(); if (col != null) Destroy(col);
            g.transform.SetParent(p, false); g.transform.localScale = size; g.transform.localPosition = pos;
            var r = g.GetComponent<Renderer>(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; r.sharedMaterial = mat; return g.transform;
        }
        Material camo = CamoMat(Color.white), camoVest = CamoMat(new Color(0.71f, 0.71f, 0.71f));   // the carrier is a little darker than the pouches   // the one fabric: tunic, trousers, vest, pouches
        Transform MakePouch(Transform pp, PouchSpec o)
        {
            float W = o.W, D = o.D, H = o.H, dropH = o.dropH, r = 0.016f, LD = D + 0.016f, hz = D / 2 + 0.005f;
            Mk(pp, "body", SoftMesh(W, D, H, r, o.nR, o.bB, o.bR, false), camo, Vector3.zero);
            Mk(pp, "lining", SoftMesh(W, D, H, r, o.nR, o.bB, o.bR, true), lin, Vector3.zero);
            Mk(pp, "binding", PrismMesh(W + 0.008f, D + 0.008f, r + 0.004f, new float[,] { { 0.001f, 1f }, { -0.007f, 1f } }), pipe, Vector3.zero);
            var fl = new GameObject("flap").transform; fl.SetParent(pp, false); fl.localPosition = new Vector3(0f, 0f, hz);
            Quaternion qx = Quaternion.Euler(86f, 0f, 0f);   // the lid slopes down towards the front over the contents
            Mk(fl, "lid_pipe", RotMesh(PlateMesh(W + 0.016f, LD + 0.008f, 0.026f, 0.008f), qx), pipe, new Vector3(0f, 0.0065f, -LD / 2));
            Mk(fl, "drop_pipe", PlateMesh(W + 0.016f, dropH + 0.018f, 0.026f, 0.008f), pipe, new Vector3(0f, -(dropH + 0.010f) / 2, -LD - 0.0055f));
            Mk(fl, "lid", RotMesh(PlateMesh(W + 0.008f, LD, 0.022f, 0.012f), qx), camo, new Vector3(0f, 0.008f, -LD / 2));
            Mk(fl, "drop", PlateMesh(W + 0.008f, dropH + 0.010f, 0.022f, 0.012f), camo, new Vector3(0f, -(dropH + 0.010f) / 2 + 0.003f, -LD - 0.0065f));
            float sw = Mathf.Min(0.035f, W * 0.4f);
            Bx(fl, "strap_top", new Vector3(sw, 0.004f, LD - 0.008f), new Vector3(0f, 0.016f, -LD / 2), web);
            Bx(fl, "strap_drop", new Vector3(sw, dropH - 0.01f, 0.004f), new Vector3(0f, -(dropH - 0.01f) / 2 - 0.003f, -LD - 0.0145f), web);
            Bx(fl, "buckle", new Vector3(0.030f, 0.020f, 0.008f), new Vector3(0f, -dropH + 0.002f, -LD - 0.0185f), buck); Bx(fl, "buckle_bar", new Vector3(0.018f, 0.008f, 0.009f), new Vector3(0f, -dropH + 0.002f, -LD - 0.0195f), dark);
            Bx(fl, "clip", new Vector3(0.004f, 0.016f, 0.006f), new Vector3(-0.0165f, -dropH + 0.002f, -LD - 0.0165f), buck); Bx(fl, "clip", new Vector3(0.004f, 0.016f, 0.006f), new Vector3(0.0165f, -dropH + 0.002f, -LD - 0.0165f), buck);
            Bx(pp, "male_tab", new Vector3(0.040f, 0.022f, 0.004f), new Vector3(0f, -dropH - 0.011f, -D / 2 - 0.009f), web); Bx(pp, "male_plug", new Vector3(0.012f, 0.012f, 0.006f), new Vector3(0f, -dropH - 0.011f, -D / 2 - 0.012f), dark);
            // stitching: the flap edges and a box stitch (square with a cross) where the strap is sewn on
            var st = Mk(fl, "stitch_lid", RotMesh(DashMesh(RRLines(W - 0.006f - 0.014f, LD - 0.014f - 0.014f, 0.012f), 0.0032f, 0.0022f, 0.0008f), qx), thread, new Vector3(0f, 0.0145f, -LD / 2));
            Mk(fl, "stitch_drop", DashMesh(RRLines(W - 0.006f - 0.014f, dropH - 0.004f, 0.012f), 0.0032f, 0.0022f, 0.0008f), thread, new Vector3(0f, -(dropH + 0.010f) / 2 + 0.003f, -LD - 0.0133f));
            float hw = sw / 2 - 0.003f, y1 = -(dropH - 0.01f) - 0.003f + 0.004f, y0 = y1 + 0.016f;
            Mk(fl, "stitch_box", DashMesh(new List<Vector2[]> { new[] { new Vector2(-hw, y0), new Vector2(hw, y0) }, new[] { new Vector2(-hw, y1), new Vector2(hw, y1) }, new[] { new Vector2(-hw, y0), new Vector2(-hw, y1) }, new[] { new Vector2(hw, y0), new Vector2(hw, y1) }, new[] { new Vector2(-hw, y0), new Vector2(hw, y1) }, new[] { new Vector2(hw, y0), new Vector2(-hw, y1) } }, 0.0022f, 0.0016f, 0.0007f), thread, new Vector3(0f, 0f, -LD - 0.0173f));
            if (o.pals) foreach (float y in new[] { -0.014f, -H + 0.016f })   // snap straps on the back, through the vest's PALS ladder
            {
                Bx(pp, "pals_strap", new Vector3(W * 0.75f, 0.0254f, 0.004f), new Vector3(0f, y, D / 2 + 0.003f), web);
                for (int sx = -1; sx <= 1; sx += 2) Bx(pp, "snap", new Vector3(0.008f, 0.008f, 0.003f), new Vector3(sx * W * 0.28f, y, D / 2 + 0.0055f), buck);
            }
            return fl;
        }
        Material skinM = Matte(new Color(0.063f, 0.067f, 0.063f));   // black mask and gloves
        Mk(waistRoot, "tunic", LoftMesh(new[] { new[] { -0.62f, 0f, -0.15f, 0.165f, 0.115f }, new[] { -0.50f, 0f, -0.16f, 0.180f, 0.118f }, new[] { -0.38f, 0f, -0.165f, 0.195f, 0.118f }, new[] { -0.28f, 0f, -0.168f, 0.205f, 0.112f }, new[] { -0.225f, 0f, -0.17f, 0.20f, 0.10f }, new[] { -0.195f, 0f, -0.17f, 0.13f, 0.085f }, new[] { -0.175f, 0f, -0.17f, 0.068f, 0.068f } }, 4, 48,
            (a2, y2) => 1f + 0.005f * Mathf.Sin(a2 * 7f + y2 * 23f) + (y2 < -0.5f ? 0.04f * Mathf.Min(1f, (-0.5f - y2) / 0.08f) * (0.5f + 0.5f * Mathf.Sin(a2 * 13f + 1f)) : 0f)), camo, Vector3.zero);   // tunic with sloped shoulders and a little blousing over the belt
        Mk(waistRoot, "collar", LoftMesh(new[] { new[] { -0.205f, 0f, -0.17f, 0.088f, 0.082f }, new[] { -0.18f, 0f, -0.17f, 0.078f, 0.074f }, new[] { -0.14f, 0f, -0.172f, 0.066f, 0.064f }, new[] { -0.125f, 0f, -0.172f, 0.064f, 0.062f } }, 4, 40, (a2, y2) => 1f + 0.006f * Mathf.Sin(a2 * 9f)), camo, Vector3.zero);   // stand collar (part of the uniform)
        Bx(waistRoot, "collar_zip", new Vector3(0.010f, 0.06f, 0.003f), new Vector3(0f, -0.168f, -0.172f + 0.071f), buck);
        Mk(waistRoot, "neck", LoftMesh(new[] { new[] { -0.18f, 0f, -0.17f, 0.050f, 0.050f }, new[] { -0.10f, 0f, -0.15f, 0.046f, 0.048f }, new[] { -0.06f, 0f, -0.13f, 0.045f, 0.047f } }, 3, 24, null, true), skinM, Vector3.zero);
        { var HR = new float[13][]; for (int k = 0; k <= 12; k++) { float ph = k / 12f * Mathf.PI; HR[k] = new[] { 0.115f * Mathf.Cos(ph), 0f, -0.085f, Mathf.Max(0.001f, 0.078f * Mathf.Sin(ph)), Mathf.Max(0.001f, 0.098f * Mathf.Sin(ph)) }; } Mk(waistRoot, "head", LoftMesh(HR, 1, 24, null, true), skinM, Vector3.zero); }   // a placeholder head (behind the camera's near plane, seen only in third person)
        for (int sx = -1; sx <= 1; sx += 2)
        {
            float[][] arm = { new[] { -0.185f, 0.15f, -0.17f, 0.03f, 0.03f }, new[] { -0.215f, 0.205f, -0.17f, 0.066f, 0.066f }, new[] { -0.30f, 0.226f, -0.17f, 0.057f, 0.057f }, new[] { -0.45f, 0.236f, -0.165f, 0.049f, 0.049f }, new[] { -0.55f, 0.236f, -0.13f, 0.044f, 0.044f }, new[] { -0.635f, 0.226f, -0.09f, 0.037f, 0.037f }, new[] { -0.65f, 0.226f, -0.088f, 0.033f, 0.033f } };
            float[][] hand = { new[] { -0.65f, 0.226f, -0.088f, 0.033f, 0.033f }, new[] { -0.675f, 0.226f, -0.082f, 0.042f, 0.026f }, new[] { -0.715f, 0.226f, -0.07f, 0.040f, 0.024f }, new[] { -0.75f, 0.226f, -0.06f, 0.030f, 0.018f }, new[] { -0.775f, 0.226f, -0.054f, 0.012f, 0.012f }, new[] { -0.785f, 0.226f, -0.052f, 0.004f, 0.004f } };
            foreach (var rr in arm) rr[1] *= sx; foreach (var rr in hand) rr[1] *= sx;
            Mk(waistRoot, "sleeve", LoftMesh(arm, 4, 24, (a2, y2) => 1f + 0.008f * Mathf.Sin(a2 * 5f + y2 * 40f)), camo, Vector3.zero);
            Mk(waistRoot, "hand", LoftMesh(hand, 3, 16, null), skinM, Vector3.zero);
        }
        Mk(waistRoot, "trousers", FrusMesh(-0.66f, BeCz, 0.17f, 0.118f, -0.95f, -0.12f, 0.17f, 0.115f, 36), camo, Vector3.zero);
        for (int sx = -1; sx <= 1; sx += 2)
        {
            Mk(waistRoot, "leg", FrusMesh(-0.93f, -0.10f, 0.085f, 0.085f, -1.52f, -0.06f, 0.062f, 0.062f, 20), camo, new Vector3(sx * 0.09f, 0f, 0f));
            Mk(waistRoot, "boot", FrusMesh(-1.50f, -0.06f, 0.058f, 0.058f, -1.60f, 0.03f, 0.052f, 0.125f, 20), bootM, new Vector3(sx * 0.09f, 0f, 0f));
            Mk(waistRoot, "sole", FrusMesh(-1.595f, 0.03f, 0.054f, 0.127f, -1.605f, 0.03f, 0.054f, 0.127f, 20), dark, new Vector3(sx * 0.09f, 0f, 0f));
        }
        // the load-bearing vest: shell, shoulder pads, two PALS ladder rows above the pouches, centre closure, three magazine pouches snapped onto the ladder
        Mk(waistRoot, "vest", FrusMesh(-0.24f, -0.165f, 0.212f, 0.128f, -0.57f, -0.155f, 0.20f, 0.125f, 36), camoVest, Vector3.zero);
        {   // the laser-cut MOLLE panel on the front of the carrier: rows of rounded slots (decals on the shell)
            var Vs = new List<Vector3>(); var Ns = new List<Vector3>(); var UVs = new List<Vector2>(); var Ts = new List<int>(); const float sw = 0.036f, sh = 0.0115f, ch = 0.003f;
            float[,] ring = { { -sw / 2 + ch, sh / 2 }, { sw / 2 - ch, sh / 2 }, { sw / 2, sh / 2 - ch }, { sw / 2, -sh / 2 + ch }, { sw / 2 - ch, -sh / 2 }, { -sw / 2 + ch, -sh / 2 }, { -sw / 2, -sh / 2 + ch }, { -sw / 2, sh / 2 - ch } };
            foreach (float yc in new[] { -0.282f, -0.320f, -0.358f })
            {
                float tt = (-0.24f - yc) / 0.33f, cz = -0.165f + 0.01f * tt, rx = 0.212f - 0.012f * tt, rz = 0.128f - 0.003f * tt;
                for (int c = -3; c <= 2; c++)
                {
                    float xc = (c + 0.5f) * 0.05f, zc = cz + rz * Mathf.Sqrt(1f - (xc / rx) * (xc / rx)), qx_ = xc / (rx * rx), qz_ = (zc - cz) / (rz * rz), nl = Mathf.Sqrt(qx_ * qx_ + qz_ * qz_), nx = qx_ / nl, nz = qz_ / nl, tx = nz, tz = -nx; int n0 = Vs.Count;
                    for (int k = 0; k < 8; k++) { Vs.Add(new Vector3(xc + nx * 0.0016f + tx * ring[k, 0], yc + ring[k, 1], zc + nz * 0.0016f + tz * ring[k, 0])); Ns.Add(new Vector3(nx, 0f, nz)); UVs.Add(Vector2.zero); }
                    for (int k = 1; k < 7; k++) { Ts.Add(n0); Ts.Add(n0 + k); Ts.Add(n0 + k + 1); }
                }
            }
            Mk(waistRoot, "molle_slots", TwoSided(Vs, Ns, UVs, Ts), slotM, Vector3.zero);
        }
        Bx(waistRoot, "closure", new Vector3(0.01f, 0.3f, 0.004f), new Vector3(0f, -0.405f, -0.0275f), web);
        foreach (var pr in new[] { (-0.075f, MagL), (0.085f, MagR) })
        {
            float x0 = pr.Item1, y0 = -0.385f, tt = (-0.24f - y0) / 0.33f, cz = -0.165f + 0.01f * tt, rx = 0.212f - 0.012f * tt, rz = 0.128f - 0.003f * tt; PouchSpec sp = pr.Item2;
            float z0 = cz + rz * Mathf.Sqrt(1f - (x0 / rx) * (x0 / rx)); float qx_ = x0 / (rx * rx), qz_ = (z0 - cz) / (rz * rz), nl = Mathf.Sqrt(qx_ * qx_ + qz_ * qz_), nx = qx_ / nl, nz = qz_ / nl;   // the shell's outward normal there
            var mp = new GameObject("vest_pouch").transform; mp.SetParent(waistRoot, false); mp.localPosition = new Vector3(x0 + nx * (sp.D / 2 - 0.004f), y0, z0 + nz * (sp.D / 2 - 0.004f)); mp.localRotation = Quaternion.Euler(0f, Mathf.Atan2(-nx, -nz) * Mathf.Rad2Deg, 0f);
            MakePouch(mp, sp);
        }
        // belt with a buckle at the front, in the same webbing
        Mk(waistRoot, "belt", BeltMesh(), web, Vector3.zero);
        Bx(waistRoot, "belt_buckle", new Vector3(0.045f, 0.045f, 0.008f), new Vector3(0f, BeY, BeCz + BeB + BeT / 2 + 0.003f), buck);
        Bx(waistRoot, "belt_buckle_in", new Vector3(0.03f, 0.03f, 0.009f), new Vector3(0f, BeY, BeCz + BeB + BeT / 2 + 0.004f), web);
        // the utility pouch sits on the belt, back against it, front facing outward
        float bnx = Mathf.Sin(BeTh) / BeA, bnz = Mathf.Cos(BeTh) / BeB, bnl = Mathf.Sqrt(bnx * bnx + bnz * bnz), ux = bnx / bnl, uz = bnz / bnl, dd = IceSpec.D / 2 + BeT / 2 + 0.003f;
        var root = new GameObject("ice_pouch").transform; root.SetParent(waistRoot, false); root.localScale = Vector3.one * PouchK; icePouch = root;
        root.localPosition = new Vector3(-(BeA * Mathf.Sin(BeTh) + ux * dd), BeY + BeH / 2 + 0.012f, BeCz + BeB * Mathf.Cos(BeTh) + uz * dd);
        root.localRotation = Quaternion.Euler(0f, (Mathf.PI - Mathf.Atan2(ux, uz)) * Mathf.Rad2Deg, 0f);
        iceFlap = MakePouch(root, IceSpec);
        // the spare packs standing in the pouch (real size; the one in the middle is the one the animation takes out); how many show follows the stock
        if (icePack != null)
        {
            iceSpares = new Transform[3]; float[] sx3 = { -0.052f, 0.052f, 0f };
            for (int q = 0; q < 3; q++)
            {
                var sp = Instantiate(icePack.gameObject, root); sp.name = "ice_spare"; sp.SetActive(true); sp.transform.localScale = Vector3.one; sp.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); sp.transform.localPosition = new Vector3(sx3[q], -IceSpec.H + 0.0365f, 0f); iceSpares[q] = sp.transform;
            }
        }
        SetLayer(waistRoot, HideLayer);
    }

    // The ice pack: the left hand reaches down to the pouch on the belt (below the view), the flap opens and the hand takes the pack out, carries it up to the bare barrel and wraps it on, then returns to the handguard.
    // Taking it off is the same in reverse: the hand pulls the pack off, carries it down and puts it back into the pouch.
    Transform[] handBones;
    Vector3 PalmWorld() { if (handBones == null) { var l = new List<Transform>(sarLWrist.GetComponentsInChildren<Transform>(true)); l.Remove(sarLWrist); handBones = l.ToArray(); } Vector3 c = Vector3.zero; for (int k = 0; k < handBones.Length; k++) c += handBones[k].position; return c / Mathf.Max(1, handBones.Length); }
    static Vector3 Bez(Vector3 a, Vector3 c, Vector3 b, float t) { float q = 1f - t; return a * (q * q) + c * (2f * q * t) + b * (t * t); }
    void IceAnim()
    {
        iceAnimT += Time.deltaTime / (iceApplying ? 2.2f : 1.7f);
        float u = iceAnimT;
        if (u >= 1f || lcT == null || icePack == null)
        {
            if (iceApplying && !iceApplied) { barrel.PackApply(); iceApplied = true; }
            if (!iceApplying && barrel.PackOn) barrel.PackRemove();
            iceT = iceApplying ? 1f : 0f; iceAnimT = -1f; icePack.localRotation = Quaternion.identity; return;
        }
        BuildWaist();
        if (icePouch == null) { iceAnimT = -1f; return; }
        if (handBones == null) PalmWorld();
        if (palmOffH == Vector3.zero) palmOffH = holder.InverseTransformVector(PalmWorld() - sarLWrist.position);   // wrist to palm, measured in the idle pose
        Vector3 idleP = lwIdleH + palmOffH;                                                        // the palm at idle
        Vector3 mouthW = icePouch.position, upW = waistRoot.up;                                    // the pouch mouth (world), the body's up
        float hov = iceApplying ? 0.07f * (1f - Ramp(u, 0.17f, 0.27f)) : 0.07f * (1f - Ramp(u, 0.56f, 0.66f)) + 0.07f * Ramp(u, 0.74f, 0.84f);   // the palm hovers over the mouth, then goes in
        Vector3 pocketP = holder.InverseTransformPoint(mouthW + upW * (0.02f + hov));            // the palm over the pouch mouth (holder space)
        Vector3 barrelP = holder.InverseTransformPoint(icePack.parent.TransformPoint(iceRest)), gripP = holder.InverseTransformPoint(icePack.parent.TransformPoint(new Vector3(0f, 0f, 0.17f)));
        Vector3 barrelPt = idleP + (barrelP - gripP);
        Vector3 ctrl = (pocketP + barrelPt) * 0.5f + new Vector3(-0.04f, 0.10f, 0f) * sarS;       // the carry rises in a small arc
        Vector3 pp; float grip, orient; int mode;   // pp: the palm; mode: 0 pack in the pouch, 1 in the hand, 2 on the barrel
        if (iceApplying)
        {
            if (u < 0.04f) pp = idleP;
            else if (u < 0.26f) pp = Vector3.Lerp(idleP, pocketP, Ramp(u, 0.04f, 0.26f));
            else if (u < 0.34f) pp = pocketP;
            else if (u < 0.66f) pp = Bez(pocketP, ctrl, barrelPt, Ramp(u, 0.34f, 0.66f));
            else if (u < 0.82f) pp = barrelPt;
            else pp = Vector3.Lerp(barrelPt, idleP, Ramp(u, 0.82f, 1f));
            grip = Mathf.Clamp01(Ramp(u, 0.16f, 0.30f) - Ramp(u, 0.70f, 0.84f));   // the fist closes on the pack in the pouch and opens once it is wrapped on
            mode = u < 0.30f ? 0 : u < 0.68f ? 1 : 2;
            orient = Ramp(u, 0.30f, 0.44f);
            if (u >= 0.68f && !iceApplied) { barrel.PackApply(); iceApplied = true; }
        }
        else
        {
            if (u < 0.20f) pp = Vector3.Lerp(idleP, barrelPt, Ramp(u, 0f, 0.20f));
            else if (u < 0.28f) pp = barrelPt;
            else if (u < 0.62f) pp = Bez(barrelPt, ctrl, pocketP, Ramp(u, 0.28f, 0.62f));
            else if (u < 0.74f) pp = pocketP;
            else pp = Vector3.Lerp(pocketP, idleP, Ramp(u, 0.74f, 1f));
            grip = Mathf.Clamp01(Ramp(u, 0.06f, 0.18f) - Ramp(u, 0.72f, 0.86f));
            mode = u < 0.26f ? 2 : u < 0.70f ? 1 : 0;
            orient = 1f - Ramp(u, 0.56f, 0.70f);
            if (u >= 0.24f && barrel.PackOn) barrel.PackRemove();   // the pack is off the barrel once the hand has pulled it
        }
        for (int k = 0; k < lcT.Length; k++) { lcT[k].localPosition = Vector3.Lerp(pI[k], pM[k], grip); lcT[k].localRotation = Quaternion.Slerp(rI[k], rM[k], grip); }
        // human touches: the hand wraps up and round the barrel and settles with a pat; a little sway throughout
        {
            float r = 0.07f * sarS, th, w, pat = 0f;
            if (iceApplying) { th = -1.2f * (1f - Ramp(u, 0.66f, 0.84f)); w = Ramp(u, 0.52f, 0.64f); if (u >= 0.84f && u < 0.92f) pat = Mathf.Sin((u - 0.84f) / 0.08f * Mathf.PI) * 0.012f; }
            else { th = -1.2f * Ramp(u, 0.08f, 0.24f); w = 1f - Ramp(u, 0.26f, 0.38f); }
            pp += new Vector3(r * Mathf.Sin(th), r * (Mathf.Cos(th) - 1f) - pat * sarS, 0f) * w;
            pp += new Vector3(0.004f * Mathf.Sin(u * 23f), 0.004f * Mathf.Sin(u * 17f + 1f), 0f) * sarS;
        }
        // the arm is two bones: the shoulder is fixed (out of view), the forearm and upper arm keep their lengths and the elbow bends; the wrist is placed so that the PALM lands on the target
        for (int it = 0; it < 3; it++)
        {
            Vector3 Wt = pp - (holder.InverseTransformPoint(PalmWorld()) - holder.InverseTransformPoint(sarLWrist.position));
            Vector3 Ww = holder.TransformPoint(Wt);
            if (sarElbow != null) { Vector3 Ew = holder.TransformPoint(ElbowFor(Wt)); sarLArm.rotation = Quaternion.FromToRotation(sarElbow.position - sarLWrist.position, Ew - Ww) * sarLArm.rotation; }
            sarLArm.position += Ww - sarLWrist.position;
        }
        // the pouch flap (hinged at the back, opens upward)
        float flapOpen = iceApplying ? Mathf.Min(Ramp(u, 0.14f, 0.24f), 1f - Ramp(u, 0.36f, 0.44f)) : Mathf.Min(Ramp(u, 0.52f, 0.62f), 1f - Ramp(u, 0.74f, 0.82f));
        iceFlap.localRotation = Quaternion.Euler(1.9f * Mathf.Rad2Deg * flapOpen, 0f, 0f);
        Quaternion upright = Quaternion.FromToRotation(icePack.parent.forward, upW) * icePack.parent.rotation;
        float packScale = 1f;
        if (mode == 1) { icePack.position = PalmWorld() + icePack.parent.forward * (0.075f * orient); icePack.rotation = Quaternion.Slerp(upright, icePack.parent.rotation, orient); }
        else if (mode == 0) { float dn = iceApplying ? 1f - Ramp(u, 0.20f, 0.30f) : Ramp(u, 0.74f, 0.88f); icePack.position = mouthW - upW * ((IceSpec.H - 0.0365f) * dn); icePack.rotation = upright; if (flapOpen < 0.3f) packScale = 0f; }   // the pack stands in the pouch and is drawn up by the hand
        else { icePack.localPosition = iceRest; icePack.localRotation = Quaternion.identity; }
        iceT = Mathf.Sqrt(Mathf.Clamp01(packScale));   // the pack's size (see Update)
        float sizeK = mode == 2 ? 1f : Mathf.Lerp(1f / Mathf.Max(0.01f, icePack.parent.lossyScale.x), 1f, grip);   // real size in the pouch, the viewmodel's size once in the hand
        icePack.localScale = Vector3.one * Mathf.Max(0.01f, packScale * sizeK);
    }

    static float Pulse(float x, float c, float w) { float q = (x - c) / w; return Mathf.Exp(-q * q); }
    static float Ramp(float x, float a, float b) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }

    void Sample()
    {
        if (cur < 0 || clips[cur] == null || animRoots[cur] == null) return;
        clips[cur].SampleAnimation(animRoots[cur], (Defs[cur].sar && (st == St.Reload || st == St.Take || (st == St.Fire && aim.AimAmount > 0.5f))) ? Defs[cur].idle.a / 30f : Mathf.Min(frame / 30f, clips[cur].length));
    }

    void Update()
    {
        if (d == null) return;
        var kb = Keyboard.current; var mouse = Mouse.current;
        if (kb == null || mouse == null) return;
        bool ok = !aim.WheelOpen && Cursor.lockState == CursorLockMode.Locked;
        float dt = Time.deltaTime;
        kick = Mathf.MoveTowards(kick, 0f, dt * 9f);
        barrel.Step(Mathf.Min(dt, 0.25f), Atmosphere.AirTempC, Atmosphere.WindMs, hvVel.magnitude);
        if (barrel.PackOn && (!d.sar || barrel.PackSpent)) barrel.PackRemove();   // a spent pack (all ice melted) or another weapon: the pack comes off
        if (iceT > 0.9f) barrel.PackStep(Mathf.Min(dt, 0.25f));   // it only starts to work once it is wrapped on
        UpdateCasings(dt);
        if (!d.sar || st != St.Idle || aim.AimAmount > 0.3f) iceAnimT = -1f;   // the animation only plays on the idle rifle, not while aiming, firing or reloading
        if (iceAnimT < 0f) { iceT = Mathf.MoveTowards(iceT, barrel.PackOn ? 1f : 0f, dt * 4f); if (icePack != null) icePack.localPosition = iceRest; }
        if (icePack != null) { bool vis = d.sar && iceT > 0.001f; if (icePack.gameObject.activeSelf != vis) icePack.gameObject.SetActive(vis); icePack.localScale = Vector3.one * Mathf.Max(0.01f, iceT * iceT * (3f - 2f * iceT)); }   // cools in the air it is in, faster when it moves through it
        if (kb.f6Key.wasPressedThisFrame) Atmosphere.Cycle();
        float gapT = 7f + kick * 9f + Mathf.Clamp01(Rise / 400f) * 14f + Mathf.Min(hvVel.magnitude, 8f) * 1.3f + (sprintW > 0.5f ? 10f : 0f);
        crossGap = Mathf.Lerp(crossGap, gapT, 1f - Mathf.Exp(-dt * 14f));
        if (d.sar)
        {
            ApplyBarrelGlow();
            // nothing is hidden while aiming: the glass at the eye end covers the bore, so every solid part stays opaque throughout the animation
        }
        if (flashT > 0f) { flashT -= dt; if (flashT <= 0f) flash.enabled = false; }

        frame += dt * 30f;
        if (frame >= play.b)
        {
            if (play.loop) frame = play.a + (frame - play.b) % Mathf.Max(1, play.b - play.a);
            else
            {
                frame = play.b;
                if (st == St.Reload) ammo[cur] = d.cap;
                if (st != St.Idle) { st = St.Idle; play = d.idle; frame = d.idle.a; }
            }
        }
        if (d.sar && st == St.Idle) frame = d.idle.a;   // heavy rifle: hold still, no idle sway
        Sample();
        if (!ok || models[cur] == null) return;

        if (st == St.Idle || (d.sar && st == St.Fire))   // the SAR-21 fires on its own cyclic timer, not on the length of the artist's AK animation
        {
            if (iceAnimT < 0f && kb.rKey.wasPressedThisFrame && d.cap > 0 && ammo[cur] < d.cap) { reloadFull = ammo[cur] > 0 && d.reloadFull.Has; Begin(St.Reload, reloadFull ? d.reloadFull : d.reload); return; }
            if (iceAnimT < 0f && kb.vKey.wasPressedThisFrame && d.bash.Has) { Begin(St.Bash, d.bash); Hit(d.melee ? d.range : 1.6f, d.damage); return; }
            if (kb.bKey.wasPressedThisFrame && !d.melee) modeIdx[cur] = (modeIdx[cur] + 1) % Modes[cur].Length;
            if (kb.fKey.wasPressedThisFrame && d.sar && st == St.Idle && iceAnimT < 0f)
            {
                bool has = barrel.PackStock > 0 || (barrel.PackE > 0.0 && !barrel.PackSpent);
                if (aim.AimAmount > 0.3f || lcT == null || icePack == null) { if (barrel.PackOn) barrel.PackRemove(); else barrel.PackApply(); }   // aiming: no room for the hand animation, it is done at once
                else if (barrel.PackOn) { iceAnimT = 0f; iceApplying = false; }
                else if (has) { iceAnimT = 0f; iceApplying = true; iceApplied = false; }
            }
            var fm = Mode;
            bool want = fm == FireMode.Auto ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;
            if (want && fm == FireMode.Single && !d.melee && Time.time < nextSingle) want = false;
            if (want && d.sar && Time.time < nextShotT) want = false;
            if (want && iceAnimT >= 0f) want = false;
            if (want && d.sar && barrel.PackOn) { barrel.PackRemove(); nextShotT = Time.time + 0.35f; want = false; }   // pulling the trigger with the pack on: it is pulled off first (0.35 s), and kept for later
            if (want)
            {
                if (d.cap > 0 && ammo[cur] == 0) { if (mouse.leftButton.wasPressedThisFrame) { reloadFull = false; Begin(St.Reload, d.reload.Has ? d.reload : d.reloadFull); } }
                else
                {
                    if (d.cap > 0) ammo[cur]--;
                    lastShotT = Time.time;
                    if (d.sar) nextShotT = (Time.time - nextShotT < 0.05f) ? nextShotT + SarShotInterval : Time.time + SarShotInterval;   // keeps the average rate exact whatever the frame rate
                    Quaternion dev = Quaternion.identity; Vector3 shotDir = cam.transform.forward;
                    if (d.ballistic)
                    {   // hot barrel: the point of impact walks down and left and the group opens up, in proportion to the temperature rise (see BarrelThermal)
                        float rise = Rise, sig = rise * (float)(BarrelThermal.GrowthMoaPerK * BarrelThermal.EsToSigma) / 60f;   // degrees
                        float down = rise * (float)BarrelThermal.DriftDownMoaPerK / 60f, left = rise * (float)BarrelThermal.DriftLeftMoaPerK / 60f;
                        dev = Quaternion.AngleAxis(-left + Gauss() * sig, cam.transform.up) * Quaternion.AngleAxis(down + Gauss() * sig, cam.transform.right);
                        shotDir = dev * shotDir; barrel.Shot(); if (d.sar) EjectCasing();
                    }
                    if (fm == FireMode.Single) nextSingle = Time.time + SingleDelay;
                    Begin(St.Fire, d.fire); kick = 1f;
                    if (!d.melee) { flash.enabled = true; flashT = 0.04f; }
                    if (d.ballistic) BulletSim.Fire(cam.transform, dev); else Hit(d.range, d.damage, shotDir);
                }
            }
        }
    }

    void Hit(float range, float damage, Vector3? dir = null)
    {
        Vector3 fw = dir ?? cam.transform.forward;
        if (Physics.Raycast(cam.transform.position, fw, out var hit, range) && hit.rigidbody != null)
            hit.rigidbody.AddForceAtPosition(fw * (60f * damage / 45f), hit.point);
    }

    // The look of a real scope picture (reference photo): the black eyecup is out of focus, so it fades softly into the scene instead of ending in a hard line, and the clear picture is a touch darker
    // at its very edge. Both are soft shading drawn on the screen at the scope's projected size; the scope itself and the reticle are 3D parts of the rifle.
    void DrawScopeSoftness()
    {
        if (haloTex == null) { haloTex = MakeSoftTex(256, 1.0f, 1.7f, 0.85f, false); edgeTex = MakeSoftTex(256, 0.84f, 1.0f, 0.35f, true); }
        Vector3 cR = cam.WorldToScreenPoint(scopeRear.position), cA = cam.WorldToScreenPoint(scopeAp.position);
        if (cR.z <= 0f || cA.z <= 0f) return;
        float ro = (cam.WorldToScreenPoint(scopeRear.TransformPoint(new Vector3(EyecupOutR, 0f, 0f))) - cR).magnitude;     // eyecup collar, in pixels
        float rf = (cam.WorldToScreenPoint(scopeAp.TransformPoint(new Vector3(ScopeR, 0f, 0f))) - cA).magnitude;            // the clear picture's edge, in pixels
        float a = Mathf.Clamp01((aim.AimAmount - 0.5f) / 0.45f); float H = Screen.height;
        GUI.color = new Color(1f, 1f, 1f, a);
        GUI.DrawTexture(new Rect(cR.x - 2f * ro, H - cR.y - 2f * ro, 4f * ro, 4f * ro), haloTex);
        GUI.DrawTexture(new Rect(cA.x - rf, H - cA.y - rf, 2f * rf, 2f * rf), edgeTex);
        GUI.color = Color.white;
    }
    // radial black texture over +-outer: alpha 0 inside r0, then strongest at r0 (outerward: falls to 0 at r1) or, for inward = true, rising from 0 at r0 to the strength at r1
    static Texture2D MakeSoftTex(int n, float r0, float r1, float strength, bool inward)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; var px = new Color32[n * n]; float half = n * 0.5f;
        float span = inward ? 1f : 2f;   // outward texture spans +-2 units of the eyecup radius, the inward one +-1 unit of the clear picture's radius
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f - half) / half * span, dy = (y + 0.5f - half) / half * span, r = Mathf.Sqrt(dx * dx + dy * dy), a;
                if (inward) a = r < r0 ? 0f : r > r1 ? 0f : strength * Mathf.SmoothStep(0f, 1f, (r - r0) / (r1 - r0));
                else a = r < r0 ? 0f : r > r1 ? 0f : strength * (1f - Mathf.SmoothStep(0f, 1f, (r - r0) / (r1 - r0)));
                px[y * n + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
            }
        t.SetPixels32(px); t.Apply(); return t;
    }
    void OnGUI()
    {
        if (d == null || aim.WheelOpen) return;
        if (d.sar && scopeRear != null && scopeAp != null && aim.AimAmount > 0.5f) { }   // no blur: the user asked not to blur the outside
        if (hud == null)
        {
            hud = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight }; hud.normal.textColor = Color.white;
            small = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.LowerLeft }; small.normal.textColor = new Color(1, 1, 1, 0.55f);
        }
        string s = models[cur] == null ? "loading..." : d.cap == 0 ? d.name : st == St.Reload ? "RELOADING" : ammo[cur] + " / " + d.cap + (ammo[cur] == 0 ? "   press R" : "");
        GUI.Label(new Rect(Screen.width - 340, Screen.height - 70, 320, 50), s, hud);
        if (!d.melee) GUI.Label(new Rect(Screen.width - 340, Screen.height - 105, 320, 40), (Mode == FireMode.Auto ? "AUTO" : Mode == FireMode.Semi ? "SEMI-AUTO" : "SINGLE") + (Modes[cur].Length > 1 ? "   [B]" : ""), hud);
        if (!d.melee && models[cur] != null)
        {
            float h01 = Mathf.Clamp01(Rise / 400f);
            if (d.ballistic)
            {   // barrel thermometer, 0-800 C. Ticks: 204 C (400 F bore: cook-off concern), 538 C (1000 F: steel starts to lose strength), 738 C (1360 F: barrel can burst)
                var white = Texture2D.whiteTexture; float bx = Screen.width - 220f, by = Screen.height - 124f, bw = 200f, bh = 6f, T = (float)barrel.T, fill = Mathf.Clamp01(T / TopC);
                if (heatTxt == null) heatTxt = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
                if (Rise > 2f)
                {
                    GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(new Rect(bx - 1f, by - 1f, bw + 2f, bh + 2f), white);
                    GUI.color = Color.Lerp(new Color(0.35f, 0.75f, 1f), new Color(1f, 0.15f, 0.05f), Mathf.Clamp01(T / 600f)); GUI.DrawTexture(new Rect(bx, by, bw * fill, bh), white);
                    GUI.color = new Color(1f, 1f, 1f, 0.85f);
                    foreach (float tk in new[] { 204f, 538f, 738f }) GUI.DrawTexture(new Rect(bx + bw * tk / TopC - 1f, by - 3f, 2f, bh + 6f), white);
                    heatTxt.normal.textColor = T > 538f ? new Color(1f, 0.25f, 0.1f) : T > 204f ? new Color(1f, 0.7f, 0.25f) : new Color(1f, 1f, 1f, 0.8f);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(bx - 200f, by - 18f, bw + 200f, 16f), (GroupMoa >= 0.3f ? "GROUP +" + GroupMoa.ToString("F1") + " MOA   " : "") + "BARREL " + Mathf.RoundToInt(T) + " C", heatTxt);
                }
                heatTxt.normal.textColor = new Color(1f, 1f, 1f, 0.5f); heatTxt.fontStyle = FontStyle.Normal;
                GUI.Label(new Rect(bx - 200f, by - 36f, bw + 200f, 16f), (d.sar ? "[F] ICE PACK x" + barrel.PackStock + (barrel.PackE > 0.0 && !barrel.PackSpent && !barrel.PackOn ? " +1 USED" : "") + "   " : "") + "AIR " + Mathf.RoundToInt(Atmosphere.AirTempC) + " C   WIND " + Atmosphere.WindMs.ToString("F0") + " m/s from W   [F6]", heatTxt); heatTxt.fontStyle = FontStyle.Bold;
                if (barrel.PackOn) { heatTxt.normal.textColor = new Color(0.65f, 0.88f, 1f, 0.95f); GUI.Label(new Rect(bx - 200f, by - 54f, bw + 200f, 16f), "ICE PACK " + Mathf.RoundToInt((float)barrel.PackLeft * 100f) + "% LEFT   " + Mathf.RoundToInt((float)barrel.PackTempC) + " C   x" + barrel.PackStock + " SPARE   [F] remove", heatTxt); }
            }
            // crosshair at the hip: four ticks and a dot; the gap opens with recoil, movement, sprint and heat, and the colour goes red with heat. It fades out as the sights come up (aiming uses the weapon's own sights).
            if (aim.AimAmount < 0.6f && Cursor.lockState == CursorLockMode.Locked)
            {
                var white = Texture2D.whiteTexture; float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f, gap = crossGap, len = 9f, th = 2f;
                float a = (1f - aim.AimAmount / 0.6f) * 0.92f * (h01 > 0.5f ? 0.75f + 0.25f * Mathf.Sin(Time.time * 22f) : 1f);
                Color c = Color.Lerp(Color.white, new Color(1f, 0.2f, 0.1f), h01); c.a = a; Color o = new Color(0f, 0f, 0f, a * 0.6f);
                for (int pass = 0; pass < 2; pass++)
                {
                    float p = pass == 0 ? 1f : 0f; GUI.color = pass == 0 ? o : c;   // dark outline first, then the colour
                    GUI.DrawTexture(new Rect(cx - gap - len - p, cy - th * 0.5f - p, len + 2f * p, th + 2f * p), white);
                    GUI.DrawTexture(new Rect(cx + gap - p, cy - th * 0.5f - p, len + 2f * p, th + 2f * p), white);
                    GUI.DrawTexture(new Rect(cx - th * 0.5f - p, cy - gap - len - p, th + 2f * p, len + 2f * p), white);
                    GUI.DrawTexture(new Rect(cx - th * 0.5f - p, cy + gap - p, th + 2f * p, len + 2f * p), white);
                    GUI.DrawTexture(new Rect(cx - th * 0.5f - p, cy - th * 0.5f - p, th + 2f * p, th + 2f * p), white);
                }
                GUI.color = Color.white;
            }
        }
        GUI.Label(new Rect(14, Screen.height - 30, 900, 20), "Weapons & animations: Molten Aim 25 by goblimos (MIT); models via Sketchfab. R reload, V melee, B fire mode.", small);
    }
}
