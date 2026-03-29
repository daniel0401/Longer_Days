using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using CSync.Lib;
using CSync.Extensions;

namespace Longer_Days
{
    public class LongerDaysConfig : SyncedConfig2<LongerDaysConfig>
    {
        private const string DefaultTimeSpeedLabel = "0.5 (Half speed)";
        private const string DefaultClockFormatLabel = "24 Hour";
        private const bool DefaultShowClockIndoors = false;
        private const bool DefaultCompactClock = false;
        private const bool DefaultRaiseClock = false;

        private static readonly string[] TimeSpeedOptions =
        {
            "0.1 (Very slow)",
            "0.2",
            "0.3",
            "0.4",
            "0.5 (Half speed)",
            "0.6",
            "0.7",
            "0.8",
            "0.9",
            "1.0 (Normal)"
        };

        private static readonly string[] ClockFormatOptions =
        {
            "12 Hour",
            "24 Hour"
        };

        [SyncedEntryField]
        public SyncedEntry<string> TimeSpeed;

        public ConfigEntry<string> ClockFormat { get; private set; }
        public ConfigEntry<bool> ShowClockIndoors { get; private set; }
        public ConfigEntry<bool> CompactClock { get; private set; }
        public ConfigEntry<bool> RaiseClock { get; private set; }

        public LongerDaysConfig(ConfigFile cfg) : base("ElecTRiCbOi59.LongerDays")
        {
            TimeSpeed = cfg.BindSyncedEntry(
                new ConfigDefinition("Game Settings", "TimeSpeed"),
                DefaultTimeSpeedLabel,
                new ConfigDescription(
                    "Controls how fast the in-game day passes.\n\n" +
                    "HOST ONLY: This value is controlled by the host and synced to all players.",
                    new AcceptableValueList<string>(TimeSpeedOptions)
                )
            );

            ClockFormat = cfg.Bind(
                new ConfigDefinition("Display Settings", "ClockFormat"),
                DefaultClockFormatLabel,
                new ConfigDescription(
                    "Choose how the in-game clock is displayed.\n\n" +
                    "LOCAL: This setting only affects your own display.",
                    new AcceptableValueList<string>(ClockFormatOptions)
                )
            );

            ShowClockIndoors = cfg.Bind(
                new ConfigDefinition("Display Settings", "ShowClockIndoors"),
                DefaultShowClockIndoors,
                new ConfigDescription(
                    "Show the clock inside the ship and buildings.\n\n" +
                    "LOCAL: This setting only affects your own display.\n" +
                    "Disabled = default game behaviour."
                )
            );

            CompactClock = cfg.Bind(
                new ConfigDefinition("Display Settings", "CompactClock"),
                DefaultCompactClock,
                new ConfigDescription(
                    "Makes the clock more compact by reducing its height, shrinking the icon, and preventing wrapping.\n\n" +
                    "LOCAL: This setting only affects your own display."
                )
            );

            RaiseClock = cfg.Bind(
                new ConfigDefinition("Display Settings", "RaiseClock"),
                DefaultRaiseClock,
                new ConfigDescription(
                    "Raises the clock higher on the screen while keeping a small margin from the top.\n\n" +
                    "LOCAL: This setting only affects your own display."
                )
            );

            ConfigManager.Register(this);
        }
    }

    [BepInPlugin("ElecTRiCbOi59.LongerDays", "Longer Days", "1.4.0")]
    [BepInDependency("com.sigurd.csync", "5.0.1")]
    public class LongerDaysPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource log;
        internal static new LongerDaysConfig Config;

        private Harmony harmony;

        private static readonly Dictionary<string, float> TimeSpeedMap = new Dictionary<string, float>
        {
            { "0.1 (Very slow)", 0.1f },
            { "0.2", 0.2f },
            { "0.3", 0.3f },
            { "0.4", 0.4f },
            { "0.5 (Half speed)", 0.5f },
            { "0.6", 0.6f },
            { "0.7", 0.7f },
            { "0.8", 0.8f },
            { "0.9", 0.9f },
            { "1.0 (Normal)", 1.0f }
        };

