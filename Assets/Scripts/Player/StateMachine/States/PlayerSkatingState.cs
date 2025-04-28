using UnityEngine;
using BroSkater.Rails;

namespace BroSkater.Player.StateMachine
{
    // Renamed from PlayerGroundedState
    public class PlayerSkatingState : PlayerState
    {
        private const float MAX_JUMP_CHARGE_TIME = 1.0f; // <<< RE-ADDED

        // <<< ADDED: Flag for landing state >>>
        private bool applySwitch = false;

        // Jump mechanics
        private bool isJumpCharging = false;
        private float jumpChargeTime = 0f;
        
        // <<< Yaw rotation tracking for grounded state >>>
        private float yawRotationDelta = 0f;
        
        // Movement physics - most values now come from player stats
        private Vector3 moveDirection;
        private float currentSpeed; // Track speed separately
        private bool wasInAir = false; // Still needed for OnLanding logic
        
        // Landing transition tracking
        private bool isInLandingTransition = false;
        private float landingTransitionTimer = 0f;
        private const float LANDING_TRANSITION_TIME = 0.15f; 
        private Vector3 targetLandingDirection;
        
        // Manual detection
        // private bool isInManual = false; // Manual is its own state now
        // private float manualTimer = 0f;
        // private const float MANUAL_MIN_TIME = 0.25f; 

        // Standing/skating state
        // private bool isStandingStill = true; // REMOVED - This is the Skating state
        private const float MIN_SKATING_SPEED_THRESHOLD = 0.5f; // Speed below which we transition to StandingStill
        private const float PUSH_FORCE = 6.0f; // Force applied when transitioning from StandingStill
        
        // Ground rotation damping
        private const float GROUND_ROTATION_DAMPING = 20f; // Reduced damping for stability
        private const float MODEL_TILT_DAMPING = 15f; // Smoothing for model tilt
        
        // Updated rail detection parameters
        private const float RAIL_DETECTION_DISTANCE = 2.5f;  
        private const float MAX_VERTICAL_OFFSET = 2.0f;      
        private bool hasConsumedGrindInput = false;

        // --- Ground-Specific Feelers --- 
        // Only use lower feelers and primarily downward rays for ground detection
        private readonly Vector3[] groundStateFeelerOffsets = new Vector3[]
        {
            new Vector3(-0.3f, -0.15f, 0.4f),  // Front left
            new Vector3(0.0f, -0.15f, 0.4f),   // Front center
            new Vector3(0.3f, -0.15f, 0.4f),   // Front right
            new Vector3(-0.3f, -0.15f, 0.0f),  // Center left
            new Vector3(0.0f, -0.15f, 0.0f),   // Center
            new Vector3(0.3f, -0.15f, 0.0f),   // Center right
            new Vector3(-0.3f, -0.15f, -0.4f), // Back left
            new Vector3(0.0f, -0.15f, -0.4f),  // Back center
            new Vector3(0.3f, -0.15f, -0.4f),  // Back right
        };

        private readonly Vector3[] groundStateRayDirections = new Vector3[]
        {
            new Vector3(0, -1, 0),                           // Straight down
            new Vector3(0, -0.9f, 0.1f).normalized,          // Slightly forward-down
            new Vector3(0, -0.9f, -0.1f).normalized,         // Slightly backward-down
            new Vector3(0.1f, -0.9f, 0).normalized,          // Slightly right-down
            new Vector3(-0.1f, -0.9f, 0).normalized,         // Slightly left-down
            // Add a couple of slightly more outward rays for catching edges
            new Vector3(0.3f, -0.9f, 0).normalized, 
            new Vector3(-0.3f, -0.9f, 0).normalized,
        };
        // --- End Ground-Specific Feelers ---

        // Wheel and feeler offsets for rail detection (in local space)
        /*
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
        */

        // Multiple ray directions to cover different approaches
        /*
        private readonly Vector3[] rayDirections = new Vector3[]
        {
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
        */

        // Constructor - Renamed class
        public PlayerSkatingState(PlayerStateMachine stateMachine, PlayerController player) : base(stateMachine, player) { }

