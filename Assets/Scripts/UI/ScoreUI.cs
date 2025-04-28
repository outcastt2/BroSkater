using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BroSkater.Player;

namespace BroSkater.UI
{
    /// <summary>
    /// Manages the UI display for trick scores and combos
    /// </summary>
    public class ScoreUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI trickNameText;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI comboText;
        [SerializeField] private Animator trickAnimator;
        
        [Header("Player References")]
        [SerializeField] private TrickSystem trickSystem;
        
        // Animation parameter names
        private readonly string showTrickAnim = "ShowTrick";
        
        private void Start()
        {
            // Find trick system if not assigned
            if (trickSystem == null)
            {
                trickSystem = Object.FindFirstObjectByType<TrickSystem>();
            }
            
            // Initialize UI
            UpdateScoreDisplay(0);
            ClearTrickDisplay();
        }
        
        /// <summary>
        /// Shows a trick with name and score
        /// </summary>
        public void ShowTrick(string trickName, int score)
        {
            // Update trick text
            if (trickNameText != null)
            {
                trickNameText.text = trickName;
            }
            
            // Update score display
            UpdateScoreDisplay(score);
            
            // Play animation if available
            if (trickAnimator != null)
            {
                trickAnimator.SetTrigger(showTrickAnim);
            }
            
            // Auto-hide after delay
            StartCoroutine(HideTrickAfterDelay(2f));
        }
        
        /// <summary>
        /// Updates the score display
        /// </summary>
        public void UpdateScoreDisplay(int score)
        {
            if (scoreText != null)
            {
                scoreText.text = score.ToString();
            }
            
            // Update combo text if trick system is available
            // Remove combo logic from here, move to dedicated method
            /*
            if (trickSystem != null && comboText != null)
            {
                string comboString = trickSystem.GetComboString();
                int comboScore = trickSystem.GetComboScore();
                
                if (comboScore > 0)
                {
                    comboText.text = $"{comboString}\n{comboScore} pts";
                }
                else
                {
                    comboText.text = "";
                }
            }
            */
        }
        
        /// <summary>
        /// Updates the combo display UI elements.
        /// </summary>
        /// <param name="comboString">The string representation of the combo (e.g., "Kickflip + Grind")</param>
        /// <param name="comboScore">The current score of the combo pot.</param>
        /// <param name="multiplier">The current combo multiplier.</param>
        public void UpdateComboDisplay(string comboString, int comboScore, int multiplier)
        {
            if (comboText != null)
            {
                if (comboScore > 0)
                {
                    // Example format: Kickflip + Grind x3
                    //                 1500 pts
                    comboText.text = $"{comboString} x{multiplier}\n{comboScore} pts";
                }
                else
                {
                    comboText.text = ""; // Clear if no combo active
                }
            }
        }

        /// <summary>
        /// Displays the name of the last landed trick briefly.
        /// </summary>
        /// <param name="trickEntry">The formatted name of the trick (e.g., Kickflip x2)</param>
        public void DisplayTrickLanded(string trickEntry)
        {
            if (trickNameText != null)
            {
                trickNameText.text = trickEntry;
                
                // Auto-hide after delay
                // Stop previous hide coroutine if running
                StopCoroutine(nameof(HideTrickAfterDelay)); 
                StartCoroutine(nameof(HideTrickAfterDelay), 2f); 
            }
            
            // Optional: Trigger animation if needed
            // if (trickAnimator != null)
            // {
            //     trickAnimator.SetTrigger(showTrickAnim); 
            // }
        }
        
        /// <summary>
        /// Clears the trick display
        /// </summary>
        public void ClearTrickDisplay()
        {
            if (trickNameText != null)
            {
                trickNameText.text = "";
            }
        }
        
        /// <summary>
        /// Coroutine to hide trick display after a delay
        /// </summary>
        private IEnumerator HideTrickAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            ClearTrickDisplay();
        }
    }
} 