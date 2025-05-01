using UnityEngine;
using System;
using System.Collections;
using Dokkaebi.Grid;
using Dokkaebi.Units;
using Dokkaebi.Common;
using Dokkaebi.Interfaces;
using Dokkaebi.Utilities;
using Dokkaebi.UI;

namespace Dokkaebi.Core
{
    /// <summary>
    /// Manages input handling and coordinates with other managers
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        // Singleton reference
        public static InputManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private UnitManager unitManager;
        [SerializeField] private PlayerActionManager playerActionManager;
        [SerializeField] private UnitSelectionController unitSelectionController;
        [SerializeField] private Dokkaebi.Camera.CameraController cameraController;

        [Header("Input Settings")]
        [SerializeField] private LayerMask unitLayer;
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float raycastDistance = 100f;

        [Header("Debug Visualizers")]
        [SerializeField] private GameObject clickMarkerPrefab;
        [SerializeField] private float clickMarkerDuration = 1.0f;
        [SerializeField] private Color raycastColor = Color.yellow;

        private GameObject currentClickMarker;
        private Coroutine clickMarkerCoroutine;

        // Orbit Input State
        private bool isOrbiting = false;

        // Events
        public event Action<Vector2Int?> OnGridCoordHovered;
        public event Action<DokkaebiUnit> OnUnitHovered;
        public event Action<DokkaebiUnit> OnUnitSelected;
        public event Action OnUnitDeselected;

        // Add this field to the class:
        private bool leftClickProcessedThisFrame = false;

        private void Awake()
        {
            // Singleton setup
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // Find required managers
            if (gridManager == null) gridManager = FindObjectOfType<GridManager>();
            if (unitManager == null) unitManager = FindObjectOfType<UnitManager>();
            if (playerActionManager == null) playerActionManager = FindObjectOfType<PlayerActionManager>();
            if (unitSelectionController == null) unitSelectionController = FindObjectOfType<UnitSelectionController>();
            if (cameraController == null) cameraController = FindObjectOfType<Dokkaebi.Camera.CameraController>();

            if (gridManager == null || unitManager == null || playerActionManager == null)
            {
                Debug.LogError("Required managers not found in scene!");
                return;
            }
            
            if (unitSelectionController == null)
            {
                Debug.LogWarning("UnitSelectionController not found in scene. Tab cycling will not work.");
            }
        }

        private void Update()
        {
            //SmartLogger.Log($"[InputManager.Update] Frame {Time.frameCount}", LogCategory.Input);

            // Debug log to check time scale
            //Debug.Log($"[InputManager] Time.timeScale: {Time.timeScale}");

            // Update hover position
            UpdateHoverPosition();

            // Handle Spacebar press to reset camera
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (cameraController != null)
                {
                    cameraController.ResetToInitialPosition();
                }
            }

            // Handle Tab key press for cycling unit selection
            if (Input.GetKeyDown(KeyCode.Tab) && unitSelectionController != null)
            {
                SmartLogger.Log("[InputManager] Tab key pressed, cycling unit selection", LogCategory.Input);
                unitSelectionController.SelectNextUnitInOrder();
            }

            // Handle Q/E camera orbit input
            HandleOrbitInput();

