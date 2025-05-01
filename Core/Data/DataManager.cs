using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Dokkaebi.Core.Data;
using Dokkaebi.Utilities; // Added for SmartLogger and LogCategory

namespace Dokkaebi.Core.Data
{
    /// <summary>
    /// Manages loading and providing access to all ScriptableObject data assets.
    /// Implements singleton pattern for global access.
    /// </summary>
    public class DataManager : MonoBehaviour
    {
        private static DataManager instance;
        public static DataManager Instance
        {
            get
            {
                // Log when the Instance getter is accessed
                SmartLogger.Log($"[DataManager.Instance] Getter accessed. Instance is null: {instance == null}", LogCategory.Game, instance);

                if (instance == null)
                {
                    var go = new GameObject("DataManager");
                    instance = go.AddComponent<DataManager>();
                    // Use DontDestroyOnLoad on the root GameObject
                    if (go.transform.parent == null)
                    {
                        DontDestroyOnLoad(go);
                        SmartLogger.Log("[DataManager.Instance] Created new DataManager instance and applied DontDestroyOnLoad.", LogCategory.Game, instance);
                    }
                    else
                    {
                        SmartLogger.LogWarning("[DataManager.Instance] Created new DataManager instance but it is not a root GameObject. DontDestroyOnLoad was NOT applied.", LogCategory.Game, instance);
                    }
                }
                return instance;
            }
        }

        [Header("Data Assets")]
        [SerializeField] private List<OriginData> originDataAssets;
        [SerializeField] private List<CallingData> callingDataAssets;
        [SerializeField] private List<AbilityData> abilityDataAssets;
        [SerializeField] private List<ZoneData> zoneDataAssets;
        [SerializeField] private List<StatusEffectData> statusEffectDataAssets;
        [SerializeField] private UnitSpawnData unitSpawnData; // Assigned in Inspector

        // Cached dictionaries for quick lookup
        private Dictionary<string, OriginData> originDataLookup;
        private Dictionary<string, CallingData> callingDataLookup;
        private Dictionary<string, AbilityData> abilityDataLookup;
        private Dictionary<string, ZoneData> zoneDataLookup;
        private Dictionary<string, StatusEffectData> statusEffectDataLookup;

        private void Awake()
        {
            SmartLogger.Log($"[DataManager.Awake] Awake() called on GameObject: {gameObject.name} (InstanceID: {gameObject.GetInstanceID()})", LogCategory.Game, this);

            if (instance != null && instance != this)
            {
                SmartLogger.LogWarning($"[DataManager.Awake] DUPLICATE INSTANCE DETECTED on {gameObject.name} (InstanceID: {gameObject.GetInstanceID()})! Destroying this duplicate. The original singleton is on {instance.gameObject.name}", LogCategory.Game, this);
                Destroy(gameObject);
                return;
            }

            instance = this;
             // Use DontDestroyOnLoad on the root GameObject
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
                SmartLogger.Log("[DataManager.Awake] Singleton instance set and applied DontDestroyOnLoad.", LogCategory.Game, this);
            }
            else
            {
                SmartLogger.LogWarning("[DataManager.Awake] Singleton instance set but it is not a root GameObject. DontDestroyOnLoad was NOT applied.", LogCategory.Game, this);
            }


            SmartLogger.Log("[DataManager.Awake] About to call InitializeDataLookups().", LogCategory.Game, this);
            InitializeDataLookups();
            SmartLogger.Log("[DataManager.Awake] InitializeDataLookups() called.", LogCategory.Game, this);

            SmartLogger.Log("[DataManager.Awake] Awake() completed.", LogCategory.Game, this);
        }

