using UnityEngine;

namespace BroSkater.Player.StateMachine
{
    public class PlayerStandingStillState : PlayerState
    {
        // --- Constants ---
        private const float ROTATION_SPEED = 540f; // Degrees per second for rotation
        private const float PUSH_FORCE = 6.0f; // Force applied when transitioning to Skating
        private const float MAX_JUMP_CHARGE_TIME = 1.0f; // Max time to charge jump

        // --- State Variables ---
        private bool isJumpCharging = false;
        private float jumpChargeTime = 0f;

        public PlayerStandingStillState(PlayerStateMachine stateMachine, PlayerController player) : base(stateMachine, player) { }

        public override void Enter()
        {
            Debug.Log("-----> ENTER: PlayerStandingStillState");
            player.IsGrounded = true;
            player.Velocity = Vector3.zero; // Stop movement
            player.AngularVelocity = Vector3.zero; // Stop rotation

            // Reset jump state
            isJumpCharging = false;
            jumpChargeTime = 0f;
        }

        public override void Exit()
        {
            Debug.Log("<----- EXIT: PlayerStandingStillState");
            // Reset flags if needed when exiting
            isJumpCharging = false;
        }

        public override void LogicUpdate()
        {
            HandleRotationInput(); // Handle rotation always

            // <<< ADD LOG TO CHECK INPUT >>>
            if (player.InputHandler != null) {
              Debug.Log($"[StandingStill] Checking PushInputDown: {player.InputHandler.PushInputDown}");
            }

            // --- Transition Checks ---

            // Check for Forward Input -> Transition to Skating
            if (player.InputHandler.PushInputDown)
            {
                Debug.Log("[StandingStill] PushInputDown detected. Transitioning to SkatingState.");
                stateMachine.ChangeState(stateMachine.SkatingState);
                player.InputHandler.ConsumePushInputDown();
                return;
            }

            // Check for Jump Input -> Charge/Transition to Airborne
            HandleJumpInput();
        }

        public override void PhysicsUpdate()
        {
            // Standing still state primarily handles rotation based on input
            // and ensures velocity stays zero unless transitioning.
            // Actual rotation is applied based on input handled in LogicUpdate

            // Ensure player stays put (might be redundant if Enter zeroes velocity, but good safety)
            if (!isJumpCharging) // Don't interfere if charging jump might involve animation/slight movement
            {
                 player.Velocity = Vector3.zero;
                 if (player.Rb.linearVelocity.sqrMagnitude > 0.01f) // Check if Rigidbody velocity is actually non-zero
                 {
                    Debug.Log("[StandingStill] Forcing Rigidbody velocity to ZERO.");
                    player.Rb.linearVelocity = Vector3.zero; // <<< Force Rigidbody velocity too
                 }
            }
            // No gravity application needed explicitly if Rigidbody uses gravity and we are grounded.
        }

        // --- Helper Methods ---

        private void HandleRotationInput()
        {
            float rotationInput = player.InputHandler.MoveInput.x;
            if (Mathf.Abs(rotationInput) > 0.1f)
            {
                // Calculate rotation amount based on input and speed
                float rotationAmount = rotationInput * ROTATION_SPEED * Time.deltaTime;
                // Apply rotation around the Y axis
                player.transform.Rotate(Vector3.up, rotationAmount, Space.World);
                 // Directly rotating the transform might be simpler here than applying torque
                 // Ensure angular velocity is zeroed if not using torque
                 player.AngularVelocity = Vector3.zero;
            }
            else
            {
                 // Optional: Dampen any residual angular velocity if not actively rotating
                 player.AngularVelocity = Vector3.Lerp(player.AngularVelocity, Vector3.zero, Time.deltaTime * 10f);
            }
        }

        private void HandleJumpInput()
        {
            Debug.Log($"[StandingStill] Checking HandleJumpInput. JumpInputDown={player.InputHandler.JumpInputDown}, isCharging={isJumpCharging}");

            if (player.InputHandler.JumpInputDown && !isJumpCharging)
            {
                Debug.Log("[StandingStill] Jump Charge Started (Consume Input)");
                isJumpCharging = true;
                jumpChargeTime = 0f;
                player.InputHandler.ConsumeJumpInputDown();
                Debug.Log("Jump Charge Started");
                // TODO: Trigger crouching animation
            }

            if (isJumpCharging)
            {
                jumpChargeTime += Time.deltaTime;
                 // Optional: Clamp charge time visually or mechanically
                 // jumpChargeTime = Mathf.Clamp(jumpChargeTime, 0f, MAX_JUMP_CHARGE_TIME);
                 // Debug.Log($"Charging Jump: {jumpChargeTime:F2}s");

                if (player.InputHandler.JumpInputUp)
                {
                    Debug.Log("[StandingStill] Jump Released (Executing Jump)");
                    // Execute jump on release
                    ExecuteJump();
                    player.InputHandler.ConsumeJumpInputUp();
                    // Transition is handled within ExecuteJump
                }
                // Optional: Cancel charge if held too long?
                // if (jumpChargeTime >= MAX_JUMP_CHARGE_TIME * 1.1f) // A little buffer
                // {
                //     Debug.Log("Jump charge cancelled (held too long)");
                //     isJumpCharging = false;
                //     // TODO: Trigger stand up animation
                // }
            }
        }

        private void ApplyPushForce()
        {
            // Apply a forward impulse when starting to skate
            player.AddForce(player.transform.forward * PUSH_FORCE, ForceMode.Impulse);
            Debug.Log("StandingStill: Applied Push Force to start skating via AddForce");
        }

        private void ExecuteJump()
        {
             Debug.Log($"Executing Jump with charge time: {jumpChargeTime:F2}s");
            // Reset charging state
            isJumpCharging = false;
            
            float chargeRatio = Mathf.Clamp01(jumpChargeTime / MAX_JUMP_CHARGE_TIME);

            // Calculate VERTICAL jump force based on PhysicsParams and statOllie
            float baseVerticalForce = player.PhysicsParams.GetValueFromStat(player.PhysicsParams.ollieVerticalForce, player.statOllie, player.IsSwitch);
            float verticalJumpForce = Mathf.Lerp(baseVerticalForce * 0.5f, baseVerticalForce, chargeRatio); // Mimic SkatingState's charge influence (50%-100%)
            
            // Add special boost if active
            if (player.IsSpecialActive())
            {
                verticalJumpForce *= 1.2f; // Consistent with SkatingState
            }

            // Apply the jump velocity (VERTICAL ONLY from standstill)
            Vector3 jumpVelocity = player.transform.up * verticalJumpForce;
            player.Velocity = new Vector3(0, jumpVelocity.y, 0); 
            
            Debug.Log($"[StandingStill ExecuteJump] Applied VERTICAL Jump Velocity: {player.Velocity.y:F2} (Force: {verticalJumpForce:F2})");

            player.IsGrounded = false; // <<< Force IsGrounded to false immediately
            player.IsIgnoringGround = true; // <<< SET FLAG

            // Transition to Airborne state
            stateMachine.ChangeState(stateMachine.AirborneState);
            
             // Reset charge time for next jump
             jumpChargeTime = 0f;
             // TODO: Trigger jump/stand up animation
        }
    }
} 
