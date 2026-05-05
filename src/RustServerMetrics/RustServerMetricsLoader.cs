using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RustServerMetrics;

public class RustServerMetricsLoader
{
    public static bool __serverStarted = false;
    
    public static Harmony __harmonyInstance;
    
    public static List<Harmony> __modTimeWarningsHarmonyInstances = [];
    public void AddModTimeWarnings(List<MethodInfo> methods)
    { 
        var instance = new Harmony($"RustServerMetrics.ModTimeWarnings.{__modTimeWarningsHarmonyInstances.Count}");
        __modTimeWarningsHarmonyInstances.Add(instance);
         
        ModTimeWarnings.Methods.Clear();
        ModTimeWarnings.Methods.AddRange(methods);
        
        var patchProcessor = new PatchClassProcessor(instance, typeof(ModTimeWarnings));
        patchProcessor.Patch();
        
        foreach (var method in methods)
        {
            Debug.Log($"{method.DeclaringType?.Name}.{method.Name}");
        }

        Debug.Log($"[ServerMetrics]: Added {methods.Count} ModTimeWarnings");
    }
}
