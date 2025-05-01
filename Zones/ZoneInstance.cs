using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Dokkaebi.Common;
using Dokkaebi.Interfaces;
using Dokkaebi.Core;
using Dokkaebi.Utilities;
using Dokkaebi.Grid;
using Dokkaebi.Core.Data;
using Dokkaebi.Units;
using Dokkaebi.Abilities.Specific;

namespace Dokkaebi.Zones
{
    /// <summary>
    /// Represents an active zone instance in the game world.
    /// Contains the runtime properties of the zone including its effects.
    /// </summary>
    public class ZoneInstance : MonoBehaviour, IZoneInstance
    {
        [SerializeField] private Renderer zoneVisual;
        [SerializeField] private float visualFadeSpeed = 2f;
        
        private IZoneData zoneData;
        private IStatusEffect statusEffect;
        private int remainingDuration;
        private int currentStacks = 1;
        private bool isActive = true;
        private GridPosition position;
        private int ownerUnitId = -1;
        private float currentAlpha = 1f;
        private bool isFading;
        private Material zoneMaterialInstance; // Store the material instance
        
        private HashSet<IDokkaebiUnit> _affectedUnits = new HashSet<IDokkaebiUnit>();
        private HashSet<IDokkaebiUnit> _unitsAffectedLastTurn = new HashSet<IDokkaebiUnit>();
        
        // IZoneInstance implementation
        public GridPosition Position => position;
        public int Id => GetInstanceID();
        public int Radius => zoneData?.Radius ?? 1;
        public bool IsActive => isActive;
        public int RemainingDuration => remainingDuration;
        public int OwnerUnitId => ownerUnitId;
        
        // Additional properties
        public string DisplayName => zoneData?.DisplayName ?? "Unknown Zone";
        public int CurrentStacks => currentStacks;
        public bool CanMerge => zoneData?.CanMerge ?? false;
        public int MaxStacks => zoneData?.MaxStacks ?? 1;
        public IReadOnlyList<string> MergesWithZoneIds => zoneData?.MergesWithZoneIds ?? new List<string>();
        public IReadOnlyList<string> ResonanceOrigins => zoneData?.ResonanceOrigins ?? new List<string>();
        public float ResonanceEffectMultiplier => zoneData?.ResonanceEffectMultiplier ?? 1f;
        
        private void Awake()
        {
            // Try to find the renderer if not assigned
            if (zoneVisual == null)
            {
                // Look for a child object named "Plane" with a Renderer
                var planeChild = transform.Find("Plane");
                if (planeChild != null)
                {
                    zoneVisual = planeChild.GetComponent<Renderer>();
                    if (zoneVisual != null)
                    {
                        SmartLogger.Log($"[Instance:{GetInstanceID()}] Found Renderer on 'Plane' child object", LogCategory.Zone, this);
                    }
                }
                
                if (zoneVisual == null)
                {
                    SmartLogger.LogWarning($"[Instance:{GetInstanceID()}] No Renderer found on 'Plane' child. Zone visuals won't work!", LogCategory.Zone, this);
                }
            }
        }
        
        public void Initialize(IZoneData data, GridPosition pos, int ownerUnit, int duration = -1)
        {
            zoneData = data;
            position = pos;
            ownerUnitId = ownerUnit;
            remainingDuration = duration >= 0 ? duration : data.DefaultDuration;
            SetupVisuals();
            // --- ADDED LOGGING FOR DEBUGGING ZONE PLACEMENT (ensure this remains) ---
            SmartLogger.Log($"[ZoneInstance.Initialize] Initializing zone '{DisplayName}' (Instance:{GetInstanceID()})", LogCategory.Zone, this);
            SmartLogger.Log($"[ZoneInstance.Initialize] Input GridPosition: {pos}", LogCategory.Zone, this);
            // Calculate world position and set transform
            if (GridManager.Instance != null)
            {
                Vector3 worldPosition = GridManager.Instance.GridToWorldPosition(pos);
                worldPosition.y += 0.1f; // Raise zone slightly above ground
                SmartLogger.Log($"[ZoneInstance.Initialize] Calculated world position for grid {pos}: {worldPosition}", LogCategory.Zone, this);
                transform.position = worldPosition;
                SmartLogger.Log($"[ZoneInstance.Initialize] Final transform.position after setting: {transform.position}", LogCategory.Zone, this);
            }
            else
            {
                SmartLogger.LogError("[ZoneInstance.Initialize] GridManager.Instance is null! Cannot set zone world position.", LogCategory.Zone, this);
            }
            // --- END ADDED LOGGING ---
        }
        
        private void SetupVisuals()
        {
            SmartLogger.Log($"[Instance:{GetInstanceID()}] SetupVisuals() START for zone '{DisplayName}'. zoneVisual field assigned: {zoneVisual != null}", LogCategory.Zone, this);
            if (zoneVisual != null && zoneData != null)
            {
                // Create a material instance to avoid modifying the shared material
                zoneMaterialInstance = new Material(zoneVisual.sharedMaterial);
                SmartLogger.Log($"[Instance:{GetInstanceID()}] SetupVisuals() - zoneMaterialInstance created: {zoneMaterialInstance != null}", LogCategory.Zone, this);
                zoneVisual.material = zoneMaterialInstance;
                
                // Set initial color and alpha
                var color = zoneData.ZoneColor;
                color.a = currentAlpha;
                zoneMaterialInstance.color = color;
                
                SmartLogger.Log($"[Instance:{GetInstanceID()}] Zone visuals initialized with color {color} and alpha {currentAlpha}", LogCategory.Zone, this);
            }
            else
            {
                SmartLogger.LogWarning($"[Instance:{GetInstanceID()}] Cannot setup visuals: zoneVisual={zoneVisual}, zoneData={zoneData}", LogCategory.Zone, this);
            }
        }
        
        private void OnDestroy()
        {
            // Clean up the material instance when the zone is destroyed
            if (zoneMaterialInstance != null)
            {
                Destroy(zoneMaterialInstance);
            }
        }
        
        public void AddStack()
        {
            if (currentStacks < zoneData.MaxStacks)
            {
                currentStacks++;
            }
        }
        
        public void DecreaseDuration()
        {
            if (remainingDuration > 0)
            {
                remainingDuration--;
                if (remainingDuration <= 0)
                {
                    StartFade();
                }
            }
        }
        
        public void SetStatusEffect(IStatusEffect effect)
        {
            statusEffect = effect;
        }
        
        public IStatusEffect GetStatusEffect()
        {
            return statusEffect;
        }
        
        public bool ContainsPosition(GridPosition pos)
        {
            // Calculate Manhattan distance between positions
            int dx = Mathf.Abs(pos.x - position.x);
            int dz = Mathf.Abs(pos.z - position.z);
            return dx + dz <= Radius;
        }
        
        private void StartFade()
        {
            isFading = true;
            isActive = false;
        }
        
