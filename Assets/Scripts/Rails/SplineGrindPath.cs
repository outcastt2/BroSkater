using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace BroSkater.Rails
{
    /// <summary>
    /// Represents a grindable rail path using Unity's Spline system
    /// Can be initialized directly from points or (optionally) from a GrindPath component.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    // [RequireComponent(typeof(GrindPath))] // Removed: No longer strictly required
    public class SplineGrindPath : MonoBehaviour
    {
        [Header("Rail Settings")]
        [SerializeField] private float maxSnapDistance = 1.5f;
        [SerializeField] private float entryAngleThreshold = 60f;
        [SerializeField] private bool canGrindInBothDirections = true;
        
        [Header("Physics")]
        [SerializeField] private float railFriction = 0.1f;
        [SerializeField] private float speedBoost = 1.2f;
        
        [Header("Collider")]
        [SerializeField] private float colliderRadius = 0.25f;
        [SerializeField] private bool generateCollider = true;
        [SerializeField] private LayerMask railLayer; // Consider setting this via code if needed
        
        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private int debugGizmoSegments = 20;
        [SerializeField] private Color railColor = Color.blue;
        [SerializeField] private Color normalColor = Color.green;
        [SerializeField] private Color tangentColor = Color.red;
        [SerializeField] private Color snapZoneColor = new Color(1f, 0.5f, 0f, 0.3f);
        
        // Components
        private SplineContainer splineContainer;
        private Spline spline;
        private CapsuleCollider railCollider; // Reference to the main (now likely unused) collider
        // private GrindPath grindPath; // Removed direct reference
        
        // State
        private float totalLength;
        private List<GameObject> segmentColliders = new List<GameObject>();
        private List<Vector3> _worldPathPoints = new List<Vector3>(); // Store the path points used for generation
        private bool _isInitializedExternally = false; // Flag for new initialization path
        
        // Public property to access the associated GrindPath (optional)
        // public GrindPath AssociatedGrindPath => grindPath;
        
        // Public getters
        public Spline Spline => spline; 
        public bool IsClosed => spline != null && spline.Closed;
        public List<Vector3> WorldPathPoints => _worldPathPoints; // Expose the points used
        
        private void OnValidate()
        {
            if (splineContainer == null)
                splineContainer = GetComponent<SplineContainer>();
                
            if (splineContainer != null && splineContainer.Splines.Count > 0)
                spline = splineContainer.Splines[0];
            else if (splineContainer != null && splineContainer.Splines.Count == 0)
                spline = null; // Reset spline if container is empty
                
            // Only update collider if spline exists (meaning initialization likely happened)
            if(spline != null && spline.Count > 0)
            UpdateCollider();
        }
        
        private void Awake()
        {
            splineContainer = GetComponent<SplineContainer>();
            // grindPath = GetComponent<GrindPath>(); // Removed
            
            if (splineContainer == null)
            {
                Debug.LogError("SplineContainer not found!", this);
                return;
            }
            
            // Ensure Spline exists in container or add one
            if (splineContainer.Spline == null)
            {
                 spline = splineContainer.AddSpline();
                 Debug.Log("Added new Spline to SplineContainer.", this);
            }
            else
            {
                 spline = splineContainer.Spline; // Use the first/main spline
            }
            
            // Set layer - Consider doing this after initialization?
             if (gameObject.layer != LayerMask.NameToLayer("Rail") && railLayer.value != 0) // Use railLayer.value
             {
                 // Find layer index from mask
                 int layerIndex = GetLayerFromMask(railLayer);
                 if (layerIndex != -1)
                 {
                    gameObject.layer = layerIndex;
                 }
                 else
                 {
                    Debug.LogWarning($"Could not find layer specified in railLayer mask on GameObject '{gameObject.name}'.", this);
                 }
            }
            else if (railLayer.value == 0)
            {
                 Debug.LogWarning($"Rail Layer mask not set on '{gameObject.name}'. Collider layer might be incorrect.", this);
            }
        }
        
        private void OnDisable()
        {
             ClearSegmentColliders();
        }
        
        private void Start()
        {
            // If not initialized externally by the generator, potentially try the old method (optional)
            if (!_isInitializedExternally)
            {
                Debug.LogWarning($"SplineGrindPath on '{gameObject.name}' was not initialized externally. Attempting fallback or ensure generator runs first.", this);
                 // --- Fallback Logic (Optional - requires GrindPath component) ---
                /*
                GrindPath fallbackGrindPath = GetComponent<GrindPath>();
                if (fallbackGrindPath != null)
                {
                    Debug.Log($"Found GrindPath component, attempting initialization from it.", this);
                    // Ensure points are ready
                    if (fallbackGrindPath.PathPoints == null || fallbackGrindPath.PathPoints.Count < 2)
            {
                       Debug.LogWarning("Fallback GrindPath has no points yet.", this);
                       // Optionally force regeneration? Be careful with timing.
                       // fallbackGrindPath.ForceRegenerate();
                    }

                    if (fallbackGrindPath.PathPoints != null && fallbackGrindPath.PathPoints.Count >= 2)
                    {
                        // Use the GrindPath points (already in world space)
                        if(GenerateSplineFromWorldPoints(fallbackGrindPath.PathPoints))
                        {
                            CalculateTotalLength(); // Calculate length after spline generation
                        }
                    }
                    else
                    {
                         Debug.LogError("Fallback GrindPath failed to provide points.", this);
                    }
                }
                else
                {
                    Debug.LogError($"SplineGrindPath on '{gameObject.name}' requires initialization via InitializeFromPoints or a GrindPath component for fallback.", this);
                    return; // Cannot proceed without points
                }
                */
                // --- End Fallback Logic ---
                // If no fallback, simply return or log error if points are needed immediately
                if (_worldPathPoints.Count < 2)
                {
                    Debug.LogError($"SplineGrindPath '{gameObject.name}' has no path points. Ensure InitializeFromPoints is called or a fallback method provides points.", this);
                    return;
                }
            }

            // Length should be calculated *after* successful spline generation
            // which happens either in InitializeFromPoints or the fallback Start logic.
             if (spline != null && spline.Count > 0 && totalLength <= 0)
             {
                 CalculateTotalLength();
             }

             Debug.Log($"SplineGrindPath '{gameObject.name}' Start completed. Length: {totalLength:F2}. Initialized Externally: {_isInitializedExternally}", this);
        }
        
        /// <summary>
        /// NEW: Initializes the spline path from a list of points provided in local space relative to a reference transform.
        /// </summary>
        /// <param name="localPoints">Path points in local space of referenceTransform.</param>
        /// <param name="referenceTransform">The transform the localPoints are relative to.</param>
        /// <returns>True if initialization was successful, false otherwise.</returns>
        public bool InitializeFromPoints(List<Vector3> localPoints, Transform referenceTransform)
        {
            if (localPoints == null || localPoints.Count < 2)
            {
                Debug.LogError($"InitializeFromPoints called with insufficient points ({localPoints?.Count ?? 0}).", this);
                return false;
            }
            if (referenceTransform == null)
            {
                 Debug.LogError("InitializeFromPoints called with null referenceTransform.", this);
                 return false;
            }
            if (spline == null)
            {
                // This should ideally be ensured by Awake, but double-check
                splineContainer = GetComponent<SplineContainer>();
                if (splineContainer == null || splineContainer.Spline == null) {
                    Debug.LogError("Spline or SplineContainer missing during InitializeFromPoints!", this);
                    return false;
                }
                spline = splineContainer.Spline;
            }

            Debug.Log($"[{gameObject.name}] Initializing SplineGrindPath from {localPoints.Count} local points (relative to '{referenceTransform.name}').", this);

            // 1. Convert local points to world space
            _worldPathPoints = localPoints.Select(p => referenceTransform.TransformPoint(p)).ToList();

            // 2. Generate the spline using these world points
            bool splineGenerated = GenerateSplineFromWorldPoints(_worldPathPoints);

            if (splineGenerated)
            {
                 // 3. Calculate length
                CalculateTotalLength();

                // 4. Generate collider segments
                UpdateCollider(); // This uses the generated spline

                // 5. Set flag
                _isInitializedExternally = true;
                Debug.Log($"[{gameObject.name}] Successfully initialized. Length: {totalLength:F2}", this);
                return true;
            }
            else
            {
                 Debug.LogError($"[{gameObject.name}] Failed to generate spline from provided points.", this);
                 _worldPathPoints.Clear();
                 ClearSplineKnots(); // Clear any partially created knots
                 ClearSegmentColliders();
                _isInitializedExternally = false;
                return false;
            }
            }
               
        // Helper to get layer index from LayerMask
        private int GetLayerFromMask(LayerMask mask)
        {
            int layerNumber = 0;
            int layer = mask.value;
            while(layer > 1)
            {
                layer = layer >> 1;
                layerNumber++;
            }
             // Check if the found layer number is valid (0 to 31)
             if (layerNumber >= 0 && layerNumber <= 31 && LayerMask.LayerToName(layerNumber) != "")
             { // Also check if the layer name is valid
                 return layerNumber;
             }
             else
             {
                 return -1; // Invalid layer
             }
        }
        
        /// <summary>
        /// Gets the total length of the spline
        /// </summary>
        public float GetTotalLength()
        {
            // Use the cached length if available and valid
            if (totalLength > 0)
                return totalLength;
            // Otherwise, recalculate if needed
            CalculateTotalLength();
            return totalLength;
        }
        
        /// <summary>
        /// Recalculates the total length of the spline.
        /// </summary>
        private void CalculateTotalLength()
        {
             if (spline != null && spline.Count >= 2)
             {
                 // Calculate length based on world space using the object's transform
                 totalLength = SplineUtility.CalculateLength(spline, transform.localToWorldMatrix);
                 // Alternative: Calculate based purely on local knot positions if transform is identity
                 // totalLength = SplineUtility.CalculateLength(spline);
                 Debug.Log($"[{gameObject.name}] Calculated spline length: {totalLength}", this);
             }
             else
             {
                 totalLength = 0f;
                 // Debug.LogWarning($"[{gameObject.name}] Cannot calculate length: spline has < 2 knots.", this);
             }
        }
        
        /// <summary>
        /// Attempts to generate or update the collider for this rail using segments along the spline.
        /// </summary>
        private void UpdateCollider()
        {
            Debug.Log($"[{gameObject.name}] UpdateCollider called. generateCollider = {generateCollider}", this);

            if (!generateCollider)
            {
                Debug.Log($"[{gameObject.name}] generateCollider is false. Clearing segments.", this);
                ClearSegmentColliders();
                 if (railCollider == null) railCollider = GetComponent<CapsuleCollider>();
                 if (railCollider != null) railCollider.enabled = false;
                 return;
            }

            if (spline == null || spline.Count < 2)
            {
                Debug.LogWarning($"[{gameObject.name}] Cannot generate colliders: Spline is null or has < 2 knots ({spline?.Count ?? 0}). Clearing segments.", this);
                ClearSegmentColliders();
                if (railCollider == null) railCollider = GetComponent<CapsuleCollider>();
                if (railCollider != null) railCollider.enabled = false;
                return;
            }

            // Ensure main single capsule collider is disabled
            if (railCollider == null) railCollider = GetComponent<CapsuleCollider>();
            if (railCollider != null) railCollider.enabled = false;

            ClearSegmentColliders(); // Clears the list and destroys existing objects
            int segments = Mathf.Max(1, debugGizmoSegments);
            float step = 1f / segments;
            Debug.Log($"[{gameObject.name}] Attempting to generate {segments} collider segments.", this);

            Vector3 previousWorldPos = Vector3.zero;

            for (int i = 0; i < segments; i++)
            {
                 // --- Add Log inside loop --- 
                // Debug.Log($"[{gameObject.name}] Creating segment {i}");
                 // ---------------------------
                float t1 = i * step;
                float t2 = (i + 1) * step;

                // Evaluate points and tangents in spline's local space
                spline.Evaluate(t1, out float3 knot1_local, out float3 tan1_local, out float3 up1_local);
                spline.Evaluate(t2, out float3 knot2_local, out float3 tan2_local, out float3 up2_local);

                // Convert evaluated local points to world space for distance/direction calculation
                Vector3 pos1_world = transform.TransformPoint(knot1_local);
                Vector3 pos2_world = transform.TransformPoint(knot2_local);

                // Calculate segment properties in world space
                Vector3 segmentCenter_world = (pos1_world + pos2_world) * 0.5f;
                float segmentLength_world = Vector3.Distance(pos1_world, pos2_world);
                Vector3 segmentDirection_world = (pos2_world - pos1_world).normalized;

                // Correct very small segments (can happen at tight spline curves)
                if (segmentLength_world < 0.001f)
                {
                    if (i > 0) // Try using previous segment's direction
                    {
                        segmentDirection_world = (pos1_world - previousWorldPos).normalized;
                    } else // Default if first segment is tiny
                    {
                         segmentDirection_world = transform.forward; // Or some other default
                    }
                    // Ensure length isn't zero for collider height calculation
                    segmentLength_world = 0.01f;
                }

                previousWorldPos = pos1_world; // Store for next iteration's fallback

                // Create child GameObject for the collider segment
                GameObject segmentGO = new GameObject($"ColliderSegment_{i}");
                segmentGO.transform.position = segmentCenter_world; // Set world position directly
                segmentGO.transform.SetParent(transform, true); // Parent, keep world position
                int segmentLayer = GetLayerFromMask(railLayer);
                segmentGO.layer = (segmentLayer != -1) ? segmentLayer : gameObject.layer; // Assign layer

                // Align the segment GameObject's UP (Y) axis with the world direction
                if (segmentDirection_world != Vector3.zero)
                {
                    segmentGO.transform.rotation = Quaternion.LookRotation(segmentDirection_world); // Point Z along direction
                    // If you need Y to be the height axis for CapsuleCollider.direction = 1:
                    segmentGO.transform.rotation = Quaternion.FromToRotation(Vector3.up, segmentDirection_world);
                }
                else
                {
                    segmentGO.transform.rotation = transform.rotation; // Fallback to parent rotation
                }

                // Add and configure the Capsule Collider
                CapsuleCollider segmentCollider = segmentGO.AddComponent<CapsuleCollider>();
                segmentCollider.isTrigger = true;
                segmentCollider.radius = colliderRadius;
                segmentCollider.height = segmentLength_world + colliderRadius * 2f;
                segmentCollider.center = Vector3.zero;
                segmentCollider.direction = 1; // Height along local Y-axis (aligned by FromToRotation)

                segmentColliders.Add(segmentGO);
            }
            // Debug.Log($"[{gameObject.name}] Finished generating {segmentColliders.Count} collider segments.", this);
            Debug.Log($"[{gameObject.name}] Finished UpdateCollider. Generated {segmentColliders.Count} collider segments.", this); // More specific log
        }
        
        // Helper to clean up previously generated colliders
        private void ClearSegmentColliders()
        {
            if (segmentColliders == null) segmentColliders = new List<GameObject>();
            
            for(int i = segmentColliders.Count - 1; i >= 0; i--)
            {
                 GameObject go = segmentColliders[i];
                 if (go != null)
                 {
                    if (Application.isPlaying)
                        Destroy(go);
                    else
                        DestroyImmediate(go);
                 }
            }
            segmentColliders.Clear();
        }
        
        /// <summary>
        /// Checks if the player can grind this rail from the given position with the given velocity
        /// </summary>
        public bool CanGrind(Vector3 playerPosition, Vector3 playerVelocity, 
                            out Vector3 entryPoint, out Vector3 railTangent, out float distance)
        {
            entryPoint = Vector3.zero;
            railTangent = Vector3.forward;
            distance = 0f;
            
            if (spline == null || spline.Count < 2 || playerVelocity.magnitude < 0.1f)
                return false;
                
            // Find closest point on spline (requires world space point)
            float t;
            // Convert player pos to spline container's local space first
            Vector3 localPlayerPos = transform.InverseTransformPoint(playerPosition);
            
            float dist = SplineUtility.GetNearestPoint(spline, (float3)localPlayerPos, out float3 nearestPointLocalF3, out t);
            Vector3 nearestPointLocal = (Vector3)nearestPointLocalF3;

            // Convert nearest point back to world space for distance check and output
            Vector3 nearestPointWorld = transform.TransformPoint(nearestPointLocal);
            distance = Vector3.Distance(playerPosition, nearestPointWorld);

            // 1. Check Distance
            if (distance > maxSnapDistance)
            {
                return false;
            }

            // 2. Check Angle
            spline.Evaluate(t, out _, out float3 tangentLocalF3, out _);
            Vector3 tangentWorld = transform.TransformDirection((Vector3)tangentLocalF3).normalized;
            Vector3 playerVelDir = playerVelocity.normalized;

            float angleForward = Vector3.Angle(playerVelDir, tangentWorld);
            float angleBackward = Vector3.Angle(playerVelDir, -tangentWorld);

            bool angleValid = false;
            Vector3 entryTangent = Vector3.zero;

            if (angleForward <= entryAngleThreshold)
            {
                angleValid = true;
                entryTangent = tangentWorld;
            }
            else if (canGrindInBothDirections && angleBackward <= entryAngleThreshold)
                {
                angleValid = true;
                entryTangent = -tangentWorld; // Use reversed tangent
            }
            
            if (!angleValid)
            {
                return false;
            }

            // If all checks pass
            entryPoint = nearestPointWorld;
            railTangent = entryTangent;
            // distance = dist; // Already calculated world distance
            return true;
        }
        
        public bool GetPointAtDistance(float distance, out Vector3 point, out Vector3 tangent, out bool isEndReached)
        {
            point = Vector3.zero;
            tangent = Vector3.forward;
            isEndReached = false;
            
            if (spline == null || spline.Count < 2 || totalLength <= 0)
                return false;
            
            // Clamp distance to valid range
            float clampedDistance = Mathf.Clamp(distance, 0, totalLength);
            float t = SplineUtility.GetNormalizedInterpolation(spline, clampedDistance, PathIndexUnit.Distance);
            
            // Evaluate at the normalized time t
            spline.Evaluate(t, out float3 localPos, out float3 localTan, out float3 localUp);
            
            // Convert to world space
            point = transform.TransformPoint(localPos);
            tangent = transform.TransformDirection(localTan).normalized;
            
            // Check if near the end (within a small tolerance)
            float endTolerance = 0.01f;
            isEndReached = (distance >= totalLength - endTolerance);
            if (!IsClosed && distance <= endTolerance) // Also check near start if not closed
            {
                 // Potentially differentiate start/end reached if needed
                 isEndReached = true;
            }
            
            return true;
        }
        
        public bool GetNearestPoint(Vector3 worldPosition, out Vector3 nearestPointWorld, out Vector3 tangentWorld, out float distanceAlongSpline)
        {
            nearestPointWorld = Vector3.zero;
            tangentWorld = Vector3.forward;
            distanceAlongSpline = 0f;
            
            if (spline == null || spline.Count < 2)
                return false;
                
            Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
            float t;
            float distToPoint = SplineUtility.GetNearestPoint(spline, (float3)localPosition, out float3 nearestPointLocalF3, out t);
            
            Vector3 nearestPointLocal = (Vector3)nearestPointLocalF3;
            nearestPointWorld = transform.TransformPoint(nearestPointLocal);
            
            spline.Evaluate(t, out _, out float3 tangentLocalF3, out _);
            tangentWorld = transform.TransformDirection((Vector3)tangentLocalF3).normalized;
            
            // Calculate distance along spline
            distanceAlongSpline = SplineUtility.ConvertIndexUnit(spline, t, PathIndexUnit.Normalized, PathIndexUnit.Distance);
            
            return true;
        }
        
        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || spline == null || spline.Count < 2)
                return;
                
            Gizmos.matrix = transform.localToWorldMatrix; // Draw in local space transformed to world
            
            // Draw Spline Path
            Gizmos.color = railColor;
            int segments = debugGizmoSegments * spline.Count; // More segments for longer/complex splines
            Vector3 prevPoint = spline.EvaluatePosition(0);
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 point = spline.EvaluatePosition(t);
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }
            
             // Draw Normals and Tangents
             segments = debugGizmoSegments; // Reset segments for normal/tangent drawing
             for (int i = 0; i <= segments; i++)
            {
                 float t = (float)i / segments;
                 spline.Evaluate(t, out float3 pos, out float3 tan, out float3 up);

                Gizmos.color = tangentColor;
                 Gizmos.DrawRay(pos, math.normalize(tan) * 0.3f);
                
                Gizmos.color = normalColor;
                 Gizmos.DrawRay(pos, math.normalize(up) * 0.3f);
            }
            
             // Draw Snap Zone (Approximate)
            Gizmos.color = snapZoneColor;
            prevPoint = spline.EvaluatePosition(0);
            Vector3 prevUp = spline.EvaluateUpVector(0);
             for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 point = spline.EvaluatePosition(t);
                 Vector3 up = spline.EvaluateUpVector(t);
                 // Draw quad approximating the snap zone tube segment
                 Vector3 offset1 = (Vector3)math.normalize(math.cross(spline.EvaluateTangent(t), up)) * maxSnapDistance;
                 Vector3 offset2 = (Vector3)math.normalize(math.cross(spline.EvaluateTangent((float)(i-1)/segments), prevUp)) * maxSnapDistance;

                 // Simplified line drawing for snap distance
                 Gizmos.DrawLine(point + offset1, point - offset1);
                 // More complex quad drawing omitted for brevity

                prevPoint = point;
                prevUp = up;
            }

            Gizmos.matrix = Matrix4x4.identity;
        }

        /// <summary>
        /// MODIFIED: Generates the spline knots based on a list of world-space points.
        /// </summary>
        private bool GenerateSplineFromWorldPoints(List<Vector3> worldPoints)
        {
            if (splineContainer == null || spline == null)
            {
                Debug.LogError($"[{gameObject.name}] SplineContainer or Spline is null. Cannot generate.", this);
                return false;
            }
            if (worldPoints == null || worldPoints.Count < 2)
            {
                Debug.LogError($"[{gameObject.name}] Not enough world points ({worldPoints?.Count ?? 0}) to generate spline.", this);
                return false;
            }

            // Clear existing knots before adding new ones
            ClearSplineKnots();

            // Convert world points to the SplineContainer's local space
            var knots = new List<BezierKnot>();
            for (int i = 0; i < worldPoints.Count; i++)
            {
                // Transform world point to local space of the spline container
                float3 localPos = transform.InverseTransformPoint(worldPoints[i]);

                // Simple tangent calculation (forward difference, backward for last point)
                float3 tangent = float3.zero;
                if (worldPoints.Count > 1)
                {
                    if (i < worldPoints.Count - 1)
                        tangent = transform.InverseTransformDirection(worldPoints[i+1] - worldPoints[i]);
                    else // Last point
                        tangent = transform.InverseTransformDirection(worldPoints[i] - worldPoints[i-1]);
            }

                 // Normalize and scale tangent slightly (adjust scale as needed)
                tangent = math.normalizesafe(tangent) * 0.5f; // Small tangent magnitude

                // Create knot with position, zeroed in/out tangents initially
                // For smoother curves, tangent calculation might need improvement (e.g., Catmull-Rom style)
                // Let's try auto-smoothing after adding knots
                 knots.Add(new BezierKnot(localPos)); // Auto tangent might be better
                // knots.Add(new BezierKnot(localPos, -tangent, tangent, quaternion.identity)); // Manual tangent
            }

             // Add all knots to the spline at once
             spline.Knots = knots;

             // Determine if the path is a loop
            bool isLoop = false;
            if (worldPoints.Count > 2)
            {
                 float loopThreshold = 0.1f; // Threshold to consider start/end points the same
                 isLoop = Vector3.Distance(worldPoints[0], worldPoints[worldPoints.Count - 1]) < loopThreshold;
                 spline.Closed = isLoop;
                 Debug.Log($"[{gameObject.name}] Setting spline Closed = {isLoop}", this);
            }
            else
            {
                spline.Closed = false; // Can't be a loop with < 3 points
            }

             // Optional: Apply auto-smoothing to tangents for potentially better curves
             // SplineUtility.CalculateTangents(spline); // Removed: Method not available in runtime API, tangents are usually handled automatically
             // Or try AutoSmooth based on selection? Requires UnityEditor namespace
             #if UNITY_EDITOR
             // UnityEditor.Splines.SplineUtility.AutoSmoothEntireSpline(spline);
             #endif

             Debug.Log($"[{gameObject.name}] Generated spline with {spline.Count} knots. Is Closed: {spline.Closed}", this);
             return true;
        }

        // Helper to clear spline knots safely
        private void ClearSplineKnots()
        {
             if(spline != null)
             {
                 // Check if spline has knots before clearing
                 // Using Knots property setter seems safer than Clear()
                 if (spline.Knots.Any())
                 {
                     spline.Knots = System.Array.Empty<BezierKnot>();
                     // spline.Clear(); // Alternative, might be less safe depending on Unity version
                 }
                 spline.Closed = false;
             }
        }

        // --- Original spline generation from GrindPath (kept for reference, commented out) ---
        /*
        public void GenerateSplineFromGrindPath()
        {
            if (splineContainer == null || grindPath == null || grindPath.PathPoints == null || grindPath.PathPoints.Count < 2)
            {
                Debug.LogWarning("Cannot generate spline: Missing components or GrindPath points.", this);
                return;
            }

            // Ensure spline exists
            if (spline == null)
            {
                 spline = splineContainer.Spline ?? splineContainer.AddSpline();
            }

            // Clear existing knots
            ClearSplineKnots();

            var knots = new List<BezierKnot>();
            List<Vector3> worldPoints = grindPath.PathPoints; // GrindPath points are already world space

            for (int i = 0; i < worldPoints.Count; i++)
            {
                float3 localPos = transform.InverseTransformPoint(worldPoints[i]);
                knots.Add(new BezierKnot(localPos));
            }

            spline.Knots = knots;

             bool isLoop = false;
            if (worldPoints.Count > 2)
            {
                 float loopThreshold = 0.1f;
                 isLoop = Vector3.Distance(worldPoints[0], worldPoints[worldPoints.Count - 1]) < loopThreshold;
                 spline.Closed = isLoop;
                }
                else
                {
                  spline.Closed = false;
             }
             SplineUtility.CalculateTangents(spline);

             Debug.Log($"Generated spline from GrindPath with {spline.Count} knots. Is Closed: {spline.Closed}", this);

             // After generating spline, update collider and length
            UpdateCollider(); 
             CalculateTotalLength();
        }
        */
    }
} 