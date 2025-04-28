using UnityEngine;
using BroSkater.Player;
using BroSkater.Rails;
using BroSkater.Player.StateMachine;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace BroSkater.Player.StateMachine
{
    public class PlayerGrindingState : PlayerState
    {
        [System.Serializable]
        public class GrindParameters
        {
            [Header("Rail Physics")]
            public float speedBoost = 1.2f;           // Multiplier for initial grind speed
            public float minGrindSpeed = 5f;          // Minimum speed when grinding
            public float maxGrindSpeed = 20f;         // Maximum speed when grinding
            public float acceleration = 15f;          // How quickly speed builds up
            public float deceleration = 5f;           // How quickly speed drops when not accelerating
            public float railGravity = 20f;           // Force pulling down to rail
            public float snapStrength = 15f;          // How strongly to snap to rail center
            public float snapDistance = 1.0f;         // Maximum position snap distance per frame
            public float maxRailDeviation = 0.1f;     // Threshold for bailing if position deviates (should be small)
            public float endDistance = 0.05f;          // Distance from rail end to trigger dismount
            public float minSpeedToBail = 2f;         // Minimum speed required to stay on rail
            
            [Header("Jump Parameters")]
            public float jumpBoost = 1.2f;            // Speed boost when jumping off rail
            public float jumpForce = 10f;             // Increased Base vertical force (was 8f)
            public float directionalJumpFactor = 0.2f; // How much input affects jump direction (Note: less relevant with separate push force)
            public float directionalPushForce = 18f;   // Increased Force applied in the input direction (was 15f)
        }
        
        // Default parameters (can be overridden by PlayerStateMachine)
        private GrindParameters parameters = new GrindParameters();
        
        // Balance Meter reference
        private BalanceMeter balanceMeter;

        // Rail information
        private SplineGrindPath currentSplinePath; // Store the spline path
        private Vector3 railNormal = Vector3.up;
        private float currentGrindSpeed;
        private float enterTime; // Track when we entered the state
        private bool isMovingBackwards = false; // Track intended movement direction 
        private Vector3 intendedGrindDirection; // Store the consistent direction of movement (Set on Enter)
        private int stuckFrameCount = 0; // For stuck detection
        private float totalDistanceAlongRail = 0f; // Total distance along rail 
        private RigidbodyInterpolation originalInterpolation; // Store original RB interpolation
        private bool originalIsKinematic; // Store original kinematic state
        
        // <<< ADDED: Layer Collision Management >>>
        private int playerLayer = -1;
        private int groundLayer = -1; // Assuming rails might be on Ground or a specific Rail layer
        private int railLayer = -1; // Specific rail layer
        private const float COLLISION_REENABLE_DELAY = 0.05f; // Delay before re-enabling collision
        // <<< END ADDED >>>

        // THUG-style rail segment tracking
        private bool isAtPathStart = false;
        private bool isAtPathEnd = false;
        private GrindPath.GrindPoint currentGrindPoint;
        private GrindPath.GrindPoint nextGrindPoint;
        private GrindPath.GrindPoint previousGrindPoint;
        
        // Entry/exit point detection
        private bool hasDetectedExitPoint = false;
        private float exitPointDetectionTime = 0f;

        [SerializeField] private float railStuckThreshold = 0.2f; // Min movement needed to consider not stuck
        [SerializeField] private float railEndCheckDelay = 0.5f; // Delay before checking for rail ends
        [SerializeField] private float railStuckTime = 0.75f; // Time before bailing from being stuck
        [SerializeField] private float connectorDetectionDistance = 0.35f; // Distance to detect a connector

        private float _timeSinceLastMovement = 0f;
        private float _timeSinceStateStart = 0f;
        private Vector3 _lastPosition;
        private GrindPathConnector _currentConnector;
        private bool isChargingGrindJump = false; // Flag for charged jump
        private Vector2 chargeDirectionInput;     // Added: Store input direction at charge start

        // Constants from original logic can be defined here or in parameters
        private const float RAIL_STUCK_THRESHOLD = 0.01f; // Minimum movement squared per frame
        private const float RAIL_STUCK_TIME_LIMIT = 0.75f; // Time before bailing

        public PlayerGrindingState(PlayerStateMachine stateMachine, PlayerController player) : base(stateMachine, player)
        {
            balanceMeter = stateMachine.BalanceMeter;
            if (balanceMeter == null) Debug.LogError("BalanceMeter reference is missing in PlayerStateMachine!");
        }
        
        public void SetParameters(GrindParameters newParams)
        {
            if (newParams != null)
                parameters = newParams;
        }

        /// <summary>
        /// Reinstated simplified version for compatibility with older detection logic.
        /// Sets the target rail and basic info. Default Enter() will validate and use this.
        /// </summary>
        public void SetRailInfo(SplineGrindPath splinePath, Vector3 snapPoint, Vector3 snapDirection, float distance, Vector3 hitNormal)
        {
            Debug.Log($"[Compatibility] SetRailInfo called: Rail={splinePath?.name}, Dist={distance}");
            currentSplinePath = splinePath; // Set the path for default Enter() to check
            intendedGrindDirection = snapDirection.normalized;
            totalDistanceAlongRail = distance; // Store initial distance
            railNormal = hitNormal.normalized;
        }

        public override void Enter()
        {
            // Default Enter used by older flow (SetRailInfo)
            // --- Validation --- 
            if (currentSplinePath == null || currentSplinePath.GetTotalLength() <= 0f)
            {
                 Debug.LogError($"GrindingState.Enter (Default) validation failed: Rail set via SetRailInfo is null or length is zero! Rail: {(currentSplinePath ? currentSplinePath.name : "NULL")}");
                 // No need to clear PlayerController state here as it wasn't set via RequestGrind
                 stateMachine.ChangeState(stateMachine.AirborneState);
                 return;
            }
             if (balanceMeter == null)
            {
                 Debug.LogError("Grind State entered without valid BalanceMeter! Bailing.");
                 stateMachine.ChangeState(stateMachine.AirborneState);
                 return;
            }
            // --- End Validation ---
            
            Debug.Log($"Entering Grinding State (Default) with Rail: {currentSplinePath.name}, Entry Dist: {totalDistanceAlongRail:F2}");
            
            // Proceed with setup using info from SetRailInfo
            SetupGrindState();
        }

        // Overloaded Enter used by new RequestGrind flow
        public void Enter(SplineGrindPath rail, Vector3 entryPoint, Vector3 entryDirection, float entryDistance, Vector3 hitNormal)
        {
            // --- Validation --- 
            if (rail == null || rail.GetTotalLength() <= 0f)
            {
                Debug.LogError($"GrindingState.Enter (Overload) validation failed: Rail is null or length is zero! Rail: {(rail ? rail.name : "NULL")}");
                CleanupGrindStateInternals(false); // Cleanup without changing state here
                player.SetCurrentRail(null); // Clear potential pending request
                player.IsGrinding = false;
                stateMachine.ChangeState(stateMachine.AirborneState);
                return; 
            }
             if (balanceMeter == null)
            {
                Debug.LogError("Grind State entered without valid BalanceMeter! Bailing.");
                CleanupGrindStateInternals(false);
                player.SetCurrentRail(null);
                player.IsGrinding = false;
                stateMachine.ChangeState(stateMachine.AirborneState);
                return; 
            }
            // --- End Validation ---
            
            Debug.Log($"Entering Grinding State (Overload) with Rail: {rail.name}, Entry Dist: {entryDistance:F2}");
            currentSplinePath = rail; // Assign the validated rail
            intendedGrindDirection = entryDirection.normalized;
            totalDistanceAlongRail = Mathf.Clamp(entryDistance, 0f, currentSplinePath.GetTotalLength());
            railNormal = hitNormal.normalized;
            
            // Proceed with common setup logic
            SetupGrindState();
        }
        
        /// <summary>
        /// Common setup logic called by both Enter methods after validation.
        /// </summary>
        private void SetupGrindState()
        {
             // Store entry time & reset counters
            enterTime = Time.time;
            _timeSinceStateStart = 0f;
            stuckFrameCount = 0;
             _timeSinceLastMovement = 0f;
            _lastPosition = player.transform.position;

            // Store and set Rigidbody state
            originalInterpolation = player.Rb.interpolation;
            player.Rb.interpolation = RigidbodyInterpolation.Interpolate;
            originalIsKinematic = player.Rb.isKinematic;
            player.Rb.isKinematic = true;

            // <<< ADDED: Ignore Collisions >>>
            playerLayer = player.gameObject.layer;
            groundLayer = LayerMask.NameToLayer("Ground"); // Adjust if your ground layer name differs
            railLayer = LayerMask.NameToLayer("Rail");   // Adjust if your rail layer name differs
            
            if (playerLayer != -1 && groundLayer != -1)
            {
                Physics.IgnoreLayerCollision(playerLayer, groundLayer, true);
                Debug.Log($"Ignoring collisions between Player layer ({LayerMask.LayerToName(playerLayer)}) and Ground layer ({LayerMask.LayerToName(groundLayer)}) for grind.");
            }
            else { Debug.LogWarning("Could not find Player or Ground layer indices for collision ignoring!"); }

            if (playerLayer != -1 && railLayer != -1)
            {
                 Physics.IgnoreLayerCollision(playerLayer, railLayer, true);
                 Debug.Log($"Ignoring collisions between Player layer ({LayerMask.LayerToName(playerLayer)}) and Rail layer ({LayerMask.LayerToName(railLayer)}) for grind.");
            }
            else { Debug.LogWarning("Could not find Player or Rail layer indices for collision ignoring!"); }
            // <<< END ADDED >>>

            // Determine movement direction
            Vector3 tangentAtEntry;
            // Use bool _ for the unused out parameter
            bool success = currentSplinePath.GetPointAtDistance(totalDistanceAlongRail, out _, out tangentAtEntry, out _);
            if (!success)
            {
                 Debug.LogError("SetupGrindState: Failed to get spline tangent! Bailing.");
                 HandleBail("Failed spline check during setup");
                 return;
            }
            isMovingBackwards = Vector3.Dot(intendedGrindDirection, tangentAtEntry.normalized) < 0f;
            Debug.Log($"Grind Direction Determined: {(isMovingBackwards ? "Backward" : "Forward")}");

            // Calculate Initial Speed
            float initialSpeed = player.GetVelocity().magnitude;
            currentGrindSpeed = Mathf.Clamp(initialSpeed * parameters.speedBoost, parameters.minGrindSpeed, parameters.maxGrindSpeed);
            player.SetVelocity(intendedGrindDirection * currentGrindSpeed);

            // Physics setup
            player.Rb.useGravity = false;

            // Start balance meter
            balanceMeter.StartGrindBalance(player.statRailBalance);

            // Snap Position and Rotation
            AlignPlayerToRail(totalDistanceAlongRail);

            Debug.Log($"Grinding state setup complete. Initial Speed: {currentGrindSpeed:F2}");
        }

        public override void LogicUpdate()
        {
             if (currentSplinePath == null) return; // Early exit if path became invalid
            balanceMeter.UpdateLogic();
            // ... (Rest of LogicUpdate as before, checking Bail/Jump/End) ...
            // --- Bail/Exit Conditions ---
            if (balanceMeter.HasBailed)
            {
                HandleBail("Balance Failed");
                return; // State changed
            }

             // Check minimum speed after a brief moment
            if (_timeSinceStateStart > 0.2f && currentGrindSpeed < parameters.minSpeedToBail)
            {
                HandleBail($"Speed ({currentGrindSpeed:F2}) too low (Min: {parameters.minSpeedToBail})" );
                return; // State changed
            }

            // --- Charged Jump Logic ---
            if (inputHandler.JumpInputDown)
            {
                isChargingGrindJump = true;
                chargeDirectionInput = inputHandler.MoveInput.normalized; // Store normalized direction
                inputHandler.ConsumeJumpInputDown();
                Debug.Log($"[Grind State] Jump Charge Started. Dir: {chargeDirectionInput}" );
            }

            if (inputHandler.JumpInputUp)
            {
                inputHandler.ConsumeJumpInputUp(); // Consume regardless
                if (isChargingGrindJump)
                {
                    Debug.Log("[Grind State] Jump Input Released - Executing Jump" );
                    HandleJumpOff(chargeDirectionInput);
                    isChargingGrindJump = false;
                    return; // State changed
                }
                 else {
                     Debug.Log("[Grind State] Jump Input Released - Was not charging." );
                 }
            }
            // --- End Charged Jump Logic ---


            // --- Rail End Check ---
            // Only check rail ends if we've been on the rail for a bit
             if (_timeSinceStateStart < 0.2f) return;

            // Check if we've moved off the ends based on distance
            bool isPastEnd = (!isMovingBackwards && totalDistanceAlongRail >= currentSplinePath.GetTotalLength() - parameters.endDistance);
            bool isPastStart = (isMovingBackwards && totalDistanceAlongRail <= parameters.endDistance);

            // Only handle rail end if the path is NOT a closed loop
            if (!currentSplinePath.IsClosed && (isPastStart || isPastEnd))
            {
                HandleRailEnd(isPastStart ? "Reached Start" : "Reached End" );
                return; // State changed
            }
             // --- End Rail End Check ---
        }

        public override void PhysicsUpdate()
        {
            if (currentSplinePath == null) return; // Early exit if path became invalid

            float dt = Time.fixedDeltaTime;
            _timeSinceStateStart += dt;

            // ... (Speed update logic as before) ...
            float forwardInput = inputHandler.MoveInput.y;
            if (forwardInput > 0.1f && !isMovingBackwards) { currentGrindSpeed += parameters.acceleration * dt; }
            else if ((forwardInput < -0.1f && !isMovingBackwards) || (forwardInput >= -0.1f && forwardInput <= 0.1f && !isMovingBackwards)) { currentGrindSpeed -= parameters.deceleration * dt; }
            else if (isMovingBackwards && forwardInput < -0.1f) { currentGrindSpeed += parameters.acceleration * dt; }
            else if (isMovingBackwards) { currentGrindSpeed -= parameters.deceleration * dt; }
            currentGrindSpeed = Mathf.Clamp(currentGrindSpeed, parameters.minGrindSpeed, parameters.maxGrindSpeed);

            // ... (Movement Delta calculation as before) ...
            float distanceDelta = currentGrindSpeed * dt;
            if (isMovingBackwards) { distanceDelta *= -1f; }
            float previousDistance = totalDistanceAlongRail;
            totalDistanceAlongRail += distanceDelta;

            // ... (Loop handling as before) ...
             if (currentSplinePath.IsClosed)
             {
                 float totalLength = currentSplinePath.GetTotalLength();
                 if (totalDistanceAlongRail >= totalLength) { totalDistanceAlongRail -= totalLength; }
                 else if (totalDistanceAlongRail < 0f) { totalDistanceAlongRail += totalLength; }
             }
             else {
                 totalDistanceAlongRail = Mathf.Clamp(totalDistanceAlongRail, 0f, currentSplinePath.GetTotalLength());
             }

            // --- Update Position & Rotation ---
            AlignPlayerToRail(totalDistanceAlongRail);

            // ... (Stuck Check logic as before) ...
            Vector3 currentPosition = player.transform.position;
            float movementSqr = (currentPosition - _lastPosition).sqrMagnitude;
            if (movementSqr < RAIL_STUCK_THRESHOLD * RAIL_STUCK_THRESHOLD * dt * dt) { _timeSinceLastMovement += dt; }
            else { _timeSinceLastMovement = 0f; }
            _lastPosition = currentPosition;
            if (_timeSinceLastMovement >= RAIL_STUCK_TIME_LIMIT) { HandleBail($"Stuck on rail for {_timeSinceLastMovement:F2}s" ); return; }
        }

        private void AlignPlayerToRail(float distance)
        {
            if (currentSplinePath == null) return;

            // Use bool _ for the unused out parameter
            bool success = currentSplinePath.GetPointAtDistance(distance, out Vector3 railPoint, out Vector3 railTangent, out _);

            if (!success)
            {
                Debug.LogWarning($"AlignPlayerToRail: Failed to get point at distance {distance}. Clamping.");
                 distance = Mathf.Clamp(distance, 0f, currentSplinePath.GetTotalLength());
                 // Use bool __ for the second unused out parameter
                 success = currentSplinePath.GetPointAtDistance(distance, out railPoint, out railTangent, out _);
                 if (!success) {
                     HandleBail("Failed spline point after clamping");
                     return;
                 }
            }

            Vector3 forwardDirection = isMovingBackwards ? -railTangent.normalized : railTangent.normalized;
            
            // Get Spline Up vector using SplineUtility.Evaluate
            Vector3 effectiveUp = Vector3.up; // Default to world up
            if (currentSplinePath.Spline != null && currentSplinePath.GetTotalLength() > 0f)
            {
                 // Normalize distance to t value (0-1)
                 float t = Mathf.Clamp01(distance / currentSplinePath.GetTotalLength());
                 // Evaluate spline at t
                 SplineUtility.Evaluate(currentSplinePath.Spline, t, out float3 pos, out float3 tan, out float3 up);
                 Vector3 splineUp = ((Vector3)up).normalized;
                 if (splineUp != Vector3.zero) // Use spline up if it's valid
                 {
                     effectiveUp = splineUp;
                 }
            }
            else {
                 Debug.LogWarning("AlignPlayerToRail: Could not evaluate spline up vector (Spline null or zero length).");
            }
            
            Quaternion targetRotation = Quaternion.LookRotation(forwardDirection, effectiveUp);

            // --- POSITION UPDATE: Revert to Lerp ---
            // Calculate target position slightly above the rail point
            float boardHeightOffset = 0.1f; // Adjust as needed
            Vector3 targetPosition = railPoint + effectiveUp * boardHeightOffset; // Use effectiveUp for offset
        
            // Smoothly move towards the target position using Lerp
            // Use a Lerp factor based on snapStrength or a fixed value
            float positionLerpSpeed = parameters.snapStrength * Time.fixedDeltaTime; // Or adjust speed
            player.transform.position = Vector3.Lerp(player.transform.position, targetPosition, positionLerpSpeed);
            // -----------------------------------------
        
            // --- ROTATION UPDATE: Keep Slerp or make direct ---
            // Direct rotation might feel snappier and work fine with Lerped position
            player.transform.rotation = Quaternion.Slerp(player.transform.rotation, targetRotation, 15f * Time.fixedDeltaTime); // Keep Slerp for now
            // OR direct assignment: player.transform.rotation = targetRotation;
            // --------------------------------------------------
        }

        // Helper methods from original code - kept for functionality
        private GrindPath.GrindPoint FindClosestGrindPoint(float distance)
        {
            // Implementation depends on how GrindPath and GrindPoint are structured
            // Needs access to the discrete points if using that system.
            // For SplineGrindPath, this might be less relevant unless mapping distance to original nodes.
            return null; // Placeholder
        }

        private GrindPath.GrindPoint FindNextGrindPoint(GrindPath.GrindPoint current, bool movingBackwards)
        {
            // Implementation depends on GrindPath/GrindPoint structure
            return null; // Placeholder
        }

        private void HandleBail(string reason)
        {
            Debug.Log($"Bailing from Grind: {reason}");
            CleanupGrindState(); // Ensure cleanup happens before changing state
            stateMachine.ChangeState(stateMachine.BailedState); // Or AirborneState?
        }

        private void HandleJumpOff(Vector2 directionInput) // Now accepts direction
        {
            if (currentSplinePath == null) return; // Safety check

            // Parameters
            float baseJumpForce = player.PhysicsParams.GetValueFromStat(
                player.PhysicsParams.grindExitJumpForce, 
                player.statOllie, 
                player.IsSwitch, 
                player.PhysicsParams.standardSwitchMultiplier
            );
            Debug.Log($"[Grind Jump] Base Vertical Force Calculated: {baseJumpForce} (Ollie Stat: {player.statOllie}, Switch: {player.IsSwitch})");

            float speedBoostMultiplier = parameters.jumpBoost;
            float directionalForceMagnitude = parameters.directionalPushForce; // Use the parameter

            // --- Calculate Velocity Components --- 

            // 1. Base Vertical Jump
            Vector3 verticalJumpVel = Vector3.up * baseJumpForce;

            // 2. Forward Momentum/Boost (based on current grind direction)
            currentSplinePath.GetPointAtDistance(totalDistanceAlongRail, out _, out Vector3 currentTangent, out _);
            Vector3 forwardDir = isMovingBackwards ? -currentTangent.normalized : currentTangent.normalized;
            // Retain horizontal speed + boost multiplier effect
            Vector3 forwardVel = forwardDir * currentGrindSpeed * speedBoostMultiplier; 

            // 3. Directional Input Push
            Vector3 directionalPushVel = Vector3.zero;
            if (directionInput.magnitude > 0.1f)
            {
                // Convert 2D input relative to player's orientation
                Vector3 pushDirection = (Vector3.right * directionInput.x + Vector3.forward * directionInput.y).normalized;
                
                directionalPushVel = pushDirection * directionalForceMagnitude;
                Debug.Log($"Grind Jump Direction Input: {directionInput}, Calculated Push Vel (World Relative): {directionalPushVel}");
            }

            // --- Combine Velocities --- 
            // Start with forward momentum, add vertical jump, add directional push
            Vector3 finalJumpVelocity = forwardVel + verticalJumpVel + directionalPushVel;

            Debug.Log($"HandleJumpOff: Vertical={verticalJumpVel}, Forward={forwardVel}, Directional={directionalPushVel}, Final={finalJumpVelocity}");

            // --- State Change --- 
            CleanupGrindState(); // Clean up current state (restores rigidbody to non-kinematic, calls OnEndGrind)
            
            // *** Apply velocity AFTER cleanup makes Rigidbody non-kinematic ***
            player.Velocity = finalJumpVelocity;
            
            Debug.Log($"Jumped off rail. Applied Velocity: {player.Velocity}");

            // Change to the airborne state (this calls AirborneState.Enter)
            stateMachine.ChangeState(stateMachine.AirborneState); 
        }

        private void HandleRailEnd(string reason)
        {
            Debug.Log($"Reached Grind End: {reason}");
            if (currentSplinePath == null) return; // Safety

            // Use bool _ for the unused out parameter
            currentSplinePath.GetPointAtDistance(totalDistanceAlongRail, out _, out Vector3 exitTangent, out _);
            Vector3 exitVelocityDirection = (isMovingBackwards ? -exitTangent.normalized : exitTangent.normalized);
            float exitSpeed = currentGrindSpeed * parameters.jumpBoost;

            CleanupGrindState();
            stateMachine.ChangeState(stateMachine.AirborneState);

            player.SetVelocity(exitVelocityDirection * exitSpeed);
            Debug.Log($"Exiting rail end. Applied Velocity: {exitVelocityDirection * exitSpeed}");
        }

        /// <summary>
        /// Centralized cleanup logic called before changing state from grind.
        /// </summary>
        private void CleanupGrindState()
        {
            Debug.Log("CleanupGrindState Called");
            // Restore Rigidbody state
            if (player != null && player.Rb != null) // Add null check for player
            {
                player.Rb.isKinematic = originalIsKinematic;
                player.Rb.interpolation = originalInterpolation;
                player.Rb.useGravity = true;
            }
            balanceMeter?.StopBalance();
            
            // <<< TRIGGER Delayed Collision Re-enable >>>
            if (player != null)
            {
                player.RequestCollisionReEnable(COLLISION_REENABLE_DELAY);
            }
            // <<< END TRIGGER >>>
            
             // Tell PlayerController to clear its state
            player?.OnEndGrind(); // Use null-conditional
            if(player != null) player.IsGrinding = false; // Reset flag
            
             // Reset GrindDetector
             // Get component directly from the player GameObject
             if (player != null) 
             {
                 GrindDetector detector = player.GetComponent<GrindDetector>(); 
                 detector?.ResetGrindAttempt(); // Use null-conditional
             }
             
             // Clear state variables
            currentSplinePath = null;
            isChargingGrindJump = false;
        }
        
         /// <summary>
        /// Internal cleanup only, doesn't change state or call PlayerController.OnEndGrind
        /// Used when validation fails within an Enter method.
        /// </summary>
        private void CleanupGrindStateInternals(bool restoreRigidbody = true)
        {
             if (restoreRigidbody && player != null && player.Rb != null)
             {
                 player.Rb.isKinematic = originalIsKinematic;
                 player.Rb.interpolation = originalInterpolation;
                 player.Rb.useGravity = true;
             }
             balanceMeter?.StopBalance();
             // Don't call player.OnEndGrind here
             // Don't reset player.IsGrinding here
             currentSplinePath = null;
             isChargingGrindJump = false;
        }

        public override void Exit()
        {
             Debug.Log("GrindingState Exit() called.");
             // Call primary cleanup if the path wasn't already cleared by HandleBail/Jump/End
             if (currentSplinePath != null) {
                  Debug.LogWarning("GrindingState Exit() called with active spline - forcing cleanup.");
                  CleanupGrindState();
             }
             else {
                  Debug.Log("GrindingState Exit() called, cleanup likely already done.");
             }
        }
    }
} 