        /// <summary>
        /// Applies this zone's effects to units within its area.
        /// Called by ZoneManager during turn resolution.
        /// </summary>
        public void ApplyZoneEffects(IDokkaebiUnit targetUnit = null)
        {
            SmartLogger.Log($"[ZoneInstance.ApplyZoneEffects] BEGIN: Zone '{DisplayName}' at {position}", LogCategory.Zone, this);
            if (!IsActive || zoneData == null)
            {
                SmartLogger.Log($"Zone '{DisplayName}' cannot apply effects: IsActive={IsActive}, zoneData={(zoneData == null ? "null" : "valid")}", LogCategory.Zone);
                return;
            }
            ZoneData concreteZoneData = zoneData as ZoneData;
            if (concreteZoneData == null)
            {
                SmartLogger.LogError($"Cannot apply effects: zoneData is not of type ZoneData for {DisplayName}", LogCategory.Zone);
                return;
            }
            SmartLogger.Log($"[ZoneInstance.ApplyZoneEffects] Confirmed valid concreteZoneData for '{DisplayName}'", LogCategory.Zone, this);
            if (UnitManager.Instance == null)
            {
                SmartLogger.LogError("[ZoneInstance.ApplyZoneEffects] Cannot apply zone effects: UnitManager instance is null.", LogCategory.Zone);
                return;
            }
            SmartLogger.Log($"[ZoneInstance.ApplyZoneEffects] Starting unit check loop for zone '{DisplayName}' (Radius: {Radius})", LogCategory.Zone, this);

            // The StormSurge movement buff is now applied only on entry via SetGridPosition -> ApplyStatusEffectToUnitImmediate
            bool skipImmediateEffectHere = concreteZoneData.Id == "StormSurge";

            if (targetUnit != null)
            {
                if (targetUnit is DokkaebiUnit dokkaebiUnit && ContainsPosition(dokkaebiUnit.GetGridPosition()))
                {
                    SmartLogger.Log($"Zone '{DisplayName}' applying effects to specific target unit: {targetUnit.GetUnitName()}", LogCategory.Zone);
                    ApplyEffectsToUnit(targetUnit, concreteZoneData); // Apply damage/healing/periodic effects
                    // Do NOT call ApplyStatusEffectToUnitImmediate here for any zone type,
                    // as it's specifically for effects applied *upon entering*.
                }
                return;
            }

            var unitsInZone = GetUnitsInZone();
            _unitsAffectedLastTurn = new HashSet<IDokkaebiUnit>(_affectedUnits);
            _affectedUnits.Clear();
            SmartLogger.Log($"[ZoneInstance.ApplyZoneEffects] Found {unitsInZone.Count} units in zone '{DisplayName}' at {position}. Tracking last turn's affected units: {_unitsAffectedLastTurn.Count}.", LogCategory.Zone, this);

            foreach (var unit in unitsInZone)
            {
                if (unit == null || !unit.IsAlive)
                {
                    if(unit == null) SmartLogger.LogWarning("[ZoneInstance.ApplyZoneEffects] Skipping null unit in unitsInZone list.", LogCategory.Zone, this);
                    else if (!unit.IsAlive) SmartLogger.LogWarning($"[ZoneInstance.ApplyZoneEffects] Skipping dead unit {unit.GetUnitName()}.", LogCategory.Zone, this);
                    continue;
                }

                _affectedUnits.Add(unit);
                SmartLogger.Log($"[ZoneInstance.ApplyZoneEffects] Unit {unit.GetUnitName()} at {unit.CurrentGridPosition} is in zone. About to call ApplyEffectsToUnit.", LogCategory.Zone, this);

                // Apply zone-specific effects using the correct method with ZoneData
                ApplyEffectsToUnit(unit, concreteZoneData);
                // Do NOT call ApplyStatusEffectToUnitImmediate here for StormSurge or any zone.
            }

            // Remove effects from units that left the zone
            foreach (var unit in _unitsAffectedLastTurn)
            {
                if (unit == null || !unit.IsAlive) continue;

                // If unit is no longer in the affected set, they've left the zone
                if (!_affectedUnits.Contains(unit))
                {
                    RemoveZoneEffectsFromUnit(unit);
                }
                else
                {
                    SmartLogger.Log($"[ZoneInstance.ApplyZoneEffects] Unit {unit.GetUnitName()} was in zone last turn and is still in zone this turn. Keeping effects.", LogCategory.Zone, this);
                }
            }
            SmartLogger.Log($"[ZoneInstance.ApplyZoneEffects] COMPLETE: Effect application complete for zone '{DisplayName}'", LogCategory.Zone, this);
        }

        private void ApplyEffectsToUnit(IDokkaebiUnit targetUnit, ZoneData concreteZoneData)
        {
            SmartLogger.Log($"[ZoneInstance.ApplyEffectsToUnit] BEGIN: Zone '{DisplayName}' applying to unit '{targetUnit.GetUnitName()}'.", LogCategory.Zone, this);
            if (!(targetUnit is DokkaebiUnit dokkaebiUnit))
            {
                SmartLogger.LogWarning($"Zone '{DisplayName}': Target unit is not a DokkaebiUnit, cannot apply effects", LogCategory.Zone);
                return;
            }
            var ownerUnit = ownerUnitId >= 0 ? UnitManager.Instance?.GetUnitById(ownerUnitId) : null;
            bool isAlly = ownerUnit != null && dokkaebiUnit.TeamId == ownerUnit.TeamId;
            bool isSelf = ownerUnit != null && dokkaebiUnit.UnitId == ownerUnit.UnitId;
            bool isEnemy = ownerUnit != null && dokkaebiUnit.TeamId != ownerUnit.TeamId;
            SmartLogger.Log($"[ZoneInstance.ApplyEffectsToUnit] Allegiance Check Details for {dokkaebiUnit.GetUnitName()}: Zone Affects='{concreteZoneData.affects}', IsAlly={isAlly}, IsSelf={isSelf}, IsEnemy={isEnemy}, OwnerUnit TeamId={(ownerUnit != null ? ownerUnit.TeamId.ToString() : "NULL")}, TargetUnit TeamId={dokkaebiUnit.TeamId}", LogCategory.Zone, this);
            bool shouldAffect = concreteZoneData.affects switch
            {
                AllegianceTarget.Any => true,
                AllegianceTarget.AllyOnly => isAlly || isSelf,
                AllegianceTarget.EnemyOnly => isEnemy,
                _ => false
            };
            SmartLogger.Log($"[ZoneInstance.ApplyEffectsToUnit] Allegiance check result: shouldAffect={shouldAffect}", LogCategory.Zone, this);
            if (!shouldAffect)
            {
                SmartLogger.Log($"[ZoneInstance.ApplyEffectsToUnit] Skipping unit '{dokkaebiUnit.GetUnitName()}' due to allegiance mismatch.", LogCategory.Zone, this);
                return;
            }
            SmartLogger.Log($"[ZoneInstance.ApplyEffectsToUnit] Unit '{dokkaebiUnit.GetUnitName()}' will be affected.", LogCategory.Zone, this);
            float effectMultiplier = currentStacks;
            if (ResonanceOrigins.Count > 0 && ownerUnit != null)
            {
                var ownerOrigin = ownerUnit.GetOrigin();
                if (ownerOrigin != null && ResonanceOrigins.Any(originId => originId == ownerOrigin.originId))
                {
                    effectMultiplier *= ResonanceEffectMultiplier;
                    SmartLogger.Log($"- Resonance multiplier applied: {ResonanceEffectMultiplier}x", LogCategory.Zone);
                }
            }
            if (concreteZoneData.damagePerTurn != 0)
            {
                int scaledDamage = Mathf.RoundToInt(concreteZoneData.damagePerTurn * effectMultiplier);
                SmartLogger.Log($"[ZoneInstance.ApplyEffectsToUnit] Applying {scaledDamage} {concreteZoneData.damageType} damage to unit {dokkaebiUnit.GetUnitName()} at {dokkaebiUnit.CurrentGridPosition}.", LogCategory.Zone, this);
                dokkaebiUnit.TakeDamage(scaledDamage, concreteZoneData.damageType);
            }
            if (concreteZoneData.healPerTurn != 0)
            {
                int scaledHealing = Mathf.RoundToInt(concreteZoneData.healPerTurn * effectMultiplier);
                SmartLogger.Log($"[ZoneInstance.ApplyEffectsToUnit] Applying {scaledHealing} healing to unit {dokkaebiUnit.GetUnitName()}.", LogCategory.Zone, this);
                dokkaebiUnit.Heal(scaledHealing);
            }
            // NEW: Apply all status effects in the list
            if (concreteZoneData.appliedStatusEffects != null && concreteZoneData.appliedStatusEffects.Count > 0)
            {
                foreach (var effect in concreteZoneData.appliedStatusEffects)
                {
                    if (effect != null)
                    {
                        SmartLogger.Log($"[ZoneInstance.ApplyEffectsToUnit] Applying status effect '{effect.displayName}' to {dokkaebiUnit.GetUnitName()}.", LogCategory.Zone, this);
                        StatusEffectSystem.ApplyStatusEffect(dokkaebiUnit, effect, remainingDuration, ownerUnit);
                        SmartLogger.Log($"[ZoneInstance.ApplyEffectsToUnit] Applied status effect '{effect.displayName}' with duration {remainingDuration} to {dokkaebiUnit.GetUnitName()}. Effect Type: {effect.effectType}.", LogCategory.Zone, this);
                    }
                }
            }
            // Legacy: applyStatusEffect
            else if (concreteZoneData.applyStatusEffect != null)
            {
                SmartLogger.Log($"[ZoneInstance.ApplyEffectsToUnit] Applying status effect '{concreteZoneData.applyStatusEffect.displayName}' to {dokkaebiUnit.GetUnitName()} (legacy field).", LogCategory.Zone, this);
                StatusEffectSystem.ApplyStatusEffect(dokkaebiUnit, concreteZoneData.applyStatusEffect, remainingDuration, ownerUnit);
                SmartLogger.Log($"[ZoneInstance.ApplyEffectsToUnit] Applied status effect '{concreteZoneData.applyStatusEffect.displayName}' with duration {remainingDuration} to {dokkaebiUnit.GetUnitName()} (legacy field). Effect Type: {concreteZoneData.applyStatusEffect.effectType}.", LogCategory.Zone, this);
            }
            SmartLogger.Log($"[ZoneInstance.ApplyEffectsToUnit] END: Zone '{DisplayName}' applied effects to unit '{targetUnit.GetUnitName()}'.", LogCategory.Zone, this);
        }
        
