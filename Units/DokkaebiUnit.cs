using UnityEngine;
using Dokkaebi.Grid;
using Dokkaebi.Pathfinding;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System;
using Dokkaebi.Interfaces;
using Dokkaebi.Utilities;
using Dokkaebi.Core.Data;
using Dokkaebi.Common;
using Dokkaebi.Core;
using Dokkaebi.Units;
using System.Text;
using Dokkaebi.Zones;

namespace Dokkaebi.Units
{
    /// <summary>
    /// Base class for all Dokkaebi unit types, enhanced with pathfinding integration
    /// </summary>
    [RequireComponent(typeof(DokkaebiMovementHandler))]
    [RequireComponent(typeof(BoxCollider))] // Ensure there's always a collider for raycasting
    public class DokkaebiUnit : MonoBehaviour, IDokkaebiUnit, IUnitEventHandler, IUnit
    {
        [Header("Unit Properties")]
        private string unitName = "Dokkaebi";
        private bool isPlayerUnit = true;
        private int unitId = -1;
        private int movementRange = 3;
        private int teamId = 0;
        
        [Header("Stats")]
        private int maxHealth = 100;
        private int currentHealth;
        private int maxAura = 10;
        private float currentMP = 0f;
        
        // Add unit-specific Aura fields
        [Header("Unit Aura")]
        [SerializeField] private int currentUnitAura;
        [SerializeField] private int maxUnitAura;
        
        // Unit properties
        private GridPosition gridPosition;
        private GridPosition positionAtTurnStart;
        
        // Component references
        private DokkaebiMovementHandler movementHandler;
        
        // Turn-based action tracking
        private bool hasPendingMovement = false;
        public bool HasPendingMovement => hasPendingMovement;
        private GridPosition? targetPosition;
        private bool hasMovedThisTurn = false;
        
        // Interaction state
        private bool isInteractable = true;
        
        // Movement Points
        [SerializeField] private int maxMP = 4;
        
        // Events
        public event Action<int, DamageType> OnDamageTaken;
        public event Action<int> OnHealingReceived;
        public event Action OnUnitDefeated;
        public event Action<IStatusEffectInstance> OnStatusEffectApplied;
        public event Action<IStatusEffectInstance> OnStatusEffectRemoved;
        public event Action<IDokkaebiUnit, GridPosition, GridPosition> OnUnitMoved;
        
        // Add unit-specific Aura event
        public event Action<int, int> OnUnitAuraChanged; // (oldAura, newAura)
        
        // State tracking
        private bool isDefeated = false;

        // Ability cooldowns
        private Dictionary<string, int> abilityCooldowns = new Dictionary<string, int>();

        // Unit data
        private OriginData origin;
        private CallingData calling;
        private List<AbilityData> abilities = new List<AbilityData>();
        private List<IStatusEffectInstance> statusEffects = new List<IStatusEffectInstance>();
        private UnitDefinitionData _unitDefinitionData;

        [Header("Visual Effects")]
        [SerializeField] private GameObject afterimagePrefab;
        [SerializeField] private GameObject karmicTetherVisualPrefab;
        private GameObject currentAfterimageInstance;
        private GameObject activeKarmicTetherVisual;
        private Dictionary<StatusEffectType, GameObject> activeEffectVisuals = new Dictionary<StatusEffectType, GameObject>();

        // Add event for reactive effects
        public event Action<IDokkaebiUnit, IDokkaebiUnit, int, DamageType> OnBeingAttacked;

        // Debug variable for tracking final world position after SetGridPosition
        public Vector3 Debug_FinalWorldPosition = Vector3.zero;

        #region IDokkaebiUnit Implementation
        public int UnitId => unitId;
        public GridPosition CurrentGridPosition => gridPosition;
        public bool IsAlive => !isDefeated;
        public int TeamId => teamId;
        public GameObject GameObject => gameObject;
        public bool IsPlayerControlled => isPlayerUnit;
        public int MovementRange => movementRange;
        public int CurrentHealth => currentHealth;
        public string GetUnitName() => unitName;
        public string UnitName => unitName;
        public string DisplayName => unitName ?? "Unknown";

        public List<IStatusEffectInstance> GetStatusEffects()
        {
            return statusEffects;
        }

        public void MoveToGridPosition(GridPosition newPosition)
        {
            if (movementHandler != null)
            {
                movementHandler.RequestPath(newPosition);
            }
            else
            {
                SetGridPosition(newPosition);
            }
        }

        public void AddStatusEffect(IStatusEffectInstance effect)
        {
            // Log every effect's type
            SmartLogger.Log($"[DokkaebiUnit.AddStatusEffect] Unit {DisplayName} (ID: {UnitId}) received effect of type: {effect?.StatusEffectType.ToString() ?? "NULL"}", LogCategory.Unit, this);

            // DEBUG LOG: Trace Movement effects (BurningStrideMovementBuff is of type Movement)
            if (effect?.StatusEffectType == StatusEffectType.Movement)
            {
                string effectName = effect?.Effect?.name ?? effect?.StatusEffectType.ToString() ?? "<null>";
                string unitName = this.DisplayName ?? this.name;
                int movementModifier = 0;
                if (effect?.Effect is StatusEffectData data)
                    movementModifier = data.movementRangeModifier;
                string stack = UnityEngine.StackTraceUtility.ExtractStackTrace();
                SmartLogger.Log($"[DEBUG][AddStatusEffect] Movement Effect: '{effectName}' (modifier: {movementModifier}) added to '{unitName}'.\nStackTrace:\n{stack}", LogCategory.Ability, this);
            }

            SmartLogger.Log($"[DokkaebiUnit.AddStatusEffect] ENTRY for unit {DisplayName} (ID: {UnitId}). Attempting to add effect: {effect?.StatusEffectType.ToString() ?? "NULL EFFECT"}", LogCategory.Unit, this);

            if (effect == null)
            {
                SmartLogger.LogWarning("[DokkaebiUnit.AddStatusEffect] Attempted to add null effect", LogCategory.Unit, this);
                return;
            }

            // --- ADD DUPLICATE CHECK START ---
            // Check if the effect is non-stackable and already exists
            if (effect.Effect != null && !effect.Effect.isStackable)
            {
                bool alreadyHasEffect = statusEffects.Any(e => e.StatusEffectType == effect.StatusEffectType);
                if (alreadyHasEffect)
                {
                    SmartLogger.LogWarning($"[DokkaebiUnit.AddStatusEffect] Unit {DisplayName} already has non-stackable effect {effect.StatusEffectType}. Skipping addition.", LogCategory.Unit, this);
                    return; // Do not add duplicate non-stackable effects
                }
            }
            // --- ADD DUPLICATE CHECK END ---

            statusEffects.Add(effect); // <--- Adds the effect to the list
            SmartLogger.Log($"[DokkaebiUnit.AddStatusEffect] Added effect {effect.StatusEffectType} to unit {DisplayName}. Total effects now: {statusEffects.Count}", LogCategory.Unit, this);
            RaiseStatusEffectApplied(effect); // <--- Raises the event
            SmartLogger.Log($"[DokkaebiUnit.AddStatusEffect] EXIT for unit {DisplayName}. Effect added and event raised.", LogCategory.Unit, this);
        }

        public void RemoveStatusEffect(IStatusEffectInstance effect)
        {
            // --- ADD LOG START ---
            SmartLogger.Log($"[DokkaebiUnit.RemoveStatusEffect] BEGIN: Called to remove effect: {(effect != null ? effect.StatusEffectType.ToString() : "<null effect>")} (Instance ID: {(effect != null ? effect.GetHashCode() : -1)}) from unit: {unitName} (ID: {unitId})", LogCategory.Unit, this);
            SmartLogger.Log($"[DokkaebiUnit.RemoveStatusEffect] Current statusEffects count BEFORE removal attempt: {statusEffects.Count}", LogCategory.Unit, this);
            // --- ADD LOG END ---

            if (effect == null)
            {
                SmartLogger.LogWarning("[DokkaebiUnit.RemoveStatusEffect] Attempted to remove null effect", LogCategory.Unit, this);
                return;
            }

            bool wasRemoved = statusEffects.Remove(effect);

            // --- ADD LOG START ---
            SmartLogger.Log($"[DokkaebiUnit.RemoveStatusEffect] Effect {(effect != null ? effect.StatusEffectType.ToString() : "<null effect>")} (Instance ID: {(effect != null ? effect.GetHashCode() : -1)}) was {(wasRemoved ? "successfully removed" : "not found")}. New statusEffects count: {statusEffects.Count}", LogCategory.Unit, this);
            // --- ADD LOG END ---

            if (wasRemoved)
            {
                // --- ADD LOG START ---
                SmartLogger.Log($"[DokkaebiUnit.RemoveStatusEffect] Effect was removed from list. About to call RaiseStatusEffectRemoved for {(effect != null ? effect.StatusEffectType.ToString() : "<null effect>")}.", LogCategory.Unit, this);
                // --- ADD LOG END ---
                RaiseStatusEffectRemoved(effect);
            }
            // --- ADD LOG START ---
            else
            {
                 SmartLogger.LogWarning($"[DokkaebiUnit.RemoveStatusEffect] Effect {(effect != null ? effect.StatusEffectType.ToString() : "<null effect>")} was not found in the statusEffects list.", LogCategory.Unit, this);
            }
            SmartLogger.Log($"[DokkaebiUnit.RemoveStatusEffect] END: Finished processing removal for {(effect != null ? effect.StatusEffectType.ToString() : "<null effect>")}.", LogCategory.Unit, this);
            // --- ADD LOG END ---
        }

