using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace RustServerMetrics.HarmonyPatches;

[HarmonyPatch(typeof(BasePlayer), nameof(BasePlayer.PerformanceReport))]
public class BasePlayer_PerformanceReport_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(BasePlayer __instance, BaseEntity.RPCMessage msg)
    {
        msg.read.Int32();
        var memorySystem = msg.read.Int32();
        var fps = msg.read.Float();
        msg.read.Int32();
        msg.read.Bit();

        if (MetricsLogger.IsReady)
        {
            MetricsLogger.Instance.OnClientPerformanceReport(__instance, memorySystem, fps);
        }

        return false;
    }
}