        /// <summary>
        /// Processes turn-based logic for this zone
        /// </summary>
        public void ProcessTurn()
        {
            // Log initial state
            SmartLogger.Log($"[Instance:{GetInstanceID()}] Zone {DisplayName} ({Id}) ProcessTurn START - IsActive: {isActive}, RemainingDuration: {remainingDuration}, IsPermanent: {zoneData?.IsPermanent ?? false}", LogCategory.Zone, this);

            // Skip if already inactive
            if (!isActive)
            {
                SmartLogger.Log($"[Instance:{GetInstanceID()}] Zone {DisplayName} ({Id}) is inactive, skipping turn processing", LogCategory.Zone, this);
                return;
            }

            // Process duration if not permanent
            if (!zoneData.IsPermanent)
            {
                // Log before decrement
                SmartLogger.Log($"[Instance:{GetInstanceID()}] Zone {DisplayName} ({Id}) BEFORE duration decrement - Current Duration: {remainingDuration}", LogCategory.Zone, this);
                
                if (remainingDuration > 0)
                {
                    remainingDuration--;
                    // Log after decrement
                    SmartLogger.Log($"[Instance:{GetInstanceID()}] Zone {DisplayName} ({Id}) AFTER duration decrement - New Duration: {remainingDuration}", LogCategory.Zone, this);

                    if (remainingDuration <= 0)
                    {
                        SmartLogger.Log($"[Instance:{GetInstanceID()}] Zone {DisplayName} ({Id}) duration reached zero, calling Deactivate()", LogCategory.Zone, this);
                        Deactivate();
                    }
                }
                
                // Log final state
                SmartLogger.Log($"[Instance:{GetInstanceID()}] Zone {DisplayName} ({Id}) ProcessTurn END - Final State: Duration={remainingDuration}, IsActive={isActive}", LogCategory.Zone, this);
            }
            else
            {
                SmartLogger.Log($"[Instance:{GetInstanceID()}] Zone {DisplayName} ({Id}) is permanent, skipping duration processing", LogCategory.Zone, this);
            }
        }
        
        /// <summary>
        /// Deactivate the zone (typically called when duration expires)
        /// </summary>
        public void Deactivate()
        {
            SmartLogger.Log($"[Instance:{GetInstanceID()}] Zone {DisplayName} ({Id}) Deactivate() START - Current State: IsActive={isActive}, IsFading={isFading}", LogCategory.Zone, this);

            // Set inactive state
            isActive = false;
            SmartLogger.Log($"[Instance:{GetInstanceID()}] Zone {DisplayName} ({Id}) set isActive to false", LogCategory.Zone, this);
            // Start the fade out process
            StartFade();
            SmartLogger.Log($"[Instance:{GetInstanceID()}] Zone {DisplayName} ({Id}) started fade out process (currentAlpha={currentAlpha})", LogCategory.Zone, this);
        }
        
        /// <summary>
        /// Checks if this zone can merge with another zone
        /// </summary>
        public bool CanMergeWith(ZoneInstance otherZone)
        {
            // TODO: Implement logic to check if this zone can merge with another based on ZoneData
            if (zoneData == null || otherZone.zoneData == null || !CanMerge) return false;
            return MergesWithZoneIds.Contains(otherZone.zoneData.Id);
        }
        
        /// <summary>
        /// Merges this zone with another zone
        /// </summary>
        public void MergeWith(ZoneInstance otherZone)
        {
            SmartLogger.Log($"Merging zone {otherZone.DisplayName} into {DisplayName} at {position}", LogCategory.Zone, this);
            // Implementation of zone merging logic
        }
        
        /// <summary>
        /// Gets the grid position of this zone
        /// </summary>
        public GridPosition GetGridPosition()
        {
            return position;
        }

        /// <summary>
        /// Sets the zone's internal grid position state.
        /// Called by ZoneManager when the zone is shifted.
        /// </summary>
        /// <param name="newPosition">The new grid position of the zone's center.</param>
        public void SetGridPosition(GridPosition newPosition)
        {
            position = newPosition;
            // Optionally update the GameObject's transform position here if the visual
            // is directly tied to the ZoneInstance GameObject and not managed elsewhere.
            // However, ZoneManager.ShiftZone already updates the transform, so this might
            // only be needed for internal state consistency.
            SmartLogger.Log($"[Instance:{GetInstanceID()}] Zone '{DisplayName}' internal position updated to {newPosition}.", LogCategory.Zone, this); // Added log
        }
        
