using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace RustServerMetrics.HarmonyPatches;

[HarmonyPatch(typeof(BasePlayer), nameof(BasePlayer.OnDisconnected))]
public class BasePlayer_OnDisconnected_Patch
{
    [HarmonyPostfix]
    public static void Postfix(BasePlayer __instance)
    {
        if (!MetricsLogger.IsReady)
        {
            return;
        }

        MetricsLogger.Instance.OnPlayerDisconnected(__instance);
    }
}
