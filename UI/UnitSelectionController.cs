using System.Collections.Generic;
using UnityEngine;
using Dokkaebi.Core;
using Dokkaebi.Interfaces;
using Dokkaebi.Common;
using Dokkaebi.Units;
using Dokkaebi.Utilities;
using Dokkaebi.Grid;
using Dokkaebi.Pathfinding;
using Dokkaebi.Core.Data;
using Dokkaebi.Zones;
using System.Linq;
using Dokkaebi.Camera;

namespace Dokkaebi.UI
{
    /// <summary>
    /// Handles unit selection and targeting input
    /// </summary>
    public class UnitSelectionController : MonoBehaviour, IUpdateObserver
    {
        [Header("References")]
        [SerializeField] private PlayerActionManager actionManager;
        [SerializeField] private PreviewManager previewManager;
        [SerializeField] private AbilitySelectionUI abilityUI;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private UnitManager unitManager;
        [SerializeField] private InputManager inputManager;
        [SerializeField] private ZoneManager zoneManager;
        [SerializeField] private CameraController cameraController;

        [Header("Unit Detail Panels")]
        [SerializeField] private GameObject playerUnitDetailPanelGameObject;
        [SerializeField] private GameObject enemyUnitDetailPanelGameObject;

        private UnitInfoPanel playerUnitDetailPanel;
        private UnitInfoPanel enemyUnitDetailPanel;

        [Header("Selection Settings")]
        [SerializeField] private float raycastDistance = 100f;
        [SerializeField] private LayerMask unitLayer;
        [SerializeField] private LayerMask groundLayer;

        [Header("Visual Feedback")]
        [SerializeField] private GameObject moveTargetMarker;
        [SerializeField] private GameObject abilityTargetMarker;
        [SerializeField] private Material validMoveMaterial;
        [SerializeField] private Material invalidMoveMaterial;
        [SerializeField] private Material validAbilityTargetMaterial;
        [SerializeField] private Material invalidAbilityTargetMaterial;

        // State tracking
        private bool isSelectingAbility;
        private bool isTargetingAbility;
        private AbilityData selectedAbility;
        private HashSet<Interfaces.GridPosition> validAbilityTargets;
        private List<GameObject> moveTargetMarkers = new List<GameObject>();
        private List<GameObject> abilityTargetMarkers = new List<GameObject>();
        private DokkaebiUnit selectedUnit;
        private IZoneInstance currentSelectedZoneForShift;

        // Unit cycling state
        private List<DokkaebiUnit> sortedPlayerUnits;
        private int currentUnitIndex = -1;

        // Dictionary to define Calling type priority for sorting (lower value = higher priority)
        private Dictionary<string, int> callingTypePriorityMap = new Dictionary<string, int>
        {
            { "Duelist", 0 },
            { "Mage", 1 },
            { "Marksman", 2 },
            { "Specialist", 3 },
            { "Controller", 4 },
            { "Guardian", 5 }
        };

        // Update registration
        private bool isRegisteredForUpdate = false;

