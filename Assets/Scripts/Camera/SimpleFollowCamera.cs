using UnityEngine;
using BroSkater.Player;
using BroSkater.Player.StateMachine;

namespace BroSkater.Camera
{
    public class SimpleFollowCamera : MonoBehaviour
    {
        [Header("Camera Settings")]
        public float baseDistance = 12f; // THUG medium distance
        public float baseHeight = 4.3f;  // THUG medium height
        public float baseTilt = 0.18f;   // ~10 degrees down
        public float baseFOV = 72f;      // THUG default FOV
        
        [Header("State-Specific Settings")]
        public float airTrickZoom = 0.7f;
        public float lipTrickZoom = 1.6f;
        public float lipTrickTilt = -0.8f;
        public float grindZoom = 1.0f;
        
        [Header("Interpolation Settings")]
        public float lerpXZ = 0.25f;     // Horizontal smoothing
        public float lerpY = 0.75f;      // Vertical smoothing
        public float zoomLerpRate = 0.0625f;
        public bool smoothTransitions = true;
        
        [Header("Air Rotation Settings")]
        public bool lockCameraInAir = true; // Whether to lock camera rotation while player is in air
        public float airRotationLerpSpeed = 2f; // How quickly to smoothly return to rotation-following after landing

        private UnityEngine.Camera cam;
        private PlayerController skater;
        private Vector3 offset;
        private float currentZoom = 1.0f;
        private float targetZoom = 1.0f;
        private float currentTilt;
        private float targetTilt;
        
        // For handling fixed orientation during jumps
        private Quaternion lastGroundedRotation;
        private bool wasInAir = false;
        private float rotationTransitionTimer = 0f;
        private const float ROTATION_TRANSITION_TIME = 0.3f; // Time to smoothly transition back to rotation following

        void Start()
        {
            cam = GetComponent<UnityEngine.Camera>();
            skater = FindObjectOfType<PlayerController>();
            
            if (cam != null)
            {
                cam.fieldOfView = baseFOV;
                offset = new Vector3(0, baseHeight, -baseDistance);
                currentTilt = baseTilt;
                targetTilt = baseTilt;
            }
            
            // Initialize rotation
            if (skater != null)
            {
                lastGroundedRotation = skater.transform.rotation;
            }
        }

        void LateUpdate()
        {
            if (skater == null || cam == null) return;
            
            // Track air state transitions
            bool isInAir = skater.StateMachine.CurrentState is PlayerAirborneState;
            
            // If just landed, start transition timer
            if (wasInAir && !isInAir)
            {
                rotationTransitionTimer = 0f;
            }
            
            // If just became airborne, store the current rotation
            if (!wasInAir && isInAir && lockCameraInAir)
            {
                // Store only the Yaw component of the skater's rotation
                Quaternion currentSkaterRot = skater.transform.rotation;
                float currentYaw = currentSkaterRot.eulerAngles.y;
                lastGroundedRotation = Quaternion.Euler(0, currentYaw, 0);
            }
            
            // Update transition timer if needed
            if (!isInAir && rotationTransitionTimer < ROTATION_TRANSITION_TIME)
            {
                rotationTransitionTimer += Time.deltaTime;
            }
            
            // Remember state for next frame
            wasInAir = isInAir;
            
            AdjustCameraParameters();
            UpdateCameraPosition(isInAir);
        }

        void AdjustCameraParameters()
        {
            if (skater == null || cam == null) return;

            // Calculate base distance from offset
            float targetDistance = baseDistance;
            float targetHeight = baseHeight;
            targetTilt = baseTilt;
            targetZoom = 1.0f;

            // Get skater's velocity for speed-based adjustments
            float speed = skater.Rb.linearVelocity.magnitude;
            float speedFactor = Mathf.Clamp01(speed / 30f); // Normalize speed, max at 30 units/sec

            // Adjust FOV based on speed
            float targetFOV = baseFOV + (speedFactor * 15f); // Max +15 degree FOV increase at high speeds
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * 5f);

