using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR && SATURA_SPACE
using SaturaSpace;
#endif

// Spud Bowl: hold to pour little spuds into a neon bowl; equal spuds merge into bigger ones.
// Reach the Golden King before the bowl backs up into the neck.
public class SpudGame : MonoBehaviour
{
    public static SpudGame I;

    [Tooltip("Sprite material (Sprites/Default). Built at runtime when empty.")]
    public Material spriteMaterial;
    [Tooltip("TMP SDF font asset (Montserrat Bold) and its material presets")]
    public TMP_FontAsset font;
    public Material shadowMaterial, bannerMaterial;

    public const int MaxTier = 7;
    public static readonly float[] Radius = { 0.22f, 0.3f, 0.39f, 0.49f, 0.6f, 0.72f, 0.86f, 1.1f };
    public static readonly string[] Names = { "Nub", "Sprout", "Specs", "Beanie", "Chef", "Viking", "Wizard", "Golden King" };

    const float R = 4f, NeckW = 1.3f, NeckTop = 9.5f, SpawnY = 7.0f, AimRange = 0.9f;
    const float PourInterval = 0.13f, OverflowSeconds = 2.5f, FullFill = 0.6f;

    float neckBaseY;
    Sprite[] spudSprites;
    Sprite square, circle, pill;
    Material neonMat;
    Camera cam;
    LineRenderer bowlLine, bowlGlow;
    readonly List<SpriteRenderer> bars = new List<SpriteRenderer>();
    float[] barKick;
    readonly List<SpriteRenderer> stars = new List<SpriteRenderer>();
    readonly List<Vector3> starSeed = new List<Vector3>();
    Transform spudRoot, fxRoot;
    SpriteRenderer preview;
    PhysicsMaterial2D bouncy;

    // UI
    TextMeshProUGUI scoreText, bannerText;
    Image[] slotImg;
    TextMeshProUGUI[] slotQ;
    RectTransform[] slotRt;
    float[] slotPop;
    float bannerT, bannerHold;

    // state
    public readonly List<Spud> spuds = new List<Spud>();
    public int score;
    public int merges;
    public int mashes;
    public bool gameOver;
    public bool bot;
    readonly bool[] found = new bool[8];
    public float fill;
    float shownScore, overflowT, pourT, aim, hue = 0.55f, gameOverAt, botPhase;
    int nextTier;

    void Awake()
    {
        I = this;
        neckBaseY = Mathf.Sqrt(R * R - NeckW * NeckW);
        if (spriteMaterial == null) spriteMaterial = new Material(Shader.Find("Sprites/Default"));
        neonMat = new Material(spriteMaterial) { name = "Neon", color = new Color(1.7f, 1.7f, 1.7f, 1f) };
        spudSprites = SpudArt.BuildSpuds();
        square = SpudArt.Square();
        circle = SpudArt.Circle();
        pill = SpudArt.Pill();
        bouncy = new PhysicsMaterial2D("Spud") { bounciness = 0.9f, friction = 0.05f };

        BuildCamera();
        BuildBowl();
        BuildBackdrop();
        BuildUI();
        spudRoot = new GameObject("Spuds").transform;
        fxRoot = new GameObject("FX").transform;

        preview = MakeSprite("NextSpud", spudSprites[0], 9);
        preview.transform.SetParent(transform, false);
        Restart();
    }