        public override void Enter()
        {
            Debug.Log("-----> ENTER: PlayerSkatingState"); // <<< ADDED LOG

            // <<< MOVED & REVISED: Stance switch check using PreviousState >>>
            if (stateMachine.PreviousState is AirborneState previousAirState) // Check if we came from Airborne
            {
                 float lastYaw = previousAirState.GetTotalYawRotation(); // Get final rotation
                 float safeZoneAngle = 45f; // Degrees around 0 and 180
                 float landingAngle = Mathf.DeltaAngle(0f, lastYaw); // Normalize to +/- 180
                 Debug.Log($"SkatingState Enter: Checking angle from Previous AirborneState. LastYaw={lastYaw:F1}, LandingAngle={landingAngle:F1}");

                 // Check Backward Zone (Switch Stance)
                 if (Mathf.Abs(Mathf.DeltaAngle(landingAngle, 180f)) <= safeZoneAngle)
                 {
                     Debug.Log("SkatingState Enter: Backward Zone detected. Applying Switch.");
                     applySwitch = true;
                 }
                  // Check Forward Zone (No Switch)
                 else if (Mathf.Abs(landingAngle) <= safeZoneAngle) 
                 {
                     Debug.Log("SkatingState Enter: Forward Zone detected. No Switch.");
                     applySwitch = false;
                 }
                  // Bail Zone (No Switch, bail should have been handled by Controller ideally)
                 else 
                 {
                     Debug.Log("SkatingState Enter: Bail Zone detected based on previous state angle. (No Switch)");
                     applySwitch = false;
                     // TODO: Could potentially force bail here if Controller missed it.
                 }
            }

            // Apply the switch if needed
            if (applySwitch)
            {
                player.ToggleSwitch(); // This updates IsSwitch and the UI text
            }
            // <<< END MOVED & REVISED >>>

            Debug.Log("Entering Skating State"); // Renamed log
            player.IsGrounded = true; 
            
            // Reset jump state
            isJumpCharging = false;
            jumpChargeTime = 0f;
            
            // Reset grind input state
            hasConsumedGrindInput = false;
            
            // Handle state entry based on previous state 
            if (stateMachine.PreviousState is AirborneState || stateMachine.PreviousState is PlayerVertAirState) // <<< CHECKING PREVIOUS STATE DIRECTLY
            {
                OnLanding();
                 Debug.Log($"SkatingState Enter (Post-Landing): Velocity={player.Velocity}, Speed={player.Velocity.magnitude:F2}"); // <<< ADDED LOG
            }
            else if (stateMachine.PreviousState is PlayerStandingStillState) // <<< CHECKING PREVIOUS STATE DIRECTLY
            {
                // Apply initial push force when coming from Standstill by directly setting velocity
                // player.AddForce(player.Forward * PUSH_FORCE, ForceMode.Impulse); // <<< REMOVED AddForce
                player.Velocity = player.Forward * PUSH_FORCE; // <<< SET VELOCITY DIRECTLY
                moveDirection = player.Forward; // Ensure move direction is set
                 Debug.Log($"SkatingState Enter (From Standstill): Set Initial Velocity={player.Velocity}, Speed={player.Velocity.magnitude:F2}"); // <<< UPDATED LOG
            }
            else
            {
                // Default continuation logic (e.g., from manual)
                OnGroundContinue();
            }
            
            // Initialize speed based on entry velocity projection
            currentSpeed = Vector3.ProjectOnPlane(player.Velocity, player.GetGroundNormal()).magnitude;
            
            // Manual state is handled separately
            // isInManual = false;
            // manualTimer = 0f;
            
            // isStandingStill flag removed
        }
        
        private void OnLanding()
        {
            // 1. Project velocity onto ground plane immediately
            Vector3 groundNormal = player.GetGroundNormal();
            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(player.Velocity, groundNormal);
            float currentSpeed = horizontalVelocity.magnitude;
            
            // <<< Get Stance Info BEFORE Blending >>>
            bool wasSwitchLanding = player.ShouldSwitchStance; // Check the flag BEFORE SkatingState.Enter resets it!
            // NOTE: This assumes OnLanding runs *before* the Enter method's stance switch logic.
            // If Enter runs first, this flag check won't work reliably here. Consider passing it from HandleTransitions.
            // For now, let's assume this timing works.

            // 2. Set initial move direction and blend with velocity
            if (currentSpeed > 1.0f)
            {
                // Get normalized directions
                Vector3 velocityDir = horizontalVelocity.normalized;
                Vector3 facingDir = player.Forward;
                
                // Calculate dot product to determine alignment
                float alignmentDot = Vector3.Dot(velocityDir, facingDir);
                
                // More strongly align with facing direction when landing
                float spinFactor = Mathf.Lerp(0.4f, 0.8f, player.statSpin / 10f);
                float facingBias = Mathf.Lerp(0.9f, 0.8f, currentSpeed / 15f);
                
                // Extra alignment for backward landings
                if (alignmentDot < 0)
                {
                    facingBias += 0.1f;
                    currentSpeed *= 0.8f;
                }
                
                // Apply spin stat influence
                facingBias = Mathf.Lerp(facingBias, 1.0f, spinFactor);
                
                // <<< ADJUSTMENT: Reduce facing bias significantly on switch landing >>>
                if (applySwitch) { 
                    facingBias *= 0.1f; // Heavily favor original velocity direction after 180
                    Debug.Log("Switch landing detected, reducing facing bias.");
                }
                // <<< END ADJUSTMENT >>>
                
                // Create blended direction - heavily biased toward facing
                Vector3 blendedDirection = Vector3.Lerp(velocityDir, facingDir, facingBias).normalized;
                
                // Apply the blended velocity with speed reduction
                float speedRetention = Mathf.Lerp(0.7f, 0.9f, alignmentDot * 0.5f + 0.5f);
                Vector3 newVelocity = blendedDirection * currentSpeed * speedRetention;
                
                // 3. Snap transform rotation to face movement direction and zero angular velocity
                player.transform.rotation = Quaternion.LookRotation(blendedDirection, Vector3.up);
                player.AngularVelocity = Vector3.zero;
                
                // Set velocity and move direction
                player.Velocity = newVelocity;
                moveDirection = blendedDirection;
                currentSpeed = newVelocity.magnitude; // Update tracked speed
                
                // Set up landing transition
                isInLandingTransition = true;
                landingTransitionTimer = 0f;
                targetLandingDirection = facingDir;
                
                Debug.Log($"Landing velocity: orig={currentSpeed:F1}, new={newVelocity.magnitude:F1}, bias={facingBias:F2}, retention={speedRetention:F2}");
            }
            else
            {
                // For very low speeds, align with facing and zero momentum
                moveDirection = player.Forward;
                player.transform.rotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                player.AngularVelocity = Vector3.zero;
                player.Velocity = moveDirection * currentSpeed * 0.7f;
                currentSpeed *= 0.7f; // Update tracked speed
            }
            
            // Apply stronger landing friction
            player.Velocity *= 0.98f; // Apply only a very minor damping on landing
            currentSpeed *= 0.98f; // Update tracked speed after damping
            
            // Clear the air flag
            wasInAir = false;
        }
        
