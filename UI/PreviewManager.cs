using UnityEngine;
using System.Collections.Generic;
using Dokkaebi.Units;
using Dokkaebi.Core.Data;
using Dokkaebi.Grid;
using Dokkaebi.Pathfinding;
using Dokkaebi.Core;
using Dokkaebi.Interfaces;
using Dokkaebi.Common;
using Dokkaebi.Utilities;
using Dokkaebi.Zones;
using System.Text;
using System.Linq;

namespace Dokkaebi.UI
{
    public class PreviewManager : MonoBehaviour
    {
        public static PreviewManager Instance { get; private set; }

        [Header("Movement Preview")]
        [SerializeField] private LineRenderer movementLine;
        [SerializeField] private Material movementLineMaterial;
        [SerializeField] private float lineWidth = 0.1f;
        [SerializeField] private Color validPathColor = Color.green;
        [SerializeField] private Color invalidPathColor = Color.red;

        [Header("Ability Preview")]
        [SerializeField] private GameObject tileHighlightPrefab;
        [SerializeField] private Material validTargetMaterial;
        [SerializeField] private Material invalidTargetMaterial;
        [SerializeField] private float highlightHeight = 0.1f;

        private Dictionary<Vector2Int, GameObject> activeHighlights = new Dictionary<Vector2Int, GameObject>();
        private DokkaebiUnit selectedUnit;
        private AbilityData selectedAbility;
        private bool isAbilityTargetingMode = false;
        private PlayerActionManager playerActionManager;
        private UnitManager unitManager;
        private AbilityManager abilityManager;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                SmartLogger.Log($"[PreviewManager.Awake] Singleton Instance set to {gameObject.name} (Instance ID: {GetInstanceID()}).", LogCategory.UI, this);
            }
            else
            {
                SmartLogger.LogWarning($"[PreviewManager.Awake] Multiple PreviewManager instances detected. Destroying duplicate {gameObject.name} (Instance ID: {GetInstanceID()}). Existing instance is {Instance.gameObject.name} (Instance ID: {Instance.GetInstanceID()}).", LogCategory.UI, this);
                Destroy(gameObject);
                return;
            }

