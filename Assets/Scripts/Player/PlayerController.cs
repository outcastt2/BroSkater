using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BroSkater.Player.StateMachine;
// using BroSkater.Player.States; // <<< REMOVE or COMMENT OUT
using BroSkater.Rails;
using UnityEngine.UI;

namespace BroSkater.Player
{
    /// <summary>
    /// Controls player movement and skateboard physics using a state machine approach based on THUG mechanics
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerStateMachine))] // Add requirement for PlayerStateMachine
    public class PlayerController : MonoBehaviour
    {
        [Header("Core Components")]
        [SerializeField] private Transform playerModel;
        [SerializeField] private Transform skateboardModel;
        public Transform PlayerModel => playerModel; // Public getter
        public Transform SkateboardModel => skateboardModel; // Public getter
        [SerializeField] private CharacterController characterController;
        [SerializeField] private LayerMask grindableLayers;
        public Rigidbody Rb { get; private set; }
        public PlayerInputHandler InputHandler { get; private set; }
        public PlayerStateMachine StateMachine { get; private set; }
        public BalanceMeter BalanceMeter { get; private set; }
        public CharacterController CharacterController => characterController;
        public LayerMask GrindableLayers => grindableLayers;
        public float Speed => velocity.magnitude;

        [Header("UI References")]
        public Text stanceText;

        [Header("Physics & Stats")]
        [SerializeField] private SkaterPhysicsParameters physicsParameters; // Assign the ScriptableObject asset here
        public SkaterPhysicsParameters PhysicsParams => physicsParameters; // Public getter

        [Header("Player Stats (0-10 Range)")]
        [Range(0, 10)] public float statAir = 5f;
        [Range(0, 10)] public float statRun = 5f; // For walking/running speed off board
        [Range(0, 10)] public float statOllie = 5f;
        [Range(0, 10)] public float statSpeed = 5f;
        [Range(0, 10)] public float statSpin = 5f;
        [Range(0, 10)] public float statFlipSpeed = 5f;
        [Range(0, 10)] public float statSwitch = 5f; // How well the skater performs in switch
        [Range(0, 10)] public float statRailBalance = 5f;
        [Range(0, 10)] public float statLipBalance = 5f;
        [Range(0, 10)] public float statManual = 5f;
        [Range(0, 10)] public float statAccel = 5f;        // Acceleration stat
        [Range(0, 10)] public float statGroundFriction = 5f; // Ground friction stat
        [Range(0, 10)] public float statSlopeFriction = 5f;  // Slope friction stat
        [Range(0, 10)] public float statMaxSpeed = 10f;
        [Range(0, 10)] public float statAcceleration = 8f;
        [Range(0, 10)] public float statFriction = 0.92f;

        [Header("Ground Detection")]
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float groundCheckDistance = 0.3f;
        [SerializeField] private Transform groundCheck;
        [SerializeField] private Transform frontGroundCheck;  // Front wheel check
        [SerializeField] private Transform backGroundCheck;   // Back wheel check
        [SerializeField] private Transform leftSideCheck;     // Left side check for rails
        [SerializeField] private Transform rightSideCheck;    // Right side check for rails
        public LayerMask GroundLayer => groundLayer;
        public Transform GroundCheck => groundCheck;
        public float GroundCheckDistance => groundCheckDistance;

        [Header("Ground Snap Settings")]
        [SerializeField] private float snapToGroundDistance = 0.7f; // How far below to check for ground to snap to
        [SerializeField] private float snapToGroundOffset = 0.05f; // How far above the hit point to snap the player

        // Physics parameters - calculated from stats
        private float topSpeed;
        private float acceleration;
        private float groundFriction;
        private float airFriction = 0.98f;
        private float slopeFriction;
        
        // Velocity storage - managed here instead of using Rigidbody directly for more control
        private Vector3 velocity;
        private Vector3 angularVelocity;

        // State Information
        public bool IsGrounded { get; set; }
        public bool IsSwitch { get; set; }
        public bool ShouldSwitchStance { get; set; } // Flag to trigger stance switch on landing
        public bool IsCrouching { get; set; }
        public bool IsGrinding { get; set; }
        public Vector3 Velocity { get { return velocity; } set { velocity = value; } }
        public Vector3 AngularVelocity { get { return angularVelocity; } set { angularVelocity = value; } }
        public Vector3 Forward => transform.forward;
        public Vector3 Right => transform.right;
        public Vector3 Up => transform.up;

        // Track previous grounded state for better transitions
        private bool wasGrounded = false;
        private float groundStickinessTimer = 0f; // Timer for allowing brief moments of being ungrounded
        private const float GROUND_STICKINESS_DURATION = 0.2f; // How long to stay sticky (Increased)
        
        // Store ground normal for slope handling
        private Vector3 groundNormal = Vector3.up;
        public RaycastHit LastGroundHit { get; private set; } // Store last hit for info
        
        // Current special state
        private bool specialActive = false;

        // Grind state flags
        private bool isGrinding = false;
        
        // Rail reference
        private SplineGrindPath currentRail;

        // Temporary storage for pending grind request data
        private SplineGrindPath pendingGrindRail = null;
        private Vector3 pendingGrindEntryPoint;
        private Vector3 pendingGrindEntryDirection;
        private float pendingGrindEntryDistance;
        private Vector3 pendingGrindHitNormal;
        private float landingYawRotation = 0f; // Store yaw from air state during landing check
        private bool physicalGroundCheckResult = false; // <<< ADDED: Stores the raw ground check result
        public bool IsIgnoringGround { get; set; } = false; // <<< ADDED Flag

        private void Awake()
        {
            Rb = GetComponent<Rigidbody>();
            InputHandler = GetComponent<PlayerInputHandler>();
            StateMachine = GetComponent<PlayerStateMachine>();
            // Create a new BalanceMeter of the correct type using our own PlayerController reference
            BalanceMeter = new BalanceMeter(this);

            // Ensure input handler exists
            if (InputHandler == null)
            {
                Debug.LogError("PlayerInputHandler component missing on PlayerController!", this);
            }

            // Ensure Physics Parameters are assigned
            if (physicsParameters == null)
            {
                Debug.LogError("SkaterPhysicsParameters ScriptableObject not assigned in PlayerController!", this);
            }

            // Set up the rigidbody - we'll handle most physics ourselves
            Rb.useGravity = false; // Disable gravity - we'll handle it
            Rb.interpolation = RigidbodyInterpolation.Interpolate;
            Rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            Rb.constraints = RigidbodyConstraints.FreezeRotation; // <<< REINSTATE this line

            // Create ground check if not assigned
            SetupFeelers();
            
            // Initialize velocity from rigidbody
            velocity = Rb.linearVelocity;
            angularVelocity = Vector3.zero;
        }

        private void SetupFeelers()
        {
            // Check if any essential feelers are missing and warn about it
            if (groundCheck == null)
            {
                Debug.LogWarning("Main GroundCheck Transform is not assigned! Please assign it in the Inspector.");
            }
            
            if (frontGroundCheck == null)
            {
                Debug.LogWarning("FrontGroundCheck Transform is not assigned! Please assign it in the Inspector.");
            }
            
            if (backGroundCheck == null)
            {
                Debug.LogWarning("BackGroundCheck Transform is not assigned! Please assign it in the Inspector.");
            }
            
            if (leftSideCheck == null)
            {
                Debug.LogWarning("LeftSideCheck Transform is not assigned! Please assign it in the Inspector.");
            }
            
            if (rightSideCheck == null)
            {
                Debug.LogWarning("RightSideCheck Transform is not assigned! Please assign it in the Inspector.");
            }
        }

        private void Start()
        {
            // Calculate initial physics parameters
            UpdatePhysicsParameters();
            UpdateStanceUI();
        }

        private void Update()
        {
            // Update physics parameters based on current stats
            UpdatePhysicsParameters();
            
            // Delegate Update logic to the current state
            StateMachine.CurrentState.LogicUpdate();
            
            // Update other components
            BalanceMeter.UpdateLogic();
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            
            // Store previous grounded state BEFORE check
            wasGrounded = IsGrounded;
            
            // Check ground state
            bool groundedResult = CheckGrounded();
            Debug.Log($"FixedUpdate: CheckGrounded Result = {groundedResult}, Ground Normal = {groundNormal}");
            
            // Handle state transitions if needed based on ground state change
            if (StateMachine != null)
            {
                HandleStateTransitions();
                
                // Delegate FixedUpdate logic to the current state
                if (StateMachine.CurrentState != null)
                {
                    Debug.Log($"FixedUpdate: BEFORE {StateMachine.CurrentState.GetType().Name}.PhysicsUpdate(). Player Velocity = {velocity}"); // Log BEFORE PhysicsUpdate
                    StateMachine.CurrentState.PhysicsUpdate();
                    Debug.Log($"FixedUpdate: AFTER {StateMachine.CurrentState.GetType().Name}.PhysicsUpdate(). Player Velocity = {velocity}"); // Log AFTER PhysicsUpdate
                }
            }
            
            // --- ADDED: Decrement Ground Stickiness Timer ---
            if (groundStickinessTimer > 0f)
            {
                groundStickinessTimer -= dt;
            }
            // --- END ADDED ---
            
            // ApplyGravity() call ADDED
            ApplyGravity(); 

            IntegrateVelocities(dt); // Applies player.velocity to Rb.linearVelocity
            if (Rb != null) {
                Debug.Log($"<FixedUpdate END> Rigidbody Velocity = {Rb.linearVelocity}");
            }
        }

        private void UpdatePhysicsParameters()
        {
            // Calculate physics parameters from stats
            // This mimics the GetScriptedStat() function from THUG
            float statBoost = specialActive ? 2.0f : 0.0f; // Special state boost
            
            // Map 0-10 range to appropriate physics values
            acceleration = MapStat(statAccel + statBoost, 5.0f, 20.0f);
            topSpeed = MapStat(statSpeed + statBoost, 5.0f, 15.0f);
            groundFriction = MapStat(10.0f - statGroundFriction, 0.2f, 0.5f); // Inverse: higher stat = less friction
            slopeFriction = MapStat(statSlopeFriction + statBoost, 0.1f, 0.9f);
        }
        
        private float MapStat(float stat, float minValue, float maxValue)
        {
            // Map stat from 0-10 range to min-max range
            return Mathf.Lerp(minValue, maxValue, Mathf.Clamp01(stat / 10.0f));
        }

        private void HandleStateTransitions()
        {
            if (StateMachine == null || StateMachine.CurrentState == null) return;

            if (StateMachine.CurrentState == StateMachine.GrindingState)
            {
                return; 
            }
            
            // float minAirTimeForLanding = 0.18f; // <<< REMOVED

            // Check using the raw physics ground check result
            bool shouldAttemptLanding = physicalGroundCheckResult && // Use physicalGroundCheckResult
                                      (StateMachine.CurrentState == StateMachine.AirborneState || StateMachine.CurrentState == StateMachine.VertAirState);
                                      // && StateMachine.TimeInCurrentState >= minAirTimeForLanding; // <<< REMOVED Time Check

            // Debug log for landing condition check
            if (StateMachine.CurrentState == StateMachine.AirborneState || StateMachine.CurrentState == StateMachine.VertAirState)
            {
                 // Debug.Log($"Landing Check: physicalGrd={physicalGroundCheckResult}, State={StateMachine.CurrentState.GetType().Name}, TimeInState={StateMachine.TimeInCurrentState:F2} >= MinAirTime={minAirTimeForLanding} -> ShouldLand={shouldAttemptLanding}"); // Updated Log
                 Debug.Log($"Landing Check: physicalGrd={physicalGroundCheckResult}, State={StateMachine.CurrentState.GetType().Name} -> ShouldLand={shouldAttemptLanding}"); // Simpler Log
            }
            // --- END LOGGING ---

            if (shouldAttemptLanding)
            {
                 // --- Store Landing Rotation --- 
                 // Reset flag first
                 ShouldSwitchStance = false; 
                 if (StateMachine.CurrentState is AirborneState currentAirStateForLanding) 
                 {
                     landingYawRotation = currentAirStateForLanding.GetTotalYawRotation();
                 }
                 else if (StateMachine.CurrentState is PlayerVertAirState currentVertAirStateForLanding) // <<< Corrected type name
                 {
                     landingYawRotation = currentVertAirStateForLanding.GetTotalYawRotation(); // Assuming VertAirState also has this method
                 }
                 else {
                     // Should not happen if shouldAttemptLanding is true, but reset as failsafe
                     landingYawRotation = 0f; 
                     Debug.LogWarning("Landing check: CurrentState was not AirborneState when expected!");
                 }
                 // --- End Store --- 
                 
                 // TODO: Implement a proper bail check/state transition here if not landing in safe zones.
                 // For now, we assume if we reach here, landing is allowed (flag is already set).
                 bool landNormally = true; // Assume landing is allowed if we get here.
                 
                 // <<< RE-ADDED: Vert Check >>> 
                 // Check if the landing surface is vert, needed for state decision below
                 bool isOnVert = CheckVertexColor(LastGroundHit, physicsParameters.vertColor);
                 Debug.Log($"HandleTransitions: isOnVert Check Result: {isOnVert}");
                 // <<< END RE-ADDED >>>
                 
                 // --- Proceed with Landing State Change ONLY if landNormally is true --- 
                 if (landNormally) 
                 {
                     // This block remains the same, determining target state based on vert
                     if (!isOnVert)
                     {
                          Debug.Log($"Transitioning: Landed on Non-Vert -> SkatingState");
                          StateMachine.ChangeState(StateMachine.SkatingState); 
                     }
                     else // Landed on Vert
                     {
                          // Go to the unified SkatingState, regardless of landing on vert
                          Debug.Log($"Transitioning: Landed on Vert -> SkatingState");
                          StateMachine.ChangeState(StateMachine.SkatingState); // <<< CHANGE TO SkatingState
                     }
                 }
                 // If landNormally is false (bail condition), no state change happens here
                 // The player might remain in AirborneState until CheckGrounded makes them fall, 
                 // or ideally, we'd transition to a BailedState above.

            }
            // --- Check for takeoff --- 
            else if (wasGrounded && !IsGrounded) // Took off from any grounded state
            {
                 // --- Reinstated Transition Logic ---
                 Debug.Log("Ground Lost -> Transitioning to Air");
                 RaycastHit launchSurfaceHit = LastGroundHit; 
                 bool launchedFromVert = CheckVertexColor(launchSurfaceHit, physicsParameters.vertColor);
                 
                 if (launchedFromVert)
                 {   
                     // Launched from SkatingState off a vert surface
                     Debug.Log($"Transitioning: Launched off Vert ({StateMachine.PreviousState?.GetType().Name}) -> PlayerVertAirState"); // Removed PlayerVertSkatingState from comment
                     StateMachine.VertAirState.EnterWithVertLaunch(transform.position); 
                     StateMachine.ChangeState(StateMachine.VertAirState); 
                 }
                 else
                 {   
                     // Launched from SkatingState off a non-vert surface
                     Debug.Log($"Transitioning: Took off from Non-Vert ({StateMachine.PreviousState?.GetType().Name}) -> AirborneState"); // Removed PlayerVertSkatingState from comment
                     StateMachine.ChangeState(StateMachine.AirborneState);
                 }
                 // --- End Reinstated Logic ---
            }
            // --- ADDED: Check Stickiness Timer Expiry --- 
            /* // <<< Keep COMMENTED OUT START
            else if (groundStickinessTimer <= 0f && !IsGrounded && StateMachine.CurrentState != StateMachine.AirborneState && StateMachine.CurrentState != StateMachine.PlayerVertAirState) // Check timer AND still not grounded AND not already in air
            { 
               // ... (Stickiness logic remains commented out) ...
            }
            */ // <<< Keep COMMENTED OUT END
            // --- END ADDED --- 
            // --- Check for rolling onto/off vert WHILE grounded --- 
            else if (IsGrounded) // Only check this if currently grounded
            {
                // *** Reset stickiness timer logic remains commented out ***
                
                // Check the current surface AFTER potential landing logic
                // bool currentlyOnVert = CheckVertexColor(LastGroundHit, physicsParameters.vertColor); // <<< COMMENT OUT VERT CHECK
                
                // --- REMOVE/COMMENT OUT PlayerVertSkatingState Transitions ---
                /* // Start Comment Out
                // If in SkatingState and now on vert -> PlayerVertSkatingState
                if (StateMachine.CurrentState == StateMachine.SkatingState && currentlyOnVert)
                {
                    Debug.Log("Transitioning: Rolled onto Vert -> PlayerVertSkatingState");
                    StateMachine.ChangeState(StateMachine.PlayerVertSkatingState);
                }
                // If in PlayerVertSkatingState and now NOT on vert -> SkatingState
                else if (StateMachine.CurrentState == StateMachine.PlayerVertSkatingState && !currentlyOnVert)
                {
                    Debug.Log("Transitioning: Rolled off Vert -> SkatingState");
                    StateMachine.ChangeState(StateMachine.SkatingState);
                }
                */ // End Comment Out
                // --- END REMOVAL ---
            }
            // If we enter a grind (ensure not already grinding or in vert)
            else if (pendingGrindRail != null && // Check pending request
                     !IsGrinding &&              // Ensure not already grinding 
                     StateMachine.CurrentState != StateMachine.GrindingState && 
                     StateMachine.CurrentState != StateMachine.VertAirState)
            {
                Debug.Log($"Transitioning: Grind Requested -> GrindingState (Rail: {pendingGrindRail.name})");
                // Set IsGrinding flag *before* changing state
                IsGrinding = true; 
                // Pass the pending data to the state's specific Enter method
                StateMachine.GrindingState.Enter(
                    pendingGrindRail, 
                    pendingGrindEntryPoint, 
                    pendingGrindEntryDirection, 
                    pendingGrindEntryDistance, 
                    pendingGrindHitNormal
                );
                // Change the state in the state machine
                StateMachine.ChangeState(StateMachine.GrindingState); 
                
                // CRITICAL: Clear the pending request now that it's being processed
                pendingGrindRail = null;
            }
            // If we exit a grind (This condition might be redundant if Exit handlers call OnEndGrind)
            // Let's comment it out for now, as CleanupGrindState should handle the IsGrinding flag.
            /*
            else if (IsGrinding && StateMachine.CurrentState == StateMachine.GrindingState && currentRail == null) 
            {
                 Debug.Log($"Transitioning: Ended Grind (currentRail is null) -> AirborneState");
                IsGrinding = false; // Reset flag HERE
                StateMachine.ChangeState(StateMachine.AirborneState); 
            }
            */
        }

        private void IntegrateVelocities(float dt)
        {
            // Apply our calculated velocity to the rigidbody
            Rb.linearVelocity = velocity;
            
            // Integrate angular velocity into rotation
            if (angularVelocity.magnitude > 0.01f)
            {
                // Use proper Euler angle integration instead of quaternion math
                // This is more intuitive for human-readable angles
                Vector3 rotationDelta = angularVelocity * dt;
                
                // Apply rotation
                transform.Rotate(rotationDelta, Space.Self);
                
                // Apply angular drag
                angularVelocity *= Mathf.Pow(airFriction, dt);
                
                // Debug rotation amounts
                if (Mathf.Abs(angularVelocity.y) > 0.1f)
                {
                    Debug.Log($"Rotating at: {angularVelocity.y} deg/s, Applied rotation: {rotationDelta.y} degrees");
                }
            }
        }

        /// <summary>
        /// Rotates the skateboard model based on turning input and state
        /// </summary>
        private void RotateSkateboardModel()
        {
            float horizontalInput = InputHandler != null ? InputHandler.MoveInput.x : 0f;

            if (IsGrounded)
            {
                // Tilt the board based on turning input
                float tiltAngle = -horizontalInput * 15f;
                skateboardModel.localRotation = Quaternion.Lerp(
                    skateboardModel.localRotation,
                    Quaternion.Euler(0, 0, tiltAngle),
                    Time.deltaTime * 5f
                );
            }
            else
            {
                // Reset tilt when in air, showing board rotation
                // Actual board flipping is handled by state machine
                skateboardModel.localRotation = Quaternion.Lerp(
                    skateboardModel.localRotation,
                    Quaternion.identity,
                    Time.deltaTime * 3f
                );
            }
        }

        /// <summary>
        /// Performs comprehensive ground checks using multiple feelers
        /// </summary>
        public bool CheckGrounded()
        {
            // <<< ADDED: Immediately return false if actively ignoring ground after an ollie >>>
            if (IsIgnoringGround)
            {
                 Debug.Log("[CheckGrounded] Ignoring ground check (IsIgnoringGround=true) - Returning false.");
                 // Ensure internal flags are also false
                 physicalGroundCheckResult = false; 
                 IsGrounded = false; 
                 return false; 
            }
            // <<< END ADDED >>>

            // <<< REMOVED: Initial return false if in air state block >>>
            // if (StateMachine != null && (StateMachine.CurrentState == StateMachine.AirborneState || StateMachine.CurrentState == StateMachine.PlayerVertAirState)) 
            // {
            //      Debug.Log("[CheckGrounded] Currently in Air State - Returning false.");
            //      IsGrounded = false; 
            //      return false; 
            // }
            
            bool frontHitResult = false;
            bool backHitResult = false;
            bool centerHitResult = false;
            RaycastHit frontHitInfo = new RaycastHit();
            RaycastHit backHitInfo = new RaycastHit();
            RaycastHit centerHitInfo = new RaycastHit();
            RaycastHit combinedHitInfo = new RaycastHit(); // To store the most relevant hit
            
            float sphereRadius = 0.1f; // Radius for sphere cast
            float effectiveDistance = groundCheckDistance - sphereRadius; // Adjust distance for sphere radius
            if (effectiveDistance < 0) effectiveDistance = 0.01f; // Ensure distance is positive

            // SphereCast down from front and back wheels
            if (frontGroundCheck != null)
            {
                // Cast sphere straight down (World Space)
                frontHitResult = Physics.SphereCast(frontGroundCheck.position + transform.up * sphereRadius, sphereRadius, Vector3.down, out frontHitInfo, effectiveDistance, groundLayer, QueryTriggerInteraction.Ignore);
                Debug.DrawRay(frontGroundCheck.position, Vector3.down * groundCheckDistance, frontHitResult ? Color.green : Color.red);
            }
            if (backGroundCheck != null)
            {
                 // Cast sphere straight down (World Space)
                 backHitResult = Physics.SphereCast(backGroundCheck.position + transform.up * sphereRadius, sphereRadius, Vector3.down, out backHitInfo, effectiveDistance, groundLayer, QueryTriggerInteraction.Ignore);
                 Debug.DrawRay(backGroundCheck.position, Vector3.down * groundCheckDistance, backHitResult ? Color.green : Color.red);
            }

            // Use main ground check sphere cast if others fail or aren't assigned
             if (groundCheck != null && (!frontHitResult && !backHitResult))
            {
                // Cast sphere straight down (World Space)
                centerHitResult = Physics.SphereCast(groundCheck.position + transform.up * sphereRadius, sphereRadius, Vector3.down, out centerHitInfo, effectiveDistance, groundLayer, QueryTriggerInteraction.Ignore);
                Debug.DrawRay(groundCheck.position, Vector3.down * groundCheckDistance, centerHitResult ? Color.blue : Color.yellow);
            }

            // Determine current grounded status
            bool currentlyGrounded = frontHitResult || backHitResult || centerHitResult;

            if (currentlyGrounded)
            {
                // Prioritize wheel hits for normal calculation
                if (frontHitResult && backHitResult)
                {
                    groundNormal = (frontHitInfo.normal + backHitInfo.normal).normalized;
                    combinedHitInfo = frontHitInfo; // Use front hit as primary info source
                }
                else if (frontHitResult)
                {
                    groundNormal = frontHitInfo.normal;
                    combinedHitInfo = frontHitInfo;
                }
                else if (backHitResult)
                {
                    groundNormal = backHitInfo.normal;
                    combinedHitInfo = backHitInfo;
                }
                else // Only center hit
                {
                    groundNormal = centerHitInfo.normal;
                    combinedHitInfo = centerHitInfo;
                }
                LastGroundHit = combinedHitInfo; // Store the hit info
            }
            else // Initial SphereCasts Failed
            {
                // --- ADDED: Snap To Ground Logic ---
                RaycastHit snapHit;
                if (groundCheck != null && Physics.Raycast(groundCheck.position, Vector3.down, out snapHit, snapToGroundDistance, groundLayer, QueryTriggerInteraction.Ignore))
                {
                    Debug.Log("SnapToGround SUCCESS - Found ground within snap distance.");
                    currentlyGrounded = true; // <<< Snap means we consider ourselves physically grounded now
                    groundNormal = snapHit.normal; 
                    LastGroundHit = snapHit; 

                    Vector3 snapPosition = snapHit.point + Vector3.up * snapToGroundOffset;
                    
                    if (StateMachine.CurrentState != StateMachine.AirborneState && StateMachine.CurrentState != StateMachine.VertAirState)
                    {
                        Rb.MovePosition(new Vector3(transform.position.x, snapPosition.y, transform.position.z));
                        Debug.Log("SnapToGround applied Rb.MovePosition (Was not in AirState).");
                    }
                    else
                    {
                        Debug.Log("SnapToGround DETECTED ground, but SKIPPED Rb.MovePosition (Was in AirState).");
                    }
                }
                else
                {
                    Debug.Log("SnapToGround FAILED");
                    if (!currentlyGrounded) { // Only reset if the initial spherecasts also failed
                        groundNormal = Vector3.up;
                    }
                }
            }
            
            // <<< STORE raw physics result >>>
            physicalGroundCheckResult = currentlyGrounded; 

            // Set the public IsGrounded property
            IsGrounded = physicalGroundCheckResult;
            
            // <<< RE-ADD override IF still needed, but maybe not with better landing logic? >>>
            // Let's keep it commented out for now and rely on HandleStateTransitions minAirTime.
            // if (StateMachine != null && (StateMachine.CurrentState == StateMachine.AirborneState || StateMachine.CurrentState == StateMachine.VertAirState)) // <<< CORRECTED COMPARISON
            // {
            //      IsGrounded = false; // Force public flag false while airborne state is active
            // }

            if (wasGrounded != IsGrounded) // Log change in the public flag
            {
                Debug.Log($"Ground State Changed (Public IsGrounded): {wasGrounded} -> {IsGrounded}");
            }

            return IsGrounded; // Return the potentially overridden public value
        }
        
        /// <summary>
        /// Checks if the surface the player just left was a vert surface.
        /// Should be called during the transition from grounded to airborne.
        /// </summary>
        private bool CheckForVertLaunch(out RaycastHit vertHitInfo)
        {
            vertHitInfo = new RaycastHit();
            if (wasGrounded) // Only check if we were just grounded
            {
                // Use the LastGroundHit information from the previous frame
                if (LastGroundHit.collider != null)
                {
                    if (CheckVertexColor(LastGroundHit, physicsParameters.vertColor))
                    {                        
                        vertHitInfo = LastGroundHit;
                        //Debug.Log($"Vert Launch Detected! Normal: {vertHitInfo.normal}, Point: {vertHitInfo.point}");
                        return true;
                    }
                }
            }
            return false;
        }
        
        /// <summary>
        /// Utility function to check vertex color of a hit triangle.
        /// Moved here from PlayerVertAirState for broader use.
        /// </summary>
        public bool CheckVertexColor(RaycastHit hit, Color targetColor)
        {
            MeshCollider meshCollider = hit.collider as MeshCollider;
            if (meshCollider == null || meshCollider.sharedMesh == null)
            {
                return false; // Not a mesh or no shared mesh
            }

            Mesh mesh = meshCollider.sharedMesh;
            if (!mesh.isReadable)
            {
                // This check is important! Mesh needs Read/Write enabled in import settings.
                Debug.LogError($"Mesh '{mesh.name}' on GameObject '{hit.collider.gameObject.name}' is not readable. Enable Read/Write in Import Settings.", meshCollider.gameObject);
                return false; 
            }
            
            if (mesh.colors == null || mesh.colors.Length == 0)
            {
                // Allow check to pass if mesh has no colors? Or treat as false?
                // Forcing vert/grind requires colors. 
                // Debug.LogWarning($"Mesh {mesh.name} on {hit.collider.gameObject.name} does not have vertex colors.");
                return false; // No vertex colors on the mesh
            }

            // Get the vertices of the hit triangle
            int[] triangles = mesh.triangles;
            int triIndex = hit.triangleIndex;
            
            // Basic bounds check for triangle index
             if (triIndex < 0 || triIndex * 3 + 2 >= triangles.Length) 
            { 
                // This can happen if the hit info is invalid or stale
                // Debug.LogError($"Invalid triangle index {triIndex} for mesh {mesh.name}. Triangle array size: {triangles.Length}");
                return false; 
            }

            int v0Index = triangles[triIndex * 3];
            int v1Index = triangles[triIndex * 3 + 1];
            int v2Index = triangles[triIndex * 3 + 2];
            
            // Bounds check for vertex indices against color array size
            if (v0Index >= mesh.colors.Length || v1Index >= mesh.colors.Length || v2Index >= mesh.colors.Length)
            {
                 Debug.LogError($"Vertex index out of bounds for vertex colors array on mesh {mesh.name}. Indices:({v0Index},{v1Index},{v2Index}), Colors array size: {mesh.colors.Length}, Triangle Index: {triIndex}");
                 return false;
            }

            // Check if any vertex color matches the target color (within tolerance)
            Color[] colors = mesh.colors;
            float tolerance = 0.01f; // Allow for minor floating point differences
            bool match = (Mathf.Abs(colors[v0Index].r - targetColor.r) < tolerance && Mathf.Abs(colors[v0Index].g - targetColor.g) < tolerance && Mathf.Abs(colors[v0Index].b - targetColor.b) < tolerance) ||
                         (Mathf.Abs(colors[v1Index].r - targetColor.r) < tolerance && Mathf.Abs(colors[v1Index].g - targetColor.g) < tolerance && Mathf.Abs(colors[v1Index].b - targetColor.b) < tolerance) ||
                         (Mathf.Abs(colors[v2Index].r - targetColor.r) < tolerance && Mathf.Abs(colors[v2Index].g - targetColor.g) < tolerance && Mathf.Abs(colors[v2Index].b - targetColor.b) < tolerance);
            
            return match;
        }

        /// <summary>
        /// Apply slope forces to velocity
        /// </summary>
        public void ApplySlopeForces()
        {
            // --- MODIFIED: Only apply if NOT Grounded --- 
            // Grounded states will handle velocity projection themselves.
            // This prevents unwanted downward pull when skating uphill.
            if (!IsGrounded && groundNormal != Vector3.up)
            {
                Debug.LogWarning("ApplySlopeForces RUNNING (Should only happen if NOT Grounded!)"); // Add Warning Log
                // Original logic remains, but now only for non-grounded states (if ever needed)
                Vector3 slopeDir = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
                Vector3 currentVelDir = Vector3.ProjectOnPlane(velocity, groundNormal).normalized;

                // Check if moving generally upwards on the slope or charging jump
                bool movingUpSlope = Vector3.Dot(currentVelDir, slopeDir) < -0.1f;
                // bool chargingJump = (InputHandler != null && InputHandler.JumpInputHeld);
                
                // // --- MODIFIED: Only apply downward force if NOT moving up slope AND NOT charging jump ---
                // if (!movingUpSlope /* && !chargingJump */) // Jump charge check removed, handled by state
                // {
                    // Calculate gravity component down the slope
                    float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);
                    float slopeForce = 9.81f * Mathf.Sin(slopeAngle * Mathf.Deg2Rad);
                    
                    // Apply force based on slope friction (lower values = more slide)
                    Vector3 slideForce = slopeDir * slopeForce * (1.0f - slopeFriction);
                    velocity += slideForce * Time.fixedDeltaTime;
                // }
            }
            // Optional: Log when skipped
            // else if (IsGrounded) { Debug.Log("ApplySlopeForces SKIPPED (Player is Grounded)"); }
        }

        /// <summary>
        /// Reset the player to a specific position and rotation
        /// </summary>
        public void ResetPlayer(Vector3 position, Quaternion rotation)
        {
            // Reset state machine
            StateMachine.ChangeState(StateMachine.StandingStillState);
            BalanceMeter.StopBalance();

            // Reset velocities
            velocity = Vector3.zero;
            angularVelocity = Vector3.zero;
            Rb.linearVelocity = Vector3.zero;
            Rb.angularVelocity = Vector3.zero;

            // Set position and rotation
            transform.position = position;
            transform.rotation = rotation;

            // Reset state flags
            IsGrounded = false;
            IsSwitch = false;
            IsCrouching = false;
            wasGrounded = false;
            ShouldSwitchStance = false; // Reset on player reset
        }
        
        /// <summary>
        /// Get the ground normal at the player's position
        /// </summary>
        public Vector3 GetGroundNormal()
        {
            return groundNormal;
        }
        
        /// <summary>
        /// Activate or deactivate special mode
        /// </summary>
        public void SetSpecialActive(bool active)
        {
            specialActive = active;
            
            // Recalculate physics parameters
            UpdatePhysicsParameters();
        }
        
        /// <summary>
        /// Check if special mode is active
        /// </summary>
        public bool IsSpecialActive()
        {
            return specialActive;
        }

        public void Jump()
        {
            // Ensure we have a state machine reference
            if (StateMachine == null) 
            { 
                Debug.LogError("StateMachine reference is null in PlayerController.Jump()");
                return;
            }
            
            // Calculate Ollie Force (Use Vertical Force now)
            float verticalOllieForce = physicsParameters.GetValueFromStat(physicsParameters.ollieVerticalForce, statOllie, IsSwitch, physicsParameters.standardSwitchMultiplier);
            
            // Apply vertical velocity immediately
            velocity.y = verticalOllieForce;
            
            // Set IsGrounded to false - CheckGrounded will verify next frame
            IsGrounded = false;
            
            // --- ADDED: Explicitly change to AirborneState --- 
            // Check if the surface we *were* on was vert
            RaycastHit launchSurfaceHit;
            bool launchedFromVert = CheckForVertLaunch(out launchSurfaceHit);

            if (launchedFromVert)
            {   
                // If launched from vert, enter PlayerVertAirState
                Debug.Log("Jump() -> Transitioning to PlayerVertAirState");
                StateMachine.VertAirState.EnterWithVertInfo(launchSurfaceHit.normal, launchSurfaceHit.point); 
                StateMachine.ChangeState(StateMachine.VertAirState); 
            }
            else
            {   
                // Otherwise, enter normal AirborneState
                Debug.Log("Jump() -> Transitioning to AirborneState");
                StateMachine.ChangeState(StateMachine.AirborneState);
            }
            // ------------------------------------------------
        }

        public void ApplyGravity()
        {
            // Only apply gravity if not grounded and not grinding
            if (!IsGrounded && !IsGrinding) // isGrinding flag check
            {
                // <<< Log ADDED >>>
                Debug.Log($"ApplyGravity: Applying gravity. IsGrounded={IsGrounded}, IsGrinding={IsGrinding}. Current Vel Y: {Velocity.y}");
                Velocity += Physics.gravity * Time.fixedDeltaTime;
            }
            // <<< Added Log >>>
            else if (IsGrounded)
            {
                 Debug.Log($"ApplyGravity SKIPPED because IsGrounded=True. CurrentState={StateMachine.CurrentState?.GetType().Name}");
            }
            // <<< ADDED Log for Skipped due to Grinding >>>
            else if (IsGrinding)
            {
                 Debug.Log($"ApplyGravity SKIPPED because IsGrinding=True. CurrentState={StateMachine.CurrentState?.GetType().Name}");
            }
            // <<< END ADDED Log >>>
        }

        public void AddForce(Vector3 force, ForceMode mode = ForceMode.Force)
        {
            if (Rb != null)
            {
                Rb.AddForce(force, mode);
            }
        }

        public void SetVelocity(Vector3 velocity)
        {
            if (Rb != null)
            {
                Rb.linearVelocity = velocity;
            }
        }

        public Vector3 GetVelocity()
        {
            return Rb != null ? Rb.linearVelocity : Vector3.zero;
        }

        public void ToggleSwitch()
        {
            IsSwitch = !IsSwitch;
            UpdateStanceUI();
        }

        /// <summary>
        /// Called by detectors (GrindDetector, Lip Check) to signal a potential grind.
        /// Stores the necessary info for HandleStateTransitions to process.
        /// </summary>
        public void RequestGrind(SplineGrindPath rail, Vector3 entryPoint, Vector3 entryDirection, float entryDistance, Vector3 hitNormal)
        {
            // Store the details for the state machine to potentially use next FixedUpdate
            pendingGrindRail = rail;
            pendingGrindEntryPoint = entryPoint;
            pendingGrindEntryDirection = entryDirection.normalized; // Ensure normalized
            pendingGrindEntryDistance = entryDistance;
            pendingGrindHitNormal = hitNormal;
             Debug.Log($"Grind Requested: Rail={rail.name}, Point={entryPoint}, Dir={pendingGrindEntryDirection}, Dist={entryDistance}");
        }

        /// <summary>
        /// Called when player begins a grind - Primarily used by TryStickToRail
        /// DEPRECATED? We now use RequestGrind. Keep for now if other systems use it.
        /// </summary>
        public void OnBeginGrind(SplineGrindPath rail)
        {
            Debug.LogWarning("OnBeginGrind(rail) called - Should use RequestGrind(...) with full details.");
            // SetCurrentRail(rail); // Avoid setting currentRail directly here now
        }
        
        /// <summary>
        /// Called when player exits a grind - Called by GrindingState.CleanupGrindState()
        /// </summary>
        public void OnEndGrind()
        {
             Debug.Log("PlayerController.OnEndGrind called - Clearing grind state.");
            // Explicitly clear any pending request that might not have been processed
            pendingGrindRail = null; 
            // Clear the legacy currentRail reference if still used
            currentRail = null; 
             // Ensure the IsGrinding flag is reset (Already done in CleanupGrindState, but safe to do again)
            IsGrinding = false;
        }
        
        /// <summary>
        /// Update the reference to the current rail
        /// </summary>
        public void SetCurrentRail(SplineGrindPath rail)
        {
            currentRail = rail;
        }
        
        /// <summary>
        /// Get the current rail
        /// </summary>
        public SplineGrindPath GetCurrentRail()
        {
            return currentRail;
        }

        /// <summary>
        /// Updates the stance UI text based on the IsSwitch flag.
        /// </summary>
        public void UpdateStanceUI()
        {
            if (stanceText != null)
            {
                stanceText.text = IsSwitch ? "Stance: Switch" : "Stance: Regular";
            }
            else
            {
                // Optional: Warn if the text component isn't assigned
                // Debug.LogWarning("Stance Text UI component not assigned in PlayerController Inspector!");
            }
        }

        // --- ADDED: Collision Re-enable Logic ---
        public void RequestCollisionReEnable(float delay)
        {
            StartCoroutine(ReEnableCollisionsAfterDelay(delay));
        }

        private IEnumerator ReEnableCollisionsAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            int playerLayer = gameObject.layer;
            int groundLayer = LayerMask.NameToLayer("Ground"); // Adjust if different
            int railLayer = LayerMask.NameToLayer("Rail");   // Adjust if different

            if (playerLayer != -1 && groundLayer != -1)
            {
                Physics.IgnoreLayerCollision(playerLayer, groundLayer, false);
                Debug.Log($"Re-enabled collisions between Player layer ({LayerMask.LayerToName(playerLayer)}) and Ground layer ({LayerMask.LayerToName(groundLayer)}) after {delay}s delay.");
            }
             if (playerLayer != -1 && railLayer != -1)
            {
                 Physics.IgnoreLayerCollision(playerLayer, railLayer, false);
                 Debug.Log($"Re-enabled collisions between Player layer ({LayerMask.LayerToName(playerLayer)}) and Rail layer ({LayerMask.LayerToName(railLayer)}) after {delay}s delay.");
            }
        }
        // --- END ADDED ---
    }
} 