        /// <summary>
        /// Applies this zone's initial effects when it is first created.
        /// </summary>
        public void ApplyInitialEffects()
        {
            SmartLogger.Log($"[Instance:{GetInstanceID()}] Zone '{DisplayName}' ApplyInitialEffects() START.", LogCategory.Zone, this);
            // Ensure we have valid zone data and the zone is active
            if (!IsActive || zoneData == null)
            {
                SmartLogger.Log($"[Instance:{GetInstanceID()}] Zone '{DisplayName}' cannot apply initial effects: IsActive={IsActive}, zoneData={(zoneData == null ? "null" : "valid")}", LogCategory.Zone, this);
                return;
            }

            // Cast zoneData to the concrete ZoneData type once
            ZoneData concreteZoneData = zoneData as ZoneData;
            if (concreteZoneData == null)
            {
                SmartLogger.LogError($"[Instance:{GetInstanceID()}] ApplyInitialEffects failed: ZoneData is not of type ZoneData for {zoneData?.DisplayName ?? "Unknown Zone"}", LogCategory.Zone, this);
                return;
            }
            SmartLogger.Log($"[Instance:{GetInstanceID()}] Zone '{DisplayName}' has valid concreteZoneData. Checking for initial effects configuration.", LogCategory.Zone, this);

            // --- Check if applyInitialStatusEffect is assigned ---
            if (concreteZoneData.applyInitialStatusEffect == null)
            {
                SmartLogger.LogWarning($"[Instance:{GetInstanceID()}] Zone '{DisplayName}' has NO initial status effect configured. Skipping initial status effect application.", LogCategory.Zone, this);
            }
            // --- End check for applyInitialStatusEffect ---
            // Apply initial status effect if configured
            if (concreteZoneData.applyInitialStatusEffect != null)
            {
                SmartLogger.Log($"[Instance:{GetInstanceID()}] Zone '{DisplayName}' applying initial status effect '{concreteZoneData.applyInitialStatusEffect.displayName}'. Checking 3x3 area.", LogCategory.Zone, this);

                // Iterate through the 3x3 area centered on the zone's position
                for (int xOffset = -1; xOffset <= 1; xOffset++)
                {
                    for (int zOffset = -1; zOffset <= 1; zOffset++)
                    {
                        GridPosition checkPos = new GridPosition(position.x + xOffset, position.z + zOffset);

                        // --- ADD LOGGING HERE ---
                        SmartLogger.Log($"[Instance:{GetInstanceID()}] Initial Effects Check - Checking position: {checkPos}", LogCategory.Zone, this);
                        // --- END ADDING LOGGING ---
                        // Ensure the position is valid within the grid bounds
                        if (GridManager.Instance != null && GridManager.Instance.IsValidGridPosition(checkPos))
                        {
                            // Check for a unit at this valid position
                            IDokkaebiUnit targetUnit = UnitManager.Instance?.GetUnitAtPosition(checkPos);
                            if (targetUnit != null && targetUnit.IsAlive)
                            {
                                // --- ADD LOGGING HERE ---
                                SmartLogger.Log($"[Instance:{GetInstanceID()}] Initial Effects Check - Found unit {targetUnit.GetUnitName()} at position {checkPos}. Performing allegiance check.", LogCategory.Zone, this);
                                // --- END ADDING LOGGING ---

                                // Perform Allegiance Check
                                IDokkaebiUnit ownerUnit = ownerUnitId >= 0 ? UnitManager.Instance.GetUnitById(ownerUnitId) : null;
                                bool isAlly = ownerUnit != null && targetUnit.TeamId == ownerUnit.TeamId;
                                bool isEnemy = ownerUnit != null && targetUnit.TeamId != ownerUnit.TeamId;
                                bool isSelf = ownerUnit != null && targetUnit.UnitId == ownerUnit.UnitId;

                                bool shouldAffect = concreteZoneData.affects switch
                                {
                                    AllegianceTarget.Any => true,
                                    AllegianceTarget.AllyOnly => isAlly || isSelf,
                                    AllegianceTarget.EnemyOnly => isEnemy,
                                    _ => false
                                };

                                // --- ADD LOGGING HERE ---
                                SmartLogger.Log($"[Instance:{GetInstanceID()}] Initial Effects Check - Allegiance Check Details for {targetUnit.GetUnitName()}: Zone Affects='{concreteZoneData.affects}', IsAlly={isAlly}, IsSelf={isSelf}, IsEnemy={isEnemy}, shouldAffect={shouldAffect}", LogCategory.Zone, this);
                                // --- END ADDING LOGGING ---
                                if (shouldAffect)
                                {
                                    SmartLogger.Log($"[Instance:{GetInstanceID()}] Applying initial status effect '{concreteZoneData.applyInitialStatusEffect.displayName}' to unit {targetUnit.GetUnitName()} at position {checkPos}. Zone's remaining duration: {remainingDuration}.", LogCategory.Zone, this);
                                    // Pass the zone's remainingDuration as the status effect duration
                                    StatusEffectSystem.ApplyStatusEffect(targetUnit, concreteZoneData.applyInitialStatusEffect, remainingDuration, ownerUnit);
                                    SmartLogger.Log($"[Instance:{GetInstanceID()}] Initial status effect applied to {targetUnit.GetUnitName()} with duration {remainingDuration}.", LogCategory.Zone, this);
                                }
                                else
                                {
                                    SmartLogger.Log($"[Instance:{GetInstanceID()}] Skipping unit {targetUnit.GetUnitName()} at position {checkPos} due to allegiance rules for initial effect.", LogCategory.Zone, this);
                                }
                            }
                            else
                            {
                                // --- ADD LOGGING HERE ---
                                SmartLogger.Log($"[Instance:{GetInstanceID()}] Initial Effects Check - No unit found at position {checkPos}.", LogCategory.Zone, this);
                                // --- END ADDING LOGGING ---
                            }
                        }
                        else if (GridManager.Instance == null)
                        {
                            SmartLogger.LogError($"[Instance:{GetInstanceID()}] GridManager.Instance is null during initial effects check at {checkPos}!", LogCategory.Zone, this);
                        }
                        else if (UnitManager.Instance == null)
                        {
                            SmartLogger.LogError($"[Instance:{GetInstanceID()}] UnitManager.Instance is null during initial effects check at {checkPos}!", LogCategory.Zone, this);
                        }
                    }
                }
            }
            // Apply initial damage if configured
            if (concreteZoneData.initialDamageAmount > 0)
            {
                SmartLogger.Log($"[Instance:{GetInstanceID()}] Zone '{DisplayName}' applying initial damage of {concreteZoneData.initialDamageAmount}. Checking 3x3 area.", LogCategory.Zone, this);
                // Iterate through the 3x3 area centered on the zone's position
                for (int xOffset = -1; xOffset <= 1; xOffset++)
                {
                    for (int zOffset = -1; zOffset <= 1; zOffset++)
                    {
                        GridPosition checkPos = new GridPosition(position.x + xOffset, position.z + zOffset);

                        // --- ADD LOGGING HERE ---
                        SmartLogger.Log($"[Instance:{GetInstanceID()}] Initial Damage Check - Checking position: {checkPos}", LogCategory.Zone, this);
                        // --- END ADDING LOGGING ---
                        if (!GridManager.Instance.IsValidGridPosition(checkPos))
                        {
                            SmartLogger.Log($"[Instance:{GetInstanceID()}] Position {checkPos} is outside grid bounds, skipping initial damage.", LogCategory.Zone, this);
                            continue;
                        }

                        var unit = UnitManager.Instance.GetUnitAtPosition(checkPos);
                        if (unit != null)
                        {
                            // --- ADD LOGGING HERE ---
                            SmartLogger.Log($"[Instance:{GetInstanceID()}] Initial Damage Check - Found unit {unit.GetUnitName()} at position {checkPos}. Performing allegiance check.", LogCategory.Zone, this);
                            // --- END ADDING LOGGING ---
                            // Check if the unit should be affected based on allegiance
                            IDokkaebiUnit ownerUnit = ownerUnitId >= 0 ? UnitManager.Instance.GetUnitById(ownerUnitId) : null;
                            bool isAlly = ownerUnit != null && unit.TeamId == ownerUnit.TeamId;
                            bool isEnemy = ownerUnit != null && unit.TeamId != ownerUnit.TeamId;
                            bool isSelf = ownerUnit != null && unit.UnitId == ownerUnit.UnitId;
                            bool shouldAffect = concreteZoneData.affects switch
                            {
                                AllegianceTarget.Any => true,
                                AllegianceTarget.AllyOnly => isAlly || isSelf,
                                AllegianceTarget.EnemyOnly => isEnemy,
                                _ => false
                            };
                            // --- ADD LOGGING HERE ---
                            SmartLogger.Log($"[Instance:{GetInstanceID()}] Initial Damage Check - Allegiance check result for {unit.GetUnitName()}: shouldAffect={shouldAffect}.", LogCategory.Zone, this);
                            // --- END ADDING LOGGING ---

                            if (shouldAffect)
                            {
                                unit.TakeDamage(concreteZoneData.initialDamageAmount, concreteZoneData.initialDamageType);
                                SmartLogger.Log($"[Instance:{GetInstanceID()}] Applied initial zone damage {concreteZoneData.initialDamageAmount} to unit {unit.GetUnitName()} at position {checkPos}.", LogCategory.Zone, this);
                            }
                            else
                            {
                                SmartLogger.Log($"[Instance:{GetInstanceID()}] Unit {unit.GetUnitName()} at position {checkPos} not affected by initial damage due to allegiance rules.", LogCategory.Zone, this);
                            }
                        }
                        else
                        {
                            // --- ADD LOGGING HERE ---
                            SmartLogger.Log($"[Instance:{GetInstanceID()}] Initial Damage Check - No unit found at position {checkPos}.", LogCategory.Zone, this);
                            // --- END ADDING LOGGING ---
                        }
                    }
                }
            }
            SmartLogger.Log($"[Instance:{GetInstanceID()}] Zone '{DisplayName}' ApplyInitialEffects() END.", LogCategory.Zone, this);
        }
        