            playerActionManager = PlayerActionManager.Instance;
            unitManager = UnitManager.Instance;
            abilityManager = FindObjectOfType<AbilityManager>();
            if (playerActionManager == null)
            {
                SmartLogger.LogError("PlayerActionManager not found in scene!", LogCategory.UI);
                return;
            }
            if (unitManager == null)
            {
                SmartLogger.LogError("UnitManager not found in scene!", LogCategory.UI);
                return;
            }
        }

        private void OnEnable()
        {
            // Subscribe to input events
            if (playerActionManager != null)
            {
                playerActionManager.OnCommandResult += HandleCommandResult;
                playerActionManager.OnAbilityTargetingStarted += HandleAbilityTargetingStarted;
                playerActionManager.OnAbilityTargetingCancelled += HandleAbilityTargetingCancelled;
            }
        }

        private void OnDisable()
        {
            // Unsubscribe from events
            if (playerActionManager != null)
            {
                playerActionManager.OnCommandResult -= HandleCommandResult;
                playerActionManager.OnAbilityTargetingStarted -= HandleAbilityTargetingStarted;
                playerActionManager.OnAbilityTargetingCancelled -= HandleAbilityTargetingCancelled;
            }

            // Clean up
            ClearHighlights();
            if (movementLine != null)
            {
                movementLine.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            if (movementLine == null)
            {
                movementLine = gameObject.AddComponent<LineRenderer>();
                movementLine.material = movementLineMaterial;
                movementLine.startWidth = lineWidth;
                movementLine.endWidth = lineWidth;
                movementLine.useWorldSpace = true;
            }
        }

        private void HandleCommandResult(bool success, string message)
        {
            SmartLogger.Log($"[PreviewManager.HandleCommandResult] ENTRY. Command Result: Success={success}, Message='{message}'. isAbilityTargetingMode: {isAbilityTargetingMode}", LogCategory.UI, this);

            // Clear previews on command completion
            if (isAbilityTargetingMode)
            {
                SmartLogger.Log("[PreviewManager.HandleCommandResult] isAbilityTargetingMode is true. Clearing highlights.", LogCategory.UI, this);
                ClearHighlights();
                SmartLogger.Log("[PreviewManager.HandleCommandResult] Called ClearHighlights.", LogCategory.UI, this);
            }
            else
            {
                SmartLogger.Log("[PreviewManager.HandleCommandResult] Not in ability targeting mode. Hiding movement line.", LogCategory.UI, this);
                if (movementLine != null)
                {
                    movementLine.enabled = false;
                }
            }
            SmartLogger.Log("[PreviewManager.HandleCommandResult] EXIT.", LogCategory.UI, this);
        }

        private void HandleAbilityTargetingStarted(AbilityData ability)
        {
            selectedAbility = ability;
            selectedUnit = unitManager.GetSelectedUnit();
            isAbilityTargetingMode = true;

            // Clear any existing previews
            ClearHighlights();
            if (movementLine != null)
            {
                movementLine.enabled = false;
            }
        }

        private void HandleAbilityTargetingCancelled()
        {
            selectedAbility = null;
            isAbilityTargetingMode = false;
            ClearHighlights();
        }

        public void UpdatePreview(Vector2Int hoverPosition)
        {
//            SmartLogger.Log($"[PreviewManager.UpdatePreview] ENTRY. Hover Pos: {hoverPosition}. isAbilityTargetingMode: {isAbilityTargetingMode}, Selected Ability: {(selectedAbility != null ? selectedAbility.displayName : "NULL")}", LogCategory.UI, this); // NOTE: This method is called every frame via InputManager.Update. Avoid logging here unless throttled.
            if (isAbilityTargetingMode)
            {
                UpdateAbilityPreview(hoverPosition);
            }
            else
            {
                UpdateMovementPreview(hoverPosition);
            }
        }

        private void UpdateMovementPreview(Vector2Int targetPos)
        {
            selectedUnit = unitManager.GetSelectedUnit();
            if (selectedUnit == null || !movementLine) return;

            // Convert Vector2Int to GridPosition
            GridPosition startPos = selectedUnit.GetGridPosition();
            GridPosition endPos = GridPosition.FromVector2Int(targetPos);

            // Get path from unit to target using GridManager
            var path = GridManager.Instance.FindPath(
                startPos,
                endPos,
                selectedUnit.GetMovementRange()
            );

            // Update line renderer
            if (path != null && path.Count > 0)
            {
                movementLine.positionCount = path.Count;
                for (int i = 0; i < path.Count; i++)
                {
                    Vector3 worldPos = GridManager.Instance.GridToWorldPosition(path[i]);
                    movementLine.SetPosition(i, worldPos + Vector3.up * highlightHeight);
                }

                // Set color based on path validity
                bool isValidPath = path.Count <= selectedUnit.GetMovementRange() + 1;
                Color pathColor = isValidPath ? validPathColor : invalidPathColor;
                movementLine.startColor = pathColor;
                movementLine.endColor = pathColor;
                movementLine.enabled = true;
            }
            else
            {
                movementLine.enabled = false;
            }
        }

        private void UpdateAbilityPreview(Vector2Int targetPos)
        {
            SmartLogger.Log($"[PreviewManager.UpdateAbilityPreview] ENTRY. Target Pos: {targetPos}. isAbilityTargetingMode: {isAbilityTargetingMode}, Selected Ability: {(selectedAbility != null ? selectedAbility.displayName : "NULL")}", LogCategory.UI, this);
            selectedUnit = unitManager.GetSelectedUnit();
            if (selectedUnit == null || selectedAbility == null || GridManager.Instance == null)
            {
                SmartLogger.LogWarning("[PreviewManager.UpdateAbilityPreview] Missing dependencies (selectedUnit, selectedAbility, or GridManager). Aborting preview update.", LogCategory.UI, this);
                return;
            }
            // Calculate the effective range (this call includes the Wind Zone bonus for Gale Arrow)
            int effectiveRange = abilityManager != null
                ? abilityManager.GetEffectiveRange(selectedAbility, selectedUnit)
                : selectedAbility.range; // Fallback to base range if AbilityManager instance is null
            SmartLogger.Log($"[PreviewManager.UpdateAbilityPreview] Calculated Effective Range: {effectiveRange} (Base Range: {selectedAbility.range}, AoE: {selectedAbility.areaOfEffect})", LogCategory.UI, this);

            List<Vector2Int> targetableTiles = new List<Vector2Int>();

            // --- Logic for Single-Target Abilities (AoE == 0) ---
            if (selectedAbility.areaOfEffect == 0)
            {
                SmartLogger.Log($"[PreviewManager.UpdateAbilityPreview] Ability '{selectedAbility.displayName}' is single-target (AoE 0). Generating Manhattan distance tiles for effective range {effectiveRange}.", LogCategory.UI, this);
                GridPosition sourcePos = selectedUnit.GetGridPosition();

                // Generate tiles within Manhattan distance (diamond shape)
                for (int xOffset = -effectiveRange; xOffset <= effectiveRange; xOffset++)
                {
                    // The absolute value of xOffset determines the remaining allowance for zOffset
                    int zLimit = effectiveRange - Mathf.Abs(xOffset);
                    for (int zOffset = -zLimit; zOffset <= zLimit; zOffset++)
                    {
                        GridPosition checkPos = new GridPosition(sourcePos.x + xOffset, sourcePos.z + zOffset);
                        Vector2Int checkPosVector = checkPos.ToVector2Int();

                        // Check if the position is within grid bounds
                        if (!GridManager.Instance.IsValidGridPosition(checkPos))
                        {
                            continue;
                        }

                        // At this point, checkPos is guaranteed to be within the Manhattan distance diamond
                        // and within grid bounds. Now check ability-specific targeting rules (ground, unit types).

                        bool isValidTarget = false;
                        var unitsAtPosition = UnitManager.Instance.GetUnitsAtPosition(checkPosVector); // Get units at this potential target position

                        // Check if the ability can target ground AND the position is not occupied
                        if (selectedAbility.targetsGround && (unitsAtPosition == null || unitsAtPosition.Count == 0))
                        {
                            isValidTarget = true;
                            SmartLogger.Log($"  - Pos {checkPos}: Valid Ground Target (Ability targets ground, no unit found).", LogCategory.UI, this);
                        }

                        // Check if the ability can target units at this position
                        if (unitsAtPosition != null && unitsAtPosition.Count > 0)
                        {
                            foreach (var targetUnit in unitsAtPosition)
                            {
                                if (targetUnit == null || !targetUnit.IsAlive) continue; // Skip if unit is null or dead

                                bool isSelfTarget = selectedAbility.targetsSelf && targetUnit.UnitId == selectedUnit.UnitId;
                                bool isAllyTarget = selectedAbility.targetsAlly && targetUnit.TeamId == selectedUnit.TeamId && targetUnit.UnitId != selectedUnit.UnitId;
                                bool isEnemyTarget = selectedAbility.targetsEnemy && targetUnit.IsPlayerControlled != selectedUnit.IsPlayerControlled;

                                if (isSelfTarget || isAllyTarget || isEnemyTarget)
                                {
                                    isValidTarget = true;
                                    SmartLogger.Log($"  - Pos {checkPos}: Valid Unit Target (Ability targets units, found matching unit type: Self={isSelfTarget}, Ally={isAllyTarget}, Enemy={isEnemyTarget}).", LogCategory.UI, this);
                                    break; // Found a valid unit target at this position, no need to check other units on this tile
                                }
                                else
                                {
                                    SmartLogger.Log($"  - Pos {checkPos}: Unit target NOT valid (Ability targets units, but unit type doesn't match: Self={isSelfTarget}, Ally={isAllyTarget}, Enemy={isEnemyTarget}).", LogCategory.UI, this);
                                }
                            }
                        }

                        // If the position is a valid target based on the ability's targeting rules, add it to the list
                        if (isValidTarget)
                        {
                            targetableTiles.Add(checkPosVector);
                            SmartLogger.Log($"  - Pos {checkPos}: Added to targetableTiles list. Current count: {targetableTiles.Count}", LogCategory.UI, this);
                        }
                    }
                }
                SmartLogger.Log($"[PreviewManager.UpdateAbilityPreview] Finished generating Manhattan distance tiles. Found {targetableTiles.Count} tiles.", LogCategory.UI, this);
            }
            // --- Logic for Area-of-Effect Abilities (AoE > 0) ---
            else // selectedAbility.areaOfEffect > 0
            {
                SmartLogger.Log($"[PreviewManager.UpdateAbilityPreview] Ability '{selectedAbility.displayName}' is AoE (AoE {selectedAbility.areaOfEffect}). Using GetAffectedTiles based on hover position {targetPos}.", LogCategory.UI, this);
                // Use the existing GetAffectedTiles logic for AoE abilities
                targetableTiles = GetAffectedTiles(targetPos, selectedAbility); // GetAffectedTiles already returns List<Vector2Int>
                SmartLogger.Log($"[PreviewManager.UpdateAbilityPreview] GetAffectedTiles returned {targetableTiles.Count} tiles for AoE ability.", LogCategory.UI, this);
            }

            // --- Highlighting Logic (Applies to both single-target and AoE) ---

            // Clear existing highlights first
            ClearHighlights();
            SmartLogger.Log($"[PreviewManager.UpdateAbilityPreview] Cleared existing highlights. activeHighlights count: {activeHighlights.Count}", LogCategory.UI, this);
            // Create and show highlights for all targetable tiles
            SmartLogger.Log($"[PreviewManager.UpdateAbilityPreview] Creating highlights for {targetableTiles.Count} targetable tiles.", LogCategory.UI, this);
            foreach (var tile in targetableTiles)
            {
                // Log before creating highlight for each tile
                SmartLogger.Log($"[PreviewManager.UpdateAbilityPreview] Processing tile {tile} for highlight creation.", LogCategory.UI, this);
                // Check if the tile is valid according to the ability's target rules (redundant if logic above is correct, but good failsafe)
                bool isValidTarget = IsValidTarget(tile, selectedAbility); // This check is performed against the *base* range and original rules
                // We should check validity against the *effective* range for single-target abilities here as well,
                // but the logic above in the AoE == 0 block already filters by effective range and targeting type.
                // For AoE, IsValidTarget checks if the *center* tile is valid within base range.
                // Let's refine the highlighting validity check based on the *type* of ability and the calculated tile list.
                bool highlightIsValid = false;
                if (selectedAbility.areaOfEffect == 0) // Single-target ability
                {
                    // If the tile is in the 'targetableTiles' list for a single-target ability, it's a valid place to highlight
                    // We don't need the IsValidTarget call here because the list was generated based on validity
                    highlightIsValid = true; // All tiles in targetableTiles list are valid for highlighting
                }
                else // AoE ability
                {
                    // For AoE, check if this specific tile is contained within the area of effect centered at the hover position
                    // and if the *center* tile itself is a valid target location (this is what IsValidTarget(targetPos...) does)
                    // AND if this specific tile within the AoE is a valid *impact* tile (e.g., doesn't block AoE) - this is not currently checked.
                    // For now, let's rely on the fact that GetAffectedTiles should only return valid grid positions within the AoE shape.
                    // The color of the highlight should perhaps indicate if the *center* of the AoE is a valid *placement* target (IsValidTarget(targetPos...)).
                    // This makes the highlighting a bit complex.
                    // Let's simplify: Highlight ALL tiles returned by GetAffectedTiles/targetableTiles, and let the highlight color indicate if the *center* targetPos is valid.
                    highlightIsValid = IsValidTarget(targetPos, selectedAbility); // Color based on whether the center is a valid target location
                }
                // Create the highlight GameObject
                GameObject hoverHighlight = CreateTileHighlight(tile, highlightIsValid); // Pass calculated validity
                if (hoverHighlight != null)
                {
                    // === Corrected logic for adding to dictionary ===
                    // We need a unique key for each highlight position
                    if (!activeHighlights.ContainsKey(tile))
                    {
                        activeHighlights.Add(tile, hoverHighlight);
                        // SmartLogger.Log($"[PreviewManager.UpdateAbilityPreview] Added hover highlight for {tile} to activeHighlights dictionary. Current count: {activeHighlights.Count}.", LogCategory.UI, this);
                    }
                    else
                    {
                        SmartLogger.LogWarning($"[PreviewManager.UpdateAbilityPreview] Dictionary already contains key {tile}. Skipping add to avoid error. This might indicate an issue with highlight clearing or tile list generation.", LogCategory.UI, this);
                    }
                    // === End Corrected Logic ===
                }
                else
                {
                    SmartLogger.LogWarning($"[PreviewManager.UpdateAbilityPreview] CreateTileHighlight returned null for {tile}. Cannot add to activeHighlights.", LogCategory.UI, this);
                }
            }
            SmartLogger.Log($"[PreviewManager.UpdateAbilityPreview] Finished creating highlights. Final activeHighlights count: {activeHighlights.Count}", LogCategory.UI, this);
        }

        private List<Vector2Int> GetAffectedTiles(Vector2Int center, AbilityData ability)
        {
            var affectedTiles = new List<Vector2Int>();
            int range = ability.range;
            int area = ability.areaOfEffect;

            // Add center tile
            affectedTiles.Add(center);

            // Add tiles in area of effect
            for (int x = -area; x <= area; x++)
            {
                for (int y = -area; y <= area; y++)
                {
                    if (x == 0 && y == 0) continue; // Skip center tile

                    Vector2Int offset = new Vector2Int(x, y);
                    Vector2Int tilePos = center + offset;

                    // Check if tile is within range and grid bounds
                    if (Vector2Int.Distance(center, tilePos) <= area &&
                        GridManager.Instance.IsPositionValid(GridPosition.FromVector2Int(tilePos)))
                    {
                        affectedTiles.Add(tilePos);
                    }
                }
            }

            return affectedTiles;
        }

        private bool IsValidTarget(Vector2Int targetPos, AbilityData ability)
        {
            selectedUnit = unitManager.GetSelectedUnit();
            if (selectedUnit == null) return false;

            // Check if target is within range
            float distance = Vector2.Distance(selectedUnit.GetGridPosition().ToVector2Int(), targetPos);
            if (distance > ability.range)
            {
                return false;
            }

            // Get units at the target position
            var unitsAtPosition = UnitManager.Instance.GetUnitsAtPosition(targetPos);
            if (unitsAtPosition == null || unitsAtPosition.Count == 0)
            {
                // If no units at position, only valid if targeting ground
                return ability.targetsGround;
            }

            // Check each unit at the position
            foreach (var targetUnit in unitsAtPosition)
            {
                if (ability.targetsSelf && targetUnit == selectedUnit)
                    return true;
                if (ability.targetsAlly && targetUnit.IsPlayer() == selectedUnit.IsPlayer())
                    return true;
                if (ability.targetsEnemy && targetUnit.IsPlayer() != selectedUnit.IsPlayer())
                    return true;
            }

            // If we get here and targetsGround is true, it's valid
            return ability.targetsGround;
        }

        private GameObject CreateTileHighlight(Vector2Int gridPos, bool isValid)
        {
            // Ensure GridManager instance is available for validation
            GridManager gridManager = GridManager.Instance;
            if (gridManager == null)
            {
                SmartLogger.LogError("[PreviewManager.CreateTileHighlight] GridManager.Instance is null. Cannot validate position or create highlight.", LogCategory.UI, this);
                return null;
            }

            // Validity check
            GridPosition checkGridPosition = GridPosition.FromVector2Int(gridPos);
            if (!gridManager.IsValidGridPosition(checkGridPosition))
            {
                SmartLogger.LogWarning($"[PreviewManager.CreateTileHighlight] Attempted to create highlight for invalid grid position {checkGridPosition}. Aborting.", LogCategory.UI, this);
                return null;
            }
            if (tileHighlightPrefab == null)
            {
                SmartLogger.LogError("[PreviewManager.CreateTileHighlight] tileHighlightPrefab is null. Cannot create highlight.", LogCategory.UI, this);
                return null;
            }
            GameObject highlight = Instantiate(tileHighlightPrefab, transform);
            // ===> Keep Logs After Instantiation <===
            SmartLogger.Log($"[PreviewManager.CreateTileHighlight] Instantiated highlight for grid position {gridPos}. GameObject Name: {highlight.name}, InstanceID: {highlight.GetInstanceID()}", LogCategory.UI, this);
            SmartLogger.Log($"  ActiveSelf: {highlight.activeSelf}, ActiveInHierarchy: {highlight.activeInHierarchy}", LogCategory.UI, this);
            SmartLogger.Log($"  Initial Position: {highlight.transform.position}, Initial Scale: {highlight.transform.localScale}", LogCategory.UI, this);
            SmartLogger.Log($"  Called CreateTileHighlight for {checkGridPosition}.", LogCategory.UI, this);
            // Convert grid position to world position and set highlight position
            Vector3 worldPos = gridManager.GridToWorldPosition(checkGridPosition);
            highlight.transform.position = worldPos + Vector3.up * highlightHeight;
            // Set material based on validity
            var renderer = highlight.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = isValid ? validTargetMaterial : invalidTargetMaterial;
                SmartLogger.Log($"  Renderer found. Renderer Enabled: {renderer.enabled}, Material Name: {renderer.material.name}, Material Color: {renderer.material.color}", LogCategory.UI, this);
                // No dictionary add here
                return highlight;
            }
            else
            {
                SmartLogger.LogError($"[PreviewManager.CreateTileHighlight] No Renderer component found on highlight prefab for {gridPos}. Visuals may not appear. Aborting highlight creation.", LogCategory.UI, this);
                Destroy(highlight);
                return null;
            }
        }

        /// <summary>
        /// Clears all active highlight visuals.
        /// </summary>
        public void ClearHighlights()
        {
            SmartLogger.Log($"[PreviewManager.ClearHighlights] ENTRY. Attempting to clear {activeHighlights.Count} general highlights.", LogCategory.UI, this);

            // To avoid modifying the collection while iterating, iterate over a copy of the values
            var highlightsToDestroy = activeHighlights.Values.ToList(); // Create a list from the dictionary values

            foreach (var highlight in highlightsToDestroy) // Iterate over the list
            {
                if (highlight != null)
                {
                    string highlightInfo = $"{highlight.name} (ID: {highlight.GetInstanceID()})";
                    SmartLogger.Log($"  Processing highlight for destruction: {highlightInfo}.", LogCategory.UI, this);

                    // --- ADDED: Explicitly disable the GameObject before destroying ---
                    // Set the GameObject to inactive. This should disable all its components, including renderers.
                    if (highlight.activeSelf) // Check if it's currently active before trying to set
                    {
                         highlight.SetActive(false);
                         SmartLogger.Log($"  Explicitly set {highlightInfo} to inactive.", LogCategory.UI, this); // This log confirms the line is hit
                    }
                    // --- END ADDED ---

                    Destroy(highlight); // Mark the GameObject for destruction
                    SmartLogger.Log($"  Called Destroy for highlight {highlightInfo}.", LogCategory.UI, this);
                } else {
                    SmartLogger.LogWarning("[PreviewManager.ClearHighlights] Encountered null highlight GameObject in the list of highlights to destroy.", LogCategory.UI, this);
                }
            }

            activeHighlights.Clear(); // Clear the dictionary after processing the list
            SmartLogger.Log($"[PreviewManager.ClearHighlights] activeHighlights dictionary cleared. Current count: {activeHighlights.Count}.", LogCategory.UI, this);

            SmartLogger.Log("[PreviewManager.ClearHighlights] EXIT.", LogCategory.UI, this);
        }

        /// <summary>
        /// Shows valid destination tiles for a zone shift ability (e.g., Terrain Shift).
        /// </summary>
        public void ShowShiftDestinationPreview(GridPosition zonePosition, int shiftDistance, GridManager gridManager)
        {
            SmartLogger.Log($"[PreviewManager.ShowShiftDestinationPreview] ENTRY. Highlighting potential shift destinations for zone at {zonePosition} with shift distance {shiftDistance}. GridManager null: {gridManager == null}, HighlightPrefab null: {tileHighlightPrefab == null}, ValidMaterial null: {validTargetMaterial == null}", LogCategory.UI, this);
            if (gridManager == null || tileHighlightPrefab == null || validTargetMaterial == null)
            {
                SmartLogger.LogError("[PreviewManager.ShowShiftDestinationPreview] Missing required references.", LogCategory.UI, this);
                return;
            }
            int maxCheckDistance = shiftDistance;
            SmartLogger.Log($"[PreviewManager.ShowShiftDestinationPreview] Checking positions within a {maxCheckDistance * 2 + 1}x{maxCheckDistance * 2 + 1} area around {zonePosition}.", LogCategory.UI, this);

            for (int xOffset = -maxCheckDistance; xOffset <= maxCheckDistance; xOffset++)
            {
                for (int zOffset = -maxCheckDistance; zOffset <= maxCheckDistance; zOffset++)
                {
                    GridPosition potentialDestination = new GridPosition(zonePosition.x + xOffset, zonePosition.z + zOffset);
                    SmartLogger.Log($"[PreviewManager.ShowShiftDestinationPreview] Checking potential destination {potentialDestination} (offset: {xOffset}, {zOffset}).", LogCategory.UI, this);
                    if (!gridManager.IsValidGridPosition(potentialDestination))
                    {
                        SmartLogger.Log($"  Position {potentialDestination} is outside grid bounds. Skipping.", LogCategory.UI, this);
                        continue;
                    }
                    int distance = GridPosition.GetManhattanDistance(zonePosition, potentialDestination);
                    SmartLogger.Log($"  Manhattan distance from {zonePosition} to {potentialDestination}: {distance}. Required distance: {shiftDistance}.", LogCategory.UI, this);
                    if (distance != shiftDistance)
                    {
                        SmartLogger.Log($"  Distance {distance} does not match required shift distance {shiftDistance}. Skipping.", LogCategory.UI, this);
                        continue;
                    }
                    bool isDestinationOccupied = gridManager.IsTileOccupied(potentialDestination);
                    bool isDestinationVoidSpace = ZoneManager.Instance?.IsVoidSpace(potentialDestination) ?? false;
                    bool isValidDestination = !isDestinationOccupied && !isDestinationVoidSpace;
                    SmartLogger.Log($"  Destination {potentialDestination} Validity Checks: IsTileOccupied={isDestinationOccupied}, IsVoidSpace={isDestinationVoidSpace}. IsValidDestination = {isValidDestination}", LogCategory.UI, this);
                    if (isValidDestination)
                    {
                        SmartLogger.Log($"  Position {potentialDestination} is a valid shift destination. Creating highlight.", LogCategory.UI, this);
                        GameObject shiftHighlight = CreateTileHighlight(potentialDestination.ToVector2Int(), true);
                        if (shiftHighlight != null)
                        {
                            // === Corrected logic for adding to dictionary ===
                            // We need a unique key for each highlight position
                            if (!activeHighlights.ContainsKey(potentialDestination.ToVector2Int()))
                            {
                                activeHighlights.Add(potentialDestination.ToVector2Int(), shiftHighlight);
                            }
                            else
                            {
                                SmartLogger.LogWarning($"[PreviewManager.ShowShiftDestinationPreview] Dictionary already contains key {potentialDestination.ToVector2Int()}. Skipping add to avoid error. This might indicate an issue with highlight clearing or tile list generation.", LogCategory.UI, this);
                            }
                            // === End Corrected Logic ===
                        }
                    }
                    else
                    {
                        SmartLogger.Log($"  Position {potentialDestination} is NOT a valid shift destination.", LogCategory.UI, this);
                    }
                }
            }
            SmartLogger.Log("[PreviewManager.ShowShiftDestinationPreview] Finished iterating through potential destinations.", LogCategory.UI, this);
            SmartLogger.Log($"[PreviewManager.ShowShiftDestinationPreview] Method finished. Total shift highlights added to list: {activeHighlights.Count}.", LogCategory.UI, this);
        }
    }
} 
