using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Dokkaebi.Grid;
using Dokkaebi.Units;
using Dokkaebi.Utilities;
using Dokkaebi.Interfaces;
using Dokkaebi.Common;
using Dokkaebi.Core.Data;
using Dokkaebi.Core;

namespace Dokkaebi.Zones
{
    /// <summary>
    /// Manages the creation, interaction, and lifecycle of all zones in the game.
    /// </summary>
    public class ZoneManager : MonoBehaviour
    {
        public static ZoneManager Instance { get; private set; }
        
        [Header("References")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private Transform zonesParent;
        [SerializeField] private DokkaebiTurnSystemCore turnSystem;
        
        [Header("Prefabs")]
        [SerializeField] private GameObject zoneInstancePrefab;
        [SerializeField] private GameObject volatileZonePrefab;
        
        [Header("Settings")]
        [SerializeField] private int maxZonesPerTile = 4;
        [SerializeField] private int voidSpaceDuration = 2;
        
        // Track active zones by position
        private Dictionary<Interfaces.GridPosition, List<ZoneInstance>> zonesByPosition = new Dictionary<Interfaces.GridPosition, List<ZoneInstance>>();
        
        // Track void spaces
        private Dictionary<Interfaces.GridPosition, int> voidSpaces = new Dictionary<Interfaces.GridPosition, int>();
        
        // Track all active zones
        private Dictionary<string, Zone> activeZones = new Dictionary<string, Zone>();
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
            
            if (zoneInstancePrefab == null)
            {
                SmartLogger.LogError("ZoneManager: Zone instance prefab not assigned!", LogCategory.Zone, this);
                return;
            }
            
            // Create zones parent if it doesn't exist
            if (zonesParent == null)
            {
                zonesParent = new GameObject("Zones").transform;
                zonesParent.SetParent(transform);
            }
            
            // Get references if needed
            if (gridManager == null)
            {
                gridManager = FindObjectOfType<GridManager>();
                if (gridManager == null)
                {
                    SmartLogger.LogWarning("ZoneManager: GridManager reference not found!", LogCategory.Zone, this);
                    return;
                }
            }

            // Auto-reference turnSystem if not assigned
            if (turnSystem == null)
            {
                turnSystem = FindObjectOfType<DokkaebiTurnSystemCore>();
                if (turnSystem != null)
                {
                    SmartLogger.Log("ZoneManager: Auto-referenced TurnSystem", LogCategory.Zone);
                }
                else
                {
                    SmartLogger.LogError("ZoneManager: Could not find TurnSystem in scene!", LogCategory.Zone);
                }
            }
        }
        
        private void Start()
        {
            // Check if turn system reference is assigned
            if (turnSystem == null)
            {
                SmartLogger.LogWarning("ZoneManager: TurnSystem reference not assigned in inspector!", LogCategory.Zone);
            }
            else
            {
                SmartLogger.Log("ZoneManager: TurnSystem reference found", LogCategory.Zone);
            }
        }
        
        private void OnEnable()
        {
            if (turnSystem != null)
            {
                turnSystem.OnTurnResolutionEnd += HandleTurnResolutionEnd;
                SmartLogger.Log("ZoneManager: Successfully subscribed to TurnResolutionEnd event", LogCategory.Zone);
            }
            else
            {
                SmartLogger.LogWarning("ZoneManager: TurnSystem reference is null in OnEnable, cannot subscribe to TurnResolutionEnd.", LogCategory.Zone);
            }
        }
        
        private void OnDisable()
        {
            if (turnSystem != null)
            {
                turnSystem.OnTurnResolutionEnd -= HandleTurnResolutionEnd;
                SmartLogger.Log("ZoneManager: Unsubscribed from TurnResolutionEnd event", LogCategory.Zone);
            }
        }
        
        /// <summary>
        /// Handles the end of turn resolution when all actions are complete
        /// </summary>
        private void HandleTurnResolutionEnd()
        {
            SmartLogger.Log("[HandleTurnResolutionEnd] Event received. (ZoneManager)" , LogCategory.Zone, this);
            // Call ProcessTurn first (for duration updates, continuous effects)
            SmartLogger.Log("[HandleTurnResolutionEnd] About to call ProcessTurn() (ZoneManager)", LogCategory.Zone, this);
            ProcessTurn();

            // --- Add the missing call to ProcessTurnResolutionEnd() here ---
            SmartLogger.Log("[HandleTurnResolutionEnd] About to call ProcessTurnResolutionEnd() (ZoneManager)", LogCategory.Zone, this);
            ProcessTurnResolutionEnd();
            // --- End added call ---
        }
        