            // Handle other input
            HandleGeneralInput();
            // Reset the left click processed flag at the end of the frame
            leftClickProcessedThisFrame = false;
        }

        /// <summary>
        /// Handles camera orbital rotation input (Q/E keys).
        /// </summary>
        private void HandleOrbitInput()
        {
            if (cameraController == null || !cameraController.AllowRotation)
                return;

            DokkaebiUnit selectedUnit = UnitManager.Instance != null ? UnitManager.Instance.GetSelectedUnit() : null;
            Transform pivotTransform = (selectedUnit != null) ? selectedUnit.transform : null;

            float rotationAmount = 0f;
            bool qHeld = Input.GetKey(KeyCode.Q);
            bool eHeld = Input.GetKey(KeyCode.E);

            // Start Orbit on KeyDown
            if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.E))
            {
                if (!isOrbiting)
                {
                    SmartLogger.Log("[InputManager] Orbit KeyDown - Starting Orbit", LogCategory.Input);
                    cameraController.StartOrbitRotation(pivotTransform);
                    isOrbiting = true;
                }
            }

            // Set Rotation Amount while Key Held
            if (isOrbiting)
            {
                if (qHeld)
                {
                    rotationAmount = -cameraController.RotationSpeed * Time.deltaTime;
                }
                else if (eHeld)
                {
                    rotationAmount = cameraController.RotationSpeed * Time.deltaTime;
                }
                // If neither is held but we think we are orbiting (e.g., lost focus), 
                // ensure amount is 0. StopOrbitRotation will handle the state change on KeyUp.
                else
                {
                    rotationAmount = 0f;
                }
            }

            // Continuously update the camera controller with the current input amount
            cameraController.SetOrbitRotationInputAmount(rotationAmount);

            // Stop Orbit on KeyUp (only if both keys are released)
            if (isOrbiting && (Input.GetKeyUp(KeyCode.Q) || Input.GetKeyUp(KeyCode.E)))
            {
                // Check if *both* keys are now up before stopping
                if (!Input.GetKey(KeyCode.Q) && !Input.GetKey(KeyCode.E))
                {
                    SmartLogger.Log("[InputManager] Orbit KeyUp - Stopping Orbit", LogCategory.Input);
                    cameraController.StopOrbitRotation();
                    isOrbiting = false;
                }
            }
        }

        /// <summary>
        /// Handles general input like clicks, spacebar, tab, etc.
        /// Renamed from HandleInput to avoid confusion.
        /// </summary>
        private void HandleGeneralInput()
        {
            Ray ray = UnityEngine.Camera.main.ScreenPointToRay(UnityEngine.Input.mousePosition);
            RaycastHit hit;

            // Draw Ray Visualization (Scene View Only)
            Debug.DrawRay(ray.origin, ray.direction * raycastDistance, raycastColor);

            // Process left click for unit selection or ability targeting
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                if (leftClickProcessedThisFrame)
                {
                    // Already processed a left click this frame, skip
                    return;
                }
                leftClickProcessedThisFrame = true;

                var currentState = PlayerActionManager.Instance?.GetCurrentActionState() ?? PlayerActionManager.ActionState.Idle;
                SmartLogger.Log($"[InputManager.HandleGeneralInput] Left Click DETECTED! Current PAM State: {currentState}", LogCategory.Input);

                // Check for unit hit
                if (Physics.Raycast(ray, out hit, raycastDistance, unitLayer))
                {
                    bool hasUnitComponent = hit.collider.GetComponent<DokkaebiUnit>() != null;
                    SmartLogger.Log($"[InputManager.HandleGeneralInput] Raycast hit on UnitLayer: Object='{hit.collider.gameObject.name}', Layer='{LayerMask.LayerToName(hit.collider.gameObject.layer)}', HasDokkaebiUnitComponent={hasUnitComponent}", LogCategory.Input, this);

                    var unit = hit.collider.GetComponent<DokkaebiUnit>();
                    if (unit != null)
                    {
                        SmartLogger.Log($"[InputManager.HandleGeneralInput] Passing UnitClick to PlayerActionManager: Unit='{unit.DisplayName}', UnitId={unit.UnitId}, IsPlayerControlled={unit.IsPlayerControlled}", LogCategory.Input, this);
                        playerActionManager.HandleUnitClick(unit);
                    }
                    else
                    {
                        SmartLogger.LogWarning($"[InputManager.HandleGeneralInput] Raycast hit on UnitLayer, but NO DokkaebiUnit component found! Object='{hit.collider.gameObject.name}', Path='{GetGameObjectPath(hit.collider.gameObject)}'", LogCategory.Input, this);
                    }
                }
                // Check for ground hit
                else if (Physics.Raycast(ray, out hit, raycastDistance, groundLayer))
                {
                    var gridPos = gridManager.WorldToGridPosition(hit.point);
                    SmartLogger.Log($"[InputManager] Mouse click at world {hit.point}, grid {gridPos}", LogCategory.Input, this);
                    SmartLogger.Log($"[InputManager.HandleGeneralInput] Raycast hit GroundLayer: WorldPos={hit.point}, GridPos={gridPos}", LogCategory.Input, this);
                    playerActionManager.HandleGroundClick(gridPos.ToVector2Int());
                    ShowClickMarker(gridPos);
                }
                else
                {
                    SmartLogger.Log("[InputManager.HandleGeneralInput] Left Click hit nothing (neither Unit nor Ground layer).", LogCategory.Input, this);
                }
            }
            // Handle right click for deselection/cancelling
            else if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                SmartLogger.Log("[InputManager.HandleGeneralInput] Right Click DETECTED!", LogCategory.Input);
                if (playerActionManager.GetCurrentActionState() == PlayerActionManager.ActionState.SelectingAbilityTarget ||
                    playerActionManager.GetCurrentActionState() == PlayerActionManager.ActionState.SelectingZoneDestination)
                {
                    SmartLogger.Log("[InputManager.HandleGeneralInput] Right Click during targeting state - Cancelling via PlayerActionManager", LogCategory.Input);
                    playerActionManager.CancelAbilityTargeting();
                }
                else
                {
                    SmartLogger.Log("[InputManager.HandleGeneralInput] Right Click in non-targeting state - Deselecting via UnitSelectionController", LogCategory.Input);
                    OnUnitDeselected?.Invoke();
                }
            }
            // Handle ability keyboard shortcuts (1-4)
            if (unitManager != null)
            {
                var selectedUnit = unitManager.GetSelectedUnit();
                if (selectedUnit != null)
                {
                    var abilities = selectedUnit.GetAbilities();
                    if (abilities != null)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                            {
                                if (i < abilities.Count)
                                {
                                    SmartLogger.Log($"[InputManager] LOG_CHECK: Hotkey {i+1}: About to check CanUseAbility for '{abilities[i]?.displayName}'.", LogCategory.Input);
                                    if (selectedUnit.CanUseAbility(abilities[i]))
                                    {
                                        SmartLogger.Log($"[InputManager] LOG_CHECK: Hotkey {i+1}: CanUseAbility PASSED for '{abilities[i]?.displayName}'.", LogCategory.Input);
                                        playerActionManager?.StartAbilityTargeting(selectedUnit, i);
                                        break;
                                    }
                                    else
                                    {
                                        SmartLogger.LogWarning($"[InputManager] LOG_CHECK: Hotkey {i+1}: CanUseAbility FAILED for '{abilities[i]?.displayName}'.", LogCategory.Input);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void UpdateHoverPosition()
        {
            Ray ray = UnityEngine.Camera.main.ScreenPointToRay(UnityEngine.Input.mousePosition);
            RaycastHit hit;

            // Check for unit hover
            if (Physics.Raycast(ray, out hit, raycastDistance, unitLayer))
            {
                DokkaebiUnit unit = hit.collider.GetComponent<DokkaebiUnit>();
                if (unit != null)
                {
                    OnUnitHovered?.Invoke(unit);
                    return;
                }
            }

            // Check for ground hover
            if (Physics.Raycast(ray, out hit, raycastDistance, groundLayer))
            {
                GridPosition gridPos = gridManager.WorldToNearestGrid(hit.point);
                // Convert GridPosition to Vector2Int using z instead of y
                Vector2Int vectorPos = new Vector2Int(gridPos.x, gridPos.z);
                OnGridCoordHovered?.Invoke(vectorPos);
            }
            else
            {
                OnGridCoordHovered?.Invoke(null);
            }
        }

        private void ShowClickMarker(GridPosition gridPos)
        {
            if (clickMarkerPrefab == null) return;

            if (currentClickMarker == null)
            {
                currentClickMarker = Instantiate(clickMarkerPrefab, transform);
            }

            // Ensure GridManager instance is available
            if (gridManager == null) gridManager = GridManager.Instance;
            if (gridManager == null) return;

            // Convert grid position to world position
            Vector3 worldPos = gridManager.GridToWorldPosition(gridPos);
            currentClickMarker.transform.position = worldPos + Vector3.up * 0.05f; // Slightly above ground
            currentClickMarker.SetActive(true);

            // Restart the timer coroutine
            if (clickMarkerCoroutine != null)
            {
                StopCoroutine(clickMarkerCoroutine);
            }
            clickMarkerCoroutine = StartCoroutine(ClickMarkerTimer());
        }

        private void HideClickMarker()
        {
            if (currentClickMarker != null)
            {
                currentClickMarker.SetActive(false);
            }
            if (clickMarkerCoroutine != null)
            {
                StopCoroutine(clickMarkerCoroutine);
                clickMarkerCoroutine = null;
            }
        }

        private IEnumerator ClickMarkerTimer()
        {
            yield return new WaitForSeconds(clickMarkerDuration);
            if (currentClickMarker != null)
            {
                currentClickMarker.SetActive(false);
            }
            clickMarkerCoroutine = null;
        }

        // Helper method to get full hierarchy path of GameObject
        private string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
} 

