using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BroSkater.Core
{
    /// <summary>
    /// Manages core game functionality and state
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        #region Singleton
        public static GameManager Instance { get; private set; }
        
        private void Awake()
        {
            // Implement singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Initialize systems
            InitializeGame();
        }
        #endregion
        
        [Header("Game Settings")]
        [SerializeField] private bool isPaused = false;
        [SerializeField] private float timeScale = 1.0f;
        
        [Header("Player References")]
        [SerializeField] private GameObject currentPlayer;
        
        public GameObject CurrentPlayer => currentPlayer;
        public bool IsPaused => isPaused;
        
        private void InitializeGame()
        {
            Debug.Log("GameManager: Initializing game systems...");
            Time.timeScale = timeScale;
        }
        
        private void Update()
        {
            // Handle pause input
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TogglePause();
            }
        }
        
        /// <summary>
        /// Spawns the player in the scene
        /// </summary>
        /// <param name="playerPrefab">The player prefab to instantiate</param>
        /// <param name="position">Spawn position</param>
        /// <param name="rotation">Spawn rotation</param>
        /// <returns>The spawned player GameObject</returns>
        public GameObject SpawnPlayer(GameObject playerPrefab, Vector3 position, Quaternion rotation)
        {
            if (playerPrefab == null)
            {
                Debug.LogError("GameManager: Cannot spawn player - null prefab reference");
                return null;
            }
            
            // Destroy current player if it exists
            if (currentPlayer != null)
            {
                Destroy(currentPlayer);
            }
            
            // Instantiate new player
            currentPlayer = Instantiate(playerPrefab, position, rotation);
            Debug.Log("GameManager: Player spawned at " + position);
            
            return currentPlayer;
        }
        
        /// <summary>
        /// Toggles the pause state of the game
        /// </summary>
        public void TogglePause()
        {
            isPaused = !isPaused;
            Time.timeScale = isPaused ? 0 : timeScale;
            
            Debug.Log("GameManager: Game " + (isPaused ? "paused" : "resumed"));
            
            // Additional pause logic can be added here (UI, etc.)
        }
        
        /// <summary>
        /// Restarts the current level/scene
        /// </summary>
        public void RestartScene()
        {
            // Resume time if paused
            if (isPaused)
            {
                isPaused = false;
                Time.timeScale = timeScale;
            }
            
            // Reload the current scene
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
            );
        }
        
        /// <summary>
        /// Quits the application
        /// </summary>
        public void QuitGame()
        {
            Debug.Log("GameManager: Quitting application");
            
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
    }
} 