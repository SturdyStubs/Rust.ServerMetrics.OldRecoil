using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

// ReSharper disable InconsistentNaming

namespace RustServerMetrics.HarmonyPatches;

[HarmonyPatch(typeof(BasePlayer), nameof(BasePlayer.PerformanceReport))]
public class BasePlayer_PerformanceReport_Patch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> originalInstructions, ILGenerator iLGenerator)
    {
        var skipMetricsLabel = iLGenerator.DefineLabel();
        var retList = new List<CodeInstruction>(originalInstructions);

        var bitMethodInfo = typeof(BaseEntity.RPCMessage).GetField(nameof(BaseEntity.RPCMessage.read)).FieldType.GetMethod("Bit", Type.EmptyTypes);
        var insertionIndex = retList.FindIndex(x => x.opcode == OpCodes.Callvirt && Equals(x.operand, bitMethodInfo));
        if (insertionIndex < 0) throw new Exception("Failed to find the insertion index for BasePlayer_PerformanceReport_Patch");
        insertionIndex = retList.FindIndex(insertionIndex, x => x.opcode == OpCodes.Stloc_S || x.opcode == OpCodes.Stloc || x.opcode == OpCodes.Stloc_0 || x.opcode == OpCodes.Stloc_1 || x.opcode == OpCodes.Stloc_2 || x.opcode == OpCodes.Stloc_3);
        if (insertionIndex < 0) throw new Exception("Failed to find the store index for BasePlayer_PerformanceReport_Patch");
        insertionIndex++;

        var fieldInfo = typeof(SingletonComponent<MetricsLogger>)
            .GetField(nameof(SingletonComponent<MetricsLogger>.Instance), BindingFlags.Static | BindingFlags.Public);

        var methodInfo = typeof(MetricsLogger)
            .GetMethod(nameof(MetricsLogger.OnClientPerformanceReport), BindingFlags.Instance | BindingFlags.NonPublic);

        var labels = retList[insertionIndex].labels;
        retList[insertionIndex].labels = [];
        retList[insertionIndex].labels.Add(skipMetricsLabel);

        retList.InsertRange(insertionIndex, [
            new CodeInstruction(OpCodes.Ldsfld, fieldInfo)
            {
                labels = labels,
            },
            new CodeInstruction(OpCodes.Brfalse_S, skipMetricsLabel),
            new CodeInstruction(OpCodes.Ldsfld, fieldInfo),
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldloc_0),
            new CodeInstruction(OpCodes.Ldloc_1),
            new CodeInstruction(OpCodes.Call, methodInfo),
        ]);

        return retList;
    }
}
