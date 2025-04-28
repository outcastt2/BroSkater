using UnityEngine;

namespace BroSkater.Player.StateMachine
{
    public class PlayerVertAirState : PlayerState
    {
        // Store the horizontal position to lock onto
        private Vector3 lockedXZPosition;
        
        // Tunable gravity (can copy from AirborneState or define separately)
        [SerializeField] private float fakeGravityForce = 25f;
        [SerializeField] private float risingGravityMultiplier = 0.6f;
        [SerializeField] private float fallingGravityMultiplier = 1.0f;
        private const float AIR_RESISTANCE = 0.995f; // Can share or tune

        // Air rotation parameters (can copy from AirborneState)
        private float totalRotation = 0f;

        // Physics Parameters
        private const float VERT_GRAVITY_MULTIPLIER = 1.0f; // Adjust gravity if needed
        private const float VERT_AIR_CONTROL_FORCE = 0.5f; // Reduced air control in vert
        private const float LIP_LOCK_STRENGTH = 5.0f; // How strongly to pull back towards the lip trajectory
        
        // State Info
        private Vector3 vertNormal; // Normal of the vert surface launched from
        private Vector3 launchPoint; // World position where the player launched from
        private Vector3 lipDirection; // Direction along the lip (perpendicular to vertNormal and world up)
        private float airTime = 0f;

        // <<< ADDED: Yaw getter >>>
        public float GetTotalYawRotation() => totalRotation;
        // <<< END ADDED >>>

        public PlayerVertAirState(PlayerStateMachine stateMachine, PlayerController player) : base(stateMachine, player) { }

        /// <summary>
        /// Called before Enter() to provide context about the vert launch.
        /// </summary>
        public void EnterWithVertInfo(Vector3 normal, Vector3 point)
        {
            vertNormal = normal.normalized;
            launchPoint = point;
            // Calculate lip direction (tangent to the wall at the launch point)
            lipDirection = Vector3.Cross(vertNormal, Vector3.up).normalized;
             if (lipDirection == Vector3.zero)
            {
                 // Handle cases where vertNormal is aligned with world up (e.g., flat surface tagged as vert?)
                 // Use player's forward direction as a fallback, though this indicates an issue.
                 Debug.LogWarning("VertAirState: vertNormal is aligned with world up. Using player forward as fallback lip direction.");
                 lipDirection = player.Forward;
            }
            Debug.Log($"EnterWithVertInfo: Normal={vertNormal}, Point={launchPoint}, LipDir={lipDirection}");
        }

        public void EnterWithVertLaunch(Vector3 launchPosition)
        {
            // Store only the X and Z components
            lockedXZPosition = new Vector3(launchPosition.x, 0, launchPosition.z);
            Enter(); // Call the base Enter logic
        }

        public override void Enter()
        {
            Debug.Log($"Entering VertAir State - Locking to XZ: {lockedXZPosition}");
            player.IsGrounded = false;
            totalRotation = 0f; 
            // player.AngularVelocity = Vector3.zero; // Optionally zero angular velocity on entry?
        }

        public override void LogicUpdate()
        {
            // Ground checks are handled in PlayerController.FixedUpdate
            // Landing transitions are handled in PlayerController.HandleStateTransitions
        }

        public override void PhysicsUpdate()
        {
            float dt = Time.fixedDeltaTime;

            // 1. Apply Gravity (World Down)
            ApplyGravity(dt);

            // 2. Apply Air Rotation Control
            HandleAirRotation(dt);

            // 3. Apply Air Resistance (Optional)
            player.Velocity *= Mathf.Pow(AIR_RESISTANCE, dt * 60f);

            // 4. >>> LOCK HORIZONTAL POSITION <<<
            Vector3 currentPos = player.transform.position; // Or player.Rb.position
            Vector3 targetPos = new Vector3(
                lockedXZPosition.x, 
                currentPos.y, // Keep calculated Y from gravity
                lockedXZPosition.z
            );
            Debug.Log($"VertAir Lock: Current={currentPos}, Target={targetPos}, LockedXZ={lockedXZPosition}");
            // Move the rigidbody to the target position
            player.Rb.MovePosition(targetPos);
            Debug.Log($"VertAir Lock: Position AFTER MovePosition={player.Rb.position}");
            
            // Also ensure horizontal velocity is zeroed out to prevent drift
            player.Velocity = new Vector3(0, player.Velocity.y, 0);
        }

        private void ApplyGravity(float dt)
        {
            float currentMultiplier = player.Velocity.y > 0 ? risingGravityMultiplier : fallingGravityMultiplier;
            player.Velocity += Vector3.down * fakeGravityForce * currentMultiplier * dt;
        }

        private void HandleAirRotation(float dt)
        {
            // Simplified rotation logic (copied from AirborneState's HandleAirControl)
            float rotationInput = inputHandler.MoveInput.x;
            // No trick checks here, just basic spin
            if (Mathf.Abs(rotationInput) > 0.1f)
            {
                float baseSpinSpeed = 180f; 
                float spinStrength = Mathf.Lerp(baseSpinSpeed, baseSpinSpeed * 1.5f, player.statSpin / 10f);
                // Apply angular velocity directly (Rigidbody handles integration if constraints allow)
                // If using FreezeRotation, this won't work directly. We might need to rotate transform.
                 player.Rb.angularVelocity = new Vector3(0, rotationInput * spinStrength * Mathf.Deg2Rad, 0); // Use Rigidbody angular velocity
                // player.transform.Rotate(Vector3.up, rotationInput * spinStrength * dt, Space.World);
                totalRotation += rotationInput * spinStrength * dt;
            }
            else
            {
                // Dampen angular velocity if no input
                player.Rb.angularVelocity *= Mathf.Pow(0.95f, dt * 60f);
                if (player.Rb.angularVelocity.magnitude < 0.1f * Mathf.Deg2Rad)
                {
                    player.Rb.angularVelocity = Vector3.zero;
                }
            }
        }

        public override void Exit()
        {
            Debug.Log("Exiting VertAir State");
            // Reset angular velocity on exit?
             player.Rb.angularVelocity = Vector3.zero;
        }
    }
} 