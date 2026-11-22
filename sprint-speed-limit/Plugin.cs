using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace SprintSpeedLimit
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "wuguishifu.SprintSpeedLimit";
        public const string PluginName = "SprintSpeedLimit";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<int> MaxSprintUpgrades;

        private void Awake()
        {
            Log = Logger;
            MaxSprintUpgrades = Config.Bind(
                "General",
                "MaxSprintSpeedUpgrades",
                3,
                new ConfigDescription(
                    "Maximum number of sprint speed upgrades a single player can have. " +
                    "Upgrades used beyond this limit are consumed and do nothing.",
                    new AcceptableValueRange<int>(0, 100)));

            new Harmony(PluginGuid).PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo($"{PluginName} {PluginVersion} loaded (max sprint upgrades: {MaxSprintUpgrades.Value})");
        }
    }

    // Vanilla: PunManager.UpgradePlayerSprintSpeed(steamID, value) adds `value` to
    // StatsManager.playerUpgradeSpeed[steamID] and applies the same delta to the local
    // PlayerController. Every client runs this when an upgrade item is used, so clamping
    // `value` here keeps the stat and the live speed in sync for everyone with the mod.
    [HarmonyPatch(typeof(PunManager), nameof(PunManager.UpgradePlayerSprintSpeed))]
    internal static class UpgradePlayerSprintSpeedPatch
    {
        private static void Prefix(string _steamID, ref int value)
        {
            if (value <= 0 || StatsManager.instance == null)
                return;

            int max = Plugin.MaxSprintUpgrades.Value;
            StatsManager.instance.playerUpgradeSpeed.TryGetValue(_steamID, out int current);

            int allowed = Math.Max(0, max - current);
            if (value > allowed)
            {
                Plugin.Log.LogInfo(
                    $"Sprint upgrade for {_steamID} clamped: has {current}, requested +{value}, cap {max} -> +{allowed}");
                value = allowed;
            }
        }
    }
}
