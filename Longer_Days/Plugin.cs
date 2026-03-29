using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
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
                    "Makes the clock more compact by shrinking the icon and tightening the layout.\n\n" +
                    "Useful if the default clock feels a bit bulky.\n\n" +
                    "LOCAL: This setting only affects your own display."
                )
            );

            RaiseClock = cfg.Bind(
                new ConfigDefinition("Display Settings", "RaiseClock"),
                DefaultRaiseClock,
                new ConfigDescription(
                    "Raises the clock a little higher on the screen while keeping some margin from the top.\n\n" +
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
        private const float RaisedClockYOffset = 28f;
        private const float CompactClockHeight = 50f;
        private const float CompactIconScale = 0.42f;

        private static readonly Vector3 Compact12HourTextOffset = new Vector3(-6f, -1f, 0f);
        private static readonly Vector3 Compact12HourIconOffset = new Vector3(-28f, -1f, 0f);
        private static readonly Vector3 Compact24HourIconOffset = new Vector3(-12f, -1f, 0f);

        internal static ManualLogSource log;
        internal static new LongerDaysConfig Config;

        private Harmony harmony;

        private sealed class ClockUiState
        {
            internal Transform Parent;
            internal RectTransform ParentRect;
            internal RectTransform IconRect;
            internal TMP_Text Text;

            internal Vector3 DefaultParentLocalPosition;
            internal Vector2 DefaultParentSizeDelta;
            internal Vector3 DefaultTextLocalPosition;
            internal Vector2 DefaultIconSizeDelta;
            internal Vector3 DefaultIconLocalPosition;
            internal bool DefaultWordWrapping;
            internal TextAlignmentOptions DefaultAlignment;

            internal bool DefaultsCaptured;
        }

        private static ClockUiState clockUi;
        private static TimeOfDay currentTimeOfDay;
        private static float vanillaTimeSpeed = 1.4f;

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

        internal static float GetConfiguredTimeScale()
        {
            switch (Config.TimeSpeed.Value)
            {
                case "0.1 (Very slow)":
                    return 0.1f;
                case "0.2":
                    return 0.2f;
                case "0.3":
                    return 0.3f;
                case "0.4":
                    return 0.4f;
                case "0.5 (Half speed)":
                    return 0.5f;
                case "0.6":
                    return 0.6f;
                case "0.7":
                    return 0.7f;
                case "0.8":
                    return 0.8f;
                case "0.9":
                    return 0.9f;
                case "1.0 (Normal)":
                    return 1.0f;
                default:
                    log.LogWarning("Unknown TimeSpeed value '" + Config.TimeSpeed.Value + "'. Falling back to 0.5.");
                    return 0.5f;
            }
        }

        internal static string FormatTime(int hour, int minute)
        {
            if (Config.ClockFormat.Value == "12 Hour")
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

        internal static void RefreshClockLayout()
        {
            if (!BindClockUi())
            {
                return;
            }

            CaptureClockDefaultsIfNeeded();
            RestoreClockDefaults();

            if (Config.RaiseClock.Value)
            {
                clockUi.Parent.localPosition += new Vector3(0f, RaisedClockYOffset, 0f);
            }

            if (!Config.CompactClock.Value)
            {
                return;
            }

            clockUi.ParentRect.sizeDelta = new Vector2(clockUi.ParentRect.sizeDelta.x, CompactClockHeight);
            clockUi.Text.enableWordWrapping = false;
            clockUi.Text.alignment = TextAlignmentOptions.Center;
            clockUi.IconRect.sizeDelta = clockUi.DefaultIconSizeDelta * CompactIconScale;

            if (Config.ClockFormat.Value == "12 Hour")
            {
                clockUi.Text.transform.localPosition += Compact12HourTextOffset;
                clockUi.IconRect.transform.localPosition += Compact12HourIconOffset;
            }
            else
            {
                clockUi.IconRect.transform.localPosition += Compact24HourIconOffset;
            }
        }

        private static void CaptureClockDefaultsIfNeeded()
        {
            if (clockUi.DefaultsCaptured)
            {
                return;
            }

            // Cache the original HUD layout once so config changes can be applied
            // and reverted cleanly without stacking offsets every time SetClock runs.
            clockUi.DefaultParentLocalPosition = clockUi.Parent.localPosition;
            clockUi.DefaultParentSizeDelta = clockUi.ParentRect.sizeDelta;
            clockUi.DefaultTextLocalPosition = clockUi.Text.transform.localPosition;
            clockUi.DefaultIconSizeDelta = clockUi.IconRect.sizeDelta;
            clockUi.DefaultIconLocalPosition = clockUi.IconRect.transform.localPosition;
            clockUi.DefaultWordWrapping = clockUi.Text.enableWordWrapping;
            clockUi.DefaultAlignment = clockUi.Text.alignment;
            clockUi.DefaultsCaptured = true;
        }

        private static void RestoreClockDefaults()
        {
            clockUi.Parent.localPosition = clockUi.DefaultParentLocalPosition;
            clockUi.ParentRect.sizeDelta = clockUi.DefaultParentSizeDelta;
            clockUi.Text.transform.localPosition = clockUi.DefaultTextLocalPosition;
            clockUi.IconRect.sizeDelta = clockUi.DefaultIconSizeDelta;
            clockUi.IconRect.transform.localPosition = clockUi.DefaultIconLocalPosition;
            clockUi.Text.enableWordWrapping = clockUi.DefaultWordWrapping;
            clockUi.Text.alignment = clockUi.DefaultAlignment;
        }

        private static bool BindClockUi()
        {
            if (HUDManager.Instance == null || HUDManager.Instance.clockNumber == null || HUDManager.Instance.clockIcon == null)
            {
                clockUi = null;
                return false;
            }

            if (clockUi != null && clockUi.Parent != null && clockUi.Text != null && clockUi.IconRect != null)
            {
                return true;
            }

            TMP_Text clockText = (TMP_Text)HUDManager.Instance.clockNumber;
            Transform clockParent = clockText.transform.parent;
            RectTransform clockParentRect = clockParent != null ? clockParent.GetComponent<RectTransform>() : null;
            RectTransform clockIconRect = HUDManager.Instance.clockIcon.GetComponent<RectTransform>();

            if (clockParent == null || clockParentRect == null || clockIconRect == null)
            {
                return false;
            }

            clockUi = new ClockUiState
            {
                Parent = clockParent,
                ParentRect = clockParentRect,
                IconRect = clockIconRect,
                Text = clockText
            };

            return true;
        }

        internal static void ApplyScaledTimeSpeed(TimeOfDay timeOfDay)
        {
            if (currentTimeOfDay != timeOfDay)
            {
                // A new TimeOfDay instance can appear on round or scene changes.
                // Grab the current vanilla multiplier again before applying our scale.
                currentTimeOfDay = timeOfDay;
                vanillaTimeSpeed = timeOfDay.globalTimeSpeedMultiplier;
            }

            timeOfDay.globalTimeSpeedMultiplier = vanillaTimeSpeed * GetConfiguredTimeScale();
        }
    }

    [HarmonyPatch(typeof(TimeOfDay), "Update")]
    internal class TimePatch
    {
        [HarmonyPostfix]
        private static void UpdatePostfix(TimeOfDay __instance)
        {
            LongerDaysPlugin.ApplyScaledTimeSpeed(__instance);
        }
    }

    [HarmonyPatch(typeof(HUDManager), "Awake")]
    internal class ClockLayoutPatch
    {
        [HarmonyPostfix]
        private static void AwakePostfix()
        {
            LongerDaysPlugin.RefreshClockLayout();
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

            ((TMP_Text)HUDManager.Instance.clockNumber).text = LongerDaysPlugin.FormatTime(hour, minute);

            // The game updates the clock text here, so this is a reliable place to
            // reapply our local HUD tweaks without fighting the entire HUD every frame.
            LongerDaysPlugin.RefreshClockLayout();
        }
    }

    [HarmonyPatch(typeof(HUDManager), "Update")]
    internal class IndoorClockPatch
    {
        [HarmonyPrefix]
        private static void UpdatePrefix(ref HUDElement ___Clock)
        {
            if (!LongerDaysPlugin.Config.ShowClockIndoors.Value)
            {
                return;
            }

            ___Clock.targetAlpha = 1f;
        }
    }
}