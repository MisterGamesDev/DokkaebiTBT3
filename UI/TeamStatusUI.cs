using UnityEngine;
using System.Collections.Generic;
using Dokkaebi.Core; // For UnitManager
using Dokkaebi.Units; // For DokkaebiUnit
using Dokkaebi.Utilities; // For SmartLogger

namespace Dokkaebi.UI
{
    /// <summary>
    /// Manages the instantiation and arrangement of UnitStatusOverlayItems
    /// for both player and enemy teams.
    /// </summary>
    public class TeamStatusUI : MonoBehaviour
    {
        [Header("UI Containers")]
        [SerializeField] private RectTransform playerTeamContainer; // Assign the parent RectTransform for player units
        [SerializeField] private RectTransform enemyTeamContainer;  // Assign the parent RectTransform for enemy units

        [Header("Prefab")]
        [SerializeField] private GameObject unitStatusItemPrefab; // Assign the UnitStatusOverlayItem prefab

        // Keep track of instantiated items to manage them (e.g., on unit defeat)
        private Dictionary<int, UnitStatusOverlayItem> playerUnitItems = new Dictionary<int, UnitStatusOverlayItem>();
        private Dictionary<int, UnitStatusOverlayItem> enemyUnitItems = new Dictionary<int, UnitStatusOverlayItem>();

        private UnitManager _unitManager;

        void Awake()
        {
            SmartLogger.Log($"[TeamStatusUI] Awake() called on GameObject: {gameObject.name}", LogCategory.UI, this);

            _unitManager = UnitManager.Instance;
            SmartLogger.Log($"[TeamStatusUI] UnitManager instance found: {_unitManager != null}", LogCategory.UI, this);

            bool containersOk = playerTeamContainer != null && enemyTeamContainer != null;
            bool prefabOk = unitStatusItemPrefab != null;
            SmartLogger.Log($"[TeamStatusUI] UI references - playerTeamContainer: {playerTeamContainer != null}, enemyTeamContainer: {enemyTeamContainer != null}, unitStatusOverlayItemPrefab: {unitStatusItemPrefab != null}", LogCategory.UI, this);

            SmartLogger.Log("[TeamStatusUI] Awake() completed.", LogCategory.UI, this);
        }

        void Start()
        {
            SmartLogger.Log("[TeamStatusUI] Start() called.", LogCategory.UI, this);

            // Basic validation
            if (playerTeamContainer == null || enemyTeamContainer == null || unitStatusItemPrefab == null)
            {
                SmartLogger.LogError("[TeamStatusUI] Required UI containers or prefab not assigned in the Inspector!", LogCategory.UI, this);
                return;
            }
            if (unitStatusItemPrefab.GetComponent<UnitStatusOverlayItem>() == null)
            {
                SmartLogger.LogError("[TeamStatusUI] Assigned prefab is missing the UnitStatusOverlayItem script!", LogCategory.UI, this);
                return;
            }

            if (_unitManager == null)
            {
                SmartLogger.LogError("[TeamStatusUI] UnitManager instance not found! Make sure it exists in the scene.", LogCategory.UI, this);
                return;
            }

            _unitManager.OnUnitDefeated += HandleUnitDefeated;
            SmartLogger.Log("[TeamStatusUI] Subscribed to OnUnitDefeated event.", LogCategory.UI, this);

            SmartLogger.Log("[TeamStatusUI] Start() completed.", LogCategory.UI, this);
        }

        void OnDestroy()
        {
            // Unsubscribe from events
            if (_unitManager != null)
            {
                _unitManager.OnUnitDefeated -= HandleUnitDefeated;
            }
        }

        /// <summary>
        /// Populates the UI for both player and enemy teams.
        /// </summary>
        public void PopulateTeamStatusUI()
        {
            SmartLogger.Log("[TeamStatusUI] PopulateTeamStatusUI() called (external or internal).", LogCategory.UI, this);

            List<DokkaebiUnit> playerUnits = _unitManager.GetUnitsByPlayer(true);
            List<DokkaebiUnit> enemyUnits = _unitManager.GetUnitsByPlayer(false);
            SmartLogger.Log($"[TeamStatusUI] Found {playerUnits?.Count ?? 0} player units and {enemyUnits?.Count ?? 0} enemy units.", LogCategory.UI, this);
            SmartLogger.Log($"[TeamStatusUI] Raw counts - playerUnits.Count: {playerUnits?.Count ?? 0}, enemyUnits.Count: {enemyUnits?.Count ?? 0}", LogCategory.UI, this);

            foreach (var item in playerUnitItems.Values) Destroy(item.gameObject);
            foreach (var item in enemyUnitItems.Values) Destroy(item.gameObject);
            playerUnitItems.Clear();
            enemyUnitItems.Clear();

            if (playerUnits != null)
            {
                foreach (DokkaebiUnit unit in playerUnits)
                {
                    SmartLogger.Log($"[TeamStatusUI] Considering PLAYER unit: Name='{unit?.name}', ID={unit?.UnitId}", LogCategory.UI, this);
                    if (unit != null && !playerUnitItems.ContainsKey(unit.UnitId))
                    {
                        CreateUnitStatusItem(unit, playerTeamContainer.gameObject, playerUnitItems);
                    }
                }
            }
            if (enemyUnits != null)
            {
                foreach (DokkaebiUnit unit in enemyUnits)
                {
                    SmartLogger.Log($"[TeamStatusUI] Considering ENEMY unit: Name='{unit?.name}', ID={unit?.UnitId}", LogCategory.UI, this);
                    if (unit != null && !enemyUnitItems.ContainsKey(unit.UnitId))
                    {
                        CreateUnitStatusItem(unit, enemyTeamContainer.gameObject, enemyUnitItems);
                    }
                }
            }

            SmartLogger.Log("[TeamStatusUI] PopulateTeamStatusUI() completed.", LogCategory.UI, this);
        }