        private void Update()
        {
            // Log every 0.5 seconds to avoid spam but still track execution
            if (Time.frameCount % 30 == 0)  // Assuming 60 FPS, this logs every ~0.5 seconds
            {
                //SmartLogger.Log($"[Instance:{GetInstanceID()}] Zone {DisplayName} Update() called. IsActive={isActive}, IsFading={isFading}, CurrentAlpha={currentAlpha:F3}", LogCategory.Zone, this);
            }

            if (isFading)
            {
                float previousAlpha = currentAlpha;
                float deltaAlpha = Time.deltaTime * visualFadeSpeed;
                currentAlpha = Mathf.MoveTowards(currentAlpha, 0f, deltaAlpha);
                
                // Log the alpha calculation details if there's a significant change
                if (Mathf.Abs(previousAlpha - currentAlpha) > 0.05f)
                {
                    SmartLogger.Log($"[Instance:{GetInstanceID()}] Zone {DisplayName} Fading calculation: previousAlpha={previousAlpha:F3}, deltaAlpha={deltaAlpha:F3}, newAlpha={currentAlpha:F3}, fadeSpeed={visualFadeSpeed}", LogCategory.Zone, this);
                }
                
                if (zoneVisual != null && zoneMaterialInstance != null)
                {
                    var color = zoneMaterialInstance.color;
                    color.a = currentAlpha;
                    zoneMaterialInstance.color = color;
                    
                    // Log visual update if alpha changed significantly
                    if (Mathf.Abs(previousAlpha - currentAlpha) > 0.05f)
                    {
                        SmartLogger.Log($"[Instance:{GetInstanceID()}] Zone {DisplayName} Visual alpha updated to {currentAlpha:F3}", LogCategory.Zone, this);
                    }
                }
                else
                {
                    SmartLogger.LogWarning($"[Instance:{GetInstanceID()}] Zone {DisplayName} is fading but renderer or material is null! Renderer={zoneVisual}, Material={zoneMaterialInstance}", LogCategory.Zone, this);
                    // Stop fading if we lost our renderer or material
                    isFading = false;
                }
                
                // Log before destruction check
                if (currentAlpha <= 0.05f)
                {
                    //SmartLogger.Log($"[Instance:{GetInstanceID()}] Zone {DisplayName} approaching destruction threshold. CurrentAlpha={currentAlpha:F3}", LogCategory.Zone, this);
                }
                
                if (currentAlpha <= 0f)
                {
                    SmartLogger.Log($"[Instance:{GetInstanceID()}] Zone {DisplayName} DESTROYING - Final state: IsActive={isActive}, IsFading={isFading}, CurrentAlpha={currentAlpha:F3}", LogCategory.Zone, this);
                    Destroy(gameObject);
                }
            }
        }

        /// <summary>
        /// Apply zone effects to units within the zone
        /// </summary>
        public void ApplyZoneEffects()
        {
            if (!isActive || zoneData == null)
            {
                SmartLogger.LogWarning($"[ZoneInstance.ApplyZoneEffects] Cannot apply effects: Zone inactive or data null", LogCategory.Zone);
                return;
            }

            // Cast zoneData to the concrete ZoneData type once
            ZoneData concreteZoneData = zoneData as ZoneData;
            if (concreteZoneData == null)
            {
                SmartLogger.LogError($"[ZoneInstance.ApplyZoneEffects] Could not cast zoneData to ZoneData for {DisplayName}", LogCategory.Zone);
                return;
            }

            // Get all units in the zone's area
            var unitsInZone = GetUnitsInZone();
            
            // Track which units were affected this turn
            _unitsAffectedLastTurn = new HashSet<IDokkaebiUnit>(_affectedUnits);
            _affectedUnits.Clear();

            // Apply effects to units in zone
            foreach (var unit in unitsInZone)
            {
                if (unit == null || !unit.IsAlive) continue;

                // Add to tracking
                _affectedUnits.Add(unit);

                // Apply zone-specific effects using the correct method with ZoneData
                ApplyEffectsToUnit(unit, concreteZoneData);
            }

            // Remove effects from units that left the zone
            foreach (var unit in _unitsAffectedLastTurn)
            {
                if (unit == null || !unit.IsAlive) continue;

                // If unit is no longer in the affected set, they've left the zone
                if (!_affectedUnits.Contains(unit))
                {
                    RemoveZoneEffectsFromUnit(unit);
                }
            }
        }

        /// <summary>
        /// Apply this zone's effects to a specific unit
        /// </summary>
        private void ApplyZoneEffectsToUnit(IDokkaebiUnit unit)
        {
            if (unit == null) return;

            SmartLogger.Log($"[ZoneInstance.ApplyZoneEffectsToUnit] Applying {zoneData.DisplayName} effects to {unit.DisplayName}", LogCategory.Zone);

            // Temporal Rift: Deal true damage
            if (zoneData.DisplayName.Contains("Temporal Rift"))
            {
                var dokkaebiUnit = unit as DokkaebiUnit;
                if (dokkaebiUnit != null && dokkaebiUnit.IsAlive)
                {
                    SmartLogger.Log($"[ZoneInstance] Applying Temporal Rift damage to {dokkaebiUnit.GetUnitName()}", LogCategory.Zone);
                    dokkaebiUnit.TakeDamage(2, DamageType.True);
                }
            }
            // Storm Surge: Movement buff + Accuracy debuff
            else if (zoneData.DisplayName.Contains("Storm"))
            {
                // Apply movement buff if not already present
                if (!unit.HasStatusEffect(StatusEffectType.Movement))
                {
                    var moveEffect = DataManager.Instance.GetStatusEffectData("PlusOneMovementStorm");
                    if (moveEffect != null)
                    {
                        StatusEffectSystem.ApplyStatusEffect(unit, moveEffect, -1); // -1 for permanent while in zone
                        SmartLogger.Log($"[ZoneInstance] Applied Storm movement buff to {unit.DisplayName}", LogCategory.Zone);
                    }
                }

                // Apply accuracy debuff if not already present
                if (!unit.HasStatusEffect(StatusEffectType.AccuracyDebuff))
                {
                    var accEffect = DataManager.Instance.GetStatusEffectData("MinusTenAccuracyStorm");
                    if (accEffect != null)
                    {
                        StatusEffectSystem.ApplyStatusEffect(unit, accEffect, -1);
                        SmartLogger.Log($"[ZoneInstance] Applied Storm accuracy debuff to {unit.DisplayName}", LogCategory.Zone);
                    }
                }
            }
            // Quake Zone: Movement debuff
            else if (zoneData.DisplayName.Contains("Quake"))
            {
                if (!unit.HasStatusEffect(StatusEffectType.Movement))
                {
                    var moveEffect = DataManager.Instance.GetStatusEffectData("MinusOneMovementQuake");
                    if (moveEffect != null)
                    {
                        StatusEffectSystem.ApplyStatusEffect(unit, moveEffect, -1);
                        SmartLogger.Log($"[ZoneInstance] Applied Quake movement debuff to {unit.DisplayName}", LogCategory.Zone);
                    }
                }
            }
            // Time Lock: Cooldown lock
            else if (zoneData.DisplayName.Contains("Time Lock"))
            {
                if (!unit.HasStatusEffect(StatusEffectType.CooldownLock))
                {
                    var lockEffect = DataManager.Instance.GetStatusEffectData("CooldownLocked");
                    if (lockEffect != null)
                    {
                        StatusEffectSystem.ApplyStatusEffect(unit, lockEffect, -1);
                        SmartLogger.Log($"[ZoneInstance] Applied Time Lock effect to {unit.DisplayName}", LogCategory.Zone);
                    }
                }
            }
        }

        /// <summary>
        /// Remove this zone's effects from a unit that has left the zone
        /// </summary>
        private void RemoveZoneEffectsFromUnit(IDokkaebiUnit unit)
        {
            if (unit == null) return;
            SmartLogger.Log($"[ZoneInstance.RemoveZoneEffectsFromUnit] Removing {zoneData.DisplayName} effects from {unit.DisplayName}", LogCategory.Zone);
            ZoneData concreteZoneData = zoneData as ZoneData;
            if (concreteZoneData != null && concreteZoneData.appliedStatusEffects != null && concreteZoneData.appliedStatusEffects.Count > 0)
            {
                foreach (var effect in concreteZoneData.appliedStatusEffects)
                {
                    if (effect != null)
                    {
                        StatusEffectSystem.RemoveStatusEffect(unit, effect.effectType);
                        SmartLogger.Log($"[ZoneInstance] Removed status effect '{effect.displayName}' from {unit.DisplayName}", LogCategory.Zone);
                    }
                }
            }
            // Legacy: applyStatusEffect
            else if (concreteZoneData != null && concreteZoneData.applyStatusEffect != null)
            {
                StatusEffectSystem.RemoveStatusEffect(unit, concreteZoneData.applyStatusEffect.effectType);
                SmartLogger.Log($"[ZoneInstance] Removed status effect '{concreteZoneData.applyStatusEffect.displayName}' from {unit.DisplayName} (legacy field)", LogCategory.Zone);
            }
        }