        public bool HasStatusEffect(StatusEffectType effectType)
        {
            return statusEffects.Any(effect => effect.StatusEffectType == effectType);
        }

        /// <summary>
        /// Records the unit's current position at the start of a turn for rewind ability.
        /// </summary>
        public void RecordPositionAtTurnStart()
        {
            positionAtTurnStart = gridPosition;
            SmartLogger.Log($"[DokkaebiUnit.RecordPositionAtTurnStart] Unit {unitName} (ID: {unitId}) recorded position {positionAtTurnStart}", LogCategory.Unit, this);
        }

        /// <summary>
        /// Gets the unit's recorded position from the start of the current turn.
        /// </summary>
        /// <returns>The GridPosition where the unit was at the start of the current turn.</returns>
        public GridPosition GetPositionAtTurnStart()
        {
            return positionAtTurnStart;
        }
        #endregion

        #region IUnit Implementation
        public int MaxHealth => maxHealth;
        public GridPosition CurrentPosition => gridPosition;

        public void ModifyHealth(int amount, DamageType damageType = DamageType.Normal)
        {
            if (amount < 0)
            {
                TakeDamage(-amount, damageType);
            }
            else if (amount > 0)
            {
                Heal(amount);
            }
        }
        #endregion

        private void Awake()
        {
            SmartLogger.Log($"[DokkaebiUnit] Awake called on {gameObject.name}", LogCategory.Unit, this);
            
            // Ensure proper layer setup
            if (gameObject.layer != LayerMask.NameToLayer("Unit"))
            {
                gameObject.layer = LayerMask.NameToLayer("Unit");
                SmartLogger.Log($"[DokkaebiUnit] Set layer to Unit for {gameObject.name}", LogCategory.Unit, this);
            }
            
            // Validate collider setup
            var collider = GetComponent<Collider>();
            if (collider == null)
            {
                SmartLogger.LogError($"[DokkaebiUnit] No Collider found on {gameObject.name}. Adding BoxCollider.", LogCategory.Unit, this);
                collider = gameObject.AddComponent<BoxCollider>();
            }
            
            // Ensure collider is enabled
            if (!collider.enabled)
            {
                collider.enabled = true;
                SmartLogger.Log($"[DokkaebiUnit] Enabled collider on {gameObject.name}", LogCategory.Unit, this);
            }
            
            // Initialize health
            currentHealth = maxHealth;
            SmartLogger.Log($"[DokkaebiUnit] Initialized health - Max: {maxHealth}, Current: {currentHealth}", LogCategory.Unit, this);
            
            // Initialize unit Aura
            SmartLogger.Log($"[DokkaebiUnit] Initialized unit Aura - Max: {maxUnitAura}, Current: {currentUnitAura}", LogCategory.Unit, this);
            
            // Get or add movement handler
            movementHandler = GetComponent<DokkaebiMovementHandler>();
            if (movementHandler == null)
            {
                movementHandler = gameObject.AddComponent<DokkaebiMovementHandler>();
                SmartLogger.Log("[DokkaebiUnit] Added DokkaebiMovementHandler component", LogCategory.Unit, this);
            }
        }
        
        private void Start()
        {
            SmartLogger.Log($"[DokkaebiUnit] Start called on {gameObject.name}", LogCategory.Unit, this);
            
            // Register with grid manager
            if (GridManager.Instance != null)
            {
                GridManager.Instance.SetTileOccupant(gridPosition, this);
                SmartLogger.Log($"[DokkaebiUnit] Registered with GridManager at position {gridPosition}", LogCategory.Unit, this);
            }
            // Subscribe to status effect applied event
            OnStatusEffectApplied += HandleStatusEffectAdded;
            // Subscribe to movement modifier handlers
            OnStatusEffectApplied += HandleStatusEffectAddedModifier;
            OnStatusEffectRemoved += HandleStatusEffectRemovedModifier;
            // Subscribe to being attacked event for reactive abilities
            OnBeingAttacked += HandleBeingAttacked;
        }
        
        private void OnDestroy()
        {
            SmartLogger.Log($"[DokkaebiUnit.OnDestroy] Unit {unitName} (ID: {unitId}) is being destroyed", LogCategory.Unit, this);
            
            // Clear from GridManager
            if (GridManager.Instance != null)
            {
                GridManager.Instance.ClearUnitFromPreviousTile(this);
                SmartLogger.Log($"[DokkaebiUnit.OnDestroy] Clearing unit from previous tile in GridManager for unit {unitName}", LogCategory.Unit, this);
            }

            // Clean up afterimage
            HideAfterimage();
            // Unsubscribe from status effect applied event
            OnStatusEffectApplied -= HandleStatusEffectAdded;
            // Unsubscribe from movement modifier handlers
            OnStatusEffectApplied -= HandleStatusEffectAddedModifier;
            OnStatusEffectRemoved -= HandleStatusEffectRemovedModifier;
        }

        private void OnDisable()
        {
            SmartLogger.Log($"[DokkaebiUnit.OnDisable] Unit {unitName} (ID: {unitId}) is being disabled", LogCategory.Unit, this);
            // Clean up afterimage when disabled
            HideAfterimage();
        }

        private void OnEnable()
        {
            SmartLogger.Log($"[DokkaebiUnit.OnEnable] Unit {unitName} (ID: {unitId}) is being enabled", LogCategory.Unit, this);
        }

        #region Configuration Methods
        public void SetUnitId(int id) {
            unitId = id;
            SmartLogger.Log($"[DokkaebiUnit] SetUnitId called for {gameObject.name} (InstanceID: {gameObject.GetInstanceID()}). New ID: {id}", LogCategory.Unit, this);
        }
        public void SetUnitName(string name) => unitName = name;
        public void SetIsPlayerUnit(bool isPlayer) => isPlayerUnit = isPlayer;
        public void SetMovementRange(int range) => movementRange = range;
        public void SetMaxHealth(int max) => maxHealth = max;
        public void SetCurrentHealth(int current) => currentHealth = current;
        public void SetMaxAura(int max) => maxAura = max;

        // Add unit-specific Aura configuration methods
        public void SetMaxUnitAura(int max)
        {
            maxUnitAura = max;
            // Clamp current unit aura if it exceeds new max
            if (currentUnitAura > maxUnitAura)
            {
                ModifyUnitAura(maxUnitAura - currentUnitAura);
            }
        }

        public void SetCurrentUnitAura(int current)
        {
            int oldAura = currentUnitAura;
            currentUnitAura = Mathf.Clamp(current, 0, maxUnitAura);
            if (oldAura != currentUnitAura)
            {
                OnUnitAuraChanged?.Invoke(oldAura, currentUnitAura);
            }
        }
        #endregion
        
        // Public API
        public bool IsPlayer() => isPlayerUnit;
        public int GetUnitId() => unitId;
        public int GetMovementRange() => movementRange;
        public int GetCurrentHealth() => currentHealth;
        public int GetMaxHealth() => maxHealth;
        public int GetCurrentAura() => AuraManager.Instance.GetCurrentAura(isPlayerUnit);
        public int GetMaxAura() => maxAura;
        public float GetCurrentMP() => currentMP;
        public int GetMaxMP() => maxMP;

        // Add unit-specific Aura getters
        public int GetCurrentUnitAura() => currentUnitAura;
        public int GetMaxUnitAura() => maxUnitAura;
        
        public GridPosition GetGridPosition() => gridPosition;
        
