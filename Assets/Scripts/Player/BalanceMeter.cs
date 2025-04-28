using UnityEngine;

namespace BroSkater.Player
{
    public enum BalanceType { Grind, Manual, Lip } // Add others if needed

    public class BalanceMeter
    {
        private SkaterPhysicsParameters physicsParams;
        private PlayerController player; // To get stats

        public float CurrentBalance { get; private set; } // e.g., -1 (left) to 1 (right)
        public bool IsActive { get; private set; }

        private BalanceType currentType;
        private float currentInstabilityRate = 0f;
        private float currentLeanAcc = 0f;
        private float currentLeanGravity = 0f;
        private float currentWobbleBaseMag = 0f;
        private float currentWobbleIncreaseRate = 0f;
        private float maxWobbleMag = 1f;
        private float balanceStartTime = 0f;
        // Add other parameters as needed (random speed, cheese time etc.)
        private float bailThreshold = 1.0f; // Default, will be overridden

        // TODO: Add properties/events for UI updates

        public BalanceMeter(PlayerController playerController)
        {
            this.player = playerController;
            this.physicsParams = playerController.PhysicsParams;
            IsActive = false;
            CurrentBalance = 0f;
        }

        public void StartGrindBalance(float railBalanceStat)
        {
            StartBalance(BalanceType.Grind);
        }

        public void StartManualBalance(float manualStat)
        {
            StartBalance(BalanceType.Manual);
        }

        public void StartBalance(BalanceType type)
        {
            IsActive = true;
            currentType = type;
            CurrentBalance = 0f; // Start centered
            balanceStartTime = Time.time; // Record start time

            // Load parameters based on type
            LoadParameters();

            // TODO: Apply initial 'cheese' time/stability?
            // TODO: Trigger UI activation
            Debug.Log($"Starting {type} Balance");
        }

        public void StopBalance()
        {
            IsActive = false;
            Debug.Log($"Stopping {currentType} Balance");
            // TODO: Trigger UI deactivation
        }

        public void UpdatePhysics(float leanInput) // Alias for UpdateBalancePhysics
        {
            UpdateBalancePhysics(leanInput);
        }

        public void UpdateBalancePhysics(float leanInput) // Pass player input (-1 to 1)
        {
            if (!IsActive) return;

            // 1. Apply Player Lean Input
            CurrentBalance += leanInput * currentLeanAcc * Time.fixedDeltaTime;

            // 2. Apply Random Instability / Wobble (Updated)
            float timeBalancing = Time.time - balanceStartTime;
            float currentWobbleMagnitude = Mathf.Clamp(
                currentWobbleBaseMag + (currentWobbleIncreaseRate * timeBalancing),
                0f, 
                maxWobbleMag
            );
            float randomWobble = Random.Range(-1f, 1f) * currentWobbleMagnitude * Time.fixedDeltaTime;
            CurrentBalance += randomWobble;

            // --- DEBUG LOG for Wobble --- 
            if (Time.frameCount % 30 == 0) // Log every 30 frames to avoid spam
            {
                Debug.Log($"[BalanceMeter Wobble] Time: {timeBalancing:F2}, BaseMag: {currentWobbleBaseMag:F3}, IncrRate: {currentWobbleIncreaseRate:F3}, CurrMag: {currentWobbleMagnitude:F3}, RandWobble: {randomWobble:F4}, BalanceAfterWobble: {CurrentBalance:F4}", player.gameObject);
            }
            // --- END DEBUG LOG ---

            // 3. Apply Lean Gravity (pull back to center)
            // Apply gravity only if not actively leaning strongly in the opposite direction
            if (Mathf.Sign(CurrentBalance) == Mathf.Sign(leanInput) || Mathf.Abs(leanInput) < 0.1f || currentLeanGravity <= 0)
            { 
                // Apply gravity pull towards center
                CurrentBalance -= Mathf.Sign(CurrentBalance) * currentLeanGravity * Time.fixedDeltaTime;
                // Prevent overshooting center due to gravity
                if (Mathf.Sign(CurrentBalance) != Mathf.Sign(CurrentBalance - Mathf.Sign(CurrentBalance) * currentLeanGravity * Time.fixedDeltaTime))
                {
                    CurrentBalance = 0f; 
                }
            }
            
            // 4. Clamp to prevent immediate overshoot if gravity/wobble is high
            CurrentBalance = Mathf.Clamp(CurrentBalance, -bailThreshold * 1.1f, bailThreshold * 1.1f); // Clamp slightly beyond threshold

            // Check for Bail Condition (LogicUpdate might be better?)
            // CheckBailCondition(); // Moved to LogicUpdate for cleaner separation
        }

        public void UpdateLogic()
        {
            if (!IsActive) return;
            // Check Bail Condition after physics update
            CheckBailCondition();
            // TODO: Update UI representation
        }