        /// <summary>
        /// Get all units currently within this zone's area (modified for 3x3 square)
        /// </summary>
        private List<IDokkaebiUnit> GetUnitsInZone()
        {
            var unitsInZone = new List<IDokkaebiUnit>();

            // Get GridManager and UnitManager instances
            GridManager gridManager = GridManager.Instance;
            UnitManager unitManager = UnitManager.Instance;

            if(gridManager == null || unitManager == null)
            {
                if(gridManager == null) SmartLogger.LogError("[ZoneInstance.GetUnitsInZone] GridManager.Instance is null!", LogCategory.Zone, this);
                if(unitManager == null) SmartLogger.LogError("[ZoneInstance.GetUnitsInZone] UnitManager.Instance is null!", LogCategory.Zone, this);
                return unitsInZone; // Return empty list if managers are null
            }

            // Calculate the 9 positions for a 3x3 square centered on the zone's position
            List<GridPosition> squarePositions = new List<GridPosition>();
            for (int xOffset = -1; xOffset <= 1; xOffset++)
            {
                for (int zOffset = -1; zOffset <= 1; zOffset++)
                {
                    GridPosition checkPos = new GridPosition(position.x + xOffset, position.z + zOffset);

                    // Ensure the position is valid within the grid bounds
                    if (gridManager.IsValidGridPosition(checkPos))
                    {
                        squarePositions.Add(checkPos);
                    }
                    else
                    {
                        SmartLogger.Log($"[ZoneInstance.GetUnitsInZone] Skipping invalid grid position: {checkPos}", LogCategory.Zone, this);
                    }
                }
            }
            // --- ADD LOGGING HERE ---
            SmartLogger.Log($"[ZoneInstance.GetUnitsInZone] Zone '{DisplayName}' at {position}. Checking {squarePositions.Count} positions in a 3x3 square:", LogCategory.Zone, this);
            foreach(var pos in squarePositions)
            {
                SmartLogger.Log($"  - Checking position: {pos}", LogCategory.Zone, this);
            }
            // --- END ADDING LOGGING ---
            // Check each calculated position for units
            foreach (var pos in squarePositions)
            {
                var unit = unitManager.GetUnitAtPosition(pos);
                if (unit != null)
                {
                    // --- ADD LOGGING HERE ---
                    SmartLogger.Log($"[ZoneInstance.GetUnitsInZone] Found unit {unit.GetUnitName()} at position {pos}. Unit's CurrentGridPosition: {unit.CurrentGridPosition}", LogCategory.Zone, this);
                    // --- END ADDING LOGGING ---
                    unitsInZone.Add(unit);
                }
                else
                {
                    // --- ADD LOGGING HERE ---
                    SmartLogger.Log($"[ZoneInstance.GetUnitsInZone] No unit found at position {pos}.", LogCategory.Zone, this);
                    // --- END ADDING LOGGING ---
                }
            }
            // --- ADD LOGGING HERE ---
            SmartLogger.Log($"[ZoneInstance.GetUnitsInZone] Returning list with {unitsInZone.Count} units.", LogCategory.Zone, this);
            // --- END ADDING LOGGING ---

            return unitsInZone;
        }

