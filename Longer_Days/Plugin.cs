using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;

namespace Longer_Days
{
    [BepInPlugin("ElecTRiCbOi59.LongerDays", "Longer Days", "1.0.0")]
    public class LongerDaysPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource log;
        internal static ConfigEntry<string> TimeSpeed;
        private Harmony harmony;

        private const string DefaultTimeSpeedLabel = "0.5 (Half speed)";

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

            // If an older config value exists and is invalid, force it back to the safe default.
            if (!TimeSpeedMap.ContainsKey(TimeSpeed.Value))
            {
                log.LogWarning("Invalid TimeSpeed config value found: " + TimeSpeed.Value + ". Resetting to " + DefaultTimeSpeedLabel);
                TimeSpeed.Value = DefaultTimeSpeedLabel;
            }

            log.LogInfo("Longer Days loaded. TimeSpeed = " + TimeSpeed.Value + " -> " + GetSafeTimeSpeed());

            harmony = new Harmony("ElecTRiCbOi59.LongerDays");
            harmony.PatchAll();
        }

        internal static float GetSafeTimeSpeed()
        {
            float speed;
            if (TimeSpeedMap.TryGetValue(TimeSpeed.Value, out speed))
            {
                return speed;
            }

            log.LogWarning("Unknown TimeSpeed value: " + TimeSpeed.Value + ". Falling back to " + DefaultTimeSpeedLabel);
            return 0.5f;
        }
    }

    [HarmonyPatch(typeof(TimeOfDay), "Start")]
    internal class TimeOfDayStartPatch
    {
        [HarmonyPostfix]
        private static void StartPostfix(TimeOfDay __instance)
        {
            float speed = LongerDaysPlugin.GetSafeTimeSpeed();
            LongerDaysPlugin.log.LogInfo("Applying TimeSpeed = " + speed);
            __instance.globalTimeSpeedMultiplier = speed;
        }
    }

    [HarmonyPatch(typeof(TimeOfDay), "Update")]
    internal class TimeOfDayUpdatePatch
    {
        [HarmonyPostfix]
        private static void UpdatePostfix(TimeOfDay __instance)
        {
            __instance.globalTimeSpeedMultiplier = LongerDaysPlugin.GetSafeTimeSpeed();
        }
    }
}