        /// <summary>
        /// Set the unit's grid position and update its world position
        /// </summary>
        public void SetGridPosition(GridPosition position)
        {
            SmartLogger.Log($"[DokkaebiUnit.SetGridPosition] ENTRY - Input Position: {position}, Current GridPosition: {gridPosition}", LogCategory.Unit, this);
            if (position == gridPosition) 
            {
                SmartLogger.Log($"[DokkaebiUnit.SetGridPosition] Early return - position matches current gridPosition: {position}", LogCategory.Unit, this);
                return;
            }
            var oldPosition = gridPosition;
            SmartLogger.Log($"[DokkaebiUnit.SetGridPosition] Saved oldPosition: {oldPosition}", LogCategory.Unit, this);
            gridPosition = position;
            Vector3 worldPos = GridManager.Instance.GridToWorld(position);
            SmartLogger.Log($"[DokkaebiUnit.SetGridPosition] About to assign transform.position. Passing position: {position} to GridManager.Instance.GridToWorld, Resulting WorldPos: {worldPos}", LogCategory.Unit, this);
            transform.position = worldPos;
            Debug_FinalWorldPosition = transform.position; // Set the debug variable here
            SmartLogger.Log($"[DokkaebiUnit.SetGridPosition DEBUG] Unit {unitName} (ID: {unitId}) set Debug_FinalWorldPosition to: {Debug_FinalWorldPosition}", LogCategory.Unit, this);
            SmartLogger.Log($"[DokkaebiUnit.SetGridPosition] Unit {unitId} transform.position FINAL after setting: {transform.position}", LogCategory.Unit, this);
            SmartLogger.Log($"[DokkaebiUnit.SetGridPosition] AFTER assignment, transform.position: {transform.position}", LogCategory.Unit, this);
            if (GridManager.Instance != null)
            {
                GridManager.Instance.ClearUnitFromPreviousTile(this);
                GridManager.Instance.SetTileOccupant(position, this);
                SmartLogger.Log($"[DokkaebiUnit.SetGridPosition] Updated GridManager tracking for unit {unitName} from {oldPosition} to {gridPosition}", LogCategory.Unit, this);
            }
            else
            {
                SmartLogger.LogError("[DokkaebiUnit.SetGridPosition] GridManager.Instance is null!", LogCategory.Unit, this);
            }

            // --- CHECK FOR ZONE ENTRY AND APPLY IMMEDIATE EFFECTS ---
            SmartLogger.Log($"[DokkaebiUnit.SetGridPosition] Checking for zone entry/exit for unit {unitName} at new position {gridPosition} (was at {oldPosition})", LogCategory.Zone, this);
            ZoneManager zoneManager = ZoneManager.Instance;
            if (zoneManager != null)
            {
                var allActiveZones = zoneManager.GetAllZones().Select(z => z.GetComponent<ZoneInstance>()).Where(zi => zi != null && zi.IsActive).ToList();
                SmartLogger.Log($"[DokkaebiUnit.SetGridPosition] Found {allActiveZones.Count} active ZoneInstances to check for entry.", LogCategory.Zone, this);
                foreach (var zoneInstance in allActiveZones)
                {
                    if (zoneInstance != null && zoneInstance.IsActive)
                    {
                        SmartLogger.Log($"[DokkaebiUnit.SetGridPosition] Checking entry/exit status for zone '{zoneInstance.DisplayName}' at {zoneInstance.Position} (Radius: {zoneInstance.Radius}) for unit {unitName}. OldPos: {oldPosition}, NewPos: {gridPosition}", LogCategory.Zone, this);
                        bool wasOutsideZone = !zoneInstance.ContainsPosition(oldPosition);
                        bool isInsideZoneNow = zoneInstance.ContainsPosition(gridPosition);
                        SmartLogger.Log($"[DokkaebiUnit.SetGridPosition] Zone '{zoneInstance.DisplayName}': Unit at {oldPosition} was Outside: {wasOutsideZone}. Unit at {gridPosition} is Inside Now: {isInsideZoneNow}. Entry Condition (wasOutside && isInside): {wasOutsideZone && isInsideZoneNow}", LogCategory.Zone, this);
                        if (wasOutsideZone && isInsideZoneNow)
                        {
                            SmartLogger.Log($"[DokkaebiUnit.SetGridPosition] Unit {unitName} ENTERED zone '{zoneInstance.DisplayName}' at {gridPosition}.", LogCategory.Zone, this);
                            zoneInstance.ApplyStatusEffectToUnitImmediate(this);
                        }
                        else if (!wasOutsideZone && isInsideZoneNow)
                        {
                            SmartLogger.Log($"[DokkaebiUnit.SetGridPosition] Unit {unitName} is still inside zone '{zoneInstance.DisplayName}'. No immediate entry effect.", LogCategory.Zone, this);
                        }
                        else if (wasOutsideZone && !isInsideZoneNow)
                        {
                            SmartLogger.Log($"[DokkaebiUnit.SetGridPosition] Unit {unitName} moved from outside zone '{zoneInstance.DisplayName}' to outside or bordering.", LogCategory.Zone, this);
                        }
                        // The case where isInsideZoneNow is false and wasOutsideZone is false (unit left the zone)
                        // is handled by the end-of-turn processing which removes effects from units that were
                        // affected last turn but are not in the zone this turn.
                    }
                }
            }
            else
            {
                SmartLogger.LogError("[DokkaebiUnit.SetGridPosition] ZoneManager.Instance is null!", LogCategory.Zone, this);
            }
            // --- END CHECK FOR ZONE ENTRY ---

            OnUnitMoved?.Invoke(this, oldPosition, gridPosition);
            SmartLogger.Log($"[DokkaebiUnit.SetGridPosition] EXIT - Final Transform Position: {transform.position}, Final GridPosition: {gridPosition}", LogCategory.Unit, this);
        }

        

        /// <summary>
        /// Update the unit's grid position based on its current world position
        /// </summary>
        public void UpdateGridPosition(GridPosition newPosition)
        {
            if (newPosition == gridPosition) return;
            
            // Update grid position
            var oldPosition = gridPosition;
            gridPosition = newPosition;
            
            // Update grid manager
            if (GridManager.Instance != null)
            {
                GridManager.Instance.ClearUnitFromPreviousTile(this);
                GridManager.Instance.SetTileOccupant(newPosition, this);
            }
        }

        /// <summary>
        /// Take damage from any source
        /// </summary>
        public void TakeDamage(int amount, DamageType damageType, IDokkaebiUnit attacker = null, bool isReactiveDamage = false, bool isSplitDamage = false)
        {
            if (amount <= 0)
            {
                SmartLogger.Log($"[DokkaebiUnit.TakeDamage] Ignoring non-positive damage amount: {amount}", LogCategory.Ability);
                return;
            }

            SmartLogger.Log($"[DokkaebiUnit.TakeDamage START] Unit: {unitName}, Attacker: {attacker?.GetUnitName() ?? "None"}, Base Damage: {amount}, Type: {damageType}, Current Health: {currentHealth}/{maxHealth}, IsReactive: {isReactiveDamage}, IsSplitDamage: {isSplitDamage}", LogCategory.Ability);

            // Trigger OnBeingAttacked event for reactive effects BEFORE damage calculation
            // Only trigger if this is not already a reactive damage instance to prevent infinite loops
            if (!isReactiveDamage)
            {
                OnBeingAttacked?.Invoke(this, attacker, amount, damageType);
            }

            // Get and log all active status effects that might modify damage
            var activeEffects = GetStatusEffects();
            if (activeEffects.Any())
            {
                SmartLogger.Log($"[DokkaebiUnit.TakeDamage] Active status effects: {string.Join(", ", activeEffects.Select(e => e.StatusEffectType.ToString()))}", LogCategory.Ability);
            }

            // Apply damage modifiers based on status effects
            float damageMultiplier = StatusEffectSystem.GetStatModifier(this, UnitAttributeType.DamageTaken);
            SmartLogger.Log($"[DokkaebiUnit.TakeDamage] Status effect damage multiplier: {damageMultiplier:F2}", LogCategory.Ability);

            // Calculate modified damage
            int modifiedDamage = Mathf.RoundToInt(amount * damageMultiplier);
            SmartLogger.Log($"[DokkaebiUnit.TakeDamage] Final damage after modifiers: {modifiedDamage} (Base: {amount} x Multiplier: {damageMultiplier:F2})", LogCategory.Ability);

            // Apply damage
            int oldHealth = currentHealth;
            currentHealth = Mathf.Max(0, currentHealth - modifiedDamage);
            int actualDamage = oldHealth - currentHealth;

            // Log the actual damage taken and new health state
            SmartLogger.Log($"[DokkaebiUnit.TakeDamage] Actual damage taken: {actualDamage}, Health reduced from {oldHealth} to {currentHealth}", LogCategory.Ability);

            // --- FateLinked Damage Splitting ---
            if (!isSplitDamage)
            {
                var fateLinkedEffect = GetStatusEffects().FirstOrDefault(e => e.StatusEffectType == StatusEffectType.FateLinked) as StatusEffectInstance;
                if (fateLinkedEffect != null && fateLinkedEffect.linkedUnitId != -1)
                {
                    SmartLogger.Log($"[DokkaebiUnit.TakeDamage] Unit {DisplayName} (ID: {UnitId}) is FateLinked. Splitting {actualDamage} damage with linked unit {fateLinkedEffect.linkedUnitId}.", LogCategory.Combat);
                    var linkedUnit = UnitManager.Instance?.GetUnitById(fateLinkedEffect.linkedUnitId);
                    if (linkedUnit != null && linkedUnit.IsAlive && linkedUnit != this)
                    {
                        int damageToLinked = Mathf.FloorToInt(actualDamage / 2f);
                        int damageToSelf = actualDamage - damageToLinked;
                        SmartLogger.Log($"[DokkaebiUnit.TakeDamage] Splitting: Self takes {damageToSelf}, Linked ({linkedUnit.GetUnitName()}) takes {damageToLinked}.", LogCategory.Combat);
                        if (damageToLinked > 0)
                        {
                            SmartLogger.Log($"[DokkaebiUnit.TakeDamage] Applying split damage of {damageToLinked} ({damageType}) to linked unit {linkedUnit.GetUnitName()} (ID: {linkedUnit.UnitId}).", LogCategory.Combat);
                            linkedUnit.TakeDamage(damageToLinked, damageType, attacker, isReactiveDamage, true);
                        }
                        else
                        {
                            SmartLogger.Log($"[DokkaebiUnit.TakeDamage] Calculated damage to linked unit is 0. No split damage applied.", LogCategory.Combat);
                        }
                    }
                    else
                    {
                        SmartLogger.Log($"[DokkaebiUnit.TakeDamage] Unit {DisplayName} is NOT FateLinked or linkedUnitId invalid. No damage splitting performed.", LogCategory.Combat);
                    }
                }
            }
            // --- End FateLinked Damage Splitting ---

            // Invoke damage taken event
            OnDamageTaken?.Invoke(modifiedDamage, damageType);
            SmartLogger.Log($"[DokkaebiUnit.TakeDamage] OnDamageTaken event invoked with damage: {modifiedDamage}, type: {damageType}", LogCategory.Ability);

            // Check for defeat
            if (currentHealth <= 0 && !isDefeated)
            {
                isDefeated = true;
                SmartLogger.Log($"[DokkaebiUnit.TakeDamage] Unit {unitName} has been defeated!", LogCategory.Ability);
                OnUnitDefeated?.Invoke();
            }

            SmartLogger.Log($"[DokkaebiUnit.TakeDamage END] Unit: {unitName}, Final Health: {currentHealth}/{maxHealth}, Defeated: {isDefeated}", LogCategory.Ability);
        }

