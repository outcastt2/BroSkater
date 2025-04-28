using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using BroSkater.Rails;
using System.Collections.Generic;

[InitializeOnLoad]
public class RegenerateGrindPaths
{
    static RegenerateGrindPaths()
    {
        // Subscribe to play mode state change events
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }
    
    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // When entering play mode, regenerate all grind paths
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Debug.Log("Play mode entered - regenerating all grind paths");
            RegenerateAllPaths();
        }
    }
    
    // This will be called automatically when entering play mode
    static void RegenerateAllPaths()
    {
        GrindPath[] paths = Object.FindObjectsOfType<GrindPath>();
        Debug.Log($"Found {paths.Length} GrindPath objects in the scene");
        
        foreach (GrindPath path in paths)
        {
            Debug.Log($"Regenerating GrindPath on {path.gameObject.name}");
            
            // Force regeneration by calling public method
            path.ForceRegenerate();
        }
        
        Debug.Log("Finished regenerating all grind paths");
    }
    
    [MenuItem("BroSkater/Regenerate All Grind Paths")]
    static void MenuRegenerateAllPaths()
    {
        RegenerateAllPaths();
    }
} 