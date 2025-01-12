using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace PolytopiaCustomBackground;

public static class Main
{
    public static readonly string BASE_PATH = Path.Combine(BepInEx.Paths.BepInExRootPath, "..");
    private static bool hasBg = false;
    public static void Load()
    {
        Harmony.CreateAndPatchAll(typeof(Main));
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(StartSceneBg), nameof(StartSceneBg.Start))]
    private static void StartSceneBg_Start(StartSceneBg __instance)
    {
        string fileName = "background.png";
        string filePath = Path.Combine(BASE_PATH, fileName);

        if (File.Exists(filePath))
        {
            __instance.nature.sprite = PolyMod.SpritesLoader.BuildSprite(File.ReadAllBytes(filePath), new Vector2(0, 0));
            __instance.starContainer.gameObject.SetActive(false);
            __instance.gradientBgSprite.gameObject.SetActive(false);
            hasBg = true;
            Console.WriteLine("Background image found and set!");
        }
        else
        {
            Console.WriteLine("Background image not found!");
        }
    }

    [HarmonyPrefix]
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