        /// <summary>
        /// Process the turn end for all zones, applying effects and updating durations
        /// </summary>
        public void ProcessTurn()
        {
            SmartLogger.Log($"[ZoneManager.ProcessTurn] START - Processing {zonesByPosition?.Count ?? 0} zone positions", LogCategory.Zone, this);
            if (zonesByPosition == null || zonesByPosition.Count == 0)
            {
                SmartLogger.Log("[ZoneManager.ProcessTurn] No active zones to process, exiting", LogCategory.Zone, this);
                return;
            }
            
            // Log the current state of unit positions for debugging
            LogUnitPositions();
            
            // Process all zones using a copy to avoid modification during iteration
            Dictionary<Interfaces.GridPosition, List<ZoneInstance>> zonesCopy = 
                new Dictionary<Interfaces.GridPosition, List<ZoneInstance>>(zonesByPosition);
            
            // Tracking variables for summary
            int totalZonesProcessed = 0;
            int zonesRemoved = 0;
            
            foreach (var kvp in zonesCopy)
            {
                var position = kvp.Key;
                var zonesAtPos = kvp.Value;
                
                SmartLogger.Log($"[ZoneManager.ProcessTurn] Processing position {position} with {zonesAtPos.Count} zones", LogCategory.Zone, this);
                
                // First apply effects for all zones at this position
                foreach (ZoneInstance zone in zonesAtPos.ToList())
                {
                    if (zone == null)
                    {
                        SmartLogger.LogWarning($"[ZoneManager.ProcessTurn] Null zone reference found at position {position}, removing", LogCategory.Zone, this);
                        zonesAtPos.Remove(zone);
                        zonesRemoved++;
                        continue;
                    }

                    // Log zone details before processing
                    SmartLogger.Log($"[ZoneManager.ProcessTurn] Processing Zone: {zone.DisplayName} (Instance:{zone.GetInstanceID()}), IsActive: {zone.IsActive}, RemainingDuration: {zone.RemainingDuration}", LogCategory.Zone, this);

                    if (!zone.IsActive)
                    {
                        SmartLogger.Log($"[ZoneManager.ProcessTurn] Skipping inactive zone: {zone.DisplayName} (Instance:{zone.GetInstanceID()}) at position {position}", LogCategory.Zone, this);
                        zonesAtPos.Remove(zone);
                        zonesRemoved++;
                        continue;
                    }

                    try
                    {
                        // Let the zone instance handle its own area effect application
                        SmartLogger.Log($"[ZoneManager.ProcessTurn] About to call zone.ApplyZoneEffects() for {zone.DisplayName} (Instance:{zone.GetInstanceID()})", LogCategory.Zone, this);
                        zone.ApplyZoneEffects();
                        
                        // Process turn for duration
                        SmartLogger.Log($"[ZoneManager.ProcessTurn] About to call zone.ProcessTurn() for {zone.DisplayName} (Instance:{zone.GetInstanceID()})", LogCategory.Zone, this);
                        zone.ProcessTurn();
                        totalZonesProcessed++;
                        
                        // Log zone state after processing
                        SmartLogger.Log($"[ZoneManager.ProcessTurn] Zone processed: {zone.DisplayName} (Instance:{zone.GetInstanceID()}), New State - IsActive: {zone.IsActive}, RemainingDuration: {zone.RemainingDuration}", LogCategory.Zone, this);
                        
                        // Remove inactive zones
                        if (!zone.IsActive)
                        {
                            SmartLogger.Log($"[ZoneManager.ProcessTurn] Zone {zone.DisplayName} (Instance:{zone.GetInstanceID()}) is inactive and being removed.", LogCategory.Zone, this);
                            zonesAtPos.Remove(zone);
                            zonesRemoved++;
                        }
                    }
                    catch (System.Exception e)
                    {
                        SmartLogger.LogError($"[ZoneManager.ProcessTurn] Error processing zone {zone.DisplayName} (Instance:{zone.GetInstanceID()}) at {position}: {e.Message}\n{e.StackTrace}", LogCategory.Zone, this);
                        // Continue processing other zones even if one fails
                    }
                }
                
                // Remove empty lists
                if (zonesAtPos.Count == 0)
                {
                    SmartLogger.Log($"[ZoneManager.ProcessTurn] List for position {position} is empty and being removed.", LogCategory.Zone, this);
                    zonesByPosition.Remove(position);
                }
            }
            
            // Process void spaces
            ProcessVoidSpaces();
            
            // Final summary log
            SmartLogger.Log($"[ZoneManager.ProcessTurn] END - Summary: Processed {totalZonesProcessed} zones, Removed {zonesRemoved} zones, Remaining positions with active zones: {zonesByPosition.Count}", LogCategory.Zone, this);
        }
        
        /// <summary>
        /// Logs the current state of unit positions for debugging purposes
        /// </summary>
        private void LogUnitPositions()
        {
            SmartLogger.Log("[ZoneManager] Current Unit Positions:", LogCategory.Zone);
            var currentUnitPositions = UnitManager.Instance.GetUnitPositionsReadOnly();
            
            if (currentUnitPositions == null || currentUnitPositions.Count == 0)
            {
                SmartLogger.Log("-- No units found in positions dictionary.", LogCategory.Zone);
                return;
            }
            
            foreach (var kvp in currentUnitPositions)
            {
                string unitInfo = kvp.Value != null ? 
                    $"{kvp.Value.GetUnitName()} (ID: {kvp.Value.UnitId})" : "NULL";
                SmartLogger.Log($"- Position: {kvp.Key}, Unit: {unitInfo}", LogCategory.Zone);
            }
        }
        