        private void InitializeDataLookups()
        {
            SmartLogger.Log("[DataManager.InitializeDataLookups] Method entered.", LogCategory.Game, this);

            // Initialize origin data lookup
            originDataLookup = originDataAssets?.ToDictionary(x => x.originId) ?? new Dictionary<string, OriginData>();

            // Initialize calling data lookup
            callingDataLookup = callingDataAssets?.ToDictionary(x => x.callingId) ?? new Dictionary<string, CallingData>();

            // Initialize ability data lookup
            abilityDataLookup = abilityDataAssets?.ToDictionary(x => x.abilityId) ?? new Dictionary<string, AbilityData>();

            // Initialize zone data lookup
            zoneDataLookup = zoneDataAssets?.ToDictionary(x => x.zoneId) ?? new Dictionary<string, ZoneData>();

            // Initialize status effect data lookup
            statusEffectDataLookup = statusEffectDataAssets?.ToDictionary(x => x.effectId) ?? new Dictionary<string, StatusEffectData>();

            // Log confirmation that other lookups are complete
            SmartLogger.Log("[DataManager.InitializeDataLookups] Other lookup dictionaries initialized.", LogCategory.Game, this);

            // Log the state of the unitSpawnData field (assigned in Inspector)
            SmartLogger.Log("[DataManager.InitializeDataLookups] Checking unitSpawnData field (assigned in Inspector).", LogCategory.Game, this);
            SmartLogger.Log($"[DataManager.InitializeDataLookups] unitSpawnData field is null: {unitSpawnData == null}. {(unitSpawnData != null ? $"Name: {unitSpawnData.name}" : "")}", LogCategory.Game, this);

            SmartLogger.Log("[DataManager.InitializeDataLookups] Method completed.", LogCategory.Game, this);
        }

        #region Data Access Methods

        public OriginData GetOriginData(string originId)
        {
            if (originDataLookup.TryGetValue(originId, out var data))
                return data;

            Debug.LogError($"Origin data not found for ID: {originId}");
            return null;
        }

        public CallingData GetCallingData(string callingId)
        {
            if (callingDataLookup.TryGetValue(callingId, out var data))
                return data;

            Debug.LogError($"Calling data not found for ID: {callingId}");
            return null;
        }

        public AbilityData GetAbilityData(string abilityId)
        {
            if (abilityDataLookup.TryGetValue(abilityId, out var data))
            {
                // --- LOG: Trace movementType for FlameLunge ---
                if (data != null && data.abilityId == "FlameLunge")
                {
SmartLogger.Log($"[DataManager.GetAbilityData] Retrieved FlameLunge ability data. Movement Type: {data.movementType}", LogCategory.Game, this);                }
                return data;
            }
            return null;
        }

        public ZoneData GetZoneData(string zoneId)
        {
            if (zoneDataLookup.TryGetValue(zoneId, out var data))
                return data;

            Debug.LogError($"Zone data not found for ID: {zoneId}");
            return null;
        }

        public StatusEffectData GetStatusEffectData(string effectId)
        {
            if (statusEffectDataLookup.TryGetValue(effectId, out var data))
                return data;

            Debug.LogError($"Status effect data not found for ID: {effectId}");
            return null;
        }

        public UnitSpawnData GetUnitSpawnData() {
            SmartLogger.Log("[DataManager.GetUnitSpawnData] Method entered.", LogCategory.Game, this);
            SmartLogger.Log($"[DataManager.GetUnitSpawnData] Returning unitSpawnData. Is null: {unitSpawnData == null}", LogCategory.Game, this);
            return unitSpawnData;
        }

        #endregion

        #region Validation Methods

        public bool ValidateOriginId(string originId) => originDataLookup.ContainsKey(originId);
        public bool ValidateCallingId(string callingId) => callingDataLookup.ContainsKey(callingId);
        public bool ValidateAbilityId(string abilityId) => abilityDataLookup.ContainsKey(abilityId);
        public bool ValidateZoneId(string zoneId) => zoneDataLookup.ContainsKey(zoneId);
        public bool ValidateStatusEffectId(string effectId) => statusEffectDataLookup.ContainsKey(effectId);

        #endregion
    }
}
