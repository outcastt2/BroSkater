using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace BroSkater.Rails
{
    public static class MultiGrindPathExtractor
    {
        // Tolerance for considering vertex positions equal
        private const float POSITION_EQUALITY_TOLERANCE = 0.001f;

        // Represents an edge by its vertex indices, canonicalized (v1 < v2)
        private struct EdgeByIndex : System.IEquatable<EdgeByIndex>
        {
            public readonly int v1;
            public readonly int v2;

            public EdgeByIndex(int vertex1, int vertex2)
            {
                if (vertex1 < vertex2)
                {
                    v1 = vertex1;
                    v2 = vertex2;
                }
                else
                {
                    v1 = vertex2;
                    v2 = vertex1;
                }
            }

            public bool Equals(EdgeByIndex other) => v1 == other.v1 && v2 == other.v2;
            public override bool Equals(object obj) => obj is EdgeByIndex other && Equals(other);
            public override int GetHashCode() => v1.GetHashCode() ^ (v2.GetHashCode() << 2);
        }

        /// <summary>
        /// Extracts multiple continuous paths from a mesh based on vertex alpha color.
        /// Vertices are considered part of the path if their color.a >= colorWeightThreshold.
        /// </summary>
        /// <param name="mesh">The mesh to extract paths from.</param>
        /// <param name="colorWeightThreshold">The minimum alpha value for a vertex to be included.</param>
        /// <param name="logContext">Optional GameObject for context in debug logs.</param>
        /// <returns>A list of paths, where each path is a list of vertex positions in local space.</returns>
        public static List<List<Vector3>> ExtractPaths(Mesh mesh, float colorWeightThreshold, Object logContext = null)
        {
            if (mesh == null || !mesh.isReadable)
            {
                Debug.LogError($"[{logContext?.name}] Mesh is null or not readable!", logContext);
                return new List<List<Vector3>>();
            }

            Vector3[] vertices = mesh.vertices;
            Color[] colors = mesh.colors;
            int[] triangles = mesh.triangles;

            if (colors == null || colors.Length != vertices.Length)
            {
                Debug.LogError($"[{logContext?.name}] Vertex color data missing or mismatched! Cannot extract paths.", logContext);
                return new List<List<Vector3>>();
            }

            // 1. Find all edges where both vertices meet the threshold
            var potentialEdges = FindPotentialGrindEdges(vertices, colors, triangles, colorWeightThreshold, logContext);

            if (potentialEdges.Count == 0)
            {
                Debug.LogWarning($"[{logContext?.name}] No edges found meeting the color threshold ({colorWeightThreshold}).", logContext);
                return new List<List<Vector3>>();
            }

            // 2. Build adjacency list for path finding
            var adjacency = BuildAdjacencyList(potentialEdges, vertices);

            // 3. Find all distinct paths using graph traversal
            var paths = FindAllPaths(adjacency, logContext);

            return paths;
        }

        private static HashSet<(Vector3, Vector3)> FindPotentialGrindEdges(Vector3[] vertices, Color[] colors, int[] triangles, float threshold, Object logContext)
        {
            HashSet<EdgeByIndex> processedEdgesByIndex = new HashSet<EdgeByIndex>();
             // Using a HashSet<(Vector3, Vector3)> to store unique spatial edges directly
            HashSet<(Vector3, Vector3)> uniqueSpatialEdges = new HashSet<(Vector3, Vector3)>();
            int grindVertCount = 0;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int idx1 = triangles[i];
                int idx2 = triangles[i + 1];
                int idx3 = triangles[i + 2];

                CheckAndAddEdge(idx1, idx2, vertices, colors, threshold, processedEdgesByIndex, uniqueSpatialEdges, ref grindVertCount);
                CheckAndAddEdge(idx2, idx3, vertices, colors, threshold, processedEdgesByIndex, uniqueSpatialEdges, ref grindVertCount);
                CheckAndAddEdge(idx3, idx1, vertices, colors, threshold, processedEdgesByIndex, uniqueSpatialEdges, ref grindVertCount);
            }

            Debug.Log($"[{logContext?.name}] Found {grindVertCount} verts meeting threshold, {uniqueSpatialEdges.Count} unique spatial grind edges.", logContext);
            return uniqueSpatialEdges;
        }

         private static void CheckAndAddEdge(int vIdx1, int vIdx2, Vector3[] vertices, Color[] colors, float threshold,
                                     HashSet<EdgeByIndex> processedIndices, HashSet<(Vector3, Vector3)> uniqueSpatialEdges, ref int grindVertCount)
        {
            if (vIdx1 >= colors.Length || vIdx2 >= colors.Length) return; // Bounds check

            bool v1Grind = colors[vIdx1].a >= threshold;
            bool v2Grind = colors[vIdx2].a >= threshold;

            if (v1Grind) grindVertCount++; // Count verts meeting threshold (might double count, but ok for logging)

            if (v1Grind && v2Grind)
            {
                var edgeIndices = new EdgeByIndex(vIdx1, vIdx2);
                if (processedIndices.Add(edgeIndices)) // If this index pair hasn't been processed
                {
                    // Add the actual spatial edge (using vertices)
                    Vector3 p1 = vertices[vIdx1];
                    Vector3 p2 = vertices[vIdx2];
                    // Canonicalize based on position hashcode for the HashSet
                    var spatialEdge = CanonicalizeSpatialEdge(p1, p2);
                    uniqueSpatialEdges.Add(spatialEdge);
                }
            }
        }

        // Use HashCode for a slightly cheaper canonicalization suitable for HashSet
        private static (Vector3, Vector3) CanonicalizeSpatialEdge(Vector3 p1, Vector3 p2)
        {
            return p1.GetHashCode() < p2.GetHashCode() ? (p1, p2) : (p2, p1);
        }


        // Use Dictionary for adjacency list: Key = point, Value = list of adjacent points
        private static Dictionary<Vector3, List<Vector3>> BuildAdjacencyList(HashSet<(Vector3, Vector3)> edges, Vector3[] vertices)
        {
             var adjacency = new Dictionary<Vector3, List<Vector3>>(new Vector3EqualityComparer(POSITION_EQUALITY_TOLERANCE));

            foreach (var edge in edges)
            {
                if (!adjacency.ContainsKey(edge.Item1)) adjacency[edge.Item1] = new List<Vector3>();
                if (!adjacency.ContainsKey(edge.Item2)) adjacency[edge.Item2] = new List<Vector3>();

                // Add connections in both directions
                if (!adjacency[edge.Item1].Contains(edge.Item2, new Vector3EqualityComparer(POSITION_EQUALITY_TOLERANCE)))
                    adjacency[edge.Item1].Add(edge.Item2);
                if (!adjacency[edge.Item2].Contains(edge.Item1, new Vector3EqualityComparer(POSITION_EQUALITY_TOLERANCE)))
                    adjacency[edge.Item2].Add(edge.Item1);
            }
            return adjacency;
        }

        private static List<List<Vector3>> FindAllPaths(Dictionary<Vector3, List<Vector3>> adjacency, Object logContext)
        {
            var allPaths = new List<List<Vector3>>();
            var visitedPoints = new HashSet<Vector3>(new Vector3EqualityComparer(POSITION_EQUALITY_TOLERANCE));

            // Find all terminal points (nodes with only one connection) to start paths from
            var startPoints = adjacency.Where(kvp => kvp.Value.Count == 1).Select(kvp => kvp.Key).ToList();

            // Also consider points in cycles (nodes with > 1 connection that haven't been visited)
            // Add all points initially, traversal will handle visited status
             var potentialCyclePoints = adjacency.Keys.ToList();


            // Prioritize starting from defined endpoints
            foreach (var startPoint in startPoints)
            {
                if (visitedPoints.Contains(startPoint)) continue;

                var path = FollowPath(startPoint, adjacency, visitedPoints);
                if (path.Count >= 2)
                {
                    allPaths.Add(path);
                }
            }

            // Handle cycles or paths missed (e.g., if a path starts mid-segment due to mesh issues)
            foreach (var potentialStart in potentialCyclePoints)
            {
                 if (visitedPoints.Contains(potentialStart)) continue;

                 // Check if this point belongs to a purely cyclical structure or a path fragment
                var path = FollowPath(potentialStart, adjacency, visitedPoints);
                if (path.Count >= 2)
                {
                     // Detect if it's a loop by checking proximity of start/end
                     bool isLoop = Vector3.Distance(path[0], path[path.Count - 1]) < POSITION_EQUALITY_TOLERANCE * 5; // Increased tolerance slightly for loops
                     if (isLoop && path.Count > 2)
                     {
                         // Optional: Add start point again to explicitly close the loop in the list
                         // path.Add(path[0]);
                          Debug.Log($"[{logContext?.name}] Detected closed loop path with {path.Count} points.", logContext);
                     }
                     else if(isLoop && path.Count <=2)
                     {
                        Debug.LogWarning($"[{logContext?.name}] Detected loop-like structure with only {path.Count} points. Ignoring.", logContext);
                        continue; // Ignore very short loops
                     }
                    allPaths.Add(path);
                }
            }


            Debug.Log($"[{logContext?.name}] Extracted {allPaths.Count} distinct paths.", logContext);
            return allPaths;
        }

        private static List<Vector3> FollowPath(Vector3 startPoint, Dictionary<Vector3, List<Vector3>> adjacency, HashSet<Vector3> visitedPoints)
        {
            var path = new List<Vector3>();
            var currentPoint = startPoint;
            Vector3 previousPoint = default; // Keep track to avoid immediately going back

            // Use a queue for BFS-like exploration, but prioritize extending the current line
            Queue<Vector3> pointsToProcess = new Queue<Vector3>();
            pointsToProcess.Enqueue(startPoint);

            while(pointsToProcess.Count > 0)
            {
                currentPoint = pointsToProcess.Dequeue();

                // Ensure we haven't visited this point *as part of constructing this specific path*
                // Use a local visited set for the current path construction? No, global visited is better
                // to prevent paths from crossing back over themselves incorrectly.
                 if (visitedPoints.Contains(currentPoint))
                 {
                      // If we hit an already visited point, it might mean we've completed a loop
                      // or joined another path. Stop extending this particular path segment here.
                      if (!path.Contains(currentPoint, new Vector3EqualityComparer(POSITION_EQUALITY_TOLERANCE))) // Check if it's the start of *this* path closing a loop
                      {
                           // Only add if it's closing a loop on itself, otherwise stop.
                           if (path.Count > 1 && Vector3.Distance(currentPoint, path[0]) < POSITION_EQUALITY_TOLERANCE * 5)
                           {
                               path.Add(currentPoint); // Add the closing point
                           }
                      }
                      continue; // Don't process neighbours of an already globally visited point again
                 }


                path.Add(currentPoint);
                visitedPoints.Add(currentPoint); // Mark globally visited

                if (adjacency.TryGetValue(currentPoint, out var neighbors))
                {
                    bool extended = false;
                     foreach (var neighbor in neighbors)
                    {
                        // Simple check to prevent immediate U-turns in non-loop scenarios
                        // This might be too simplistic for complex junctions.
                         if (neighbor != previousPoint || adjacency[currentPoint].Count == 1) // Allow returning to previous if it's a dead end
                         {
                            if (!visitedPoints.Contains(neighbor)) // Don't enqueue already visited points
                            {
                                // Basic path following: prioritize the unvisited neighbor.
                                // For more complex junctions, might need smarter logic.
                                previousPoint = currentPoint; // Update previous *before* moving to neighbor
                                // Instead of direct recursion/loop, add to queue for processing
                                pointsToProcess.Enqueue(neighbor);
                                extended = true;
                                // In this simple model, we only follow one path out.
                                // If there are multiple unvisited neighbours, the BFS nature handles branching,
                                // but FollowPath is intended to get one continuous line segment.
                                // Let's break here to follow the first unvisited neighbor found.
                                // The other branches will be picked up later if they form separate paths.
                                break;
                             }
                             else if (neighbor == path[0] && path.Count > 1) // Check if neighbor closes the loop
                             {
                                 // If the only valid neighbor is the start point, add it to close the loop
                                 // Check if *any* neighbour is the start point.
                                 if (Vector3.Distance(neighbor, path[0]) < POSITION_EQUALITY_TOLERANCE * 5)
                                 {
                                     if (!path.Contains(neighbor, new Vector3EqualityComparer(POSITION_EQUALITY_TOLERANCE)))
                                     {
                                        path.Add(neighbor); // Close the loop explicitly
                                        // Don't need to enqueue startPoint again.
                                        extended = true; // Mark as extended to prevent stopping prematurely
                                        break; // Stop processing neighbors for this point once loop is closed.
                                     }
                                 }
                             }
                        }
                    }
                     // If no unvisited neighbour was found to extend the path, this path segment ends here.
                }
                // If no neighbours, path ends. Loop continues if queue isn't empty.
            }


            // Simple ordering attempt: Check if path needs reversing
            // If the start point has >1 neighbor and the end point has 1, it might be reversed.
            // This is heuristic and may not cover all cases perfectly, especially complex graphs.
            if (path.Count > 1)
            {
                int startNeighbors = adjacency.ContainsKey(path[0]) ? adjacency[path[0]].Count : 0;
                int endNeighbors = adjacency.ContainsKey(path[path.Count - 1]) ? adjacency[path[path.Count - 1]].Count : 0;
                bool startIsEndpoint = startNeighbors == 1;
                bool endIsEndpoint = endNeighbors == 1;
                bool startIsLoopConnection = Vector3.Distance(path[0], path[path.Count - 1]) < POSITION_EQUALITY_TOLERANCE * 5;


                // If path doesn't start at a natural endpoint but ends at one, reverse it.
                // Don't reverse if it's a closed loop.
                if (!startIsEndpoint && endIsEndpoint && !startIsLoopConnection)
                {
                    path.Reverse();
                }
            }


            return path;
        }
    }


    // Custom comparer for Vector3 using a tolerance
    public class Vector3EqualityComparer : IEqualityComparer<Vector3>
    {
        private readonly float _tolerance;

        public Vector3EqualityComparer(float tolerance)
        {
            _tolerance = tolerance;
        }

        public bool Equals(Vector3 v1, Vector3 v2)
        {
            return Vector3.Distance(v1, v2) < _tolerance;
        }

        public int GetHashCode(Vector3 obj)
        {
            // Use a grid-based hash code generation that's tolerant to small position differences
            // Adjust the multiplier based on expected coordinate ranges and desired tolerance granularity
            int hashX = Mathf.RoundToInt(obj.x / _tolerance);
            int hashY = Mathf.RoundToInt(obj.y / _tolerance);
            int hashZ = Mathf.RoundToInt(obj.z / _tolerance);
            // Combine hashes (simple combination)
            int hash = 17;
            hash = hash * 31 + hashX;
            hash = hash * 31 + hashY;
            hash = hash * 31 + hashZ;
            return hash;
        }
    }
} 