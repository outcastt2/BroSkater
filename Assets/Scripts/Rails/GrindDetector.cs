using UnityEngine;
using BroSkater.Player;
using BroSkater.Player.StateMachine;

namespace BroSkater.Rails
{
    /// <summary>
    /// Detects when the player is near a grindable rail and handles entry/exit
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class GrindDetector : MonoBehaviour
    {
        [Header("Detection Settings")]
        [SerializeField] private float detectDistance = 1.5f;
        [SerializeField] private float entryAngleThreshold = 45f;
        [SerializeField] private LayerMask railLayerMask;
        [SerializeField] private Transform raycastOrigin;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private Color rayColor = Color.cyan;
        [SerializeField] private Color hitColor = Color.green;
        [SerializeField] private Color noHitColor = Color.red;
        
        // Component references
        private PlayerController playerController;
        private PlayerStateMachine stateMachine;
        
        // Current rail being detected/used
        private SplineGrindPath currentRail;
        private float distanceAlongRail;
        private Vector3 railDirection;
        private bool isNearRail;
        private bool wasGrindInputActive;
        private RaycastHit currentHit;
        
        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            stateMachine = GetComponent<PlayerStateMachine>();
            
            if (raycastOrigin == null)
            {
                raycastOrigin = transform;
                Debug.LogWarning("No raycast origin set for GrindDetector. Using player transform.", this);
            }
        }
        
        private void Update()
        {
            // Only detect rails if not already grinding
            if (!playerController.IsGrinding)
            {
                DetectRails();
                
                // Check for grind button press when near a rail
                if (isNearRail && playerController.InputHandler.GrindInputDown && !wasGrindInputActive)
                {
                    // Comment out this line:
                    // AttemptGrindEntry();
                    AttemptGrindEntry(); // Uncommented this line
                }
            }
            else
            {
                // Already grinding, check for exit conditions
                if (playerController.InputHandler.JumpInputDown)
                {
                    ExitGrind(true); // Jump off rail
                }
            }
            
            // Track input state for next frame
            wasGrindInputActive = playerController.InputHandler.GrindInputDown || playerController.InputHandler.GrindInputHeld;
        }
        
        private void DetectRails()
        {
            RaycastHit hit;
            Vector3 rayOrigin = raycastOrigin.position;
            Vector3 rayDirection = Vector3.down;
            isNearRail = false; // Reset flag each frame
            currentRail = null; // Reset rail reference
            
            // Draw the ray for debugging
            if (showDebugGizmos) Debug.DrawRay(rayOrigin, rayDirection * detectDistance, Color.magenta);
            
            // Cast a ray down to detect rails
            bool hitRail = Physics.Raycast(rayOrigin, rayDirection, out hit, detectDistance, railLayerMask);
            
            if (hitRail)
            {
                Debug.Log($"[{gameObject.name} GrindDetector] Raycast HIT: {hit.collider.name} on layer {LayerMask.LayerToName(hit.collider.gameObject.layer)}", hit.collider);
                // Try to get SplineGrindPath component from hit object
                SplineGrindPath rail = hit.collider.GetComponentInParent<SplineGrindPath>();
                Debug.Log($"[{gameObject.name} GrindDetector] Found SplineGrindPath parent? {(rail != null)}", rail);
                
                if (rail != null)
                {
                    currentRail = rail;
                    isNearRail = true;
                    currentHit = hit;
                    
                    // Get closest point on the rail
                    currentRail.GetNearestPoint(hit.point, out Vector3 closestPoint, out Vector3 tangent, out distanceAlongRail);
                    railDirection = tangent;
                }
                else
                {
                    isNearRail = false;
                    currentRail = null;
                }
            }
            else
            {
                isNearRail = false;
                currentRail = null;
            }
        }
        
        private void AttemptGrindEntry()
        {
            if (!isNearRail || currentRail == null)
            {
                Debug.Log($"[{gameObject.name} GrindDetector] AttemptGrindEntry failed: Not near rail or currentRail is null.");
                return;
            }
                
            // Check if player is moving fast enough and in a compatible direction
            Vector3 playerVelocity = playerController.Velocity;
            float playerSpeed = playerVelocity.magnitude;
            
            // Minimum speed check
            if (playerSpeed < 2.0f)
            {
                Debug.Log($"[{gameObject.name} GrindDetector] AttemptGrindEntry failed: Not moving fast enough ({playerSpeed:F2} < 2.0)");
                return;
            }
            
            // Check angle between player direction and rail direction
            Vector3 playerDirection = playerVelocity.normalized;
            float angle = Vector3.Angle(playerDirection, railDirection);
            Debug.Log($"[{gameObject.name} GrindDetector] Approach angle: {angle:F2}, Threshold: {entryAngleThreshold}");
            
            if (angle > entryAngleThreshold && angle < (180f - entryAngleThreshold))
            {
                Debug.Log($"[{gameObject.name} GrindDetector] Bad approach angle for grind: {angle}");
                return;
            }
            
            // Determine if we should flip the rail direction based on player approach
            bool reverseRailDirection = (Vector3.Dot(playerDirection, railDirection) < 0);
            
            // Start grinding!
            BeginGrind(reverseRailDirection);
        }
        
