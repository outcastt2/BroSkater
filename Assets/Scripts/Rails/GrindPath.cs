using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace BroSkater.Rails
{
    // Add the missing GrindPointType enum
    public enum GrindPointType
    {
        Entry,
        Middle,
        Exit
    }
    
    [RequireComponent(typeof(MeshFilter))]
    public class GrindPath : MonoBehaviour
    {
        [Header("Grind Path Settings")]
        [SerializeField] private bool visualizePath = true;
        [SerializeField] private Color pathColor = Color.cyan;
        [SerializeField] private float pathThickness = 0.05f;
        [SerializeField] private float weightThreshold = 0.5f; // Minimum vertex color alpha to consider part of GRIND group
        [SerializeField] private float colliderRadius = 0.1f; // Radius for the generated collider
        [SerializeField] private float pointDetectionRadius = 0.4f; // Radius for entry/exit point detection
        [SerializeField] private GameObject grindPointPrefab; // Reference to the grind point prefab
        
        private List<Vector3> pathPoints = new List<Vector3>();
        private List<Vector3> pathTangents = new List<Vector3>();
        private float pathLength = 0f;
        private Vector3[] debugPoints;
        private List<GrindPoint> grindPoints = new List<GrindPoint>();
        
        // --- Added for Debugging --- 
        private List<Vector3> debugLocalPathPoints = new List<Vector3>(); 
        // ---------------------------
        
        // Entry/exit points (first and last point in each segment)
        private List<GrindPoint> entryExitPoints = new List<GrindPoint>();
        
        public List<Vector3> PathPoints => pathPoints;
        public List<Vector3> PathTangents => pathTangents;
        public float PathLength => pathLength;
        public List<GrindPoint> GrindPoints => grindPoints;
        public List<GrindPoint> EntryExitPoints => entryExitPoints;
        
        // Represents a point on the grind path with metadata
        public class GrindPoint
        {
            public Vector3 position;      // World position
            public Vector3 tangent;       // Direction along rail
            public float distanceAlongPath; // Distance from start of rail
            public SphereCollider detector; // Optional collider for this point
            public GrindPointType pointType; // Type of grind point
            public GameObject gameObject; // Reference to the GameObject
            public Transform transform => gameObject.transform; // Shorthand to access transform
            
            public GrindPoint(Vector3 pos, Vector3 tan, float dist)
            {
                position = pos;
                tangent = tan;
                distanceAlongPath = dist;
            }
            
            // Add method to get components from the GameObject
            public T GetComponent<T>() where T : Component
            {
                return gameObject.GetComponent<T>();
            }
        }
        
        private void OnEnable()
        {
            ExtractPathFromMesh();
            GenerateGrindPoints();
        }

        private void OnDisable()
        {
            CleanupColliders();
        }
        
        private void CleanupColliders()
        {
            // Clean up any existing point detectors
            foreach (var point in grindPoints)
            {
                if (point.detector != null && point.gameObject != null)
            {
                    if (point.gameObject != gameObject)
                        DestroyImmediate(point.gameObject);
                    else
                        DestroyImmediate(point.detector);
                }
            }
            
            grindPoints.Clear();
            entryExitPoints.Clear();
        }
        
        // --- Added Public Regeneration Method ---
        public void ForceRegenerate()
        {
            Debug.Log($"[{gameObject.name} GrindPath] ForceRegenerate called.", this);
            ExtractPathFromMesh();
            GenerateGrindPoints();
        }
        // ---------------------------------------
        
        public void GenerateGrindPoints()
        {
            Debug.Log($"[{gameObject.name} GrindPath] GenerateGrindPoints called.", this); // Added Log
            // Clean up any existing grind points first
            CleanupGrindPoints();

            if (pathPoints == null || pathPoints.Count < 2)
            {
                Debug.LogWarning($"[{gameObject.name} GrindPath] Cannot generate grind points: not enough path points ({pathPoints?.Count ?? 0})", this); // Improved Log
                return;
            }
            
            if (grindPointPrefab == null)
            {
                Debug.LogError($"[{gameObject.name} GrindPath] Grind point prefab is missing", this); // Improved Log
                return;
            }

            // Calculate the total path length
            float totalPathLength = PathLength; // Use cached length
            Debug.Log($"[{gameObject.name} GrindPath] Generating grind points for path of length {totalPathLength:F2}", this);
            
            // --- Loop Detection --- 
            bool isLoop = false;
            float loopThreshold = 0.1f; // Distance threshold to consider it a loop
            if (pathPoints.Count > 2) // Need at least 3 points to form a meaningful loop
            {
                 Vector3 startPos = pathPoints[0];
                 Vector3 endPos = pathPoints[pathPoints.Count - 1];
                 float startEndDistance = Vector3.Distance(startPos, endPos);
                 isLoop = startEndDistance < loopThreshold;
                 Debug.Log($"[{gameObject.name} GrindPath] Loop Check: Start={startPos}, End={endPos}, Dist={startEndDistance:F3}. Is Loop? {isLoop}", this);
            }
            // --------------------

            // Minimum number of points for the entire path (can be adjusted)
            int minPointsForGeneration = Mathf.Max(2, Mathf.CeilToInt(totalPathLength * 1.5f)); 
            Debug.Log($"[{gameObject.name} GrindPath] Min points for generation: {minPointsForGeneration}", this);

            if (!isLoop)
            {
                // --- Generate START Exit Point (Only for non-loops) --- 
                GameObject startExitObj = Instantiate(grindPointPrefab, transform);
                if (startExitObj == null) { Debug.LogError($"[{gameObject.name} GrindPath] Failed to Instantiate startExitObj!", this); return; } // Added Check
                startExitObj.transform.position = pathPoints[0];
                startExitObj.name = "GrindPoint_Exit_Start";
                GrindPoint startExit = new GrindPoint(pathPoints[0], Vector3.forward, 0f);
                startExit.gameObject = startExitObj;
                startExit.pointType = GrindPointType.Exit; 
                grindPoints.Add(startExit);
                entryExitPoints.Add(startExit); // Add to exits
                Debug.Log($"[{gameObject.name} GrindPath] Generated Start Exit Point.", this);

                // --- Generate END Exit Point (Only for non-loops) --- 
                GameObject endExitObj = Instantiate(grindPointPrefab, transform);
                if (endExitObj == null) { Debug.LogError($"[{gameObject.name} GrindPath] Failed to Instantiate endExitObj!", this); return; } // Added Check
                endExitObj.transform.position = pathPoints[pathPoints.Count - 1];
                endExitObj.name = "GrindPoint_Exit_End";
                GrindPoint endExit = new GrindPoint(pathPoints[pathPoints.Count - 1], Vector3.forward, totalPathLength);
                endExit.gameObject = endExitObj;
                endExit.pointType = GrindPointType.Exit;
                // Note: We add this to grindPoints later after sorting for non-loops
                entryExitPoints.Add(endExit); // Add to exits
                 Debug.Log($"[{gameObject.name} GrindPath] Generated End Exit Point.", this);
            }
            else
            {
                 Debug.Log($"[{gameObject.name} GrindPath] Skipping Start/End point generation because it's a loop.", this);
            }

            // --- Generate Intermediate Points --- 
            int pointsToGenerate = isLoop ? minPointsForGeneration : Mathf.Max(0, minPointsForGeneration - 2);
            Debug.Log($"[{gameObject.name} GrindPath] Attempting to generate {pointsToGenerate} mid points.", this);

            if (pointsToGenerate > 0 && totalPathLength > 0.01f) 
            {
                // Adjust spacing calculation for loops vs non-loops
                float spacing = totalPathLength / (isLoop ? pointsToGenerate : (pointsToGenerate + 1)); // Divide by total points needed (including virtual ends for non-loops)
                Debug.Log($"[{gameObject.name} GrindPath] Mid point spacing: {spacing:F3}", this);
                
                int midPointCount = 0;
                for (int i = 1; i <= pointsToGenerate; i++)
                {
                    float targetDist = i * spacing;

                    // Get point data directly using the path's method
                    if (GetPointAtDistance(targetDist, out Vector3 pointPosition, out Vector3 pointTangent, out bool endReachedCheck))
                    {
                         // Safety check: Don't add points too close to actual ends for non-loops
                        if (!isLoop && (targetDist < spacing * 0.1f || targetDist > totalPathLength - spacing * 0.1f))
                        {
                            continue;
                        }

                        GameObject pointObj = Instantiate(grindPointPrefab, transform);
                        if (pointObj == null) { Debug.LogError($"[{gameObject.name} GrindPath] Failed to Instantiate mid point {i}!", this); continue; } 
                        pointObj.transform.position = pointPosition;
                        pointObj.name = $"GrindPoint_Mid_{i}";
                        
                        GrindPoint midPoint = new GrindPoint(pointPosition, pointTangent, targetDist);
                        midPoint.gameObject = pointObj;
                        midPoint.pointType = GrindPointType.Middle; // Mark as Middle
                        grindPoints.Add(midPoint);
                        midPointCount++;
                    }
                    else
                    {
                         Debug.LogWarning($"[{gameObject.name} GrindPath] GetPointAtDistance failed for mid point at distance {targetDist:F2}", this);
                    }
                }
                 Debug.Log($"[{gameObject.name} GrindPath] Actually generated {midPointCount} mid points.", this);
            }
            
            // Add the end exit point only if it's not a loop
            if (!isLoop && entryExitPoints.Count > 1) // Ensure endExit was created
            {
                grindPoints.Add(entryExitPoints[1]); // Add the pre-created endExit point
            }
            
            // Sort points by distance along path to ensure correct order
            grindPoints = grindPoints.OrderBy(p => p.distanceAlongPath).ToList();
            
            // Calculate tangents based on the final sorted list
            CalculateGrindPointTangents();
            
            Debug.Log($"Generated {grindPoints.Count} grind points ({entryExitPoints.Count} exit points).", this);
        }

        private float GetDistanceToSegment(int segmentIndex, List<Vector3> pathPoints)
        {
            float distance = 0f;

            // Calculate the distance to the start of the segment
            for (int i = 0; i < segmentIndex; i++)
            {
                distance += Vector3.Distance(pathPoints[i], pathPoints[i + 1]);
            }
            
            return distance;
        }

        // Calculate proper tangents for all points
        private void CalculateGrindPointTangents()
        {
            if (grindPoints.Count < 2)
                return;

            for (int i = 0; i < grindPoints.Count; i++)
            {
                Vector3 tangent;
                
                if (i == 0) // First point (start exit)
                {
                    tangent = (grindPoints[i + 1].position - grindPoints[i].position).normalized;
                }
                else if (i == grindPoints.Count - 1) // Last point (end exit)
                {
                    tangent = (grindPoints[i].position - grindPoints[i - 1].position).normalized;
                }
                else // Middle points
                {
                    Vector3 prevToThis = (grindPoints[i].position - grindPoints[i - 1].position).normalized;
                    Vector3 thisToNext = (grindPoints[i + 1].position - grindPoints[i].position).normalized;
                    // Check for zero vectors in case points are identical
                    if (prevToThis == Vector3.zero) prevToThis = thisToNext;
                    if (thisToNext == Vector3.zero) thisToNext = prevToThis;
                    tangent = ((prevToThis + thisToNext) * 0.5f).normalized;
                    // If tangent is still zero (collinear points?), use one of the directions
                    if (tangent == Vector3.zero) tangent = thisToNext;
                }
                
                // Set the tangent in the GrindPoint
                grindPoints[i].tangent = tangent;
                
                // pointType is now set during generation, no need to set it here
                // if (i == 0)
                //     grindPoints[i].pointType = GrindPointType.Entry; // OLD
                // else if (i == grindPoints.Count - 1)
                //     grindPoints[i].pointType = GrindPointType.Exit; // OLD
                // else
                //     grindPoints[i].pointType = GrindPointType.Middle; // OLD
            }
        }
        
        // Custom component to identify a grind point and provide easy access to its properties
        public class GrindPointDetector : MonoBehaviour
        {
            public GrindPoint grindPoint;
            public GrindPath parentPath;
            
            public void InitializePoint(GrindPoint point, GrindPath path)
            {
                grindPoint = point;
                parentPath = path;
            }
            
            private void OnDrawGizmos()
            {
                if (grindPoint != null)
                {
                    Gizmos.color = grindPoint.pointType == GrindPointType.Exit ? Color.green : Color.cyan;
                    Gizmos.DrawSphere(transform.position, GetComponent<SphereCollider>().radius * 0.8f);
                }
            }
        }
        
        // Component for the connecting line between points
        public class GrindPathConnector : MonoBehaviour
        {
            public GrindPath parentPath;
            public GrindPoint startPoint;
            public GrindPoint endPoint;
            
            public void Initialize(GrindPath path, GrindPoint start, GrindPoint end)
            {
                parentPath = path;
                startPoint = start;
                endPoint = end;
            }
            
            private void OnDrawGizmos()
            {
                if (startPoint != null && endPoint != null)
                {
                    Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.5f); // Light blue with transparency
                    Gizmos.DrawLine(startPoint.position, endPoint.position);
                    
                    // Draw the collider shape
                    CapsuleCollider capsule = GetComponent<CapsuleCollider>();
                    if (capsule != null)
                    {
                        Matrix4x4 oldMatrix = Gizmos.matrix;
                        Gizmos.matrix = transform.localToWorldMatrix;
                        
                        // Draw a simplified representation of the capsule using standard Gizmos methods
                        float halfHeight = capsule.height * 0.5f;
                        Vector3 center = Vector3.zero;
                        
                        // Draw two circles at the ends
                        DrawWireCircle(center + Vector3.forward * halfHeight, capsule.radius, Vector3.forward);
                        DrawWireCircle(center - Vector3.forward * halfHeight, capsule.radius, Vector3.forward);
                        
                        // Draw connecting lines
                        int segments = 8;
                        float angleStep = 360f / segments;
                        
                        for (int i = 0; i < segments; i++)
                        {
                            float angle = i * angleStep * Mathf.Deg2Rad;
                            float x = Mathf.Cos(angle) * capsule.radius;
                            float y = Mathf.Sin(angle) * capsule.radius;
                            
                            Vector3 start = new Vector3(x, y, halfHeight);
                            Vector3 end = new Vector3(x, y, -halfHeight);
                            Gizmos.DrawLine(start, end);
                        }
                        
                        Gizmos.matrix = oldMatrix;
                }
            }
            }
            
            // Helper method to draw a circle in 3D space
            private void DrawWireCircle(Vector3 center, float radius, Vector3 normal)
            {
                int segments = 16;
                float angleStep = 360f / segments;
                Vector3 forward = Vector3.forward;
                
                // Find a perpendicular vector to normal for our first point
                Vector3 right = Vector3.Cross(normal, forward);
                if (right.magnitude < 0.001f)
                    {
                    right = Vector3.Cross(normal, Vector3.up);
                }
                right.Normalize();
                
                Vector3 up = Vector3.Cross(right, normal).normalized;
                
                Vector3 prevPoint = center + right * radius;
                
                for (int i = 1; i <= segments; i++)
                {
                    float angle = i * angleStep * Mathf.Deg2Rad;
                    Vector3 nextPoint = center + right * Mathf.Cos(angle) * radius + up * Mathf.Sin(angle) * radius;
                    Gizmos.DrawLine(prevPoint, nextPoint);
                    prevPoint = nextPoint;
                }
            }
        }
        
        private void ExtractPathFromMesh()
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogError("MeshFilter or sharedMesh not found!", this);
                return;
            }

            Mesh mesh = meshFilter.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            Color[] colors = mesh.colors;
            int[] triangles = mesh.triangles;

            Debug.Log($"[{gameObject.name} GrindPath] Extracting path. Verts: {vertices.Length}, Colors: {colors.Length}, Tris: {triangles.Length}. Mesh R/W Enabled? {mesh.isReadable}", this); // Added Log

            if (colors.Length != vertices.Length)
            {
                Debug.LogError("Vertex color data missing or mismatched! Cannot extract GRIND group path.", this);
                return;
            }

            // --- Step 1: Collect all potential grind edges based on vertex colors --- 
            List<Edge> potentialEdgesByIndex = new List<Edge>();
            HashSet<Edge> processedEdgesByIndex = new HashSet<Edge>();
            int grindVertCount = 0; 

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int v1 = triangles[i];
                int v2 = triangles[i + 1];
                int v3 = triangles[i + 2];

                CheckAndAddPotentialGrindEdgeByIndex(v1, v2, colors, processedEdgesByIndex, potentialEdgesByIndex, ref grindVertCount);
                CheckAndAddPotentialGrindEdgeByIndex(v2, v3, colors, processedEdgesByIndex, potentialEdgesByIndex, ref grindVertCount);
                CheckAndAddPotentialGrindEdgeByIndex(v3, v1, colors, processedEdgesByIndex, potentialEdgesByIndex, ref grindVertCount);
            }
            Debug.Log($"[{gameObject.name} GrindPath] Step 1: Found {grindVertCount} verts meeting threshold, {potentialEdgesByIndex.Count} potential edges by index.", this);

            if (potentialEdgesByIndex.Count == 0)
            {
                 Debug.LogWarning($"[{gameObject.name} GrindPath] No edges found for the GRIND group. Check vertex colors/weights and threshold ({weightThreshold}).", this);
                 pathPoints = new List<Vector3>();
                 debugLocalPathPoints = new List<Vector3>();
                 return;
            }

            // --- Step 2: Filter spatially duplicate edges --- 
            List<(Vector3, Vector3)> uniqueEdgePairs = new List<(Vector3, Vector3)>();
            HashSet<System.Tuple<Vector3, Vector3>> addedPositionPairs = new HashSet<System.Tuple<Vector3, Vector3>>();
            float spatialTolerance = 0.001f; // Tolerance for considering positions equal

            foreach (var edgeIdx in potentialEdgesByIndex)
            {
                Vector3 p1 = vertices[edgeIdx.vertex1];
                Vector3 p2 = vertices[edgeIdx.vertex2];

                // Create canonical representation (e.g., always order by X, then Y, then Z)
                System.Tuple<Vector3, Vector3> canonicalPair = CanonicalizeEdge(p1, p2);
                
                // Check if this spatial edge is already added
                bool foundDuplicate = false;
                foreach (var addedPair in addedPositionPairs)
                {
                    // Check against the canonical representation
                    if (Vector3.Distance(addedPair.Item1, canonicalPair.Item1) < spatialTolerance &&
                        Vector3.Distance(addedPair.Item2, canonicalPair.Item2) < spatialTolerance)
                    {
                        foundDuplicate = true;
                        break;
                    }
                }

                if (!foundDuplicate)
                {
                    uniqueEdgePairs.Add((p1, p2)); // Add the unique spatial edge pair
                    addedPositionPairs.Add(canonicalPair); // Store its canonical form
                }
            }
            Debug.Log($"[{gameObject.name} GrindPath] Step 2: Filtered to {uniqueEdgePairs.Count} unique spatial edges.", this);

            if (uniqueEdgePairs.Count == 0)
            {
                Debug.LogWarning($"[{gameObject.name} GrindPath] No unique spatial edges found after filtering.", this);
                pathPoints = new List<Vector3>();
                debugLocalPathPoints = new List<Vector3>();
                return;
            }

            // --- Step 3: Order the unique edges --- 
            List<Vector3> localPathPoints = OrderEdgesIntoPath(uniqueEdgePairs);
            debugLocalPathPoints = new List<Vector3>(localPathPoints); // Store for gizmo debugging

            // --- Added Logging for Local Points --- 
            if (localPathPoints.Count > 1)
            {
                Debug.Log($"[{gameObject.name} GrindPath] LOCAL path points: Count={localPathPoints.Count}, First={localPathPoints[0]}, Last={localPathPoints[localPathPoints.Count - 1]}", this);
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name} GrindPath] Ordering resulted in {localPathPoints.Count} local points.", this);
            }
            // ------------------------------------

            // Convert local path points to world space
            pathPoints = localPathPoints.Select(p => transform.TransformPoint(p)).ToList();
            
            Debug.Log($"[{gameObject.name} GrindPath] Path points extracted: {pathPoints.Count}", this); // Added Log

            if (pathPoints.Count >= 2)
            {
                CalculateTangents();
                CalculatePathLength();
                // Don't generate GrindPoints here, let OnEnable handle it
            }
            else
            {
                 Debug.LogError($"[{gameObject.name} GrindPath] Failed to order edges into a valid path ({pathPoints.Count} points).", this);
                 pathPoints.Clear(); // Ensure list is empty on failure
            }
        }

        // Renamed and modified CheckAndAdd...
        private void CheckAndAddPotentialGrindEdgeByIndex(int v1, int v2, Color[] colors, HashSet<Edge> processedEdges, List<Edge> potentialEdges, ref int grindVertCounter)
        {
            // Bounds check
            if (v1 >= colors.Length || v2 >= colors.Length) return;

            bool v1Grind = colors[v1].a >= weightThreshold;
            bool v2Grind = colors[v2].a >= weightThreshold;
            
            if(v1Grind) grindVertCounter++; 

            if (v1Grind && v2Grind)
            {
                Edge edge = new Edge(v1, v2); // Creates canonical edge {minIdx, maxIdx}

                if (!processedEdges.Contains(edge))
                {
                    potentialEdges.Add(edge); // Add the index-based edge
                    processedEdges.Add(edge); // Mark index-based edge as processed
                }
            }
        }
        
        // Helper to create a consistent representation of an edge based on position
        private System.Tuple<Vector3, Vector3> CanonicalizeEdge(Vector3 p1, Vector3 p2)
        {
             // Order points consistently (e.g., by X, then Y, then Z) to handle reversed edges
            if (p1.x > p2.x || 
               (Mathf.Approximately(p1.x, p2.x) && p1.y > p2.y) || 
               (Mathf.Approximately(p1.x, p2.x) && Mathf.Approximately(p1.y, p2.y) && p1.z > p2.z))
            {
                return System.Tuple.Create(p2, p1); // Swap them
            }
            else
            {
                 return System.Tuple.Create(p1, p2); // Keep original order
            }
        }

        private struct Edge
        {
            public int vertex1;
            public int vertex2;

            public Edge(int v1, int v2)
            {
                // Always store vertices in order for proper equality comparison
                if (v1 < v2)
                {
                    vertex1 = v1;
                    vertex2 = v2;
                }
                else
                {
                    vertex1 = v2;
                    vertex2 = v1;
                }
            }

            public override bool Equals(object obj)
            {
                if (!(obj is Edge)) return false;
                Edge other = (Edge)obj;
                return vertex1 == other.vertex1 && vertex2 == other.vertex2;
            }

            public override int GetHashCode()
            {
                return vertex1.GetHashCode() ^ (vertex2.GetHashCode() << 2);
            }
        }

        private void CalculateTangents()
        {
            pathTangents.Clear();
            
            if (pathPoints.Count < 2)
            {
                Debug.LogWarning("Not enough points to calculate tangents");
                return;
            }
            
            // First point's tangent
            pathTangents.Add((pathPoints[1] - pathPoints[0]).normalized);
            
            // Middle points
            for (int i = 1; i < pathPoints.Count - 1; i++)
            {
                Vector3 tangent = (pathPoints[i + 1] - pathPoints[i - 1]).normalized;
                pathTangents.Add(tangent);
            }
            
            // Last point's tangent
            pathTangents.Add((pathPoints[pathPoints.Count - 1] - pathPoints[pathPoints.Count - 2]).normalized);
        }
        
        private void CalculatePathLength()
        {
            pathLength = 0f;
            
            if (pathPoints.Count < 2)
                return;
                
            for (int i = 0; i < pathPoints.Count - 1; i++)
            {
                pathLength += Vector3.Distance(pathPoints[i], pathPoints[i + 1]);
            }
            
            Debug.Log($"Calculated path length: {pathLength:F2}m with {pathPoints.Count} points");
        }
        
        // Find the closest point on the path to the input position
        public bool GetClosestPointOnPath(Vector3 position, out Vector3 closestPoint, out Vector3 tangent, out float normalizedPosition)
        {
            closestPoint = Vector3.zero;
            tangent = Vector3.forward;
            normalizedPosition = 0f;
            
            if (pathPoints.Count < 2)
                return false;
                
            float minDistance = float.MaxValue;
            int closestSegmentIndex = 0;
            Vector3 closestSegmentPoint = Vector3.zero;
            float closestSegmentT = 0f;
            
            float distanceAlongPath = 0f;
            float totalDistanceToClosestPoint = 0f;
            
            // Check each segment
            for (int i = 0; i < pathPoints.Count - 1; i++)
            {
                Vector3 startPoint = pathPoints[i];
                Vector3 endPoint = pathPoints[i + 1];
                Vector3 segmentDirection = (endPoint - startPoint).normalized;
                float segmentLength = Vector3.Distance(startPoint, endPoint);
                
                // Project position onto segment
                Vector3 toPosition = position - startPoint;
                float dot = Vector3.Dot(toPosition, segmentDirection);
                dot = Mathf.Clamp(dot, 0f, segmentLength);
                
                Vector3 projectedPoint = startPoint + segmentDirection * dot;
                float distanceToSegment = Vector3.Distance(position, projectedPoint);
                
                if (distanceToSegment < minDistance)
                {
                    minDistance = distanceToSegment;
                    closestSegmentIndex = i;
                    closestSegmentPoint = projectedPoint;
                    closestSegmentT = dot / segmentLength;
                    totalDistanceToClosestPoint = distanceAlongPath + dot;
                }
                
                distanceAlongPath += segmentLength;
            }
            
            // Set output values
            closestPoint = closestSegmentPoint;
            tangent = (pathPoints[closestSegmentIndex + 1] - pathPoints[closestSegmentIndex]).normalized;
            normalizedPosition = totalDistanceToClosestPoint / pathLength;
            
            return true;
        }
        
        public float GetNormalizedPositionOnPath(Vector3 position)
        {
            Vector3 closestPoint;
            Vector3 tangent;
            float normalizedPosition;
            
            if (GetClosestPointOnPath(position, out closestPoint, out tangent, out normalizedPosition))
                return normalizedPosition;
                
            return 0f;
        }
        
        public bool IsAtEndPoint(Vector3 position, float threshold = 0.1f)
        {
            if (pathPoints.Count < 2) return true;
            
            Vector3 endPoint = pathPoints[pathPoints.Count - 1];
            return Vector3.Distance(position, endPoint) <= threshold;
        }
        
        public bool IsNearEndPoint(Vector3 position, float threshold = 0.5f)
        {
            if (pathPoints.Count < 2) return true;
            
            float normalizedPos = GetNormalizedPositionOnPath(position);
            return normalizedPos > (1.0f - threshold / pathLength);
        }
        
        /// <summary>
        /// Checks if there's an entry/exit point near the given position.
        /// </summary>
        /// <param name="position">Position to check from</param>
        /// <param name="maxDistance">Maximum distance to consider "near"</param>
        /// <param name="isTravelingForward">Whether the player is traveling in the forward direction of the rail</param>
        /// <returns>True if an appropriate entry/exit point is found</returns>
        public bool IsNearEntryExitPoint(Vector3 position, float maxDistance, bool isTravelingForward)
        {
            if (entryExitPoints.Count == 0)
                return false;
                
            foreach (var point in entryExitPoints)
            {
                float distance = Vector3.Distance(position, point.position);
                
                if (distance <= maxDistance)
                {
                    // Check if this is a valid exit point based on direction
                    bool isStart = Mathf.Approximately(point.distanceAlongPath, 0f);
                    bool isEnd = Mathf.Approximately(point.distanceAlongPath, pathLength);
                    
                    // Valid exit conditions: at start going backward or at end going forward
                    if ((isStart && !isTravelingForward) || (isEnd && isTravelingForward))
                    {
                        Debug.Log($"Near exit point: {(isStart ? "START" : "END")} while moving {(isTravelingForward ? "FORWARD" : "BACKWARD")}, distance: {distance:F2}");
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Check if the given distance is near the end of the path.
        /// </summary>
        /// <param name="distance">Current distance along the path</param>
        /// <param name="endThreshold">Distance threshold to consider "near end"</param>
        /// <returns>True if near the end of the path</returns>
        public bool IsNearEnd(float distance, float endThreshold)
        {
            if (pathLength == 0f)
                return true;
                
            float remainingDistance = pathLength - distance;
            return remainingDistance <= endThreshold;
        }
        
        /// <summary>
        /// Gets a point on the path at the specified distance.
        /// </summary>
        /// <param name="distance">Distance along the path</param>
        /// <param name="point">Output point at the specified distance</param>
        /// <param name="tangent">Output tangent at the specified distance</param>
        /// <param name="endReached">True if the end of the path has been reached</param>
        /// <returns>True if successful, false if path is invalid</returns>
        public bool GetPointAtDistance(float distance, out Vector3 point, out Vector3 tangent, out bool endReached)
        {
            point = Vector3.zero;
            tangent = Vector3.forward;
            endReached = false;
            
            if (pathPoints.Count < 2)
                return false;
                
            // Explicitly check both ends, with slightly larger threshold for reliability
            bool atStart = distance <= 0.05f; 
            bool atEnd = distance >= pathLength - 0.05f;
            endReached = atStart || atEnd;
            
            // Debug logging for endpoint detection
            if (atStart || atEnd)
            {
                Debug.Log($"Rail Endpoint: distance={distance:F2}, pathLength={pathLength:F2}, atStart={atStart}, atEnd={atEnd}");
            }
            
            // Don't clamp distance here - instead, detect if we're beyond the path
            // and signal that the end was reached, but still return the correct point
            if (distance <= 0f)
            {
                point = pathPoints[0];
                tangent = pathTangents[0];
                endReached = true;
                return true;
            }
            
            if (distance >= pathLength)
            {
                point = pathPoints[pathPoints.Count - 1];
                tangent = pathTangents[pathTangents.Count - 1];
                endReached = true;
                return true;
            }
            
            // Not at the ends - continue with normal path traversal
            float currentDistance = 0f;
            int segmentIndex = 0;
            
            while (segmentIndex < pathPoints.Count - 1)
            {
                float segmentLength = Vector3.Distance(pathPoints[segmentIndex], pathPoints[segmentIndex + 1]);
                
                // Check if the target distance falls within the *end* of this segment
                // Add a small epsilon for floating point safety
                if (currentDistance + segmentLength + 0.0001f >= distance)
                {
                    // This segment contains our target distance
                    // Handle potential division by zero and clamp segmentT
                    float segmentT = segmentLength > 0.0001f ? (distance - currentDistance) / segmentLength : 0f;
                    segmentT = Mathf.Clamp01(segmentT); // Clamp for safety
                    
                    // Linear interpolation between points
                    point = Vector3.Lerp(pathPoints[segmentIndex], pathPoints[segmentIndex + 1], segmentT);
                    
                    // Interpolate tangent as well
                    tangent = Vector3.Lerp(pathTangents[segmentIndex], pathTangents[segmentIndex + 1], segmentT).normalized;
                    
                    return true;
                }
                
                currentDistance += segmentLength;
                segmentIndex++;
            }
            
            // Shouldn't reach here given our early end checks
            Debug.LogWarning("Unexpected condition in GetPointAtDistance");
            point = pathPoints[pathPoints.Count - 1];
            tangent = pathTangents[pathTangents.Count - 1];
            endReached = true;
            return true;
        }
        
        public Vector3 GetPointAtDistance(float distance)
        {
            // Clamp to path length
            distance = Mathf.Clamp(distance, 0, pathLength);
            
            // Find the segment
            float accumulatedDistance = 0f;
            for (int i = 0; i < pathPoints.Count - 1; i++)
            {
                float segmentLength = Vector3.Distance(pathPoints[i], pathPoints[i + 1]);
                
                if (accumulatedDistance + segmentLength >= distance)
                {
                    // The point is on this segment
                    float localDistance = distance - accumulatedDistance;
                    float t = localDistance / segmentLength;
                    return Vector3.Lerp(pathPoints[i], pathPoints[i + 1], t);
                }
                
                accumulatedDistance += segmentLength;
            }
            
            // If we got here, return the last point
            return pathPoints[pathPoints.Count - 1];
        }
        
        // Method to check and fix entry/exit points
        // This ensures first and last points are always marked as entry/exit
        public void ForceRefreshEntryExitPoints()
        {
            if (grindPoints.Count < 2) return;
            
            Debug.Log($"ForceRefreshEntryExitPoints called on {gameObject.name} with {grindPoints.Count} points");
            
            // Force first and last points to be entry/exit points
            var firstPoint = grindPoints[0];
            var lastPoint = grindPoints[grindPoints.Count - 1];
            
            // Process first point
            if (firstPoint.pointType != GrindPointType.Exit || !entryExitPoints.Contains(firstPoint))
            {
                Debug.Log($"Fixing first point on {gameObject.name}");
                firstPoint.pointType = GrindPointType.Exit;
                
                if (!entryExitPoints.Contains(firstPoint))
                {
                    entryExitPoints.Add(firstPoint);
                }
                
                // Update detector if it exists
                if (firstPoint.detector != null)
                {
                    var detector = firstPoint.detector.gameObject.GetComponent<GrindPointDetector>();
                    if (detector != null)
                    {
                        detector.grindPoint.pointType = GrindPointType.Exit;
                        firstPoint.detector.radius = pointDetectionRadius * 1.5f;
                    }
                }
            }
            
            // Process last point
            if (lastPoint.pointType != GrindPointType.Exit || !entryExitPoints.Contains(lastPoint))
            {
                Debug.Log($"Fixing last point on {gameObject.name}");
                lastPoint.pointType = GrindPointType.Exit;
                
                if (!entryExitPoints.Contains(lastPoint))
                {
                    entryExitPoints.Add(lastPoint);
                }
                
                // Update detector if it exists
                if (lastPoint.detector != null)
                {
                    var detector = lastPoint.detector.gameObject.GetComponent<GrindPointDetector>();
                    if (detector != null)
                    {
                        detector.grindPoint.pointType = GrindPointType.Exit;
                        lastPoint.detector.radius = pointDetectionRadius * 1.5f;
                    }
                }
            }
            
            // Make sure middle points are NOT entry/exit points
            for (int i = 1; i < grindPoints.Count - 1; i++)
            {
                var midPoint = grindPoints[i];
                if (midPoint.pointType == GrindPointType.Exit || entryExitPoints.Contains(midPoint))
                {
                    Debug.Log($"Fixing middle point {i} on {gameObject.name}");
                    midPoint.pointType = GrindPointType.Middle;
                    
                    if (entryExitPoints.Contains(midPoint))
                    {
                        entryExitPoints.Remove(midPoint);
                    }
                    
                    // Update detector if it exists
                    if (midPoint.detector != null)
                    {
                        var detector = midPoint.detector.gameObject.GetComponent<GrindPointDetector>();
                        if (detector != null)
                        {
                            detector.grindPoint.pointType = GrindPointType.Middle;
                            midPoint.detector.radius = pointDetectionRadius;
                        }
                    }
                }
            }
        }
        
        // --- Add OnDrawGizmos for Debugging ---
        private void OnDrawGizmos()
        {
            if (!visualizePath) return;

            // Draw Local Path Points (relative to object origin)
            if (debugLocalPathPoints != null && debugLocalPathPoints.Count >= 2)
            {
                Gizmos.color = Color.yellow; // Yellow for local points
                for (int i = 0; i < debugLocalPathPoints.Count - 1; i++)
                {
                    // Convert local points to world for drawing Gizmos
                    Vector3 worldP1 = transform.TransformPoint(debugLocalPathPoints[i]);
                    Vector3 worldP2 = transform.TransformPoint(debugLocalPathPoints[i+1]);
                    Gizmos.DrawLine(worldP1, worldP2);
                    Gizmos.DrawSphere(worldP1, pathThickness * 2f); 
                }
                // Draw last point
                Gizmos.DrawSphere(transform.TransformPoint(debugLocalPathPoints[debugLocalPathPoints.Count - 1]), pathThickness * 2f);
            }
            
            // Draw World Path Points (stored in pathPoints / debugPoints)
            if (debugPoints != null && debugPoints.Length >= 2)
            {
                Gizmos.color = pathColor; // Use original pathColor (cyan) for world points
                for (int i = 0; i < debugPoints.Length - 1; i++)
                {
                    Gizmos.DrawLine(debugPoints[i], debugPoints[i + 1]);
                    Gizmos.DrawSphere(debugPoints[i], pathThickness);
                }
                Gizmos.DrawSphere(debugPoints[debugPoints.Length - 1], pathThickness);
            }
        }
        // --- End OnDrawGizmos --- 

        private void CleanupGrindPoints()
        {
            // Destroy any existing grind points
            if (grindPoints != null)
            {
                foreach (GrindPoint point in grindPoints)
                {
                    if (point.gameObject != null)
                    {
                        DestroyImmediate(point.gameObject);
                    }
                }
            }
            
            // Clear the list
            grindPoints = new List<GrindPoint>();
            entryExitPoints = new List<GrindPoint>();

            // Also clean up any child objects with "GrindPoint" in the name
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name.Contains("GrindPoint"))
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private List<Vector3> OrderEdgesIntoPath(List<(Vector3, Vector3)> edges)
        {
            List<Vector3> orderedPath = new List<Vector3>();
            if (edges.Count == 0) return orderedPath;
            Debug.Log($"[{gameObject.name} GrindPath] --- Ordering {edges.Count} Unique Spatial Edges ---", this); // Log start

            // Make a mutable copy
            List<(Vector3, Vector3)> remainingEdges = new List<(Vector3, Vector3)>(edges);

            // Calculate the bounds of all vertices to find extreme points
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            
            foreach (var edge in remainingEdges)
            {
                min = Vector3.Min(min, edge.Item1);
                min = Vector3.Min(min, edge.Item2);
                max = Vector3.Max(max, edge.Item1);
                max = Vector3.Max(max, edge.Item2);
            }
            
            // Determine the primary axis (longest dimension)
            Vector3 size = max - min;
            int primaryAxis = 0; // 0 = X, 1 = Y, 2 = Z
            
            if (size.y > size.x && size.y > size.z)
                primaryAxis = 1;
            else if (size.z > size.x && size.z > size.y)
                primaryAxis = 2;
            
            // Choose better starting edge - use the edge with minimum value along primary axis
            var firstEdge = remainingEdges[0];
            float minValue = float.MaxValue;
            
            foreach (var edge in remainingEdges)
            {
                float edgeValue1 = primaryAxis == 0 ? edge.Item1.x : (primaryAxis == 1 ? edge.Item1.y : edge.Item1.z);
                float edgeValue2 = primaryAxis == 0 ? edge.Item2.x : (primaryAxis == 1 ? edge.Item2.y : edge.Item2.z);
                float minEdgeValue = Mathf.Min(edgeValue1, edgeValue2);
                
                if (minEdgeValue < minValue)
                {
                    minValue = minEdgeValue;
                    firstEdge = edge;
                }
            }
            
            // Determine which point of the first edge should come first
            Vector3 point1 = firstEdge.Item1;
            Vector3 point2 = firstEdge.Item2;
            Debug.Log($"[{gameObject.name} GrindPath] First Edge Selected: {point1} -> {point2}", this); // Log first edge
            float pointValue1 = primaryAxis == 0 ? point1.x : (primaryAxis == 1 ? point1.y : point1.z);
            float pointValue2 = primaryAxis == 0 ? point2.x : (primaryAxis == 1 ? point2.y : point2.z);
            
            // Add points in correct order (min value first)
            if (pointValue1 <= pointValue2)
            {
                orderedPath.Add(point1);
                orderedPath.Add(point2);
                Debug.Log($"[{gameObject.name} GrindPath] Added first edge points: {point1}, then {point2}", this); // Log added points
            }
            else
            {
                orderedPath.Add(point2);
                orderedPath.Add(point1);
                 Debug.Log($"[{gameObject.name} GrindPath] Added first edge points: {point2}, then {point1}", this); // Log added points
            }
            
            remainingEdges.Remove(firstEdge);

             Debug.Log($"[{gameObject.name} GrindPath] Edges remaining after first: {remainingEdges.Count}", this);

            // Iteratively find connected edges
            int maxIterations = remainingEdges.Count * 2 + 5; // Safety to prevent infinite loop
            int iteration = 0;
            float connectionTolerance = 0.01f; // How close points need to be to connect
            
            while (remainingEdges.Count > 0 && iteration++ < maxIterations)
            {
                Vector3 lastPoint = orderedPath[orderedPath.Count - 1];
                bool foundConnectionToEnd = false;
                (Vector3, Vector3) connectedEdgeToEnd = default;
                float bestDistanceToEnd = connectionTolerance; 
                bool connectToEndItem1 = false;
                
                // --- Find edge closest to the END of the current path --- 
                foreach (var edge in remainingEdges)
                {
                    float dist1 = Vector3.Distance(lastPoint, edge.Item1);
                    float dist2 = Vector3.Distance(lastPoint, edge.Item2);
                    
                    if (dist1 < bestDistanceToEnd)
                    {
                        bestDistanceToEnd = dist1;
                        connectedEdgeToEnd = edge;
                        connectToEndItem1 = true;
                        foundConnectionToEnd = true;
                    }
                    if (dist2 < bestDistanceToEnd)
                    {
                        bestDistanceToEnd = dist2;
                        connectedEdgeToEnd = edge;
                        connectToEndItem1 = false;
                        foundConnectionToEnd = true;
                    }
                }
                
                if (foundConnectionToEnd)
                {
                    Vector3 pointToAdd;
                    if (connectToEndItem1)
                    {
                        pointToAdd = connectedEdgeToEnd.Item2; // Add the second point
                    }
                    else
                    {
                         pointToAdd = connectedEdgeToEnd.Item1; // Add the first point
                    }
                    // Avoid adding a point that's essentially identical to the last one
                    if (Vector3.Distance(lastPoint, pointToAdd) > connectionTolerance * 0.5f)
                    {
                        orderedPath.Add(pointToAdd);
                    }
                    remainingEdges.Remove(connectedEdgeToEnd);
                    Debug.Log($"[{gameObject.name} OrdEdges Loop {iteration}] Connected edge {connectedEdgeToEnd} to END. Added point {pointToAdd}. Edges left: {remainingEdges.Count}");
                }
                else
                {
                    // --- If no connection to END found, check START (potential loop or disconnected segment) --- 
                    Vector3 firstPoint = orderedPath[0];
                    bool foundConnectionToStart = false;
                    (Vector3, Vector3) connectedEdgeToStart = default;
                    float bestDistanceToStart = connectionTolerance;
                    bool connectToStartItem1 = false;
                    
                    foreach (var edge in remainingEdges.ToList()) // Iterate copy if modifying
                    {
                        float dist1 = Vector3.Distance(firstPoint, edge.Item1);
                        float dist2 = Vector3.Distance(firstPoint, edge.Item2);
                        
                        if (dist1 < bestDistanceToStart)
                        {
                            bestDistanceToStart = dist1;
                            connectedEdgeToStart = edge;
                            connectToStartItem1 = true;
                            foundConnectionToStart = true;
                        }
                        if (dist2 < bestDistanceToStart)
                        {
                            bestDistanceToStart = dist2;
                            connectedEdgeToStart = edge;
                            connectToStartItem1 = false;
                            foundConnectionToStart = true;
                        }
                    }
                       
                    if (foundConnectionToStart)
                    {
                        Vector3 pointToInsert;
                        if (connectToStartItem1)
                        {
                            pointToInsert = connectedEdgeToStart.Item2; // Insert the second point
                        }
                        else
                        {
                            pointToInsert = connectedEdgeToStart.Item1; // Insert the first point
                        }
                        // Avoid inserting a point that's essentially identical to the first one
                        if (Vector3.Distance(firstPoint, pointToInsert) > connectionTolerance * 0.5f)
                        {
                            orderedPath.Insert(0, pointToInsert);
                        }
                        remainingEdges.Remove(connectedEdgeToStart);
                        Debug.Log($"[{gameObject.name} OrdEdges Loop {iteration}] Connected edge {connectedEdgeToStart} to START. Inserted point {pointToInsert}. Edges left: {remainingEdges.Count}");
                    }
                    else
                    {
                        // No connection found to either end
                        Debug.LogWarning($"[{gameObject.name} OrdEdges Loop {iteration}] No connection found to end ({lastPoint}) or start ({firstPoint}). Breaking loop. Edges left: {remainingEdges.Count}", this);
                        break; // Exit loop
                    }
                }
            }
            
            if (iteration >= maxIterations)
            {
                Debug.LogWarning($"[{gameObject.name} GrindPath] Ordering loop reached max iterations ({maxIterations}). Path might be incomplete or contain disconnected segments.", this);
            }

            Debug.Log($"[{gameObject.name} GrindPath] Ordered path raw count: {orderedPath.Count}. Primary axis: {primaryAxis}", this);
            if(orderedPath.Count > 1)
            {
                Debug.Log($"[{gameObject.name} GrindPath] First raw local point: {orderedPath[0]}, Last raw local point: {orderedPath[orderedPath.Count - 1]}", this);
            }

            // Create a refined path with more points for better grinding
            List<Vector3> refinedPath = new List<Vector3>();
            
            // Parameters for subdivision
            int subdivisionsPerSegment = 3; // Add points between each original vertex
            float minSegmentLength = 0.1f; // Only subdivide segments longer than this
            
            if (orderedPath.Count < 2)
            {
                Debug.LogWarning($"[{gameObject.name} GrindPath] Raw ordered path has less than 2 points ({orderedPath.Count}). Skipping refinement.", this);
                return orderedPath; // Return the raw (likely empty or single point) path
            }
               
            refinedPath.Add(orderedPath[0]); // Add first point
            
            // Subdivide each segment to create more points
            for (int i = 0; i < orderedPath.Count - 1; i++)
            {
                Vector3 start = orderedPath[i];
                Vector3 end = orderedPath[i + 1];
                float segmentLength = Vector3.Distance(start, end);
                
                // Only subdivide longer segments
                if (segmentLength > minSegmentLength)
                {
                    for (int j = 1; j <= subdivisionsPerSegment; j++)
                    {
                        float t = j / (float)(subdivisionsPerSegment + 1);
                        Vector3 point = Vector3.Lerp(start, end, t);
                        refinedPath.Add(point);
                    }
                }
                
                // Always add the endpoint of the current segment
                refinedPath.Add(end); 
            }
            
            Debug.Log($"[{gameObject.name} GrindPath] Refined path from {orderedPath.Count} to {refinedPath.Count} points", this); // Added Name
            return refinedPath; 
        }
    }
} 