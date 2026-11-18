using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace PolytopiaCustomBackground;

public static class BackgroundManager
{
    public record Preset(string Id, string[] Colors);

    public static readonly Preset[] Presets = {
        new("original", Array.Empty<string>()),
        new("custom", Array.Empty<string>()),
        new("dusk", new[] { "#1A1B41", "#5B3A7A", "#E07A5F" }),
        new("sunset", new[] { "#2B1B3D", "#C2416B", "#F9A03F" }),
        new("ocean", new[] { "#03254C", "#1167B1", "#9ED8F0" }),
        new("aurora", new[] { "#061A2C", "#0E6B5C", "#7FE0A6" }),
        new("midnight", new[] { "#000008", "#0B1A3A", "#233A6B" }),
        new("lava", new[] { "#1A0000", "#7A1400", "#FF6A00" }),
        new("rose", new[] { "#3D1F3D", "#B5658E", "#F6C6D5" }),
        new("mono", new[] { "#101010", "#6A6A6A" }),
    };

    public static readonly string[] FitModes = { "cover", "stretch", "fit" };

    private const int SKY_ORDER = -1;
    private const int BACKGROUND_ORDER = 0;
    private const int GRADIENT_HEIGHT = 256;
    private const float FILL_OVERLAP = 0.001f;
    private const int LOGO_SPRITE = 1718;
    private const int LOGO_SPRITE_CHAMPIONSHIP = 2118;
    private const int SMALL_LOGO_SPRITE = 1414;
    private const float LOGO_MAX_SHARE_OF_FREE_SPACE = 0.8f;

    private static bool imagesLoaded;
    private static Texture2D? foregroundTexture, backgroundTexture, logoTexture;
    private static Sprite? foregroundSprite, backgroundSprite, logoSprite;
    private static Sprite? gradientSprite, whiteSprite;

    private static Sprite? originalLandscape;
    private static Material? originalGradientMaterial;
    private static float landscapeScale;

    private static SpriteRenderer? gradientLayer, fillLayer, imageLayer;
    private static Color fillColor;

    private static StartScreen_UI2? startScreen;
    private static Vector2 logoSize, smallLogoSize;
    private static bool logoReplaced;

    public static Preset CurrentPreset()
    {
        string id = BgConfig.Instance.gradientPreset ?? "original";
        return Presets.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? Presets[0];
    }

    public static bool IsCustomized
    {
        get
        {
            EnsureImages();
            return backgroundTexture != null || !BgConfig.Instance.showLandscape || GradientColors().Count > 0;
        }
    }

    public static void OnBeforeStart(StartSceneBg bg)
    {
        EnsureImages();
        originalLandscape = bg.nature.sprite;
        originalGradientMaterial = bg.gradientBgSprite.sharedMaterial;
        bg.nature.sprite = LandscapeSprite();
    }

    public static void OnAfterStart(StartSceneBg bg)
    {
        landscapeScale = bg.natureSize.x / bg.nature.sprite.bounds.size.x;
        Apply(bg);
    }

    public static void OnScreenSizeChanged(StartSceneBg bg) => Layout(bg);

    public static void OnUpdate(StartSceneBg bg)
    {
        Color skyTint = bg.gradientBgSprite.color;
        if (gradientLayer != null)
            gradientLayer.color = skyTint;
        if (fillLayer != null)
            fillLayer.color = fillColor * skyTint;
        if (imageLayer != null)
            imageLayer.color = bg.nature.color;
    }

    public static void ApplyToCurrentScene()
    {
        try
        {
            if (startScreen != null)
                ApplyLogo(startScreen, relayout: true);
            if (StartSceneBg.instance != null && originalLandscape != null)
                Apply(StartSceneBg.instance);
        }
        catch (Exception e)
        {
            Main.modLogger!.LogError($"Could not apply background settings: {e}");
        }
    }

