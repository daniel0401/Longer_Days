using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using TMPro;
using CSync.Lib;
using CSync.Extensions;

namespace Longer_Days
{
    public class LongerDaysConfig : SyncedConfig2<LongerDaysConfig>
    {
        private const string DefaultTimeSpeedLabel = "0.5 (Half speed)";
        private const string DefaultClockFormatLabel = "24 Hour";

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

            ConfigManager.Register(this);
        }
    }

    [BepInPlugin("ElecTRiCbOi59.LongerDays", "Longer Days", "1.2.0")]
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

        private void Awake()
        {
            log = Logger;
            Config = new LongerDaysConfig(base.Config);

            harmony = new Harmony("ElecTRiCbOi59.LongerDays");
            harmony.PatchAll();

            log.LogInfo("Longer Days loaded (Sigurd-CSync).");
            log.LogInfo("Synced host TimeSpeed = " + Config.TimeSpeed.Value);
            log.LogInfo("Local ClockFormat = " + Config.ClockFormat.Value);
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
    }

    [HarmonyPatch(typeof(TimeOfDay), "Update")]
    internal class TimePatch
    {
        [HarmonyPostfix]
        private static void UpdatePostfix(TimeOfDay __instance)
        {
            __instance.globalTimeSpeedMultiplier = LongerDaysPlugin.GetTimeSpeed();
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
        }
    }
}