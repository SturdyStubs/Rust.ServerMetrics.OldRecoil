using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RustServerMetrics.HarmonyPatches.Utility;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

// ReSharper disable InconsistentNaming

namespace RustServerMetrics.HarmonyPatches.Delayed;

[DelayedHarmonyPatch]
[HarmonyPatch]
internal static class ServerMgr_Metrics_Patches
{
    private static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;

    [HarmonyPrepare]
    public static bool Prepare()
    {
        if (!RustServerMetricsLoader.__serverStarted)
        {
            Debug.Log("Note: Cannot patch ServerMgr_Metrics_Patches yet. We will patch it upon server start.");
            return false;
        }

        return true;
    }

    [HarmonyTargetMethods]
    public static IEnumerable<MethodBase> TargetMethods(Harmony harmonyInstance)
    {
        foreach (var method in ExistingMethods())
        {
            yield return method;
        }
    }

    private static IEnumerable<MethodBase> ExistingMethods()
    {
        MethodBase method;

        method = AccessTools.Method(typeof(ServerMgr), "Update");
        if (method != null) yield return method;

        method = AccessTools.Method(typeof(ServerBuildingManager), "Cycle");
        if (method != null) yield return method;

        method = AccessTools.Method(typeof(ServerBuildingManager), "Merge");
        if (method != null) yield return method;

        method = AccessTools.Method(typeof(ServerBuildingManager), "Split");
        if (method != null) yield return method;

        method = AccessTools.Method(typeof(BasePlayer), nameof(BasePlayer.ServerCycle));
        if (method != null) yield return method;

        method = AccessTools.Method(typeof(ConnectionQueue), nameof(ConnectionQueue.Cycle));
        if (method != null) yield return method;

        method = AccessTools.Method(typeof(AIThinkManager), nameof(AIThinkManager.ProcessQueue));
        if (method != null) yield return method;

        method = AccessTools.Method(typeof(IOEntity), nameof(IOEntity.ProcessQueue));
        if (method != null) yield return method;

        var basePetType = AccessTools.TypeByName("BasePet");
        method = basePetType == null ? null : AccessTools.Method(basePetType, "ProcessMovementQueue");
        if (method != null) yield return method;

        method = AccessTools.Method(typeof(BaseMountable), "FixedUpdateCycle");
        if (method != null) yield return method;

        method = AccessTools.Method(typeof(Buoyancy), nameof(Buoyancy.Cycle));
        if (method != null) yield return method;

        method = AccessTools.Method(typeof(BaseEntity), nameof(BaseEntity.Kill));
        if (method != null) yield return method;

        method = AccessTools.Method(typeof(BaseEntity), nameof(BaseEntity.Spawn));
        if (method != null) yield return method;

        method = AccessTools.Method(typeof(Facepunch.Network.Raknet.Server), nameof(Facepunch.Network.Raknet.Server.Cycle));
        if (method != null) yield return method;
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> originalInstructions, MethodBase methodBase, ILGenerator ilGenerator)
    {
        var ret = originalInstructions.ToList();
        var local = ilGenerator.DeclareLocal(typeof(long));

        ret.InsertRange(0, [
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Stopwatch), nameof(Stopwatch.GetTimestamp))),
            new CodeInstruction(OpCodes.Stloc, local)
        ]);

        return Helpers.Postfix(ret,
                               CustomPostfix,
                               new CodeInstruction(OpCodes.Ldstr, $"{methodBase.DeclaringType?.Name}.{methodBase.Name}"),
                               new CodeInstruction(OpCodes.Ldloc, local));
    }

    private static void CustomPostfix(string methodName, long __state)
    {
        if (!MetricsLogger.IsReady)
        {
            return;
        }

        var ms = (Stopwatch.GetTimestamp() - __state) * TicksToMs;
        MetricsLogger.Instance.ServerUpdate.LogTime(methodName, ms);
    }
}