        private void OnGroundContinue()
        {
            // Get horizontal velocity components
            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(player.Velocity, player.GetGroundNormal());
            
            // Keep horizontal velocity but apply some friction
            player.Velocity = horizontalVelocity * 0.95f;
            currentSpeed = player.Velocity.magnitude; // Update tracked speed
            
            // Set initial move direction based on velocity or facing
            if (horizontalVelocity.magnitude > 1.0f)
            {
                moveDirection = horizontalVelocity.normalized;
            }
            else
            {
                moveDirection = player.Forward;
            }
        }

        public override void LogicUpdate()
        {
            // Update landing transition if active
            if (isInLandingTransition)
            {
                UpdateLandingTransition();
                // <<< ADDED: Potentially check for stop during transition too? >>>
                // if (player.Velocity.magnitude < MIN_SKATING_SPEED_THRESHOLD)
                // {
                //     stateMachine.TransitionTo(stateMachine.StandingStillState);
                //     return;
                // }
            }
            
            // Ground detection <<< REMOVE THIS BLOCK
            // player.CheckGrounded();
            // 
            // // Check if we've lost ground contact
            // if (!player.IsGrounded)
            // {
            //     wasInAir = true;
            //     stateMachine.ChangeState(stateMachine.AirborneState);
            //     return;
            // }
            // <<< END REMOVAL

            // Handle jump input (ollie)
            // <<< ADDED LOG >>>
            Debug.Log($"[SkatingState] Checking HandleJumpInput. JumpInputDown={inputHandler.JumpInputDown}, isCharging={isJumpCharging}");
            HandleJumpInput();
            
            /* // <<< COMMENTED OUT FOR TESTING
            // Check for manual input
            float verticalInput = inputHandler.MoveInput.y;
            if (Mathf.Abs(verticalInput) > 0.8f && player.Speed > PlayerManualState.MIN_MANUAL_SPEED)
            {
                stateMachine.ChangeState(stateMachine.ManualState);
                return;
            }
            */ // <<< END COMMENT OUT

            // <<< ADDED: Check for stopping condition >>>
            float horizontalSpeed = new Vector3(player.Velocity.x, 0, player.Velocity.z).magnitude;
            
            // <<< ADDED LOG BEFORE CHECK >>>
            // Log input value for debugging braking/pushing
            Debug.Log($"Skating LogicUpdate: Input Y = {inputHandler.MoveInput.y:F2}"); 
            Debug.Log($"Skating LogicUpdate: Checking speed. TimeInState={stateMachine.TimeInCurrentState:F2}, horizontalSpeed={horizontalSpeed:F2}, Threshold={MIN_SKATING_SPEED_THRESHOLD}, isCharging={isJumpCharging}, isInLandingTrans={isInLandingTransition}");
            
            // <<< MODIFIED: Check threshold with <= and removed time constraint for debugging >>>
            if (horizontalSpeed <= MIN_SKATING_SPEED_THRESHOLD &&
                !isJumpCharging && 
                !isInLandingTransition)
            {
                 Debug.Log($"Skating speed {horizontalSpeed:F2} below/equal threshold {MIN_SKATING_SPEED_THRESHOLD}. Transitioning to Standing Still."); // Updated log
                 stateMachine.ChangeState(stateMachine.StandingStillState); 
                 return; // Transitioned
            }
        }
        
