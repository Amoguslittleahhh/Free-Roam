using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Aiming down sights as with a real weapon: right mouse brings the sight picture to the eye, the camera
// zooms by the optic's magnification, mouse movement slows by the same ratio, hands sway (less while holding
// breath), and the scope or red-dot picture is drawn over the screen.
// Keys 1-3 switch weapons. Hold Q for the weapon wheel: point the mouse at a weapon, release Q to equip it.
public class WeaponAim : MonoBehaviour
{
    public float SensScale { get; private set; } = 1f;
    public bool WheelOpen => wheelOpen;
    public float AimAmount => aimT;
    public float Zoom { get; private set; } = 1f;
    Texture2D scopeTex;
    Camera cam;
    Transform holder;
    ArtistView artist;
    WeaponSpec spec;
    int current = -1;
    float aimT, baseFov;
    Texture2D hole, wheelTex;
    bool wheelOpen;
    int wheelShown = -2;
    GUIStyle labelStyle;
    const float TAU = Mathf.PI * 2f;

    public void Init(Camera c)
    {
        cam = c;
        baseFov = c.fieldOfView;
        holder = new GameObject("Weapon").transform;
        holder.SetParent(c.transform, false);
        artist = gameObject.AddComponent<ArtistView>(); artist.Init(c, holder, this);
        c.nearClipPlane = 0.02f;   // never slice the held gun
        hole = MakeHole(256);
        scopeTex = MakeScopeTex(1024);
        Select(0);
    }

