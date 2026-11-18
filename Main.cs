using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace PolytopiaCustomBackground;

public static class Main
{
    public static ManualLogSource? modLogger;

    public static void Load(ManualLogSource logger)
    {
        modLogger = logger;
        BgConfig.Read();
        Harmony.CreateAndPatchAll(typeof(Main));
        Harmony.CreateAndPatchAll(typeof(UI));
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(StartSceneBg), nameof(StartSceneBg.Start))]
    private static void StartSceneBg_Start_Prefix(StartSceneBg __instance)
    {
        try
        {
            BackgroundManager.OnBeforeStart(__instance);
        }
        catch (Exception e)
        {
            modLogger!.LogError($"Could not set up the menu background: {e}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartSceneBg), nameof(StartSceneBg.Start))]
    private static void StartSceneBg_Start_Postfix(StartSceneBg __instance)
    {
        try
        {
            BackgroundManager.OnAfterStart(__instance);
        }
        catch (Exception e)
        {
            modLogger!.LogError($"Could not set up the menu background: {e}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartSceneBg), nameof(StartSceneBg.OnScreenSizeChanged))]
    private static void StartSceneBg_OnScreenSizeChanged(StartSceneBg __instance) => BackgroundManager.OnScreenSizeChanged(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartSceneBg), nameof(StartSceneBg.Update))]
    private static void StartSceneBg_Update(StartSceneBg __instance) => BackgroundManager.OnUpdate(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartScreen_UI2), nameof(StartScreen_UI2.Init))]
    private static void StartScreen_UI2_Init(StartScreen_UI2 __instance)
    {
        try
        {
            BackgroundManager.OnStartScreenInit(__instance);
        }
        catch (Exception e)
        {
            modLogger!.LogError($"Could not set up the menu logo: {e}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartScreen_UI2), nameof(StartScreen_UI2.RunLayout))]
    private static void StartScreen_UI2_RunLayout(StartScreen_UI2 __instance, ScreenBase_UI2.ScreenSize screenSize)
    {
        try
        {
            BackgroundManager.OnStartScreenLayout(__instance, screenSize);
        }
        catch (Exception e)
        {
            modLogger!.LogError($"Could not lay out the menu logo: {e}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIScroller_UI2), nameof(UIScroller_UI2.LayoutTopFade))]
    private static void UIScroller_UI2_LayoutTopFade(UIScroller_UI2 __instance)
    {
        try
        {
            BackgroundManager.StyleTopFade(__instance);
        }
        catch (Exception e)
        {
            modLogger!.LogError($"Could not style the scroll fade: {e}");
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ScrollerGradient), nameof(ScrollerGradient.OnEnable))]
    private static void ScrollerGradient_OnEnable(ScrollerGradient __instance)
    {
        if (!BackgroundManager.IsCustomized)
            return;
        Image[] allImages = GameObject.FindObjectsOfType<Image>();
        foreach (Image image in allImages)
        {
            if (image.gameObject.name == "ScrollerTopGradient")
                GameObject.Destroy(image);
        }
    }
}