        /// <summary>
        /// Applies effects that trigger at the end of the turn resolution phase
        /// to all units within the zone's area of effect.
        /// </summary>
        public void ApplyTurnEndEffects()
        {
            // Log at the very beginning (already confirmed working)
            SmartLogger.Log($"[Instance:{GetInstanceID()}] ApplyTurnEndEffects called for Zone '{zoneData?.DisplayName ?? "NULL"}' (ID: {zoneData?.Id ?? "NULL ID"}). IsActive: {isActive}, RemainingDuration: {remainingDuration}", LogCategory.Zone, this);
            // --- Diagnostic log for zoneData.Id ---
            SmartLogger.Log($"[Instance:{GetInstanceID()}] ApplyTurnEndEffects: zoneData.Id = '{zoneData?.Id ?? "NULL"}'", LogCategory.Zone, this);
            // --- End diagnostic log ---

            // Initial checks for null zoneData or if it's not a concrete ZoneData
            if (zoneData == null || !(zoneData is ZoneData concreteZoneData))
            {
                SmartLogger.LogWarning($"[ZoneInstance.ApplyTurnEndEffects] Skipping zone (NULL zoneData or not concrete ZoneData). IsActive: {isActive}", LogCategory.Zone);
                return;
            }

            // --- Restructured Logic for Specific Zone Types ---
            // Handle Temporal Rift damage effects
            if (concreteZoneData.Id == "TemporalRift")
            {
                if (concreteZoneData.damagePerTurn > 0)
                {
                    SmartLogger.Log($"[ZoneInstance.ApplyTurnEndEffects] Zone {concreteZoneData.Id} ({Id}) applying turn end damage. Center: {this.position}, Radius: {this.Radius}, Damage: {concreteZoneData.damagePerTurn}, Type: {concreteZoneData.damageType}", LogCategory.Zone);

                    GridManager gridManager = GridManager.Instance;
                    UnitManager unitManager = UnitManager.Instance;
                    if (gridManager == null || unitManager == null)
                    {
                        SmartLogger.LogError("[ZoneInstance.ApplyTurnEndEffects] GridManager or UnitManager instance is null! Cannot apply damage.", LogCategory.Zone);
                        return;
                    }
                    List<GridPosition> positionsInRange = gridManager.GetGridPositionsInRange(this.position, this.Radius);
                    System.Text.StringBuilder posLog = Dokkaebi.Utilities.StringBuilderPool.Get();
                    posLog.Append($"[ApplyTurnEndEffects DEBUG] Zone '{concreteZoneData.Id ?? "NULL"}' ({Id}) at {this.position} with Radius {this.Radius}. GetGridPositionsInRange returned {positionsInRange.Count} positions: [");
                    foreach (var logPos in positionsInRange) { posLog.Append(logPos.ToString() + ", "); }
                    if (positionsInRange.Count > 0) posLog.Length -= 2;
                    posLog.Append("]");
                    Dokkaebi.Utilities.SmartLogger.Log(Dokkaebi.Utilities.StringBuilderPool.GetStringAndReturn(posLog), Dokkaebi.Utilities.LogCategory.Zone, this);

                    if (positionsInRange.Count == 0)
                    {
                        SmartLogger.LogWarning($"[ZoneInstance.ApplyTurnEndEffects] GetGridPositionsInRange returned 0 positions for radius {this.Radius} at {this.position}!", LogCategory.Zone);
                    }
                    else
                    {
                        int unitsDamaged = 0;
                        foreach (GridPosition pos in positionsInRange)
                        {
                            Dokkaebi.Utilities.SmartLogger.Log($"[ApplyTurnEndEffects DEBUG] Zone '{concreteZoneData.Id ?? "NULL"}' checking position: {pos}", Dokkaebi.Utilities.LogCategory.Zone, this);
                            IDokkaebiUnit unit = UnitManager.Instance?.GetUnitAtPosition(pos); // Use null-conditional for safety
                            if (unit != null && unit.IsAlive)
                            {
                                Dokkaebi.Utilities.SmartLogger.Log($"[ApplyTurnEndEffects DEBUG] Zone '{concreteZoneData.Id ?? "NULL"}' APPLYING {concreteZoneData.damagePerTurn} {concreteZoneData.damageType} damage to unit {unit.GetUnitName()} at {pos}.", Dokkaebi.Utilities.LogCategory.Zone, this);
                                (unit as IUnit)?.ModifyHealth(-(int)concreteZoneData.damagePerTurn, concreteZoneData.damageType);
                                unitsDamaged++;
                            }
                            else if (unit != null && !unit.IsAlive)
                            {
                                Dokkaebi.Utilities.SmartLogger.Log($"[ApplyTurnEndEffects DEBUG] Zone '{concreteZoneData.Id ?? "NULL"}' found unit {unit.GetUnitName()} at {pos} is not alive, skipping damage.", Dokkaebi.Utilities.LogCategory.Zone, this);
                            }
                            else
                            {
                                Dokkaebi.Utilities.SmartLogger.Log($"[ApplyTurnEndEffects DEBUG] Zone '{concreteZoneData.Id ?? "NULL"}' found no unit at {pos}.", Dokkaebi.Utilities.LogCategory.Zone, this);
                            }
                        }
                        SmartLogger.Log($"[ZoneInstance.ApplyTurnEndEffects] Finished processing zone {concreteZoneData.Id} ({Id}). Damaged {unitsDamaged} units out of {positionsInRange.Count} checked positions.", LogCategory.Zone);
                    }
                }
            }
            // Handle Storm Surge random push effect
            else if (concreteZoneData.Id == "StormSurge")
            {
                // --- Keep the existing Storm Surge Random Push Logic here ---
                SmartLogger.Log($"[Instance:{GetInstanceID()}] ApplyTurnEndEffects: Storm Surge specific logic block entered.", LogCategory.Zone, this);
                var unitsInZone = GetUnitsInZone();
                SmartLogger.Log($"[Instance:{GetInstanceID()}] Found {unitsInZone.Count} units in Storm Surge Zone to potentially push.", LogCategory.Zone, this);

                foreach (var unit in unitsInZone.ToList())
                {
                    if (unit == null || !unit.IsAlive)
                    {
                        if(unit == null) SmartLogger.LogWarning($"[Instance:{GetInstanceID()}] Skipping null unit in Storm Surge push list.", LogCategory.Zone, this);
                        else if (!unit.IsAlive) SmartLogger.LogWarning($"[Instance:{GetInstanceID()}] Skipping dead unit {unit.GetUnitName()} in Storm Surge push list.", LogCategory.Zone, this);
                        continue;
                    }
                    SmartLogger.Log($"[Instance:{GetInstanceID()}] Processing push for unit {unit.GetUnitName()} (ID: {unit.UnitId}) at {unit.CurrentGridPosition}", LogCategory.Zone, this);
                    bool isImmune = StatusEffectSystem.HasStatusEffect(unit, StatusEffectType.MovementImmunity);
                    SmartLogger.Log($"[Instance:{GetInstanceID()}] Unit {unit.GetUnitName()}: Is immune to push? {isImmune}", LogCategory.Zone, this);
                    if (isImmune)
                    {
                        SmartLogger.Log($"[Instance:{GetInstanceID()}] Unit {unit.GetUnitName()} is immune to movement effects, skipping push.", LogCategory.Zone, this);
                        continue;
                    }

                    var possibleDirections = DirectionExtensions.Cardinals;
                    int randomDirIndex = UnityEngine.Random.Range(0, possibleDirections.Length);
                    Direction randomDirection = possibleDirections[randomDirIndex];
                    GridPosition directionOffset = randomDirection.GetOffset();
                    GridPosition potentialDestination = new GridPosition(
                        unit.CurrentGridPosition.x + directionOffset.x,
                        unit.CurrentGridPosition.z + directionOffset.z
                    );
                    SmartLogger.Log($"[Instance:{GetInstanceID()}] Unit {unit.GetUnitName()}: Attempting to push in direction {randomDirection} to potential position {potentialDestination}", LogCategory.Zone, this);

                    bool isValidGridPos = GridManager.Instance != null && GridManager.Instance.IsValidGridPosition(potentialDestination);
                    bool isOccupied = GridManager.Instance != null && GridManager.Instance.IsTileOccupied(potentialDestination);
                    bool canPushToDestination = isValidGridPos && !isOccupied;

                    SmartLogger.Log($"[Instance:{GetInstanceID()}] Unit {unit.GetUnitName()}: Push destination {potentialDestination} validation - IsValidGridPos: {isValidGridPos}, IsOccupied: {isOccupied}, CanPushToDestination: {canPushToDestination}", LogCategory.Zone, this);
                    if (canPushToDestination)
                    {
                        SmartLogger.Log($"[Instance:{GetInstanceID()}] Unit {unit.GetUnitName()}: All checks passed. Calling SetGridPosition({potentialDestination}) to push.", LogCategory.Zone, this);
                        unit.SetGridPosition(potentialDestination);
                        SmartLogger.Log($"[Instance:{GetInstanceID()}] Unit {unit.GetUnitName()} successfully pushed to {unit.CurrentGridPosition} (Logical Position After SetGridPosition).", LogCategory.Zone, this);
                    }
                    else
                    {
                        SmartLogger.Log($"[Instance:{GetInstanceID()}] Unit {unit.GetUnitName()}: Cannot push to {potentialDestination}. Reason: IsValidGridPos={isValidGridPos}, IsOccupied={isOccupied}.", LogCategory.Zone, this);
                    }
                }
                SmartLogger.Log($"[Instance:{GetInstanceID()}] Storm Surge Zone end-of-turn push completed.", LogCategory.Zone, this);
                // --- End Storm Surge Random Push Logic ---
            }
            // *** Add the new Probability Field logic here ***
            else if (concreteZoneData.Id == "ProbabilityField")
            {
                SmartLogger.Log($"[Instance:{GetInstanceID()}] ApplyTurnEndEffects: Probability Field specific logic block entered.", LogCategory.Zone, this);

                // *** Cast concreteZoneData to ZoneData to access the specific fields ***
                if (concreteZoneData is ZoneData probabilityFieldZoneData)
                {
                    SmartLogger.Log($"[Instance:{GetInstanceID()}] Found concrete ZoneData for Probability Field. Application Chance: {probabilityFieldZoneData.ApplicationChance}, Crit Buff Effect: {(probabilityFieldZoneData.CritBuffEffectData != null ? probabilityFieldZoneData.CritBuffEffectData.displayName : "NULL")}", LogCategory.Zone, this);

                    if (probabilityFieldZoneData.CritBuffEffectData != null)
                    {
                        var unitsInZone = GetUnitsInZone();
                        SmartLogger.Log($"[Instance:{GetInstanceID()}] Found {unitsInZone.Count} units in Probability Field Zone.", LogCategory.Zone, this);

                        foreach (var unit in unitsInZone.ToList()) // Iterate over a copy
                        {
                            if (unit == null || !unit.IsAlive) continue;

                            // Perform the chance roll using the value from ZoneData
                            float randomValue = UnityEngine.Random.value;
                            SmartLogger.Log($"[Instance:{GetInstanceID()}] Probability Field Roll for {unit.GetUnitName()}: {randomValue:F2} vs Chance {probabilityFieldZoneData.ApplicationChance:F2}", LogCategory.Zone, this);

                            if (randomValue < probabilityFieldZoneData.ApplicationChance)
                            {
                                SmartLogger.Log($"[Instance:{GetInstanceID()}] Probability Field roll SUCCESS for {unit.GetUnitName()}. Applying Crit Buff.", LogCategory.Zone, this);

                                // Apply the crit buff status effect for 1 turn using the effect from ZoneData
                                StatusEffectSystem.ApplyStatusEffect(
                                    unit,
                                    probabilityFieldZoneData.CritBuffEffectData, // Use the effect data from ZoneData
                                    1, // Duration is 1 turn (reapplied each turn if roll is successful)
                                    ownerUnitId >= 0 ? UnitManager.Instance?.GetUnitById(ownerUnitId) : null // Pass owner unit
                                );
                                SmartLogger.Log($"[Instance:{GetInstanceID()}] Probability Field Crit Buff status effect applied to {unit.GetUnitName()} for 1 turn.", LogCategory.Zone, this);
                            }
                            else
                            {
                                SmartLogger.Log($"[Instance:{GetInstanceID()}] Probability Field roll FAILED for {unit.GetUnitName()}. No buff applied this turn.", LogCategory.Zone, this);
                            }
                        }
                    }
                    else
                    {
                        SmartLogger.LogWarning($"[Instance:{GetInstanceID()}] Probability Field ZoneData is missing the Crit Buff Effect Data reference!", LogCategory.Zone, this);
                    }
                }
                else
                {
                     SmartLogger.LogError($"[Instance:{GetInstanceID()}] ZoneData for ProbabilityField zone is not a concrete ZoneData! Type: {concreteZoneData?.GetType().Name ?? "NULL"}.", LogCategory.Zone, this);
                }

                SmartLogger.Log($"[Instance:{GetInstanceID()}] Probability Field Zone end-of-turn processing completed.", LogCategory.Zone, this);
            }
            // Add else if for other zone types with end-of-turn effects here
            // No default 'return' needed here anymore, as each specific zone type
            // has its own logic block. If a zone type doesn't have turn-end effects,
            // it simply won't match any of the conditions, and the method will complete
            // without doing anything for that zone.
        }

