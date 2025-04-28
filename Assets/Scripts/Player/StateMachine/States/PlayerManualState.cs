using UnityEngine;

namespace BroSkater.Player.StateMachine
{
    public class PlayerManualState : PlayerState
    {
        public const float MIN_MANUAL_SPEED = 2f;
        private const float MANUAL_SPEED_DECAY = 0.95f;
        private const float MIN_BALANCE_THRESHOLD = 0.1f;
        private const float MAX_BALANCE_THRESHOLD = 0.9f;

        private bool isNoseManual;
        private float currentSpeed;

        public PlayerManualState(PlayerStateMachine stateMachine, PlayerController player) : base(stateMachine, player)
        {
        }

        public override void Enter()
        {
            base.Enter();
            
            // Determine if this is a nose manual based on input direction
            isNoseManual = inputHandler.MoveInput.y > 0;
            
            // Initialize manual state
            currentSpeed = player.Speed;
            stateMachine.BalanceMeter.StartManualBalance(player.statManual);
            
            Debug.Log($"Entered {(isNoseManual ? "Nose" : "Regular")} Manual State");
        }

        public override void Exit()
        {
            stateMachine.BalanceMeter.StopBalance();
            base.Exit();
        }

        public override void LogicUpdate()
        {
            // Update balance based on input
            float horizontalInput = inputHandler.MoveInput.x;
            stateMachine.BalanceMeter.UpdateBalancePhysics(horizontalInput);
            stateMachine.BalanceMeter.UpdateLogic();

            // Check for balance failure
            if (stateMachine.BalanceMeter.HasBailed)
            {
                // stateMachine.ChangeState(stateMachine.SkatingState); // Temporarily commented out bail transition
                stateMachine.ChangeState(stateMachine.BailedState); // Bail state is more appropriate
                return;
            }

            // --- Add Jump Exit --- 
            if (inputHandler.JumpInputDown) {
                // TODO: Add specific manual jump pop/force?
                // For now, just transition to air state
                stateMachine.ChangeState(stateMachine.AirborneState);
                return;
            }
            // ---------------------

            // Update speed
            currentSpeed *= MANUAL_SPEED_DECAY;
            
            // Exit manual if speed is too low
            if (currentSpeed < MIN_MANUAL_SPEED)
            {
                // stateMachine.ChangeState(stateMachine.SkatingState); // Temporarily commented out low speed transition
                stateMachine.ChangeState(stateMachine.SkatingState);
                return;
            }

            // Apply forward movement
            Vector3 forwardVelocity = player.transform.forward * currentSpeed;
            player.SetVelocity(new Vector3(forwardVelocity.x, player.GetVelocity().y, forwardVelocity.z));
        }

        public override void PhysicsUpdate()
        {
            // Check if still grounded
            if (!player.IsGrounded)
            {
                stateMachine.ChangeState(stateMachine.AirborneState);
                return;
            }
        }
    }
} 