        private void UpdateLandingTransition()
        {
            // Increment timer
            landingTransitionTimer += Time.deltaTime;
            
            if (landingTransitionTimer < LANDING_TRANSITION_TIME)
            {
                // During the transition period, continue to align velocity with facing direction
                Vector3 horizontalVelocity = new Vector3(player.Velocity.x, 0, player.Velocity.z);
                float currentSpeed = horizontalVelocity.magnitude;
                
                if (currentSpeed > 0.5f)
                {
                    // Calculate progress through transition (0-1)
                    float transitionProgress = landingTransitionTimer / LANDING_TRANSITION_TIME;
                    
                    // Progressively strengthen alignment as we move through transition
                    // Start at 20% alignment, end at 60% alignment per frame
                    float alignFactor = Mathf.Lerp(0.2f, 0.6f, transitionProgress);
                    
                    // Blend current velocity toward the target direction
                    Vector3 currentDir = horizontalVelocity.normalized;
                    Vector3 blendedDir = Vector3.Lerp(currentDir, targetLandingDirection, alignFactor).normalized;
                    
                    // Apply the new velocity direction but maintain speed
                    player.Velocity = blendedDir * currentSpeed;
                    
                    // Update move direction to match the blended direction
                    moveDirection = blendedDir;
                }
            }
            else
            {
                // End the landing transition
                isInLandingTransition = false;
            }
        }

        // Modified to ONLY update moveDirection based on input, ROTATING AROUND NORMAL
        private void HandleRotation(float dt, Vector3 groundNormal) 
        {
            float rotationInput = inputHandler.MoveInput.x;

            // Calculate desired YAW rotation delta based on input
            yawRotationDelta = 0f; // Reset delta each frame
            if (Mathf.Abs(rotationInput) > 0.1f)
            {
                float speedFactor = Mathf.Clamp01(player.Velocity.magnitude / 5.0f); 
                float rotationSpeed = Mathf.Lerp(120f, 180f, player.statSpin / 10f);
                yawRotationDelta = rotationInput * rotationSpeed * dt; 
                yawRotationDelta *= Mathf.Lerp(1.0f, 0.7f, speedFactor); // Slow rotation slightly at higher speeds
            }
            
            // moveDirection will be updated AFTER yaw is applied in PhysicsUpdate
        }

        private void HandleJumpInput()
        {
            // --- DEBUG LOGS START ---
            // Debug.Log($"HandleJumpInput: JumpDown={inputHandler.JumpInputDown}, JumpHeld={inputHandler.JumpInputHeld}, JumpUp={inputHandler.JumpInputUp}, isCharging={isJumpCharging}");
            // --- DEBUG LOGS END ---

            if (inputHandler.JumpInputHeld && !isJumpCharging)
            {
                // Start charging jump
                Debug.Log("[SkatingState] Jump Charge Started (Triggered by Held).");
                isJumpCharging = true;
                jumpChargeTime = 0f;
                
                // Apply an immediate small boost when starting to charge
                // player.Velocity += moveDirection * 1.5f; // <<< Consider removing or tuning this boost
            }
            else if (inputHandler.JumpInputHeld && isJumpCharging)
            {
                // Continue charging jump
                jumpChargeTime += Time.deltaTime;
                // Cap charge time at maximum but don't automatically jump
                jumpChargeTime = Mathf.Min(jumpChargeTime, MAX_JUMP_CHARGE_TIME);
                 Debug.Log($"HandleJumpInput: Holding jump charge. Time: {jumpChargeTime:F2}"); // Add log
            }
            else if (inputHandler.JumpInputUp && isJumpCharging)
            {
                // Jump button released, execute jump with current charge
                Debug.Log($"[SkatingState] Jump button released. Executing jump with charge: {jumpChargeTime:F2}"); // Add log
                ExecuteJump();
                inputHandler.ConsumeJumpInputUp();
            }
            // <<< ADDED: Check if charge stopped unexpectedly >>>
            else if (!inputHandler.JumpInputHeld && isJumpCharging)
            {
                 Debug.LogWarning("HandleJumpInput: Jump charge stopped unexpectedly (Held is false but still isCharging=true). Resetting charge.");
                 isJumpCharging = false;
                 jumpChargeTime = 0f;
            }
        }
        