        /// <summary>
        /// Applies this zone's status effect to a single unit immediately if allegiance allows.
        /// Used for effects applied upon entering a zone.
        /// </summary>
        public void ApplyStatusEffectToUnitImmediate(IDokkaebiUnit targetUnit)
        {
            SmartLogger.Log($"[ZoneInstance.ApplyStatusEffectToUnitImmediate] BEGIN: Zone '{DisplayName}' attempting immediate apply to unit '{targetUnit?.GetUnitName() ?? "NULL"}'.", LogCategory.Zone, this);

            if (!IsActive || zoneData == null)
            {
                SmartLogger.Log($"Zone '{DisplayName}' cannot apply immediate effects: IsActive={IsActive}, zoneData={(zoneData == null ? "null" : "valid")}", LogCategory.Zone);
                return;
            }

            ZoneData concreteZoneData = zoneData as ZoneData;
            if (concreteZoneData == null)
            {
                SmartLogger.LogError($"[ZoneInstance.ApplyStatusEffectToUnitImmediate] Cannot apply immediate effects: concreteZoneData is null for {DisplayName}", LogCategory.Zone);
                return;
            }

            // --- Handle Storm Surge specific immediate effect (Movement Buff) ---
            if (concreteZoneData.Id == "StormSurge")
            {
                SmartLogger.Log($"[ZoneInstance.ApplyStatusEffectToUnitImmediate] Handling Storm Surge specific immediate effect for unit {targetUnit?.GetUnitName() ?? "NULL"}.", LogCategory.Zone, this);

                // Get the "PlusOneMovementStorm" Status Effect Data asset
                StatusEffectData movementBuffEffectData = DataManager.Instance?.GetStatusEffectData("PlusOneMovementStorm");

                if (movementBuffEffectData != null)
                {
                    // Check allegiance before applying
                    if (!(targetUnit is DokkaebiUnit dokkaebiUnit))
                    {
                        SmartLogger.LogWarning($"Zone '{DisplayName}': Target unit is not a DokkaebiUnit, cannot apply immediate effects", LogCategory.Zone);
                        return;
                    }

                    var ownerUnit = ownerUnitId >= 0 ? UnitManager.Instance?.GetUnitById(ownerUnitId) : null;

                    bool isAlly = ownerUnit != null && dokkaebiUnit.TeamId == ownerUnit.TeamId;
                    bool isSelf = ownerUnit != null && dokkaebiUnit.UnitId == ownerUnit.UnitId;
                    bool isEnemy = ownerUnit != null && dokkaebiUnit.TeamId != ownerUnit.TeamId;

                    bool shouldAffect = concreteZoneData.affects switch
                    {
                        AllegianceTarget.Any => true,
                        AllegianceTarget.AllyOnly => isAlly || isSelf,
                        AllegianceTarget.EnemyOnly => isEnemy,
                        _ => false
                    };

                    SmartLogger.Log($"[ZoneInstance.ApplyStatusEffectToUnitImmediate] Allegiance check for '{dokkaebiUnit.GetUnitName()}': Zone Affects='{concreteZoneData.affects}', IsAlly={isAlly}, IsSelf={isSelf}, IsEnemy={isEnemy}, shouldAffect={shouldAffect}", LogCategory.Zone, this);

                    if (shouldAffect)
                    {
                        SmartLogger.Log($"[ZoneInstance.ApplyStatusEffectToUnitImmediate] Applying Storm Surge movement buff '{movementBuffEffectData.displayName}' to unit {dokkaebiUnit.GetUnitName()} for {movementBuffEffectData.duration} turns.", LogCategory.Zone, this);
                        // Apply the status effect with its defined duration (2 turns)
                        StatusEffectSystem.ApplyStatusEffect(dokkaebiUnit, movementBuffEffectData, movementBuffEffectData.duration, ownerUnit);
                        SmartLogger.Log($"[ZoneInstance.ApplyStatusEffectToUnitImmediate] Applied Storm Surge movement buff to {dokkaebiUnit.GetUnitName()} immediately.", LogCategory.Zone, this);
                    }
                    else
                    {
                        SmartLogger.Log($"[ZoneInstance.ApplyStatusEffectToUnitImmediate] Skipping unit '{dokkaebiUnit.GetUnitName()}' due to allegiance mismatch for Storm Surge immediate apply.", LogCategory.Zone, this);
                    }
                }
                else
                {
                    SmartLogger.LogWarning($"[ZoneInstance.ApplyStatusEffectToUnitImmediate] Could not find PlusOneMovementStorm StatusEffectData for StormSurge zone.", LogCategory.Zone, this);
                }
                SmartLogger.Log($"[ZoneInstance.ApplyStatusEffectToUnitImmediate] END", LogCategory.Zone, this);
                return;
            }

            // --- Existing logic for general immediate status effect ---
            if (concreteZoneData.applyStatusEffect != null)
            {
                SmartLogger.Log($"[ZoneInstance.ApplyStatusEffectToUnitImmediate] Zone '{DisplayName}' has a general status effect '{concreteZoneData.applyStatusEffect.displayName}' configured for immediate application.", LogCategory.Zone, this);

                if (!(targetUnit is DokkaebiUnit dokkaebiUnit))
                {
                    SmartLogger.LogWarning($"Zone '{DisplayName}': Target unit is not a DokkaebiUnit, cannot apply immediate effects", LogCategory.Zone);
                    return;
                }

                var ownerUnit = ownerUnitId >= 0 ? UnitManager.Instance?.GetUnitById(ownerUnitId) : null;

                // Perform Allegiance Check (similar to ApplyEffectsToUnit)
                bool isAlly = ownerUnit != null && dokkaebiUnit.TeamId == ownerUnit.TeamId;
                bool isSelf = ownerUnit != null && dokkaebiUnit.UnitId == ownerUnit.UnitId;
                bool isEnemy = ownerUnit != null && dokkaebiUnit.TeamId != ownerUnit.TeamId;

                bool shouldAffect = concreteZoneData.affects switch
                {
                    AllegianceTarget.Any => true,
                    AllegianceTarget.AllyOnly => isAlly || isSelf,
                    AllegianceTarget.EnemyOnly => isEnemy,
                    _ => false
                };

                SmartLogger.Log($"[ZoneInstance.ApplyStatusEffectToUnitImmediate] Allegiance check for '{dokkaebiUnit.GetUnitName()}': Zone Affects='{concreteZoneData.affects}', IsAlly={isAlly}, IsSelf={isSelf}, IsEnemy={isEnemy}, shouldAffect={shouldAffect}", LogCategory.Zone, this);

                if (!shouldAffect)
                {
                    SmartLogger.Log($"[ZoneInstance.ApplyStatusEffectToUnitImmediate] Skipping unit '{dokkaebiUnit.GetUnitName()}' due to allegiance mismatch for immediate apply.", LogCategory.Zone, this);
                    return;
                }

                SmartLogger.Log($"[ZoneInstance.ApplyStatusEffectToUnitImmediate] Unit '{dokkaebiUnit.GetUnitName()}' should be affected immediately. Applying status effect '{concreteZoneData.applyStatusEffect.displayName}'.", LogCategory.Zone, this);

                // Apply the status effect using the zone's current remaining duration
                // Note: This will overwrite any existing stack of this non-stackable effect,
                // refreshing its duration to the zone's remaining time.
                StatusEffectSystem.ApplyStatusEffect(dokkaebiUnit, concreteZoneData.applyStatusEffect, remainingDuration, ownerUnit);

                SmartLogger.Log($"[ZoneInstance.ApplyStatusEffectToUnitImmediate] Applied status effect '{concreteZoneData.applyStatusEffect.displayName}' with duration {remainingDuration} to {dokkaebiUnit.GetUnitName()} immediately.", LogCategory.Zone, this);
            }
            else // --- No immediate status effect configured for this zone type ---
            {
                SmartLogger.Log($"[ZoneInstance.ApplyStatusEffectToUnitImmediate] Zone '{DisplayName}' has no immediate status effect configured.", LogCategory.Zone, this);
            }
        }
    }
} 