    public static void ReloadImages()
    {
        foreach (UnityEngine.Object? asset in new UnityEngine.Object?[] { foregroundSprite, backgroundSprite, logoSprite, foregroundTexture, backgroundTexture, logoTexture })
        {
            if (asset != null)
                UnityEngine.Object.Destroy(asset);
        }
        foregroundSprite = backgroundSprite = logoSprite = null;
        foregroundTexture = backgroundTexture = logoTexture = null;
        imagesLoaded = false;
    }

    private static void Apply(StartSceneBg bg)
    {
        EnsureImages();
        BgConfig config = BgConfig.Instance;
        List<Color> colors = GradientColors();
        bool coveredByImage = backgroundTexture != null && config.backgroundFit != "fit";

        bg.nature.sprite = LandscapeSprite();
        bg.nature.enabled = config.showLandscape;
        bg.natureSize = (Vector2)bg.nature.sprite.bounds.size * landscapeScale;
        bg.starContainer.gameObject.SetActive(config.showStars);

        SpriteRenderer gameGradient = bg.gradientBgSprite;
        gameGradient.sortingOrder = SKY_ORDER;
        gameGradient.enabled = colors.Count == 0 && !coveredByImage;
        TintGameGradient(gameGradient);

        gradientLayer = Layer(bg, "CustomBG_Gradient", SKY_ORDER);
        gradientLayer.sprite = colors.Count > 0 ? GradientSprite(colors) : null;
        gradientLayer.enabled = colors.Count > 0 && !coveredByImage;

        fillLayer = Layer(bg, "CustomBG_SkyFill", SKY_ORDER);
        fillLayer.sprite = whiteSprite ??= CreateSprite(Texture2D.whiteTexture);
        fillColor = colors.Count > 0 ? colors[^1] : GameGradientBottomColor(gameGradient);
        fillLayer.enabled = !coveredByImage && (foregroundTexture != null || !config.showLandscape);

        imageLayer = Layer(bg, "CustomBG_Background", BACKGROUND_ORDER);
        imageLayer.sprite = backgroundTexture != null ? backgroundSprite ??= CreateSprite(backgroundTexture) : null;
        imageLayer.enabled = backgroundTexture != null;

        OnUpdate(bg);
        bg.RefreshLayout();
        Layout(bg);
    }

    private static void Layout(StartSceneBg bg)
    {
        Bounds screen = bg.cameraBounds;
        Transform sky = bg.gradientBgSprite.transform;
        Bounds skyLocal = bg.gradientBgSprite.sprite.bounds;
        Vector2 skyCenter = sky.TransformPoint(skyLocal.center);
        Vector2 skySize = Vector3.Scale(skyLocal.size, sky.lossyScale);
        float skyBottom = skyCenter.y - skySize.y / 2f + screen.size.y * FILL_OVERLAP;
        float z = sky.position.z;

        Place(gradientLayer, skyCenter, skySize, z);
        Place(fillLayer, new Vector2(screen.center.x, (skyBottom + screen.min.y) / 2f), new Vector2(screen.size.x, Mathf.Max(0f, skyBottom - screen.min.y)), z);
        Place(imageLayer, screen.center, BackgroundSize(screen.size), z);
    }

    private static Vector2 BackgroundSize(Vector2 screenSize)
    {
        if (backgroundSprite == null)
            return Vector2.zero;
        Vector2 imageSize = backgroundSprite.bounds.size;
        float coverScale = Mathf.Max(screenSize.x / imageSize.x, screenSize.y / imageSize.y);
        float fitScale = Mathf.Min(screenSize.x / imageSize.x, screenSize.y / imageSize.y);
        return BgConfig.Instance.backgroundFit switch
        {
            "stretch" => screenSize,
            "fit" => imageSize * fitScale,
            _ => imageSize * coverScale,
        };
    }

    private static SpriteRenderer Layer(StartSceneBg bg, string name, int order)
    {
        Transform child = bg.transform.Find(name);
        if (child == null)
        {
            child = new GameObject(name).transform;
            child.SetParent(bg.transform, false);
            child.gameObject.layer = bg.gradientBgSprite.gameObject.layer;
            child.gameObject.AddComponent<SpriteRenderer>();
        }
        SpriteRenderer layer = child.GetComponent<SpriteRenderer>();
        layer.sharedMaterial = bg.nature.sharedMaterial;
        layer.sortingLayerID = bg.gradientBgSprite.sortingLayerID;
        layer.sortingOrder = order;
        return layer;
    }

