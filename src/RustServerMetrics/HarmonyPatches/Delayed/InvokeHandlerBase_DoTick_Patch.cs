using HarmonyLib;
using RustServerMetrics.HarmonyPatches.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

// ReSharper disable once InconsistentNaming

namespace RustServerMetrics.HarmonyPatches.Delayed;

[DelayedHarmonyPatch]
[HarmonyPatch]
internal static class InvokeHandlerBase_DoTick_Patch
{
    #region Members

    private static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;

    private static readonly FieldInfo InvokeActionField =
        AccessTools.Field(typeof(InvokeAction), nameof(InvokeAction.action));

    private static readonly MethodInfo ActionInvokeMethod =
        AccessTools.Method(typeof(Action), nameof(Action.Invoke));

    private static readonly MethodInfo InvokeWrapperMethod =
        AccessTools.Method(typeof(InvokeHandlerBase_DoTick_Patch), nameof(InvokeWrapper));

    #endregion

    #region Patching

    [HarmonyPrepare]
    public static bool Prepare()
    {
        // ReSharper disable once InvertIf
        if (!RustServerMetricsLoader.__serverStarted)
        {
            UnityEngine.Debug.Log("Note: Cannot patch InvokeHandlerBase_DoTick_Patch yet. We will patch it upon server start.");
            return false;
        }

        return true;
    }

    [HarmonyTargetMethods]
    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(InvokeHandlerBase<InvokeHandler>), "DoTick");
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> originalInstructions)
    {
        var instructionsList = originalInstructions.ToList();

        try
        {
            var insertionIndex = FindInvokeActionCall(instructionsList);
            if (insertionIndex < 0)
            {
                throw new InvalidOperationException("Unable to find the expected injection point");
            }

            instructionsList.RemoveRange(insertionIndex, 2);
            instructionsList.Insert(insertionIndex, new CodeInstruction(OpCodes.Call, InvokeWrapperMethod));

            return instructionsList;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[ServerMetrics] {nameof(InvokeHandlerBase_DoTick_Patch)}: " + e.Message);
            return instructionsList;
        }
    }

    #endregion

    private static int FindInvokeActionCall(IReadOnlyList<CodeInstruction> instructions)
    {
        for (var i = 0; i < instructions.Count - 1; i++)
        {
            var current = instructions[i];
            var next = instructions[i + 1];

            if ((current.opcode == OpCodes.Ldfld || current.opcode == OpCodes.Ldflda) &&
                Equals(current.operand, InvokeActionField) &&
                (next.opcode == OpCodes.Call || next.opcode == OpCodes.Callvirt) &&
                Equals(next.operand, ActionInvokeMethod))
            {
                return i;
            }
        }

        return -1;
    }

    #region Handler

    private static void InvokeWrapper(InvokeAction invokeAction)
    {
        if (!MetricsLogger.IsReady)
        {
            invokeAction.action.Invoke();
            return;
        }

        var start = Stopwatch.GetTimestamp();
        try
        {
            invokeAction.action.Invoke();
        }
        finally
        {
            var ms = (Stopwatch.GetTimestamp() - start) * TicksToMs;
            MetricsLogger.Instance.ServerInvokes.LogTime(invokeAction.action.Method, ms);
        }
    }

    #endregion
}
