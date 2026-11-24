using System;
using UnityEngine;

// Draws every Spud Bowl sprite in code: the eight spud characters and a few UI shapes.
public static class SpudArt
{
    const int N = 256;
    const float CX = 128f, CY = 112f, RX = 86f, RY = 78f;
    public const float BodyPixels = 82f; // pixels per unit, so the body radius is ~1 unit

    class Canvas
    {
        public readonly int size;
        public readonly Color[] px;
        public Canvas(int size) { this.size = size; px = new Color[size * size]; }

        public void Shape(Func<float, float, float> sdf, Color col) => Shape(sdf, (x, y) => col);

        public void Shape(Func<float, float, float> sdf, Func<float, float, Color> colAt)
        {
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = sdf(x + 0.5f, y + 0.5f);
                    if (d > 0.5f) continue;
                    Color c = colAt(x + 0.5f, y + 0.5f);
                    float a = Mathf.Clamp01(0.5f - d) * c.a;
                    if (a <= 0f) continue;
                    int i = y * size + x;
                    Color dst = px[i];
                    float oa = a + dst.a * (1f - a);
                    if (oa <= 0f) continue;
                    px[i] = new Color(
                        (c.r * a + dst.r * dst.a * (1f - a)) / oa,
                        (c.g * a + dst.g * dst.a * (1f - a)) / oa,
                        (c.b * a + dst.b * dst.a * (1f - a)) / oa, oa);
                }
        }

