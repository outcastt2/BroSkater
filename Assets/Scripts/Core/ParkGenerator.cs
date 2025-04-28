using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BroSkater.Core
{
    /// <summary>
    /// Generates a skate park environment for testing
    /// </summary>
    public class ParkGenerator : MonoBehaviour
    {
        [Header("Park Objects")]
        [SerializeField] private GameObject groundPrefab;
        [SerializeField] private List<GameObject> rampPrefabs = new List<GameObject>();
        [SerializeField] private List<GameObject> railPrefabs = new List<GameObject>();
        [SerializeField] private List<GameObject> obstaclePrefabs = new List<GameObject>();
        
        [Header("Spawn Settings")]
        [SerializeField] private Vector2 parkSize = new Vector2(50f, 50f);
        [SerializeField] private int numberOfRamps = 5;
        [SerializeField] private int numberOfRails = 5;
        [SerializeField] private int numberOfObstacles = 5;
        [SerializeField] private float minDistanceBetweenObjects = 5f;
        [SerializeField] private Transform parkParent;
        
        [Header("Test Park Preset")]
        [SerializeField] private bool useTestParkPreset = true;
        [SerializeField] private GameObject testParkPrefab;
        
        private List<GameObject> spawnedObjects = new List<GameObject>();
        private GameObject ground;
        
        private void Awake()
        {
            if (parkParent == null)
            {
                parkParent = transform;
            }
        }
        
        /// <summary>
        /// Generates a skate park based on the current settings
        /// </summary>
        public void GeneratePark()
        {
            ClearPark();
            
            if (useTestParkPreset && testParkPrefab != null)
            {
                GenerateTestPark();
                return;
            }
            
            CreateGround();
            SpawnParkObjects();
            
            Debug.Log("ParkGenerator: Park generation complete");
        }
        
        /// <summary>
        /// Generates a predefined test park from a prefab
        /// </summary>
        private void GenerateTestPark()
        {
            GameObject park = Instantiate(testParkPrefab, Vector3.zero, Quaternion.identity, parkParent);
            spawnedObjects.Add(park);
            Debug.Log("ParkGenerator: Test park generated from prefab");
        }
        
        /// <summary>
        /// Creates the ground plane for the park
        /// </summary>
        private void CreateGround()
        {
            if (groundPrefab == null)
            {
                Debug.LogWarning("ParkGenerator: Ground prefab not assigned, creating a basic plane");
                
                // Create a basic plane if no ground prefab is assigned
                GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                plane.transform.localScale = new Vector3(parkSize.x / 10f, 1f, parkSize.y / 10f);
                plane.transform.position = Vector3.zero;
                plane.transform.parent = parkParent;
                
                ground = plane;
            }
            else
            {
                ground = Instantiate(groundPrefab, Vector3.zero, Quaternion.identity, parkParent);
            }
            
            spawnedObjects.Add(ground);
        }
        
        /// <summary>
        /// Spawns all park objects (ramps, rails, obstacles)
        /// </summary>
        private void SpawnParkObjects()
        {
            // Spawn ramps
            SpawnObjects(rampPrefabs, numberOfRamps);
            
            // Spawn rails
            SpawnObjects(railPrefabs, numberOfRails);
            
            // Spawn obstacles
            SpawnObjects(obstaclePrefabs, numberOfObstacles);
        }
        
        /// <summary>
        /// Spawns a specific number of objects from a list of prefabs
        /// </summary>
        private void SpawnObjects(List<GameObject> prefabs, int count)
        {
            if (prefabs == null || prefabs.Count == 0)
            {
                Debug.LogWarning("ParkGenerator: No prefabs assigned for spawning");
                return;
            }
            
            for (int i = 0; i < count; i++)
            {
                if (TryGetValidSpawnPosition(out Vector3 position))
                {
                    // Get random prefab from the list
                    GameObject prefab = prefabs[Random.Range(0, prefabs.Count)];
                    
                    // Random rotation (only y-axis)
                    Quaternion rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
                    
                    // Spawn object
                    GameObject obj = Instantiate(prefab, position, rotation, parkParent);
                    spawnedObjects.Add(obj);
                }
            }
        }
        
        /// <summary>
        /// Tries to find a valid spawn position that doesn't overlap with other objects
        /// </summary>
        private bool TryGetValidSpawnPosition(out Vector3 position)
        {
            // Maximum attempts to find a valid position
            int maxAttempts = 50;
            
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // Generate random position within park bounds
                float x = Random.Range(-parkSize.x / 2f, parkSize.x / 2f);
                float z = Random.Range(-parkSize.y / 2f, parkSize.y / 2f);
                position = new Vector3(x, 0, z);
                
                // Check if position is valid (not too close to other objects)
                bool isValid = true;
                foreach (GameObject obj in spawnedObjects)
                {
                    if (obj == ground) continue; // Ignore ground
                    
                    float distance = Vector3.Distance(position, obj.transform.position);
                    if (distance < minDistanceBetweenObjects)
                    {
                        isValid = false;
                        break;
                    }
                }
                
                if (isValid)
                {
                    return true;
                }
            }
            
            Debug.LogWarning("ParkGenerator: Failed to find valid spawn position after " + maxAttempts + " attempts");
            position = Vector3.zero;
            return false;
        }
        
        /// <summary>
        /// Clears all spawned park objects
        /// </summary>
        public void ClearPark()
        {
            foreach (GameObject obj in spawnedObjects)
            {
                if (obj != null)
                {
                    DestroyImmediate(obj);
                }
            }
            
            spawnedObjects.Clear();
            ground = null;
            
            Debug.Log("ParkGenerator: Park cleared");
        }
        
        /// <summary>
        /// Get a random spawn point within the park
        /// </summary>
        public Vector3 GetRandomSpawnPoint()
        {
            float x = Random.Range(-parkSize.x / 2f + 5f, parkSize.x / 2f - 5f);
            float z = Random.Range(-parkSize.y / 2f + 5f, parkSize.y / 2f - 5f);
            
            // Add some height to make sure the player doesn't clip into ground
            return new Vector3(x, 1f, z);
        }
    }
} 