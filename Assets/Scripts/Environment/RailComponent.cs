using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BroSkater.Environment
{
    /// <summary>
    /// Defines a rail or ledge that can be grinded on
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class RailComponent : MonoBehaviour
    {
        [Header("Rail Properties")]
        [Tooltip("Is this a rail (cylinder) or a ledge (edge of platform)?")]
        [SerializeField] private bool isLedge = false;
        
        [Tooltip("Is this a 'bad ledge' that's harder to grind?")]
        [SerializeField] private bool isBadLedge = false;
        
        [Tooltip("The starting point of the rail")]
        [SerializeField] private Transform startPoint;
        
        [Tooltip("The ending point of the rail")]
        [SerializeField] private Transform endPoint;
        
        [Tooltip("The friction of the rail - higher values mean you slow down faster")]
        [Range(0f, 1f)]
        [SerializeField] private float railFriction = 0.1f;
        
        [Header("Debugging")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private Color gizmoColor = Color.blue;
        
        private Collider railCollider;
        
        private void Awake()
        {
            railCollider = GetComponent<Collider>();
            
            // Make sure the collider is a trigger
            railCollider.isTrigger = true;
            
            // If start/end points aren't set, create them
            if (startPoint == null)
            {
                GameObject start = new GameObject("RailStart");
                start.transform.parent = transform;
                start.transform.localPosition = Vector3.zero - transform.forward * GetRailLength() * 0.5f;
                startPoint = start.transform;
            }
            
            if (endPoint == null)
            {
                GameObject end = new GameObject("RailEnd");
                end.transform.parent = transform;
                end.transform.localPosition = Vector3.zero + transform.forward * GetRailLength() * 0.5f;
                endPoint = end.transform;
            }
        }
        
        private void Start()
        {
            // Set layer to "Rail" layer if it exists
            if (LayerMask.NameToLayer("Rail") != -1)
            {
                gameObject.layer = LayerMask.NameToLayer("Rail");
            }
            
            // If this is a ledge, tag it appropriately
            if (isLedge)
            {
                if (isBadLedge)
                {
                    gameObject.tag = "BadLedge";
                }
                else
                {
                    gameObject.tag = "Ledge";
                }
            }
            else
            {
                gameObject.tag = "Rail";
            }
        }
        
        // Get the closest point on the rail to a given world position
        public Vector3 GetClosestPointOnRail(Vector3 worldPosition)
        {
            // Get rail start and end in world space
            Vector3 railStart = startPoint.position;
            Vector3 railEnd = endPoint.position;
            
            // Create a line segment for the rail
            Vector3 railDirection = (railEnd - railStart).normalized;
            float railLength = Vector3.Distance(railStart, railEnd);
            
            // Find the closest point on the rail line segment
            Vector3 pointToRailStart = worldPosition - railStart;
            float dotProduct = Vector3.Dot(pointToRailStart, railDirection);
            
            // Clamp to rail endpoints
            dotProduct = Mathf.Clamp(dotProduct, 0f, railLength);
            
            // Calculate the closest point
            return railStart + railDirection * dotProduct;
        }
        
        // Get the progress along the rail (0 = start, 1 = end)
        public float GetRailProgress(Vector3 worldPosition)
        {
            Vector3 closestPoint = GetClosestPointOnRail(worldPosition);
            float distanceFromStart = Vector3.Distance(startPoint.position, closestPoint);
            float railLength = GetRailLength();
            
            return distanceFromStart / railLength;
        }
        
        // Get the length of the rail
        public float GetRailLength()
        {
            return Vector3.Distance(startPoint.position, endPoint.position);
        }
        
        // Get the direction of the rail at a given point
        public Vector3 GetRailDirection(Vector3 worldPosition)
        {
            return (endPoint.position - startPoint.position).normalized;
        }
        
        // Get whether this is a ledge
        public bool IsLedge()
        {
            return isLedge;
        }
        
        // Get whether this is a bad ledge
        public bool IsBadLedge()
        {
            return isBadLedge;
        }
        
        // Get the rail friction
        public float GetRailFriction()
        {
            return railFriction;
        }
        
        // Visualize the rail in the editor
        private void OnDrawGizmos()
        {
            if (!showGizmos) return;
            
            Gizmos.color = gizmoColor;
            
            Vector3 start = startPoint != null ? startPoint.position : transform.position - transform.forward * 2f;
            Vector3 end = endPoint != null ? endPoint.position : transform.position + transform.forward * 2f;
            
            // Draw line for the rail
            Gizmos.DrawLine(start, end);
            
            // Draw spheres at start and end
            Gizmos.DrawSphere(start, 0.1f);
            Gizmos.DrawSphere(end, 0.1f);
            
            // Draw rail direction
            Gizmos.color = Color.green;
            Vector3 center = (start + end) * 0.5f;
            Gizmos.DrawRay(center, (end - start).normalized * 0.5f);
        }
    }
} 