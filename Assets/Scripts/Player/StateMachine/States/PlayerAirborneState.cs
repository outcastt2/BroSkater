using UnityEngine;
using BroSkater.Rails;

namespace BroSkater.Player.StateMachine
{
    public class PlayerAirborneState : PlayerState
    {
        // Air control
        private const float AIR_CONTROL_FORCE = 1.0f; // Reduced air control for better physics feel
        
        // Trick rotation
        private const float INITIAL_TRICK_ROTATION_SPEED = 360.0f;
        private float currentTrickRotationSpeed;
        private bool isTrickActive = false;
        private string currentTrickName = "";
        private Vector3 trickRotationAxis = Vector3.forward; // Default is kickflip axis
        
        // Gravity parameters
        // private const float GRAVITY_FORCE = 9.81f; // Standard gravity (REPLACED)
        [SerializeField] private float fakeGravityForce = 25f; // Tunable downward force
        [SerializeField] private float risingGravityMultiplier = 0.6f; // Gravity multiplier when moving up
        [SerializeField] private float fallingGravityMultiplier = 1.0f; // Gravity multiplier when moving down
        private const float AIR_RESISTANCE = 0.995f; // Air resistance factor
        
        // Jump physics
        private float airTime = 0f;
        
        // Rotation tracking
        private float totalRotation = 0f;
        private Quaternion initialRotation;
        private Vector3 initialUp;
        
        // --- Added for Grind Detection ---
        private const float RAIL_DETECTION_DISTANCE = 2.5f;
        private const float MAX_VERTICAL_OFFSET = 2.0f; 
        private bool hasConsumedGrindInput = false;
        
        // Wheel and feeler offsets for rail detection (in local space)
        private readonly Vector3[] groundFeelerOffsets = new Vector3[]
        {
            // Front wheel area
            new Vector3(-0.3f, -0.15f, 0.4f),  // Front left
            new Vector3(0.0f, -0.15f, 0.4f),   // Front center
            new Vector3(0.3f, -0.15f, 0.4f),   // Front right
            
            // Center wheel area
            new Vector3(-0.3f, -0.15f, 0.0f),  // Center left
            new Vector3(0.0f, -0.15f, 0.0f),   // Center
            new Vector3(0.3f, -0.15f, 0.0f),   // Center right
            
            // Back wheel area 
            new Vector3(-0.3f, -0.15f, -0.4f), // Back left
            new Vector3(0.0f, -0.15f, -0.4f),  // Back center
            new Vector3(0.3f, -0.15f, -0.4f),  // Back right
            
            // Side feelers (for catching rails from the side)
            new Vector3(-0.4f, 0.0f, 0.0f),    // Left side
            new Vector3(0.4f, 0.0f, 0.0f),     // Right side
            
            // --- Added Higher/Side Feelers ---
            new Vector3(-0.3f, 0.0f, 0.4f),  // Front left (Level)
            new Vector3(0.3f, 0.0f, 0.4f),   // Front right (Level)
            new Vector3(-0.3f, 0.0f, -0.4f), // Back left (Level)
            new Vector3(0.3f, 0.0f, -0.4f),  // Back right (Level)
        };

        // Multiple ray directions to cover different approaches
        private readonly Vector3[] rayDirections = new Vector3[]
        {
            // Straight down
            new Vector3(0, -1, 0),                           // Straight down
            new Vector3(0, -0.9f, 0.1f).normalized,          // Slightly forward-down
            new Vector3(0, -0.9f, -0.1f).normalized,         // Slightly backward-down
            new Vector3(0.1f, -0.9f, 0).normalized,          // Slightly right-down
            new Vector3(-0.1f, -0.9f, 0).normalized,         // Slightly left-down
            new Vector3(0, -0.7f, 0.3f).normalized,          // More forward-down
            new Vector3(0, -0.7f, -0.3f).normalized,         // More backward-down
            
            // --- Added Horizontal & Upward --- 
            new Vector3(1, 0, 0),                            // Right
            new Vector3(-1, 0, 0),                           // Left
            new Vector3(0.707f, 0.707f, 0).normalized,       // Up-Right
            new Vector3(-0.707f, 0.707f, 0).normalized,      // Up-Left
            new Vector3(0, 0.3f, 1).normalized,             // Slightly Up-Forward
            new Vector3(0, 0.3f, -1).normalized,            // Slightly Up-Backward
        };
        // --- End Added for Grind Detection ---
        
