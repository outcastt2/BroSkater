using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BroSkater.Player
{
    /// <summary>
    /// Handles the setup of the player GameObject, adding required components and configuring them
    /// </summary>
    public class PlayerSetup : MonoBehaviour
    {
        [Header("Required Components")]
        [SerializeField] private bool addRigidbody = true;
        [SerializeField] private bool addCollider = true;
        
        [Header("Component Settings")]
        [SerializeField] private float playerHeight = 1.8f;
        [SerializeField] private float playerRadius = 0.3f;
        [SerializeField] private float playerMass = 70f;
        [SerializeField] private PhysicsMaterial playerPhysicsMaterial;
        
        [Header("Child Objects")]
        [SerializeField] private bool createChildCamera = true;
        [SerializeField] private Vector3 cameraOffset = new Vector3(0, 2f, -5f);
        
        [Header("Mesh References")]
        [SerializeField] private GameObject skaterModelPrefab;
        [SerializeField] private GameObject skateboardModelPrefab;
        
        private void Awake()
        {
            // Set up the player tag
            gameObject.tag = "Player";
            
            // Add required components
            SetupComponents();
            
            // Create child objects
            SetupChildObjects();
            
            // Add visual models
            SetupVisuals();
        }
        
        private void SetupComponents()
        {
            // Add Rigidbody if needed
            if (addRigidbody && GetComponent<Rigidbody>() == null)
            {
                Rigidbody rb = gameObject.AddComponent<Rigidbody>();
                rb.mass = playerMass;
                rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.useGravity = false; // We'll handle gravity in the physics component
            }
            
            // Add Collider if needed
            if (addCollider && GetComponent<Collider>() == null)
            {
                CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
                capsule.height = playerHeight;
                capsule.radius = playerRadius;
                capsule.center = new Vector3(0, playerHeight * 0.5f, 0);
                
                if (playerPhysicsMaterial != null)
                    capsule.material = playerPhysicsMaterial;
            }
            
            // Add player controller
            if (GetComponent<PlayerController>() == null)
            {
                gameObject.AddComponent<PlayerController>();
            }
            
            // Add input handler if needed
            if (GetComponent<PlayerInputHandler>() == null)
            {
                gameObject.AddComponent<PlayerInputHandler>();
            }
        }
        
        private void SetupChildObjects()
        {
            // Create camera if needed
            if (createChildCamera && FindFirstObjectByType<UnityEngine.Camera>() == null)
            {
                // Create camera gameobject
                GameObject cameraObj = new GameObject("PlayerCamera");
                cameraObj.transform.position = transform.position + cameraOffset;
                cameraObj.transform.LookAt(transform);
                
                // Add camera component
                UnityEngine.Camera cam = cameraObj.AddComponent<UnityEngine.Camera>();
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 1000f;
                
                // Add audio listener
                cameraObj.AddComponent<AudioListener>();
                
                // Set camera as main camera
                cameraObj.tag = "MainCamera";
                
                // Adjust field of view
                cam.fieldOfView = 60f;
            }
        }
        
        private void SetupVisuals()
        {
            // Add skater model if provided
            if (skaterModelPrefab != null)
            {
                GameObject skaterModel = Instantiate(skaterModelPrefab, transform);
                skaterModel.transform.localPosition = new Vector3(0, 0, 0);
                skaterModel.transform.localRotation = Quaternion.identity;
                skaterModel.name = "SkaterModel";
            }
            
            // Add skateboard model if provided
            if (skateboardModelPrefab != null)
            {
                GameObject boardModel = Instantiate(skateboardModelPrefab, transform);
                boardModel.transform.localPosition = new Vector3(0, 0.1f, 0);
                boardModel.transform.localRotation = Quaternion.identity;
                boardModel.name = "SkateboardModel";
            }
            else
            {
                // Create a simple board representation if no model provided
                CreateSimpleBoard();
            }
        }
        
        private void CreateSimpleBoard()
        {
            // Create a simple board mesh as placeholder
            GameObject boardObj = new GameObject("SimpleBoard");
            boardObj.transform.SetParent(transform);
            boardObj.transform.localPosition = new Vector3(0, 0.1f, 0);
            
            // Add mesh filter and renderer
            MeshFilter meshFilter = boardObj.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = boardObj.AddComponent<MeshRenderer>();
            
            // Create a simple board mesh
            Mesh boardMesh = new Mesh();
            
            // Board dimensions
            float length = 0.8f;
            float width = 0.2f;
            float height = 0.02f;
            
            // Create vertices
            Vector3[] vertices = new Vector3[8];
            // Bottom
            vertices[0] = new Vector3(-length/2, 0, -width/2);
            vertices[1] = new Vector3(length/2, 0, -width/2);
            vertices[2] = new Vector3(length/2, 0, width/2);
            vertices[3] = new Vector3(-length/2, 0, width/2);
            // Top
            vertices[4] = new Vector3(-length/2, height, -width/2);
            vertices[5] = new Vector3(length/2, height, -width/2);
            vertices[6] = new Vector3(length/2, height, width/2);
            vertices[7] = new Vector3(-length/2, height, width/2);
            
            // Create triangles
            int[] triangles = new int[]
            {
                // Bottom
                0, 1, 2,
                0, 2, 3,
                // Top
                4, 6, 5,
                4, 7, 6,
                // Sides
                0, 4, 1,
                1, 4, 5,
                1, 5, 2,
                2, 5, 6,
                2, 6, 3,
                3, 6, 7,
                3, 7, 0,
                0, 7, 4
            };
            
            // Assign to mesh
            boardMesh.vertices = vertices;
            boardMesh.triangles = triangles;
            boardMesh.RecalculateNormals();
            
            // Assign mesh to filter
            meshFilter.mesh = boardMesh;
            
            // Create a simple material
            Material boardMaterial = new Material(Shader.Find("Standard"));
            boardMaterial.color = new Color(0.3f, 0.2f, 0.1f); // Brown color
            meshRenderer.material = boardMaterial;
        }
    }
} 