        private void Awake()
        {
            SmartLogger.Log($"[UnitSelectionController.Awake START] Running for {gameObject.name} (Instance ID: {GetInstanceID()}).", LogCategory.General, this);

            // Get references using Singleton Instance where available, otherwise use FindObjectOfType as a fallback
            // Use PlayerActionManager.Instance for the authoritative manager
            actionManager = PlayerActionManager.Instance; // Get reference using Singleton Instance
            if (actionManager == null)
            {
                SmartLogger.LogError("[UnitSelectionController.Awake] PlayerActionManager.Instance is null! Ensure PlayerActionManager exists and is initialized before UnitSelectionController.", LogCategory.General, this);
                // Fallback to FindObjectOfType if necessary, but log a warning
                actionManager = FindObjectOfType<PlayerActionManager>();
                if (actionManager == null)
                {
                     SmartLogger.LogError("[UnitSelectionController.Awake] PlayerActionManager reference not found via Instance or FindObjectOfType! Unit selection and actions will not work.", LogCategory.General, this);
                     // Note: If actionManager is critically missing, you might want to disable this component.
                     // enabled = false; // Uncomment to disable this component if actionManager is null
                     // return; // Uncomment to exit Awake if disabling
                }
                else
                {
                    SmartLogger.LogWarning("[UnitSelectionController.Awake] PlayerActionManager.Instance was null, using FindObjectOfType as fallback. Ensure proper initialization order.", LogCategory.General, this);
                }
            }

            // Log the Instance ID of the actionManager reference obtained
            SmartLogger.Log($"[UnitSelectionController.Awake] Obtained PlayerActionManager reference. actionManager is null: {actionManager == null}. Instance ID: {(actionManager != null ? actionManager.GetInstanceID().ToString() : "NULL")}.", LogCategory.General, this); // Added log


            // Get other references using FindObjectOfType if they are not Singletons
            // (Assuming PreviewManager, AbilitySelectionUI, GridManager, UnitManager, InputManager, ZoneManager might not be Singletons,
            // if they are, use their .Instance property instead)
            if (previewManager == null) previewManager = FindObjectOfType<PreviewManager>();
            if (abilityUI == null) abilityUI = FindObjectOfType<AbilitySelectionUI>();
            if (gridManager == null) gridManager = FindObjectOfType<GridManager>();
            if (unitManager == null) unitManager = FindObjectOfType<UnitManager>();
            if (inputManager == null) inputManager = FindObjectOfType<InputManager>();
            if (zoneManager == null) zoneManager = FindObjectOfType<ZoneManager>();


            // Check if all required references are found for the component to function
            if (actionManager == null || previewManager == null || abilityUI == null || gridManager == null || unitManager == null || inputManager == null || zoneManager == null) // Include actionManager in the final null check
            {
                SmartLogger.LogError("[UnitSelectionController.Awake] One or more required references are still null after attempting to find them. Unit selection and actions may not work correctly.", LogCategory.General, this);
                // Note: The component might need to be disabled here if critical references are missing.
                // enabled = false; // Uncomment to disable component if critical references are missing
                // return; // Return early if disabling component
            }

            if (cameraController == null) cameraController = FindObjectOfType<CameraController>();
            if (cameraController == null)
            {
                SmartLogger.LogWarning("[UnitSelectionController.Awake] CameraController reference not found. Camera follow functionality will be disabled.", LogCategory.General, this);
            }

            // Get UnitInfoPanel components from GameObjects
            if (playerUnitDetailPanelGameObject != null)
            {
                playerUnitDetailPanel = playerUnitDetailPanelGameObject.GetComponent<UnitInfoPanel>();
                if (playerUnitDetailPanel == null)
                {
                    SmartLogger.LogError("[UnitSelectionController.Awake] playerUnitDetailPanelGameObject does not have a UnitInfoPanel component!", LogCategory.UI, this);
                }
                else
                {
                    playerUnitDetailPanelGameObject.SetActive(false); // Ensure inactive at start
                    SmartLogger.Log("[UnitSelectionController.Awake] Found Player UnitInfoPanel component.", LogCategory.UI, this);
                }
            }
            else
            {
                SmartLogger.LogError("[UnitSelectionController.Awake] playerUnitDetailPanelGameObject reference is not set in the inspector!", LogCategory.UI, this);
            }

            if (enemyUnitDetailPanelGameObject != null)
            {
                enemyUnitDetailPanel = enemyUnitDetailPanelGameObject.GetComponent<UnitInfoPanel>();
                if (enemyUnitDetailPanel == null)
                {
                    SmartLogger.LogError("[UnitSelectionController.Awake] enemyUnitDetailPanelGameObject does not have a UnitInfoPanel component!", LogCategory.UI, this);
                }
                else
                {
                    enemyUnitDetailPanelGameObject.SetActive(false); // Ensure inactive at start
                    SmartLogger.Log("[UnitSelectionController.Awake] Found Enemy UnitInfoPanel component.", LogCategory.UI, this);
                }
            }
            else
            {
                SmartLogger.LogError("[UnitSelectionController.Awake] enemyUnitDetailPanelGameObject reference is not set in the inspector!", LogCategory.UI, this);
            }

            // Initialize state
            isSelectingAbility = false;
            isTargetingAbility = false;
            validAbilityTargets = new HashSet<Interfaces.GridPosition>();
            currentSelectedZoneForShift = null;
            sortedPlayerUnits = new List<DokkaebiUnit>();

            // Subscribe to PlayerActionManager events ONLY if actionManager is not null
            if (actionManager != null)
            {
                actionManager.OnAbilityTargetingStarted += HandleAbilityTargetingStarted;
                actionManager.OnAbilityTargetingCancelled += HandleAbilityTargetingCancelled;
                actionManager.OnZoneDestinationSelectionStarted += HandleZoneDestinationSelectionStarted; // Subscribe to the new event
                 SmartLogger.Log("[UnitSelectionController.Awake] Subscribed to PlayerActionManager events.", LogCategory.General, this); // Added log
            }
            else
            {
                 SmartLogger.LogError("[UnitSelectionController.Awake] Cannot subscribe to PlayerActionManager events because actionManager is null.", LogCategory.General, this); // Added log
            }

            SmartLogger.Log($"[UnitSelectionController.Awake END] Initialization complete for {gameObject.name} (Instance ID: {GetInstanceID()}). actionManager is null: {actionManager == null}.", LogCategory.General, this);
        }

        private void OnEnable()
        {
            DokkaebiUpdateManager.Instance?.RegisterUpdateObserver(this);
            
            // Subscribe to input manager events
            if (inputManager != null)
            {
                inputManager.OnUnitSelected += HandleUnitSelected;
                inputManager.OnUnitDeselected += HandleUnitDeselected;
                inputManager.OnGridCoordHovered += HandleGridCoordHovered;
            }
        }

        private void OnDisable()
        {
            DokkaebiUpdateManager.Instance?.UnregisterUpdateObserver(this);
            
            // Unsubscribe from input manager events
            if (inputManager != null)
            {
                inputManager.OnUnitSelected -= HandleUnitSelected;
                inputManager.OnUnitDeselected -= HandleUnitDeselected;
                inputManager.OnGridCoordHovered -= HandleGridCoordHovered;
            }

            // Clean up any remaining markers
            HideMoveTargets();
            HideAbilityTargets();
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (actionManager != null)
            {
                actionManager.OnAbilityTargetingStarted -= HandleAbilityTargetingStarted;
                actionManager.OnAbilityTargetingCancelled -= HandleAbilityTargetingCancelled;
                actionManager.OnZoneDestinationSelectionStarted -= HandleZoneDestinationSelectionStarted; // Unsubscribe from the new event
            }
        }