        public PlayerAirborneState(PlayerStateMachine stateMachine, PlayerController player) : base(stateMachine, player) { }
        
        public override void Enter()
        {
            Debug.Log("Entering Airborne State");
            
            // We're airborne
            player.IsGrounded = false;
            
            // Reset trick state
            isTrickActive = false;
            currentTrickName = "";
            currentTrickRotationSpeed = INITIAL_TRICK_ROTATION_SPEED;
            
            // Reset air time counter
            airTime = 0f;
            
            // Store initial rotation for rotation tracking
            initialRotation = player.transform.rotation;
            initialUp = initialRotation * Vector3.up; // Transform up vector to world space
            totalRotation = 0f;

            // *** DO NOT MODIFY VELOCITY ON ENTRY ***
            // Let the velocity from the previous state carry over.
            // The ollie logic in SkatingState sets the specific velocity needed for jumps.
            // For falls, we want the existing momentum.
        }
        
        public override void LogicUpdate()
        {
            // Increment air time
            airTime += Time.deltaTime;
            
            // Handle trick input
            HandleTrickInput();
            
            // Check for ground
            // player.CheckGrounded(); // REMOVED: Check is done in PlayerController.FixedUpdate
            
            // If grounded, the main controller will handle state transition
            
            // --- Reset Grind Consumption --- 
            if (!inputHandler.GrindInputHeld)
            {
                hasConsumedGrindInput = false;
            }
            // --- End Reset Grind Consumption ---
        }
        
        public override void PhysicsUpdate()
        {
            Debug.Log("--- AirborneState PhysicsUpdate --- ACTIVE"); // Log state activity
            float dt = Time.fixedDeltaTime;
            
            // Increment air time
            airTime += Time.deltaTime;
            
            // Handle trick input
            HandleTrickInput();
            
            // Check for ground
            // player.CheckGrounded(); // REMOVED: Check is done in PlayerController.FixedUpdate
            
            // If grounded, the main controller will handle state transition
            
            // --- Reset Grind Consumption --- 
            if (!inputHandler.GrindInputHeld)
            {
                hasConsumedGrindInput = false;
            }
            // --- End Reset Grind Consumption ---
            
            // Apply air control input
            HandleAirControl(dt);
            
            // Apply gravity
            ApplyGravity(dt);
            
            // Process rotations if a trick is active
            if (isTrickActive)
            {
                ApplyTrickRotation(dt);
            }
            
            // Apply air resistance to velocity only (not angular)
            player.Velocity *= Mathf.Pow(AIR_RESISTANCE, dt * 60f);
            
            // --- Modified Grind Check --- 
            // Check for rail if grind input is HELD, hasn't been consumed, and other conditions met
            if (airTime > 0.1f && inputHandler.GrindInputHeld && !hasConsumedGrindInput && // Changed from GrindInputDown
                player.Velocity.magnitude >= player.PhysicsParams.GetValue(player.PhysicsParams.minGrindSpeed, player.IsSwitch)) 
            {
                Debug.Log($"Airborne: Attempting TryStickToRail (Input Held: {inputHandler.GrindInputHeld}, Consumed: {hasConsumedGrindInput})");
                if (TryStickToRail()) // Call the copied method
                {
                    // Successfully found a rail and transitioned to grind state
                    hasConsumedGrindInput = true; // Consume the input now
                    // inputHandler.ConsumeGrindInputDown(); // No longer need to consume the 'Down' event here
                    return; // Exit PhysicsUpdate early as we changed state
                }
                // If TryStickToRail fails, do nothing, it will check again next physics frame while held
            }
            // --- End Modified Grind Check ---
            
            // <<< RE-ADD: Reset ignore ground flag after a short delay >>>
            if (player.IsIgnoringGround && airTime > 0.05f) // Reset after a very short time
            {
                Debug.Log($"[AirborneState] Resetting IsIgnoringGround flag (airTime = {airTime:F2})");
                player.IsIgnoringGround = false;
            }
            // <<< END RE-ADD >>>
        }
        