        /// <summary>
        /// Heal the unit
        /// </summary>
        public void Heal(int amount)
        {
            if (amount <= 0)
            {
                SmartLogger.Log($"[DokkaebiUnit.Heal] Ignoring non-positive heal amount: {amount}", LogCategory.Ability);
                return;
            }

            SmartLogger.Log($"[DokkaebiUnit.Heal START] Unit: {unitName}, Base Heal: {amount}, Current Health: {currentHealth}/{maxHealth}", LogCategory.Ability);

            // Get and log all active status effects that might modify healing
            var activeEffects = GetStatusEffects();
            if (activeEffects.Any())
            {
                SmartLogger.Log($"[DokkaebiUnit.Heal] Active status effects: {string.Join(", ", activeEffects.Select(e => e.StatusEffectType.ToString()))}", LogCategory.Ability);
            }

            // Apply healing modifiers based on status effects
            float healingMultiplier = StatusEffectSystem.GetStatModifier(this, UnitAttributeType.HealingReceived);
            SmartLogger.Log($"[DokkaebiUnit.Heal] Status effect healing multiplier: {healingMultiplier:F2}", LogCategory.Ability);

            // Calculate modified healing
            int modifiedHealing = Mathf.RoundToInt(amount * healingMultiplier);
            SmartLogger.Log($"[DokkaebiUnit.Heal] Final healing after modifiers: {modifiedHealing} (Base: {amount} x Multiplier: {healingMultiplier:F2})", LogCategory.Ability);

            // Apply healing
            int oldHealth = currentHealth;
            currentHealth = Mathf.Min(maxHealth, currentHealth + modifiedHealing);
            int actualHealing = currentHealth - oldHealth;

            // Log the actual healing received and new health state
            SmartLogger.Log($"[DokkaebiUnit.Heal] Actual healing received: {actualHealing}, Health increased from {oldHealth} to {currentHealth}", LogCategory.Ability);

            // Invoke healing received event
            OnHealingReceived?.Invoke(actualHealing);
            SmartLogger.Log($"[DokkaebiUnit.Heal] OnHealingReceived event invoked with healing: {actualHealing}", LogCategory.Ability);

            SmartLogger.Log($"[DokkaebiUnit.Heal END] Unit: {unitName}, Final Health: {currentHealth}/{maxHealth}", LogCategory.Ability);
        }

        /// <summary>
        /// Apply a status effect to the unit for the specified duration
        /// </summary>
        public void ApplyStatusEffect(StatusEffectData effect, int duration)
        {
            StatusEffectSystem.ApplyStatusEffect(this, effect, duration);
        }

        /// <summary>
        /// Reduce all ability cooldowns by 1 at the start of a new turn
        /// </summary>
        public void ReduceCooldowns()
        {
            SmartLogger.Log($"[ReduceCooldowns] Starting cooldown reduction for Unit: {unitName}", LogCategory.Unit, this);
            
            var cooldownKeys = abilityCooldowns.Keys.ToList();
            foreach (var abilityId in cooldownKeys)
            {
                if (abilityCooldowns[abilityId] > 0)
                {
                    int oldCooldown = abilityCooldowns[abilityId];
                    abilityCooldowns[abilityId]--;
                    
                    SmartLogger.Log($"[ReduceCooldowns] Unit: {unitName}, Ability: {abilityId}, Old Cooldown: {oldCooldown}, New Cooldown: {abilityCooldowns[abilityId]}", LogCategory.Unit, this);
                    
                    if (abilityCooldowns[abilityId] <= 0)
                    {
                        abilityCooldowns.Remove(abilityId);
                        SmartLogger.Log($"[ReduceCooldowns] Removed completed cooldown for Ability: {abilityId} on Unit: {unitName}", LogCategory.Unit, this);
                    }
                }
            }
            
            SmartLogger.Log($"[ReduceCooldowns] Completed cooldown reduction for Unit: {unitName}", LogCategory.Unit, this);
        }

        /// <summary>
        /// Set a cooldown for a specific ability
        /// </summary>
        public void SetAbilityCooldown(string abilityId, int cooldown)
        {
            if (cooldown <= 0)
            {
                abilityCooldowns.Remove(abilityId);
            }
            else
            {
                abilityCooldowns[abilityId] = cooldown;
            }
        }

        /// <summary>
        /// Set a cooldown for an ability by its type
        /// </summary>
        public void SetAbilityCooldown(AbilityType abilityType, int cooldown)
        {
            var ability = abilities.FirstOrDefault(a => a.abilityType == abilityType);
            if (ability != null)
            {
                SetAbilityCooldown(ability.abilityId, cooldown);
            }
        }

        /// <summary>
        /// Get the remaining cooldown for a specific ability
        /// </summary>
        public int GetRemainingCooldown(string abilityId)
        {
            SmartLogger.Log($"[GetRemainingCooldown] Checking cooldown for ability {abilityId}", LogCategory.Unit, this);
            return abilityCooldowns.TryGetValue(abilityId, out int cooldown) ? cooldown : 0;
        }

        /// <summary>
        /// Check if an ability is on cooldown
        /// </summary>
        public bool IsOnCooldown(string abilityId)
        {
            int cooldown = GetRemainingCooldown(abilityId);
            SmartLogger.Log($"[IsOnCooldown] Ability {abilityId} cooldown check result: {cooldown > 0} (Remaining: {cooldown})", LogCategory.Unit, this);
            return cooldown > 0;
        }

        // Legacy methods for backward compatibility - these should be phased out
        public int GetRemainingCooldown(AbilityType type)
        {
            var ability = abilities.FirstOrDefault(a => a.abilityType == type);
            if (ability != null)
            {
                return GetRemainingCooldown(ability.abilityId);
            }
            return 0;
        }

        public bool IsOnCooldown(AbilityType type)
        {
            var ability = abilities.FirstOrDefault(a => a.abilityType == type);
            if (ability != null)
            {
                return IsOnCooldown(ability.abilityId);
            }
            return false;
        }

        /// <summary>
        /// Checks if the unit can use the specified ability based on aura cost and cooldown
        /// </summary>
        public bool CanUseAbility(AbilityData abilityData)
        {
            if (abilityData == null) return false;
            
            bool hasEnoughAura = HasEnoughUnitAura(abilityData.auraCost);
            bool isOffCooldown = !IsOnCooldown(abilityData.abilityId);
            
            SmartLogger.Log($"[CanUseAbility] LOG_CHECK: Unit: {unitName}, Ability: {abilityData?.displayName}, HasEnoughAura: {hasEnoughAura}, IsOffCooldown: {isOffCooldown}", LogCategory.Ability, this.gameObject);
            
            return hasEnoughAura && isOffCooldown;
        }

        /// <summary>
        /// Resets the unit's Aura to its maximum value
        /// </summary>
        public void ResetUnitAura()
        {
            if (currentUnitAura != maxUnitAura)
            {
                ModifyUnitAura(maxUnitAura - currentUnitAura);
            }
        }