        /// <summary>
        /// Create a zone at the specified position with the given parameters
        /// </summary>
        public ZoneInstance CreateZone(
            Interfaces.GridPosition position,
            IZoneData zoneTypeData,
            DokkaebiUnit ownerUnit,
            AbilityData creatingAbilityData,
            int duration = -1)
        {
            SmartLogger.Log($"[ZoneManager.CreateZone] Attempting to create zone '{zoneTypeData?.DisplayName ?? "Unknown"}' at {position}", LogCategory.Zone, this);
            // Check if position is valid
            if (!IsValidPosition(position))
            {
                SmartLogger.LogWarning($"ZoneManager: Cannot create zone at invalid position {position}", LogCategory.Zone, this);
                return null;
            }
            
            // Check if the tile is in void space
            if (IsVoidSpace(position))
            {
                SmartLogger.LogWarning($"ZoneManager: Cannot create zone in void space at {position}", LogCategory.Zone, this);
                return null;
            }

            // Calculate adjusted position to keep zone within grid boundaries
            int radius = zoneTypeData.Radius;
            int gridWidth = gridManager.GetGridWidth();
            int gridHeight = gridManager.GetGridHeight();

            // Calculate the minimum and maximum valid center positions that keep the zone in bounds
            int minValidX = radius;
            int maxValidX = gridWidth - 1 - radius;
            int minValidZ = radius;
            int maxValidZ = gridHeight - 1 - radius;

            // Clamp the target position to keep the zone in bounds
            Interfaces.GridPosition adjustedPosition = new Interfaces.GridPosition(
                Mathf.Clamp(position.x, minValidX, maxValidX),
                Mathf.Clamp(position.z, minValidZ, maxValidZ)
            );

            // Log if position was adjusted
            if (position != adjustedPosition)
            {
                SmartLogger.Log($"ZoneManager: Adjusted zone position from {position} to {adjustedPosition} to keep within bounds", LogCategory.Zone, this);
            }
            
            // Get or create the list of zones at this position
            if (!zonesByPosition.TryGetValue(adjustedPosition, out var zonesAtPosition))
            {
                zonesAtPosition = new List<ZoneInstance>();
                zonesByPosition[adjustedPosition] = zonesAtPosition;
                SmartLogger.Log($"[ZoneManager.CreateZone] Created new list for zones at {adjustedPosition}", LogCategory.Zone, this);
            }
            
            // Check for unstable resonance (too many zones)
            if (zonesAtPosition.Count >= maxZonesPerTile)
            {
                HandleUnstableResonance(adjustedPosition);
                return null;
            }
            
            // Instantiate the zone instance
            GameObject prefabToInstantiate = null;

            // Check if the concrete ZoneData type has a specific prefab assigned
            if (zoneTypeData is ZoneData concreteZoneData && concreteZoneData.zonePrefab != null)
            {
                prefabToInstantiate = concreteZoneData.zonePrefab;
                SmartLogger.Log($"Using specific zone prefab '{prefabToInstantiate.name}' from ZoneData '{concreteZoneData.displayName}'", LogCategory.Zone, this);
            }
            else
            {
                // Fallback to the default prefab assigned in the ZoneManager inspector
                prefabToInstantiate = this.zoneInstancePrefab;
                SmartLogger.Log($"Using default zone prefab '{prefabToInstantiate?.name ?? "NULL"}' from ZoneManager", LogCategory.Zone, this);
            }

            if (prefabToInstantiate == null)
            {
                SmartLogger.LogError($"Cannot create zone '{zoneTypeData?.DisplayName ?? "Unknown"}': No valid prefab found (neither in ZoneData nor ZoneManager).", LogCategory.Zone, this);
                return null;
            }

            SmartLogger.Log($"[ZoneManager.CreateZone] Instantiating zone prefab: {prefabToInstantiate?.name ?? "NULL"}", LogCategory.Zone, this);
            // Instantiate the chosen prefab
            GameObject zoneObject = Instantiate(prefabToInstantiate, Vector3.zero, Quaternion.identity, zonesParent);
            zoneObject.name = $"Zone_{zoneTypeData.DisplayName}_{adjustedPosition}";
            
            // Get and initialize the zone instance component
            ZoneInstance zoneInstance = zoneObject.GetComponent<ZoneInstance>();
            if (zoneInstance == null)
            {
                zoneInstance = zoneObject.AddComponent<ZoneInstance>();
            }
            SmartLogger.Log($"[ZoneManager.CreateZone] ZoneInstance component found on instantiated object: {zoneInstance != null}", LogCategory.Zone, this);
            
            // Extract the owner unit ID from the DokkaebiUnit
            int ownerUnitId = ownerUnit != null ? ownerUnit.UnitId : -1;
            
            // Initialize the zone with the provided parameters
            zoneInstance.Initialize(
                zoneTypeData,
                adjustedPosition,
                ownerUnitId,
                duration > 0 ? duration : zoneTypeData.DefaultDuration
            );
            
            // Add to the list of zones at this position
            zonesAtPosition.Add(zoneInstance);
            SmartLogger.Log($"[ZoneManager.CreateZone] Added zone '{zoneInstance.DisplayName}' (Instance:{zoneInstance.GetInstanceID()}) to zonesByPosition at {adjustedPosition}. Total zones at this pos: {zonesAtPosition.Count}", LogCategory.Zone, this);
            
            // Apply initial effects immediately after creation
            zoneInstance.ApplyInitialEffects();
            
            return zoneInstance;
        }
        