        private void ExecuteJump()
        {
            float chargeRatio = Mathf.Clamp01(jumpChargeTime / MAX_JUMP_CHARGE_TIME);
            
            // Calculate VERTICAL jump force
            // float baseJumpForce = player.PhysicsParams.GetValueFromStat(player.PhysicsParams.ollieJumpForce, player.statOllie, player.IsSwitch); // <<< OLD
            float baseVerticalForce = player.PhysicsParams.GetValueFromStat(player.PhysicsParams.ollieVerticalForce, player.statOllie, player.IsSwitch); // <<< NEW
            float verticalJumpForce = Mathf.Lerp(baseVerticalForce * 0.5f, baseVerticalForce, chargeRatio);
            
            // Calculate FORWARD boost 
            // Maybe scale forward boost by charge as well? Let's try it.
            float baseForwardBoost = player.PhysicsParams.GetValueFromStat(player.PhysicsParams.ollieForwardBoost, player.statOllie, player.IsSwitch); // Use Ollie stat for forward boost too?
            float forwardBoost = Mathf.Lerp(baseForwardBoost * 0.2f, baseForwardBoost, chargeRatio); // Scale forward boost from 20%-100%

            // Check if launching from vert for potentially different force (Applies mainly to vertical?)
            bool onVert = player.CheckVertexColor(player.LastGroundHit, player.PhysicsParams.vertColor);
            if (onVert)
            {
                // Use vert-specific VERTICAL force 
                float vertBaseForce = player.PhysicsParams.GetValueFromStat(player.PhysicsParams.vertJumpForce, player.statOllie, player.IsSwitch);
                verticalJumpForce = Mathf.Lerp(vertBaseForce * 0.5f, vertBaseForce, chargeRatio); 
                // Maybe zero out forward boost on vert launch?
                forwardBoost = 0f; 
                Debug.Log($"Using VERT Jump Force (Vertical Only): {verticalJumpForce}");
            }

            // Add more force if special is active (Apply to both? Let's try vertical mainly)
            if (player.IsSpecialActive())
            {
                verticalJumpForce *= 1.2f; 
                // forwardBoost *= 1.1f; // Optional: slight boost to forward too?
            }
            
            // Get current horizontal velocity (momentum)
            Vector3 currentHorizontalVel = Vector3.ProjectOnPlane(player.Velocity, Vector3.up);
            // float horizontalRetentionFactor = player.PhysicsParams.GetValue(player.PhysicsParams.jumpHorizontalRetention, player.IsSwitch); // <<< REMOVED Retention
            
            // Combine HORIZONTAL MOMENTUM + FORWARD BOOST + VERTICAL FORCE
            player.Velocity = currentHorizontalVel + (player.transform.forward * forwardBoost) + (Vector3.up * verticalJumpForce);
            
            // <<< ADD LOG >>>
            Debug.Log($"[Skating ExecuteJump] Applied Velocity: {player.Velocity} (VerticalForce: {verticalJumpForce:F2}, ForwardBoost: {forwardBoost:F2}, Momentum: {currentHorizontalVel})");
            
            player.IsGrounded = false; // <<< Force IsGrounded to false immediately
            player.IsIgnoringGround = true; // <<< SET FLAG
            
            // Reset jump variables
            isJumpCharging = false;
            jumpChargeTime = 0f;
            
            // Mark that we're going into air state
            wasInAir = true;
            
            // --- Transition to Correct Air State ---
            if (onVert)
            {
                Vector3 launchPos = player.transform.position;
                Debug.Log($"ExecuteJump (SkatingState): On VERT -> Transitioning to VertAirState. Launch Pos: {launchPos}");
                stateMachine.VertAirState.EnterWithVertLaunch(launchPos);
                stateMachine.ChangeState(stateMachine.VertAirState);
            }
            else
            {
                Debug.Log($"ExecuteJump (SkatingState): Not on VERT -> Transitioning to AirborneState");
                stateMachine.ChangeState(stateMachine.AirborneState);
            }
            // --- End Transition ---
        }
        
        public override void PhysicsUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 groundNormal = player.GetGroundNormal();

            // Handle Landing Transition if active
            if (isInLandingTransition)
            {
                UpdateLandingTransition();
                return; // Skip other physics during transition
            }

            // Apply smooth rotation to align with ground normal
            AlignToGround(dt, groundNormal);

            // Handle Rotation based on Input
            HandleRotation(dt, groundNormal);
            
            // <<< APPLY the calculated Yaw Rotation >>>
            if (Mathf.Abs(yawRotationDelta) > 0.001f)
            {
                player.transform.Rotate(Vector3.up * yawRotationDelta, Space.World);
                // Reset delta after applying to prevent accumulation if HandleRotation isn't called next frame
                // yawRotationDelta = 0f; // Optional: reset here or ensure HandleRotation always resets it first
            }
            // <<< END APPLY Yaw Rotation >>>

            // Apply forces and update velocity
            CalculateAndApplyVelocity(dt, groundNormal);

            // Update Angular Velocity (potentially dampening)
            ApplyAngularDamping(dt);
        }

        /// <summary>
        /// Smoothly aligns the player's up vector with the ground normal.
        /// </summary>
        private void AlignToGround(float dt, Vector3 groundNormal)
        {
            // Target rotation to align player's up vector with the ground normal
            Quaternion targetRotation = Quaternion.FromToRotation(player.transform.up, groundNormal) * player.transform.rotation;

            // Speed of rotation towards the target normal
            float alignmentSpeed = 15f; // Adjust this value for faster/slower alignment

            // Smoothly rotate towards the target rotation
            player.transform.rotation = Quaternion.RotateTowards(player.transform.rotation, targetRotation, alignmentSpeed * dt);
        }