        /// <summary>
        /// Sets the unit's current aura directly, typically from authoritative state.
        /// </summary>
        /// <param name="targetAmount">The desired current aura amount.</param>
        public void SetCurrentAura(int targetAmount)
        {
            // Ensure AuraManager is available
            if (AuraManager.Instance == null)
            {
                SmartLogger.LogError($"Cannot SetCurrentAura for {GetUnitName()}: AuraManager instance is null.", LogCategory.Ability);
                return;
            }

            int currentAura = GetCurrentAura(); // Uses the existing getter that likely calls AuraManager
            int difference = targetAmount - currentAura;

            // Use the existing ModifyAura method which handles clamping and events via AuraManager
            ModifyAura(difference);

            // Optional: Add a log to confirm
            SmartLogger.Log($"{GetUnitName()} current Aura set to {targetAmount} (from state sync). Previous: {currentAura}, Diff: {difference}", LogCategory.StateSync);
        }

        /// <summary>
        /// Internally invokes the OnStatusEffectApplied event.
        /// Called by external systems after an effect is successfully added.
        /// </summary>
        public void RaiseStatusEffectApplied(IStatusEffectInstance instance)
        {
            // LOG: At the very beginning
            SmartLogger.Log($"[DokkaebiUnit.RaiseStatusEffectApplied] UNIT {DisplayName} (ID: {UnitId}) - Raising event for Effect Type: {instance?.StatusEffectType}, Instance ID: {instance?.GetHashCode()}. Call Stack:\n{UnityEngine.StackTraceUtility.ExtractStackTrace()}", LogCategory.Unit, this);

            if (instance is StatusEffectInstance effectInstance)
            {
                SmartLogger.Log($"[DokkaebiUnit.RaiseStatusEffectApplied] About to invoke OnStatusEffectApplied for effect: {effectInstance.StatusEffectType} on unit: {DisplayName}", LogCategory.Unit, this);
                
                int subscriberCount = (OnStatusEffectApplied != null) ? OnStatusEffectApplied.GetInvocationList().Length : 0;
                SmartLogger.Log($"[DokkaebiUnit.RaiseStatusEffectApplied] Number of subscribers to OnStatusEffectApplied: {subscriberCount}", LogCategory.Unit, this);
                
                OnStatusEffectApplied?.Invoke(instance);
                
                SmartLogger.Log($"[DokkaebiUnit.RaiseStatusEffectApplied] OnStatusEffectApplied event invoked for effect: {effectInstance.StatusEffectType}", LogCategory.Unit, this);
            }
            else
            {
                SmartLogger.LogWarning($"[DokkaebiUnit.RaiseStatusEffectApplied] Invalid effect instance type for unit: {DisplayName}", LogCategory.Unit, this);
            }
        }

        /// <summary>
        /// Internally invokes the OnStatusEffectRemoved event.
        /// Called by external systems after an effect is successfully removed.
        /// </summary>
        public void RaiseStatusEffectRemoved(IStatusEffectInstance instance)
        {
            // --- ADDED LOG: Trace removed effect ---
            SmartLogger.Log($"[DokkaebiUnit.RaiseStatusEffectRemoved] BEGIN: Handling removed effect: {instance?.StatusEffectType.ToString() ?? "<null effect>"} (Instance ID: {instance?.GetHashCode() ?? -1}) from unit: {DisplayName} (ID: {UnitId})", LogCategory.Unit, this);
            // --- END ADDED LOG ---
            // Karmic Tether visual cleanup
            if (instance != null && instance.StatusEffectType == StatusEffectType.FateLinked)
            {
                // --- ADDED LOGS FOR FATE LINKED CLEANUP ---
                SmartLogger.Log($"[DokkaebiUnit.RaiseStatusEffectRemoved] Detected FateLinked effect removal. Checking activeKarmicTetherVisual (is null: {activeKarmicTetherVisual == null}).", LogCategory.Unit, this);
                // --- END ADDED LOGS ---

                if (activeKarmicTetherVisual != null)
                {
                    // --- ADDED LOG BEFORE DESTROY ---
                    SmartLogger.Log($"[DokkaebiUnit.RaiseStatusEffectRemoved] Destroying activeKarmicTetherVisual GameObject (Instance ID: {activeKarmicTetherVisual.GetInstanceID()}) for unit {DisplayName}.", LogCategory.Unit, this);
                    // --- END ADDED LOG ---
                    UnityEngine.GameObject.Destroy(activeKarmicTetherVisual);
                    activeKarmicTetherVisual = null;
                    SmartLogger.Log($"[DokkaebiUnit.RaiseStatusEffectRemoved] activeKarmicTetherVisual set to null.", LogCategory.Unit, this);
                }
                // Unsubscribe the private method from the event (should be done here)
                this.OnStatusEffectRemoved -= PropagateRemovedStatusEffect;
                SmartLogger.Log($"[DokkaebiUnit.RaiseStatusEffectRemoved] Unsubscribed from OnStatusEffectRemoved for removal propagation.", LogCategory.Unit, this);
            }
            // Effect-specific visual cleanup
            if (instance != null && activeEffectVisuals.TryGetValue(instance.StatusEffectType, out var visualInstance))
            {
                if (visualInstance != null)
                {
                    // --- ADDED LOG BEFORE DESTROYING EFFECT VISUAL ---
                    SmartLogger.Log($"[DokkaebiUnit.RaiseStatusEffectRemoved] Destroying effect visual GameObject (Instance ID: {visualInstance.GetInstanceID()}) for effect type {instance.StatusEffectType} on unit {DisplayName}.", LogCategory.Unit, this);
                    // --- END ADDED LOG ---
                    UnityEngine.GameObject.Destroy(visualInstance);
                }
                activeEffectVisuals.Remove(instance.StatusEffectType);
                SmartLogger.Log($"[DokkaebiUnit.RaiseStatusEffectRemoved] Removed effect visual entry from dictionary for type {instance.StatusEffectType}.", LogCategory.Unit, this);
            }
            else if (instance != null) // Added check if effect is null to avoid logging null
            {
                SmartLogger.Log($"[DokkaebiUnit.RaiseStatusEffectRemoved] No specific visual found or removed for effect type {instance.StatusEffectType}.", LogCategory.Unit, this);
            }

            // --- ADDED LOG: END ---
            SmartLogger.Log($"[DokkaebiUnit.RaiseStatusEffectRemoved] END for unit: {DisplayName}. activeKarmicTetherVisual state: {(activeKarmicTetherVisual == null ? "null" : "not null")}", LogCategory.Unit, this);
            // --- END ADDED LOG ---
        }

        public void SetPendingMovement(bool isPending)
        {
            SmartLogger.Log($"[DokkaebiUnit.SetPendingMovement] Unit {unitName} (ID: {unitId}) - Setting hasPendingMovement={isPending}. Previous value: {hasPendingMovement}", LogCategory.Unit, this);
            hasPendingMovement = isPending;
            SmartLogger.Log($"[DokkaebiUnit.SetPendingMovement] Unit {unitName} (ID: {unitId}) - hasPendingMovement SET TO: {hasPendingMovement}", LogCategory.Unit, this);
        }

        /// <summary>
        /// Checks if this unit has an afterimage prefab defined.
        /// </summary>
        public bool HasAfterimagePrefabDefined()
        {
            bool hasData = _unitDefinitionData != null;
            bool hasPrefab = hasData && _unitDefinitionData.afterimagePrefab != null;
            // Add this specific log:
            SmartLogger.Log($"[HasAfterimagePrefabDefined] Check for {unitName}. HasData: {hasData}, HasPrefabInData: {hasPrefab}", LogCategory.Unit, this); 
            return hasPrefab;
        }

        /// <summary>
        /// Shows the afterimage at the specified position.
        /// </summary>
        /// <param name="position">The grid position where the afterimage should appear.</param>
        public void ShowAfterimage(GridPosition position)
        {
            GameObject prefabToUse = (_unitDefinitionData != null) ? _unitDefinitionData.afterimagePrefab : null;

            // Add/verify this log:
            SmartLogger.Log($"[ShowAfterimage ENTRY] Called for {unitName}. Prefab from Data is Null: {(prefabToUse == null)}", LogCategory.Unit, this);

            if (prefabToUse == null) 
            {
                SmartLogger.LogWarning($"[DokkaebiUnit.ShowAfterimage] No afterimage prefab assigned for unit {unitName} (ID: {unitId})", LogCategory.Unit);
                return;
            }

            // Clean up any existing afterimage first
            HideAfterimage();

            if (GridManager.Instance != null)
            {
                Vector3 worldPosition = GridManager.Instance.GridToWorldPosition(position);
                currentAfterimageInstance = Instantiate(prefabToUse, worldPosition, Quaternion.identity); 
                // Add/verify this log:
                SmartLogger.Log($"[ShowAfterimage INSTANTIATED] Created afterimage for unit {unitName} at position {position}.", LogCategory.Unit, this);
            }
        }