        /// <summary>
        /// Handle the case where too many zones are on a tile, causing unstable resonance
        /// </summary>
        private void HandleUnstableResonance(Interfaces.GridPosition position)
        {
            SmartLogger.Log($"Unstable Resonance triggered at {position}", LogCategory.Zone, this);
            
            // Create a volatile zone (damaging zone) for 1 turn
            if (volatileZonePrefab != null)
            {
                GameObject volatileObj = Instantiate(volatileZonePrefab, gridManager.GridToWorldPosition(position), Quaternion.identity);
                volatileObj.name = $"VolatileZone_{position}";
                
                // TODO: Initialize volatile zone with appropriate effects
                // This would typically be done through some other means
            }
            
            // Clear all zones at this position
            if (zonesByPosition.TryGetValue(position, out var zonesAtPosition))
            {
                foreach (var zone in zonesAtPosition)
                {
                    if (zone != null && zone.IsActive)
                    {
                        zone.Deactivate();
                    }
                }
                
                zonesAtPosition.Clear();
                zonesByPosition.Remove(position);
            }
            
            // Mark this position as void space for a certain duration
            voidSpaces[position] = voidSpaceDuration;
        }
        
        /// <summary>
        /// Update the state of void spaces
        /// </summary>
        private void ProcessVoidSpaces()
        {
            List<Interfaces.GridPosition> expiredVoidSpaces = new List<Interfaces.GridPosition>();
            
            foreach (var kvp in voidSpaces)
            {
                int remainingDuration = kvp.Value - 1;
                
                if (remainingDuration <= 0)
                {
                    expiredVoidSpaces.Add(kvp.Key);
                }
                else
                {
                    voidSpaces[kvp.Key] = remainingDuration;
                }
            }
            
            // Remove expired void spaces
            foreach (var position in expiredVoidSpaces)
            {
                voidSpaces.Remove(position);
            }
        }
        
        /// <summary>
        /// Check for possible merging of the new zone with existing zones
        /// </summary>
        private void CheckForZoneMerging(ZoneInstance newZone, List<ZoneInstance> zonesAtPosition)
        {
            if (zonesAtPosition.Count <= 1) return;
            
            foreach (var existingZone in zonesAtPosition.ToArray())
            {
                if (existingZone != newZone && existingZone.IsActive && newZone.IsActive)
                {
                    if (newZone.CanMergeWith(existingZone))
                    {
                        newZone.MergeWith(existingZone);
                        
                        // Remove the merged zone from the list
                        zonesAtPosition.Remove(existingZone);
                    }
                }
            }
        }
        
        /// <summary>
        /// Find all zones at a specific position
        /// </summary>
        public List<ZoneInstance> GetZonesAtPosition(Interfaces.GridPosition position)
        {
            SmartLogger.Log($"[ZoneManager.GetZonesAtPosition] Called for position: {position}", LogCategory.Zone, this);
            SmartLogger.Log($"[ZoneManager.GetZonesAtPosition] Method called for position: {position}. Checking internal zonesByPosition dictionary.", LogCategory.Zone, this);

            if (zonesByPosition.TryGetValue(position, out var zones))
            {
                SmartLogger.Log($"[ZoneManager.GetZonesAtPosition] Found entry for position {position}. Number of zones in the list: {zones?.Count ?? 0}", LogCategory.Zone, this);
                return new List<ZoneInstance>(zones);
            }

            SmartLogger.Log($"[ZoneManager.GetZonesAtPosition] No entry found for position {position} in zonesByPosition dictionary.", LogCategory.Zone, this);
            // Log all keys currently in the dictionary to see what positions *are* tracked
            if (zonesByPosition != null && zonesByPosition.Count > 0)
            {
                System.Text.StringBuilder sb = Dokkaebi.Utilities.StringBuilderPool.Get();
                sb.Append("[ZoneManager.GetZonesAtPosition] Currently tracked positions with zones: [");
                foreach (var key in zonesByPosition.Keys)
                {
                    sb.Append(key.ToString() + ", ");
                }
                if (zonesByPosition.Count > 0) sb.Length -= 2; // Remove last ", "
                sb.Append("]");
                SmartLogger.Log(Dokkaebi.Utilities.StringBuilderPool.GetStringAndReturn(sb), LogCategory.Zone, this);
            }
            return new List<ZoneInstance>();
        }
        
