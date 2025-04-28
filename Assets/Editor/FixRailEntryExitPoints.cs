using UnityEngine;
using UnityEditor;
using BroSkater.Rails;
using System.Collections.Generic;

public class FixRailEntryExitPoints : EditorWindow
{
    [MenuItem("BroSkater/Fix Rail Entry Exit Points")]
    static void FixEntryExitPoints()
    {
        // Find all GrindPath objects in the scene
        var paths = Object.FindObjectsOfType<GrindPath>();
        Debug.Log($"Found {paths.Length} GrindPath objects in the scene");
        
        foreach (var path in paths)
        {
            Debug.Log($"Fixing rail: {path.gameObject.name}");
            
            // Force complete regeneration to fix potential issues
            path.enabled = false;
            path.enabled = true;
            
            // Use the new direct method to fix entry/exit points
            path.ForceRefreshEntryExitPoints();
            
            // Make sure Unity knows this object needs to be saved
            EditorUtility.SetDirty(path.gameObject);
            
            // Force Unity to refresh the scene view immediately to show changes
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
        
        // Save the scene to ensure changes persist
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log("Finished fixing rail entry/exit points - SCENE MARKED AS DIRTY");
    }
} 