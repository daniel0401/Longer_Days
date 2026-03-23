using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using TMPro;

namespace Longer_Days
{
    [BepInPlugin("ElecTRiCbOi59.LongerDays", "Longer Days", "1.1.0")]
    public class LongerDaysPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource log;
        internal static ConfigEntry<string> TimeSpeed;
        internal static ConfigEntry<string> ClockFormat;
        private Harmony harmony;

        private const string DefaultTimeSpeedLabel = "0.5 (Half speed)";
        private const string DefaultClockFormatLabel = "24 Hour";

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

        private void Awake()
        {
            log = Logger;

            TimeSpeed = Config.Bind(
                "General",
                "TimeSpeed",
                DefaultTimeSpeedLabel,
                new ConfigDescription(
                    "Controls how fast the in-game day passes.",
                    new AcceptableValueList<string>(TimeSpeedOptions)
                )
            );

            ClockFormat = Config.Bind(
                "General",
                "ClockFormat",
                DefaultClockFormatLabel,
                new ConfigDescription(
                    "Choose 12-hour or 24-hour clock display.",
                    new AcceptableValueList<string>(ClockFormatOptions)
                )
            );

            harmony = new Harmony("ElecTRiCbOi59.LongerDays");
            harmony.PatchAll();

            log.LogInfo("Longer Days loaded.");
        }

        internal static float GetSafeTimeSpeed()
        {
            float speed;
            if (TimeSpeedMap.TryGetValue(TimeSpeed.Value, out speed))
            {
                return speed;
            }
            return 0.5f;
        }

        internal static bool Use12HourClock()
        {
            return ClockFormat.Value == "12 Hour";
        }

        internal static string FormatTime(int hour, int minute)
        {
            if (Use12HourClock())
            {
                string suffix = hour >= 12 ? "PM" : "AM";
                int h = hour % 12;
                if (h == 0) h = 12;

                return h.ToString("00") + ":" + minute.ToString("00") + " " + suffix;
            }

            return hour.ToString("00") + ":" + minute.ToString("00");
        }
    }

    // TIME SPEED
    [HarmonyPatch(typeof(TimeOfDay), "Update")]
    internal class TimeOfDayUpdatePatch
    {
        [HarmonyPostfix]
        private static void UpdatePostfix(TimeOfDay __instance)
        {
            __instance.globalTimeSpeedMultiplier = LongerDaysPlugin.GetSafeTimeSpeed();
        }
    }

    // CLOCK DISPLAY
    [HarmonyPatch(typeof(HUDManager), "SetClock")]
    internal class HUDManagerPatch
    {
        [HarmonyPostfix]
        private static void SetClockPatch(float timeNormalized, float numberOfHours)
        {
            if (HUDManager.Instance == null)
                return;

            var clock = HUDManager.Instance.clockNumber;

            int totalMinutes = (int)(timeNormalized * (60f * numberOfHours)) + 360;
            int hour = totalMinutes / 60;
            int minute = totalMinutes % 60;

            string formatted = LongerDaysPlugin.FormatTime(hour, minute);

            ((TMP_Text)clock).text = formatted;
        }
    }
}