        /// <summary>
        /// Check if a tile is in void space
        /// </summary>
        public bool IsVoidSpace(Interfaces.GridPosition position)
        {
            return voidSpaces.ContainsKey(position);
        }
        
        /// <summary>
        /// Check if a position is valid for zone placement
        /// </summary>
        private bool IsValidPosition(Interfaces.GridPosition position)
        {
            // Use GridManager if available
            if (gridManager != null)
            {
                return gridManager.IsPositionValid(position);
            }
            
            // Fallback to default grid size
            return position.x >= 0 && position.x < 10 && position.z >= 0 && position.z < 10;
        }
        
        /// <summary>
        /// Trigger a terrain shift on a zone (move it to a new position)
        /// </summary>
        public bool ShiftZone(ZoneInstance zone, Interfaces.GridPosition newPosition)
        {
            SmartLogger.Log($"[ZoneManager.ShiftZone START] Attempting to shift zone '{(zone?.DisplayName ?? "NULL")}' (Instance:{(zone?.GetInstanceID() ?? -1)}) from {(zone?.GetGridPosition().ToString() ?? "NULL_POS")} to {newPosition}.", LogCategory.Zone, this);

            if (zone == null || !zone.IsActive || !IsValidPosition(newPosition))
            {
                SmartLogger.LogWarning($"[ZoneManager.ShiftZone] Shift failed: Zone is null, inactive, or new position {newPosition} is invalid.", LogCategory.Zone, this);
                return false;
            }

            // Get current position
            Interfaces.GridPosition oldPosition = zone.GetGridPosition();
            SmartLogger.Log($"[ZoneManager.ShiftZone] Zone found and active. Old Position: {oldPosition}. Checking new position: {newPosition}.", LogCategory.Zone, this);

            // Remove from old position
            if (zonesByPosition.TryGetValue(oldPosition, out var zonesAtOldPos))
            {
                zonesAtOldPos.Remove(zone);
                SmartLogger.Log($"[ZoneManager.ShiftZone] Removed zone from old position {oldPosition}. List count at old pos: {zonesAtOldPos.Count}.", LogCategory.Zone, this);
                if (zonesAtOldPos.Count == 0)
                {
                    zonesByPosition.Remove(oldPosition);
                    SmartLogger.Log($"[ZoneManager.ShiftZone] Removed empty list for old position {oldPosition} from dictionary.", LogCategory.Zone, this);
                }
            } else {
                 SmartLogger.LogWarning($"[ZoneManager.ShiftZone] Could not find zone '{zone.DisplayName}' at expected old position {oldPosition} in zonesByPosition.", LogCategory.Zone, this);
            }


            // Add to new position
            if (!zonesByPosition.TryGetValue(newPosition, out var zonesAtNewPos))
            {
                zonesAtNewPos = new List<ZoneInstance>();
                zonesByPosition[newPosition] = zonesAtNewPos;
                 SmartLogger.Log($"[ZoneManager.ShiftZone] Created new list for zones at new position {newPosition}.", LogCategory.Zone, this);
            }
             SmartLogger.Log($"[ZoneManager.ShiftZone] Retrieved/Created list for zones at new position {newPosition}. Current count: {zonesAtNewPos.Count}.", LogCategory.Zone, this);


            // Check for unstable resonance at the NEW position BEFORE adding
            if (zonesAtNewPos.Count >= maxZonesPerTile)
            {
                 SmartLogger.LogWarning($"[ZoneManager.ShiftZone] Cannot shift zone to {newPosition} due to unstable resonance ({zonesAtNewPos.Count}/{maxZonesPerTile} zones already present). Aborting shift.", LogCategory.Zone, this);

                 return false;
            }


            zonesAtNewPos.Add(zone);
             SmartLogger.Log($"[ZoneManager.ShiftZone] Zone added to new position {newPosition}'s list. List now has {zonesAtNewPos.Count} zones.", LogCategory.Zone, this);


            // Move the zone visually
            GameObject zoneObject = zone.gameObject;
            if (gridManager != null)
            {
                zoneObject.transform.position = gridManager.GridToWorldPosition(newPosition);
                SmartLogger.Log($"[ZoneManager.ShiftZone] Zone GameObject transform updated to world position for {newPosition}.", LogCategory.Zone, this);
            } else {
                SmartLogger.LogError("[ZoneManager.ShiftZone] gridManager is null, cannot update zone GameObject transform position.", LogCategory.Zone, this);
            }


            // Update zone's internal grid position state
            zone.SetGridPosition(newPosition);
            SmartLogger.Log($"[ZoneManager.ShiftZone] ZoneInstance internal grid position updated to {newPosition}.", LogCategory.Zone, this);


            // Check for merging at new position
            // CheckForZoneMerging(zone, zonesAtNewPos);

            SmartLogger.Log($"[ZoneManager.ShiftZone END] Successfully shifted zone '{zone.DisplayName}' (Instance:{zone.GetInstanceID()}) from {oldPosition} to {newPosition}.", LogCategory.Zone, this);
            return true;
        }
        
