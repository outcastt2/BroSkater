using UnityEngine;

[CreateAssetMenu(fileName = "NewSkaterPhysics", menuName = "BroSkater/Skater Physics Parameters")]
public class SkaterPhysicsParameters : ScriptableObject
{
    [Header("Jump Parameters")]
    // REMOVED: public Vector2 ollieJumpForce = new Vector2(7f, 12f);
    public Vector2 ollieVerticalForce = new Vector2(7f, 12f); // Vertical impulse range (stat 0 to 10)
    public Vector2 ollieForwardBoost = new Vector2(1f, 3f);   // Forward impulse ADDED during ollie (stat 0 to 10, applied based on charge?)
    public Vector2 vertJumpForce = new Vector2(10f, 16f); // Higher force for vert jumps (Primarily vertical)
    public Vector2 grindExitJumpForce = new Vector2(6f, 10f); // Base force for jumping off grinds (stat 0 to 10)
    public Vector2 jumpChargeBoost = new Vector2(15f, 25f); // Boost added per second of charge (stat 0 to 10)
    // REMOVED: public float jumpHorizontalRetention = 0.8f; 

    [Header("Grind Parameters")]
    public float minGrindSpeed = 2.0f; // Minimum speed required to start grinding
    public float grindEntrySpeedBoostMultiplier = 1.2f; // Speed multiplier on successfully starting grind
    [Header("Grind Balance Parameters")]
    public Vector2 grindLeanGravity = new Vector2(0.5f, 0.2f);
    public Vector2 grindLeanAcc = new Vector2(2.0f, 1.0f);
    public Vector2 grindInstabilityRate = new Vector2(0.1f, 0.05f);
    public Vector2 grindLeanBailAngle = new Vector2(4000f, 4000f); // Range for bail angle (Needs interpretation)

    [Header("Grind Balance Wobble")]
    public Vector2 grindWobbleBaseMagnitude = new Vector2(0.1f, 0.05f); // Base random push per second (Lower is harder)
    public Vector2 grindWobbleIncreaseRate = new Vector2(0.05f, 0.02f); // How much wobble increases per second (Lower is harder)
    public float grindWobbleMaxMagnitude = 1.5f; // Max random push per second

    [Header("Movement Parameters")]
    public Vector2 maxPushSpeed = new Vector2(12f, 18f);         // Max speed while actively pushing forward (stat 0 to 10)
    public float coastSpeedMultiplier = 0.9f;                   // Multiplier on maxPushSpeed to get max coasting speed
    public Vector2 acceleration = new Vector2(8f, 16f);           // Rate of acceleration when pushing (stat 0 to 10)
    public float brakeForce = 30.0f;                            // Rate of deceleration when braking
    public float coastDeceleration = 0.5f;                      // Rate of deceleration when coasting (gentle drag)
    public Vector2 maxJumpChargeSpeed = new Vector2(15f, 20f);  // Max speed allowed while charging ollie (stat 0 to 10)

    [Header("Manual Balance Parameters")]
    // TODO: Add Manual balance parameters (gravity, acc, instability, bail, wobble)

    [Header("Vert Ramp Parameters")]
    public float vertPushOutDistance = 0.1f; // Distance to push away from vert wall on launch/track
    public float vertAlignSpeed = 5.0f;      // How quickly the player aligns rotation to the vert normal
    public Color vertColor = Color.green;    // Color indicating vert geometry
    public Color grindColor = Color.white;   // Color indicating grind geometry

    [Header("General Parameters")]
    public float standardSwitchMultiplier = 0.8f; // General multiplier for stats when in switch

    /// <summary>
    /// Gets a value interpolated between range.x (stat=0) and range.y (stat=10)
    /// based on the provided stat value. Applies switch multiplier if applicable.
    /// </summary>
    public float GetValueFromStat(Vector2 range, float statValue, bool isSwitch = false, float switchMultiplier = 1.0f)
    {
        Debug.Log($"[GetValueFromStat] Input: range=({range.x:F2},{range.y:F2}), stat={statValue:F2}, isSwitch={isSwitch}, switchMult={switchMultiplier:F2}");
        
        // Apply the specific switchMultiplier passed in, usually standardSwitchMultiplier
        float effectiveMultiplier = isSwitch ? switchMultiplier : 1.0f; 
        // Interpolate based on the stat value (0-10)
        float baseValue = Mathf.Lerp(range.x, range.y, Mathf.Clamp01(statValue / 10.0f));
        
        float finalValue = baseValue * effectiveMultiplier; 
        Debug.Log($"[GetValueFromStat] Output: baseValue={baseValue:F2}, effectiveMult={effectiveMultiplier:F2}, Returning={finalValue:F2}");
        
        // Apply switch penalty/bonus
        return finalValue; 
    }
    
    /// <summary>
    /// Gets a single value, applying switch multiplier if applicable.
    /// </summary>
    public float GetValue(float value, bool isSwitch = false, float switchMultiplier = 1.0f)
    {
        float effectiveMultiplier = isSwitch ? switchMultiplier : 1.0f; 
        return value * effectiveMultiplier;
    }
} 