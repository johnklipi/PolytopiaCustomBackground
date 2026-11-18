using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace PolytopiaCustomBackground;

public static class UI
{
    private static UIHorizontalList? presetList;
    private static SettingsSliderContainer? hueSlider;
    private static SettingsSliderContainer? brightnessSlider;
    private static UIHorizontalList? fitList;
    private static UIHorizontalList? landscapeList;
    private static UIHorizontalList? starsList;

    private static readonly string[] ShowHideKeys = { "custombg.show", "custombg.hide" };

    private static bool syncing;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SettingsScreen), nameof(SettingsScreen.Init))]
    private static void SettingsScreen_Init(SettingsScreen __instance)
    {
        __instance.CreateVerticalSpacer(20f, __instance.container);
        UICollapsableContainer section = UnityEngine.Object.Instantiate(__instance.collapsableContainerPrefab, __instance.container);
        section.gameObject.name = "CustomBG_Settings";
        section.ShowLocalizationKey = "custombg.settings.show";
        section.HideLocalizationKey = "custombg.settings.hide";
        section.SetCollapsed(false);
        RectTransform content = section.GetContentRectTransform();

        presetList = NewList(__instance, content, "custombg.gradient", BackgroundManager.Presets.Select(p => $"custombg.gradient.{p.Id}").ToArray(), id => BgConfig.Instance.gradientPreset = BackgroundManager.Presets[id].Id);

        __instance.CreateVerticalSpacer(20f, content);
        hueSlider = NewSlider(__instance, content, "custombg.hue", value =>
        {
            BgConfig.Instance.gradientHue = value * 360f;
            UpdateSliderLabels();
        });
        __instance.CreateVerticalSpacer(25f, content);
        brightnessSlider = NewSlider(__instance, content, "custombg.brightness", value =>
        {
            BgConfig.Instance.gradientBrightness = 0.5f + value;
            UpdateSliderLabels();
        });

        __instance.CreateVerticalSpacer(20f, content);
        fitList = NewList(__instance, content, "custombg.fit", BackgroundManager.FitModes.Select(mode => $"custombg.fit.{mode}").ToArray(), id => BgConfig.Instance.backgroundFit = BackgroundManager.FitModes[id]);
        __instance.CreateVerticalSpacer(10f, content);
        landscapeList = NewList(__instance, content, "custombg.landscape", ShowHideKeys, id => BgConfig.Instance.showLandscape = id == 0);
        __instance.CreateVerticalSpacer(10f, content);
        starsList = NewList(__instance, content, "custombg.stars", ShowHideKeys, id => BgConfig.Instance.showStars = id == 0);

        __instance.CreateVerticalSpacer(20f, content);
        NewButton(__instance, content, "custombg.reload", () =>
        {
            BackgroundManager.ReloadImages();
            BackgroundManager.ApplyToCurrentScene();
        });
        NewButton(__instance, content, "custombg.openfolder", () =>
        {
            try
            {
                Application.OpenURL(new Uri(Path.GetFullPath(BgConfig.BG_FOLDER)).AbsoluteUri);
            }
            catch (Exception e)
            {
                Main.modLogger!.LogError($"Could not open {BgConfig.BG_FOLDER}: {e.Message}");
            }
        });

        Sync();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SettingsScreen), nameof(SettingsScreen.Show))]
    private static void SettingsScreen_Show() => Sync();

    public static void Sync()
    {
        if (presetList == null)
            return;
        syncing = true;
        try
        {
            BgConfig config = BgConfig.Instance;
            int preset = Array.IndexOf(BackgroundManager.Presets, BackgroundManager.CurrentPreset());
            presetList.SelectId(preset, true);
            fitList?.SelectId(Math.Max(0, Array.IndexOf(BackgroundManager.FitModes, config.backgroundFit)), true);
            if (hueSlider != null)
                hueSlider.SliderValue = config.gradientHue / 360f;
            if (brightnessSlider != null)
                brightnessSlider.SliderValue = config.gradientBrightness - 0.5f;
            landscapeList?.SelectId(config.showLandscape ? 0 : 1, true);
            starsList?.SelectId(config.showStars ? 0 : 1, true);
            UpdateSliderLabels();
        }
        catch (Exception e)
        {
            Main.modLogger!.LogWarning($"Background settings UI is gone: {e.Message}");
            presetList = null;
        }
        finally
        {
            syncing = false;
        }
    }

    private static void Changed()
    {
        BgConfig.Save();
        BackgroundManager.ApplyToCurrentScene();
    }

    private static void UpdateSliderLabels()
    {
        if (hueSlider != null)
            hueSlider.Arguments = new[] { Mathf.RoundToInt(BgConfig.Instance.gradientHue).ToString() };
        if (brightnessSlider != null)
            brightnessSlider.Arguments = new[] { Mathf.RoundToInt(BgConfig.Instance.gradientBrightness * 100f).ToString() };
    }

    private static SettingsSliderContainer NewSlider(SettingsScreen screen, Transform parent, string key, Action<float> onChange)
    {
        SettingsSliderContainer slider = UnityEngine.Object.Instantiate(screen.volumeSliderPrefab, parent);
        slider.HeaderKey = key;
        slider.SliderValueChangedCallback = Callback<float>(value =>
        {
            if (syncing)
                return;
            onChange(value);
            Changed();
        });
        return slider;
    }

    private static void NewButton(SettingsScreen screen, Transform parent, string key, Action onClick)
    {
        SettingsButtonContainer container = UnityEngine.Object.Instantiate(screen.singleButtonContainerPrefab, parent);
        container.button.Key = key;
        container.button.OnClickedSignal.Add(DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(onClick)!);
    }

    private static UIHorizontalList NewList(SettingsScreen screen, Transform parent, string key, string[] labelKeys, Action<int> onSelect)
    {
        UIHorizontalList list = UnityEngine.Object.Instantiate(screen.horizontalListPrefab, parent);
        list.UpdateScrollerOnHighlight = true;
        list.HeaderKey = key;
        list.SetData(labelKeys, Enumerable.Range(0, labelKeys.Length).ToArray(), 0, true);
        list.IndexSelectedCallback = Callback<int>(index =>
        {
            if (syncing || index < 0 || index >= labelKeys.Length)
                return;
            onSelect(index);
            Changed();
        });
        return list;
    }

    // ai added this but tis helpful actually so ig
    private static Il2CppSystem.Action<T> Callback<T>(Action<T> action)
        => DelegateSupport.ConvertDelegate<Il2CppSystem.Action<T>>(action)!;
}