        /// <summary>
        /// Clear all zones
        /// </summary>
        public void ClearAllZones()
        {
            foreach (var positionZones in zonesByPosition.Values)
            {
                foreach (var zone in positionZones)
                {
                    if (zone != null)
                    {
                        Destroy(zone.gameObject);
                    }
                }
                
                positionZones.Clear();
            }
            
            zonesByPosition.Clear();
            voidSpaces.Clear();
        }
        
        /// <summary>
        /// Create a new zone at the specified position
        /// </summary>
        public Zone CreateZone(string zoneType, Vector2Int position, int size, int duration, string ownerUnitId = "")
        {
            // Convert Vector2Int to GridPosition using the utility
            var gridPosition = Dokkaebi.Interfaces.GridPosition.FromVector2Int(position);
            
            // Generate a unique ID for the zone
            string zoneId = System.Guid.NewGuid().ToString();
            
            // Determine which prefab to use based on zone type
            GameObject prefab = GetZonePrefabByType(zoneType);
            
            // Instantiate the zone
            GameObject zoneObj = Instantiate(prefab, Vector3.zero, Quaternion.identity, zonesParent);
            zoneObj.name = $"Zone_{zoneType}_{zoneId.Substring(0, 8)}";
            
            // Get and initialize zone component
            Zone zone = zoneObj.GetComponent<Zone>();
            if (zone == null)
            {
                zone = zoneObj.AddComponent<Zone>();
            }
            
            // Initialize zone data using Vector2Int
            zone.Initialize(zoneId, zoneType, position, size, duration, ownerUnitId);
            zone.SetPosition(position);
            
            // Add to active zones
            activeZones[zoneId] = zone;
            
            return zone;
        }
        
        /// <summary>
        /// Get the appropriate zone prefab based on zone type
        /// </summary>
        private GameObject GetZonePrefabByType(string zoneType)
        {
            // Use the existing zoneInstancePrefab or volatileZonePrefab
            switch (zoneType.ToLower())
            {
                case "damage":
                    return volatileZonePrefab != null ? volatileZonePrefab : zoneInstancePrefab;
                case "healing":
                    return volatileZonePrefab != null ? volatileZonePrefab : zoneInstancePrefab;
                default:
                    return zoneInstancePrefab;
            }
        }
        
        /// <summary>
        /// Create a ZoneInstance from a simple Zone object
        /// </summary>
        public ZoneInstance CreateInstanceFromZone(Zone zone)
        {
            if (zone == null)
            {
                SmartLogger.LogWarning("Cannot create ZoneInstance from null Zone", LogCategory.Zone);
                return null;
            }

            // Get zone data from DataManager based on zone type
            var zoneData = DataManager.Instance.GetZoneData(zone.ZoneType);
            if (zoneData == null)
            {
                SmartLogger.LogError($"Failed to find ZoneData for type {zone.ZoneType}", LogCategory.Zone);
                return null;
            }

            // Convert Vector2Int position to GridPosition
            var gridPosition = GridPosition.FromVector2Int(zone.Position);

            // Determine which prefab to use
            GameObject prefabToInstantiate = null;
            if (zoneData.zonePrefab != null)
            {
                prefabToInstantiate = zoneData.zonePrefab;
                SmartLogger.Log($"Using specific zone prefab '{prefabToInstantiate.name}' from ZoneData '{zoneData.displayName}'", LogCategory.Zone, this);
            }
            else
            {
                prefabToInstantiate = this.zoneInstancePrefab;
                SmartLogger.Log($"Using default zone prefab '{prefabToInstantiate?.name ?? "NULL"}' from ZoneManager", LogCategory.Zone, this);
            }

            if (prefabToInstantiate == null)
            {
                SmartLogger.LogError($"Cannot create zone instance: No valid prefab found (neither in ZoneData nor ZoneManager).", LogCategory.Zone, this);
                return null;
            }

            // Create zone instance
            GameObject zoneObject = Instantiate(prefabToInstantiate, Vector3.zero, Quaternion.identity, zonesParent);
            zoneObject.name = $"ZoneInstance_{zoneData.DisplayName}_{zone.ZoneId}";

            // Get and initialize the zone instance component
            ZoneInstance zoneInstance = zoneObject.GetComponent<ZoneInstance>();
            if (zoneInstance == null)
            {
                zoneInstance = zoneObject.AddComponent<ZoneInstance>();
            }

            // Parse owner unit ID to int (or -1 if invalid)
            int ownerUnitId = -1;
            if (!string.IsNullOrEmpty(zone.OwnerUnitId))
            {
                int.TryParse(zone.OwnerUnitId, out ownerUnitId);
            }

            // Initialize the zone instance
            zoneInstance.Initialize(
                zoneData,
                gridPosition,
                ownerUnitId,
                zone.RemainingDuration
            );

            // Add to the list of zones at this position
            if (!zonesByPosition.TryGetValue(gridPosition, out var zonesAtPosition))
            {
                zonesAtPosition = new List<ZoneInstance>();
                zonesByPosition[gridPosition] = zonesAtPosition;
            }
            zonesAtPosition.Add(zoneInstance);

            return zoneInstance;
        }

