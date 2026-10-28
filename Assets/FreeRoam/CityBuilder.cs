using System.Collections.Generic;
using UnityEngine;

// The city from the browser version: a 16 x 16 grid of 40 m blocks with 14 m roads between them.
public static class CityBuilder
{
    const int N = 16;
    const float BLOCK = 40f, ROAD = 14f, P = BLOCK + ROAD, HALF = N * P / 2f;
    static readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();
    static Material template;
    static readonly Color[] BUILDINGS =
    {
        new Color(0.80f, 0.76f, 0.68f), new Color(0.62f, 0.66f, 0.72f), new Color(0.72f, 0.50f, 0.38f),
        new Color(0.55f, 0.58f, 0.60f), new Color(0.88f, 0.85f, 0.78f), new Color(0.46f, 0.55f, 0.52f),
    };

    public static void Build(Transform parent)
    {
        var rnd = new System.Random(7);
        // asphalt under everything: roads are the gaps between the pavement slabs
        Box(parent, new Vector3(0, -0.5f, 0), new Vector3(HALF * 2, 1, HALF * 2), new Color(0.17f, 0.18f, 0.20f));
        for (int i = 0; i < N; i++)
        for (int j = 0; j < N; j++)
        {
            float x0 = -HALF + ROAD / 2f + i * P, z0 = -HALF + ROAD / 2f + j * P;
            Box(parent, new Vector3(x0 + BLOCK / 2f, 0.075f, z0 + BLOCK / 2f), new Vector3(BLOCK, 0.15f, BLOCK), new Color(0.66f, 0.65f, 0.62f));
            float h = 10f + (float)rnd.NextDouble() * 35f;
            var c = BUILDINGS[rnd.Next(BUILDINGS.Length)];
            Box(parent, new Vector3(x0 + BLOCK / 2f, 0.15f + h / 2f, z0 + BLOCK / 2f), new Vector3(BLOCK - 6f, h, BLOCK - 6f), c);
        }
    }

    public static GameObject Box(Transform parent, Vector3 pos, Vector3 size, Color color)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = Mat(color);
        return g;
    }

    // every colour is a copy of the URP default material from a primitive, so it always uses a valid shader
    public static Material Mat(Color c)
    {
        if (!cache.TryGetValue(c, out var m) || m == null)
        {
            if (template == null)
            {
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                template = probe.GetComponent<Renderer>().sharedMaterial;
                Object.Destroy(probe);
            }
            m = new Material(template);
            m.SetColor("_BaseColor", c);
            cache[c] = m;
        }
        return m;
    }
}
