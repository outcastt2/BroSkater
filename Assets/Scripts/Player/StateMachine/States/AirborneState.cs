using UnityEngine;

namespace BroSkater.Player.StateMachine
{
    public class AirborneState : PlayerState
    {
        // Air control parameters (Consider moving to SkaterPhysicsParameters)
        private const float AIR_CONTROL_FORCE = 5f;
        private const float MAX_AIR_SPEED = 10f;
        private const float AIR_ROTATION_SPEED = 100f;
        private const float AIR_FRICTION = 0.995f;

        // <<< ADDED: Yaw tracking >>>
        private float totalYawRotation = 0f;
        public float GetTotalYawRotation() => totalYawRotation;
        // <<< END ADDED >>>

        // Reference to the Rigidbody for applying forces/torques
        private Rigidbody rb;

        public AirborneState(PlayerStateMachine stateMachine, PlayerController player) : base(stateMachine, player)
        {
            rb = player.Rb;
        }

        public override void Enter()
        {
            Debug.Log("Entering Airborne State");
            player.IsGrounded = false;
            // Reset total yaw when entering air
            totalYawRotation = 0f;
            // Gravity will be applied by PlayerController.ApplyGravity()
            // Optional: Trigger jumping animation
        }

        public override void LogicUpdate()
        {
            // Landing checks are handled in PlayerController.HandleStateTransitions

            // Check for grind input (using GrindDetector now)
            // HandleGrindInput();
        }

        public override void PhysicsUpdate()
        {
            // Gravity is applied in PlayerController.FixedUpdate

            // Apply Air Control
            HandleAirControl();

            // Apply Air Friction
            player.Velocity *= AIR_FRICTION;

            // <<< ADDED: Yaw Tracking >>>
            // Accumulate yaw rotation based on angular velocity around Y axis
            if (player.AngularVelocity.magnitude > 0.01f) // Avoid accumulating tiny values
            {
                totalYawRotation += player.AngularVelocity.y * Time.fixedDeltaTime;
            }
            // <<< END ADDED >>>
        }

        private void HandleAirControl()
        {
            if (rb == null) return;

            // Get input
            Vector2 moveInput = inputHandler.MoveInput;

            // --- Horizontal Movement Control --- 
            // Convert input to world direction relative to camera
            Vector3 inputDir = Vector3.zero;
             if (moveInput.magnitude > 0.1f)
            {
                inputDir = new Vector3(moveInput.x, 0, moveInput.y).normalized;
                UnityEngine.Camera mainCam = UnityEngine.Camera.main;
                if (mainCam != null)
                {
                    Vector3 camForward = mainCam.transform.forward;
                    camForward.y = 0;
                    camForward.Normalize();
                    Quaternion camRotation = Quaternion.LookRotation(camForward);
                    inputDir = camRotation * inputDir;
                }
            }
            
            // Apply air control force
            if (inputDir != Vector3.zero)
            {
                 // Calculate force based on input direction
                 Vector3 controlForce = inputDir * AIR_CONTROL_FORCE;
                
                 // Limit effect on current velocity direction
                 // Allow some change but prevent instant direction reversal
                 Vector3 currentHorizontalVel = Vector3.ProjectOnPlane(player.Velocity, Vector3.up);
                 float speed = currentHorizontalVel.magnitude;
                
                 // Apply force more effectively if speed is lower or direction is similar
                 float alignment = Vector3.Dot(currentHorizontalVel.normalized, inputDir);
                 float forceMultiplier = Mathf.Lerp(0.5f, 1.0f, 1.0f - Mathf.Clamp01(speed / MAX_AIR_SPEED));
                 forceMultiplier *= Mathf.Lerp(0.7f, 1.0f, alignment * 0.5f + 0.5f); // Less force against current momentum

                 rb.AddForce(controlForce * forceMultiplier, ForceMode.Acceleration); // Use Acceleration to ignore mass

                 // Clamp horizontal speed
                 Vector3 newHorizontalVel = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
                 if (newHorizontalVel.magnitude > MAX_AIR_SPEED)
                 {
                     newHorizontalVel = newHorizontalVel.normalized * MAX_AIR_SPEED;
                     rb.linearVelocity = new Vector3(newHorizontalVel.x, rb.linearVelocity.y, newHorizontalVel.z);
                 }
            }
            
            // --- Rotation Control --- 
            float horizontalInput = moveInput.x;
             if (Mathf.Abs(horizontalInput) > 0.1f)
             {
                 // Apply torque for air rotation (yaw)
                 Vector3 torque = Vector3.up * horizontalInput * AIR_ROTATION_SPEED;
                 // Consider player stats (Spin stat)
                 float spinMultiplier = player.PhysicsParams.GetValueFromStat(
                     new Vector2(0.5f, 1.5f), // Example range for spin effect on air rotation
                     player.statSpin
                 );
                 rb.AddTorque(torque * spinMultiplier, ForceMode.Acceleration);
                 
                 // Damp sideways torque if needed (optional)
                 // rb.angularVelocity = new Vector3(rb.angularVelocity.x * 0.9f, rb.angularVelocity.y, rb.angularVelocity.z * 0.9f);
             }
             else
             {
                 // Damp yaw rotation when no input
                 rb.angularVelocity = new Vector3(rb.angularVelocity.x, rb.angularVelocity.y * 0.95f, rb.angularVelocity.z);
             }
             // Update PlayerController's angular velocity tracking
             player.AngularVelocity = rb.angularVelocity;
        }


        private void HandleGrindInput()
        {
            // Grind logic is now primarily handled by GrindDetector
            // This state might need to check if a grind was *requested* and transition
            // but the actual detection happens elsewhere.
        }

        public override void Exit()
        {
            Debug.Log("Exiting Airborne State");
            
            // Reset angular velocity damping? Or let next state handle it.
            // player.AngularVelocity *= 0.5f; // Optional: reduce spin on landing/grinding
        }
    }
} 