        /// <summary>
        /// Hides and cleans up the current afterimage if it exists.
        /// </summary>
        public void HideAfterimage()
        {
            SmartLogger.Log($"[HideAfterimage] Called for {unitName}. Current Pos: {gridPosition}. Stored Start Pos: {positionAtTurnStart}", LogCategory.Unit, this);
            
            if (currentAfterimageInstance != null)
            {
                SmartLogger.Log($"[DokkaebiUnit.HideAfterimage] Destroying afterimage (Instance ID: {currentAfterimageInstance.GetInstanceID()}) for unit {unitName} (ID: {unitId})", LogCategory.Unit);
                Destroy(currentAfterimageInstance);
                currentAfterimageInstance = null;
            }
            else
            {
                SmartLogger.Log($"[DokkaebiUnit.HideAfterimage] No afterimage to destroy for unit {unitName} (ID: {unitId})", LogCategory.Unit);
            }
        }

        public void ModifyAura(int amount)
        {
            AuraManager.Instance.ModifyAura(isPlayerUnit, amount);
        }

        /// <summary>
        /// Modifies the unit's own Aura pool, clamps the value, and invokes the OnUnitAuraChanged event.
        /// </summary>
        /// <param name="amount">The amount to change Aura by (positive to add, negative to subtract)</param>
        /// <returns>The actual amount of Aura modified</returns>
        public int ModifyUnitAura(int amount)
        {
            SmartLogger.Log($"[{GetUnitName()}({UnitId})] ModifyUnitAura called with amount: {amount}. Current Aura: {currentUnitAura}", LogCategory.Ability | LogCategory.Debug, this);
            if (!IsAlive) return 0;

            int oldAura = currentUnitAura;
            currentUnitAura = Mathf.Clamp(currentUnitAura + amount, 0, maxUnitAura);
            int actualChange = currentUnitAura - oldAura;

            if (actualChange != 0)
            {
                SmartLogger.Log($"[{unitName}] Unit Aura changed from {oldAura} to {currentUnitAura} (Amount: {amount}, Actual: {actualChange})", LogCategory.Ability);
                OnUnitAuraChanged?.Invoke(oldAura, currentUnitAura);
            }

            return actualChange;
        }

        /// <summary>
        /// Checks if the unit has enough unit-specific Aura for a cost
        /// </summary>
        /// <param name="cost">The Aura cost to check</param>
        /// <returns>True if the unit has enough Aura, false otherwise</returns>
        public bool HasEnoughUnitAura(int cost)
        {
            return currentUnitAura >= cost;
        }

        public List<GridPosition> GetValidMovePositions()
        {
            if (movementHandler == null)
            {
                SmartLogger.LogError($"[DUnit.GetValidMoves] MovementHandler reference is null for unit {UnitId}! Cannot get valid moves.", LogCategory.Unit, this);
                return new List<GridPosition>(); // Return empty list if handler is missing
            }
            // Call the movement handler's method which performs the BFS/pathfinding based check
            SmartLogger.Log($"[DUnit.GetValidMoves] Unit {UnitId} delegating to MovementHandler.GetValidMovePositions().", LogCategory.Unit, this);
            List<GridPosition> validPositions = movementHandler.GetValidMovePositions(); // Assuming this method exists on the handler

            // Log the result received from the handler
            using (var sb = new StringBuilderScope(out StringBuilder builder))
            {
                builder.AppendLine($"[DUnit.GetValidMoves] Received {validPositions.Count} positions from MovementHandler:");
                foreach(var pos in validPositions) { builder.AppendLine($"- {pos}"); }
                SmartLogger.LogWithBuilder(builder, LogCategory.Unit, this);
            }

            return validPositions;
        }

        public void SetTargetPosition(GridPosition targetPos)
        {
            SmartLogger.Log($"[DokkaebiUnit.SetTargetPosition] Unit {unitName} (ID: {unitId}) - Setting hasPendingMovement=true. Previous value: {hasPendingMovement}", LogCategory.Unit, this);
            targetPosition = targetPos;
            hasPendingMovement = true;
            SmartLogger.Log($"[DokkaebiUnit.SetTargetPosition] Unit {unitName} (ID: {unitId}) - hasPendingMovement SET TO: {hasPendingMovement}", LogCategory.Unit, this);
        }

        public void ClearPendingMovement()
        {
            SmartLogger.Log($"[DokkaebiUnit.ClearPendingMovement] Unit {unitName} (ID: {unitId}) - Setting hasPendingMovement=false. Previous value: {hasPendingMovement}", LogCategory.Unit, this);
            hasPendingMovement = false;
            targetPosition = null;
            SmartLogger.Log($"[DokkaebiUnit.ClearPendingMovement] Unit {unitName} (ID: {unitId}) - hasPendingMovement SET TO: {hasPendingMovement}", LogCategory.Unit, this);
        }

        public GridPosition GetPendingTargetPosition()
        {
            if (hasPendingMovement && targetPosition.HasValue)
            {
                return targetPosition.Value;
            }
            
            return gridPosition;
        }

        public bool CanMove()
        {
            SmartLogger.Log($"[DokkaebiUnit.CanMove] Checking {unitName} (ID: {unitId}). hasMovedThisTurn: {hasMovedThisTurn}, hasPendingMovement: {hasPendingMovement}", LogCategory.Unit, this);
            return !hasMovedThisTurn && !hasPendingMovement;
        }

        public bool HasMovedThisTurn => hasMovedThisTurn;

        public void SetHasMoved(bool moved)
        {
            hasMovedThisTurn = moved;
            SmartLogger.Log($"[DokkaebiUnit.SetHasMoved] Unit {UnitId} hasMovedThisTurn set to {moved}", LogCategory.Unit, this);
        }

        // --- Properties & Getters ---
        public List<AbilityData> GetAbilities() => abilities ?? (abilities = new List<AbilityData>());
        public OriginData GetOrigin() => origin;
        public CallingData GetCalling() => calling;

        // --- Setters (Used by UnitManager during spawn/config) ---
        public void SetUnitDefinitionData(UnitDefinitionData definitionData)
        {
            this._unitDefinitionData = definitionData;
            SmartLogger.Log($"[{this.unitName}] Set UnitDefinitionData to {definitionData?.name}", LogCategory.Unit, this);
        }

        public void SetOrigin(OriginData originData)
        {
            this.origin = originData;
            SmartLogger.Log($"[{this.unitName}] Set Origin to {originData?.displayName}", LogCategory.Unit, this);
        }

        public void SetCalling(CallingData callingData)
        {
            this.calling = callingData;
            SmartLogger.Log($"[{this.unitName}] Set Calling to {callingData?.displayName}", LogCategory.Unit, this);
        }

        public void SetAbilities(List<AbilityData> abilityList)
        {
            this.abilities = abilityList ?? new List<AbilityData>();
            SmartLogger.Log($"[{this.unitName}] Set Abilities. Count: {this.abilities.Count}", LogCategory.Unit, this);
        }

        // --- Turn/Action State Methods ---
        public void ResetMP()
        {
            if (!this.gameObject.TryGetComponent<DokkaebiUnit>(out var unitComponent))
            {
                SmartLogger.LogError($"[{this.unitName}] Failed to get DokkaebiUnit component in ResetMP.", LogCategory.Unit, this);
                return;
            }
            this.currentMP = this.GetMaxMP();
            SmartLogger.Log($"[{this.unitName}] MP Reset to {this.currentMP}/{this.GetMaxMP()}", LogCategory.Unit, this);
        }

        public void EndTurn()
        {
            SmartLogger.Log($"[DokkaebiUnit.EndTurn] Unit {unitName} (ID: {unitId}) - Starting EndTurn", LogCategory.Unit, this);
            ClearPendingMovement();
            hasMovedThisTurn = false;
            SmartLogger.Log($"[DokkaebiUnit.EndTurn] Unit {unitName} (ID: {unitId}) - Completed EndTurn. Final hasPendingMovement: {hasPendingMovement}", LogCategory.Unit, this);
        }

        public void ResetActionState()
        {
            SmartLogger.Log($"[DokkaebiUnit.ResetActionState] Unit {unitName} (ID: {unitId}) - Setting hasPendingMovement=false. Previous value: {hasPendingMovement}", LogCategory.Unit, this);
            hasPendingMovement = false;
            hasMovedThisTurn = false;

            // --- ADD MOVEMENT RANGE RESET START ---
            SmartLogger.Log($"[DokkaebiUnit.ResetActionState] DEBUG: Before movementRange reset. Current movementRange: {movementRange}", LogCategory.Unit, this);
            if (_unitDefinitionData != null)
            {
                movementRange = _unitDefinitionData.baseMovement;
                SmartLogger.Log($"[DokkaebiUnit.ResetActionState] DEBUG: movementRange reset to base: {_unitDefinitionData.baseMovement}. New movementRange: {movementRange}", LogCategory.Unit, this);
            }
            else
            {
                SmartLogger.LogWarning($"[DokkaebiUnit.ResetActionState] DEBUG: _unitDefinitionData is null for unit {unitName}. Cannot reset movementRange to base.", LogCategory.Unit, this);
            }
            // --- ADD MOVEMENT RANGE RESET END ---

            SmartLogger.Log($"[DokkaebiUnit.ResetActionState] Unit {unitName} (ID: {unitId}) - haspendingMovement SET TO: {hasPendingMovement}, hasMovedThisTurn SET TO: {hasMovedThisTurn}", LogCategory.Unit, this);
        }

