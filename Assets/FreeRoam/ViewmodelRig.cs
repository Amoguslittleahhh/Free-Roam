using UnityEngine;
using UnityEngine.InputSystem;

// First-person arms, ammo and reloads built from human anatomy and real reload technique.
//  - Arms: two-bone chains (upper arm 0.33 m, forearm + hand 0.30 m), elbows solved with law-of-cosines IK and bent down and out.
//  - Hands: palm, four fingers and thumb. Fingers curl around grips; they open while travelling to the magazine.
//  - Technique (Marine Corps rifle reload doctrine, Shooting Illustrated pistol and AK guides):
//      * the gun comes down to the "work space" at the chest, muzzle angled below horizontal, eyes stay up;
//      * the strong hand stays on the grip and its index finger / thumb works the magazine release;
//      * the support hand goes to the pouch, indexes the new magazine with the index finger along its front, brings it up
//        to the well, seats it with one firm push, tugs down to confirm it is locked;
//      * then the bolt is released (palm slap on the bolt catch for the M16) or the charging handle / slide is racked;
//      * AK: hook the front lip of the magazine into the trunnion, then rock it back until the rear catch locks;
//      * pump shotgun: strong hand leaves the grip to thumb shells into the loading port while the support hand holds the forend;
//      * bolt rifle: strong hand lifts, draws back, pushes and locks the bolt after the magazine is changed.
// R reloads, left mouse fires.
public partial class ViewmodelRig : MonoBehaviour
{
    enum Kind { Pistol, Mag, Shotgun, Bolt }
    enum Charge { Pull, Slap, Slide, None }
    // B cycles the selector. Single: one round per trigger pull, with a deliberate lock-out between shots (SingleDelay).
    // Semi-auto: one round per pull at the weapon's cyclic rate. Auto: rounds continue while the trigger is held.
    static readonly FireMode[][] Modes =
    {
        new[] { FireMode.Single, FireMode.Semi },                      // Glock 17
        new[] { FireMode.Single, FireMode.Semi, FireMode.Auto },       // MP5A5
        new[] { FireMode.Single, FireMode.Semi, FireMode.Auto },       // M16A4
        new[] { FireMode.Single },                                     // Remington 870 (pump)
        new[] { FireMode.Single },                                     // AWM (bolt)
        new[] { FireMode.Single, FireMode.Semi, FireMode.Auto },       // AK-47
        new[] { FireMode.Single, FireMode.Semi, FireMode.Auto },       // SAR-21
        new[] { FireMode.Single, FireMode.Semi, FireMode.Auto },       // FN P90
    };
    const float SingleDelay = 0.30f;   // game-design value, not a published figure
    static bool Is556(int i) => i == 6;   // SAR-21: simulated with real 5.56x45 ballistics (BulletSim)
    int[] modeIdx = new int[8];
    FireMode Mode => Modes[idx][modeIdx[idx]];
    class Anchor { public Kind kind; public Charge charge = Charge.Pull; public bool rock; public int cap; public float rpm; public bool auto; public Vector2 grip, fore, mag, handle; public Vector3 pull = new Vector3(0, -0.16f, 0); }

    // Anchor points as fractions of the model's box: (rear to muzzle, bottom to top).
    static readonly Anchor[] A =
    {
        new Anchor{kind=Kind.Pistol, charge=Charge.Slide, cap=17, rpm=400, grip=new Vector2(.30f,.35f), fore=new Vector2(.36f,.28f), mag=new Vector2(.20f,.10f), handle=new Vector2(.30f,.85f)},
        new Anchor{kind=Kind.Mag,    cap=30, rpm=800, auto=true, grip=new Vector2(.45f,.30f), fore=new Vector2(.75f,.55f), mag=new Vector2(.58f,.22f), handle=new Vector2(.50f,.90f)},
        new Anchor{kind=Kind.Mag,    charge=Charge.Slap, cap=30, rpm=800, auto=true, grip=new Vector2(.30f,.25f), fore=new Vector2(.62f,.55f), mag=new Vector2(.45f,.18f), handle=new Vector2(.34f,.70f)},
        new Anchor{kind=Kind.Shotgun,charge=Charge.None, cap=7, rpm=70, grip=new Vector2(.30f,.55f), fore=new Vector2(.62f,.35f), mag=new Vector2(.42f,.25f), handle=new Vector2(.40f,.55f)},
        new Anchor{kind=Kind.Bolt,   charge=Charge.None, cap=5, rpm=45, grip=new Vector2(.23f,.45f), fore=new Vector2(.55f,.35f), mag=new Vector2(.36f,.18f), handle=new Vector2(.27f,.60f)},
        new Anchor{kind=Kind.Mag,    rock=true, cap=30, rpm=600, auto=true, grip=new Vector2(.35f,.28f), fore=new Vector2(.60f,.50f), mag=new Vector2(.48f,.20f), handle=new Vector2(.45f,.82f)},
        new Anchor{kind=Kind.Mag,    cap=30, rpm=650, auto=true, grip=new Vector2(.43f,.31f), fore=new Vector2(.70f,.50f), mag=new Vector2(.28f,.22f), handle=new Vector2(.52f,.80f)},
        new Anchor{kind=Kind.Mag,    cap=50, rpm=900, auto=true, grip=new Vector2(.66f,.16f), fore=new Vector2(.62f,.45f), mag=new Vector2(.36f,.72f), handle=new Vector2(.70f,.52f), pull=new Vector3(0, 0.05f, -0.16f)},
    };

