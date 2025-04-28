using UnityEngine;
using BroSkater.Player;
using BroSkater.Rails;

namespace BroSkater.Rails
{
    /// <summary>
    /// Component responsible for managing the grind state of the player
    /// </summary>
    public class GrindState : MonoBehaviour
    {
        [Header("Grind Settings")]
        [SerializeField] private float maxSnapDistance = 2.0f;
        [SerializeField] private float minGrindSpeed = 2.0f;
        [SerializeField] private float maxGrindSpeed = 20.0f;
        [SerializeField] private float grindAcceleration = 2.5f;
        [SerializeField] private float grindDeceleration = 1.0f;
        [SerializeField] private float exitJumpForce = 10.0f;
        [SerializeField] private float balanceDecayRate = 0.1f;
        [SerializeField] private float balanceRecoveryRate = 0.05f;

        [Header("Debug")]
        [SerializeField] private bool showDebugVisuals = true;
        
        // Dependencies
        private PlayerController playerController;
        private Rigidbody rb;
        
        // Current grind state
        private bool isGrinding = false;
        private SplineGrindPath currentRail;
        private float distanceAlongRail = 0f;
        private float railDirection = 1f; // 1 for forward, -1 for backward
        private float grindSpeed = 0f;
        private float balanceAmount = 1.0f; // 0 = failed, 1 = perfect balance
        
        // Methods for external components to query
        public bool IsGrinding() => isGrinding;
        public float GetMaxSnapDistance() => maxSnapDistance;
        public float GetBalanceAmount() => balanceAmount;

        // Method to check if player can initiate a grind
        public bool CanGrindOnRail()
        {
            // Can only grind if:
            // - Not already grinding
            // - In the air
            // - Has enough speed
            return !isGrinding && 
                   !playerController.IsGrounded &&
                   rb.linearVelocity.magnitude > minGrindSpeed;
        }
        
        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            rb = GetComponent<Rigidbody>();
            
            if (playerController == null)
            {
                Debug.LogError("GrindState requires a PlayerController component on the same GameObject");
                enabled = false;
            }
        }
        
        private void Update()
        {
            if (!isGrinding) return;
            
            // Handle grind input
            HandleGrindInput();
            
            // Update balance
            UpdateBalance();
            
            // Check for grind exit conditions
            CheckGrindExit();
        }
        
        private void FixedUpdate()
        {
            if (!isGrinding) return;
            
            // Move along rail
            MoveAlongRail();
        }
        
        // Called by GrindDetector to start grinding
        public void InitiateGrind(SplineGrindPath rail, GrindPath.GrindPoint entryPoint)
        {
            // Set up grind variables
            isGrinding = true;
            currentRail = rail;
            balanceAmount = 1.0f;
            
            // Determine initial direction along rail based on player velocity and rail direction
            Vector3 playerVelocity = rb.linearVelocity.normalized;
            rail.GetPointAtDistance(entryPoint.distanceAlongPath, out Vector3 point, out Vector3 railDirection, out bool _);
            float dotProduct = Vector3.Dot(playerVelocity, railDirection);
            this.railDirection = (dotProduct >= 0) ? 1f : -1f;
            
            // Set initial position and distance
            distanceAlongRail = entryPoint.distanceAlongPath;
            transform.position = entryPoint.position;
            
            // Set initial grind speed based on player velocity
            grindSpeed = Mathf.Clamp(rb.linearVelocity.magnitude, minGrindSpeed, maxGrindSpeed);
            
            // Align player rotation to rail
            AlignToRail(railDirection);
            
            // Disable physics temporarily
            rb.isKinematic = true;
            
            // Notify player controller we're grinding
            // playerController.OnBeginGrind(); // Removed - Handled by state transition logic
            
            // Display debug message
            Debug.Log($"Started grinding on {rail.name} at distance {distanceAlongRail:F2} with speed {grindSpeed:F2}");
        }
        
