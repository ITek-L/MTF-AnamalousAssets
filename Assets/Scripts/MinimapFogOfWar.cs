using UnityEngine;
using UnityEngine.UI;

public class MinimapFogOfWar : MonoBehaviour
{
    [Header("UI")]
    public RawImage fogImage; // MinimapFog RawImage

    [Header("World Mapping")]
    public DungeonGenerator2D generator; // to get width/height
    public Transform player;

    [Header("Fog Settings")]
    public int textureSize = 512;
    public float revealRadiusWorld = 8f;   // how far the scanner reveals
    public float revealStrength = 1f;      // 1 = fully clear
    public int revealStepsPerFrame = 1;    // keep low (perf)

    Texture2D fogTex;
    Color32[] pixels;

    void Start()
    {
        if (!generator) generator = FindFirstObjectByType<DungeonGenerator2D>();
        InitFog();
    }

    void InitFog()
    {
        fogTex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        fogTex.filterMode = FilterMode.Bilinear;
        fogTex.wrapMode = TextureWrapMode.Clamp;

        pixels = new Color32[textureSize * textureSize];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(0, 0, 0, 255); // fully black/opaque

        fogTex.SetPixels32(pixels);
        fogTex.Apply(false);

        if (fogImage) fogImage.texture = fogTex;
    }

    void Update()
    {
        if (!fogTex || !fogImage || !generator || !player) return;

        // Reveal around player
        RevealCircle(player.position, revealRadiusWorld);
    }

    void RevealCircle(Vector3 worldPos, float radiusWorld)
    {
        // Convert world position into fog texture coords
        // Your generator uses tile coords centered around 0,0: [-width/2..width/2], same for height.
        float halfW = generator.width / 2f;
        float halfH = generator.height / 2f;

        float u = (worldPos.x + halfW) / generator.width;
        float v = (worldPos.y + halfH) / generator.height;

        int cx = Mathf.RoundToInt(u * (textureSize - 1));
        int cy = Mathf.RoundToInt(v * (textureSize - 1));

        float rTex = (radiusWorld / generator.width) * textureSize; // approximate scaling by width
        int r = Mathf.CeilToInt(rTex);

        int minX = Mathf.Clamp(cx - r, 0, textureSize - 1);
        int maxX = Mathf.Clamp(cx + r, 0, textureSize - 1);
        int minY = Mathf.Clamp(cy - r, 0, textureSize - 1);
        int maxY = Mathf.Clamp(cy + r, 0, textureSize - 1);

        float r2 = rTex * rTex;

        for (int y = minY; y <= maxY; y++)
        {
            int dy = y - cy;
            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - cx;
                if (dx * dx + dy * dy > r2) continue;

                int idx = y * textureSize + x;

                // Reduce alpha toward 0 (revealed)
                byte a = pixels[idx].a;
                if (a == 0) continue;

                int newA = Mathf.RoundToInt(a * (1f - revealStrength));
                pixels[idx].a = (byte)Mathf.Clamp(newA, 0, 255);
            }
        }

        fogTex.SetPixels32(pixels);
        fogTex.Apply(false);
    }
}