        public void CustomUpdate(float deltaTime)
        {
            // The state transitions and highlighting are now driven by events.
            // CustomUpdate is primarily used for input handling and continuous updates like hover feedback.

            // Handle input based on the current state (retrieved from PlayerActionManager)
            PlayerActionManager.ActionState currentPAMState = actionManager?.GetCurrentActionState() ?? PlayerActionManager.ActionState.Idle;
            //SmartLogger.Log($"[UnitSelectionController.CustomUpdate] Frame {Time.frameCount}. Current PAM State: {currentPAMState}. isTargetingAbility: {isTargetingAbility}.", LogCategory.UI, this); // Modified log

            if (currentPAMState == PlayerActionManager.ActionState.SelectingAbilityTarget || currentPAMState == PlayerActionManager.ActionState.SelectingZoneDestination) // Check both targeting states
            {
                HandleAbilityTargetingInput(); // Handle input for both targeting phases
            }
            else // currentPAMState == PlayerActionManager.ActionState.Idle or other unexpected state
            {
                // Idle state or unexpected state - handle unit selection input
                if (isTargetingAbility || currentSelectedZoneForShift != null) // If we were in any targeting mode (before state change)
                {
                     SmartLogger.Log("[UnitSelectionController] PAM state is Idle or unexpected. Cancelling any active targeting/destination visuals.", LogCategory.UI, this);
                     HandleAbilityTargetingCancelled(); // Clean up any remaining targeting/destination visuals
                }
                // Ensure flags are correct for Idle state
                isTargetingAbility = false;
                currentSelectedZoneForShift = null;


                HandleSelectionInput(); // Handle normal unit selection/movement input
            }

            // Note: PreviewManager is typically updated by InputManager's OnGridCoordHovered event.
            // You might need to update PreviewManager.UpdatePreview to handle the SelectingZoneDestination state
            // and display zone shift valid destinations on hover, in addition to the permanent markers shown here.

        }

        private void HandleSelectionInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = UnityEngine.Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;