            // State-specific adjustments
            if (skater.StateMachine.CurrentState is PlayerAirborneState airState)
            {
                // Air state - pull back and up for better trick visibility
                float verticalVel = skater.Rb.linearVelocity.y;
                
                // More dynamic height adjustment based on vertical velocity
                float heightAdjust = Mathf.Clamp(verticalVel * 0.15f, 0f, 4f);
                
                // Higher jumps get more camera adjustment
                targetHeight = baseHeight + heightAdjust;
                targetDistance = baseDistance + (heightAdjust * 0.7f);
                
                // Assume no tricks for now - we'll need to update this once we know the
                // correct property for checking trick status
                bool isDoingTrick = false;
                
                targetZoom = isDoingTrick ? airTrickZoom * 0.85f : airTrickZoom;
                
                // Slight tilt adjustment for better trick visibility
                targetTilt = baseTilt * 0.8f; // Less downward tilt in air
            }
            else if (skater.StateMachine.CurrentState is PlayerGrindingState grindState)
            {
                // Grind state - closer follow, slight zoom
                targetDistance = baseDistance * 0.85f;
                targetHeight = baseHeight * 0.9f;
                targetZoom = grindZoom;
                
                // Adjust tilt based on grind balance
                float balanceFactor = 0.5f; // Default to centered
                if (skater.StateMachine.BalanceMeter != null)
                {
                    // Assuming BalanceMeter has a way to get balance value
                    // For now, use a default balanced value until we find the right property
                    balanceFactor = 0.5f;
                }
                
                // More extreme tilt when balance is off-center
                float balanceAdjust = Mathf.Abs(balanceFactor - 0.5f) * 2f;
                targetTilt = baseTilt * (1f + (balanceAdjust * 0.3f));
            }
            else if (skater.StateMachine.CurrentState is PlayerSkatingState skateState)
            {
                // Check if the balance meter is active for manual state
                if (skater.StateMachine.BalanceMeter != null && skater.StateMachine.BalanceMeter.IsActive)
                {
                    // Manual state - lower camera for better balance visibility
                    targetHeight = baseHeight * 0.75f;
                    targetTilt = baseTilt * 1.5f; // More downward tilt for better balance visibility
                    
                    // Adjust camera distance based on balance
                    // Using default balanced value until we find the right property
                    float balanceFactor = 0.5f;
                    float balanceAdjust = Mathf.Abs(balanceFactor - 0.5f) * 2f; // 0 to 1 range
                    targetDistance = baseDistance * (0.9f + (balanceAdjust * 0.1f));
                }
                else
                {
                    // Standard ground movement
                    // Adjust distance based on speed for more dynamic feel
                    targetDistance = baseDistance * (1f - (speedFactor * 0.1f)); // Pull in slightly at high speeds
                }
            }
            // TODO: Add LipState when implemented
            // else if (skater.StateMachine.CurrentState is PlayerLipState)
            // {
            //     targetHeight = baseHeight * 1.2f;
            //     targetTilt = lipTrickTilt;
            //     targetZoom = lipTrickZoom;
            //     
            //     // For lip tricks, we want to see the player and the lip they're on
            //     // Adjust height based on the lip direction
            //     // targetHeight += someDirectionalAdjustment;
            // }

            // Clamp target distance to avoid camera issues
            targetDistance = Mathf.Clamp(targetDistance, baseDistance * 0.6f, baseDistance * 1.5f);