    private static void Place(SpriteRenderer? layer, Vector2 center, Vector2 size, float z)
    {
        if (layer == null || layer.sprite == null)
            return;
        Vector2 spriteSize = layer.sprite.bounds.size;
        Vector3 parentScale = layer.transform.parent.lossyScale;
        layer.transform.position = new Vector3(center.x, center.y, z);
        layer.transform.localScale = new Vector3(size.x / spriteSize.x / parentScale.x, size.y / spriteSize.y / parentScale.y, 1f);
    }

    private static Sprite LandscapeSprite()
    {
        if (foregroundTexture == null)
            return originalLandscape!;
        Vector2 pivot = originalLandscape!.pivot / originalLandscape.rect.size;
        return foregroundSprite ??= CreateSprite(foregroundTexture, pivot, foregroundTexture.width / originalLandscape.bounds.size.x);
    }

    private static Sprite GradientSprite(List<Color> colors)
    {
        if (gradientSprite == null)
        {
            Texture2D texture = new(1, GRADIENT_HEIGHT, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            gradientSprite = CreateSprite(texture);
        }

        Texture2D pixels = gradientSprite.texture;
        for (int y = 0; y < GRADIENT_HEIGHT; y++)
            pixels.SetPixel(0, y, Sample(colors, 1f - y / (GRADIENT_HEIGHT - 1f)));
        pixels.Apply();
        return gradientSprite;
    }

    private static Color Sample(List<Color> colors, float t)
    {
        if (colors.Count == 1)
            return colors[0];
        float position = t * (colors.Count - 1);
        int index = Mathf.Min((int)position, colors.Count - 2);
        return Color.Lerp(colors[index], colors[index + 1], position - index);
    }

    private static List<Color> GradientColors()
    {
        Preset preset = CurrentPreset();
        IEnumerable<string> hexColors = preset.Id == "custom" ? BgConfig.Instance.customGradient : preset.Colors;
        List<Color> colors = new();
        foreach (string hex in hexColors)
        {
            Color? color = ParseHex(hex);
            if (color.HasValue)
                colors.Add(Adjust(color.Value));
        }
        return colors;
    }

    private static Color? ParseHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return null;
        string s = hex.Trim().TrimStart('#');
        if (s.Length == 3)
            s = string.Concat(s.Select(c => $"{c}{c}"));
        if ((s.Length != 6 && s.Length != 8) || !uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out uint v))
        {
            Main.modLogger!.LogWarning($"'{hex}' is not a hex color like #RRGGBB.");
            return null;
        }
        if (s.Length == 6)
            v = (v << 8) | 0xFF;
        return new Color32((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    private static Color Adjust(Color color)
    {
        Color.RGBToHSV(color, out float h, out float s, out float v);
        h = Mathf.Repeat(h + BgConfig.Instance.gradientHue / 360f, 1f);
        v = Mathf.Clamp01(v * BgConfig.Instance.gradientBrightness);
        Color result = Color.HSVToRGB(h, s, v);
        result.a = color.a;
        return result;
    }

    private static IEnumerable<string> ShaderColorNames(Material material)
    {
        Shader shader = material.shader;
        for (int i = 0; i < shader.GetPropertyCount(); i++)
        {
            string name = shader.GetPropertyName(i);
            if (shader.GetPropertyType(i) == ShaderPropertyType.Color && name != "White" && !name.StartsWith("unity_"))
                yield return name;
        }
    }

    private static void TintGameGradient(SpriteRenderer gradient)
    {
        foreach (string name in ShaderColorNames(originalGradientMaterial!))
            gradient.material.SetColor(name, Adjust(originalGradientMaterial!.GetColor(name)));
    }

    private static Color GameGradientBottomColor(SpriteRenderer gradient)
    {
        Color pink = ColorConstants.pink;
        string bottom = ShaderColorNames(originalGradientMaterial!)
            .OrderByDescending(name => ((Vector4)(originalGradientMaterial!.GetColor(name) - pink)).sqrMagnitude)
            .First();
        Color color = gradient.material.GetColor(bottom);
        color.a = 1f;
        return color;
    }

    public static void StyleTopFade(UIScroller_UI2 scroller)
    {
        Image fade = scroller.topFade;
        if (fade == null)
            return;
        EnsureImages();
        fade.gameObject.SetActive(backgroundTexture == null);
        List<Color> colors = GradientColors();
        fade.color = colors.Count > 0 ? colors[0] : Adjust(ColorConstants.pink);
    }

    public static void OnStartScreenInit(StartScreen_UI2 screen)
    {
        startScreen = screen;
        logoSize = screen.logo.rectTransform.sizeDelta;
        smallLogoSize = screen.logoSmall.rectTransform.sizeDelta;
        logoReplaced = false;
        ApplyLogo(screen, relayout: false);
    }

    public static void OnStartScreenLayout(StartScreen_UI2 screen, ScreenBase_UI2.ScreenSize screenSize)
    {
        if (logoReplaced)
            FitLogo(screen, screenSize.safeRect.Top - screen.startButton.GetTop());
    }

    private static void ApplyLogo(StartScreen_UI2 screen, bool relayout)
    {
        if (screen.logo == null || screen.logoSmall == null)
        {
            startScreen = null;
            return;
        }

        EnsureImages();
        if (logoTexture != null)
        {
            screen.logo.image.sprite = screen.logoSmall.image.sprite = logoSprite ??= CreateSprite(logoTexture);
            logoReplaced = true;
            FitLogo(screen, 0f);
        }
        else if (logoReplaced)
        {
            SpriteAtlasManager atlas = GameManager.GetSpriteAtlasManager();
            screen.logo.image.sprite = atlas.GetSprite(screen.isBeforeWorldChampionship2026 ? LOGO_SPRITE_CHAMPIONSHIP : LOGO_SPRITE);
            screen.logoSmall.image.sprite = atlas.GetSprite(SMALL_LOGO_SPRITE);
            screen.logo.rectTransform.sizeDelta = logoSize;
            screen.logoSmall.rectTransform.sizeDelta = smallLogoSize;
            logoReplaced = false;
        }
        else
        {
            return;
        }

        if (relayout)
            screen.UpdateLayout();
    }

    private static void FitLogo(StartScreen_UI2 screen, float freeSpace)
    {
        if (logoSprite == null || logoSize.x <= 0f || logoSize.y <= 0f)
            return;
        Vector2 size = logoSprite.rect.size;
        float maxHeight = Mathf.Max(logoSize.y, freeSpace * LOGO_MAX_SHARE_OF_FREE_SPACE);
        float scale = Mathf.Min(logoSize.x / size.x, maxHeight / size.y);
        screen.logo.rectTransform.sizeDelta = size * scale;
    }

    private static void EnsureImages()
    {
        if (imagesLoaded)
            return;
        imagesLoaded = true;
        foregroundTexture = LoadTexture(BgConfig.ForegroundPath);
        backgroundTexture = LoadTexture(BgConfig.BackgroundPath);
        logoTexture = LoadTexture(BgConfig.LogoPath);
    }

    private static Texture2D? LoadTexture(string path)
    {
        if (!File.Exists(path))
            return null;
        try
        {
            Texture2D texture = PolyMod.Managers.Visual.BuildTexture(File.ReadAllBytes(path));
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }
        catch (Exception e)
        {
            Main.modLogger!.LogError($"Could not load {path}: {e.Message}");
            return null;
        }
    }

    private static Sprite CreateSprite(Texture2D texture) => CreateSprite(texture, new Vector2(0.5f, 0.5f), 100f);

    private static Sprite CreateSprite(Texture2D texture, Vector2 pivot, float pixelsPerUnit)
    {
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