        private void BeginGrind(bool reverseDirection)
        {
            if (currentRail == null)
                return;
                
            // Get entry point based on the distance calculated in DetectRails
            currentRail.GetPointAtDistance(distanceAlongRail, out Vector3 entryPosition, out Vector3 tangent, out bool _);
            
            // Determine final forward direction based on player velocity approach
            Vector3 forwardDir = reverseDirection ? -tangent.normalized : tangent.normalized;
            
            // --- Snap Player ---
            // Snap position slightly above the calculated entry point on the rail spline
            float boardHeightOffset = 0.1f; // Match PlayerGrindingState's offset
            Vector3 newPosition = entryPosition + Vector3.up * boardHeightOffset; 
            transform.position = newPosition;
            
            // Snap rotation to align with the grind direction, keeping player upright
            transform.rotation = Quaternion.LookRotation(forwardDir, Vector3.up);
            
            // --- Set Initial Velocity ---
            // Project current velocity onto the grind direction and apply boost
            float entrySpeed = Mathf.Max(
                Mathf.Abs(Vector3.Dot(playerController.Velocity, forwardDir)), 
                2.0f // Ensure minimum speed if projection is low/zero
            );
            float boostedSpeed = entrySpeed * 1.2f; // Apply boost (sync with GrindParameters if possible)
            playerController.Velocity = forwardDir * boostedSpeed;
            
            // --- Notify State Machine ---
            if (stateMachine != null && stateMachine.GrindingState != null)
            {
                // Pass rail info to the grinding state: 
                // - SplineGrindPath (currentRail)
                // - Calculated entry point (entryPosition)
                // - Calculated forward direction (forwardDir)
                // - Distance along spline (distanceAlongRail - calculated in DetectRails)
                // - Hit normal (currentHit.normal)
                stateMachine.GrindingState.SetRailInfo(currentRail, entryPosition, forwardDir, distanceAlongRail, currentHit.normal);
                // Transition state *after* setting info and snapping
                stateMachine.ChangeState(stateMachine.GrindingState); 
            }
            else
            {
                 Debug.LogError("StateMachine or GrindingState not found in GrindDetector!");
                 // Optionally handle this error, e.g., by preventing the grind
                 return; // Don't proceed if state machine is not set up
            }

            Debug.Log($"Grind Started on {currentRail.name}. Direction: {(reverseDirection ? "Reverse":"Forward")}, EntryDist: {distanceAlongRail:F2}");
        }
        
        /// <summary>
        /// Exit the current grind
        /// </summary>
        /// <param name="isJumping">True if player is jumping off the rail</param>
        public void ExitGrind(bool isJumping)
        {
            if (!playerController.IsGrinding)
                return;
                
            // Get current velocity
            Vector3 currentVelocity = playerController.Velocity;
            
            if (isJumping)
            {
                // Apply upward force for jumping off rail
                float jumpForce = 5f;
                Vector3 jumpVelocity = currentVelocity + Vector3.up * jumpForce;
                
                // Apply directional adjustments based on input
                Vector2 moveInput = playerController.InputHandler.MoveInput;
                if (moveInput.magnitude > 0.1f)
                {
                    // Convert input to world direction relative to camera
                    Vector3 inputDir = new Vector3(moveInput.x, 0, moveInput.y).normalized;
                    
                    // Rotate input direction based on camera's forward
                    UnityEngine.Camera mainCam = UnityEngine.Camera.main;
                    if (mainCam != null)
                    {
                        Vector3 camForward = mainCam.transform.forward;
                        camForward.y = 0;
                        camForward.Normalize();
                        
                        Quaternion camRotation = Quaternion.LookRotation(camForward);
                        inputDir = camRotation * inputDir;
                    }
                    
                    // Add directional adjustment
                    jumpVelocity += inputDir * 2f;
                }
                
                // Apply new velocity
                playerController.Velocity = jumpVelocity;
            }
            
            // Reset rail references
            currentRail = null;
            isNearRail = false;
            
            // Notify player controller
            playerController.OnEndGrind();
        }
        
        // Add this method
        public void ResetGrindAttempt()
        {
            // Reset any logic that prevents immediate re-grinding
            // For example, if there's a cooldown or specific flag:
            // wasGrindInputActive = false; // Reset input tracking if needed
            // Or simply allow DetectRails to run again freely
            Debug.Log("Grind attempt reset.");
        }
        
        private void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;
            
            // Draw detection ray
            if (raycastOrigin != null)
            {
                Vector3 rayStart = raycastOrigin.position;
                Vector3 rayEnd = rayStart + Vector3.down * detectDistance;
                
                Gizmos.color = isNearRail ? hitColor : noHitColor;
                Gizmos.DrawLine(rayStart, rayEnd);
                
                if (isNearRail && currentRail != null)
                {
                    // Draw sphere at closest point on rail
                    currentRail.GetPointAtDistance(distanceAlongRail, out Vector3 pos, out _, out bool _);
                    Gizmos.DrawSphere(pos, 0.1f);
                    
                    // Draw rail direction
                    Gizmos.color = rayColor;
                    Gizmos.DrawLine(pos, pos + railDirection * 0.5f);
                }
            }
        }
    }
} 