        /// <summary>
        /// Create a simple Zone object from a ZoneInstance
        /// </summary>
        public Zone CreateZoneFromInstance(ZoneInstance instance)
        {
            if (instance == null)
            {
                SmartLogger.LogWarning("Cannot create Zone from null ZoneInstance", LogCategory.Zone);
                return null;
            }

            // Generate a unique ID for the zone
            string zoneId = System.Guid.NewGuid().ToString();

            // Get the zone type from the instance's data
            string zoneType = instance.DisplayName;

            // Convert GridPosition to Vector2Int
            Vector2Int position = instance.Position.ToVector2Int();

            // Create the zone object
            GameObject zoneObj = new GameObject($"Zone_{zoneType}_{zoneId.Substring(0, 8)}");
            zoneObj.transform.SetParent(zonesParent);

            // Add and initialize the Zone component
            Zone zone = zoneObj.AddComponent<Zone>();
            zone.Initialize(
                zoneId,
                zoneType,
                position,
                instance.Radius,
                instance.RemainingDuration,
                instance.OwnerUnitId.ToString()
            );

            // Add to active zones
            activeZones[zoneId] = zone;

            return zone;
        }

        /// <summary>
        /// Get a zone by ID
        /// </summary>
        public Zone GetZone(string zoneId)
        {
            if (activeZones.TryGetValue(zoneId, out Zone zone))
            {
                return zone;
            }
            return null;
        }
        
        /// <summary>
        /// Get all currently active zones
        /// </summary>
        public List<Zone> GetAllZones()
        {
            return new List<Zone>(activeZones.Values);
        }
        
