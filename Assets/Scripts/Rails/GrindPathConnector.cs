using UnityEngine;
using BroSkater.Rails;

public class GrindPathConnector : MonoBehaviour
{
    public GrindPath parentPath;
    public GrindPath.GrindPoint startPoint;
    public GrindPath.GrindPoint endPoint;
    
    // Cache the direction vector and midpoint for faster calculations
    [HideInInspector] public Vector3 direction;
    [HideInInspector] public Vector3 midpoint;
    [HideInInspector] public float length;
    
    public void Initialize(GrindPath path, GrindPath.GrindPoint start, GrindPath.GrindPoint end)
    {
        parentPath = path;
        startPoint = start;
        endPoint = end;
        
        // Cache these values for faster access during gameplay
        direction = (endPoint.position - startPoint.position).normalized;
        midpoint = (startPoint.position + endPoint.position) * 0.5f;
        length = Vector3.Distance(startPoint.position, endPoint.position);
        
        // Log details about this connector
        Debug.Log($"GrindPathConnector initialized: Length={length:F2}m, Start={startPoint.distanceAlongPath:F2}, End={endPoint.distanceAlongPath:F2}");
    }
    
    // Get the closest point along this connector segment
    public bool GetClosestPoint(Vector3 position, out Vector3 closestPoint, out Vector3 tangent, out float distanceAlongConnector)
    {
        // Calculate the closest point on the line segment
        Vector3 lineStart = startPoint.position;
        Vector3 lineEnd = endPoint.position;
        Vector3 lineDir = direction;
        
        // Project the position onto the line
        Vector3 lineToPoint = position - lineStart;
        float dotProduct = Vector3.Dot(lineToPoint, lineDir);
        
        // Clamp to segment
        dotProduct = Mathf.Clamp(dotProduct, 0f, length);
        
        // Calculate closest point
        closestPoint = lineStart + lineDir * dotProduct;
        
        // Calculate interpolated tangent based on position
        float t = dotProduct / length;
        tangent = Vector3.Slerp(startPoint.tangent, endPoint.tangent, t).normalized;
        
        // Distance along connector
        distanceAlongConnector = dotProduct;
        
        return true;
    }
    
    // Get a point at a specific distance along the connector
    public bool GetPointAtDistance(float distance, out Vector3 point, out Vector3 tangent)
    {
        // Ensure distance is within bounds
        distance = Mathf.Clamp(distance, 0f, length);
        
        // Calculate t parameter (0 to 1) 
        float t = distance / length;
        
        // Interpolate position
        point = Vector3.Lerp(startPoint.position, endPoint.position, t);
        
        // Interpolate tangent
        tangent = Vector3.Slerp(startPoint.tangent, endPoint.tangent, t).normalized;
        
        return true;
    }
    
    // Calculate the distance along the parent path for a point along this connector
    public float CalculatePathDistance(float distanceAlongConnector)
    {
        // Convert connector-local distance to path-global distance
        float t = Mathf.Clamp01(distanceAlongConnector / length);
        return Mathf.Lerp(startPoint.distanceAlongPath, endPoint.distanceAlongPath, t);
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