    // ---------------------------------------------------------------- build
    void BuildCamera()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = go.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 7.65f;
        cam.transform.SetPositionAndRotation(new Vector3(0f, -0.2f, -10f), Quaternion.identity);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.098f, 0.094f, 0.188f);
        cam.allowHDR = true;
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

        var vol = new GameObject("Glow").AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 10;
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom = prof.Add<Bloom>(true);
        bloom.threshold.Override(1.05f);
        bloom.intensity.Override(0.9f);
        bloom.scatter.Override(0.72f);
        var tm = prof.Add<Tonemapping>(true);
        tm.mode.Override(TonemappingMode.None);
        vol.sharedProfile = prof;
    }

    void BuildBowl()
    {
        var pts = new List<Vector2> { new Vector2(-NeckW, NeckTop), new Vector2(-NeckW, neckBaseY) };
        float aL = Mathf.Atan2(neckBaseY, -NeckW);
        float aR = Mathf.Atan2(neckBaseY, NeckW) + Mathf.PI * 2f;
        const int seg = 140;
        for (int i = 1; i < seg; i++)
        {
            float a = Mathf.Lerp(aL, aR, i / (float)seg);
            pts.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R);
        }
        pts.Add(new Vector2(NeckW, neckBaseY));
        pts.Add(new Vector2(NeckW, NeckTop));

        var bowl = new GameObject("Bowl");
        var edge = bowl.AddComponent<EdgeCollider2D>();
        var closed = new List<Vector2>(pts) { pts[0] }; // lid above the screen keeps spuds in
        edge.SetPoints(closed);
        edge.edgeRadius = 0.04f;
        edge.sharedMaterial = bouncy;

        bowlGlow = MakeLine(bowl.transform, "Glow", pts, 0.3f, 4);
        bowlLine = MakeLine(bowl.transform, "Neon", pts, 0.045f, 5);
    }

    LineRenderer MakeLine(Transform parent, string name, List<Vector2> pts, float width, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = neonMat;
        lr.useWorldSpace = true;
        lr.positionCount = pts.Count;
        for (int i = 0; i < pts.Count; i++) lr.SetPosition(i, pts[i]);
        lr.widthMultiplier = width;
        lr.numCornerVertices = 4;
        lr.numCapVertices = 4;
        lr.sortingOrder = order;
        return lr;
    }

    void BuildBackdrop()
    {
        var root = new GameObject("Backdrop").transform;
        const int n = 96;
        barKick = new float[n];
        for (int i = 0; i < n; i++)
        {
            var sr = MakeSprite("Bar" + i, square, -10);
            sr.transform.SetParent(root, false);
            bars.Add(sr);
        }
        var rnd = new System.Random(7);
        for (int i = 0; i < 70; i++)
        {
            var sr = MakeSprite("Dust" + i, circle, -20);
            sr.transform.SetParent(root, false);
            var p = new Vector3((float)rnd.NextDouble() * 12f - 6f, (float)rnd.NextDouble() * 17f - 8.5f, (float)rnd.NextDouble() * 10f);
            sr.transform.position = new Vector3(p.x, p.y, 0f);
            sr.transform.localScale = Vector3.one * (0.03f + (float)rnd.NextDouble() * 0.05f);
            stars.Add(sr);
            starSeed.Add(p);
        }
    }

    SpriteRenderer MakeSprite(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sharedMaterial = spriteMaterial;
        sr.sortingOrder = order;
        return sr;
    }

    void BuildUI()
    {
        var cgo = new GameObject("HUD");
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 1f;
        var root = cgo.transform;

        // tag
        var tag = MakeImage(root, "Tag", pill, new Color(0.96f, 0.40f, 0.22f), new Vector2(0, 1), new Vector2(40, -64), new Vector2(330, 72), new Vector2(0, 0.5f));
        tag.type = Image.Type.Sliced;
        MakeText(tag.transform, "SPUD BOWL #1", 32, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 1), new Vector2(330, 72));

        scoreText = MakeText(root, "SCORE: 0", 78, Color.white, new Vector2(0.5f, 0), new Vector2(0, 400), new Vector2(1000, 110), shadowMaterial);
        bannerText = MakeText(root, "", 64, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(900, 260), bannerMaterial);
        bannerText.lineSpacing = -8f;

        slotImg = new Image[8]; slotQ = new TextMeshProUGUI[8]; slotRt = new RectTransform[8]; slotPop = new float[8];
        for (int i = 0; i < 8; i++)
        {
            float x = (i - 3.5f) * 106f;
            var ring = MakeImage(root, "Slot" + i, circle, new Color(0.62f, 0.62f, 0.72f), new Vector2(0.5f, 0), new Vector2(x, 290), new Vector2(92, 92), new Vector2(0.5f, 0.5f));
            slotRt[i] = ring.rectTransform;
            MakeImage(ring.transform, "Bg", circle, new Color(0.13f, 0.13f, 0.24f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84, 84), new Vector2(0.5f, 0.5f));
            var img = MakeImage(ring.transform, "Spud", spudSprites[i], Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, -4), new Vector2(84, 84), new Vector2(0.5f, 0.5f));
            img.preserveAspect = true;
            slotImg[i] = img;
            var q = MakeText(ring.transform, "?", 42, new Color(0.65f, 0.65f, 0.75f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84, 84));
            slotQ[i] = q;
        }
    }

    Image MakeImage(Transform parent, string name, Sprite s, Color c, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.sprite = s;
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    TextMeshProUGUI MakeText(Transform parent, string s, int size, Color c, Vector2 anchor, Vector2 pos, Vector2 box, Material mat = null)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        if (mat != null) t.fontSharedMaterial = mat;
        t.fontSize = size;
        t.fontStyle = FontStyles.Normal;
        t.color = c;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        t.text = s;
        return t;
    }

    // ---------------------------------------------------------------- run
    public void Restart()
    {
        foreach (var s in spuds) if (s) Destroy(s.gameObject);
        spuds.Clear();
        if (fxRoot) for (int i = fxRoot.childCount - 1; i >= 0; i--) Destroy(fxRoot.GetChild(i).gameObject);
        score = 0; shownScore = 0; merges = 0; mashes = 0;
        gameOver = false; overflowT = 0f; pourT = 0f;
        for (int i = 0; i < 8; i++) found[i] = i == 0;
        nextTier = 0;
        aim = 0f;
        UpdateSlots();
        Banner("HOLD TO POUR\nTHE SPUDS!", 2.2f);
    }

    public bool Found(int tier) => tier >= 0 && tier < 8 && found[tier];
    public int Highest() { for (int i = 7; i >= 0; i--) if (found[i]) return i; return 0; }

    public string State() =>
        $"score={score} spuds={spuds.Count} merges={merges} mashes={mashes} highest={Highest() + 1}({Names[Highest()]}) found={System.Array.FindAll(found, f => f).Length}/8 fill={fill:P0} gameOver={gameOver} overflow={overflowT:F1}s bot={bot}";

    int RollTier()
    {
        float r = Random.value;
        return r < 0.5f ? 0 : r < 0.85f ? 1 : 2;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        cam.orthographicSize = Mathf.Max(7.65f, 4.3f / Mathf.Max(0.1f, cam.aspect));

        // ---- input
        var pointer = Pointer.current;
        var kb = Keyboard.current;
        bool pressing = false, pressedNow = false;
        if (pointer != null)
        {
            pressedNow |= pointer.press.wasPressedThisFrame;
            if (pointer.press.isPressed)
            {
                pressing = true;
                float u = pointer.position.ReadValue().x / Mathf.Max(1, Screen.width);
                aim = Mathf.Lerp(-AimRange, AimRange, Mathf.InverseLerp(0.2f, 0.8f, u));
            }
        }
        if (kb != null)
        {
            pressing |= kb.spaceKey.isPressed || kb.downArrowKey.isPressed;
            pressedNow |= kb.spaceKey.wasPressedThisFrame || kb.rKey.wasPressedThisFrame;
            float dir = (kb.rightArrowKey.isPressed || kb.dKey.isPressed ? 1f : 0f) - (kb.leftArrowKey.isPressed || kb.aKey.isPressed ? 1f : 0f);
            aim = Mathf.Clamp(aim + dir * 1.8f * dt, -AimRange, AimRange);
        }
        if (bot)
        {
            botPhase += dt;
            pressing = true;
            aim = Mathf.Sin(botPhase * 1.15f) * AimRange * 0.9f;
            if (gameOver && Time.time > gameOverAt + 1.5f) pressedNow = true;
        }

        if (gameOver)
        {
            if (pressedNow && Time.time > gameOverAt + 1f) Restart();
        }
        else
        {
            pourT -= dt;
            if (pressing && pourT <= 0f)
            {
                var s = SpawnSpud(nextTier, new Vector2(aim, SpawnY), new Vector2(Random.Range(-0.4f, 0.4f), -4.5f));
                s.transform.rotation = Quaternion.Euler(0, 0, Random.Range(-25f, 25f));
                nextTier = RollTier();
                pourT = PourInterval;
            }
        }

        // ---- visuals
        preview.enabled = !gameOver && pourT < PourInterval * 0.6f;
        preview.sprite = spudSprites[nextTier];
        preview.transform.position = new Vector3(aim, SpawnY, 0f);
        preview.transform.localScale = Vector3.one * Radius[nextTier];

        hue = Mathf.Repeat(hue + dt * 0.035f, 1f);
        float danger = Mathf.Clamp01(overflowT / OverflowSeconds);
        var grad = new Gradient();
        var ck = new GradientColorKey[3];
        for (int i = 0; i < 3; i++)
        {
            var c = Color.HSVToRGB(Mathf.Repeat(hue + i * 0.13f, 1f), 0.65f, 1f);
            if (danger > 0f) c = Color.Lerp(c, new Color(1f, 0.15f, 0.2f), danger * (0.6f + 0.4f * Mathf.Sin(Time.time * 18f)));
            ck[i] = new GradientColorKey(c, i * 0.5f);
        }
        grad.SetKeys(ck, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        bowlLine.colorGradient = grad;
        grad.SetKeys(ck, new[] { new GradientAlphaKey(0.05f, 0f), new GradientAlphaKey(0.05f, 1f) });
        bowlGlow.colorGradient = grad;

        float t = Time.time;
        for (int i = 0; i < bars.Count; i++)
        {
            float a = i / (float)bars.Count * Mathf.PI * 2f;
            barKick[i] = Mathf.MoveTowards(barKick[i], 0f, dt * 2.5f);
            float len = 0.35f + 1.3f * Mathf.PerlinNoise(i * 0.31f, t * 1.4f) + barKick[i];
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var tr = bars[i].transform;
            tr.position = (Vector3)(dir * (R + 0.45f + len * 0.5f));
            tr.rotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg - 90f);
            tr.localScale = new Vector3(0.14f, len, 1f);
            var bc = Color.HSVToRGB(Mathf.Repeat(hue + 0.1f + 0.25f * Mathf.Sin(a), 1f), 0.75f, 0.95f);
            bc.a = 0.16f + 0.12f * Mathf.Clamp01(barKick[i]);
            bars[i].color = bc;
        }
        for (int i = 0; i < stars.Count; i++)
        {
            var s = starSeed[i];
            float y = Mathf.Repeat(s.y + 8.5f + t * 0.08f * (0.5f + s.z * 0.1f), 17f) - 8.5f;
            stars[i].transform.position = new Vector3(s.x, y, 0f);
            stars[i].color = new Color(0.8f, 0.85f, 1f, 0.15f + 0.35f * (0.5f + 0.5f * Mathf.Sin(t * 2f + s.z * 3f)));
        }

        // ---- UI
        shownScore = Mathf.MoveTowards(shownScore, score, Mathf.Max(60f, (score - shownScore) * 6f) * dt);
        scoreText.text = "SCORE: " + Mathf.RoundToInt(shownScore).ToString("N0");
        if (bannerHold > 0f) bannerHold -= dt; else bannerT = Mathf.MoveTowards(bannerT, 0f, dt * 2f);
        var bcol = bannerText.color; bcol.a = bannerT; bannerText.color = bcol;
        for (int i = 0; i < 8; i++)
        {
            slotPop[i] = Mathf.MoveTowards(slotPop[i], 0f, dt * 2.5f);
            slotRt[i].localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(slotPop[i] * Mathf.PI));
        }
    }

    void FixedUpdate()
    {
        // equal spuds drift toward each other (keeps the merges coming), different ones keep a little space
        int n = spuds.Count;
        float area = 0f;
        for (int i = 0; i < n; i++)
        {
            var a = spuds[i];
            float ra = Radius[a.tier];
            area += ra * ra;
            for (int j = i + 1; j < n; j++)
            {
                var b = spuds[j];
                Vector2 d = b.rb.position - a.rb.position;
                float dist = d.magnitude;
                if (dist < 0.001f) continue;
                Vector2 f;
                if (a.tier == b.tier)
                {
                    float range = ra * 7f + 0.8f;
                    if (dist > range) continue;
                    f = d / dist * 2.6f * (1f - dist / range);
                }
                else
                {
                    float range = ra + Radius[b.tier] + 0.45f;
                    if (dist > range) continue;
                    f = -d / dist * 1.6f * (1f - dist / range);
                }
                a.rb.AddForce(f * a.rb.mass);
                b.rb.AddForce(-f * b.rb.mass);
            }
        }
        fill = area / (R * R);

        if (gameOver) return;
        bool over = fill > FullFill;
        overflowT = over ? overflowT + Time.fixedDeltaTime : Mathf.Max(0f, overflowT - Time.fixedDeltaTime * 2f);
        if (overflowT > OverflowSeconds) GameOver();
    }

    Spud SpawnSpud(int tier, Vector2 pos, Vector2 vel)
    {
        var sr = MakeSprite("Spud" + (tier + 1) + " " + Names[tier], spudSprites[tier], 10 + tier);
        var go = sr.gameObject;
        go.transform.SetParent(spudRoot, false);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * Radius[tier];
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0.05f;
        rb.linearDamping = 0.02f;
        rb.angularDamping = 0.6f;
        rb.mass = Radius[tier] * Radius[tier] * 4f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.linearVelocity = vel;
        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.95f;
        col.sharedMaterial = bouncy;
        var s = go.AddComponent<Spud>();
        s.tier = tier;
        s.rb = rb;
        s.born = Time.time;
        spuds.Add(s);
        return s;
    }

    public void Merge(Spud a, Spud b)
    {
        if (gameOver) return;
        a.merging = b.merging = true;
        Vector2 mid = (a.rb.position + b.rb.position) * 0.5f;
        if (a.tier == MaxTier)
        {
            // two Golden Kings burst for a bonus and free the bowl
            spuds.Remove(a); spuds.Remove(b);
            Destroy(a.gameObject); Destroy(b.gameObject);
            merges++; mashes++;
            score += 2000;
            Ring(mid, Radius[MaxTier] * 1.4f);
            Sparks(mid, Radius[MaxTier], 30);
            KickBars(mid, 2f);
            Confetti();
            Banner("ROYAL MASH!\n+2,000", 1.6f);
#if UNITY_EDITOR && SATURA_SPACE
            SaturaLog.Log("spud", $"royal mash, score {score}");
#endif
            return;
        }
        int t = a.tier + 1;
        Vector2 vel = (a.rb.linearVelocity + b.rb.linearVelocity) * 0.5f + Vector2.up * 0.8f;
        spuds.Remove(a); spuds.Remove(b);
        Destroy(a.gameObject); Destroy(b.gameObject);
        var s = SpawnSpud(t, mid, vel);
        s.transform.rotation = Quaternion.Euler(0, 0, Random.Range(-30f, 30f));
        s.Pop();
        merges++;
        score += 10 * t * (t + 1);
        Ring(mid, Radius[t]);
        Sparks(mid, Radius[t], t == MaxTier ? 26 : 8);
        KickBars(mid, 0.6f + t * 0.15f);
        if (!found[t])
        {
            found[t] = true;
            slotPop[t] = 1f;
            UpdateSlots();
            if (t == MaxTier) { Banner("GOLDEN KING!", 3f); Confetti(); }
            else Banner("NEW SPUD:\n" + Names[t].ToUpper() + "!", 1.1f);
        }
#if UNITY_EDITOR && SATURA_SPACE
        SaturaLog.Log("spud", $"merge -> tier {t + 1} {Names[t]} score {score} spuds {spuds.Count}");
#endif
    }

    void GameOver()
    {
        gameOver = true;
        gameOverAt = Time.time;
        Banner("BOWL FULL!\nTAP TO POUR AGAIN", 0f, true);
#if UNITY_EDITOR && SATURA_SPACE
        SaturaLog.Log("spud", $"bowl full, score {score}");
#endif
    }

    void UpdateSlots()
    {
        for (int i = 0; i < 8; i++)
        {
            slotImg[i].enabled = found[i];
            slotQ[i].enabled = !found[i];
        }
    }

    void Banner(string text, float hold, bool sticky = false)
    {
        bannerText.text = text;
        bannerT = 1f;
        bannerHold = sticky ? 9999f : hold;
    }

    void KickBars(Vector2 at, float amount)
    {
        float a0 = Mathf.Atan2(at.y, at.x);
        for (int i = 0; i < bars.Count; i++)
        {
            float a = i / (float)bars.Count * Mathf.PI * 2f;
            float d = Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, a0 * Mathf.Rad2Deg));
            barKick[i] = Mathf.Max(barKick[i], amount * Mathf.Clamp01(1f - d / 50f));
        }
    }

    // ---------------------------------------------------------------- fx
    void Ring(Vector2 at, float r)
    {
        var go = new GameObject("Ring");
        go.transform.SetParent(fxRoot, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = neonMat;
        lr.loop = true;
        lr.useWorldSpace = true;
        lr.positionCount = 40;
        lr.sortingOrder = 30;
        StartCoroutine(RingAnim(lr, at, r));
    }

    IEnumerator RingAnim(LineRenderer lr, Vector2 at, float r)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.45f)
        {
            if (lr == null) yield break;
            float rr = r * Mathf.Lerp(1.05f, 1.9f, 1f - (1f - t) * (1f - t));
            for (int i = 0; i < 40; i++)
            {
                float a = i / 40f * Mathf.PI * 2f;
                lr.SetPosition(i, at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr);
            }
            lr.widthMultiplier = Mathf.Lerp(0.07f, 0.015f, t);
            var c = new Color(0.85f, 0.95f, 1f, 1f - t);
            lr.startColor = lr.endColor = c;
            yield return null;
        }
        if (lr != null) Destroy(lr.gameObject);
    }

    void Sparks(Vector2 at, float r, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var sr = MakeSprite("Spark", circle, 31);
            sr.transform.SetParent(fxRoot, false);
            float a = (i + Random.value * 0.5f) / count * Mathf.PI * 2f;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            StartCoroutine(Fly(sr, at + dir * r, dir * Random.Range(2.5f, 4.5f), 0.08f, new Color(1f, 0.95f, 0.7f), 0.45f, 0f));
        }
    }

    void Confetti()
    {
        for (int i = 0; i < 140; i++)
        {
            var sr = MakeSprite("Confetti", square, 32);
            sr.transform.SetParent(fxRoot, false);
            var c = Color.HSVToRGB(Random.value, 0.75f, 1f);
            var pos = new Vector2(Random.Range(-3.5f, 3.5f), Random.Range(-1f, 3f));
            var v = new Vector2(Random.Range(-3f, 3f), Random.Range(2f, 7f));
            StartCoroutine(Fly(sr, pos, v, Random.Range(0.08f, 0.16f), c, Random.Range(2.2f, 3.2f), 6f));
        }
    }

    IEnumerator Fly(SpriteRenderer sr, Vector2 p, Vector2 v, float size, Color c, float life, float gravity)
    {
        float spin = Random.Range(-500f, 500f), rot = Random.Range(0f, 360f);
        for (float t = 0f; t < life; t += Time.deltaTime)
        {
            if (sr == null) yield break;
            v.y -= gravity * Time.deltaTime;
            v *= 1f - Mathf.Min(1f, 2.2f * Time.deltaTime);
            p += v * Time.deltaTime;
            rot += spin * Time.deltaTime;
            sr.transform.SetPositionAndRotation(p, Quaternion.Euler(0, 0, rot));
            sr.transform.localScale = new Vector3(size, gravity > 0f ? size * 0.6f : size, 1f);
            c.a = 1f - Mathf.Pow(t / life, 3f);
            sr.color = c;
            yield return null;
        }
        if (sr != null) Destroy(sr.gameObject);
    }

    // ---------------------------------------------------------------- test helpers
    public void DropPair(int tier)
    {
        SpawnSpud(tier, new Vector2(-0.8f, -2.5f), new Vector2(2f, 0f));
        SpawnSpud(tier, new Vector2(0.8f, -2.5f), new Vector2(-2f, 0f));
    }
}