        private void ApplyGravity(float dt)
        {
            // Determine gravity multiplier based on vertical velocity
            float currentMultiplier = player.Velocity.y > 0 ? risingGravityMultiplier : fallingGravityMultiplier;
            
            // Apply the fake gravity force
            Vector3 velocityBeforeGravity = player.Velocity; 
            player.Velocity += Vector3.down * fakeGravityForce * currentMultiplier * dt;
            // Debug.Log($"AirborneState.ApplyGravity: VelBefore={velocityBeforeGravity.y:F2}, VelAfter={player.Velocity.y:F2}, Multiplier={currentMultiplier:F1}");
        }
        
        private void HandleAirControl(float dt)
        {
            // --- Handle Rotation Only --- 
            float rotationInput = inputHandler.MoveInput.x;
            if (!isTrickActive) // Only allow non-trick rotation if not actively flipping
            {
                if (Mathf.Abs(rotationInput) > 0.1f)
                {
                    float baseSpinSpeed = 180f; 
                    float spinStrength = Mathf.Lerp(baseSpinSpeed, baseSpinSpeed * 1.5f, player.statSpin / 10f);
                    player.AngularVelocity = new Vector3(0, rotationInput * spinStrength, 0);
                    totalRotation += rotationInput * spinStrength * dt;
                }
                else
                {
                    player.AngularVelocity *= Mathf.Pow(0.95f, dt * 60f);
                    if (player.AngularVelocity.magnitude < 0.1f)
                    {
                        player.AngularVelocity = Vector3.zero;
                    }
                }
            }
            // --- Removed Forward/Backward Velocity Control ---
            /* 
            Vector2 moveInput = inputHandler.MoveInput;
            if (moveInput.magnitude > 0.1f)
            {
                Vector3 right = Vector3.right;
                Vector3 forward = Vector3.forward;
                Vector3 controlDirection = (right * moveInput.x + forward * moveInput.y).normalized;
                float airControlMultiplier = Mathf.Lerp(0.5f, 1.0f, player.statAir / 10f);
                player.Velocity += controlDirection * AIR_CONTROL_FORCE * airControlMultiplier * dt;
            }
            */
            
            // Rotation handling based on MoveInput.x was moved above
            // ... (rest of old rotation logic removed as it's integrated above) ...
        }
        
        private void HandleTrickInput()
        {
            // Only allow starting tricks if not already doing one and we have some air time
            if (!isTrickActive && airTime > 0.2f)
            {
                if (inputHandler.TrickInputDown)
                {
                    // Start kickflip
                    isTrickActive = true;
                    currentTrickName = "Kickflip";
                    currentTrickRotationSpeed = INITIAL_TRICK_ROTATION_SPEED * (player.statFlipSpeed / 5f);
                    trickRotationAxis = Vector3.forward; // Local Z axis
                    
                    // Set angular velocity for kickflip
                    player.AngularVelocity = trickRotationAxis * currentTrickRotationSpeed * Mathf.Deg2Rad;
                    
                    inputHandler.ConsumeTrickInputDown();
                }
                else if (inputHandler.TrickAltInputDown)
                {
                    // Start heelflip (opposite direction)
                    isTrickActive = true;
                    currentTrickName = "Heelflip";
                    currentTrickRotationSpeed = -INITIAL_TRICK_ROTATION_SPEED * (player.statFlipSpeed / 5f);
                    trickRotationAxis = Vector3.forward; // Local Z axis
                    
                    // Set angular velocity for heelflip (negative for opposite rotation)
                    player.AngularVelocity = trickRotationAxis * currentTrickRotationSpeed * Mathf.Deg2Rad;
                    
                    inputHandler.ConsumeTrickAltInputDown();
                }
                else if (inputHandler.Special1InputDown)
                {
                    // Start 360 flip (combo of flip and shuv-it)
                    isTrickActive = true;
                    currentTrickName = "360 Flip";
                    currentTrickRotationSpeed = INITIAL_TRICK_ROTATION_SPEED * 1.2f * (player.statFlipSpeed / 5f);
                    
                    // Combined rotation axis for 360 flip
                    trickRotationAxis = (Vector3.forward + Vector3.up * 0.5f).normalized;
                    
                    // Set angular velocity for 360 flip
                    player.AngularVelocity = trickRotationAxis * currentTrickRotationSpeed * Mathf.Deg2Rad;
                    
                    inputHandler.ConsumeSpecial1InputDown();
                }
            }
        }
        