                // Check for unit hit
                if (Physics.Raycast(ray, out hit, raycastDistance, unitLayer))
                {
                    var unit = hit.collider.GetComponent<DokkaebiUnit>();
                    if (unit != null)
                    {
                        HandleUnitClick(unit);
                    }
                }
                // Check for ground hit
                else if (Physics.Raycast(ray, out hit, raycastDistance, groundLayer))
                {
                    var gridPos = gridManager.WorldToGridPosition(hit.point);
                    HandleGroundClick(gridPos);
                }
            }
        }

        private void HandleAbilityTargetingInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = UnityEngine.Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;

                // Check for unit hit
                if (Physics.Raycast(ray, out hit, raycastDistance, unitLayer))
                {
                    var unit = hit.collider.GetComponent<DokkaebiUnit>();
                    if (unit != null)
                    {
                        actionManager.HandleUnitClick(unit);
                    }
                }
                // Check for ground hit
                else if (Physics.Raycast(ray, out hit, raycastDistance, groundLayer))
                {
                    var gridPos = gridManager.WorldToGridPosition(hit.point);
                    actionManager.HandleGroundClick(gridPos.ToVector2Int());
                }
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                actionManager.CancelAbilityTargeting();
            }
        }

        private void HandleUnitClick(DokkaebiUnit unit)
        {
            // Log entry into the method
            string unitNameLog = (unit != null) ? unit.DisplayName : "NULL";
            int unitIdLog = (unit != null) ? unit.UnitId : -1;
            bool isPlayerLog = (unit != null) ? unit.IsPlayerControlled : false;
            PlayerActionManager.ActionState stateLog = actionManager?.GetCurrentActionState() ?? PlayerActionManager.ActionState.Idle;
            SmartLogger.Log($"[UnitSelectionController.HandleUnitClick ENTRY] Unit: {unitNameLog} (ID: {unitIdLog}, Player: {isPlayerLog}), Current PAM State: {stateLog}", LogCategory.UI, this);

            if (actionManager == null)
            {
                SmartLogger.LogError("[HandleUnitClick] PlayerActionManager reference is null! Cannot process click.", LogCategory.General, this);
                return;
            }

            PlayerActionManager.ActionState currentState = actionManager.GetCurrentActionState();
            string unitName = (unit != null) ? unit.DisplayName : "NULL";
            bool isPlayerUnit = (unit != null) ? unit.IsPlayerControlled : false;
            // SmartLogger.Log($"[HandleUnitClick] Clicked Unit: {unitName} (Player: {isPlayerUnit}). Current State: {currentState}", LogCategory.Input, this); // Redundant with entry log


            switch (currentState)
            {
                case PlayerActionManager.ActionState.Idle:
                    // Log entry into Idle/non-targeting case
                    SmartLogger.Log($"[UnitSelectionController.HandleUnitClick] Handling click in Idle state (standard selection/info display). Unit clicked: {unitName}", LogCategory.UI | LogCategory.Input, this);
                    
                    // Log before the main if/else if/else structure
                    SmartLogger.Log($"[UnitSelectionController.HandleUnitClick - Idle] Checking unit status. Unit is null: {unit == null}", LogCategory.UI, this);
                    
                    if (unit != null) // Clicked on a unit
                    {
                         // Log before player check
                        SmartLogger.Log($"[UnitSelectionController.HandleUnitClick - Idle] Performing check: unit.IsPlayerControlled ({unit.IsPlayerControlled})", LogCategory.UI, this);
                        if (unit.IsPlayerControlled)
                        {
                            // Log player unit case
                            SmartLogger.Log($"[UnitSelectionController.HandleUnitClick - Idle] Processing PLAYER unit click: {unitName}. Selecting unit.", LogCategory.UI, this);
                            SelectUnit(unit); // Handles setting selectedUnit, ability UI, and calls playerUnitDetailPanel.SetUnit
                            HideEnemyDetailPanel(); // Hide enemy panel using existing helper
                        }
                        else // Clicked on an enemy unit
                        {
                             // Log enemy unit case
                            SmartLogger.Log($"[UnitSelectionController.HandleUnitClick - Idle] Processing ENEMY unit click: {unitName}. Displaying info.", LogCategory.UI, this);
                            // If a player unit was selected, deselect it first using the existing handler
                            if (selectedUnit != null && selectedUnit.IsPlayerControlled)
                            {
                                SmartLogger.Log($"[UnitSelectionController.HandleUnitClick - Idle] Deselecting previously selected player unit: {selectedUnit.DisplayName} before showing enemy info.", LogCategory.Input, this);
                                HandleUnitDeselected(); // This handles clearing selection, hiding panels, etc.
                            }

                            // Now show the enemy panel
                            if (enemyUnitDetailPanel != null && enemyUnitDetailPanelGameObject != null)
                            {
                                enemyUnitDetailPanelGameObject.SetActive(true);
                                enemyUnitDetailPanel.SetUnit(unit); // Populate enemy panel
                                SmartLogger.Log($"[UnitSelectionController.HandleUnitClick - Idle] Showing and populating enemy panel for {unitName}.", LogCategory.UI, this);
                            }
                            // Ensure player panel is hidden (HandleUnitDeselected might already do this, but belt-and-suspenders)
                            HidePlayerDetailPanel();
                        }
                    }
                    else // Clicked on empty ground (or unit == null was passed somehow)
                    {
                         // Log null unit/ground case
                        SmartLogger.Log("[UnitSelectionController.HandleUnitClick - Idle] Processing NULL unit / Ground click. Deselecting.", LogCategory.UI, this);
                        HandleUnitDeselected(); // Handles clearing selection and hiding both panels
                    }
                    break;

                case PlayerActionManager.ActionState.SelectingAbilityTarget:
                    // Reverted this section to original/previous state to avoid linter errors
                    SmartLogger.Log($"[HandleUnitClick] Handling click in SelectingAbilityTarget state. Unit clicked: {unitName}", LogCategory.Input, this);
                    actionManager.HandleUnitClick(unit); // Forward to PAM
                    break;

                case PlayerActionManager.ActionState.SelectingZoneDestination:
                     // Reverted this section to original/previous state to avoid linter errors
                    SmartLogger.Log($"[HandleUnitClick] Handling click in SelectingZoneDestination state. Unit clicked: {unitName}", LogCategory.Input, this);
                    actionManager.HandleUnitClick(unit); // Forward to PAM
                    break;

                default:
                    SmartLogger.LogWarning($"[HandleUnitClick] Click received in unhandled state: {currentState}", LogCategory.General, this);
                    break;
            }
        }

        private void HandleGroundClick(Interfaces.GridPosition gridPos)
        {
            if (actionManager == null)
            {
                 SmartLogger.LogError("[HandleGroundClick] PlayerActionManager reference is null! Cannot process click.", LogCategory.General, this);
                return;
            }

            PlayerActionManager.ActionState currentState = actionManager.GetCurrentActionState();
            SmartLogger.Log($"[HandleGroundClick] Clicked Ground Position: {gridPos}. Current State: {currentState}", LogCategory.Input, this);


            switch (currentState)
            {
                case PlayerActionManager.ActionState.Idle:
                    SmartLogger.Log("[HandleGroundClick - Idle] Ground clicked. Deselecting unit.", LogCategory.Input, this);
                    HandleUnitDeselected(); // Handles clearing selection and hiding both panels
                    break;

                case PlayerActionManager.ActionState.SelectingAbilityTarget:
                     // Reverted this section to original/previous state to avoid linter errors
                    SmartLogger.Log($"[HandleGroundClick] Handling ground click in SelectingAbilityTarget state at {gridPos}", LogCategory.Input, this);
                    actionManager.HandleGroundClick(gridPos.ToVector2Int()); // Forward to PAM
                    break;

                case PlayerActionManager.ActionState.SelectingZoneDestination:
                    // Reverted this section to original/previous state to avoid linter errors
                    SmartLogger.Log($"[HandleGroundClick] Handling ground click in SelectingZoneDestination state at {gridPos}", LogCategory.Input, this);
                    actionManager.HandleGroundClick(gridPos.ToVector2Int()); // Forward to PAM
                    break;

                default:
                    SmartLogger.LogWarning($"[HandleGroundClick] Ground click received in unhandled state: {currentState}", LogCategory.General, this);
                    break;
            }
        }

        private void HandleUnitSelected(DokkaebiUnit unit)
        {
            SmartLogger.Log($"[UnitSelectionController] HandleUnitSelected called for unit: {unit?.GetUnitName()}, isTargetingAbility: {isTargetingAbility}", LogCategory.Ability);
            
            // Ignore unit selection events during ability targeting
            if (isTargetingAbility)
            {
                SmartLogger.Log("[UnitSelectionController] Ignoring unit selection during ability targeting", LogCategory.Ability);
                return;
            }

            if (unit != null && unit.IsPlayerControlled)
            {
                SelectUnit(unit);
            }
        }

        private void HandleUnitDeselected()
        {
            SmartLogger.Log("[UnitSelectionController.HandleUnitDeselected] Called. Deselecting unit and hiding panels.", LogCategory.UI, this);
            
            // Store unit name before clearing selection for logging
            string deselectedUnitName = selectedUnit?.GetUnitName() ?? "None";

            // Clear local selection state
            selectedUnit = null;
            // No need to call unitManager.SetSelectedUnit(null) here, as SelectUnit is the counterpart
            // and unit deselection is primarily handled by InputManager -> this controller
            // and PlayerActionManager handles its own state.

            // Cancel any ongoing targeting action in PlayerActionManager
            actionManager?.CancelAbilityTargeting();
            SmartLogger.Log($"[UnitSelectionController.HandleUnitDeselected] Called actionManager.CancelAbilityTargeting(). Previous unit: {deselectedUnitName}", LogCategory.UI, this);

            // Hide UI elements related to selection
            HideMoveTargets();
            HideAbilityTargets();
            abilityUI?.SetUnit(null); // Use SetUnit(null) to hide/reset the Ability UI

            // Hide both detail panels
            HideBothDetailPanels();

            // Optionally, if camera follows selection, reset camera focus
            // cameraController?.ResetFocus();

            SmartLogger.Log("[UnitSelectionController.HandleUnitDeselected] Finished.", LogCategory.UI, this);
        }

        private void HandleGridCoordHovered(Vector2Int? gridCoord)
        {
            if (previewManager == null) return;

            if (gridCoord.HasValue)
            {
                // Update preview based on current mode (movement or ability targeting)
                previewManager.UpdatePreview(gridCoord.Value);
            }
            else
            {
                // Clear preview when not hovering over grid
                previewManager.UpdatePreview(new Vector2Int(-1, -1));
            }
        }

        private void SelectUnit(DokkaebiUnit unit)
        {
            if (unit == null)
            {
                SmartLogger.LogWarning("[UnitSelectionController] SelectUnit called with null unit", LogCategory.Input, this);
                return;
            }

            SmartLogger.Log($"[UnitSelectionController] SelectUnit ENTRY - Unit: {unit.GetUnitName()}, Previous selected unit: {selectedUnit?.GetUnitName()}", LogCategory.Input, this);

            // Update selection state
            selectedUnit = unit;
            unitManager.SetSelectedUnit(unit);

            // Make camera follow the selected unit
            if (cameraController != null)
            {
                SmartLogger.Log($"[UnitSelectionController] Setting camera to follow unit: {unit.GetUnitName()}", LogCategory.Input, this);
                cameraController.SetFollowTarget(unit.transform);
            }

            // Update UI
            if (abilityUI != null)
            {
                SmartLogger.Log($"[UnitSelectionController] Updating abilityUI for unit: {unit.GetUnitName()}", LogCategory.Input, this);
                abilityUI.SetUnit(unit);
            }
            else
            {
                SmartLogger.LogWarning("[UnitSelectionController] abilityUI reference is null", LogCategory.Input, this);
            }
            
            // Update unit info panel
            if (playerUnitDetailPanel != null)
            {
                SmartLogger.Log($"[UnitSelectionController] Updating unitInfoPanel for unit: {unit.GetUnitName()}", LogCategory.Input, this);
                playerUnitDetailPanel.SetUnit(unit);
            }
            else
            {
                SmartLogger.LogWarning("[UnitSelectionController] playerUnitDetailPanel reference is null", LogCategory.Input, this);
            }

            // Show valid move targets
            ShowMoveTargets();

            SmartLogger.Log($"[UnitSelectionController] SelectUnit COMPLETE for unit: {unit.GetUnitName()}", LogCategory.Input, this);
        }

        private void ShowMoveTargets()
        {
            HideMoveTargets();

            if (selectedUnit == null || selectedUnit.GetComponent<DokkaebiMovementHandler>() == null)
                return;

            var movementHandler = selectedUnit.GetComponent<DokkaebiMovementHandler>();
            var validMovePositions = movementHandler.GetValidMovePositions();

            foreach (var pos in validMovePositions)
            {
                if (gridManager.IsValidGridPosition(pos))
                {
                    var worldPos = gridManager.GridToWorldPosition(pos);
                    worldPos.y += 0.01f; // Add the desired Y offset
                    var marker = Instantiate(moveTargetMarker, worldPos, Quaternion.identity);
                    marker.GetComponent<Renderer>().material = validMoveMaterial;
                    moveTargetMarkers.Add(marker);
                }
            }
        }

        private void HideMoveTargets()
        {
            // Clean up move target markers
            foreach (var marker in moveTargetMarkers)
            {
                if (marker != null)
                {
                    Destroy(marker);
                }
            }
            moveTargetMarkers.Clear();
        }

        private void HandleAbilityTargetingStarted(AbilityData ability)
        {
            SmartLogger.Log($"[UnitSelectionController] Ability targeting started: {ability?.displayName}", LogCategory.Ability);
            isTargetingAbility = true;
            selectedAbility = ability;
            // Clear any existing selection visuals (move targets)
            HideMoveTargets();

            // Special handling for Terrain Shift - don't show initial target highlights, wait for first click
            if (selectedAbility != null && selectedAbility.abilityId == "TerrainShift")
            {
                SmartLogger.Log("[UnitSelectionController] Terrain Shift targeting started. Waiting for first click to select zone.", LogCategory.Ability, this);
                HideAbilityTargets(); // Ensure any old ability target highlights are off
                // Don't call ShowAbilityTargets() here for Terrain Shift
                // Highlights for Terrain Shift destinations will be shown after the first click, in CustomUpdate or another handler.
            }
            else
            {
                // For other abilities, show valid ability targets immediately (the ability's range/area)
                ShowAbilityTargets();
            }
        }

        private void HandleAbilityTargetingCancelled()
        {
            SmartLogger.Log($"[UnitSelectionController.HandleAbilityTargetingCancelled] ENTRY. isTargetingAbility: {isTargetingAbility}", LogCategory.UI, this); // Added Log

            isTargetingAbility = false; // Ensure targeting state is false

            // Clear visual previews by calling the PreviewManager
            if (PreviewManager.Instance != null)
            {
                SmartLogger.Log("[UnitSelectionController.HandleAbilityTargetingCancelled] Calling PreviewManager.Instance.ClearHighlights().", LogCategory.UI, this); // Added Log before call
                PreviewManager.Instance.ClearHighlights(); // This should trigger the logs inside ClearHighlights
                SmartLogger.Log("[UnitSelectionController.HandleAbilityTargetingCancelled] Returned from PreviewManager.Instance.ClearHighlights().", LogCategory.UI, this); // Added Log after call
            }
            else
            {
                SmartLogger.LogWarning("[UnitSelectionController.HandleAbilityTargetingCancelled] PreviewManager.Instance is null. Cannot clear highlights.", LogCategory.UI, this); // Added Log if Instance is null
            }

            // Other cleanup (e.g., hiding ability UI if necessary, typically handled elsewhere)
            // For Terrain Shift, also ensure the specific selection state in PlayerActionManager is reset (PAM does this)

            SmartLogger.Log("[UnitSelectionController.HandleAbilityTargetingCancelled] EXIT.", LogCategory.UI, this); // Added Log
        }

        private void ShowAbilityTargets()
        {
            if (selectedAbility == null) return;

            var selectedUnit = unitManager.GetSelectedUnit();
            if (selectedUnit == null) return;

            // Clear any existing markers first
            HideAbilityTargets();

            // Get valid ability targets
            validAbilityTargets = GetValidAbilityTargets(selectedUnit, selectedAbility);

            // Show ability target markers
            foreach (var pos in validAbilityTargets)
            {
                var worldPos = gridManager.GridToWorldPosition(pos);
                var marker = Instantiate(abilityTargetMarker, worldPos, Quaternion.identity);
                marker.GetComponent<Renderer>().material = validAbilityTargetMaterial;
                abilityTargetMarkers.Add(marker);
            }
        }

        private void HideAbilityTargets()
        {
            // Clean up ability target markers (including zone shift destination markers)
            foreach (var marker in abilityTargetMarkers)
            {
                if (marker != null)
                {
                    Destroy(marker);
                }
            }
            abilityTargetMarkers.Clear();
            SmartLogger.Log("[UnitSelectionController] All ability/destination target markers cleared.", LogCategory.UI, this);
        }

        private HashSet<Interfaces.GridPosition> GetValidAbilityTargets(DokkaebiUnit unit, AbilityData ability)
        {
            var validTargets = new HashSet<Interfaces.GridPosition>();
            var currentPos = unit.GetGridPosition();
            var range = ability.range;

            // Get all positions within ability range
            for (int x = -range; x <= range; x++)
            {
                for (int z = -range; z <= range; z++)
                {
                    var pos = new Interfaces.GridPosition(currentPos.x + x, currentPos.z + z);
                    if (!gridManager.IsValidGridPosition(pos)) continue;

                    // Check if position is valid based on ability targeting rules
                    bool isValid = false;

                    // Check ground targeting
                    if (ability.targetsGround)
                    {
                        isValid = true;
                    }

                    // Check unit targeting
                    var unitsAtPos = unitManager.GetUnitsAtPosition(pos.ToVector2Int());
                    foreach (var targetUnit in unitsAtPos)
                    {
                        if (ability.targetsSelf && targetUnit == unit)
                        {
                            isValid = true;
                            break;
                        }
                        if (ability.targetsAlly && targetUnit.IsPlayerControlled == unit.IsPlayerControlled)
                        {
                            isValid = true;
                            break;
                        }
                        if (ability.targetsEnemy && targetUnit.IsPlayerControlled != unit.IsPlayerControlled)
                        {
                            isValid = true;
                            break;
                        }
                    }

                    if (isValid)
                    {
                        validTargets.Add(pos);
                    }
                }
            }

            return validTargets;
        }

        private void ShowValidZoneShiftDestinations(IZoneInstance zoneToShift)
        {
            // Cast to concrete ZoneInstance at the beginning to access DisplayName, Id, and GetGridPosition
            if (!(zoneToShift is Dokkaebi.Zones.ZoneInstance concreteZone))
            {
                SmartLogger.LogError($"[UnitSelectionController.ShowValidZoneShiftDestinations] Received IZoneInstance is not a ZoneInstance! Type: {zoneToShift?.GetType().Name ?? "NULL"}. Cannot show destinations.", LogCategory.UI, this); // Added null check
                return;
            }

            if (gridManager == null || zoneManager == null || abilityTargetMarker == null)
            {
                SmartLogger.LogWarning("[UnitSelectionController.ShowValidZoneShiftDestinations] Cannot show valid zone shift destinations: Missing GridManager, ZoneManager, or abilityTargetMarker prefab reference.", LogCategory.UI, this); // Modified log message
                return;
            }


            HideAbilityTargets(); // Clear any previously shown ability/destination targets

            GridPosition zonePosition = concreteZone.GetGridPosition(); // Use concreteZone
            string zoneDisplayName = concreteZone.DisplayName; // Use concreteZone
            int zoneId = concreteZone.Id; // Use concreteZone


            int maxShiftDistance = 2; // Terrain Shift moves 2 tiles

            // Get all grid positions within the max shift distance from the zone's current position
            // This uses Manhattan distance, adjust if your shift rules are different (e.g., cardinal only)
            List<GridPosition> potentialDestinations = gridManager.GetGridPositionsInRange(zonePosition, maxShiftDistance);
            SmartLogger.Log($"[UnitSelectionController.ShowValidZoneShiftDestinations] Found {potentialDestinations.Count} potential zone shift destinations within range {maxShiftDistance} of zone at {zonePosition} for zone '{zoneDisplayName}' (Instance:{zoneId}).", LogCategory.UI, this); // Added zone info to log


            // Filter potential destinations based on validity rules (e.g., not void spaces, not occupied by unshiftable objects)
            List<GridPosition> validDestinations = new List<GridPosition>();
            foreach (var pos in potentialDestinations)
            {
                // Check if the destination is a valid grid position (GridManager.GetGridPositionsInRange should already filter this)

                // Check if the destination is a void space
                if (zoneManager.IsVoidSpace(pos))
                {
                    SmartLogger.Log($"[UnitSelectionController.ShowValidZoneShiftDestinations] Skipping potential destination {pos} - it is a void space.", LogCategory.UI, this);
                    continue; // Skip void spaces
                }

                // Add other validation rules here if needed (e.g., cannot shift onto certain objects/units)
                // For now, only void space is checked.

                validDestinations.Add(pos); // Position is valid for shifting
            }

            // Show markers on valid destination tiles
            foreach (var pos in validDestinations)
            {
                var worldPos = gridManager.GridToWorldPosition(pos);
                // Instantiate the ability target marker prefab
                var marker = Instantiate(abilityTargetMarker, worldPos, Quaternion.identity);
                // Set material to indicate a valid target (can use a specific material for shift destinations if desired)
                if (marker != null && marker.GetComponent<Renderer>() != null && validAbilityTargetMaterial != null) // Added null check for marker
                {
                    marker.GetComponent<Renderer>().material = validAbilityTargetMaterial; // Or a different material for shift destinations
                }
                else
                {
                     SmartLogger.LogWarning($"[UnitSelectionController.ShowValidZoneShiftDestinations] Cannot set material for marker at {pos}. Marker, Renderer, or validAbilityTargetMaterial is null.", LogCategory.UI, this); // Modified log message
                }
                abilityTargetMarkers.Add(marker); // Reuse the abilityTargetMarkers list for destination markers
            }
             SmartLogger.Log($"[UnitSelectionController.ShowValidZoneShiftDestinations] Showing {validDestinations.Count} valid zone shift destinations for zone '{zoneDisplayName}' (Instance:{zoneId}).", LogCategory.UI, this); // Added zone info to log

        }

        // Add this new handler method
        private void HandleZoneDestinationSelectionStarted(IZoneInstance zoneToShift)
        {
            // Purpose: Visualize valid destination tiles for Terrain Shift after the first click (zone selection).
            // 1. Check if the received zoneToShift is not null.
            if (zoneToShift != null)
            {
                // 2. Store the zone instance for reference during targeting.
                currentSelectedZoneForShift = zoneToShift;
                // 3. Set the targeting flag so UI knows we're in ability targeting mode.
                isTargetingAbility = true;
                // 4. Show valid destination highlights for Terrain Shift using PreviewManager.
                //    - zoneToShift.Position: the original grid position of the zone
                //    - 2: the shift distance for Terrain Shift
                //    - GridManager.Instance: reference to the grid
                PreviewManager.Instance?.ShowShiftDestinationPreview(zoneToShift.Position, 2, GridManager.Instance);
                SmartLogger.Log($"[UnitSelectionController.HandleZoneDestinationSelectionStarted] Showing valid Terrain Shift destinations for zone at {zoneToShift.Position}.", LogCategory.UI, this);
            }
            else
            {
                SmartLogger.LogWarning("[UnitSelectionController.HandleZoneDestinationSelectionStarted] Received null zone instance. Cancelling targeting.", LogCategory.UI, this);
                HandleAbilityTargetingCancelled();
            }
        }

        public void SelectNextUnitInOrder()
        {
            SmartLogger.Log("[UnitSelectionController] Selecting next unit in order...", LogCategory.UI, this);
            
            // Get all player-controlled units
            List<DokkaebiUnit> playerUnits = unitManager.GetUnitsByPlayer(true);
            if (playerUnits == null || playerUnits.Count == 0)
            {
                SmartLogger.LogWarning("[UnitSelectionController] No player units found to select", LogCategory.UI, this);
                return;
            }
            
            // Sort units based on calling type
            sortedPlayerUnits = playerUnits.OrderBy(unit => {
                string callingId = unit.GetCalling()?.callingId ?? "";
                // If calling ID is not in our priority map, put it at the end
                return callingTypePriorityMap.TryGetValue(callingId, out int priority) ? priority : 999;
            }).ToList();
            
            // Find current unit index if a unit is selected
            if (selectedUnit != null)
            {
                currentUnitIndex = sortedPlayerUnits.IndexOf(selectedUnit);
                if (currentUnitIndex == -1)
                {
                    SmartLogger.Log("[UnitSelectionController] Currently selected unit not found in sorted list", LogCategory.UI, this);
                }
            }
            
            // Increment the index (or start at 0)
            if (currentUnitIndex == -1 || currentUnitIndex >= sortedPlayerUnits.Count - 1)
            {
                currentUnitIndex = 0; // Wrap to beginning
            }
            else
            {
                currentUnitIndex++;
            }
            
            // Select the unit at the current index
            if (currentUnitIndex >= 0 && currentUnitIndex < sortedPlayerUnits.Count)
            {
                DokkaebiUnit unitToSelect = sortedPlayerUnits[currentUnitIndex];
                SmartLogger.Log($"[UnitSelectionController] Selecting unit: {unitToSelect.GetUnitName()} at index {currentUnitIndex}", LogCategory.UI, this);
                SelectUnit(unitToSelect);
            }
            else
            {
                SmartLogger.LogWarning($"[UnitSelectionController] Invalid unit index: {currentUnitIndex}, total units: {sortedPlayerUnits.Count}", LogCategory.UI, this);
            }
        }

        private void HideBothDetailPanels()
        {
            SmartLogger.Log("[UnitSelectionController] HideBothDetailPanels called.", LogCategory.UI, this);
            if (playerUnitDetailPanel != null)
            {
                playerUnitDetailPanel.SetUnit(null); // Clear/unsubscribe
            }
            if (playerUnitDetailPanelGameObject != null && playerUnitDetailPanelGameObject.activeSelf)
            {
                playerUnitDetailPanelGameObject.SetActive(false);
                SmartLogger.Log("[UnitSelectionController] Deactivated Player Detail Panel.", LogCategory.UI, this);
            }

            if (enemyUnitDetailPanel != null)
            {
                enemyUnitDetailPanel.SetUnit(null); // Clear/unsubscribe
            }
            if (enemyUnitDetailPanelGameObject != null && enemyUnitDetailPanelGameObject.activeSelf)
            {
                enemyUnitDetailPanelGameObject.SetActive(false);
                SmartLogger.Log("[UnitSelectionController] Deactivated Enemy Detail Panel.", LogCategory.UI, this);
            }
        }

        private void HideEnemyDetailPanel()
        {
            SmartLogger.Log("[UnitSelectionController] HideEnemyDetailPanel called.", LogCategory.UI, this);
            if (enemyUnitDetailPanel != null)
            {
                enemyUnitDetailPanel.SetUnit(null); // Clear/unsubscribe
            }
            if (enemyUnitDetailPanelGameObject != null && enemyUnitDetailPanelGameObject.activeSelf)
            {
                enemyUnitDetailPanelGameObject.SetActive(false);
                SmartLogger.Log("[UnitSelectionController] Deactivated Enemy Detail Panel.", LogCategory.UI, this);
            }
        }

        private void HidePlayerDetailPanel()
        {
            SmartLogger.Log("[UnitSelectionController] HidePlayerDetailPanel called.", LogCategory.UI, this);
            if (playerUnitDetailPanel != null)
            {
                playerUnitDetailPanel.SetUnit(null); // Clear/unsubscribe
            }
            if (playerUnitDetailPanelGameObject != null && playerUnitDetailPanelGameObject.activeSelf)
            {
                playerUnitDetailPanelGameObject.SetActive(false);
                SmartLogger.Log("[UnitSelectionController] Deactivated Player Detail Panel.", LogCategory.UI, this);
            }
        }
    }
}