        /// <summary>
        /// Instantiates a UnitStatusOverlayItem for a unit and adds it to the specified container and dictionary.
        /// </summary>
        private void CreateUnitStatusItem(DokkaebiUnit unit, GameObject container, Dictionary<int, UnitStatusOverlayItem> itemDictionary)
        {
            SmartLogger.Log($"[TeamStatusUI] >> CreateUnitStatusItem entered for unit: Name='{unit?.name}', ID={unit?.UnitId}, Container='{container?.name}'", LogCategory.UI, this);

            if (unit == null)
            {
                SmartLogger.LogWarning("[TeamStatusUI] Attempted to create status item for null unit.", LogCategory.UI, this);
                SmartLogger.Log($"[TeamStatusUI] << CreateUnitStatusItem exited (null unit).", LogCategory.UI, this);
                return;
            }
            if (unitStatusItemPrefab == null)
            {
                SmartLogger.LogError("[TeamStatusUI] unitStatusOverlayItemPrefab is null!", LogCategory.UI, this);
                SmartLogger.Log($"[TeamStatusUI] << CreateUnitStatusItem exited (null prefab).", LogCategory.UI, this);
                return;
            }
            if (container == null)
            {
                SmartLogger.LogError($"[TeamStatusUI] Target container is null for unit: Name='{unit.name}', ID={unit.UnitId}", LogCategory.UI, this);
                SmartLogger.Log($"[TeamStatusUI] << CreateUnitStatusItem exited (null container).", LogCategory.UI, this);
                return;
            }

            SmartLogger.Log($"[TeamStatusUI] Instantiating prefab '{unitStatusItemPrefab.name}' in container '{container.name}'...", LogCategory.UI, this);
            GameObject itemObject = Instantiate(unitStatusItemPrefab, container.transform);
            SmartLogger.Log($"[TeamStatusUI] Instantiated GameObject: Name='{itemObject?.name}', InstanceID={itemObject?.GetInstanceID()}", LogCategory.UI, this);

            if (itemObject == null)
            {
                 SmartLogger.LogError($"[TeamStatusUI] Instantiate returned null for prefab '{unitStatusItemPrefab.name}'! Unit: Name='{unit.name}', ID={unit.UnitId}", LogCategory.UI, this);
                 SmartLogger.Log($"[TeamStatusUI] << CreateUnitStatusItem exited (instantiation failed).", LogCategory.UI, this);
                 return;
            }

            UnitStatusOverlayItem statusItem = itemObject.GetComponent<UnitStatusOverlayItem>();
            SmartLogger.Log($"[TeamStatusUI] GetComponent<UnitStatusOverlayItem> result: {(statusItem != null ? "Found" : "NOT FOUND")}", LogCategory.UI, this);

            if (statusItem != null)
            {
                SmartLogger.Log($"[TeamStatusUI] Calling statusItem.Setup() for unit: Name='{unit.name}', ID={unit.UnitId}", LogCategory.UI, this);
                statusItem.Setup(unit);
                itemDictionary[unit.UnitId] = statusItem;
                SmartLogger.Log($"[TeamStatusUI] Setup successful and item added to dictionary for unit: Name='{unit.name}', ID={unit.UnitId}", LogCategory.UI, this);
            }
            else
            {
                SmartLogger.LogError($"[TeamStatusUI] UnitStatusOverlayItem component MISSING on instantiated prefab ('{itemObject.name}') for unit: Name='{unit.name}', ID={unit.UnitId}. Destroying object.", LogCategory.UI, this);
                Destroy(itemObject);
            }

            SmartLogger.Log($"[TeamStatusUI] << CreateUnitStatusItem exited.", LogCategory.UI, this);
        }

        /// <summary>
        /// Handles the event when a unit is defeated.
        /// Finds the corresponding UI item and handles its removal or visual state change.
        /// </summary>
        /// <param name="defeatedUnit">The unit that was defeated.</param>
        private void HandleUnitDefeated(DokkaebiUnit defeatedUnit)
        {
            if (defeatedUnit == null) return;

            int unitId = defeatedUnit.UnitId;
            SmartLogger.Log($"[TeamStatusUI] Handling defeat for Unit ID: {unitId}", LogCategory.UI);

            Dictionary<int, UnitStatusOverlayItem> targetDictionary = defeatedUnit.IsPlayerControlled ? playerUnitItems : enemyUnitItems;

            if (targetDictionary.TryGetValue(unitId, out UnitStatusOverlayItem itemToHandle))
            {
                SmartLogger.Log($"[TeamStatusUI] Found UI item for defeated Unit ID: {unitId}. Destroying GameObject.", LogCategory.UI);
                // Option 1: Destroy the item's GameObject
                Destroy(itemToHandle.gameObject);
                targetDictionary.Remove(unitId);

                // Option 2: If UnitStatusOverlayItem handles its own visual state on defeat,
                // you might not need to do anything here, or just remove it from the dictionary.
                // targetDictionary.Remove(unitId);
            }
            else
            {
                 SmartLogger.LogWarning($"[TeamStatusUI] Could not find UI item for defeated Unit ID: {unitId}. It might have already been removed or never created.", LogCategory.UI);
            }
        }
    }
} 