        private void ApplyTrickRotation(float dt)
        {
            // Track rotation progress
            totalRotation += currentTrickRotationSpeed * dt;
            
            // Get current euler angles for checking completion
            Vector3 currentEulerAngles = player.transform.rotation.eulerAngles;
            
            // Normalize angles for easier checking
            float currentXRotation = NormalizeAngle(currentEulerAngles.x);
            float currentZRotation = NormalizeAngle(currentEulerAngles.z);
            
            // Determine if trick is complete (close to initial orientation)
            bool isNearlyComplete = (Mathf.Abs(currentXRotation) < 20f || Mathf.Abs(currentXRotation) > 340f) &&
                                   (Mathf.Abs(currentZRotation) < 20f || Mathf.Abs(currentZRotation) > 340f);
            
            // Check if we've completed at least one full rotation and are close to level
            if (totalRotation > 330f && isNearlyComplete && airTime > 0.5f)
            {
                // Trick has completed
                CompleteTrick();
            }
        }
        
        private void CompleteTrick()
        {
            // Stop the rotation
            isTrickActive = false;
            
            // Slow down angular velocity but don't eliminate it completely
            player.AngularVelocity *= 0.2f;
            
            // Register successful trick with score/combo system
            Debug.Log($"Completed {currentTrickName}! Rotation: {totalRotation:F1} degrees");
            
            // TODO: Register with score system
        }
        
        private void LandTrick()
        {
            // If we landed while a trick was active
            if (isTrickActive)
            {
                // Check if we're close to proper landing orientation
                Vector3 currentEulerAngles = player.transform.rotation.eulerAngles;
                float currentXRotation = NormalizeAngle(currentEulerAngles.x);
                float currentZRotation = NormalizeAngle(currentEulerAngles.z);
                
                bool properLanding = (Mathf.Abs(currentXRotation) < 30f || Mathf.Abs(currentXRotation) > 330f) &&
                                    (Mathf.Abs(currentZRotation) < 30f || Mathf.Abs(currentZRotation) > 330f);
                
                if (properLanding)
                {
                    Debug.Log($"Landed {currentTrickName} successfully!");
                    // TODO: Register with score system
                }
                else
                {
                    Debug.Log($"Failed to land {currentTrickName}! Bailing...");
                    // TODO: Trigger bail animation
                }
            }
            
            // Reset trick state
            isTrickActive = false;
            player.AngularVelocity = Vector3.zero;
            
            // Stabilize rotation to avoid weird physics
            StabilizeRotation();
        }
        
        private void StabilizeRotation()
        {
            // Get current rotation in euler
            Vector3 currentEulerAngles = player.transform.rotation.eulerAngles;
            
            // Keep only the Y rotation, completely zeroing X and Z
            float yRotation = currentEulerAngles.y;
            
            // Immediately set to the correct rotation
            player.transform.rotation = Quaternion.Euler(0, yRotation, 0);
        }
        
        private float NormalizeAngle(float angle)
        {
            // Convert 0-360 range to -180 to 180
            if (angle > 180f)
                return angle - 360f;
            return angle;
        }
        
