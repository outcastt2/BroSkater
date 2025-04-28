using UnityEngine;

namespace BroSkater.Player.StateMachine
{
    public class PlayerBailedState : PlayerState
    {
        private const float BAIL_DURATION = 1.0f; // How long to stay in bail state
        private float bailTimer;

        public PlayerBailedState(PlayerStateMachine stateMachine, PlayerController player) : base(stateMachine, player) { }

        public override void Enter()
        {
            Debug.Log("Entering Bailed State");
            bailTimer = 0f;
            
            // Stop momentum quickly
            player.Velocity *= 0.1f; 
            player.AngularVelocity = Vector3.zero;
            
            // Ensure grounded (or attempt to ground)
            player.CheckGrounded(); 
            if (!player.IsGrounded)
            {
                // Apply strong downward force if bailed in air? Or just let normal gravity take over?
                // For now, just ensure grounded flag matches reality.
            }
            
            // TODO: Trigger bail animation
        }

        public override void LogicUpdate()
        {
            bailTimer += Time.deltaTime;

            if (bailTimer >= BAIL_DURATION)
            {
                // Transition back to standing still after duration
                // stateMachine.ChangeState(stateMachine.StandingStillState);
                // Transition back to skating state instead
                 Debug.Log("Bail duration finished, transitioning to Skating State.");
                 stateMachine.ChangeState(stateMachine.SkatingState);
            }
        }

        public override void PhysicsUpdate()
        {
            // Apply heavy friction/damping while bailed
             player.Velocity *= Mathf.Pow(0.8f, Time.fixedDeltaTime * 60f);
            
            // Ensure player stays snapped to ground if possible
            if (player.IsGrounded) 
            {
               player.Velocity = Vector3.ProjectOnPlane(player.Velocity, player.GetGroundNormal());
            }
            else 
            {
                // Apply gravity if somehow still airborne
                player.Velocity += Physics.gravity * Time.fixedDeltaTime;
            }
        }

        public override void Exit()
        {
             Debug.Log("Exiting Bailed State");
             // TODO: Stop bail animation
        }
    }
} 