    bool sweeping;
    // F9: cycles all guns, saves GripShots/gun_N.png (project folder) and reach.txt (arm reach overshoot per hand, metres)
    IEnumerator Sweep()
    {
        sweeping = true; int keep = current;
        string dir = System.IO.Path.Combine(Application.dataPath, "..", "GripShots"); System.IO.Directory.CreateDirectory(dir);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < WeaponData.All.Length; i++)
        {
            Select(i);
            yield return new WaitForSeconds(1.8f); yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "gun_" + i + ".png"));
            sb.AppendLine("gun " + i + " " + WeaponData.All[i].id);
            yield return null; yield return null;
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "reach.txt"), sb.ToString());
        Select(keep); sweeping = false; Debug.Log("GripShots saved: " + dir);
    }

    void Select(int i)
    {
        current = i;
        spec = WeaponData.All[i];
        artist.Equip(i, spec);
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null || spec == null || cam == null) return;

        if (kb.f9Key.wasPressedThisFrame && !sweeping) StartCoroutine(Sweep());
        if (kb.qKey.wasPressedThisFrame) OpenWheel();
        if (wheelOpen)
        {
            int hover = HoverIndex(mouse.position.ReadValue());
            int shown = hover >= 0 ? hover : current;
            if (shown != wheelShown) BuildWheel(shown);
            if (kb.qKey.wasReleasedThisFrame)
            {
                if (hover >= 0 && hover != current) Select(hover);
                CloseWheel();
            }
        }

        var digits = new[] { kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key, kb.digit6Key, kb.digit7Key, kb.digit8Key };
        for (int i = 0; i < WeaponData.All.Length && i < digits.Length; i++)
            if (digits[i].wasPressedThisFrame && current != i) Select(i);

        bool want = !wheelOpen && mouse.rightButton.isPressed && Cursor.lockState == CursorLockMode.Locked && artist.CanAds;
        aimT = Mathf.MoveTowards(aimT, want ? 1f : 0f, Time.deltaTime / spec.adsTime);
        float e = aimT * aimT * (3f - 2f * aimT);   // ease in and settle on the sight
        float mag = Mathf.Lerp(1f, spec.magnification, e);
        Zoom = mag;
        float fovMag = spec.sight == SightType.Integral ? 1f : mag;   // an integral optic magnifies only inside its own window (a second camera in ArtistView), the world around it stays 1x
        cam.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(baseFov * 0.5f * Mathf.Deg2Rad) / fovMag) * Mathf.Rad2Deg;
        SensScale = 1f / mag;
        holder.localPosition = artist.Pos(e);
        holder.localRotation = artist.Rot;
        holder.gameObject.SetActive(!(spec.sight == SightType.Scope && e > 0.85f));   // looking through the scope: only the world is seen
    }

    void LateUpdate()
    {
        if (artist != null && artist.Kick > 0.001f) cam.transform.localRotation *= Quaternion.Euler(-artist.Kick, 0f, 0f);
        if (aimT <= 0f) return;
        var kb = Keyboard.current;
        bool holdingBreath = kb != null && kb.leftShiftKey.isPressed;
        float amp = spec.swayDeg * aimT * (holdingBreath ? 0.35f : 1f);
        float t = Time.time;
        float sx = (Mathf.PerlinNoise(t * 0.6f, 11.3f) - 0.5f) * 2f * amp;
        float sy = (Mathf.PerlinNoise(23.7f, t * 0.8f) - 0.5f) * 2f * amp;
        cam.transform.localRotation *= Quaternion.Euler(sy, sx, 0f);
    }

    void OpenWheel()
    {
        wheelOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void CloseWheel()
    {
        wheelOpen = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // which slot the mouse points at, measured clockwise from the top; -1 in the dead centre
    int HoverIndex(Vector2 m)
    {
        float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f, R = Screen.height * 0.25f;
        Vector2 d = m - new Vector2(cx, cy);
        if (d.magnitude < R * 0.3f) return -1;
        float a = Mathf.Atan2(d.x, d.y);
        if (a < 0) a += TAU;
        return Mathf.FloorToInt(a / (TAU / WeaponData.All.Length) + 0.5f) % WeaponData.All.Length;
    }

    // the wheel's ring: one segment per weapon, the highlighted one orange, thin gaps between segments
    void BuildWheel(int highlight)
    {
        const int n = 256;
        int N = WeaponData.All.Length;
        if (wheelTex == null) wheelTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = n * 0.5f, R = c - 2f, inner = R * 0.3f;
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float dx = x + 0.5f - c, dy = y + 0.5f - c, r = Mathf.Sqrt(dx * dx + dy * dy);
            Color col = Color.clear;
            if (r <= R && r >= inner)
            {
                float a = Mathf.Atan2(dx, dy);
                if (a < 0) a += TAU;
                float seg = a / (TAU / N);
                int idx = Mathf.FloorToInt(seg + 0.5f) % N;
                bool gap = Mathf.Abs(seg - Mathf.Floor(seg) - 0.5f) < 0.025f;
                col = gap ? Color.clear : idx == highlight ? new Color(1f, 0.62f, 0.1f, 0.92f) : new Color(0.07f, 0.07f, 0.09f, 0.82f);
            }
            px[y * n + x] = col;
        }
        wheelTex.SetPixels(px);
        wheelTex.Apply();
        wheelShown = highlight;
    }

    void DrawWheel(float cx, float cy, float R)
    {
        int N = WeaponData.All.Length;
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 16 };
            labelStyle.normal.textColor = Color.white;
        }
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(cx - R, cy - R, 2 * R, 2 * R), wheelTex);
        for (int i = 0; i < N; i++)
        {
            float a = i * TAU / N;
            float px = cx + Mathf.Sin(a) * R * 0.68f, py = cy - Mathf.Cos(a) * R * 0.68f;
            GUI.Label(new Rect(px - 90, py - 14, 180, 28), WeaponData.All[i].name, labelStyle);
        }
    }

    void OnGUI()
    {
        float W = Screen.width, H = Screen.height, cx = W * 0.5f, cy = H * 0.5f;
        var white = Texture2D.whiteTexture;
        if (wheelOpen && wheelTex != null) DrawWheel(cx, cy, H * 0.25f);
        if (spec == null) return;
        if (spec.sight == SightType.Integral) return;   // the SAR-21's scope and reticle are 3D parts of the rifle itself (ArtistView.BuildReticle), nothing is drawn over the screen
        if (aimT < 0.9f) return;
        if (spec.sight == SightType.Scope)
        {
            float R = H * 0.42f;   // radius of the eyepiece
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, W, cy - R), white);
            GUI.DrawTexture(new Rect(0, cy + R, W, H - (cy + R)), white);
            GUI.DrawTexture(new Rect(0, cy - R, cx - R, 2 * R), white);
            GUI.DrawTexture(new Rect(cx + R, cy - R, W - (cx + R), 2 * R), white);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(cx - R, cy - R, 2 * R, 2 * R), hole);   // black ring around the round view
            GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.95f);             // reticle
            float th = Mathf.Max(1f, H / 540f), L = R * 0.85f, g = R * 0.06f;
            GUI.DrawTexture(new Rect(cx - L, cy - th * 0.5f, L - g, th), white);
            GUI.DrawTexture(new Rect(cx + g, cy - th * 0.5f, L - g, th), white);
            GUI.DrawTexture(new Rect(cx - th * 0.5f, cy - L, th, L - g), white);
            GUI.DrawTexture(new Rect(cx - th * 0.5f, cy + g, th, L - g), white);
            float dot = th * 3f;
            for (int i = 1; i <= 4; i++)
            {
                float d = R * 0.12f * i;
                GUI.DrawTexture(new Rect(cx + d - dot * 0.5f, cy - dot * 0.5f, dot, dot), white);
                GUI.DrawTexture(new Rect(cx - d - dot * 0.5f, cy - dot * 0.5f, dot, dot), white);
                GUI.DrawTexture(new Rect(cx - dot * 0.5f, cy + d - dot * 0.5f, dot, dot), white);
                GUI.DrawTexture(new Rect(cx - dot * 0.5f, cy - d - dot * 0.5f, dot, dot), white);
            }
        }
        else if (spec.sight == SightType.RedDot)
        {
            GUI.color = new Color(1f, 0.1f, 0.05f, 0.9f);
            float d = H * 0.01f;
            GUI.DrawTexture(new Rect(cx - d * 0.5f, cy - d * 0.5f, d, d), white);
        }
        GUI.color = Color.white;
    }

    // The scope picture in units of the glass radius R (the texture spans +-1.4 R): half-transparent dark rim from R fading out by 1.4 R (the 3D scope housing shows through), faint darkening at the glass edge,
    // and the reticle: two fine crossing lines out to the edge, an open ring and a dot at the centre.
    static Texture2D MakeScopeTex(int n)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float half = n * 0.5f, px = 1.4f / half; var cols = new Color32[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float dx = (x + 0.5f - half) * px, dy = (y + 0.5f - half) * px, r = Mathf.Sqrt(dx * dx + dy * dy), a;
            if (r >= 1f) a = 0f;   // outside the glass the real 3D scope is seen, nothing is laid over it
            else a = 0.30f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r - 0.86f) / 0.14f));
            float ret = 0f; const float hw = 0.0022f, rin = 0.050f, rout = 0.074f, dot = 0.009f;
            if (r < 1f)
            {
                if (r > rout) ret = Mathf.Max(ret, 1f - Mathf.Clamp01((Mathf.Min(Mathf.Abs(dx), Mathf.Abs(dy)) - hw) / px));
                ret = Mathf.Max(ret, 1f - Mathf.Clamp01((Mathf.Abs(r - 0.5f * (rin + rout)) - 0.5f * (rout - rin)) / px));
                ret = Mathf.Max(ret, 1f - Mathf.Clamp01((r - dot) / px));
                ret *= 0.92f * (1f - Mathf.Clamp01((r - 0.985f) / 0.015f));
            }
            cols[y * n + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(Mathf.Clamp01(1f - (1f - a) * (1f - ret)) * 255f));
        }
        t.SetPixels32(cols); t.Apply(); return t;
    }

    // a round window: clear inside the eyepiece, opaque black outside, with a soft edge
    static Texture2D MakeHole(int n)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        float r = n * 0.5f;
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float dx = x + 0.5f - r, dy = y + 0.5f - r;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Clamp01((d - r * 0.97f) / (r * 0.02f));
            t.SetPixel(x, y, new Color(0f, 0f, 0f, a));
        }
        t.Apply();
        return t;
    }
}
