using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BroSkater.UI;

namespace BroSkater.Player
{
    /// <summary>
    /// Handles player trick execution and scoring
    /// </summary>
    public class TrickSystem : MonoBehaviour
    {
        [Header("Trick Settings")]
        [SerializeField] private float minAirTimeForTrick = 0.5f;
        [SerializeField] private float rotationTrickThreshold = 45f;
        [SerializeField] private float flipTrickSpeed = 720f;
        
        [Header("References")]
        [SerializeField] private Transform skateboardModel;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private ScoreUI scoreUI;
        
        // Trick state
        private bool isPerformingTrick = false;
        private bool trickLanded = false;
        private float airTime = 0f;
        private float trickStartRotation = 0f;
        private float totalRotation = 0f;
        private Vector3 trickStartPosition;
        
        // Current trick info
        private string currentTrickName = "";
        private int currentTrickScore = 0;
        private List<string> currentCombo = new List<string>();
        private int currentComboScore = 0;
        private int currentMultiplier = 1;
        private float comboTimer = 0f;
        private const float COMBO_TIMEOUT = 2.0f; // Seconds before combo ends
        
        // Total Score
        private int totalPlayerScore = 0;
        
        // Input flags
        private bool kickflipInput = false;
        private bool heelflipInput = false;
        private bool grabInput = false;
        private bool manualInput = false;
        
