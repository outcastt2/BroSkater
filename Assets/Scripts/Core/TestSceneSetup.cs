using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BroSkater.Environment;

namespace BroSkater.Core
{
    /// <summary>
    /// Sets up a test scene with player, park, and necessary components
    /// </summary>
    public class TestSceneSetup : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Transform spawnPoint;
        
        [Header("Components")]
        [SerializeField] private ParkGenerator parkGenerator;
        [SerializeField] private GameManager gameManager;
        
        [Header("Settings")]
        [SerializeField] private bool generateParkOnStart = true;
        [SerializeField] private bool spawnPlayerOnStart = true;
        
        private void Awake()
        {
            if (gameManager == null)
            {
                // Try to find GameManager in scene
                gameManager = Object.FindFirstObjectByType<GameManager>();
                
                // If not found, create one
                if (gameManager == null)
                {
                    GameObject gmObj = new GameObject("GameManager");
                    gameManager = gmObj.AddComponent<GameManager>();
                }
            }
        }
        
        private void Start()
        {
            // Generate test park if needed
            if (generateParkOnStart && parkGenerator != null)
            {
                parkGenerator.GeneratePark();
            }
            
            // Spawn player if needed
            if (spawnPlayerOnStart)
            {
                SpawnPlayer();
            }
        }
        
        [ContextMenu("Setup Test Scene")]
        public void SetupTestScene()
        {
            // Create all necessary components
            SetupGameManager();
            SetupParkGenerator();
            SetupSpawnPoint();
            
            // Generate park
            if (parkGenerator != null)
            {
                parkGenerator.GeneratePark();
            }
            
            // Spawn player
            SpawnPlayer();
            
            Debug.Log("Test scene setup complete!");
        }
        
        private void SetupGameManager()
        {
            if (gameManager == null)
            {
                // Create game manager if it doesn't exist
                GameObject gmObj = new GameObject("GameManager");
                gameManager = gmObj.AddComponent<GameManager>();
            }
        }
        
        private void SetupParkGenerator()
        {
            if (parkGenerator == null)
            {
                // Create park generator if it doesn't exist
                GameObject parkObj = new GameObject("ParkGenerator");
                parkGenerator = parkObj.AddComponent<ParkGenerator>();
            }
        }
        
        private void SetupSpawnPoint()
        {
            if (spawnPoint == null)
            {
                // Create a spawn point at a reasonable height
                GameObject spawnObj = new GameObject("SpawnPoint");
                spawnObj.transform.position = new Vector3(0, 1, 0);
                spawnPoint = spawnObj.transform;
            }
        }
        
        public void SpawnPlayer()
        {
            if (playerPrefab == null)
            {
                Debug.LogWarning("No player prefab assigned. Cannot spawn player.");
                return;
            }
            
            // Check if spawn point exists
            if (spawnPoint == null)
            {
                SetupSpawnPoint();
            }
            
            // Spawn player at spawn point
            if (gameManager != null)
            {
                gameManager.SpawnPlayer(playerPrefab, spawnPoint.position, spawnPoint.rotation);
                Debug.Log("Player spawned at " + spawnPoint.position);
            }
            else
            {
                GameObject player = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
                Debug.Log("Player spawned at " + spawnPoint.position + " (without GameManager)");
            }
        }
        
        [ContextMenu("Respawn Player")]
        public void RespawnPlayer()
        {
            // Find existing player
            GameObject existingPlayer = GameObject.FindGameObjectWithTag("Player");
            if (existingPlayer != null)
            {
                Destroy(existingPlayer);
            }
            
            // Spawn a new player
            SpawnPlayer();
        }
    }
} 