using System.Text.Json;
using System.Text.Json.Serialization;
using PolyMod;
using UnityEngine;

namespace PolytopiaCustomBackground;

public class BgConfig
{
    [JsonInclude]
    public string gradientPreset = "original";

    [JsonInclude]
    public List<string> customGradient = new() { "#0F2027", "#203A43", "#2C5364" };

    [JsonInclude]
    public float gradientHue = 0f;

    [JsonInclude]
    public float gradientBrightness = 1f;

    [JsonInclude]
    public string backgroundFit = "cover";

    [JsonInclude]
    public bool showLandscape = true;

    [JsonInclude]
    public bool showStars = true;
    public const string CONFIG_FILE = "custombg.json";
    public const string FOREGROUND_FILE = "foreground.png";
    public const string BACKGROUND_FILE = "background.png";
    public const string LOGO_FILE = "logo.png";

    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static BgConfig Instance { get; private set; } = new();

    public static string BG_FOLDER => Path.Combine(Plugin.BASE_PATH, "BG");
    public static string ConfigPath => Path.Combine(BG_FOLDER, CONFIG_FILE);
    public static string ForegroundPath => Path.Combine(BG_FOLDER, FOREGROUND_FILE);
    public static string BackgroundPath => Path.Combine(BG_FOLDER, BACKGROUND_FILE);
    public static string LogoPath => Path.Combine(BG_FOLDER, LOGO_FILE);

    public static void Read()
    {
        Directory.CreateDirectory(BG_FOLDER);
        if (!File.Exists(ConfigPath))
        {
            bool legacy = File.Exists(BackgroundPath) && !File.Exists(ForegroundPath);
            Instance = new BgConfig { showLandscape = !legacy, showStars = !legacy };
            Save();
        }
        else
        {
            try
            {
                Instance = JsonSerializer.Deserialize<BgConfig>(File.ReadAllText(ConfigPath), jsonOptions) ?? new BgConfig();
            }
            catch (Exception e)
            {
                Main.modLogger!.LogError($"Could not read {CONFIG_FILE}, using defaults: {e.Message}");
                Instance = new BgConfig();
            }
        }
        Instance.customGradient ??= new BgConfig().customGradient;
        Instance.gradientHue = Mathf.Repeat(Instance.gradientHue, 360f);
        Instance.gradientBrightness = Mathf.Clamp(Instance.gradientBrightness, 0.5f, 1.5f);
    }

    public static void Save()
    {
        try
        {
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Instance, jsonOptions));
        }
        catch (Exception e)
        {
            Main.modLogger!.LogError($"Could not write {CONFIG_FILE}: {e.Message}");
        }
    }
}
