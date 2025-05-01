/*
 * IMPLEMENTING PLAYERACTIONMANAGER - Adapted to match project's types and methods
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using Dokkaebi.Core.Networking;
using Dokkaebi.Core.Networking.Commands;
using Dokkaebi.Grid;
using Dokkaebi.Units;
using Dokkaebi.Zones;
using Dokkaebi.Core.Data;
using Dokkaebi.Common;
using Dokkaebi.Interfaces;
using Dokkaebi.Utilities;
using Dokkaebi.Pathfinding;
using System.Linq;
using Dokkaebi.UI;
using Dokkaebi.Core;

namespace Dokkaebi.Core
{
    /// <summary>
    /// Manages player actions using the Command Pattern
    /// Validates commands locally for immediate feedback
    /// Sends commands to server via NetworkingManager for authoritative validation and execution
    /// </summary>
    public class PlayerActionManager : MonoBehaviour, IUpdateObserver
    {
        // Singleton reference
        public static PlayerActionManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private NetworkingManager networkManager;
        [SerializeField] private DokkaebiTurnSystemCore turnSystem;
        [SerializeField] private UnitManager unitManager;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private ZoneManager zoneManager;

        [Header("Settings")]
        [SerializeField] private bool useLocalValidationFirst = true;
        [SerializeField] private bool enableLocalExecution = true;
        [SerializeField] private bool enableDebugLogs = true;

        // State tracking
        public enum ActionState
        {
            Idle,
            SelectingAbilityTarget,
            SelectingZoneDestination
        }
        private ActionState currentState = ActionState.Idle;
        
        private AbilityData selectedAbility;
        private int selectedAbilityIndex;
        private DokkaebiUnit selectedUnit;
        private IZoneInstance selectedZoneToShift;

        // Added state variables for Terrain Shift
        private bool isTerrainShiftSelected = false;
        private GridPosition selectedZonePosition = GridPosition.invalid;

        // Two-target ability selection state
        private DokkaebiUnit firstTargetUnit = null;
        private bool isSelectingSecondTarget = false;

        // Events
        public event Action<bool, string> OnCommandResult; // Success, message
        public event Action<AbilityData> OnAbilityTargetingStarted;
        public event Action OnAbilityTargetingCancelled;
        public event Action<IZoneInstance> OnZoneDestinationSelectionStarted;
        public event Action<DokkaebiUnit> OnFirstTargetSelected; // Event to signal the first target is selected

        private void Awake()
        {
            // Singleton reference
            if (Instance == null)
            {
                Instance = this;
                SmartLogger.Log($"[PlayerActionManager.Awake] Singleton Instance set to {gameObject.name} (Instance ID: {GetInstanceID()}).", LogCategory.Ability, this);
            }
            else
            {
                SmartLogger.LogWarning($"[PlayerActionManager.Awake] Multiple PlayerActionManager instances detected. Destroying duplicate {gameObject.name} (Instance ID: {GetInstanceID()}). Existing instance is {Instance.gameObject.name} (Instance ID: {Instance.GetInstanceID()}).", LogCategory.Ability, this);
                Destroy(gameObject);
                return;
            }

            // Get component references
            if (turnSystem == null) turnSystem = FindObjectOfType<DokkaebiTurnSystemCore>();
            if (unitManager == null) unitManager = FindObjectOfType<UnitManager>();
            if (gridManager == null) gridManager = FindObjectOfType<GridManager>();

            if (turnSystem == null || unitManager == null || gridManager == null)
            {
                Debug.LogError("Required managers not found in scene!");
                return;
            }

            // Get references if needed
            if (networkManager == null) networkManager = FindObjectOfType<NetworkingManager>();
            if (zoneManager == null) zoneManager = FindObjectOfType<ZoneManager>();

            // Register for updates
            DokkaebiUpdateManager.Instance?.RegisterUpdateObserver(this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            DokkaebiUpdateManager.Instance?.UnregisterUpdateObserver(this);
        }

        public void CustomUpdate(float deltaTime)
        {
            // Monitor state every frame
            //SmartLogger.Log($"[PAM Update Cycle] Frame {Time.frameCount}, Current State: {currentState}, Instance ID: {this.GetInstanceID()}", LogCategory.Ability);
        }

        #region Command Execution

        private void SetState(ActionState newState)
        {
            SmartLogger.Log($"[PAM.SetState] State transition: {currentState} -> {newState}", LogCategory.Ability);
            
            // Add stack trace for all state transitions
            if (newState == ActionState.Idle && currentState == ActionState.SelectingAbilityTarget)
            {
                SmartLogger.LogWarning($"[PAM.SetState] Transitioning from SelectingAbilityTarget to Idle. Stack trace:\n{System.Environment.StackTrace}", LogCategory.Ability);
            }
            
            // Reset two-target selection fields when exiting targeting states
            if (currentState == ActionState.SelectingAbilityTarget && newState != ActionState.SelectingAbilityTarget)
            {
                firstTargetUnit = null;
                isSelectingSecondTarget = false;
                SmartLogger.Log("[PAM.SetState] Resetting two-target selection fields.", LogCategory.Ability);
            }
            
            currentState = newState;
        }

        /// <summary>
        /// Start ability targeting mode
        /// </summary>
        public void StartAbilityTargeting(DokkaebiUnit unit, int abilityIndex)
        {
            SmartLogger.Log($"[PAM.StartAbilityTargeting] ENTRY. Unit: {(unit ? unit.GetUnitName() : "NULL")}, AbilityIndex: {abilityIndex}, Current State: {currentState}", LogCategory.Ability);
            
            if (currentState == ActionState.SelectingAbilityTarget)
            {
                SmartLogger.LogWarning("[PAM.StartAbilityTargeting] Already in targeting state. Cancelling previous targeting.", LogCategory.Ability);
                CancelAbilityTargeting();
            }

            if (unit == null || !unit.IsAlive)
            {
                DebugLog("Cannot start ability targeting: Invalid unit");
                return;
            }

            var abilities = unit.GetAbilities();
            if (abilityIndex < 0 || abilityIndex >= abilities.Count)
            {
                DebugLog("Cannot start ability targeting: Invalid ability index");
                return;
            }

            var ability = abilities[abilityIndex];
            
            // Check for instant-cast self-targeting abilities (like Rewind)
            if (ability.range == 0 && ability.targetsSelf && !ability.targetsGround && !ability.targetsAlly && !ability.targetsEnemy)
            {
                SmartLogger.Log($"[PAM.StartAbilityTargeting] Self-cast ability '{ability.displayName}' detected. Executing immediately.", LogCategory.Ability);
                // Use caster's current position for the command (it will be ignored by Rewind logic anyway)
                ExecuteAbilityCommand(unit.GetUnitId(), abilityIndex, unit.GetGridPosition().ToVector2Int());
                return;
            }

            selectedUnit = unit;
            selectedAbilityIndex = abilityIndex;
            selectedAbility = ability;
            SetState(ActionState.SelectingAbilityTarget);

            OnAbilityTargetingStarted?.Invoke(selectedAbility);
            Debug.Log($"[PAM.StartAbilityTargeting] State changed to: {currentState}, Selected Ability: {selectedAbility.displayName}");
            
            // Add final state verification log
            SmartLogger.Log($"[PAM.StartAbilityTargeting] END. State confirmed set to: {this.currentState}", LogCategory.Ability);
        }

        /// <summary>
        /// Cancel ability targeting mode
        /// </summary>
        public void CancelAbilityTargeting()
        {
            SmartLogger.Log($"[PAM.CancelAbilityTargeting] ENTRY. Current state: {currentState}, Selected Ability: {(selectedAbility != null ? selectedAbility.displayName : "NULL")}, isTerrainShiftSelected: {isTerrainShiftSelected}", LogCategory.Ability, this);
            // Only cancel if we are in a relevant targeting state
            if (currentState != ActionState.SelectingAbilityTarget && currentState != ActionState.SelectingZoneDestination)
            {
                SmartLogger.LogWarning($"[PAM.CancelAbilityTargeting] Called while not in targeting state ({{currentState}}). Aborting.", LogCategory.Ability, this);
                return;
            }

            selectedUnit = null;
            selectedAbility = null;
            selectedZoneToShift = null; // Ensure this is also reset
            isTerrainShiftSelected = false; // Ensure this is reset

            SetState(ActionState.Idle); // Transition to Idle state

            // The UnitSelectionController should be subscribed to OnAbilityTargetingCancelled
            // and call PreviewManager.ClearHighlights()
            OnAbilityTargetingCancelled?.Invoke();

            SmartLogger.Log("[PAM.CancelAbilityTargeting] State ensured Idle and event fired. EXIT.", LogCategory.Ability, this);
        }

        /// <summary>
        /// Handle a click on a unit
        /// </summary>
        public void HandleUnitClick(DokkaebiUnit targetUnit)
        {
            SmartLogger.Log($"[PAM.HandleUnitClick] ENTRY. Target: {(targetUnit ? targetUnit.GetUnitName() : "NULL")}, Current State: {currentState}, Selected Unit: {(selectedUnit ? selectedUnit.GetUnitName() : "NULL")}", LogCategory.Ability);

            switch (currentState)
            {
                case ActionState.Idle:
                     SmartLogger.Log($"[PAM.HandleUnitClick] In Idle State. Selecting unit: {targetUnit?.GetUnitName()}", LogCategory.Ability);
                    // This case should likely handle unit selection.
                    // The UnitSelectionController is set up to do this via InputManager.OnUnitSelected.
                    // We can add fallback logic here if needed, but the current input flow might be sufficient
                    // where UnitSelectionController handles the initial click to select a unit.
                     break; // End Idle case

                case ActionState.SelectingAbilityTarget:
                    SmartLogger.Log($"[PAM.HandleUnitClick] In SelectingAbilityTarget State. Caster: {(selectedUnit ? selectedUnit.GetUnitName() : "NULL")}, Ability: {(selectedAbility ? selectedAbility.displayName : "NULL")}");
                    if (selectedAbility == null || selectedUnit == null)
                    {
                        SmartLogger.LogWarning("[PAM.HandleUnitClick] Null ability or caster, cancelling.", LogCategory.Ability);
                        CancelAbilityTargeting();
                        return;
                    }
                    // --- Two-Target Ability Logic ---
                    bool requiresTwoTargets = selectedAbility != null && selectedAbility.abilityId == "KarmicTether";
                    if (requiresTwoTargets)
                    {
                        SmartLogger.Log($"[PAM.HandleUnitClick] Handling two-target ability '{selectedAbility.displayName}'. IsSelectingSecondTarget: {isSelectingSecondTarget}", LogCategory.Ability);
                        if (!isSelectingSecondTarget)
                        {
                            // First Target Selection
                            SmartLogger.Log("[PAM.HandleUnitClick] Selecting first target unit.", LogCategory.Ability);
                            bool isValidFirstTarget = IsValidAbilityTarget(targetUnit);
                            if (isValidFirstTarget)
                            {
                                firstTargetUnit = targetUnit;
                                isSelectingSecondTarget = true;
                                SmartLogger.Log($"[PAM.HandleUnitClick] First target unit selected: {firstTargetUnit.GetUnitName()}. Now selecting second target. Invoking OnFirstTargetSelected event.", LogCategory.Ability);

                                // Invoke the event, passing the selected first target unit
                                OnFirstTargetSelected?.Invoke(firstTargetUnit);
                            }
                            else
                            {
                                SmartLogger.LogWarning($"[PAM.HandleUnitClick] Invalid first target unit selected for '{selectedAbility.displayName}'.", LogCategory.Ability);
                                // UI feedback for invalid target
                            }
                        }
                        else
                        {
                            // Second Target Selection
                            SmartLogger.Log("[PAM.HandleUnitClick] Selecting second target unit.", LogCategory.Ability);
                            // --- LOGGING FOR KARMIC TETHER SECOND TARGET SELECTION IN HANDLEUNITCLICK ---
                            SmartLogger.Log($"[PAM.HandleUnitClick] Karmic Tether: ENTERING second target selection logic. Target Unit: {targetUnit?.GetUnitName() ?? "NULL"}, IsSelectingSecondTarget: {isSelectingSecondTarget}.", LogCategory.Ability, this);
                            // Before calling IsValidAbilityTarget(targetUnit)
                            SmartLogger.Log($"[PAM.HandleUnitClick] Karmic Tether: Calling IsValidAbilityTarget for second target {targetUnit?.GetUnitName() ?? "NULL"}.", LogCategory.Ability, this);
                            bool isValidSecondTarget = IsValidAbilityTarget(targetUnit);
                            SmartLogger.Log($"[PAM.HandleUnitClick] Karmic Tether: IsValidAbilityTarget returned {isValidSecondTarget} for second target {targetUnit?.GetUnitName() ?? "NULL"}.", LogCategory.Ability, this);
                            // Before the check 'if (isValidSecondTarget && targetUnit != firstTargetUnit)'
                            SmartLogger.Log($"[PAM.HandleUnitClick] Karmic Tether: Checking combined condition: isValidSecondTarget ({isValidSecondTarget}) && targetUnit != firstTargetUnit ({(targetUnit != firstTargetUnit)}).", LogCategory.Ability, this);
                            // The log before calling ExecuteAbilityCommand is already here from a previous step, keep it
                            // Inside the 'else' block after the 'if (isValidSecondTarget && targetUnit != firstTargetUnit)' (invalid second target)
                            // --- END LOGGING ---
                            if (isValidSecondTarget && targetUnit != firstTargetUnit)
                            {
                                SmartLogger.Log($"[PAM.HandleUnitClick] Second target unit selected: {targetUnit.GetUnitName()}. First target was: {firstTargetUnit.GetUnitName()}.", LogCategory.Ability);
                                if (selectedUnit == null || selectedAbility == null || firstTargetUnit == null)
                                {
                                    SmartLogger.LogError("[PAM.HandleUnitClick] Missing references for two-target ability execution. Cancelling.", LogCategory.Ability);
                                    CancelAbilityTargeting();
                                    return;
                                }
                                // --- LOGGING FOR KARMIC TETHER SECOND TARGET SELECTION ---
                                SmartLogger.Log($"[PAM.HandleUnitClick] Karmic Tether: Second target selected. First Target ID: {firstTargetUnit?.UnitId ?? -1}, Second Target ID: {targetUnit?.UnitId ?? -1}. Ability ID: {selectedAbility?.abilityId ?? "NULL"}.", LogCategory.Ability, this);
                                // --- END LOGGING ---
                                SmartLogger.Log($"[PAM.HandleUnitClick] Executing two-target ability '{selectedAbility.displayName}' from {selectedUnit.GetUnitName()} targeting {firstTargetUnit.GetUnitName()} and {targetUnit.GetUnitName()}.", LogCategory.Ability);
                                ExecuteAbilityCommand(
                                    selectedUnit.GetUnitId(),
                                    selectedAbilityIndex,
                                    firstTargetUnit.GetGridPosition().ToVector2Int(),
                                    null,
                                    targetUnit.UnitId
                                );
                                firstTargetUnit = null;
                                isSelectingSecondTarget = false;
                                SmartLogger.Log("[PAM.HandleUnitClick] AbilityCommand created and executed. State will be reset by callback.", LogCategory.Ability);
                            }
                            else
                            {
                                SmartLogger.LogWarning($"[PAM.HandleUnitClick] Invalid second target unit selected for '{selectedAbility.displayName}' (Valid type: {isValidSecondTarget}, Distinct from first: {targetUnit != firstTargetUnit}).", LogCategory.Ability);
                                // UI feedback for invalid second target
                            }
                        }
                        break;
                    }
                    // --- End Two-Target Ability Logic ---
                    // Existing single-target logic
                    bool isValid = IsValidAbilityTarget(targetUnit);
                    SmartLogger.Log($"[PAM.HandleUnitClick] Target validation result: {isValid}", LogCategory.Ability);
                    if (isValid)
                    {
                        var targetPos = targetUnit.GetGridPosition().ToVector2Int();
                        SmartLogger.Log($"[PAM.HandleUnitClick] Target is Valid. Creating AbilityCommand...", LogCategory.Ability);
                        var casterId = selectedUnit.GetUnitId();
                        var abilityName = selectedAbility.displayName;
                        var targetId = targetUnit.UnitId;
                        SmartLogger.Log($"[PAM.HandleUnitClick] Calling ExecuteAbilityCommand. CasterID: {casterId}, AbilityIndex: {selectedAbilityIndex}, TargetPos (from clicked unit): {targetPos}", LogCategory.Ability);
                        ExecuteAbilityCommand(selectedUnit.GetUnitId(), selectedAbilityIndex, targetPos);
                        SmartLogger.Log($"[PAM.HandleUnitClick] AbilityCommand created and executed. Caster: {casterId}, Ability: {abilityName}, Target: {targetId}", LogCategory.Ability);
                        SetState(ActionState.Idle);
                        selectedAbility = null;
                        selectedUnit = null;
                        SmartLogger.Log("[PAM.HandleUnitClick] State reset to Idle", LogCategory.Ability);
                    }
                    else
                    {
                        SmartLogger.LogWarning($"[PAM.HandleUnitClick] Target '{targetUnit?.GetUnitName()}' was invalid for ability '{selectedAbility?.displayName}'.", LogCategory.Ability);
                        // UI feedback for invalid target
                    }
                    break;

                case ActionState.SelectingZoneDestination:
                    // --- Terrain Shift Special Handling (Clicking unit cancels destination selection) ---
                    SmartLogger.Log($"[PAM.HandleUnitClick] In SelectingZoneDestination state. Clicking on a unit '{targetUnit?.GetUnitName()}' cancels zone destination selection.", LogCategory.Ability, this);
                    CancelAbilityTargeting(); // Cancel the two-click targeting process
                    break; // End SelectingZoneDestination case
            }
        }

        /// <summary>
        /// Handle a click on the ground
        /// </summary>
        public void HandleGroundClick(Vector2Int gridPosition)
        {
            // Use SmartLogger consistently
            SmartLogger.Log($"[PAM.HandleGroundClick] ENTRY. TargetPos: {gridPosition}, Current State: {currentState}", LogCategory.Ability, this);

            GridPosition clickedGridPos = GridPosition.FromVector2Int(gridPosition); // Convert Vector2Int to GridPosition

            switch (currentState)
            {
                case ActionState.Idle:
                    SmartLogger.Log("[PAM.HandleGroundClick] In Idle State. Attempting to issue Move Command.", LogCategory.Input, this); // Use SmartLogger
                    var currentSelectedUnit = unitManager.GetSelectedUnit();
                    if (currentSelectedUnit == null)
                    {
                        SmartLogger.Log("[PAM.HandleGroundClick] No unit selected, cannot move.", LogCategory.Input, this); // Use SmartLogger
                        return;
                    }

                    bool canMove = turnSystem.CanUnitMove(currentSelectedUnit);
                    SmartLogger.Log($"[PAM.HandleGroundClick] Can unit {currentSelectedUnit.GetUnitName()} move? {canMove}", LogCategory.Input, this); // Use SmartLogger

                    if (!canMove)
                    {
                        SmartLogger.Log($"[PAM.HandleGroundClick] Unit {currentSelectedUnit.GetUnitId()} cannot move at this time, stopping.", LogCategory.Input, this); // Use SmartLogger
                        return;
                    }

                    SmartLogger.Log($"[PAM.HandleGroundClick] Issuing MoveCommand for Unit {currentSelectedUnit.GetUnitId()} to {gridPosition}", LogCategory.Input, this); // Use SmartLogger
                    ExecuteMoveCommand(currentSelectedUnit.GetUnitId(), gridPosition);
                    break; // End Idle case

                case ActionState.SelectingAbilityTarget:
                    SmartLogger.Log($"[PAM.HandleGroundClick] In Targeting State. Caster: {(selectedUnit ? selectedUnit.GetUnitName() : "NULL")}, Ability: {(selectedAbility ? selectedAbility.displayName : "NULL")}", LogCategory.Ability, this);
                    if (selectedAbility == null || selectedUnit == null)
                    {
                        SmartLogger.LogWarning("[PAM.HandleGroundClick] Null ability or caster, cancelling targeting.", LogCategory.Ability, this);
                        CancelAbilityTargeting();
                        return;
                    }
                    // Cancel two-target selection if ground is clicked during second target selection for Karmic Tether
                    if (selectedAbility != null && selectedAbility.abilityId == "KarmicTether" && isSelectingSecondTarget)
                    {
                        SmartLogger.Log("[PAM.HandleGroundClick] Ground click during second target selection for Karmic Tether. Cancelling targeting.", LogCategory.Ability);
                        CancelAbilityTargeting();
                        return;
                    }
                    // --- Terrain Shift Two-Click Logic ---
                    // First Click: Select the zone
                    if (selectedAbility.abilityId == "TerrainShift")
                    {
                        if (!isTerrainShiftSelected)
                        {
                            SmartLogger.Log($"[PAM.HandleGroundClick] Terrain Shift first click: Checking for zone at {clickedGridPos}", LogCategory.Ability, this);
                            var zoneToShift = ZoneManager.Instance?.FindActiveZoneInstanceAtPosition(clickedGridPos);

                            if (zoneToShift != null)
                            {
                                // Store the selected zone instance
                                selectedZoneToShift = zoneToShift;
                                isTerrainShiftSelected = true;
                                // Log and trigger UI event for destination selection
                                string zoneDisplayName = (zoneToShift is ZoneInstance concreteZone) ? concreteZone.DisplayName : "Unknown Zone";
                                SmartLogger.Log($"[PAM.HandleGroundClick] Terrain Shift: Zone '{zoneDisplayName}' selected at its center {zoneToShift.Position} (clicked inside its area at {clickedGridPos}). Waiting for second click.", LogCategory.Ability, this);
                                // --- Trigger event for UI to show valid destinations ---
                                OnZoneDestinationSelectionStarted?.Invoke(selectedZoneToShift);
                                // --- END event trigger ---
                                return; // Wait for second click
                            }
                            else
                            {
                                SmartLogger.LogWarning($"[PAM.HandleGroundClick] Terrain Shift: No active shiftable zone found containing the clicked position {clickedGridPos}. Cancelling targeting.", LogCategory.Ability, this);
                                CancelAbilityTargeting();
                                return;
                            }
                        }
                        else // Second Click: Select the destination for the zone
                        {
                            SmartLogger.Log($"[PAM.HandleGroundClick] Terrain Shift second click: Selecting destination at {clickedGridPos}", LogCategory.Ability, this);

                            // --- Destination Validation ---
                            if (selectedZoneToShift == null)
                            {
                                SmartLogger.LogError("[PAM.HandleGroundClick] Terrain Shift: selectedZoneToShift is null on second click! This should not happen. Cancelling.", LogCategory.Ability, this);
                                CancelAbilityTargeting();
                                return;
                            }
                            if (GridManager.Instance == null || !GridManager.Instance.IsValidGridPosition(clickedGridPos))
                            {
                                SmartLogger.LogWarning($"[PAM.HandleGroundClick] Terrain Shift: Invalid destination position {clickedGridPos} (out of bounds OR GridManager.Instance is null). Cannot shift zone.", LogCategory.Ability, this);
                                // Stay in targeting state, allow player to try again
                                return;
                            }
                            if (ZoneManager.Instance != null && ZoneManager.Instance.IsVoidSpace(clickedGridPos))
                            {
                                SmartLogger.LogWarning($"[PAM.HandleGroundClick] Terrain Shift: Destination position {clickedGridPos} is a void space. Cannot shift zone.", LogCategory.Ability, this);
                                // Stay in targeting state, allow player to try again
                                return;
                            }
                            if (GridManager.Instance.IsTileOccupied(clickedGridPos))
                            {
                                SmartLogger.LogWarning($"[PAM.HandleGroundClick] Terrain Shift: Destination position {clickedGridPos} is occupied. Cannot shift zone.", LogCategory.Ability, this);
                                // Stay in targeting state, allow player to try again
                                return;
                            }
                            GridPosition zoneOriginalPos = selectedZoneToShift.Position;
                            int shiftDistance = GridPosition.GetManhattanDistance(zoneOriginalPos, clickedGridPos);
                            const int requiredShiftDistance = 2;
                            if (shiftDistance != requiredShiftDistance)
                            {
                                SmartLogger.LogWarning($"[PAM.HandleGroundClick] Terrain Shift: Shift distance from zone {zoneOriginalPos} to destination {clickedGridPos} is {shiftDistance}, but required is {requiredShiftDistance}. Cannot shift zone.", LogCategory.Ability, this);
                                // Stay in targeting state, allow player to try again
                                return;
                            }
                            // All validation passed: execute the command
                            string zoneDisplayName = (selectedZoneToShift is ZoneInstance concreteZone) ? concreteZone.DisplayName : "Unknown Zone";
                            SmartLogger.Log($"[PAM.HandleGroundClick] Terrain Shift: Valid destination {clickedGridPos} selected for zone '{zoneDisplayName}' (ID: {selectedZoneToShift.Id}). Creating AbilityCommand.", LogCategory.Ability, this);
                            ExecuteAbilityCommand(selectedUnit.GetUnitId(), selectedAbilityIndex, clickedGridPos.ToVector2Int(), selectedZoneToShift.Id);
                            // UI and state reset will be handled by ExecuteCommand callback
                            return;
                        }
                    }
                    // --- END Terrain Shift Two-Click Logic ---

                    // --- Existing logic for other ground-targeted abilities ---
                    // This block is only reached if currentState is SelectingAbilityTarget AND abilityId is NOT TerrainShift
                    // No changes needed here based on the prompt; original logic is preserved.

                    // Check if ability can target ground
                    if (!selectedAbility.targetsGround)
                    {
                        SmartLogger.LogWarning($"[PAM.HandleGroundClick] Ability {selectedAbility.displayName} cannot target ground.", LogCategory.Ability, this); // Use SmartLogger
                        // It might be better to CancelAbilityTargeting() here if the click was invalid
                        // CancelAbilityTargeting();
                        return;
                    }

                    // Check range
                    var unitPos = selectedUnit.GetGridPosition();
                    int distance = GridPosition.GetManhattanDistance(unitPos, GridPosition.FromVector2Int(gridPosition)); // Use original Vector2Int gridPosition for distance check if needed
                    SmartLogger.Log($"[PAM.HandleGroundClick] Distance to target: {distance}, Ability range: {selectedAbility.range}", LogCategory.Ability, this); // Use SmartLogger
                    if (distance > selectedAbility.range)
                    {
                        SmartLogger.LogWarning($"[PAM.HandleGroundClick] Target position out of range ({distance} > {selectedAbility.range}) for ability {selectedAbility.displayName}. Aborting.", LogCategory.Ability, this); // Use SmartLogger
                        // It might be better to CancelAbilityTargeting() here if the click was invalid
                        // CancelAbilityTargeting();
                        return;
                    }

                    // Execute the ability command (for non-Terrain Shift ground abilities)
                    SmartLogger.Log($"[PAM.HandleGroundClick] Executing ground-targeted ability {selectedAbility.displayName}", LogCategory.Ability, this); // Use SmartLogger
                    ExecuteAbilityCommand(selectedUnit.GetUnitId(), selectedAbilityIndex, gridPosition);

                    // After executing a non-Terrain Shift ability, the state should reset to Idle.
                    // The ExecuteCommand method handles resetting state via CancelAbilityTargeting on success/failure callback.
                    // So we don't need to explicitly set state to Idle here. Let ExecuteCommand handle it.
                    // SetState(ActionState.Idle); // Removed - Let ExecuteCommand handle state reset via callbacks
                    // selectedAbility = null; // Removed
                    // selectedUnit = null; // Removed
                    // selectedZoneToShift = null; // Removed
                    SmartLogger.Log("[PAM.HandleGroundClick] Handed off execution for ground ability. State will be reset by ExecuteCommand.", LogCategory.Ability, this); // Use SmartLogger

                    break; // End SelectingAbilityTarget case

                case ActionState.SelectingZoneDestination:
                    // --- Existing logic for Terrain Shift's *original* second click (Selecting Destination) ---
                    // This case is now potentially redundant or needs rethinking if the new two-click logic
                    // in SelectingAbilityTarget case handles everything.
                    // For now, keep it as is, but be aware it might not be reached correctly
                    // if the new logic always returns before changing state to SelectingZoneDestination.
                    // **Correction**: The original logic *did* change state to SelectingZoneDestination.
                    // The new logic should *replace* that original Terrain Shift handling, not duplicate it.
                    // Let's comment out the original Terrain Shift logic that led to this state,
                    // and potentially remove this state case if the new logic handles both clicks within SelectingAbilityTarget.

                    // Check if selectedZoneToShift is null or not a ZoneInstance before proceeding
                    if (!(selectedZoneToShift is Dokkaebi.Zones.ZoneInstance concreteZoneToShift))
                    {
                         SmartLogger.LogError($"[PAM.HandleGroundClick] Stored selectedZoneToShift is null or not a ZoneInstance! Type: {selectedZoneToShift?.GetType().Name ?? "NULL"}. Cancelling destination selection.", LogCategory.Ability, this);
                         CancelAbilityTargeting(); // Cancel if the stored zone is invalid
                         return;
                    }

                    SmartLogger.Log($"[PAM.HandleGroundClick] In SelectingZoneDestination state. Selected zone: {concreteZoneToShift.DisplayName}, Clicked position: {clickedGridPos}", LogCategory.Ability, this);


                    // Validate the clicked position as a valid destination
                    int maxShiftDistance = 2; // Terrain Shift moves 2 tiles
                    int distanceToSelectedZone = GridPosition.GetManhattanDistance(concreteZoneToShift.GetGridPosition(), clickedGridPos);

                    if (distanceToSelectedZone > maxShiftDistance)
                    {
                        SmartLogger.LogWarning($"[PAM.HandleGroundClick] Destination position {clickedGridPos} is too far from zone's current position {concreteZoneToShift.GetGridPosition()}. Max shift distance is {maxShiftDistance}. Destination selection failed.", LogCategory.Ability, this);
                        // Provide user feedback that the destination is out of range.
                        return; // Stay in SelectingZoneDestination state, wait for valid click
                    }

                     if (GridManager.Instance == null || !GridManager.Instance.IsValidGridPosition(clickedGridPos))
                     {
                         SmartLogger.LogWarning($"[PAM.HandleGroundClick] Destination position {clickedGridPos} is outside grid bounds OR GridManager.Instance is null. Destination selection failed.", LogCategory.Ability, this);
                         return; // Stay in SelectingZoneDestination state
                     }

                     if (ZoneManager.Instance != null && ZoneManager.Instance.IsVoidSpace(clickedGridPos))
                    {
                         SmartLogger.LogWarning($"[PAM.HandleGroundClick] Destination position {clickedGridPos} is a void space. Destination selection failed.", LogCategory.Ability, this);
                         return; // Stay in SelectingZoneDestination state, wait for valid click
                    }

                    // If destination is valid, execute the zone shift command
                    SmartLogger.Log($"[PAM.HandleGroundClick] Valid destination {clickedGridPos} selected for zone {concreteZoneToShift.DisplayName}. Executing zone shift.", LogCategory.Ability, this);

                    // Placeholder for direct execution (replace with command system later)
                    if (enableLocalExecution && ZoneManager.Instance != null)
                    {
                        bool shiftSuccess = ZoneManager.Instance.ShiftZone(concreteZoneToShift, clickedGridPos);
                         if (shiftSuccess)
                        {
                             SmartLogger.Log($"[PAM.HandleGroundClick] ZoneManager.ShiftZone successful for zone '{concreteZoneToShift.DisplayName}' to {clickedGridPos}.", LogCategory.Ability, this);
                             // Ability succeeded, return to Idle
                             SetState(ActionState.Idle);
                             selectedAbility = null;
                             selectedUnit = null;
                             selectedZoneToShift = null;
                        }
                        else
                        {
                             SmartLogger.LogWarning($"[PAM.HandleGroundClick] ZoneManager.ShiftZone failed for zone '{concreteZoneToShift.DisplayName}' to {clickedGridPos}.", LogCategory.Ability, this);
                             // Ability failed, return to Idle and clear state
                             CancelAbilityTargeting();
                        }
                    } else {
                         SmartLogger.LogWarning($"[PAM.HandleGroundClick] Local execution disabled or ZoneManager missing. Cannot execute shift locally.", LogCategory.Ability, this);
                         // If not executing locally, assume a command would be sent. Need command system integration.
                         // For now, just cancel targeting as we can't proceed.
                         CancelAbilityTargeting();
                    }
                    break; // End SelectingZoneDestination case
            }
        }

        /// <summary>
        /// Execute a move command
        /// </summary>
        public void ExecuteMoveCommand(int unitId, Vector2Int targetPosition)
        {
            // Create the command
            var command = new MoveCommand(unitId, targetPosition);
            
            // Execute the command through our generic handler
            ExecuteCommand(command);
        }

        /// <summary>
        /// Execute an ability command
        /// </summary>
        public void ExecuteAbilityCommand(int unitId, int abilityIndex, Vector2Int targetPosition, int? targetZoneId = null, int? secondTargetUnitId = null)
        {
            // Create the command, passing the optional targetZoneId and secondTargetUnitId
            var command = new AbilityCommand(unitId, abilityIndex, targetPosition, targetZoneId, secondTargetUnitId);
            
            // Execute the command through our generic handler
            ExecuteCommand(command);
        }

        /// <summary>
        /// Check if a unit is a valid target for the currently selected ability
        /// </summary>
        private bool IsValidAbilityTarget(DokkaebiUnit targetUnit)
        {
            SmartLogger.Log($"[PAM.IsValidAbilityTarget] START validation for target: {targetUnit?.GetUnitName() ?? "NULL"} (ID: {targetUnit?.UnitId}) for ability: {(selectedAbility?.displayName ?? "NULL")}", LogCategory.Ability, this);
            if (selectedAbility == null || selectedUnit == null || targetUnit == null)
            {
                SmartLogger.LogWarning($"[PAM.IsValidAbilityTarget] FAILED: Null check failed. Ability: {selectedAbility != null}, Caster: {selectedUnit != null}, Target: {targetUnit != null}", LogCategory.Ability);
                return false;
            }
            // After checking for null references at the start
            SmartLogger.Log($"[PAM.IsValidAbilityTarget] Initial null checks passed. Target: {targetUnit.GetUnitName()}, Caster: {selectedUnit.GetUnitName()}, Ability: {selectedAbility.displayName}.", LogCategory.Ability, this);

            var targetPos = targetUnit.GetGridPosition();
            int distance = GridPosition.GetManhattanDistance(selectedUnit.GetGridPosition(), targetPos);
            int effectiveRange = AbilityManager.Instance != null
                ? AbilityManager.Instance.GetEffectiveRange(selectedAbility, selectedUnit)
                : selectedAbility.range;
            SmartLogger.Log($"[PAM.IsValidAbilityTarget] Range check: Distance {distance} vs Effective Range {effectiveRange}.", LogCategory.Ability, this);
            bool isInRange = distance <= effectiveRange;
            if (!isInRange)
            {
                SmartLogger.LogWarning($"[PAM.IsValidAbilityTarget] FAILED: Target unit at position {targetPos} is out of effective range (Distance: {distance} > Effective Range: {effectiveRange})", LogCategory.Ability, this);
                return false;
            }
            // Before the 'if (selectedAbility.targetsGround)' check
            SmartLogger.Log($"[PAM.IsValidAbilityTarget] Checking targetsGround flag ({selectedAbility.targetsGround}).", LogCategory.Ability, this);
            if (selectedAbility.targetsGround)
            {
                SmartLogger.Log($"[PAM.IsValidAbilityTarget] Ground-targeting ability check - Target is in range. PASSED.", LogCategory.Ability);
                SmartLogger.Log($"[PAM.IsValidAbilityTarget] Validation complete. Final result: true.", LogCategory.Ability, this);
                return true;
            }
            // Before the 'if (targetUnit == null)' check for non-ground abilities
            if (!selectedAbility.targetsGround)
            {
                SmartLogger.Log($"[PAM.IsValidAbilityTarget] Not a ground-targeting ability. Checking if targetUnit is null ({targetUnit == null}).", LogCategory.Ability, this);
            }
            if (targetUnit == null)
            {
                SmartLogger.LogWarning($"[PAM.IsValidAbilityTarget] FAILED: Target Unit is null for non-ground ability.", LogCategory.Ability, this);
                return false;
            }
            // Before the 'if (!targetUnit.IsAlive)' check
            SmartLogger.Log($"[PAM.IsValidAbilityTarget] Target unit is not null. Checking if alive ({targetUnit.IsAlive}).", LogCategory.Ability, this);
            if (!targetUnit.IsAlive)
            {
                SmartLogger.LogWarning($"[PAM.IsValidAbilityTarget] FAILED: Target unit is not alive", LogCategory.Ability, this);
                return false;
            }
            // Before calculating canTargetSelf, canTargetAlly, canTargetEnemy
            SmartLogger.Log($"[PAM.IsValidAbilityTarget] Target unit is alive. Calculating targeting flags...", LogCategory.Ability, this);
            bool canTargetSelf = selectedAbility.targetsSelf && targetUnit.UnitId == selectedUnit.UnitId;
            bool canTargetAlly = selectedAbility.targetsAlly && targetUnit.TeamId == selectedUnit.TeamId && targetUnit.UnitId != selectedUnit.UnitId;
            bool canTargetEnemy = selectedAbility.targetsEnemy && targetUnit.IsPlayerControlled != selectedUnit.IsPlayerControlled;
            // Before the 'bool unitTypeIsValid = canTargetSelf || canTargetAlly || canTargetEnemy;' check
            SmartLogger.Log($"[PAM.IsValidAbilityTarget] Targeting flag calculations complete. Checking if unit type is valid (Self:{canTargetSelf} || Ally:{canTargetAlly} || Enemy:{canTargetEnemy}).", LogCategory.Ability, this);
            bool unitTypeIsValid = canTargetSelf || canTargetAlly || canTargetEnemy;
            if (!unitTypeIsValid)
            {
                SmartLogger.LogWarning($"[PAM.IsValidAbilityTarget] FAILED: Invalid target type. None of the targeting conditions were met (Self: {canTargetSelf}, Ally: {canTargetAlly}, Enemy: {canTargetEnemy})", LogCategory.Ability, this);
                SmartLogger.Log($"[PAM.IsValidAbilityTarget] Validation complete. Final result: false.", LogCategory.Ability, this);
                return false;
            }
            SmartLogger.Log($"[PAM.IsValidAbilityTarget] PASSED: Target '{targetUnit?.GetUnitName()}' is valid for ability '{selectedAbility?.displayName}'", LogCategory.Ability, this);
            SmartLogger.Log($"[PAM.IsValidAbilityTarget] Validation complete. Final result: true.", LogCategory.Ability, this);
            return true;
        }

        /// <summary>
        /// Execute an end turn command
        /// </summary>
        public void ExecuteEndTurnCommand()
        {
            // Create the command
            var command = new EndTurnCommand();
            
            // Execute the command through our generic handler
            ExecuteCommand(command);
        }

        /// <summary>
        /// Execute a tactical reposition command
        /// </summary>
        public void ExecuteRepositionCommand(int unitId, Vector2Int targetPosition)
        {
            // Create the command
            var command = new RepositionCommand(unitId, targetPosition);
            
            // Execute the command through our generic handler
            ExecuteCommand(command);
        }

        /// <summary>
        /// Execute a command through the command system
        /// </summary>
        private void ExecuteCommand(ICommand command)
        {
            SmartLogger.Log($"[PAM.ExecuteCommand] ENTRY with command type: {command?.CommandType ?? "NULL"}", LogCategory.Ability);
            if (command == null)
            {
                DebugLog("Cannot execute null command");
                SmartLogger.LogWarning("[PAM.ExecuteCommand] Command was NULL, aborting execution", LogCategory.Ability);
                OnCommandResult?.Invoke(false, "Invalid command");
                return;
            }
            DebugLog($"Executing command: {command.CommandType}");
            SmartLogger.Log($"[PAM.ExecuteCommand] About to check local validation (useLocalValidationFirst: {useLocalValidationFirst})", LogCategory.Ability);
            // Local validation if enabled
            if (useLocalValidationFirst)
            {
                SmartLogger.Log($"[PAM.ExecuteCommand] Starting local validation for command: {command.CommandType}", LogCategory.Ability);
                bool isValid = command.Validate();
                SmartLogger.Log($"[PAM.ExecuteCommand] Local validation result: {isValid} for command: {command.CommandType}", LogCategory.Ability);
                if (!isValid)
                {
                    DebugLog($"Command validation failed: {command.CommandType}");
                    SmartLogger.LogWarning($"[PAM.ExecuteCommand] Validation FAILED for command: {command.CommandType}", LogCategory.Ability);
                    OnCommandResult?.Invoke(false, "Command validation failed");
                    // We should cancel targeting state here if the validation failed while targeting an ability
                    if (currentState == ActionState.SelectingAbilityTarget || currentState == ActionState.SelectingZoneDestination) // Include new state
                    {
                        CancelAbilityTargeting();
                    }
                    return;
                }
            }
            SmartLogger.Log($"[PAM.ExecuteCommand] About to check local execution (enableLocalExecution: {enableLocalExecution})", LogCategory.Ability);

            // --- MODIFIED LOGIC START ---
            if (enableLocalExecution)
            {
                SmartLogger.Log($"[PAM.ExecuteCommand] About to execute command locally: {command.CommandType}", LogCategory.Ability);
                command.Execute();
                SmartLogger.Log($"[PAM.ExecuteCommand] Local execution completed for command: {command.CommandType}", LogCategory.Ability);
                DebugLog($"Command executed locally: {command.CommandType}");
                // For local execution, we are done. Report success and trigger UI/state updates.
                OnCommandResult?.Invoke(true, "Command executed successfully (local mode)");
                // If we were in a targeting state, cancel it after successful local execution
                if (currentState == ActionState.SelectingAbilityTarget || currentState == ActionState.SelectingZoneDestination) // Include new state
                {
                    // Use SmartLogger with stack trace for clarity
                    SmartLogger.LogWarning($"[PAM ExecuteCommand LOCAL Success] Command '{command?.CommandType ?? "NULL"}' completed locally. State '{currentState}' requires reset via CancelAbilityTargeting. Stack trace:\n{System.Environment.StackTrace}", LogCategory.Ability);
                    CancelAbilityTargeting();
                }
            }
            else if (networkManager != null)
            {
                // Only attempt network execution if local execution is NOT enabled AND networkManager exists
                SmartLogger.Log($"[PAM.ExecuteCommand] Sending command to NetworkManager: {command.CommandType}", LogCategory.Ability);
                DebugLog($"Sending command to network: {command.CommandType}");
                networkManager.ExecuteCommand(
                    command.CommandType,
                    command.Serialize(),
                    result =>
                    {
                        SmartLogger.Log($"[PAM.ExecuteCommand] Network execution SUCCESS for command: {command.CommandType}", LogCategory.Ability);
                        DebugLog($"Network execution succeeded: {command.CommandType}");
                        OnCommandResult?.Invoke(true, "Command executed successfully");
                        // If we were in a targeting state, cancel it after successful network execution
                        if (currentState == ActionState.SelectingAbilityTarget || currentState == ActionState.SelectingZoneDestination) // Include new state
                        {
                            // Use SmartLogger with stack trace for clarity
                            SmartLogger.LogWarning($"[PAM ExecuteCommand NETWORK Success] Command '{command?.CommandType ?? "NULL"}' completed via network. State '{currentState}' requires reset via CancelAbilityTargeting. Stack trace:\n{System.Environment.StackTrace}", LogCategory.Ability);
                            CancelAbilityTargeting();
                        }
                    },
                    error =>
                    {
                        SmartLogger.LogError($"[PAM.ExecuteCommand] Network execution FAILED for command: {command.CommandType}. Error: {error}", LogCategory.Ability);
                        DebugLog($"Network execution failed: {command.CommandType} - {error}");
                        OnCommandResult?.Invoke(false, error);
                        // Always reset targeting state on failure
                        if (currentState == ActionState.SelectingAbilityTarget || currentState == ActionState.SelectingZoneDestination) // Include new state
                        {
                            // Use SmartLogger with stack trace for clarity
                            SmartLogger.LogWarning($"[PAM ExecuteCommand NETWORK Error] Command '{command?.CommandType ?? "NULL"}' failed via network. State '{currentState}' requires reset via CancelAbilityTargeting. Stack trace:\n{System.Environment.StackTrace}", LogCategory.Ability);
                            CancelAbilityTargeting();
                        }
                    }
                );
            }
            else
            {
                // This case should ideally not be reached if setup is correct for either local or network play
                SmartLogger.LogError($"[PAM.ExecuteCommand] No execution path available for command: {command.CommandType}. enableLocalExecution={enableLocalExecution}, networkManager={networkManager != null}", LogCategory.Ability);
                DebugLog("No execution path available.");
                OnCommandResult?.Invoke(false, "No execution path available");
            }
            // --- MODIFIED LOGIC END ---
        }

        /// <summary>
        /// Get the currently selected unit
        /// </summary>
        public DokkaebiUnit GetSelectedUnit()
        {
            return selectedUnit;
        }

        /// <summary>
        /// Set the currently selected unit
        /// </summary>
        public void SetSelectedUnit(DokkaebiUnit unit)
        {
            selectedUnit = unit;
            DebugLog($"Selected unit: {(unit != null ? unit.GetUnitName() : "None")}");
        }

        /// <summary>
        /// Clear the currently selected unit
        /// </summary>
        public void ClearSelectedUnit()
        {
            selectedUnit = null;
            DebugLog("Cleared selected unit");
        }

        private void DebugLog(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerActionManager] {message}");
            }
        }

        public ActionState GetCurrentActionState() => currentState;

        #endregion

        // Temporary public getter for UI debugging
        // public IZoneInstance selectedZoneToShiftPublic => selectedZoneToShift; // Remove this property
        public DokkaebiUnit FirstTargetUnit => firstTargetUnit; // Public property to access the first target
    }
} 
