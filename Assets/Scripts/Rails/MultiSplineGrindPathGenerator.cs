using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace BroSkater.Rails
{
    [RequireComponent(typeof(MeshFilter))]
    public class MultiSplineGrindPathGenerator : MonoBehaviour
    {
        [Header("Source Mesh Settings")]
        [SerializeField] private MeshFilter sourceMeshFilter;
        [Tooltip("Minimum vertex color alpha (on the source mesh) to consider a vertex part of a grind path.")]
        [SerializeField] [Range(0f, 1f)] private float colorWeightThreshold = 0.9f;

        [Header("Generated Path Settings")]
        [Tooltip("The prefab to instantiate for each detected grind path. Must contain a SplineGrindPath component.")]
        [SerializeField] private GameObject grindPathPrefab;
        [Tooltip("Should generated path objects be children of this GameObject?")]
        [SerializeField] private bool parentToThisObject = true;
        [Tooltip("Destroy existing child objects before generating new paths?")]
        [SerializeField] private bool clearExistingChildren = true;

        [Header("Generation Control")]
        [SerializeField] private bool generateOnStart = true;

        private List<GameObject> generatedPaths = new List<GameObject>();

        void Start()
        {
            if (generateOnStart)
            {
                GeneratePaths();
            }
        }

        [ContextMenu("Generate Grind Paths Now")]
        public void GeneratePaths()
        {
            Debug.Log($"[{gameObject.name}] GeneratePaths() called.", this);

            if (sourceMeshFilter == null)
            {
                sourceMeshFilter = GetComponent<MeshFilter>();
                if (sourceMeshFilter == null)
                {
                    Debug.LogError("MultiSplineGrindPathGenerator requires a MeshFilter component on the same GameObject, or one assigned in the inspector.", this);
                    return;
                }
            }

            if (sourceMeshFilter.sharedMesh == null)
            {
                Debug.LogError("The assigned MeshFilter does not have a mesh.", this);
                return;
            }

            if (grindPathPrefab == null)
            {
                Debug.LogError("Grind Path Prefab is not assigned.", this);
                return;
            }

             if (!grindPathPrefab.GetComponentInChildren<SplineGrindPath>())
            {
                 Debug.LogError("The assigned Grind Path Prefab must contain a SplineGrindPath component (or on a child).");
                 return;
            }
            Debug.Log($"[{gameObject.name}] Prefab check passed.", this);

            if (clearExistingChildren)
            {
                ClearGeneratedPaths();
            }

            Debug.Log($"[{gameObject.name}] Starting grind path extraction from mesh '{sourceMeshFilter.sharedMesh.name}'...", this);

            // Extract paths using the static extractor class
            List<List<Vector3>> localPaths = MultiGrindPathExtractor.ExtractPaths(sourceMeshFilter.sharedMesh, colorWeightThreshold, this);

            Debug.Log($"[{gameObject.name}] Found {localPaths.Count} potential grind paths.", this);

            generatedPaths = new List<GameObject>(localPaths.Count);
            int generatedCount = 0;

            for(int i = 0; i < localPaths.Count; i++)
            {
                List<Vector3> localPath = localPaths[i];

                if (localPath == null || localPath.Count < 2)
                {
                    Debug.LogWarning($"[{gameObject.name}] Skipping path {i} as it has less than 2 points.", this);
                    continue;
                }

                // Instantiate the prefab
                 Debug.Log($"[{gameObject.name}] Instantiating prefab '{grindPathPrefab.name}' for path {i}...", this);
                GameObject pathInstance = Instantiate(grindPathPrefab);
                pathInstance.name = $"{grindPathPrefab.name}_Path_{generatedCount}";
                 Debug.Log($"[{gameObject.name}] Instantiated as '{pathInstance.name}'. Instance null? {pathInstance == null}", pathInstance);

                // Parent it if requested
                if (parentToThisObject)
                {
                    pathInstance.transform.SetParent(this.transform, false); // Set parent without changing world position initially
                     // Reset local position/rotation/scale relative to parent AFTER setting parent
                    pathInstance.transform.localPosition = Vector3.zero;
                    pathInstance.transform.localRotation = Quaternion.identity;
                    pathInstance.transform.localScale = Vector3.one;
                }
                else
                {
                    // If not parenting, place it at the world origin (or handle differently if needed)
                     pathInstance.transform.position = Vector3.zero;
                     pathInstance.transform.rotation = Quaternion.identity;
                }


                // Get the SplineGrindPath component
                SplineGrindPath splineGrindPath = pathInstance.GetComponentInChildren<SplineGrindPath>();
                 Debug.Log($"[{gameObject.name}] Found SplineGrindPath component? {splineGrindPath != null}", pathInstance);
                if (splineGrindPath == null)
                {
                    Debug.LogError($"[{gameObject.name}] Instantiated prefab '{pathInstance.name}' is missing the SplineGrindPath component! Skipping path {i}.", pathInstance);
                    Destroy(pathInstance); // Clean up incomplete instance
                    continue;
                }

                // Initialize the SplineGrindPath with the extracted local points
                // We need to add an initialization method to SplineGrindPath
                 Debug.Log($"[{gameObject.name}] Calling InitializeFromPoints on '{pathInstance.name}' for path {i}...", splineGrindPath);
                 bool success = splineGrindPath.InitializeFromPoints(localPath, this.transform);
                 Debug.Log($"[{gameObject.name}] InitializeFromPoints for path {i} returned: {success}", splineGrindPath);

                if (success)
                {
                    generatedPaths.Add(pathInstance);
                    generatedCount++;
                     Debug.Log($"[{gameObject.name}] Successfully generated SplineGrindPath for path {i} ({localPath.Count} points) on '{pathInstance.name}'.", pathInstance);
                }
                else
                {
                     Debug.LogError($"[{gameObject.name}] Failed to initialize SplineGrindPath for path {i} on '{pathInstance.name}'. Destroying instance.", pathInstance);
                     Destroy(pathInstance); // Clean up failed instance
                }
            }

            Debug.Log($"[{gameObject.name}] Finished generation. Created {generatedCount} grind path objects.", this);
        }

        [ContextMenu("Clear Generated Paths")]
        public void ClearGeneratedPaths()
        {
             Debug.Log($"[{gameObject.name}] Clearing {generatedPaths.Count} previously generated path objects.", this);
            // Destroy existing generated objects safely
            // Use a loop because DestroyImmediate is needed in editor, but Destroy is better at runtime
            if (Application.isPlaying)
            {
                foreach (var pathObj in generatedPaths)
                {
                    if(pathObj != null) Destroy(pathObj);
                }
            }
            else
            {
                 // Need to destroy immediate in editor mode if not playing
                 // Iterate backwards to avoid issues with list modification
                 for(int i = generatedPaths.Count - 1; i >= 0; i--)
                 {
                     if(generatedPaths[i] != null) DestroyImmediate(generatedPaths[i]);
                 }
                 // If clearing children was the goal, do a more thorough sweep
                 if (clearExistingChildren)
                 {
                     // Find all children with SplineGrindPath that might not be in our list
                     var existingSplines = GetComponentsInChildren<SplineGrindPath>();
                     foreach(var spline in existingSplines)
                     {
                         if(spline.gameObject != this.gameObject) // Don't destroy self
                         {
                             DestroyImmediate(spline.gameObject);
                         }
                     }
                 }
            }
            generatedPaths.Clear();
        }

         // Optional: Visualize the source mesh vertices being considered
         void OnDrawGizmosSelected()
         {
             if (sourceMeshFilter != null && sourceMeshFilter.sharedMesh != null)
             {
                 Mesh mesh = sourceMeshFilter.sharedMesh;
                 Vector3[] vertices = mesh.vertices;
                 Color[] colors = mesh.colors;

                 if (colors != null && colors.Length == vertices.Length)
                 {
                     Gizmos.matrix = transform.localToWorldMatrix;
                     for (int i = 0; i < vertices.Length; i++)
                     {
                         if (colors[i].a >= colorWeightThreshold)
                         {
                             Gizmos.color = Color.green; // Highlight vertices meeting the threshold
                             Gizmos.DrawSphere(vertices[i], 0.02f);
                         }
                         else if (colors[i].a > 0.1f) // Show vertices with *some* alpha but below threshold faintly
                         {
                             Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f); // Orange, semi-transparent
                             Gizmos.DrawSphere(vertices[i], 0.01f);
                         }
                     }
                     Gizmos.matrix = Matrix4x4.identity;
                 }
             }
         }
    }
} 