        public void SetInteractable(bool interactable)
        {
            this.isInteractable = interactable;
            SmartLogger.Log($"[{this.unitName}] Set Interactable to {interactable}", LogCategory.Unit, this);
        }

        public void UpdateCooldowns()
        {
            SmartLogger.Log($"[{this.unitName}] UpdateCooldowns START. Current Cooldowns: {string.Join(", ", abilityCooldowns.Select(kv => $"{kv.Key}:{kv.Value}"))}", LogCategory.Unit, this);
            
            var keys = new List<string>(abilityCooldowns.Keys);
            foreach (var key in keys)
            {
                if (abilityCooldowns.TryGetValue(key, out int currentCD) && currentCD > 0)
                {
                    abilityCooldowns[key] = currentCD - 1;
                    SmartLogger.Log($"[{this.unitName}] Decremented cooldown for {key}: {currentCD} -> {abilityCooldowns[key]}", LogCategory.Unit, this);
                    
                    if (abilityCooldowns[key] <= 0)
                    {
                        abilityCooldowns.Remove(key);
                        SmartLogger.Log($"[{this.unitName}] Removed completed cooldown for {key}", LogCategory.Unit, this);
                    }
                }
                else if (abilityCooldowns.ContainsKey(key))
                {
                    abilityCooldowns.Remove(key);
                }
            }
            
            SmartLogger.Log($"[{this.unitName}] UpdateCooldowns END. Final Cooldowns: {string.Join(", ", abilityCooldowns.Select(kv => $"{kv.Key}:{kv.Value}"))}", LogCategory.Unit, this);
        }

        public bool IsReady()
        {
            bool ready = this.isInteractable && !this.hasPendingMovement && StatusEffectSystem.CanUnitAct(this);
            SmartLogger.Log($"[{this.unitName}] IsReady check: Interactable={this.isInteractable}, HasPendingMove={this.hasPendingMovement}, CanActStatus={StatusEffectSystem.CanUnitAct(this)} -> Result={ready}", LogCategory.Unit, this);
            return ready;
        }

        public void HandleStatusEffectAdded(IStatusEffectInstance effect)
        {
            // LOG: Only for Movement effects
            if (effect?.StatusEffectType == StatusEffectType.Movement)
            {
                SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectAdded] UNIT {DisplayName} (ID: {UnitId}) - Handling Effect Type: {effect?.StatusEffectType}, Instance ID: {effect?.GetHashCode()}. Call Stack:\n{UnityEngine.StackTraceUtility.ExtractStackTrace()}", LogCategory.Unit, this);
            }

