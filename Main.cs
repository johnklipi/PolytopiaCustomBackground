using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace PolytopiaCustomBackground;

public static class Main
{
    public static readonly string BASE_PATH = Path.Combine(BepInEx.Paths.BepInExRootPath, "..");
    private static bool hasBg = false;
    private static ManualLogSource? modLogger;
    public static void Load(ManualLogSource logger)
    {
        modLogger = logger;
        Harmony.CreateAndPatchAll(typeof(Main));
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(StartSceneBg), nameof(StartSceneBg.Start))]
    private static void StartSceneBg_Start(StartSceneBg __instance)
    {
        string fileName = "background.png";
        string filePath = Path.Combine(BASE_PATH, fileName);

        if (!File.Exists(filePath))
        {
            modLogger!.LogInfo("Background image not found!");
            return;
        }
        __instance.nature.sprite = PolyMod.Managers.Visual.BuildSprite(File.ReadAllBytes(filePath), new Vector2(0, 0));
        __instance.starContainer.gameObject.SetActive(false);
        __instance.gradientBgSprite.gameObject.SetActive(false);
        hasBg = true;
        modLogger!.LogInfo("Background image found and set!");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSetupScreenView), nameof(GameSetupScreenView.Init))]
    private static void GameSetupScreenView_Init(GameSetupScreenView __instance, RectTransform holder)
    {
        if(hasBg)
            __instance.scroller.ShowTopFade(false);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameModeScreen_UI2), nameof(GameModeScreen_UI2.Init))]
    private static void GameModeScreen_UI2_Init(GameModeScreen_UI2 __instance, RectTransform transform)
    {
        if(hasBg)
            __instance.scroll.ShowTopFade(false);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TribePickerScreen_UI2), nameof(TribePickerScreen_UI2.Init))]
    private static void TribePickerScreen_UI2_Init(TribePickerScreen_UI2 __instance, RectTransform transform)
    {
        if(hasBg)
            __instance.scroll.ShowTopFade(false);
    }

    [HarmonyPrefix] // Old ui
    [HarmonyPatch(typeof(ScrollerGradient), nameof(ScrollerGradient.OnEnable))]
    private static void ScrollerGradient_OnEnable(ScrollerGradient __instance)
    {
        if(hasBg)
        {
            Image[] allImages = GameObject.FindObjectsOfType<Image>();
            foreach (Image image in allImages)
            {
                if (image.gameObject.name == "ScrollerTopGradient")
                {
                    GameObject.Destroy(image);
                }
            }
        }
    }
}