        public Texture2D ToTexture(string name)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.SetPixels(px);
            t.Apply(true, false);
            return t;
        }
    }

    // ---------- signed distance helpers (pixel space, y up) ----------
    static float Circle(float x, float y, float cx, float cy, float r) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

    static float Ellipse(float x, float y, float cx, float cy, float rx, float ry, float angDeg = 0f)
    {
        float dx = x - cx, dy = y - cy;
        if (angDeg != 0f)
        {
            float a = -angDeg * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            float nx = dx * c - dy * s; dy = dx * s + dy * c; dx = nx;
        }
        float k = Mathf.Sqrt((dx / rx) * (dx / rx) + (dy / ry) * (dy / ry));
        return (k - 1f) * Mathf.Min(rx, ry);
    }

    static float Box(float x, float y, float cx, float cy, float hx, float hy, float r)
    {
        float qx = Mathf.Abs(x - cx) - hx + r, qy = Mathf.Abs(y - cy) - hy + r;
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    static float Segment(float x, float y, float ax, float ay, float bx, float by, float r)
    {
        float pax = x - ax, pay = y - ay, bax = bx - ax, bay = by - ay;
        float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
        float dx = pax - bax * h, dy = pay - bay * h;
        return Mathf.Sqrt(dx * dx + dy * dy) - r;
    }

    static float Polygon(float x, float y, Vector2[] v)
    {
        float d = (x - v[0].x) * (x - v[0].x) + (y - v[0].y) * (y - v[0].y);
        float s = 1f;
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
        {
            float ex = v[j].x - v[i].x, ey = v[j].y - v[i].y;
            float wx = x - v[i].x, wy = y - v[i].y;
            float h = Mathf.Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
            float bx = wx - ex * h, by = wy - ey * h;
            d = Mathf.Min(d, bx * bx + by * by);
            bool c1 = y >= v[i].y, c2 = y < v[j].y, c3 = ex * wy > ey * wx;
            if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
        }
        return s * Mathf.Sqrt(d);
    }

    static float Body(float x, float y, float seed)
    {
        float dx = x - CX, dy = y - CY;
        float ang = Mathf.Atan2(dy, dx);
        float rr = 1f + 0.035f * Mathf.Sin(3f * ang + seed) + 0.025f * Mathf.Sin(5f * ang + 2f * seed);
        float k = Mathf.Sqrt((dx / RX) * (dx / RX) + (dy / RY) * (dy / RY)) / rr;
        return (k - 1f) * Mathf.Min(RX, RY);
    }

    static Color C(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);

    // ---------- spuds ----------
    public static Sprite[] BuildSpuds()
    {
        var sprites = new Sprite[8];
        for (int t = 0; t < 8; t++)
        {
            var tex = DrawSpud(t);
            sprites[t] = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, CY / N), BodyPixels, 0, SpriteMeshType.FullRect);
            sprites[t].name = "Spud" + (t + 1);
        }
        return sprites;
    }

    static Texture2D DrawSpud(int tier)
    {
        var cv = new Canvas(N);
        float seed = tier * 1.7f + 0.4f;
        bool gold = tier == 7;
        Color baseC = gold ? C(0.98f, 0.80f, 0.28f) : C(0.93f, 0.75f, 0.47f);
        Color shadeC = gold ? C(0.86f, 0.55f, 0.12f) : C(0.78f, 0.56f, 0.31f);
        Color lineC = gold ? C(0.42f, 0.24f, 0.04f) : C(0.33f, 0.20f, 0.11f);

        // accessories that sit behind the body
        if (tier == 5) VikingHorns(cv);

        // outline + body with soft shading
        cv.Shape((x, y) => Body(x, y, seed) - 6f, lineC);
        cv.Shape((x, y) => Body(x, y, seed), (x, y) =>
        {
            float u = (x - CX) / RX, v = (y - CY) / RY;
            float t = Mathf.Clamp01(u * 0.45f - v * 0.65f + 0.25f);
            return Color.Lerp(baseC, shadeC, t * t);
        });
        // highlight and spots
        cv.Shape((x, y) => Ellipse(x, y, CX - 38, CY + 40, 22, 12, 25), C(1f, 1f, 1f, 0.28f));
        cv.Shape((x, y) => Ellipse(x, y, CX + 52, CY + 18, 6, 4, 20), C(shadeC.r * 0.85f, shadeC.g * 0.85f, shadeC.b * 0.85f, 0.8f));
        cv.Shape((x, y) => Ellipse(x, y, CX - 58, CY - 26, 5, 3.5f, -15), C(shadeC.r * 0.85f, shadeC.g * 0.85f, shadeC.b * 0.85f, 0.8f));
        cv.Shape((x, y) => Ellipse(x, y, CX + 30, CY - 50, 4, 3, 0), C(shadeC.r * 0.85f, shadeC.g * 0.85f, shadeC.b * 0.85f, 0.8f));

        Face(cv, tier);

        switch (tier)
        {
            case 1: Sprout(cv); break;
            case 2: Specs(cv); break;
            case 3: Beanie(cv); break;
            case 4: ChefHat(cv); break;
            case 5: VikingHelmet(cv); break;
            case 6: WizardHat(cv); break;
            case 7: Crown(cv); break;
        }
        return cv.ToTexture("Spud" + (tier + 1));
    }

    static void Face(Canvas cv, int tier)
    {
        Color ink = C(0.12f, 0.08f, 0.07f);
        float ey = CY + 12f, ex = 27f;
        // blush
        cv.Shape((x, y) => Ellipse(x, y, CX - 46, CY - 8, 13, 8), C(1f, 0.45f, 0.45f, 0.45f));
        cv.Shape((x, y) => Ellipse(x, y, CX + 46, CY - 8, 13, 8), C(1f, 0.45f, 0.45f, 0.45f));
        if (tier == 7)
        {
            // happy closed eyes: upper arcs
            foreach (float s in new[] { -1f, 1f })
            {
                float cx = CX + s * ex;
                cv.Shape((x, y) => Mathf.Max(Mathf.Abs(Circle(x, y, cx, ey - 4, 11)) - 3.2f, ey - 4 - y), ink);
            }
        }
        else
        {
            float ry = tier == 0 ? 9f : 12f, rx = tier == 0 ? 7f : 9f;
            foreach (float s in new[] { -1f, 1f })
            {
                float cx = CX + s * ex;
                cv.Shape((x, y) => Ellipse(x, y, cx, ey, rx, ry), ink);
                cv.Shape((x, y) => Circle(x, y, cx + 2.5f, ey + 4f, 3.2f), Color.white);
            }
            if (tier == 5)
            {
                // determined brows
                cv.Shape((x, y) => Segment(x, y, CX - 40, ey + 24, CX - 16, ey + 18, 3f), ink);
                cv.Shape((x, y) => Segment(x, y, CX + 40, ey + 24, CX + 16, ey + 18, 3f), ink);
            }
        }
        // mouth
        if (tier == 0)
            cv.Shape((x, y) => Mathf.Max(Mathf.Abs(Circle(x, y, CX, CY - 4, 7)) - 2.5f, y - (CY - 4)), ink);
        else if (tier == 4 || tier == 7)
        {
            // open happy mouth
            cv.Shape((x, y) => Mathf.Max(Circle(x, y, CX, CY - 4, 13), y - (CY - 4)), ink);
            cv.Shape((x, y) => Mathf.Max(Circle(x, y, CX, CY - 16, 7), Circle(x, y, CX, CY - 4, 11)), C(0.95f, 0.45f, 0.45f));
        }
        else
            cv.Shape((x, y) => Mathf.Max(Mathf.Abs(Circle(x, y, CX, CY - 2, 12)) - 2.8f, y - (CY - 2)), ink);
    }

    static void Sprout(Canvas cv)
    {
        Color dark = C(0.16f, 0.38f, 0.14f), leaf = C(0.45f, 0.80f, 0.30f);
        cv.Shape((x, y) => Segment(x, y, CX, 182, CX + 2, 214, 5f), dark);
        cv.Shape((x, y) => Ellipse(x, y, CX - 16, 220, 20, 10, 25) - 4f, dark);
        cv.Shape((x, y) => Ellipse(x, y, CX + 19, 224, 20, 10, -30) - 4f, dark);
        cv.Shape((x, y) => Ellipse(x, y, CX - 16, 220, 20, 10, 25), leaf);
        cv.Shape((x, y) => Ellipse(x, y, CX + 19, 224, 20, 10, -30), leaf);
    }

    static void Specs(Canvas cv)
    {
        Color ink = C(0.10f, 0.10f, 0.14f);
        float ey = CY + 12f;
        foreach (float s in new[] { -1f, 1f })
        {
            float cx = CX + s * 27f;
            cv.Shape((x, y) => Circle(x, y, cx, ey, 19), C(0.7f, 0.9f, 1f, 0.25f));
            cv.Shape((x, y) => Mathf.Abs(Circle(x, y, cx, ey, 19)) - 3.5f, ink);
        }
        cv.Shape((x, y) => Segment(x, y, CX - 9, ey + 4, CX + 9, ey + 4, 3f), ink);
        cv.Shape((x, y) => Segment(x, y, CX - 46, ey + 4, CX - 80, ey + 12, 3f), ink);
        cv.Shape((x, y) => Segment(x, y, CX + 46, ey + 4, CX + 80, ey + 12, 3f), ink);
    }

    static void Beanie(Canvas cv)
    {
        Color line = C(0.07f, 0.22f, 0.27f), main = C(0.20f, 0.70f, 0.76f), band = C(0.12f, 0.50f, 0.58f), pom = C(0.96f, 0.95f, 0.90f);
        Func<float, float, float> dome = (x, y) => Mathf.Max(Ellipse(x, y, CX, 176, 72, 56), 170 - y);
        Func<float, float, float> bnd = (x, y) => Box(x, y, CX, 172, 76, 12, 8);
        Func<float, float, float> pm = (x, y) => Circle(x, y, CX, 236, 14);
        cv.Shape((x, y) => pm(x, y) - 5f, line);
        cv.Shape((x, y) => dome(x, y) - 5f, line);
        cv.Shape((x, y) => bnd(x, y) - 5f, line);
        cv.Shape(dome, main);
        // knit stripes
        cv.Shape((x, y) => Mathf.Max(dome(x, y), Mathf.Abs(Mathf.Repeat(x - CX + 9f, 18f) - 9f) - 2f), C(0.14f, 0.58f, 0.66f));
        cv.Shape(bnd, band);
        cv.Shape(pm, pom);
    }

    static void ChefHat(Canvas cv)
    {
        Color line = C(0.50f, 0.52f, 0.58f), white = C(0.97f, 0.97f, 0.97f);
        Func<float, float, float> hat = (x, y) => Mathf.Min(Mathf.Min(Circle(x, y, CX - 30, 220, 25), Circle(x, y, CX, 232, 28)),
            Mathf.Min(Circle(x, y, CX + 30, 220, 25), Box(x, y, CX, 196, 42, 24, 6)));
        Func<float, float, float> band = (x, y) => Box(x, y, CX, 180, 48, 11, 4);
        cv.Shape((x, y) => hat(x, y) - 5f, line);
        cv.Shape((x, y) => band(x, y) - 5f, line);
        cv.Shape(hat, white);
        cv.Shape(band, C(0.90f, 0.90f, 0.92f));
        cv.Shape((x, y) => Segment(x, y, CX - 14, 200, CX - 14, 214, 2f), C(0.80f, 0.80f, 0.84f));
        cv.Shape((x, y) => Segment(x, y, CX + 14, 200, CX + 14, 214, 2f), C(0.80f, 0.80f, 0.84f));
    }

    static void VikingHorns(Canvas cv)
    {
        Color line = C(0.35f, 0.28f, 0.18f), horn = C(0.96f, 0.91f, 0.76f);
        foreach (float s in new[] { -1f, 1f })
        {
            Func<float, float, float> h = (x, y) => Mathf.Min(Segment(x, y, CX + s * 58, 182, CX + s * 84, 206, 11f),
                Mathf.Min(Segment(x, y, CX + s * 84, 206, CX + s * 92, 232, 7.5f), Segment(x, y, CX + s * 92, 232, CX + s * 88, 248, 4f)));
            cv.Shape((x, y) => h(x, y) - 4.5f, line);
            cv.Shape(h, horn);
        }
    }

    static void VikingHelmet(Canvas cv)
    {
        Color line = C(0.18f, 0.19f, 0.22f), steel = C(0.64f, 0.67f, 0.72f), band = C(0.46f, 0.48f, 0.54f);
        Func<float, float, float> dome = (x, y) => Mathf.Max(Ellipse(x, y, CX, 176, 72, 52), 168 - y);
        Func<float, float, float> bnd = (x, y) => Box(x, y, CX, 172, 78, 11, 5);
        cv.Shape((x, y) => dome(x, y) - 5f, line);
        cv.Shape((x, y) => bnd(x, y) - 5f, line);
        cv.Shape(dome, (x, y) => Color.Lerp(C(0.85f, 0.87f, 0.9f), steel, Mathf.Clamp01((x - CX + 40) / 80f)));
        cv.Shape((x, y) => Mathf.Max(dome(x, y), Mathf.Abs(x - CX) - 5f), band);
        cv.Shape(bnd, band);
        foreach (float rx in new[] { -50f, -17f, 17f, 50f })
            cv.Shape((x, y) => Circle(x, y, CX + rx, 172, 4f), C(0.86f, 0.87f, 0.9f));
    }

    static void WizardHat(Canvas cv)
    {
        Color line = C(0.15f, 0.08f, 0.28f), main = C(0.47f, 0.30f, 0.78f), brim = C(0.36f, 0.22f, 0.62f), star = C(1f, 0.86f, 0.30f);
        var tri = new[] { new Vector2(CX - 52, 182), new Vector2(CX + 50, 182), new Vector2(CX + 30, 222), new Vector2(CX + 52, 252), new Vector2(CX + 8, 230) };
        Func<float, float, float> hat = (x, y) => Polygon(x, y, tri);
        Func<float, float, float> br = (x, y) => Ellipse(x, y, CX, 180, 86, 13);
        cv.Shape((x, y) => hat(x, y) - 5f, line);
        cv.Shape((x, y) => br(x, y) - 5f, line);
        cv.Shape(hat, main);
        cv.Shape(br, brim);
        cv.Shape((x, y) => Circle(x, y, CX - 14, 200, 5.5f), star);
        cv.Shape((x, y) => Circle(x, y, CX + 18, 212, 4f), star);
        cv.Shape((x, y) => Circle(x, y, CX - 2, 222, 3f), star);
    }

    static void Crown(Canvas cv)
    {
        Color line = C(0.40f, 0.20f, 0.02f), gold = C(1f, 0.90f, 0.35f), gem = C(0.92f, 0.18f, 0.30f), gem2 = C(0.25f, 0.65f, 0.95f);
        var pts = new[] { new Vector2(CX - 50, 172), new Vector2(CX + 50, 172), new Vector2(CX + 54, 228), new Vector2(CX + 26, 202),
            new Vector2(CX, 240), new Vector2(CX - 26, 202), new Vector2(CX - 54, 228) };
        Func<float, float, float> cr = (x, y) => Polygon(x, y, pts);
        cv.Shape((x, y) => cr(x, y) - 5f, line);
        cv.Shape(cr, (x, y) => Color.Lerp(C(1f, 0.97f, 0.7f), gold, Mathf.Clamp01((x - CX + 30) / 60f)));
        cv.Shape((x, y) => Circle(x, y, CX, 188, 7.5f), gem);
        cv.Shape((x, y) => Circle(x, y, CX - 30, 186, 5f), gem2);
        cv.Shape((x, y) => Circle(x, y, CX + 30, 186, 5f), gem2);
        foreach (var p in new[] { new Vector2(CX - 54, 228), new Vector2(CX, 240), new Vector2(CX + 54, 228) })
            cv.Shape((x, y) => Circle(x, y, p.x, p.y, 6f), gold);
    }

    // ---------- simple shapes ----------
    public static Sprite Square()
    {
        var t = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "Square", filterMode = FilterMode.Bilinear };
        var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = Color.white;
        t.SetPixels(px); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }

    public static Sprite Circle(int size = 64)
    {
        var cv = new Canvas(size);
        float h = size * 0.5f;
        cv.Shape((x, y) => Circle(x, y, h, h, h - 1.5f), Color.white);
        var t = cv.ToTexture("Circle");
        return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    public static Sprite Pill()
    {
        int s = 64; var cv = new Canvas(s);
        cv.Shape((x, y) => Box(x, y, 32, 32, 30, 30, 22), Color.white);
        var t = cv.ToTexture("Pill");
        return Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(26, 26, 26, 26));
    }
}
