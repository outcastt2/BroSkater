using UnityEngine;

namespace BroSkater.Player.StateMachine
{
    public abstract class PlayerState
    {
        protected PlayerStateMachine stateMachine;
        protected PlayerController player;
        protected SkaterPhysicsParameters physicsParams;
        protected PlayerInputHandler inputHandler; // Added for convenience

        protected PlayerState(PlayerStateMachine stateMachine, PlayerController player)
        {
            this.stateMachine = stateMachine;
            this.player = player;
            this.physicsParams = player.PhysicsParams; // Get reference from player
            this.inputHandler = player.InputHandler;   // Get reference from player
        }

        public virtual void Enter() { } // Called when entering state
        public virtual void Update() { } // Called in Update()
        public virtual void LogicUpdate() { } // Called in Update()
        public virtual void FixedUpdate() { } // Called in FixedUpdate()
        public virtual void PhysicsUpdate() { } // Called in FixedUpdate()
        public virtual void Exit() { } // Called when exiting state

        // Common helper methods can be added here
        protected void CheckForGround()
        {
            player.IsGrounded = player.CheckGrounded();
        }
    }
} 