        // References
        private Rigidbody rb;
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            
            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
            }
        }
        
        private void Start()
        {
            // Find ScoreUI if not assigned
            if (scoreUI == null)
            {
                scoreUI = Object.FindFirstObjectByType<ScoreUI>();
            }
        }
        
        private void Update()
        {
            // Get trick input
            GetTrickInput();
            
            // Update Combo Timer
            if (currentComboScore > 0)
            {
                comboTimer -= Time.deltaTime;
                if (comboTimer <= 0)
                {
                    EndCombo();
                }
            }
            
            // Check ground state from player controller
            bool isGrounded = playerController.IsGrounded; // Use PlayerController's flag
            
            // Track air time and trick state
            if (!isGrounded)
            {
                airTime += Time.deltaTime;
                
                // Start trick if we have enough air time
                if (airTime >= minAirTimeForTrick && !isPerformingTrick)
                {
                    StartTrick();
                }
                
                // Process trick input if we're performing a trick
                if (isPerformingTrick)
                {
                    ProcessTrickInput();
                }
            }
            else
            {
                // Landing
                if (isPerformingTrick)
                {
                    LandTrick();
                }
                
                // Reset air time
                airTime = 0f;
            }
            
            // Handle manual input
            if (manualInput && isGrounded)
            {
                StartManual();
            }
        }
        
        /// <summary>
        /// Gets trick input from player
        /// </summary>
        private void GetTrickInput()
        {
            kickflipInput = Input.GetButtonDown("Fire1");
            heelflipInput = Input.GetButtonDown("Fire2");
            grabInput = Input.GetButton("Fire3");
            manualInput = Input.GetButtonDown("Fire4") || (Input.GetKey(KeyCode.S) && Input.GetKey(KeyCode.W));
        }
        
        /// <summary>
        /// Starts a trick sequence
        /// </summary>
        private void StartTrick()
        {
            isPerformingTrick = true;
            trickLanded = false;
            trickStartRotation = transform.eulerAngles.y;
            trickStartPosition = transform.position;
            
            currentTrickName = "";
            currentTrickScore = 0;
        }
        
        /// <summary>
        /// Processes trick input during a trick
        /// </summary>
        private void ProcessTrickInput()
        {
            // Calculate rotation amount
            totalRotation = Mathf.Abs(Mathf.DeltaAngle(trickStartRotation, transform.eulerAngles.y));
            
            // Process kickflip
            if (kickflipInput)
            {
                PerformFlipTrick("Kickflip", 100);
            }
            
            // Process heelflip
            if (heelflipInput)
            {
                PerformFlipTrick("Heelflip", 100);
            }
            
            // Process grab tricks
            if (grabInput)
            {
                PerformGrabTrick();
            }
            
            // Handle board rotation for rotation tricks
            if (totalRotation >= rotationTrickThreshold)
            {
                UpdateRotationTrick();
            }
        }
        
        /// <summary>
        /// Performs a flip trick
        /// </summary>
        private void PerformFlipTrick(string trickName, int baseScore)
        {
            if (!string.IsNullOrEmpty(currentTrickName))
            {
                // Already doing a trick, so combine them
                currentTrickName += " " + trickName;
                currentTrickScore += baseScore;
            }
            else
            {
                currentTrickName = trickName;
                currentTrickScore = baseScore;
            }
            
            // Animate the skateboard (in a real implementation, you would use proper animations)
            if (skateboardModel != null)
            {
                StartCoroutine(AnimateFlipTrick(trickName == "Kickflip"));
            }
        }
        
        /// <summary>
        /// Performs a grab trick
        /// </summary>
        private void PerformGrabTrick()
        {
            string grabName = "Melon";
            int grabScore = 150;
            
            // Determine grab type based on direction (simplified)
            float horizontalInput = Input.GetAxis("Horizontal");
            if (horizontalInput > 0.5f)
            {
                grabName = "Indy";
                grabScore = 120;
            }
            else if (horizontalInput < -0.5f)
            {
                grabName = "Stalefish";
                grabScore = 130;
            }
            
            if (!string.IsNullOrEmpty(currentTrickName))
            {
                // Already doing a trick, so append the grab
                if (!currentTrickName.Contains(grabName))
                {
                    currentTrickName += " " + grabName;
                    currentTrickScore += grabScore;
                }
            }
            else
            {
                currentTrickName = grabName;
                currentTrickScore = grabScore;
            }
        }
        
        /// <summary>
        /// Updates the rotation trick based on current rotation
        /// </summary>
        private void UpdateRotationTrick()
        {
            // Determine the rotation trick name and score
            string rotationName = "";
            int rotationScore = 0;
            
            if (totalRotation >= 360f)
            {
                rotationName = "360";
                rotationScore = 300;
            }
            else if (totalRotation >= 180f)
            {
                rotationName = "180";
                rotationScore = 150;
            }
            else if (totalRotation >= 90f)
            {
                rotationName = "90";
                rotationScore = 50;
            }
            
            // Only update if we have a new rotation trick
            if (!string.IsNullOrEmpty(rotationName) && !currentTrickName.Contains(rotationName))
            {
                if (!string.IsNullOrEmpty(currentTrickName))
                {
                    currentTrickName += " " + rotationName;
                }
                else
                {
                    currentTrickName = rotationName;
                }
                
                currentTrickScore += rotationScore;
            }
        }
        
        /// <summary>
        /// Starts a manual trick
        /// </summary>
        private void StartManual()
        {
            // In a real implementation, you would set up proper balance mechanics
            currentTrickName = "Manual";
            currentTrickScore = 50;
        }
        
        /// <summary>
        /// Lands the current trick and adds it to the combo
        /// </summary>
        private void LandTrick()
        {
            if (trickLanded) return; // Prevent landing same trick multiple times
            trickLanded = true;
            
            // Add score based on final trick name and rotation
            currentTrickScore += CalculateRotationScore();
            
            // Add trick to combo list
            if (!string.IsNullOrEmpty(currentTrickName) && currentTrickScore > 0)
            {
                // Add multiplier to trick name string (e.g., Kickflip x2)
                string trickEntry = $"{currentTrickName} x{currentMultiplier}";
                currentCombo.Add(trickEntry);
                
                // Add score to combo pot (base score * multiplier)
                currentComboScore += currentTrickScore * currentMultiplier;
                
                // Increase multiplier for next trick
                currentMultiplier++; 
                
                // Reset combo timer
                comboTimer = COMBO_TIMEOUT;
                
                Debug.Log($"Landed Trick: {trickEntry} | Current Combo: {currentComboScore} (x{currentMultiplier})");
                
                // Update UI
                if (scoreUI != null)
                {
                    scoreUI.UpdateScoreDisplay(totalPlayerScore); // Display total score
                    scoreUI.UpdateComboDisplay(GetComboString(), currentComboScore, currentMultiplier); // Show current combo details
                    scoreUI.DisplayTrickLanded(trickEntry);
                }
            }
            else
            {
                 Debug.Log("Landed, but no trick name/score to add.");
            }

            // Reset for next potential trick in air
            isPerformingTrick = false;
            currentTrickName = "";
            currentTrickScore = 0;
        }

        private int CalculateRotationScore()
        {
             // Basic scoring based on total rotation
            if (totalRotation >= 900f) return 1500;
            if (totalRotation >= 720f) return 1000;
            if (totalRotation >= 540f) return 600;
            if (totalRotation >= 360f) return 300;
            if (totalRotation >= 180f) return 150;
            return 0;
        }
        
        /// <summary>
        /// Simple coroutine to animate a flip trick
        /// </summary>
        private IEnumerator AnimateFlipTrick(bool isKickflip)
        {
            if (skateboardModel == null) yield break;
            
            // Save original rotation
            Quaternion originalRotation = skateboardModel.localRotation;
            float direction = isKickflip ? 1f : -1f;
            float duration = 0.5f;
            float time = 0f;
            
            while (time < duration)
            {
                if (!isPerformingTrick) break;
                
                time += Time.deltaTime;
                float progress = time / duration;
                
                // Rotate around local X axis (for kickflip/heelflip)
                skateboardModel.Rotate(Vector3.forward * flipTrickSpeed * direction * Time.deltaTime, Space.Self);
                
                yield return null;
            }
            
            // Reset to original rotation on land
            skateboardModel.localRotation = originalRotation;
        }
        
        /// <summary>
        /// Resets the combo when the player bails
        /// </summary>
        public void ResetCombo()
        {
            Debug.Log($"Combo Reset! Score was: {currentComboScore}");
            currentCombo.Clear();
            currentComboScore = 0;
            currentMultiplier = 1; // Reset multiplier
            comboTimer = 0f;       // Reset timer
            isPerformingTrick = false;
            currentTrickName = "";
            currentTrickScore = 0;
            
            // Update UI
            if (scoreUI != null)
            {
                scoreUI.UpdateScoreDisplay(totalPlayerScore); // Show total score
                scoreUI.ClearTrickDisplay(); // Clear combo display
            }
        }
        
        /// <summary>
        /// Ends the current combo and banks the score.
        /// </summary>
        public void EndCombo()
        {
            if (currentComboScore > 0)
            {
                Debug.Log($"Combo Ended! Banking score: {currentComboScore}");
                totalPlayerScore += currentComboScore;
                // TODO: Update high score tracking if needed
                
                // Update UI before resetting
                if (scoreUI != null)
                {
                    scoreUI.UpdateScoreDisplay(totalPlayerScore);
                    // Optionally show the final combo score briefly?
                }
                
                // Now reset the combo variables
                ResetCombo(); 
            }
        }
        
        /// <summary>
        /// Gets the current combo score
        /// </summary>
        public int GetComboScore()
        {
            return currentComboScore;
        }
        
        /// <summary>
        /// Gets the current combo as a string
        /// </summary>
        public string GetComboString()
        {
            return string.Join(" + ", currentCombo);
        }
    }
} 