        private void MoveAlongRail()
        {
            if (currentRail == null) return;
            
            // Update distance along rail
            distanceAlongRail += grindSpeed * railDirection * Time.fixedDeltaTime;
            
            // Check if we reached the end of the rail
            if (distanceAlongRail >= currentRail.GetTotalLength() || distanceAlongRail <= 0)
            {
                ExitGrind(false); // Exit without jump
                return;
            }
            
            // Get position and direction at current distance
            currentRail.GetPointAtDistance(distanceAlongRail, out Vector3 position, out Vector3 tangent, out bool _);
            
            // Update player position
            transform.position = position;
            
            // Align player to rail
            AlignToRail(tangent * railDirection);
        }
        
        private void AlignToRail(Vector3 direction)
        {
            // Align player forward direction with the rail
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = targetRotation;
            }
        }
        
        private void HandleGrindInput()
        {
            // Get left/right input for balance
            float horizontalInput = Input.GetAxis("Horizontal");
            
            // Adjust balance based on input
            balanceAmount += horizontalInput * balanceRecoveryRate * Time.deltaTime;
            balanceAmount = Mathf.Clamp01(balanceAmount);
            
            // Acceleration/deceleration based on vertical input
            float verticalInput = Input.GetAxis("Vertical");
            grindSpeed += verticalInput * grindAcceleration * Time.deltaTime;
            
            // Apply natural deceleration
            grindSpeed -= grindDeceleration * Time.deltaTime;
            
            // Clamp speed
            grindSpeed = Mathf.Clamp(grindSpeed, minGrindSpeed, maxGrindSpeed);
            
            // Jump off rail if jump button pressed
            if (Input.GetButtonDown("Jump"))
            {
                ExitGrind(true);
            }
        }
        
        private void UpdateBalance()
        {
            // Natural balance decay over time
            balanceAmount -= balanceDecayRate * Time.deltaTime;
            balanceAmount = Mathf.Clamp01(balanceAmount);
            
            // If balance is completely lost, bail out
            if (balanceAmount <= 0)
            {
                ExitGrind(false);
            }
        }
        
        private void CheckGrindExit()
        {
            // Additional exit conditions can be added here
        }
        
        private void ExitGrind(bool jumped)
        {
            if (!isGrinding) return;
            
            // Re-enable physics
            rb.isKinematic = false;
            
            // Apply exit force if jumped
            if (jumped)
            {
                // Get rail direction
                currentRail.GetPointAtDistance(distanceAlongRail, out Vector3 position, out Vector3 tangent, out bool _);
                
                // Apply jump force upward and slightly in the direction of movement
                Vector3 jumpDirection = (Vector3.up * 0.8f + tangent * railDirection * 0.2f).normalized;
                rb.linearVelocity = jumpDirection * exitJumpForce;
            }
            else
            {
                // If not jumped, preserve some momentum in the rail direction
                currentRail.GetPointAtDistance(distanceAlongRail, out Vector3 position, out Vector3 tangent, out bool _);
                rb.linearVelocity = tangent * railDirection * grindSpeed * 0.8f;
            }
            
            // Reset grind state
            isGrinding = false;
            currentRail = null;
            
            // Notify player controller we're exiting grind
            playerController.OnEndGrind();
            
            Debug.Log($"Exited grind with speed {grindSpeed:F2}, jumped: {jumped}");
        }
        
        private void OnDrawGizmos()
        {
            if (!showDebugVisuals || !Application.isPlaying || !isGrinding) return;
            
            // Draw current position on rail
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(transform.position, 0.2f);
            
            // Draw rail direction
            if (currentRail != null)
            {
                currentRail.GetPointAtDistance(distanceAlongRail, out Vector3 position, out Vector3 tangent, out bool _);
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(position, tangent * railDirection * 2f);
            }
            
            // Draw balance indicator
            Gizmos.color = Color.Lerp(Color.red, Color.green, balanceAmount);
            Gizmos.DrawLine(
                transform.position + Vector3.up * 1.5f - Vector3.right * 0.5f,
                transform.position + Vector3.up * 1.5f + Vector3.right * 0.5f
            );
            Gizmos.DrawSphere(
                transform.position + Vector3.up * 1.5f + Vector3.right * (balanceAmount - 0.5f),
                0.1f
            );
        }
    }
} 