        public override void Exit()
        {
            Debug.Log("Exiting Airborne State");
            
            // Complete any active trick when transitioning to ground
            if (isTrickActive)
            {
                CompleteTrick();
                LandTrick();
            }
            
            // Preserve horizontal velocity when landing
            Vector3 horizontalVel = player.Velocity;
            horizontalVel.y = 0;
            
            // Apply a minimal upward boost when landing to reduce that "sticky" feeling
            if (player.Velocity.y < 0)
            {
                float bounceStrength = Mathf.Min(-player.Velocity.y * 0.15f, 2.0f);
                player.Velocity = horizontalVel + Vector3.up * bounceStrength;
            }
            else
            {
                player.Velocity = horizontalVel;
            }
            
            // Completely zero out all rotation velocities
            player.AngularVelocity = Vector3.zero;
            player.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
            
            // Immediately stabilize to correct rotation
            StabilizeRotation();

            player.IsIgnoringGround = false;
            
            // Reset trick state
            isTrickActive = false;
        }
        
        // --- Updated Grind Detection Method ---
        private bool TryStickToRail()
        {
            // Use parameter for min speed
            float minGrindSpeed = player.PhysicsParams.GetValue(player.PhysicsParams.minGrindSpeed, player.IsSwitch);
            
            int railLayerMask = LayerMask.GetMask("Rail");
            RaycastHit bestHit = new RaycastHit();
            SplineGrindPath bestSplinePath = null;
            bool foundPotentialRail = false;
            float bestCombinedDistance = float.MaxValue;

            // Debug visualization of player position
            Debug.DrawRay(player.transform.position, Vector3.up * 0.5f, Color.white, 0.1f);

            // Test all ground feelers
            foreach (Vector3 feelerOffset in groundFeelerOffsets)
            {
                Vector3 feelerPos = player.transform.TransformPoint(feelerOffset);
                Debug.DrawLine(player.transform.position, feelerPos, Color.blue, 0.1f);

                foreach (Vector3 localDirection in rayDirections)
                {
                    Vector3 worldDirection = player.transform.TransformDirection(localDirection);
                    // --- Make DrawRay persistent for debugging --- 
                    Debug.DrawRay(feelerPos, worldDirection * RAIL_DETECTION_DISTANCE, Color.cyan, 1f); // Persistent Ray
                    // --------------------------------------------

                    if (Physics.Raycast(feelerPos, worldDirection, out RaycastHit hit, RAIL_DETECTION_DISTANCE, railLayerMask))
                    {
                        Debug.Log($"Raycast from {feelerPos} hit {hit.collider.name} at {hit.point}", hit.collider.gameObject);

                        // Try to get the SplineGrindPath component
                        SplineGrindPath splinePath = hit.collider.GetComponentInParent<SplineGrindPath>();
                        if (splinePath == null) 
                        {
                            Debug.LogWarning($"Raycast hit {hit.collider.name}, but NO SplineGrindPath component found in parent.", hit.collider.gameObject);
                        }
                        else if (splinePath != null)
                        {
                            Debug.Log($"SplineGrindPath component found on {splinePath.gameObject.name}", splinePath.gameObject);

                            // Calculate distances for prioritizing hits
                            Vector3 delta = hit.point - feelerPos;
                            float verticalDistance = Mathf.Abs(Vector3.Dot(delta, hit.normal));
                            float horizontalDistance = Vector3.ProjectOnPlane(delta, hit.normal).magnitude;
                            float combinedDistance = horizontalDistance + verticalDistance * 0.5f;

                            Debug.Log($"Distances: Vertical={verticalDistance:F3}, Horizontal={horizontalDistance:F3}, Combined={combinedDistance:F3} (MaxVert: {MAX_VERTICAL_OFFSET})");

                            // Debug visualization and basic filtering
                            if (verticalDistance <= MAX_VERTICAL_OFFSET || combinedDistance < RAIL_DETECTION_DISTANCE * 0.5f)
                            {
                                Debug.DrawLine(feelerPos, hit.point, Color.yellow, 0.1f);

                                if (worldDirection.y < -0.5f && verticalDistance > MAX_VERTICAL_OFFSET)
                                {
                                    continue; // Skip hits too far below if raying downwards
                                }

                                // Store the best potential hit based on combined distance
                                if (combinedDistance < bestCombinedDistance)
                                {
                                    bestCombinedDistance = combinedDistance;
                                    bestHit = hit;
                                    bestSplinePath = splinePath;
                                    foundPotentialRail = true;
                                }
                            }
                        }
                    }
                }
            }

            // If we found a potential rail, proceed with snapping and state change
            if (foundPotentialRail && bestSplinePath != null)
            {
                // Get the closest point ON THE SPLINE to the raycast hit point
                bool foundSplinePoint = bestSplinePath.GetNearestPoint(bestHit.point, out Vector3 closestSplinePoint, out Vector3 tangentAtSpline, out float distanceAlongSpline);
                
                // --- Add Log Here ---
                Debug.Log($"Airborne: GetNearestPoint result: {foundSplinePoint} for hit point {bestHit.point} on spline {bestSplinePath.name}");
                // -------------------- 

                if (foundSplinePoint)
                {
                    Debug.Log("Airborne: Found valid spline point."); // Log success
                    // Check entry angle and speed (similar to GrindDetector)
                    Vector3 playerVelocity = player.Velocity;
                    float playerSpeed = playerVelocity.magnitude;
                    Vector3 playerDirection = playerVelocity.normalized;
                    
                    // Log Speed Check
                    Debug.Log($"Airborne: Speed Check - Player Speed: {playerSpeed:F2}, Min Grind Speed: {minGrindSpeed}");
                    if (playerSpeed < minGrindSpeed) 
                    {
                        Debug.Log("Airborne: Failed Speed Check.");
                        return false; 
                    }
                    
                    float entryAngleThreshold = 60f; // Use a threshold (sync with SplineGrindPath or Detector?)
                    float angle = Vector3.Angle(playerDirection, tangentAtSpline);
                    float oppositeAngle = Vector3.Angle(playerDirection, -tangentAtSpline);
                    float minAngle = Mathf.Min(angle, oppositeAngle);
                    
                    // Log Angle Check
                    Debug.Log($"Airborne: Angle Check - PlayerDir: {playerDirection}, SplineTangent: {tangentAtSpline}, Min Angle: {minAngle:F2}, Threshold: {entryAngleThreshold}");
                    if (minAngle > entryAngleThreshold)
                    {
                         Debug.Log($"Airborne: Failed Angle Check.");
                         return false;
                    }

                    // Determine grind direction based on velocity vs tangent
                    bool reverseDirection = Vector3.Dot(playerDirection, tangentAtSpline) < 0;
                    Vector3 forwardDir = reverseDirection ? -tangentAtSpline.normalized : tangentAtSpline.normalized;

                    // --- Perform Snap (Similar to GrindDetector.BeginGrind) ---
                    // Snap position slightly above the calculated closest point on the spline
                    float boardHeightOffset = 0.1f; 
                    Vector3 newPosition = closestSplinePoint + Vector3.up * boardHeightOffset; 
                    player.transform.position = newPosition;
                    
                    // Snap rotation to align with the grind direction, keeping player upright
                    player.transform.rotation = Quaternion.LookRotation(forwardDir, Vector3.up);
                    
                    // --- Set Initial Velocity ---
                    float entrySpeed = Mathf.Max(
                        Mathf.Abs(Vector3.Dot(playerVelocity, forwardDir)), 
                        minGrindSpeed 
                    );
                    // Use parameter for boost multiplier
                    float boostMultiplier = player.PhysicsParams.GetValue(player.PhysicsParams.grindEntrySpeedBoostMultiplier, player.IsSwitch);
                    float boostedSpeed = entrySpeed * boostMultiplier; 
                    player.Velocity = forwardDir * boostedSpeed;

                    // --- Notify State Machine ---
                    Debug.Log($"Airborne: Starting grind on spline: {bestSplinePath.name}, distance: {distanceAlongSpline:F2}m");
                    player.SetCurrentRail(bestSplinePath); // Set the current rail BEFORE changing state
                    stateMachine.GrindingState.SetRailInfo(bestSplinePath, closestSplinePoint, forwardDir, distanceAlongSpline, bestHit.normal);
                    stateMachine.ChangeState(stateMachine.GrindingState);
                    return true;
                }
                else
                {
                     Debug.LogWarning("Airborne: Found rail but failed to get closest point on spline.", bestSplinePath);
                }
            }

            return false;
        }
        // --- End Updated Grind Detection Method ---
    }
} 