    public static System.Collections.Generic.List<string> Reach = new System.Collections.Generic.List<string>();
    const float L1 = 0.3024f, L2 = 0.3050f, HAND = 0.10f; Vector3 lastE;
    static readonly Vector3 ShoulderR = new Vector3(0.20f, -0.22f, 0.06f), ShoulderL = new Vector3(-0.20f, -0.22f, 0.06f);
    static readonly Vector3 PouchL = new Vector3(-0.16f, -0.34f, 0.24f);    // magazine pouch on the left front of the belt, below the view
    static readonly Vector3 PouchR = new Vector3(0.20f, -0.34f, 0.24f);     // shells on the right side

    // Average adult male (ANSUR-style): hand 19 cm, breadth 8.9 cm, palm 10 cm, forearm 26.5 cm, upper arm 33 cm
    class Hand
    {
        public Transform palm, fingers, thumb, thumb2, uA, fA; public Quaternion tq0 = Quaternion.identity, tq1 = Quaternion.identity;
        public Transform[] f1 = new Transform[4], f2 = new Transform[4], f3 = new Transform[4];
        public void Apply(float[] mcp, float[] pip, float[] dip, float[] fy, float t0, float t1, float t2)
        {
            for (int i = 0; i < 4; i++)
            {
                f1[i].localRotation = Quaternion.Euler(mcp[i], fy[i], 0);
                f2[i].localRotation = Quaternion.Euler(pip[i], 0, 0);
                f3[i].localRotation = Quaternion.Euler(dip[i], 0, 0);
            }
            thumb.localRotation = tq0 * Quaternion.Euler(t0, t1, 0); thumb2.localRotation = tq1 * Quaternion.Euler(t2, 0, 0);
        }
    }

    Camera cam; Transform holder; WeaponAim aim; WeaponSpec spec; Anchor an; int idx;
    Transform root, upR, foreR, upL, foreL, carried;
    Hand hR, hL;
    Transform magT, slideT, pumpT; Vector3 magRest, slideRest, pumpRest; Quaternion magRestRot; bool lockedBack;
    void ApplySlide(float z) { if (slideT != null) slideT.localPosition = slideRest + new Vector3(0, 0, z); }
    Vector3 gripL, foreLoc, magLoc, handleLoc;
    int[] ammo = new int[8];
    float reloadT = -1f, kick, nextShot;
    bool dropped, inserted;
    GUIStyle hud;
    Vector3 reloadOffset = Vector3.zero; Quaternion reloadRot = Quaternion.identity;
    Material mSleeve, mSkin, mMag;

    public bool Busy => reloadT >= 0f;
    public float Kick => kick * 1.4f;   // degrees of view kick
    public void SetActive(bool on) { enabled = on; if (root != null) root.gameObject.SetActive(on); }
    public Vector3 GunPos => new Vector3(0, 0.004f * kick, -0.025f * kick) + reloadOffset;
    public Quaternion GunRot => Quaternion.Euler(-2.2f * kick, 0, 0) * reloadRot;

    public void Init(Camera c, Transform h, WeaponAim w)
    {
        cam = c; holder = h; aim = w;
        for (int i = 0; i < ammo.Length; i++) { ammo[i] = A[i].cap; modeIdx[i] = Modes[i].Length - 1; }
        mSleeve = CityBuilder.Mat(new Color(0.09f, 0.10f, 0.085f)); mSkin = CityBuilder.Mat(new Color(0.77f, 0.57f, 0.46f)); mNail = CityBuilder.Mat(new Color(0.86f, 0.72f, 0.66f)); skinMode = Resources.Load<TextAsset>("Hands/hand") != null; mGlove = new Material(mSleeve.shader); var gt = Resources.Load<Texture2D>("Hands/hand_diffuse"); if (gt != null) { mGlove.SetTexture("_BaseMap", gt); mGlove.mainTexture = gt; } mGlove.SetColor("_BaseColor", Color.white); mGlove.color = Color.white; mGlove.SetFloat("_Smoothness", 0.15f); mCuff = CityBuilder.Mat(new Color(0.04f, 0.04f, 0.045f)); mMag = CityBuilder.Mat(new Color(0.07f, 0.07f, 0.08f));
        root = new GameObject("Arms").transform; root.SetParent(c.transform, false);
        upR = Limb(mSleeve, 0.050f, 0.041f); foreR = Limb(mSleeve, 0.040f, 0.0345f); upL = Limb(mSleeve, 0.050f, 0.041f); foreL = Limb(mSleeve, 0.040f, 0.0345f);
        hR = MakeHand(1f); hL = MakeHand(-1f);
        eR = Ball(mSleeve, 0.084f); eL = Ball(mSleeve, 0.084f); sR = Ball(mSleeve, 0.10f); sL = Ball(mSleeve, 0.10f); wbR = Ball(mSkin, 0.002f); wbL = Ball(mSkin, 0.002f); skR = Limb(mSkin, 0.0300f, 0.0240f); skL = Limb(mSkin, 0.0300f, 0.0240f); cfR = Limb(mSleeve, 0.0365f, 0.0365f); cfL = Limb(mSleeve, 0.0365f, 0.0365f);
        if (skinMode) { upR.gameObject.SetActive(false); foreR.gameObject.SetActive(false); upL.gameObject.SetActive(false); foreL.gameObject.SetActive(false); eR.gameObject.SetActive(false); eL.gameObject.SetActive(false); sR.gameObject.SetActive(false); sL.gameObject.SetActive(false); wbR.gameObject.SetActive(false); wbL.gameObject.SetActive(false); }
        skR.gameObject.SetActive(false); skL.gameObject.SetActive(false); cfR.gameObject.SetActive(false); cfL.gameObject.SetActive(false);
    }

