using UnityEngine;
using UnityEngine.UI;

public class NeonFloating : MonoBehaviour
{
    public static NeonFloating Instance { get; private set; }

    [Header("Particle Settings")]
    [Tooltip("Total number of floating diamond particles")]
    public int particleCount = 60; // Lowered count so it isn't too cluttered
    
    [Tooltip("Base speed particles float upwards")]
    public float baseSpeed = 30f; 
    
    [Tooltip("Width of the side-to-side wobble")]
    public float wobbleWidth = 35f; 
    
    [Header("Glow & Heartbeat Settings")]
    [Tooltip("Maximum opacity of the particles when they pulse brightly.")]
    public float particleOpacity = 0.6f; 

    [Tooltip("How fast the heartbeat/twinkle effect pulses.")]
    public float pulseSpeedMultiplier = 1.5f;

    private NeonParticle[] particles;
    private Vector2 screenSize = new Vector2(1280, 720);

    // Neon Colors
    private readonly Color C_CYAN = new Color(0f, 0.9f, 1f);
    private readonly Color C_MAGENTA = new Color(1f, 0.15f, 0.75f);
    private readonly Color C_GREEN = new Color(0.1f, 1f, 0.6f);
    private readonly Color C_YELLOW = new Color(1f, 0.95f, 0.15f);
    private readonly Color C_RED = new Color(1f, 0.2f, 0.35f);

    private Sprite glowingDiamondSprite;

    private class NeonParticle
    {
        public RectTransform rt;
        public Image img;
        public float speedY;
        public float wobble;
        public float offset;
        public Color baseColor; 
        public float pulseSpeed;
        public float pulseOffset;
    }

    void Awake() { if (Instance != null) { Destroy(gameObject); return; } Instance = this; }

    void Start()
    {
        // 1. Create the FAKED GLOW texture
        glowingDiamondSprite = MakeSoftGlowDiamond();

        // 2. Generate the particles
        GenerateEnvironment();
    }

    void GenerateEnvironment()
    {
        particles = new NeonParticle[particleCount];
        
        GameObject canvasObj = new GameObject("_NeonFloatingCanvas");
        Canvas cv = canvasObj.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceCamera;
        cv.worldCamera = Camera.main;
        cv.planeDistance = 5f; 
        cv.sortingOrder = -10; // Keep behind the game board

        CanvasScaler cs = canvasObj.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = screenSize;
        cs.matchWidthOrHeight = 0.5f;

        Color[] availableColors = new Color[] { C_CYAN, C_MAGENTA, C_GREEN, C_YELLOW, C_RED };

        for (int i = 0; i < particleCount; i++)
        {
            GameObject pObj = new GameObject("NeonGlow_" + i);
            pObj.transform.SetParent(canvasObj.transform, false);
            
            Image img = pObj.AddComponent<Image>();
            img.sprite = glowingDiamondSprite;
            img.type = Image.Type.Simple;

            Color assignedColor = availableColors[Random.Range(0, availableColors.Length)];

            RectTransform rt = pObj.GetComponent<RectTransform>();
            
            // Made them slightly larger so the soft edges show up better
            float size = Random.Range(15f, 45f); 
            rt.sizeDelta = new Vector2(size, size);
            rt.localRotation = Quaternion.Euler(0, 0, 45f); // Turn into a diamond

            rt.anchoredPosition = new Vector2(
                Random.Range(-screenSize.x / 2f, screenSize.x / 2f),
                Random.Range(-screenSize.y / 2f, screenSize.y / 2f)
            );

            particles[i] = new NeonParticle
            {
                rt = rt,
                img = img,
                speedY = Random.Range(baseSpeed * 0.5f, baseSpeed * 1.5f),
                wobble = Random.Range(0.5f, 2.0f),
                offset = Random.Range(0f, 100f),
                baseColor = assignedColor,
                pulseSpeed = Random.Range(0.5f, 2.0f) * pulseSpeedMultiplier,
                pulseOffset = Random.Range(0f, 10f)
            };
        }
    }

    void Update()
    {
        if (particles == null) return;

        for (int i = 0; i < particles.Length; i++)
        {
            NeonParticle p = particles[i];
            Vector2 pos = p.rt.anchoredPosition;

            // 1. FLOAT AND WOBBLE
            pos.y += p.speedY * Time.deltaTime;
            pos.x += Mathf.Sin(Time.time * p.wobble + p.offset) * wobbleWidth * Time.deltaTime;

            if (pos.y > (screenSize.y / 2f) + 100f)
            {
                pos.y = -(screenSize.y / 2f) - 100f;
                pos.x = Random.Range(-screenSize.x / 2f, screenSize.x / 2f);
            }

            p.rt.anchoredPosition = pos;

            // 2. HEARTBEAT / TWINKLE PULSE
            float rawSine = Mathf.Sin(Time.time * p.pulseSpeed + p.pulseOffset);
            float pulseMath = Mathf.Pow((rawSine + 1f) / 2f, 2.5f); 
            float currentAlpha = Mathf.Lerp(particleOpacity * 0.05f, particleOpacity, pulseMath);
            
            p.img.color = new Color(p.baseColor.r, p.baseColor.g, p.baseColor.b, currentAlpha);
        }
    }

    // ── THE FIX: SOFT FADING GLOW TEXTURE ──
    static Sprite MakeSoftGlowDiamond()
    {
        int size = 128; 
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                
                // Calculate distance from center (diamond shape)
                float d = Mathf.Abs(u - 0.5f) + Mathf.Abs(v - 0.5f);
                
                // Inverse distance so the center is 1 and edges are 0
                float glow = Mathf.Clamp01(1f - (d * 2f));
                
                // Exponential falloff: This makes it soft and blurry like a light flare
                float alpha = Mathf.Pow(glow, 2.5f); 

                px[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(px);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}