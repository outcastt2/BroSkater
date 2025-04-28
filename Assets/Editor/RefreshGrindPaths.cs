using UnityEngine;
using UnityEditor;
using BroSkater.Rails;
using System.Collections.Generic;

public class RefreshGrindPaths : EditorWindow
{
    [MenuItem("BroSkater/Refresh All Grind Paths")]
    static void RefreshAllPaths()
    {
        GrindPath[] paths = Object.FindObjectsOfType<GrindPath>();
        Debug.Log($"Found {paths.Length} GrindPath objects in the scene");
        
        foreach (GrindPath path in paths)
        {
            Debug.Log($"Refreshing GrindPath on {path.gameObject.name}");
            
            // Disable and re-enable to trigger path regeneration
            path.enabled = false;
            path.enabled = true;
            
            EditorUtility.SetDirty(path.gameObject);
        }
        
        Debug.Log("Finished refreshing all grind paths");
    }
} 