            // Existing logic (if any) ...
            // Karmic Tether visual logic
            if (effect.StatusEffectType == StatusEffectType.FateLinked && karmicTetherVisualPrefab != null && activeKarmicTetherVisual == null)
            {
                // 1. At the very beginning of the FateLinked block
                if (effect is StatusEffectInstance tetherEffectInstance)
                {
                    SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectAdded] Processing FateLinked effect for unit {DisplayName} (ID: {UnitId}). Linked Unit ID in effect: {tetherEffectInstance.linkedUnitId}", LogCategory.Unit, this);
                }
                else
                {
                    SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectAdded] Processing FateLinked effect for unit {DisplayName} (ID: {UnitId}). Effect instance is not StatusEffectInstance.", LogCategory.Unit, this);
                }
                var statusEffectInstance = effect as StatusEffectInstance;
                if (statusEffectInstance != null && statusEffectInstance.linkedUnitId != -1)
                {
                    // 2. Just before the prefab null check
                    SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectAdded] Karmic Tether visual prefab assigned: {karmicTetherVisualPrefab != null}", LogCategory.Unit, this);
                    activeKarmicTetherVisual = Instantiate(karmicTetherVisualPrefab, transform);
                    // 3. Just after instantiation
                    SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectAdded] Karmic Tether visual instantiated: {activeKarmicTetherVisual != null}", LogCategory.Unit, this);
                    var tetherVisualComponent = activeKarmicTetherVisual.GetComponent<TetherVisual>();
                    // 4. After getting the TetherVisual component
                    SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectAdded] TetherVisual component found on visual: {tetherVisualComponent != null}", LogCategory.Unit, this);
                    var linkedUnit = UnitManager.Instance.GetUnitById(statusEffectInstance.linkedUnitId) as DokkaebiUnit;
                    // 5. After finding the linked unit
                    SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectAdded] Linked unit found: {linkedUnit != null} (ID: {linkedUnit?.UnitId ?? -1})", LogCategory.Unit, this);
                    // 6. Just before assigning units to TetherVisual
                    SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectAdded] Assigning units to TetherVisual. Unit1: {this.DisplayName} (ID: {this.UnitId}), Unit2: {linkedUnit?.DisplayName ?? "NULL"} (ID: {linkedUnit?.UnitId ?? -1})", LogCategory.Unit, this);
                    if (tetherVisualComponent != null)
                    {
                        tetherVisualComponent.Unit1 = this;
                        tetherVisualComponent.Unit2 = linkedUnit;
                    }
                }
                else
                {
                    SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectAdded] StatusEffectInstance is null or linkedUnitId is invalid for FateLinked effect.", LogCategory.Unit, this);
                }
                // Subscribe the private method to the event for removal propagation
                this.OnStatusEffectRemoved += PropagateRemovedStatusEffect;
                SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectAdded] Subscribed to OnStatusEffectRemoved for removal propagation.", LogCategory.Unit, this);
            }
            // Effect-specific visual logic
            var effectData = effect.Effect as StatusEffectData;
            if (effectData != null && effectData.visualEffect != null && effect.StatusEffectType != StatusEffectType.FateLinked && !activeEffectVisuals.ContainsKey(effect.StatusEffectType))
            {
                var visualInstance = Instantiate(effectData.visualEffect, transform);
                activeEffectVisuals[effect.StatusEffectType] = visualInstance;
            }
        }

        public void HandleStatusEffectRemoved(IStatusEffectInstance effect)
        {
            // --- ADDED LOG: Trace removed effect ---
            SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectRemoved] BEGIN: Handling removed effect: {effect?.StatusEffectType.ToString() ?? "<null effect>"} (Instance ID: {effect?.GetHashCode() ?? -1}) from unit: {DisplayName} (ID: {UnitId})", LogCategory.Unit, this);
            // --- END ADDED LOG ---
            // Karmic Tether visual cleanup
            if (effect != null && effect.StatusEffectType == StatusEffectType.FateLinked)
            {
                // --- ADDED LOGS FOR FATE LINKED CLEANUP ---
                SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectRemoved] Detected FateLinked effect removal. Checking activeKarmicTetherVisual (is null: {activeKarmicTetherVisual == null}).", LogCategory.Unit, this);
                // --- END ADDED LOGS ---

                if (activeKarmicTetherVisual != null)
                {
                    // --- ADDED LOG BEFORE DESTROY ---
                    SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectRemoved] Destroying activeKarmicTetherVisual GameObject (Instance ID: {activeKarmicTetherVisual.GetInstanceID()}) for unit {DisplayName}.", LogCategory.Unit, this);
                    // --- END ADDED LOG ---
                    UnityEngine.GameObject.Destroy(activeKarmicTetherVisual);
                    activeKarmicTetherVisual = null;
                    SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectRemoved] activeKarmicTetherVisual set to null.", LogCategory.Unit, this);
                }
                // Unsubscribe the private method from the event (should be done here)
                this.OnStatusEffectRemoved -= PropagateRemovedStatusEffect;
                SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectRemoved] Unsubscribed from OnStatusEffectRemoved for removal propagation.", LogCategory.Unit, this);
            }
            // Effect-specific visual cleanup
            if (effect != null && activeEffectVisuals.TryGetValue(effect.StatusEffectType, out var visualInstance))
            {
                if (visualInstance != null)
                {
                    // --- ADDED LOG BEFORE DESTROYING EFFECT VISUAL ---
                    SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectRemoved] Destroying effect visual GameObject (Instance ID: {visualInstance.GetInstanceID()}) for effect type {effect.StatusEffectType} on unit {DisplayName}.", LogCategory.Unit, this);
                    // --- END ADDED LOG ---
                    UnityEngine.GameObject.Destroy(visualInstance);
                }
                activeEffectVisuals.Remove(effect.StatusEffectType);
                SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectRemoved] Removed effect visual entry from dictionary for type {effect.StatusEffectType}.", LogCategory.Unit, this);
            }
            else if (effect != null) // Added check if effect is null to avoid logging null
            {
                SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectRemoved] No specific visual found or removed for effect type {effect.StatusEffectType}.", LogCategory.Unit, this);
            }

            // --- ADDED LOG: END ---
            SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectRemoved] END for unit: {DisplayName}. activeKarmicTetherVisual state: {(activeKarmicTetherVisual == null ? "null" : "not null")}", LogCategory.Unit, this);
            // --- END ADDED LOG ---
        }

        // --- Movement Range Modifier Handlers ---
        private void HandleStatusEffectAddedModifier(IStatusEffectInstance effect)
        {
            // LOG: For any effect
            SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectAddedModifier] UNIT {DisplayName} (ID: {UnitId}) - Received Effect Type: {effect?.StatusEffectType}, Instance ID: {effect?.GetHashCode()}. Call Stack:\n{UnityEngine.StackTraceUtility.ExtractStackTrace()}", LogCategory.Unit, this);

            if (effect == null || effect.Effect == null) return;
            var effectData = effect.Effect as StatusEffectData;
            if (effectData == null) return;
            if (effectData.effectType == StatusEffectType.Movement && effectData.movementRangeModifier != 0)
            {
                movementRange += effectData.movementRangeModifier;
                movementRange = Mathf.Max(1, movementRange);
                SmartLogger.Log($"[DokkaebiUnit] Movement modifier effect APPLIED: {unitName} received {effectData.name} (modifier: +{effectData.movementRangeModifier}). New movement range: {movementRange}", LogCategory.Unit, this);
            }
        }

        private void HandleStatusEffectRemovedModifier(IStatusEffectInstance effect)
        {
            // --- ADD LOG START ---
            SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectRemovedModifier] ENTRY for unit {unitName} (ID: {unitId}). Received effect: {(effect != null ? effect.StatusEffectType.ToString() : "NULL EFFECT")}", LogCategory.Unit, this);
            // --- ADD LOG END ---

            // --- ADD LOG START ---
            SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectRemovedModifier] Checking for null effect or effect.Effect...", LogCategory.Unit, this);
            // --- ADD LOG END ---
            if (effect == null || effect.Effect == null)
            {
                // --- ADD LOG START ---
                SmartLogger.LogWarning($"[DokkaebiUnit.HandleStatusEffectRemovedModifier] EXIT: effect or effect.Effect is null. Effect: {effect == null}, effect.Effect: {(effect != null ? effect.Effect == null : false)}", LogCategory.Unit, this);
                // --- ADD LOG END ---
                return;
            }
            // --- ADD LOG START ---
            SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectRemovedModifier] effect and effect.Effect are not null. Attempting cast to StatusEffectData...", LogCategory.Unit, this);
            // --- ADD LOG END ---

            var effectData = effect.Effect as StatusEffectData;

            // --- ADD LOG START ---
            if (effectData == null)
            {
                SmartLogger.LogWarning($"[DokkaebiUnit.HandleStatusEffectRemovedModifier] EXIT: effect.Effect could not be cast to StatusEffectData for effect type: {effect.StatusEffectType}", LogCategory.Unit, this);
                return;
            }
            SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectRemovedModifier] effectData cast successful. Checking effect type and modifier value...", LogCategory.Unit, this);
            SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectRemovedModifier] Effect Type: {effectData.effectType}, Movement Modifier: {effectData.movementRangeModifier}", LogCategory.Unit, this);
            // --- ADD LOG END ---

            if (effectData.effectType == StatusEffectType.Movement && effectData.movementRangeModifier != 0)
            {
                try
                {
                    // --- ADD NEW LOG START ---
                    SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectRemovedModifier] DEBUG: Before applying modifier. Current movementRange: {movementRange}, Modifier to apply: {effectData.movementRangeModifier}", LogCategory.Unit, this);
                    // --- ADD NEW LOG END ---

                    // FIX: Add the modifier back when the effect is removed (it was added on apply)
                    movementRange += effectData.movementRangeModifier; // Change from -= to +=
                    movementRange = Mathf.Max(1, movementRange);
                    SmartLogger.Log($"[DokkaebiUnit] Movement modifier effect REMOVED: {unitName} lost {effectData.name} (modifier: +{effectData.movementRangeModifier}). Restored movement range: {movementRange}", LogCategory.Unit, this);
                }
                catch (Exception ex)
                {
                    SmartLogger.LogWarning($"[DokkaebiUnit.HandleStatusEffectRemovedModifier] Exception occurred: {ex.Message}", LogCategory.Unit, this);
                }
            }
            // --- ADD LOG START ---
            else
            {
                 SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectRemovedModifier] Condition NOT met: EffectType is {effectData.effectType} (expected {StatusEffectType.Movement}) OR modifier is zero ({effectData.movementRangeModifier}). Skipping modifier application.", LogCategory.Unit, this);
            }
            SmartLogger.Log($"[DokkaebiUnit.HandleStatusEffectRemovedModifier] EXIT: Finished processing modifier for effect: {effect.StatusEffectType}", LogCategory.Unit, this);
            // --- ADD LOG END ---
        }

        public UnitDefinitionData GetUnitDefinitionData() { return _unitDefinitionData; }

        /// <summary>
        /// Handles the event triggered when this unit is attacked.
        /// Checks for reactive abilities like Heatwave Counter.
        /// </summary>
        /// <param name="attackedUnit">The unit being attacked (this unit).</param>
        /// <param name="attacker">The unit that attacked this unit.</param>
        /// <param name="damageAmount">The base damage amount of the attack.</param>
        /// <param name="damageType">The type of damage.</param>
        private void HandleBeingAttacked(IDokkaebiUnit attackedUnit, IDokkaebiUnit attacker, int damageAmount, DamageType damageType)
        {
            // Ensure the event is for this unit instance
            if (attackedUnit != this) return;

            SmartLogger.Log($"[{unitName}] HandleBeingAttacked triggered by {attacker?.GetUnitName() ?? "Unknown Attacker"}. Damage: {damageAmount}, Type: {damageType}", LogCategory.Ability, this);

            // Check for Heatwave Counter ability
            AbilityData heatwaveCounterAbility = abilities?.FirstOrDefault(a => a != null && a.abilityId == "HeatwaveCounter");

            if (heatwaveCounterAbility != null)
            {
                bool canTrigger = heatwaveCounterAbility.triggersOnBeingAttacked;
                bool hasBuff = StatusEffectSystem.HasStatusEffect(this, StatusEffectType.HeatwaveCounterReady);

                SmartLogger.Log($"[{unitName}] Heatwave Counter Check: triggersOnBeingAttacked={canTrigger}, hasBuff={hasBuff}", LogCategory.Ability, this);

                if (canTrigger && hasBuff)
                {
                    SmartLogger.Log($"[{unitName}] Heatwave Counter REACTION TRIGGERED! Executing reaction against attacker {attacker?.GetUnitName() ?? "Unknown Attacker"}", LogCategory.Ability, this);
                    var abilityManager = UnityEngine.Object.FindObjectOfType<AbilityManager>();
                    if (abilityManager != null)
                    {
                        abilityManager.ExecuteReactiveAbility(this, attacker, heatwaveCounterAbility);
                    }
                    else
                    {
                        SmartLogger.LogError("[DokkaebiUnit] AbilityManager not found in scene! Cannot execute Heatwave Counter reaction.", LogCategory.Ability, this);
                    }
                }
            }
            else
            {
                SmartLogger.Log($"[{unitName}] Heatwave Counter ability not found in abilities list.", LogCategory.Ability, this);
            }
        }

        /// <summary>
        /// Reduce a specific ability's cooldown by a given amount.
        /// </summary>
        public void ReduceAbilityCooldown(string abilityId, int amount)
        {
            SmartLogger.Log($"[{unitName}] Reducing cooldown for ability {abilityId} by {amount}. Current cooldown: {GetRemainingCooldown(abilityId)}", LogCategory.Ability, this);
            if (abilityCooldowns.TryGetValue(abilityId, out int currentCD))
            {
                abilityCooldowns[abilityId] = Mathf.Max(0, currentCD - amount);
                SmartLogger.Log($"[{unitName}] Cooldown for {abilityId} is now {abilityCooldowns[abilityId]}", LogCategory.Ability, this);
                if (abilityCooldowns[abilityId] <= 0)
                {
                    abilityCooldowns.Remove(abilityId);
                    SmartLogger.Log($"[{unitName}] Cooldown for {abilityId} completed and removed.", LogCategory.Ability, this);
                }
            }
            else
            {
                SmartLogger.Log($"[{unitName}] Ability {abilityId} is not on cooldown.", LogCategory.Ability, this);
            }
        }

        /// <summary>
        /// Resets all ability cooldowns for this unit.
        /// </summary>
        public void ResetAllCooldowns()
        {
            SmartLogger.Log($"[{unitName}] Resetting all ability cooldowns. Clearing {abilityCooldowns.Count} active cooldowns.", LogCategory.Ability, this);
            abilityCooldowns.Clear();
            SmartLogger.Log($"[{unitName}] All ability cooldowns cleared.", LogCategory.Ability, this);
        }

        // Remove direct subscription of StatusEffectSystem.HandleStatusEffectRemovedPropagation
        // Add a private intermediary for removal propagation
        private void PropagateRemovedStatusEffect(IStatusEffectInstance removedEffectInstance)
        {
            StatusEffectSystem.HandleStatusEffectRemovedPropagation(this, removedEffectInstance);
        }
    }
}