        private void CheckBailCondition()
        {
            if (Mathf.Abs(CurrentBalance) > bailThreshold)
            {
                Debug.Log($"Balance Bail! Meter: {CurrentBalance}");
                // The state (e.g., PlayerGrindingState) should detect this bail condition
                // via a property or event, and then trigger the appropriate bail transition.
                // StopBalance(); // State should call stop
            }
        }

        // Helper to load correct parameters based on current balance type
        private void LoadParameters()
        {
            // Default values (or maybe throw error?)
            Vector2 leanGravityRange = Vector2.zero;
            Vector2 leanAccRange = Vector2.zero;
            Vector2 instabilityRateRange = Vector2.zero;
            Vector2 bailAngleRange = new Vector2(4000f, 4000f); // Need a default
            Vector2 wobbleBaseMagRange = Vector2.zero;
            Vector2 wobbleIncreaseRateRange = Vector2.zero;
            float relevantStat = 0f;

            switch (currentType)
            {
                case BalanceType.Grind:
                    leanGravityRange = physicsParams.grindLeanGravity;
                    leanAccRange = physicsParams.grindLeanAcc;
                    instabilityRateRange = physicsParams.grindInstabilityRate;
                    bailAngleRange = physicsParams.grindLeanBailAngle;
                    relevantStat = player.statRailBalance;
                    wobbleBaseMagRange = physicsParams.grindWobbleBaseMagnitude;
                    wobbleIncreaseRateRange = physicsParams.grindWobbleIncreaseRate;
                    maxWobbleMag = physicsParams.grindWobbleMaxMagnitude;
                    // Load other grind-specific params...
                    break;
                case BalanceType.Manual:
                    // leanGravityRange = physicsParams.manualLeanGravity; // Need to add these params first
                    // leanAccRange = physicsParams.manualLeanAcc;
                    // instabilityRateRange = physicsParams.manualInstabilityRate;
                    // bailAngleRange = physicsParams.manualLeanBailAngle;
                    relevantStat = player.statManual;
                    Debug.LogWarning("Manual balance parameters not yet implemented!");
                    break;
                case BalanceType.Lip:
                    // Load lip params...
                    Debug.LogWarning("Lip balance parameters not yet implemented!");
                    break;
            }

            // Calculate actual values based on stats
            // TODO: Refine how leanGravity/Acc diff multipliers are applied
            currentLeanGravity = physicsParams.GetValueFromStat(leanGravityRange, relevantStat, player.IsSwitch, physicsParams.standardSwitchMultiplier);
            currentLeanAcc = physicsParams.GetValueFromStat(leanAccRange, relevantStat, player.IsSwitch, physicsParams.standardSwitchMultiplier);
            currentInstabilityRate = physicsParams.GetValueFromStat(instabilityRateRange, relevantStat, player.IsSwitch, physicsParams.standardSwitchMultiplier);
            currentWobbleBaseMag = physicsParams.GetValueFromStat(wobbleBaseMagRange, relevantStat);
            currentWobbleIncreaseRate = physicsParams.GetValueFromStat(wobbleIncreaseRateRange, relevantStat);

            Debug.Log($"[BalanceMeter Params Loaded ({currentType})] LeanGravity: {currentLeanGravity:F3}, LeanAcc: {currentLeanAcc:F3}, WobbleBase: {currentWobbleBaseMag:F3}, WobbleIncr: {currentWobbleIncreaseRate:F3}", player.gameObject);

            // Bail Threshold interpretation: Original value 4000. What unit? Assume simple -1 to 1 range for now.
            // A higher stat should make it HARDER to bail (larger threshold range or slower meter?).
            // The QB params seem inverted (higher stat = higher rate). Let's assume 1.0 is the standard range for now.
            // We might need to invert the stat calculation or interpretation later.
            float bailStatValue = physicsParams.GetValueFromStat(bailAngleRange, relevantStat);
            // This needs heavy interpretation. For now, let's keep threshold fixed at 1.
            bailThreshold = 1.0f; // Placeholder - Needs tuning and interpretation of bailAngleRange.

            // Apply multipliers (Lean Gravity Diff, Lean Acc Diff) - How exactly? Additive? Multiplicative?
            // Needs experimentation. Example:
            // currentLeanGravity *= physicsParams.GetValueFromStat(physicsParams.leanGravityDiff, relevantStat); // ??
        }

        // Example helper (adjust as needed based on how params scale)
        private float GetCurrentParam(Vector2 range, float stat)
        {
            return physicsParams.GetValueFromStat(range, stat, player.IsSwitch, physicsParams.standardSwitchMultiplier);
        }

        // Public properties for state access
        public bool HasBalanceFailed { get; private set; }
        public bool HasBailed => IsActive && Mathf.Abs(CurrentBalance) > bailThreshold;
        public float GetBalance() => CurrentBalance;
    }
} 