    Transform Cube(Transform parent, Material m, Vector3 pos, Vector3 scale)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g.transform;
    }
    Transform Prim(PrimitiveType t, Transform parent, Material m, Vector3 pos, Vector3 scale)
    {
        var g = GameObject.CreatePrimitive(t);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g.transform;
    }
    static Mesh TubeMesh(float len, float rx0, float ry0, float rx1, float ry1, int n = 10)
    {
        var P = new System.Collections.Generic.List<Vector3>(); var I = new System.Collections.Generic.List<int>();
        for (int j = 0; j < 2; j++) for (int k = 0; k < n; k++) { float a = k * Mathf.PI * 2f / n; P.Add(new Vector3(Mathf.Cos(a) * (j == 1 ? rx1 : rx0), Mathf.Sin(a) * (j == 1 ? ry1 : ry0), j == 1 ? len : 0f)); }
        for (int k = 0; k < n; k++) { int k2 = (k + 1) % n; I.AddRange(new[] { k, k2, n + k, k2, n + k2, n + k }); }
        int c0 = P.Count; P.Add(new Vector3(0, 0, 0)); int c1 = c0 + 1; P.Add(new Vector3(0, 0, len));
        int b = P.Count;
        for (int k = 0; k < n; k++) { float a = k * Mathf.PI * 2f / n; P.Add(new Vector3(Mathf.Cos(a) * rx0, Mathf.Sin(a) * ry0, 0f)); }
        for (int k = 0; k < n; k++) { float a = k * Mathf.PI * 2f / n; P.Add(new Vector3(Mathf.Cos(a) * rx1, Mathf.Sin(a) * ry1, len)); }
        for (int k = 0; k < n; k++) { int k2 = (k + 1) % n; I.AddRange(new[] { b + n + k, b + n + k2, c1, b + k2, b + k, c0 }); }
        var m = new Mesh(); m.SetVertices(P); m.SetTriangles(I, 0); m.RecalculateNormals(); return m;
    }
    static bool skinMode; static HandMeshData hmd;
    [System.Serializable] class HandMeshData { public float[] v; public int[] t; public int[] tn; public float[] uv; public int[] bi; public float[] bw; }
    Transform Tube(Transform parent, Material m, float len, float rx0, float ry0, float rx1, float ry1, Vector3 pos)
    {
        if (skinMode && m != mSleeve) return N(parent, pos);
        var g = new GameObject("tube"); g.transform.SetParent(parent, false); g.transform.localPosition = pos;
        g.AddComponent<MeshFilter>().sharedMesh = TubeMesh(len, rx0, ry0, rx1, ry1); g.AddComponent<MeshRenderer>().sharedMaterial = m; return g.transform;
    }
    Transform BallAt(Transform parent, Material m, float rad, Vector3 pos) => skinMode ? N(parent, pos) : Prim(PrimitiveType.Sphere, parent, m, pos, Vector3.one * rad * 2f);
    Transform Limb(Material m, float r0, float r1) { var g = new GameObject("limb"); g.transform.SetParent(root, false); g.AddComponent<MeshFilter>().sharedMesh = TubeMesh(1f, r0, r0, r1, r1, 12); g.AddComponent<MeshRenderer>().sharedMaterial = m; return g.transform; }
    Transform Ball(Material m, float d) => Prim(PrimitiveType.Sphere, root, m, Vector3.zero, Vector3.one * d);
    Transform eR, eL, sR, sL, wbR, wbL;
    Material mCuff, mNail, mGlove; Transform skR, skL, cfR, cfL;
    static Transform N(Transform parent, Vector3 pos) { var g = new GameObject("n"); g.transform.SetParent(parent, false); g.transform.localPosition = pos; return g.transform; }

    Hand MakeHand(float side)
    {
        var h = new Hand(); var G = mSkin;
        h.palm = N(root, Vector3.zero);   // origin = palm centre, 5 cm past the wrist crease
        Tube(h.palm, G, 0.100f, .030f, .016f, .0445f, .0135f, new Vector3(0, 0, -.05f));
        Tube(h.palm, G, 0.060f, .014f, .0135f, .017f, .0145f, new Vector3(-.030f * side, -.006f, -.046f)).localRotation = Quaternion.Euler(0, side * -8f, 0);
        Tube(h.palm, mCuff, 0.030f, .0335f, .0230f, .0335f, .0230f, new Vector3(0, 0, -.075f));
        h.fingers = N(h.palm, new Vector3(0, 0, .05f));
        float[] p1 = { 0.03399f, 0.03464f, 0.03249f, 0.02578f }, p2 = { 0.02547f, 0.03158f, 0.02867f, 0.02027f }, p3 = { 0.02422f, 0.02733f, 0.02367f, 0.02157f }, xs = { 0.02633f, -0.00000f, -0.02588f, -0.04818f }, ys = { -0.01058f, 0.00000f, -0.00210f, -0.01058f }, zo = { 0.01267f, 0.01508f, 0.00621f, -0.00992f };
        for (int i = 0; i < 4; i++)
        {
            var a1 = N(h.fingers, new Vector3(-side * xs[i], ys[i], zo[i]));
            var a2 = N(a1, new Vector3(0, 0, p1[i]));
            var a3 = N(a2, new Vector3(0, 0, p2[i]));
            h.f1[i] = a1; h.f2[i] = a2; h.f3[i] = a3;
        }
        h.thumb = N(h.palm, new Vector3(-0.01033f * side, -0.01913f, -0.03404f));
        h.thumb2 = N(h.thumb, new Vector3(0, 0, 0.05668f));
        h.tq0 = new Quaternion(0.314202f, -0.232034f * side, 0.000000f * side, 0.920563f);
        h.tq1 = new Quaternion(0.002513f, 0.151362f * side, 0.000000f * side, 0.988475f);
        if (skinMode) AttachSkin(h, side);
        return h;
    }

    void AttachSkin(Hand h, float side)
    {
        if (hmd == null) hmd = JsonUtility.FromJson<HandMeshData>(Resources.Load<TextAsset>("Hands/hand").text);
        var bones = new Transform[17]; bones[0] = h.palm;
        for (int i = 0; i < 4; i++) { bones[1 + 3 * i] = h.f1[i]; bones[2 + 3 * i] = h.f2[i]; bones[3 + 3 * i] = h.f3[i]; }
        bones[13] = h.thumb; bones[14] = h.thumb2;
        h.uA = N(root, new Vector3(0, 0, -0.05f - L2 - L1)); h.fA = N(root, new Vector3(0, 0, -0.05f - L2)); bones[15] = h.uA; bones[16] = h.fA;
        int n = hmd.v.Length / 3; var V = new Vector3[n]; var bw = new BoneWeight[n];
        for (int i = 0; i < n; i++)
        {
            V[i] = new Vector3(hmd.v[3 * i] * side, hmd.v[3 * i + 1], hmd.v[3 * i + 2]);
            var w = new BoneWeight(); w.boneIndex0 = hmd.bi[4 * i]; w.boneIndex1 = hmd.bi[4 * i + 1]; w.boneIndex2 = hmd.bi[4 * i + 2]; w.boneIndex3 = hmd.bi[4 * i + 3];
            w.weight0 = hmd.bw[4 * i]; w.weight1 = hmd.bw[4 * i + 1]; w.weight2 = hmd.bw[4 * i + 2]; w.weight3 = hmd.bw[4 * i + 3]; bw[i] = w;
        }
        var T = (int[])hmd.t.Clone();
        if (side < 0) for (int i = 0; i < T.Length; i += 3) { int a = T[i + 1]; T[i + 1] = T[i + 2]; T[i + 2] = a; }
        var go = new GameObject("handSkin"); go.transform.SetParent(h.palm, false);
        var mesh = new Mesh(); mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; mesh.vertices = V; mesh.subMeshCount = 2; mesh.SetTriangles(T, 0); var TN = hmd.tn != null ? (int[])hmd.tn.Clone() : new int[0];
        if (side < 0) for (int i = 0; i < TN.Length; i += 3) { int a = TN[i + 1]; TN[i + 1] = TN[i + 2]; TN[i + 2] = a; }
        mesh.SetTriangles(TN, 1); mesh.boneWeights = bw;
        if (hmd.uv != null && hmd.uv.Length == 2 * n) { var UV = new Vector2[n]; for (int i = 0; i < n; i++) UV[i] = new Vector2(hmd.uv[2 * i], hmd.uv[2 * i + 1]); mesh.uv = UV; }
        var bp = new Matrix4x4[17]; for (int i = 0; i < 17; i++) bp[i] = bones[i].worldToLocalMatrix * go.transform.localToWorldMatrix;
        mesh.bindposes = bp; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var smr = go.AddComponent<SkinnedMeshRenderer>(); smr.sharedMesh = mesh; smr.bones = bones; smr.rootBone = h.palm; smr.sharedMaterials = new[] { mGlove, mGlove }; smr.updateWhenOffscreen = true;
    }

    public void Equip(WeaponSpec s, int i)
    {
        spec = s; idx = i; an = A[i]; reloadT = -1f; kick = 0f; reloadOffset = Vector3.zero; reloadRot = Quaternion.identity;
        if (carried != null) Destroy(carried.gameObject);
        Bounds b = new Bounds((s.bmin + s.bmax) * 0.5f, s.bmax - s.bmin);
        Vector3 P(Vector2 f) => new Vector3(0f, Mathf.Lerp(b.min.y, b.max.y, f.y), Mathf.Lerp(b.min.z, b.max.z, f.x));
        gripL = P(an.grip); foreLoc = P(an.fore) + new Vector3(-0.015f, 0, 0); magLoc = P(an.mag); handleLoc = P(an.handle) + new Vector3(-0.03f, 0.03f, 0);
        magT = holder.Find("mag"); slideT = holder.Find("slide"); pumpT = holder.Find("pump");
        if (magT != null) { magRest = magT.localPosition; magRestRot = magT.localRotation; magLoc = magT.localPosition; }
        slideRest = slideT != null ? slideT.localPosition : Vector3.zero; pumpRest = pumpT != null ? pumpT.localPosition : Vector3.zero;
        if (GripData.R[i] != null) gripL = GripData.R[i].wrist + GripData.R[i].fwd * HAND;
        if (GripData.L[i] != null) foreLoc = GripData.L[i].wrist + GripData.L[i].fwd * HAND;
        lockedBack = false; ApplySlide(ammo[idx] == 0 ? -0.045f : 0f);
    }

    static float Seg(float t, float a, float b) { float x = Mathf.Clamp01((t - a) / (b - a)); return x * x * (3f - 2f * x); }
    Vector3 W(Vector3 local) => holder.TransformPoint(local);
    Vector3 PouchLocal(Vector3 camPos) => holder.InverseTransformPoint(cam.transform.TransformPoint(camPos));

    void Update()
    {
        var kb = Keyboard.current; var mouse = Mouse.current;
        if (kb == null || mouse == null || an == null) return;
        bool ok = !aim.WheelOpen && Cursor.lockState == CursorLockMode.Locked;
        if (reloadT >= 0f) { reloadT += Time.deltaTime; if (reloadT >= Dur) FinishReload(); }
        else if (ok)
        {
            if (kb.rKey.wasPressedThisFrame && ammo[idx] < an.cap) { reloadT = 0f; dropped = inserted = false; lockedBack = ammo[idx] == 0; }
            else
            {
                if (kb.bKey.wasPressedThisFrame) modeIdx[idx] = (modeIdx[idx] + 1) % Modes[idx].Length;
                var fm = Mode;
                bool want = fm == FireMode.Auto ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;
                if (want && ammo[idx] > 0 && Time.time >= nextShot)
                {
                    float cd = 60f / an.rpm; if (fm == FireMode.Single) cd = Mathf.Max(cd, SingleDelay);
                    ammo[idx]--; if (ammo[idx] == 0) ApplySlide(-0.045f); nextShot = Time.time + cd; kick = 1f;
                    if (Is556(idx)) BulletSim.Fire(cam.transform);
                    else if (Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, 300f) && hit.rigidbody != null)
                        hit.rigidbody.AddForceAtPosition(cam.transform.forward * 60f, hit.point);
                }
            }
        }
        kick = Mathf.MoveTowards(kick, 0f, Time.deltaTime * 9f);
    }

    float Dur => an.kind == Kind.Shotgun ? 4.4f : an.kind == Kind.Bolt ? 3.8f : an.kind == Kind.Pistol ? 2.6f : 3.1f;

    void FinishReload()
    {
        reloadT = -1f; ammo[idx] = an.cap; reloadOffset = Vector3.zero; reloadRot = Quaternion.identity;
        if (carried != null) Destroy(carried.gameObject);
        if (magT != null) { magT.gameObject.SetActive(true); magT.localPosition = magRest; magT.localRotation = magRestRot; }
        ApplySlide(0f); if (pumpT != null) pumpT.localPosition = pumpRest;
    }

    void LateUpdate()
    {
        if (an == null) return;
        bool visible = holder.gameObject.activeSelf;
        root.gameObject.SetActive(visible);
        if (!visible) return;

        Vector3 r = gripL, l = foreLoc, palmUpL = -holder.right, palmUpR = holder.right;
        float curlL = 0.85f, curlR = 0.85f, thumbL = 0.2f, thumbR = 0.2f;
        Vector3 pull = an.pull;
        Vector3 magOut = magLoc + pull, magCup = magLoc + pull * 0.15f;

        if (reloadT >= 0f)
        {
            float t = reloadT, d = Dur;
            // the gun comes down to the work space at the chest, muzzle below horizontal, rolled a little toward the support hand
            float ws = Seg(t, 0f, 0.28f) - Seg(t, d - 0.5f, d - 0.05f);
            if (an.kind == Kind.Shotgun) { reloadOffset = new Vector3(0.05f, -0.09f, -0.12f) * ws; reloadRot = Quaternion.Euler(24f * ws, 0, -32f * ws); }
            else if (an.kind == Kind.Bolt) { reloadOffset = new Vector3(0f, -0.05f, -0.07f) * ws; reloadRot = Quaternion.Euler(10f * ws, 0, 8f * ws); }
            else { reloadOffset = new Vector3(-0.01f, -0.06f, -0.09f) * ws; reloadRot = Quaternion.Euler(14f * ws, 0, 7f * ws); }

            if (an.kind == Kind.Shotgun) ShotgunTimeline(t, ref r, ref l, ref curlL, ref curlR);
            else
            {
                bool bolt = an.kind == Kind.Bolt;
                float tPress = 0.30f, tPullEnd = 0.62f, tPouch = 1.05f, tCarryStart = 1.10f, tNear = 1.50f, tSeat = 1.68f, tTug = 1.85f;
                if (an.kind == Kind.Pistol) { tPress = 0.22f; tPullEnd = 0.40f; tPouch = 0.85f; tCarryStart = 0.90f; tNear = 1.25f; tSeat = 1.42f; tTug = 1.55f; }
                // support hand cups the magazine well with index finger along the magazine, strong index/thumb presses the release
                l = Vector3.Lerp(l, magCup, Seg(t, 0.05f, tPress));
                curlL = Mathf.Lerp(curlL, 0.35f, Seg(t, 0.0f, 0.2f));
                curlR = 0.85f - 0.25f * Mathf.Sin(Mathf.Clamp01((t - tPress) / 0.12f) * Mathf.PI);   // index finger presses and releases
                thumbR = 0.2f + 0.7f * Mathf.Sin(Mathf.Clamp01((t - tPress) / 0.12f) * Mathf.PI);
                l = Vector3.Lerp(l, magOut, Seg(t, tPress, tPullEnd));
                if (t > tPullEnd && !dropped) { dropped = true; DropOldMag(W(magOut)); }
                l = Vector3.Lerp(l, PouchLocal(PouchL), Seg(t, tPullEnd, tPouch));
                curlL = Mathf.Lerp(curlL, 0.9f, Seg(t, tPouch - 0.1f, tPouch));        // fingers close on the new magazine
                bool carrying = t > tCarryStart && t < tSeat + 0.04f;
                if (carrying && carried == null) carried = MakeCarried();
                if (!carrying && carried != null && t > tSeat) { Destroy(carried.gameObject); carried = null; }
                Vector3 hookPos = magOut + new Vector3(0, 0, an.rock ? 0.03f : 0f);
                l = Vector3.Lerp(l, hookPos, Seg(t, tCarryStart, tNear));
                l = Vector3.Lerp(l, magLoc, Seg(t, tNear, tSeat));
                if (t >= tSeat && !inserted) { inserted = true; if (magT != null) magT.gameObject.SetActive(true); kick = 0.7f; }
                float tug = Mathf.Sin(Mathf.Clamp01((t - tSeat - 0.02f) / (tTug - tSeat)) * Mathf.PI);
                l += new Vector3(0, -0.012f * tug, 0);                                    // tug down to confirm the magazine is locked
                if (magT != null && !inserted) { }
                if (carried != null)
                {
                    float rock = an.rock ? Seg(t, tNear, tSeat) : 1f;
                    carried.position = W(l) + holder.up * -0.015f;
                    carried.rotation = holder.rotation * Quaternion.Euler(an.rock ? Mathf.Lerp(-28f, 0f, rock) : 0f, 0, 0);
                }
                // seat the magazine mesh: it rises into the well in step with the hand
                if (magT != null && inserted) { float k = Seg(t, tSeat, tSeat + 0.1f); magT.localPosition = Vector3.Lerp(magRest + pull * 0.5f, magRest, k); }
                float c0 = tTug + 0.08f;
                if (!bolt)
                {
                    Vector3 hand = handleLoc;
                    if (an.charge == Charge.Slap)
                    {   // palm slaps the bolt catch
                        l = Vector3.Lerp(l, hand, Seg(t, c0, c0 + 0.25f));
                        l = Vector3.Lerp(l, hand + new Vector3(0, -0.02f, 0), Seg(t, c0 + 0.25f, c0 + 0.33f));
                        curlL = Mathf.Lerp(curlL, 0.1f, Seg(t, c0, c0 + 0.2f));
                        l = Vector3.Lerp(l, hand, Seg(t, c0 + 0.33f, c0 + 0.42f));
                        l = Vector3.Lerp(l, foreLoc, Seg(t, c0 + 0.45f, d - 0.15f));
                        curlL = Mathf.Lerp(curlL, 0.85f, Seg(t, d - 0.5f, d - 0.15f));
                    }
                    else
                    {   // reach over the top, pull the charging handle / slide fully back, let it fly forward
                        l = Vector3.Lerp(l, hand, Seg(t, c0, c0 + 0.25f));
                        curlL = Mathf.Lerp(curlL, 0.9f, Seg(t, c0 + 0.15f, c0 + 0.25f));
                        l = Vector3.Lerp(l, hand + new Vector3(0, 0, -0.08f), Seg(t, c0 + 0.25f, c0 + 0.42f));
                        if (an.kind == Kind.Pistol && slideT != null) ApplySlide(lockedBack ? Mathf.Lerp(-0.045f, -0.05f, Seg(t, c0 + 0.25f, c0 + 0.42f)) * (t < c0 + 0.45f ? 1f : 0f) : 0f);   // slingshot: pull the slide fully back, let go and it snaps forward
                        l = Vector3.Lerp(l, hand, Seg(t, c0 + 0.42f, c0 + 0.48f));
                        l = Vector3.Lerp(l, foreLoc, Seg(t, c0 + 0.5f, d - 0.15f));
                        curlL = Mathf.Lerp(curlL, 0.85f, Seg(t, d - 0.5f, d - 0.15f));
                    }
                }
                else
                {   // bolt action: support hand returns to the forend, strong hand lifts, draws back, pushes forward and locks the bolt
                    l = Vector3.Lerp(l, foreLoc, Seg(t, tTug + 0.05f, tTug + 0.5f));
                    Vector3 bolt3 = handleLoc + new Vector3(0.045f, 0, 0), up = new Vector3(0.03f, 0.035f, 0), back = new Vector3(0, 0, -0.09f);
                    float b0 = tTug + 0.4f;
                    r = Vector3.Lerp(r, bolt3, Seg(t, b0, b0 + 0.25f));
                    curlR = Mathf.Lerp(curlR, 0.9f, Seg(t, b0 + 0.15f, b0 + 0.25f));
                    r = Vector3.Lerp(r, bolt3 + up, Seg(t, b0 + 0.25f, b0 + 0.4f));
                    r = Vector3.Lerp(r, bolt3 + up + back, Seg(t, b0 + 0.4f, b0 + 0.65f));
                    r = Vector3.Lerp(r, bolt3 + up, Seg(t, b0 + 0.7f, b0 + 0.95f));
                    r = Vector3.Lerp(r, bolt3, Seg(t, b0 + 0.95f, b0 + 1.1f));
                    r = Vector3.Lerp(r, gripL, Seg(t, b0 + 1.1f, d - 0.1f));
                    curlR = Mathf.Lerp(curlR, 0.85f, Seg(t, b0 + 1.1f, d - 0.1f));
                }
            }
        }
        DoHand(hR, GripData.R[idx], ShoulderR, 1f, r, gripL, palmUpR, curlR, thumbR, upR, foreR, eR, sR);
        DoHand(hL, GripData.L[idx], ShoulderL, -1f, l, foreLoc, palmUpL, curlL, thumbL, upL, foreL, eL, sL);
    }

    // Pump shotgun: strong hand leaves the grip and thumbs shells into the loading port; support hand stays on the forend; then pump.
    void ShotgunTimeline(float t, ref Vector3 r, ref Vector3 l, ref float curlL, ref float curlR)
    {
        Vector3 port = magLoc;
        for (int n = 0; n < 3; n++)
        {
            float s0 = 0.30f + n * 0.85f;
            r = Vector3.Lerp(r, PouchLocal(PouchR), Seg(t, s0, s0 + 0.28f));
            curlR = Mathf.Lerp(curlR, 0.9f, Seg(t, s0 + 0.2f, s0 + 0.3f));                 // pinch a shell
            r = Vector3.Lerp(r, port, Seg(t, s0 + 0.30f, s0 + 0.60f));
            r = Vector3.Lerp(r, port + new Vector3(0, 0.025f, 0.015f), Seg(t, s0 + 0.60f, s0 + 0.72f));   // thumb pushes the shell past the latch
            curlR = Mathf.Lerp(curlR, 0.3f, Seg(t, s0 + 0.62f, s0 + 0.72f));
        }
        r = Vector3.Lerp(r, gripL, Seg(t, 2.95f, 3.35f));
        curlR = Mathf.Lerp(curlR, 0.85f, Seg(t, 3.1f, 3.35f));
        l = Vector3.Lerp(l, foreLoc + new Vector3(0, 0, -0.11f), Seg(t, 3.4f, 3.75f));     // pump back
        l = Vector3.Lerp(l, foreLoc, Seg(t, 3.75f, 4.05f));                                 // and forward
        if (pumpT != null) pumpT.localPosition = pumpRest + new Vector3(0, 0, l.z - foreLoc.z);
    }

    Transform MakeCarried()
    {
        GameObject g;
        if (magT != null) { g = Instantiate(magT.gameObject); g.SetActive(true); g.transform.localScale = holder.lossyScale; }
        else { g = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(g.GetComponent<Collider>()); g.transform.localScale = new Vector3(0.026f, 0.09f, 0.04f); g.GetComponent<Renderer>().sharedMaterial = mMag; }
        return g.transform;
    }

    void DropOldMag(Vector3 pos)
    {
        GameObject g;
        if (magT != null) { g = Instantiate(magT.gameObject); g.SetActive(true); magT.gameObject.SetActive(false); }
        else { g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.transform.localScale = new Vector3(0.026f, 0.09f, 0.04f); g.GetComponent<Renderer>().sharedMaterial = mMag; Destroy(g.GetComponent<Collider>()); }
        g.transform.SetParent(null); g.transform.position = magT != null ? holder.TransformPoint(magRest) : pos; g.transform.rotation = holder.rotation;
        var bc = g.AddComponent<BoxCollider>();
        if (magT != null)
        {   // collider sized to the magazine's meshes (the mag object is a group of coloured parts)
            Bounds mb = new Bounds(); bool first = true;
            foreach (var mf in g.GetComponentsInChildren<MeshFilter>()) { var bb = mf.sharedMesh.bounds; if (first) { mb = bb; first = false; } else mb.Encapsulate(bb); }
            bc.center = mb.center; bc.size = mb.size;
        }
        var rb = g.AddComponent<Rigidbody>(); rb.mass = 0.25f; rb.linearVelocity = cam.transform.TransformDirection(new Vector3(0.05f, -1.0f, 0.1f)); rb.angularVelocity = new Vector3(2f, 0.5f, 1f);
        Destroy(g, 8f);
    }

    static readonly float[] KC = { 1f, 1.04f, 1.08f, 1.14f };
    Vector3 SolveArmTo(Vector3 S, Vector3 wrist, float side, Transform up, Transform fore, Transform elbow, Transform shoulder)
    {
        Vector3 dw = wrist - S; if (Reach.Count < 2) Reach.Add((side > 0 ? "R" : "L") + ":dist=" + dw.magnitude.ToString("F3") + ",over=" + Mathf.Max(0f, dw.magnitude - (L1 + L2 - 0.002f)).ToString("F3")); float d = Mathf.Min(dw.magnitude, L1 + L2 - 0.002f); Vector3 wd = dw.normalized; wrist = S + wd * d;
        float a = (L1 * L1 - L2 * L2 + d * d) / (2f * d); float h = Mathf.Sqrt(Mathf.Max(0f, L1 * L1 - a * a));
        Vector3 pole = cam.transform.TransformDirection(new Vector3(0.35f * side, -1f, -0.15f));
        pole = Vector3.ProjectOnPlane(pole, wd).normalized;
        Vector3 E = S + wd * a + pole * h; lastE = E;
        Vector3 fd = (wrist - E).normalized, cut = E + fd * Mathf.Max(0.05f, (wrist - E).magnitude - 0.02f);
        Place(up, S, E); Place(fore, E, cut); Place(side > 0 ? skR : skL, cut - fd * 0.004f, wrist + fd * 0.012f); Place(side > 0 ? cfR : cfL, cut - fd * 0.012f, cut + fd * 0.012f); elbow.position = E; shoulder.position = S; (side > 0 ? wbR : wbL).position = wrist;
        return wrist;
    }
    static void Place(Transform t, Vector3 a, Vector3 b)
    {
        var v = b - a; t.position = a; t.rotation = Quaternion.LookRotation(v.normalized, Vector3.up); t.localScale = new Vector3(1, 1, v.magnitude);
    }
    void DoHand(Hand h, GripPose gp, Vector3 shCam, float side, Vector3 target, Vector3 home, Vector3 palmUp, float curl, float thumb, Transform up, Transform fore, Transform elbow, Transform shoulder)
    {
        Vector3 S = cam.transform.TransformPoint(shCam), Kw = W(target);
        Vector3 fwd = (Kw - S).normalized, dor = palmUp; float g = 0f;
        if (gp != null)
        {
            g = 1f - Mathf.Clamp01(Vector3.Distance(target, home) / 0.10f); g = g * g * (3f - 2f * g);
            fwd = Vector3.Slerp(fwd, holder.TransformDirection(gp.fwd), g).normalized; dor = Vector3.Slerp(palmUp, holder.TransformDirection(gp.dorsal), g);
        }
        Vector3 wr = SolveArmTo(S, Kw - fwd * HAND, side, up, fore, elbow, shoulder);
        h.palm.position = wr + fwd * 0.05f; h.palm.rotation = Quaternion.LookRotation(fwd, dor);
        if (h.uA != null) { Vector3 pu = h.palm.up; h.uA.SetPositionAndRotation(S, Quaternion.LookRotation((lastE - S).normalized, pu)); h.fA.SetPositionAndRotation(lastE, Quaternion.LookRotation((wr - lastE).normalized, pu)); }
        var mcp = new float[4]; var pip = new float[4]; var dip = new float[4]; var fy = new float[4];
        float s = gp != null ? Mathf.Clamp(curl / 0.85f, 0f, 1.15f) : 0f;
        for (int i = 0; i < 4; i++)
        {
            float c = curl * KC[i];
            float gm = Mathf.Min(90f, 70f * c), gpp = Mathf.Min(105f, 90f * c), gd = Mathf.Min(70f, 60f * c);
            if (gp != null) { mcp[i] = Mathf.Lerp(gm, gp.mcp[i] * s, g); pip[i] = Mathf.Lerp(gpp, gp.pip[i] * s, g); dip[i] = Mathf.Lerp(gd, gp.dip[i] * s, g); fy[i] = gp.fy[i] * g; }
            else { mcp[i] = gm; pip[i] = gpp; dip[i] = gd; }
        }
        float t0 = -6f + 18f * (1f - thumb), t1 = side * (-10f + 60f * thumb), t2 = 32f - 30f * thumb;
        if (gp != null) { t0 = Mathf.Lerp(t0, gp.th[0], g); t1 = Mathf.Lerp(t1, gp.th[1], g); t2 = Mathf.Lerp(t2, gp.th[2], g); }
        h.Apply(mcp, pip, dip, fy, t0, t1, t2);
    }

    void OnGUI()
    {
        if (!string.IsNullOrEmpty(LabStatus)) GUI.Label(new Rect(20, 20, 1100, 40), LabStatus);
        if (an == null || aim.WheelOpen) return;
        if (hud == null) { hud = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight }; hud.normal.textColor = Color.white; }
        string s = Busy ? "RELOADING" : ammo[idx] + " / " + an.cap + (ammo[idx] == 0 ? "   press R" : "");
        GUI.Label(new Rect(Screen.width - 340, Screen.height - 70, 320, 50), s, hud);
        string m = Mode == FireMode.Auto ? "AUTO" : Mode == FireMode.Semi ? "SEMI-AUTO" : "SINGLE";
        GUI.Label(new Rect(Screen.width - 340, Screen.height - 105, 320, 40), m + (Modes[idx].Length > 1 ? "   [B]" : ""), hud);
    }
}