        private void ApplyAngularDamping(float dt)
        {
            // Implement angular damping logic here
        }

        private void AlignMovementToVelocity()
        {
            // Get horizontal velocity
            Vector3 horizontalVelocity = new Vector3(player.Velocity.x, 0, player.Velocity.z);
            
            // If we have meaningful velocity, align movement direction to it
            if (horizontalVelocity.magnitude > 2.0f)
            {
                moveDirection = horizontalVelocity.normalized;
            }
            else
            {
                // Otherwise use current forward
                moveDirection = player.Forward;
            }
        }

        // Method to align visual models
        // <<< This method might be redundant now if main transform handles tilt >>>
        // private void AlignModelsToNormal(Vector3 groundNormal)
        // {
            // ... (Previous code) ...
        // }

        // --- Updated Grind Detection Method (Grounded) ---
        private bool TryStickToRail()
        {
            // Use parameter for min speed
            float minGrindSpeed = player.PhysicsParams.GetValue(player.PhysicsParams.minGrindSpeed, player.IsSwitch);
            
            int railLayerMask = LayerMask.GetMask("Rail");
            RaycastHit bestHit = new RaycastHit();
            SplineGrindPath bestSplinePath = null;
            bool foundPotentialRail = false;
            float bestCombinedDistance = float.MaxValue;

            Debug.DrawRay(player.transform.position, Vector3.up * 0.5f, Color.white, 0.1f);

            // Use Ground-Specific Arrays
            foreach (Vector3 feelerOffset in groundStateFeelerOffsets)
            {
                Vector3 feelerPos = player.transform.TransformPoint(feelerOffset);
                Debug.DrawLine(player.transform.position, feelerPos, Color.blue, 0.1f);

                foreach (Vector3 localDirection in groundStateRayDirections)
                {
                    Vector3 worldDirection = player.transform.TransformDirection(localDirection);
                    Debug.DrawRay(feelerPos, worldDirection * RAIL_DETECTION_DISTANCE, new Color(0.2f, 0.8f, 0.2f, 0.3f), 0.1f);

                    if (Physics.Raycast(feelerPos, worldDirection, out RaycastHit hit, RAIL_DETECTION_DISTANCE, railLayerMask))
                    {
                         // Try to get the SplineGrindPath component
                        SplineGrindPath splinePath = hit.collider.GetComponentInParent<SplineGrindPath>();
                        if (splinePath == null) 
                        {
                            Debug.LogWarning($"Raycast hit {hit.collider.name}, but NO SplineGrindPath component found in parent.", hit.collider.gameObject);
                        }
                        else if (splinePath != null)
                        {
                            Vector3 delta = hit.point - feelerPos;
                            float verticalDistance = Mathf.Abs(Vector3.Dot(delta, hit.normal));
                            float horizontalDistance = Vector3.ProjectOnPlane(delta, hit.normal).magnitude;
                            float combinedDistance = horizontalDistance + verticalDistance * 0.5f;

                            if (verticalDistance <= MAX_VERTICAL_OFFSET || combinedDistance < RAIL_DETECTION_DISTANCE * 0.5f)
                            {
                                Debug.DrawLine(feelerPos, hit.point, Color.yellow, 0.1f);
                                Debug.DrawRay(hit.point, hit.normal * 0.5f, Color.red, 0.1f);
                                Debug.Log($"Rail detected: vert={verticalDistance:F2}, horiz={horizontalDistance:F2}, speed={player.Velocity.magnitude:F2}");

                                if (worldDirection.y < -0.5f && verticalDistance > MAX_VERTICAL_OFFSET)
                                {
                                    continue;
                                }

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
                Debug.Log($"Grounded: GetNearestPoint result: {foundSplinePoint} for hit point {bestHit.point} on spline {bestSplinePath.name}");
                // --------------------

                if (foundSplinePoint)
                {
                    Debug.Log("Grounded: Found valid spline point."); // Log success
                    // Check entry angle and speed (similar to GrindDetector)
                    Vector3 playerVelocity = player.Velocity; // Use player velocity
                    float playerSpeed = playerVelocity.magnitude;
                    Vector3 playerDirection = playerVelocity.normalized;

                    // Log Speed Check
                    Debug.Log($"Grounded: Speed Check - Player Speed: {playerSpeed:F2}, Min Grind Speed: {minGrindSpeed}");
                    if (playerSpeed < minGrindSpeed)
                    {
                        Debug.Log("Grounded: Failed Speed Check.");
                        return false;
                    }

                    float entryAngleThreshold = 60f; // Use a threshold
                    float angle = Vector3.Angle(playerDirection, tangentAtSpline);
                    float oppositeAngle = Vector3.Angle(playerDirection, -tangentAtSpline);
                    float minAngle = Mathf.Min(angle, oppositeAngle);

                    // Log Angle Check
                    Debug.Log($"Grounded: Angle Check - PlayerDir: {playerDirection}, SplineTangent: {tangentAtSpline}, Min Angle: {minAngle:F2}, Threshold: {entryAngleThreshold}");
                    if (minAngle > entryAngleThreshold)
                    {
                        Debug.Log($"Grounded: Failed Angle Check.");
                        return false;
                    }

                    // Determine grind direction based on velocity vs tangent
                    bool reverseDirection = Vector3.Dot(playerDirection, tangentAtSpline) < 0;
                    Vector3 forwardDir = reverseDirection ? -tangentAtSpline.normalized : tangentAtSpline.normalized;

                    // --- Perform Snap (Similar to GrindDetector.BeginGrind) ---
                    float boardHeightOffset = 0.1f;
                    Vector3 newPosition = closestSplinePoint + Vector3.up * boardHeightOffset;
                    player.transform.position = newPosition;
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
                    Debug.Log($"Grounded: Starting grind on spline: {bestSplinePath.name}, distance: {distanceAlongSpline:F2}m");
                    stateMachine.GrindingState.SetRailInfo(bestSplinePath, closestSplinePoint, forwardDir, distanceAlongSpline, bestHit.normal);
                    stateMachine.ChangeState(stateMachine.GrindingState);
                    return true;
                }
                else
                {
                     Debug.LogWarning("Grounded: Found rail but failed to get closest point on spline.", bestSplinePath);
                }
            }

            return false;
        }

        // NEW METHOD for velocity control
        private void CalculateAndApplyVelocity(float dt, Vector3 groundNormal)
        {
            // Get velocity from the *previous* frame, projected onto the current ground plane
            Vector3 velocityLastFrame = player.Velocity;
            Vector3 velocityOnPlane = Vector3.ProjectOnPlane(velocityLastFrame, groundNormal);
            currentSpeed = velocityOnPlane.magnitude; // Speed based on last frame's momentum
            
            // --- Determine Current Movement Direction --- 
            // Start with player's facing direction as the primary intended direction
            moveDirection = Vector3.ProjectOnPlane(player.transform.forward, groundNormal).normalized;
            // If momentum is significant and roughly aligned, blend with it?
            // (Optional: Could make steering feel slightly heavier/more momentum-based)
            // if (currentSpeed > 1.0f) { ... blend moveDirection towards velocityOnPlane.normalized ... }
            
            // --- Calculate Target Speed Modifications based on Input/State --- 
            float targetSpeed = currentSpeed; // Start with current speed (momentum)
            float forwardInput = inputHandler.MoveInput.y;
            bool isBraking = forwardInput < -0.1f;
            // bool isPushing = forwardInput > 0.1f && !isJumpCharging; // <<< REMOVE PUSHING LOGIC

            // Physics parameters
            float maxSkateSpeed = player.PhysicsParams.GetValueFromStat(player.PhysicsParams.maxPushSpeed, player.statSpeed, player.IsSwitch); // Base speed cap
            float brakeDecelerationRate = player.PhysicsParams.GetValue(player.PhysicsParams.brakeForce, player.IsSwitch); 
            float baseDecelerationRate = player.PhysicsParams.GetValue(player.PhysicsParams.coastDeceleration, player.IsSwitch); // Gentle drag
            float maxChargeSpeed = player.PhysicsParams.GetValueFromStat(player.PhysicsParams.maxJumpChargeSpeed, player.statSpeed, player.IsSwitch);
            float accelerationRate = player.PhysicsParams.GetValueFromStat(player.PhysicsParams.acceleration, player.statAccel, player.IsSwitch); // <<< Fetch acceleration here too

            // <<< ADDED LOGS FOR PARAMETER VERIFICATION >>>
            Debug.Log($"[SkateParams] Fetched maxSkateSpeed: {maxSkateSpeed:F2} (from maxPushSpeed)");
            Debug.Log($"[SkateParams] Fetched maxChargeSpeed: {maxChargeSpeed:F2}");
            Debug.Log($"[SkateParams] Fetched accelerationRate: {accelerationRate:F2}");
            Debug.Log($"[SkateParams] Fetched brakeDecelerationRate: {brakeDecelerationRate:F2}");
            Debug.Log($"[SkateParams] Fetched baseDecelerationRate: {baseDecelerationRate:F2}");
            // <<< END LOGS >>>
            
            const float VELOCITY_LERP_FACTOR = 15f; 
            
            Vector3 finalVelocity;
            bool forceZeroVelocity = false; // <<< ADD Flag to bypass Lerp

            if (isJumpCharging)
            {
                // Apply boost based on charge
                float chargeRatio = Mathf.Clamp01(jumpChargeTime / MAX_JUMP_CHARGE_TIME);
                float baseBoost = player.PhysicsParams.GetValueFromStat(player.PhysicsParams.jumpChargeBoost, player.statAccel, player.IsSwitch);
                float chargeBoost = baseBoost * chargeRatio; 

                // Accelerate towards max charge speed (starting from current speed)
                targetSpeed = currentSpeed + chargeBoost * dt; 
                Debug.Log($"[SkateParams] Jump Charge Speed BEFORE Cap: {targetSpeed:F2} (MaxChargeSpeed Param: {maxChargeSpeed:F2})"); // Added max param
                targetSpeed = Mathf.Min(targetSpeed, maxChargeSpeed); // Cap speed
                Debug.Log($"[SkateParams] Jump Charge Speed AFTER Cap ({maxChargeSpeed:F2}): {targetSpeed:F2}");
                
                // Calculate target velocity DIRECTLY from facing direction and target speed
                // moveDirection is already set to player forward at the start of the function
                finalVelocity = moveDirection * targetSpeed; 

                Debug.Log($"CalcVel (Charging): Charge={chargeRatio:F2}, Boost={chargeBoost:F2}, CurrentSpeed={currentSpeed:F2}, TargetSpeed={targetSpeed:F2}, FinalVel={finalVelocity}");
            }
            else // Regular Skating (Automatic Forward Movement or Braking)
            {
                if (isBraking)
                {
                    targetSpeed -= brakeDecelerationRate * dt;
                    if (targetSpeed < MIN_SKATING_SPEED_THRESHOLD) 
                    {
                        targetSpeed = 0f;
                        forceZeroVelocity = true; // <<< SET flag if braking causes full stop
                    }
                    targetSpeed = Mathf.Max(0f, targetSpeed); 
                    Debug.Log($"CalcVel (Braking): CurrentSpeed={currentSpeed:F2}, TargetSpeed={targetSpeed:F2}, ForceZero={forceZeroVelocity}"); // Added flag to log
                }
                else 
                { 
                    // <<< LOG BEFORE MoveTowards >>>
                    Debug.Log($"[AutoSkate] BEFORE MoveTowards: currentSpeed={currentSpeed:F2}, maxSkateSpeed={maxSkateSpeed:F2}, accelRate={accelerationRate:F2}");
                    targetSpeed = Mathf.MoveTowards(currentSpeed, maxSkateSpeed, accelerationRate * dt); 
                    // <<< LOG AFTER MoveTowards >>>
                    Debug.Log($"[AutoSkate] AFTER MoveTowards: targetSpeed={targetSpeed:F2}");
                    
                    // Apply base deceleration always (friction/drag)
                     Debug.Log($"[AutoSkate] BEFORE Decel: targetSpeed={targetSpeed:F2}, baseDecel={baseDecelerationRate:F2}");
                    targetSpeed -= baseDecelerationRate * dt;
                    targetSpeed = Mathf.Max(0f, targetSpeed); // Ensure speed doesn't drop below zero
                    Debug.Log($"[AutoSkate] AFTER Decel: targetSpeed={targetSpeed:F2}");
                }
                
                // --- Update the tracked currentSpeed (Only for non-charging) ---
                currentSpeed = targetSpeed; // Update the persistent speed field for next frame's momentum

                // --- Calculate Final Velocity Vector based on CURRENT FACING direction ---
                // Use moveDirection (which was set to player.transform.forward at the start)
                finalVelocity = moveDirection * currentSpeed;
            }
            // --- END Input Modifications --- 

            // --- Apply Final Velocity (with Smoothing) --- 
            // player.Velocity = Vector3.Lerp(player.Velocity, finalVelocity, dt * VELOCITY_LERP_FACTOR); // <<< OLD
            if (forceZeroVelocity)
            {
                player.Velocity = Vector3.zero; // <<< Direct assignment if braking to stop
                Debug.Log("CalcVel (Final): Forced Zero Velocity due to braking."); // <<< ADDED LOG
            }
            else
            {
                player.Velocity = Vector3.Lerp(player.Velocity, finalVelocity, dt * VELOCITY_LERP_FACTOR); // <<< Use Lerp otherwise
                 Debug.Log($"CalcVel (Final): TargetVel={finalVelocity}, Applied Vel={player.Velocity}"); // Adjusted Log
            }
        }

        // private Vector3 _velocitySmoothRef; // Needed for SmoothDamp option

        public override void Exit()
        {
            Debug.Log("<----- EXIT: PlayerSkatingState"); // <<< ADDED LOG

            // Reset state variables
            isJumpCharging = false;
            jumpChargeTime = 0f;
            hasConsumedGrindInput = false;
            isInLandingTransition = false; // Ensure landing transition stops
            
            // Manual state logic removed
        }
    }
} 
