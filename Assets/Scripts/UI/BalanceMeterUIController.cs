using UnityEngine;
using UnityEngine.UI;
using BroSkater.Player;
using BroSkater.Player.StateMachine;

public class BalanceMeterUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStateMachine playerStateMachine; // Assign the player's state machine
    [SerializeField] private GameObject balanceMeterRoot;     // The parent GameObject holding the meter UI
    [SerializeField] private Image needleImage;           // The Image component for the needle

    [Header("Settings")]
    [SerializeField] private float maxNeedleAngle = 65f; // Max rotation in degrees for full imbalance

    private BalanceMeter balanceMeter;

    void Start()
    {
        if (playerStateMachine == null)
        {
            // Try to find it if not assigned
            playerStateMachine = FindObjectOfType<PlayerStateMachine>();
            if (playerStateMachine == null)
            {
                 Debug.LogError("PlayerStateMachine not found or assigned for BalanceMeterUIController!");
                 enabled = false;
                 return;
            }
        }
        
        balanceMeter = playerStateMachine.BalanceMeter;
        if (balanceMeter == null)
        { 
            Debug.LogError("BalanceMeter not found on PlayerStateMachine!");
            enabled = false;
            return;
        }

        // Ensure UI elements are assigned
        if (balanceMeterRoot == null || needleImage == null)
        {
            Debug.LogError("UI References (BalanceMeterRoot or NeedleImage) not assigned in Inspector!");
            enabled = false;
            return;
        }

        // Start hidden
        balanceMeterRoot.SetActive(false); 
    }

    void Update()
    {
        if (balanceMeter == null) return; 

        bool shouldBeActive = balanceMeter.IsActive;
        
        // Activate/Deactivate the UI root
        if (balanceMeterRoot.activeSelf != shouldBeActive)
        {
            balanceMeterRoot.SetActive(shouldBeActive);
        }

        // Update needle rotation if active
        if (shouldBeActive)
        {
            // Get balance value (-1 to 1, potentially slightly beyond)
            float balanceValue = balanceMeter.CurrentBalance;
            
            // Calculate target rotation
            // Clamp balanceValue just in case it goes way beyond -1 to 1 for rotation calculation
            float clampedBalance = Mathf.Clamp(balanceValue, -1.1f, 1.1f); 
            float targetAngle = clampedBalance * maxNeedleAngle;
            
            // Apply rotation (Negative Z for clockwise rotation on the right)
            needleImage.rectTransform.localRotation = Quaternion.Euler(0, 0, -targetAngle);
        }
    }
} 