            // Smooth transitions
            if (smoothTransitions)
            {
                // Use THUG-like lerp rates
                float dt = Time.deltaTime;
                offset.y = Mathf.Lerp(offset.y, targetHeight, lerpY * dt * 60f); // Scale with framerate
                offset.z = Mathf.Lerp(offset.z, -targetDistance, lerpXZ * dt * 60f);
                currentZoom = Mathf.Lerp(currentZoom, targetZoom, zoomLerpRate * dt * 30f);
                currentTilt = Mathf.Lerp(currentTilt, targetTilt, lerpY * dt * 60f);
            }
            else
            {
                offset.y = targetHeight;
                offset.z = -targetDistance;
                currentZoom = targetZoom;
                currentTilt = targetTilt;
            }

            // Apply zoom to offset
            Vector3 zoomedOffset = offset * currentZoom;
            transform.localPosition = zoomedOffset;
        }

        void UpdateCameraPosition(bool isInAir)
        {
            if (skater == null) return;

            // Get skater's position
            Vector3 targetPos = skater.transform.position;
            
            if (isInAir && lockCameraInAir)
            {
                // When in air, use fixed camera orientation (only follow position)
                Vector3 cameraOffset = new Vector3(0, offset.y, offset.z);
                // Transform offset by the last grounded rotation instead of current rotation
                cameraOffset = lastGroundedRotation * cameraOffset;
                transform.position = targetPos + cameraOffset;
                
                // Keep the same rotation while in air
                Quaternion targetRot = lastGroundedRotation;
                transform.rotation = targetRot;
                
                // Apply tilt
                transform.Rotate(Vector3.right, Mathf.Rad2Deg * currentTilt);
            }
            else if (!isInAir && rotationTransitionTimer < ROTATION_TRANSITION_TIME && lockCameraInAir)
            {
                // Just landed, blend between fixed and rotation-following camera
                float transitionProgress = rotationTransitionTimer / ROTATION_TRANSITION_TIME;
                
                // Calculate fixed camera position based on last air rotation
                Vector3 fixedOffset = lastGroundedRotation * new Vector3(0, offset.y, offset.z);
                Vector3 fixedPos = targetPos + fixedOffset;
                
                // Calculate normal rotation-following position
                Quaternion targetRot = skater.transform.rotation;
                Vector3 rotatedOffset = targetRot * new Vector3(0, offset.y, offset.z);
                Vector3 followingPos = targetPos + rotatedOffset;
                
                // Blend positions
                transform.position = Vector3.Lerp(fixedPos, followingPos, transitionProgress);
                
                // Blend rotations
                transform.rotation = Quaternion.Slerp(lastGroundedRotation, targetRot, transitionProgress);
                
                // Apply tilt
                transform.Rotate(Vector3.right, Mathf.Rad2Deg * currentTilt);
            }
            else
            {
                // Normal rotation-following camera when on ground
                
                // 1. Get Skater's Yaw-Only Rotation
                Quaternion skaterYawRot = Quaternion.Euler(0, skater.transform.eulerAngles.y, 0);
                
                // 2. Calculate Target Camera World Position
                Vector3 desiredPosition = targetPos + (skaterYawRot * offset);

                // 3. Calculate Target Camera World Rotation
                // Start facing the same direction as the skater (yaw only)
                Quaternion desiredRot = skaterYawRot;
                // Apply the downward tilt around the camera's local right axis
                desiredRot *= Quaternion.AngleAxis(Mathf.Rad2Deg * currentTilt, Vector3.right); 

                // 4. Smoothly Interpolate Camera Transform
                float posLerpSpeed = lerpXZ * 60f; // Use horizontal lerp for position tracking speed
                float rotLerpSpeed = airRotationLerpSpeed * 30f; // Reuse air rotation speed, maybe tune later

                transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * posLerpSpeed);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, Time.deltaTime * rotLerpSpeed);

                // Update lastGroundedRotation with the smoothed camera rotation for air transitions
                lastGroundedRotation = transform.rotation; 

                // Remove old LookAt/Rotate
                // transform.LookAt(targetPos + (Vector3.up * 1f)); 
                // transform.Rotate(Vector3.right, Mathf.Rad2Deg * currentTilt);
            }
        }
    }
}