        private static bool hasStoredClockLayoutDefaults;
        private static Transform cachedClockParent;
        private static RectTransform cachedClockParentRect;
        private static RectTransform cachedClockIconRect;
        private static TMP_Text cachedClockText;

        private static Vector3 defaultClockParentLocalPosition;
        private static Vector2 defaultClockParentSizeDelta;
        private static Vector3 defaultClockTextLocalPosition;
        private static Vector2 defaultClockIconSizeDelta;
        private static Vector3 defaultClockIconLocalPosition;
        private static bool defaultClockWordWrapping;
        private static TextAlignmentOptions defaultClockAlignment;

        private void Awake()
        {
            log = Logger;
            Config = new LongerDaysConfig(base.Config);

            harmony = new Harmony("ElecTRiCbOi59.LongerDays");
            harmony.PatchAll();

            log.LogInfo("Longer Days loaded (Sigurd-CSync).");
            log.LogInfo("Synced host TimeSpeed = " + Config.TimeSpeed.Value);
            log.LogInfo("Local ClockFormat = " + Config.ClockFormat.Value);
            log.LogInfo("Local ShowClockIndoors = " + Config.ShowClockIndoors.Value);
            log.LogInfo("Local CompactClock = " + Config.CompactClock.Value);
            log.LogInfo("Local RaiseClock = " + Config.RaiseClock.Value);
        }

        internal static float GetTimeSpeed()
        {
            float speed;
            if (TimeSpeedMap.TryGetValue(Config.TimeSpeed.Value, out speed))
            {
                return speed;
            }

            log.LogWarning("Invalid TimeSpeed value detected. Falling back to default.");
            return 0.5f;
        }

        internal static bool Use12HourClock()
        {
            return Config.ClockFormat.Value == "12 Hour";
        }

        internal static bool ShouldShowClockIndoors()
        {
            return Config.ShowClockIndoors.Value;
        }

        internal static bool UseCompactClock()
        {
            return Config.CompactClock.Value;
        }

        internal static bool UseRaiseClock()
        {
            return Config.RaiseClock.Value;
        }

        internal static string FormatTime(int hour, int minute)
        {
            if (Use12HourClock())
            {
                string suffix = hour >= 12 ? "PM" : "AM";
                int displayHour = hour % 12;

                if (displayHour == 0)
                {
                    displayHour = 12;
                }

                return displayHour.ToString("00") + ":" + minute.ToString("00") + " " + suffix;
            }

            return hour.ToString("00") + ":" + minute.ToString("00");
        }

        internal static void ApplyClockLayout()
        {
            if (!TryCacheClockReferences())
            {
                return;
            }

            if (!hasStoredClockLayoutDefaults)
            {
                defaultClockParentLocalPosition = cachedClockParent.localPosition;
                defaultClockParentSizeDelta = cachedClockParentRect.sizeDelta;
                defaultClockTextLocalPosition = cachedClockText.transform.localPosition;
                defaultClockIconSizeDelta = cachedClockIconRect.sizeDelta;
                defaultClockIconLocalPosition = cachedClockIconRect.transform.localPosition;
                defaultClockWordWrapping = cachedClockText.enableWordWrapping;
                defaultClockAlignment = cachedClockText.alignment;
                hasStoredClockLayoutDefaults = true;
            }

            cachedClockParent.localPosition = defaultClockParentLocalPosition;
            cachedClockParentRect.sizeDelta = defaultClockParentSizeDelta;
            cachedClockText.transform.localPosition = defaultClockTextLocalPosition;
            cachedClockIconRect.sizeDelta = defaultClockIconSizeDelta;
            cachedClockIconRect.transform.localPosition = defaultClockIconLocalPosition;
            cachedClockText.enableWordWrapping = defaultClockWordWrapping;
            cachedClockText.alignment = defaultClockAlignment;

            if (UseRaiseClock())
            {
                cachedClockParent.localPosition += new Vector3(0f, 28f, 0f);
            }

            if (UseCompactClock())
            {
                cachedClockParentRect.sizeDelta = new Vector2(cachedClockParentRect.sizeDelta.x, 50f);
                cachedClockText.enableWordWrapping = false;
                cachedClockText.alignment = TextAlignmentOptions.Center;
                cachedClockIconRect.sizeDelta = defaultClockIconSizeDelta * 0.42f;

                if (Use12HourClock())
                {
                    cachedClockText.transform.localPosition += new Vector3(-6, -1f, 0f);
                    cachedClockIconRect.transform.localPosition += new Vector3(-28f, -1f, 0f);
                }
                else
                {
                    cachedClockIconRect.transform.localPosition += new Vector3(-12f, -1f, 0f);
                }
            }
        }

