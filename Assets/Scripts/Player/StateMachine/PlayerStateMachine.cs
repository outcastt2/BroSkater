namespace BroSkater.Player.StateMachine
{
    using UnityEngine;
    using BroSkater.Player.StateMachine;
    // using BroSkater.Player.States;

    public class PlayerStateMachine : MonoBehaviour
    {
        public PlayerState CurrentState { get; private set; }
        public PlayerState PreviousState { get; private set; }

        // References to all possible states
        public PlayerSkatingState SkatingState { get; private set; }
        public PlayerAirborneState AirborneState { get; private set; }
        public PlayerGrindingState GrindingState { get; private set; }
        public PlayerManualState ManualState { get; private set; }
        public PlayerStandingStillState StandingStillState { get; private set; }
        public PlayerBailedState BailedState { get; private set; }
        public PlayerVertAirState VertAirState { get; private set; }
        // public PlayerVertSkatingState VertSkatingState { get; private set; }
        // Future states
        // public PlayerLipState LipState { get; }
        // public PlayerWallrideState WallrideState { get; }

        private PlayerController playerController;
        public BalanceMeter BalanceMeter => playerController.BalanceMeter;

        // Track time spent in current state
        public float TimeInCurrentState { get; private set; }
        
        [Header("State Parameters")]
        // [SerializeField] private PlayerSkatingState.MovementParameters skatingParams;
        // [SerializeField] private PlayerAirborneState.AirParameters airParams;
        [SerializeField] private PlayerGrindingState.GrindParameters grindParams;
        // [SerializeField] private PlayerManualState.ManualParameters manualParams;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            if (playerController == null)
            {
                Debug.LogError("PlayerController not found on GameObject!", this);
                return;
            }

            // Instantiate states
            SkatingState = new PlayerSkatingState(this, playerController);
            AirborneState = new PlayerAirborneState(this, playerController);
            GrindingState = new PlayerGrindingState(this, playerController);
            ManualState = new PlayerManualState(this, playerController);
            StandingStillState = new PlayerStandingStillState(this, playerController);
            BailedState = new PlayerBailedState(this, playerController);
            VertAirState = new PlayerVertAirState(this, playerController);
            // VertSkatingState = new PlayerVertSkatingState(this, playerController);
            
            // Set parameters if provided
            // SkatingState.SetParameters(skatingParams);
            // AirborneState.SetParameters(airParams);
            GrindingState.SetParameters(grindParams);
            // ManualState.SetParameters(manualParams);
        }

        private void Start()
        {
            // Ensure we have all required components
            if (playerController.InputHandler == null)
            {
                Debug.LogError("PlayerInputHandler not found on PlayerController!", this);
                return;
            }

            // Initialize states
            CurrentState = StandingStillState;
            CurrentState.Enter();
        }

        private void Update()
        {
            if (CurrentState != null)
            {
                TimeInCurrentState += Time.deltaTime;
                CurrentState.Update();
                CurrentState.LogicUpdate();
            }
        }

        private void FixedUpdate()
        {
            if (CurrentState != null)
            {
                CurrentState.FixedUpdate();
                CurrentState.PhysicsUpdate();
            }
        }

        public void ChangeState(PlayerState newState)
        {
            if (newState == null)
            {
                Debug.LogError("Cannot change to null state!");
                return;
            }

            // Exit current state
            CurrentState?.Exit();

            // Update state references
            PreviousState = CurrentState;
            CurrentState = newState;
            TimeInCurrentState = 0f;

            // Enter new state
            CurrentState.Enter();
            
            Debug.Log($"State changed to: {CurrentState.GetType().Name}");
        }

        public void Initialize(PlayerState startingState)
        {
            if (startingState == null)
            {
                Debug.LogError("Cannot initialize state machine with null state!");
                return;
            }

            CurrentState = StandingStillState;
            CurrentState?.Enter();
            Debug.Log($"State Machine Initialized with state: {CurrentState.GetType().Name}");
        }

        public void RevertToPreviousState()
        {
            if (PreviousState != null)
            {
                ChangeState(PreviousState);
            }
            else
            {
                Debug.LogWarning("No previous state to revert to!");
            }
        }

        public bool IsInState<T>() where T : PlayerState
        {
            return CurrentState is T;
        }

        public float GetTimeInState()
        {
            return TimeInCurrentState;
        }
    }
} 