        /// <summary>
        /// Get zones at a specific position
        /// </summary>
        public List<Zone> GetZonesAtPosition(Vector2Int position)
        {
            List<Zone> result = new List<Zone>();
            
            foreach (var zone in activeZones.Values)
            {
                if (zone.Position == position)
                {
                    result.Add(zone);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Remove a zone by ID
        /// </summary>
        public void RemoveZone(string zoneId)
        {
            if (activeZones.TryGetValue(zoneId, out Zone zone))
            {
                Destroy(zone.gameObject);
                activeZones.Remove(zoneId);
            }
        }
        
        /// <summary>
        /// Update zones at the end of a turn
        /// </summary>
        public void ProcessTurnEnd()
        {
            List<string> expiredZoneIds = new List<string>();
            
            // Decrement duration for all zones
            foreach (var zone in activeZones.Values)
            {
                zone.DecrementDuration();
                
                // Track expired zones
                if (zone.IsExpired())
                {
                    expiredZoneIds.Add(zone.ZoneId);
                }
            }
            
            // Remove expired zones
            foreach (string zoneId in expiredZoneIds)
            {
                RemoveZone(zoneId);
            }
        }
        
        /// <summary>
        /// Process turn end effects for all active zones
        /// </summary>
        public void ProcessTurnResolutionEnd()
        {
            SmartLogger.Log("[ZoneManager.ProcessTurnResolutionEnd ENTRY]", LogCategory.Zone, this);
            SmartLogger.Log($"[ZoneManager.ProcessTurnResolutionEnd] Iterating through {zonesByPosition.Count} positions with zones.", LogCategory.Zone, this);

            int zonesProcessedCount = 0;

            foreach (var zonesAtPositionList in zonesByPosition.Values)
            {
                if (zonesAtPositionList != null)
                {
                    foreach (var zoneInstance in zonesAtPositionList.ToList())
                    {
                        // --- Ensure this debug log is at the start of the inner loop ---
                        SmartLogger.Log($"[ZoneManager.ProcessTurnResolutionEnd LOOP] Checking ZoneInstance {zoneInstance?.DisplayName ?? "NULL_ZONE"} (Instance:{zoneInstance?.GetInstanceID().ToString() ?? "NULL_INSTANCE_ID"}) at position.", LogCategory.Zone, this);
                        // --- End ensure debug log ---
                        if (zoneInstance != null && zoneInstance.IsActive && zoneInstance.gameObject != null && zoneInstance.gameObject.activeInHierarchy && zoneInstance.enabled)
                        {
                            // --- Ensure this debug log inside the valid zone check exists ---
                            SmartLogger.Log($"[ZoneManager.ProcessTurnResolutionEnd] ZoneInstance {zoneInstance.DisplayName} is valid and active. Calling ApplyTurnEndEffects().", LogCategory.Zone);
                            // --- End ensure debug log ---
                            try
                            {
                                zoneInstance.ApplyTurnEndEffects();
                                zonesProcessedCount++;
                            }
                            catch (System.Exception e)
                            {
                                SmartLogger.LogError($"[ProcessTurnResolutionEnd] Error calling ApplyTurnEndEffects for ZoneInstance {zoneInstance.DisplayName}: {e.Message}\n{e.StackTrace}", LogCategory.Zone, this);
                            }
                        }
                        else
                        {
                            // --- Ensure this log is informative about why it was skipped ---
                            string zoneName = zoneInstance?.DisplayName ?? "NULL_ZONE";
                            string instanceId = zoneInstance?.GetInstanceID().ToString() ?? "NULL_INSTANCE_ID";
                            bool isZoneNull = zoneInstance == null;
                            bool isGameObjectNull = zoneInstance?.gameObject == null;
                            bool isGameObjectActive = zoneInstance?.gameObject?.activeInHierarchy ?? false;
                            bool isZoneInstanceEnabled = zoneInstance?.enabled ?? false;
                            SmartLogger.LogWarning($"[ProcessTurnResolutionEnd] Skipping ApplyTurnEndEffects for ZoneInstance: ID={instanceId}, Name={zoneName}. Reason: IsNull={isZoneNull}, IsActive={(zoneInstance?.IsActive ?? false)}, IsGameObjectNull={isGameObjectNull}, IsGameObjectActive={isGameObjectActive}, IsComponentEnabled={isZoneInstanceEnabled}", LogCategory.Zone, this);
                            // --- End ensure log ---
                        }
                    }
                }
            }
            SmartLogger.Log($"[ZoneManager.ProcessTurnResolutionEnd EXIT] Called ApplyTurnEndEffects on {zonesProcessedCount} zones.", LogCategory.Zone, this);
        }
        
        /// <summary>
        /// Clear all active zones
        /// </summary>
        public void ClearAllActiveZones()
        {
            foreach (var zone in new List<Zone>(activeZones.Values))
            {
                Destroy(zone.gameObject);
            }
            
            activeZones.Clear();
        }

        /// <summary>
        /// Find the first active ZoneInstance whose area contains the given position.
        /// </summary>
        public ZoneInstance FindActiveZoneInstanceAtPosition(GridPosition position)
        {
            SmartLogger.Log($"[ZoneManager.FindActiveZoneInstanceAtPosition] Searching for active zone at position {position}", LogCategory.Zone, this);
            if (zonesByPosition != null)
            {
                foreach (var zonesListAtPos in zonesByPosition.Values)
                {
                    if (zonesListAtPos != null)
                    {
                        foreach (var zone in zonesListAtPos)
                        {
                            if (zone != null && zone.IsActive && zone.ContainsPosition(position))
                            {
                                SmartLogger.Log($"[ZoneManager.FindActiveZoneInstanceAtPosition] Found active zone '{zone.DisplayName}' ({zone.Id}) containing position {position}.", LogCategory.Zone, this);
                                return zone;
                            }
                        }
                    }
                }
            }
            else
            {
                SmartLogger.LogError("[ZoneManager.FindActiveZoneInstanceAtPosition] zonesByPosition dictionary is null!", LogCategory.Zone, this);
            }
            SmartLogger.Log($"[ZoneManager.FindActiveZoneInstanceAtPosition] No active zone found containing position {position}.", LogCategory.Zone, this);
            return null;
        }

        /// <summary>
        /// Returns a list of all currently active ZoneInstance objects.
        /// </summary>
        public List<ZoneInstance> GetAllActiveZoneInstances()
        {
            SmartLogger.Log($"[ZoneManager.GetAllActiveZoneInstances] Collecting active zone instances.", LogCategory.Zone, this);
            List<ZoneInstance> activeInstances = new List<ZoneInstance>();

            if (zonesByPosition != null)
            {
                // Iterate through all lists of zones stored by position
                foreach (var zonesListAtPos in zonesByPosition.Values)
                {
                    if (zonesListAtPos != null)
                    {
                        // Add all active ZoneInstances from this position's list
                        activeInstances.AddRange(zonesListAtPos.Where(zone => zone != null && zone.IsActive));
                    }
                }
            }
            else
            {
                SmartLogger.LogWarning("[ZoneManager.GetAllActiveZoneInstances] zonesByPosition dictionary is null!", LogCategory.Zone, this);
            }

            SmartLogger.Log($"[ZoneManager.GetAllActiveZoneInstances] Found {activeInstances.Count} active zone instances.", LogCategory.Zone, this);
            return activeInstances;
        }
    }
} 