        private static bool TryCacheClockReferences()
        {
            if (HUDManager.Instance == null || HUDManager.Instance.clockNumber == null || HUDManager.Instance.clockIcon == null)
            {
                cachedClockParent = null;
                cachedClockParentRect = null;
                cachedClockIconRect = null;
                cachedClockText = null;
                hasStoredClockLayoutDefaults = false;
                return false;
            }

            if (cachedClockParent == null)
            {
                cachedClockText = (TMP_Text)HUDManager.Instance.clockNumber;
                cachedClockParent = cachedClockText.transform.parent;
                cachedClockParentRect = cachedClockParent.GetComponent<RectTransform>();
                cachedClockIconRect = HUDManager.Instance.clockIcon.GetComponent<RectTransform>();

                if (cachedClockParentRect == null || cachedClockIconRect == null)
                {
                    return false;
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(TimeOfDay), "Update")]
    internal class TimePatch
    {
        private static TimeOfDay cachedTimeOfDay;
        private static float vanillaTimeSpeed = 1.4f;

        [HarmonyPostfix]
        private static void UpdatePostfix(TimeOfDay __instance)
        {
            if (cachedTimeOfDay != __instance)
            {
                cachedTimeOfDay = __instance;
                vanillaTimeSpeed = __instance.globalTimeSpeedMultiplier;
            }

            float selectedScale = LongerDaysPlugin.GetTimeSpeed();
            __instance.globalTimeSpeedMultiplier = vanillaTimeSpeed * selectedScale;
        }
    }

    [HarmonyPatch(typeof(HUDManager), "Awake")]
    internal class ClockLayoutPatch
    {
        [HarmonyPostfix]
        private static void AwakePostfix()
        {
            LongerDaysPlugin.ApplyClockLayout();
        }
    }

    [HarmonyPatch(typeof(HUDManager), "SetClock")]
    internal class ClockPatch
    {
        [HarmonyPostfix]
        private static void SetClockPatch(float timeNormalized, float numberOfHours)
        {
            if (HUDManager.Instance == null || HUDManager.Instance.clockNumber == null)
            {
                return;
            }

            int totalMinutes = (int)(timeNormalized * (60f * numberOfHours)) + 360;
            int hour = totalMinutes / 60;
            int minute = totalMinutes % 60;

            string formatted = LongerDaysPlugin.FormatTime(hour, minute);
            ((TMP_Text)HUDManager.Instance.clockNumber).text = formatted;

            LongerDaysPlugin.ApplyClockLayout();
        }
    }

    [HarmonyPatch(typeof(HUDManager), "Update")]
    internal class IndoorClockPatch
    {
        [HarmonyPrefix]
        private static void UpdatePrefix(ref HUDElement ___Clock)
        {
            if (!LongerDaysPlugin.ShouldShowClockIndoors())
            {
                return;
            }

            ___Clock.targetAlpha = 1f;
        }
    }
}