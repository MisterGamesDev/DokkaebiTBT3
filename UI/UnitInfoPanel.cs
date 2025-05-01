using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using Dokkaebi.Interfaces;
using Dokkaebi.Common;
using Dokkaebi.Units;
using Dokkaebi.Core;
using Dokkaebi.Utilities;
using Dokkaebi.Core.Data;

namespace Dokkaebi.UI
{
    [System.Serializable]
    public struct UnitNameImage
    {
        public string unitNameId;
        public UnityEngine.UI.Image nameImage;
    }

    public class UnitInfoPanel : MonoBehaviour
    {
        [Header("Unit Basic Info")]
        [SerializeField] private List<UnitNameImage> unitNameImagesList = new List<UnitNameImage>();
        [SerializeField] private UnityEngine.UI.Image originIconImage;
        [SerializeField] private Image portraitImage;

        [Header("Calling Icons")]
        [SerializeField] private UnityEngine.UI.Image callingIcon_Duelist;
        [SerializeField] private UnityEngine.UI.Image callingIcon_Mage;
        [SerializeField] private UnityEngine.UI.Image callingIcon_Marksman;
        [SerializeField] private UnityEngine.UI.Image callingIcon_Guardian;
        [SerializeField] private UnityEngine.UI.Image callingIcon_Controller;
        [SerializeField] private UnityEngine.UI.Image callingIcon_Specialist;

        [Header("Stats")]
        [SerializeField] private Slider hpSlider;
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private Slider auraSlider;
        [SerializeField] private TextMeshProUGUI auraText;

        [Header("Status Effects")]
        [SerializeField] private Transform statusEffectContainer;
        [SerializeField] private GameObject statusEffectPrefab;

        private IDokkaebiUnit currentUnit;
        private Dictionary<StatusEffectType, GameObject> activeStatusEffects = new Dictionary<StatusEffectType, GameObject>();
        private Dictionary<string, UnityEngine.UI.Image> unitNameImagesLookup = new Dictionary<string, UnityEngine.UI.Image>();
        private bool isInitialized = false;

        private void Start()
        {
            // SmartLogger.Log("[UnitInfoPanel Start] Initializing panel...", LogCategory.UI, this);
            gameObject.SetActive(false);
            currentUnit = null;
            ClearAllDisplays();
            InitializeComponents();
            isInitialized = true;
            // SmartLogger.Log($"[UnitInfoPanel Start] Panel initialized. activeSelf: {gameObject.activeSelf}", LogCategory.UI, this);
        }

        private void OnEnable()
        {
            SmartLogger.Log($"[UnitInfoPanel OnEnable] Panel enabled. InstanceID: {GetInstanceID()}, Current Unit: {(currentUnit != null ? currentUnit.DisplayName : "NULL")}, Is Player Controlled: {(currentUnit is DokkaebiUnit du ? du.IsPlayerControlled.ToString() : "N/A")}, Called from SetUnit: {Time.frameCount}", LogCategory.UI, this);
        }

        private void OnDisable()
        {
            SmartLogger.Log($"[UnitInfoPanel OnDisable] Panel disabled. InstanceID: {GetInstanceID()}, Current Unit: {(currentUnit != null ? currentUnit.DisplayName : "NULL")}, Is Player Controlled: {(currentUnit is DokkaebiUnit du ? du.IsPlayerControlled.ToString() : "N/A")}, Frame: {Time.frameCount}", LogCategory.UI, this);
            if (currentUnit != null)
            {
                // SmartLogger.Log($"[UnitInfoPanel OnDisable] Unsubscribing from events for unit: {currentUnit.GetUnitName()}", LogCategory.UI, this);
                UnsubscribeFromUnitEvents(currentUnit);
            }
        }

        private void OnDestroy()
        {
            // SmartLogger.Log($"[UnitInfoPanel OnDestroy] Panel being destroyed. InstanceID: {GetInstanceID()}, Current Unit: {(currentUnit != null ? currentUnit.DisplayName : "NULL")}", LogCategory.UI, this);
            if (currentUnit != null)
            {
                // SmartLogger.Log($"[UnitInfoPanel OnDestroy] Unsubscribing from events for unit: {currentUnit.GetUnitName()}", LogCategory.UI, this);
                UnsubscribeFromUnitEvents(currentUnit);
            }
        }

        public void SetUnit(IDokkaebiUnit unit)
        {
            // 1. Log entry
            string entryUnitName = unit?.DisplayName ?? "NULL";
            SmartLogger.Log($"[UnitInfoPanel.SetUnit ENTRY] Method entered. Unit='{entryUnitName}', InstanceID={GetInstanceID()}, Frame={Time.frameCount}", LogCategory.UI, this);

            // 2. Log same unit check
            if (currentUnit == unit)
            {
                SmartLogger.Log($"[UnitInfoPanel.SetUnit] Same unit provided ('{entryUnitName}'). Returning early.", LogCategory.UI, this);
                return;
            }
            else
            {
                 SmartLogger.Log($"[UnitInfoPanel.SetUnit] Different unit provided. Previous='{currentUnit?.DisplayName ?? "NULL"}', New='{entryUnitName}'. Proceeding.", LogCategory.UI, this);
            }

            // Clear all displays FIRST, before any other operations
            // SmartLogger.Log("[UnitInfoPanel SetUnit] Initiating display clear sequence", LogCategory.UI, this);
            ClearAllDisplays();
            // SmartLogger.Log("[UnitInfoPanel SetUnit] Display clear sequence completed", LogCategory.UI, this);

            // 3. Log before unsubscribe
            if (currentUnit != null)
            {
                SmartLogger.Log($"[UnitInfoPanel.SetUnit] Before Unsubscribe: Unsubscribing from previous unit events: '{currentUnit.DisplayName}'", LogCategory.UI, this);
                UnsubscribeFromUnitEvents(currentUnit);
            }

            // 4. Log before setting currentUnit
            string newUnitNameLog = unit?.DisplayName ?? "NULL";
            SmartLogger.Log($"[UnitInfoPanel.SetUnit] Before Update Reference: Setting currentUnit reference to: '{newUnitNameLog}'", LogCategory.UI, this);
            currentUnit = unit;
            
            // 5. Inside if (currentUnit != null)
            if (currentUnit != null)
            {
                bool isPlayerControlled = (currentUnit is DokkaebiUnit du && du.IsPlayerControlled);
                SmartLogger.Log($"[UnitInfoPanel.SetUnit] Setting up new unit '{currentUnit.DisplayName}' (Player: {isPlayerControlled}).", LogCategory.UI, this);

                SmartLogger.Log($"[UnitInfoPanel.SetUnit] Before Subscribe: Subscribing to new unit events.", LogCategory.UI, this);
                SubscribeToUnitEvents(currentUnit);
                
                SmartLogger.Log($"[UnitInfoPanel.SetUnit] Before UpdateAllInfo: Updating display elements.", LogCategory.UI, this);
                UpdateAllInfo(); // This will handle displaying name image, stats, etc.
                
                // Activate the panel if it's currently inactive
                bool isActiveBeforeCheck = gameObject.activeSelf;
                SmartLogger.Log($"[UnitInfoPanel.SetUnit] Before Activation Check: Panel activeSelf state is: {isActiveBeforeCheck}", LogCategory.UI, this);
                if (!isActiveBeforeCheck)
                {
                    SmartLogger.Log($"[UnitInfoPanel.SetUnit] Inside Activation Check: Panel is inactive, calling SetActive(true).", LogCategory.UI, this);
                    gameObject.SetActive(true);
                    SmartLogger.Log($"[UnitInfoPanel.SetUnit] After Activation: Panel activation complete, activeSelf is now: {gameObject.activeSelf}", LogCategory.UI, this);
                }
                else
                {
                    SmartLogger.Log($"[UnitInfoPanel.SetUnit] Inside Activation Check: Panel was already active. Updating info.", LogCategory.UI, this);
                }

                // Handle portrait (already exists, should work for enemies if they have data)
                if (currentUnit is DokkaebiUnit concreteUnit)
                {
                    if (portraitImage != null && concreteUnit.GetUnitDefinitionData() != null)
                    {
                        var def = concreteUnit.GetUnitDefinitionData();
                        var sprite = def.icon;
                        portraitImage.sprite = sprite;
                        portraitImage.enabled = sprite != null;
                    }
                    else if (portraitImage != null)
                    {
                        portraitImage.sprite = null;
                        portraitImage.enabled = false;
                    }
                }
                else if (portraitImage != null) // If not a DokkaebiUnit, clear portrait
                {
                    portraitImage.sprite = null;
                    portraitImage.enabled = false;
                }
            }
            // 6. Inside else (unit == null)
            else
            {
                SmartLogger.Log("[UnitInfoPanel.SetUnit] Null unit provided. Clearing displays and potentially deactivating panel.", LogCategory.UI, this);
                ClearAllDisplays(); // Clear display elements for null unit
                
                bool isActiveBeforeDeactivationCheck = gameObject.activeSelf;
                SmartLogger.Log($"[UnitInfoPanel.SetUnit] Before Deactivation Check: Panel activeSelf state is: {isActiveBeforeDeactivationCheck}", LogCategory.UI, this);
                if (isActiveBeforeDeactivationCheck) // Only deactivate if it's currently active
                {
                    SmartLogger.Log($"[UnitInfoPanel.SetUnit] Inside Deactivation Check: Panel is active, calling SetActive(false).", LogCategory.UI, this);
                    gameObject.SetActive(false);
                    SmartLogger.Log($"[UnitInfoPanel.SetUnit] After Deactivation: Panel deactivation complete, activeSelf is now: {gameObject.activeSelf}", LogCategory.UI, this);
                }
                else
                {
                    SmartLogger.Log("[UnitInfoPanel.SetUnit] Inside Deactivation Check: Panel already inactive. Doing nothing.", LogCategory.UI, this);
                }

                if (portraitImage != null)
                {
                    portraitImage.sprite = null;
                    portraitImage.enabled = false;
                }
                ClearStatusEffects(); // Ensure status effects are cleared when no unit is selected
            }

            // 7. Log exit
            string exitUnitName = currentUnit?.DisplayName ?? "NULL";
            SmartLogger.Log($"[UnitInfoPanel.SetUnit EXIT] Method finished. Current Unit='{exitUnitName}', Final Panel activeSelf={gameObject.activeSelf}, Frame={Time.frameCount}", LogCategory.UI, this);
        }

        private void InitializeComponents()
        {
            // Verify all required components are assigned
            if (/*unitNameText == null ||*/ originIconImage == null ||
                hpText == null || auraText == null || hpSlider == null || auraSlider == null)
            {
                SmartLogger.LogError("One or more required UI components are not assigned!", LogCategory.UI, this);
            }

            if (statusEffectContainer == null || statusEffectPrefab == null)
            {
                SmartLogger.LogError("Status effect display components are not assigned!", LogCategory.UI, this);
            }

            unitNameImagesLookup.Clear();
            if (unitNameImagesList != null)
            {
                foreach (var unitNameImage in unitNameImagesList)
                {
                    if (unitNameImage.nameImage != null && !string.IsNullOrEmpty(unitNameImage.unitNameId) && !unitNameImagesLookup.ContainsKey(unitNameImage.unitNameId))
                    {
                        unitNameImagesLookup.Add(unitNameImage.unitNameId, unitNameImage.nameImage);
                        unitNameImage.nameImage.gameObject.SetActive(false);
                    }
                    else if (unitNameImage.nameImage == null)
                    {
                         SmartLogger.LogWarning($"UnitNameImage entry has null Image component for ID: {unitNameImage.unitNameId ?? "NULL ID"}", LogCategory.UI, this);
                    }
                    else if (string.IsNullOrEmpty(unitNameImage.unitNameId))
                    {
                         SmartLogger.LogWarning("UnitNameImage entry has null or empty unitNameId.", LogCategory.UI, this);
                    }
                    else if (unitNameImagesLookup.ContainsKey(unitNameImage.unitNameId))
                    {
                         SmartLogger.LogWarning($"Duplicate unitNameId '{unitNameImage.unitNameId}' found in unitNameImagesList. Using the first occurrence.", LogCategory.UI, this);
                    }
                }
            }
            else
            {
                 SmartLogger.LogWarning("unitNameImagesList is null during initialization.", LogCategory.UI, this);
            }
        }

        private void ClearAllDisplays()
        {
            SmartLogger.Log($"[UnitInfoPanel ClearAllDisplays ENTRY] Starting clear sequence, Frame: {Time.frameCount}", LogCategory.UI, this);
            
            // Clear status effects first
            // SmartLogger.Log("[UnitInfoPanel ClearAllDisplays] Calling ClearStatusEffects()", LogCategory.UI, this);
            ClearStatusEffects();
            
            // Clear basic info displays
            SmartLogger.Log("[UnitInfoPanel ClearAllDisplays] Clearing name, portrait, origin, calling icons", LogCategory.UI, this);
            if (unitNameImagesList != null)
            {
                 foreach (var unitNameImage in unitNameImagesList)
                 {
                     if (unitNameImage.nameImage != null)
                     {
                         unitNameImage.nameImage.gameObject.SetActive(false);
                     }
                 }
            }

            if (originIconImage != null)
            {
                originIconImage.sprite = null;
                originIconImage.enabled = false;
            }
            DisableAllCallingIconImages();
            // Clear Portrait
            if (portraitImage != null)
            {
                portraitImage.sprite = null;
                portraitImage.enabled = false;
            }

            // Clear Stats
            SmartLogger.Log("[UnitInfoPanel ClearAllDisplays] Clearing HP and Aura displays", LogCategory.UI, this);
            if (hpText != null) hpText.text = "0/0";
            if (auraText != null) auraText.text = "0/0";
            if (hpSlider != null) hpSlider.value = 0;
            if (auraSlider != null) auraSlider.value = 0;
            
            // SmartLogger.Log("[UnitInfoPanel ClearAllDisplays] Clear sequence completed", LogCategory.UI, this);
        }

        private void SubscribeToUnitEvents(IDokkaebiUnit unit)
        {
            if (unit is IUnitEventHandler eventHandler)
            {
                SmartLogger.Log($"[UnitInfoPanel SubscribeToUnitEvents] Subscribing to HP/Status events for unit: {unit.DisplayName}", LogCategory.UI, this);
                eventHandler.OnDamageTaken += HandleDamageTaken;
                eventHandler.OnHealingReceived += HandleHealingReceived;
                eventHandler.OnStatusEffectApplied += HandleStatusEffectApplied;
                eventHandler.OnStatusEffectRemoved += HandleStatusEffectRemoved;
                
                // Subscribe to unit-specific Aura changes
                if (unit is DokkaebiUnit dokkaebiUnit)
                {
                    SmartLogger.Log($"[UnitInfoPanel SubscribeToUnitEvents] Subscribing to Aura events for unit: {unit.DisplayName}", LogCategory.UI, this);
                    dokkaebiUnit.OnUnitAuraChanged += HandleUnitAuraChanged;
                }
            }
            else
            {
                SmartLogger.LogWarning($"[UnitInfoPanel SubscribeToUnitEvents] Unit {unit.DisplayName} does not implement IUnitEventHandler, cannot subscribe to events.", LogCategory.UI, this);
            }
        }

        private void UnsubscribeFromUnitEvents(IDokkaebiUnit unit)
        {
            if (unit is IUnitEventHandler eventHandler)
            {
                SmartLogger.Log($"[UnitInfoPanel UnsubscribeFromUnitEvents] Unsubscribing from HP/Status events for unit: {unit.DisplayName}", LogCategory.UI, this);
                eventHandler.OnDamageTaken -= HandleDamageTaken;
                eventHandler.OnHealingReceived -= HandleHealingReceived;
                eventHandler.OnStatusEffectApplied -= HandleStatusEffectApplied;
                eventHandler.OnStatusEffectRemoved -= HandleStatusEffectRemoved;
                
                // Unsubscribe from unit-specific Aura changes
                if (unit is DokkaebiUnit dokkaebiUnit)
                {
                    SmartLogger.Log($"[UnitInfoPanel UnsubscribeFromUnitEvents] Unsubscribing from Aura events for unit: {unit.DisplayName}", LogCategory.UI, this);
                    dokkaebiUnit.OnUnitAuraChanged -= HandleUnitAuraChanged;
                }
            }
             else
            {
                SmartLogger.LogWarning($"[UnitInfoPanel UnsubscribeFromUnitEvents] Unit {unit.DisplayName} does not implement IUnitEventHandler, cannot unsubscribe from events.", LogCategory.UI, this);
            }
        }

        private void UpdateAllInfo()
        {
            if (!isInitialized) return;
            if (currentUnit == null)
            {
                 SmartLogger.LogWarning("[UnitInfoPanel UpdateAllInfo] Called with null currentUnit.", LogCategory.UI, this);
                 ClearAllDisplays(); // Ensure display is clear if unit becomes null unexpectedly
                 return;
            }

            bool isPlayer = (currentUnit is DokkaebiUnit du && du.IsPlayerControlled);
            SmartLogger.Log($"[UnitInfoPanel UpdateAllInfo START] Updating info for unit: {currentUnit.DisplayName} (Player: {isPlayer})", LogCategory.UI, this);

            // Clear previous state before applying new info
            ClearStatusEffects();
            DisableAllCallingIconImages();
            if (originIconImage != null) originIconImage.enabled = false;
            if (portraitImage != null) portraitImage.enabled = false;
            // Deactivate all name images before activating the correct one
            if (unitNameImagesLookup != null) 
            {
                foreach (var kvp in unitNameImagesLookup) 
                {
                    if (kvp.Value != null) kvp.Value.gameObject.SetActive(false);
                }
            }

            if (currentUnit is DokkaebiUnit concreteUnit) // This handles both player and enemy DokkaebiUnits
            {
                string currentUnitNameId = concreteUnit.GetUnitName(); // Assuming GetUnitName returns the ID used in the lookup
                SmartLogger.Log($"[UnitInfoPanel UpdateAllInfo] Processing as DokkaebiUnit. Name ID: '{currentUnitNameId}'", LogCategory.UI, this);

                // Set Portrait Icon
                var unitDef = concreteUnit.GetUnitDefinitionData();
                if (portraitImage != null && unitDef != null && unitDef.icon != null)
                {
                    portraitImage.sprite = unitDef.icon;
                    portraitImage.enabled = true;
                    SmartLogger.Log($"[UnitInfoPanel UpdateAllInfo] Set portrait from UnitDefinitionData: {unitDef.name}", LogCategory.UI, this);
                }
                else if (portraitImage != null)
                {
                    portraitImage.sprite = null;
                    portraitImage.enabled = false;
                    SmartLogger.Log($"[UnitInfoPanel UpdateAllInfo] Hiding portrait (missing definition or icon)", LogCategory.UI, this);
                }

                // Set Name Image based on lookup
                if (!string.IsNullOrEmpty(currentUnitNameId) && unitNameImagesLookup != null)
                {
                    if (unitNameImagesLookup.TryGetValue(currentUnitNameId, out UnityEngine.UI.Image targetImage))
                    {
                        if (targetImage != null)
                        {
                           targetImage.gameObject.SetActive(true);
                           SmartLogger.Log($"[UnitInfoPanel UpdateAllInfo] Activated name image for ID: '{currentUnitNameId}'", LogCategory.UI, this);
                        }
                         else
                        {
                            SmartLogger.LogWarning($"Found null Image component in lookup for unit name ID: {currentUnitNameId}", LogCategory.UI, this);
                        }
                    }
                    else
                    {
                        SmartLogger.LogWarning($"No UnitNameImage found in lookup for unit name ID: {currentUnitNameId}", LogCategory.UI, this);
                    }
                }
                else if (string.IsNullOrEmpty(currentUnitNameId))
                {
                     SmartLogger.LogWarning("Current unit has a null or empty name ID.", LogCategory.UI, this);
                }
                else // unitNameImagesLookup is null
                {
                    SmartLogger.LogWarning("unitNameImagesLookup is null.", LogCategory.UI, this);
                }

                // Set Origin icon
                var originData = concreteUnit.GetOrigin();
                if (originIconImage != null && originData != null && originData.icon != null)
                {
                    originIconImage.sprite = concreteUnit.GetOrigin().icon;
                    originIconImage.enabled = true;
                    SmartLogger.Log($"[UnitInfoPanel UpdateAllInfo] Set origin icon: {originData.displayName}", LogCategory.UI, this);
                }
                else if (originIconImage != null)
                {
                    originIconImage.sprite = null;
                    originIconImage.enabled = false;
                    SmartLogger.Log($"[UnitInfoPanel UpdateAllInfo] Hiding origin icon (missing data or icon)", LogCategory.UI, this);
                }

                // Set Calling icon
                var callingData = concreteUnit.GetCalling();
                if (callingData != null && callingData.icon != null && !string.IsNullOrEmpty(callingData.callingId))
                {
                    SmartLogger.Log($"[UnitInfoPanel UpdateAllInfo] Setting calling icon for: {callingData.displayName} (ID: {callingData.callingId})", LogCategory.UI, this);
                    // (Existing switch statement for calling icons - no changes needed here)
                    // ... existing switch ...
                    switch (callingData.callingId.ToLowerInvariant())
                    {
                        case "duelist":
                            if (callingIcon_Duelist != null)
                            {
                                callingIcon_Duelist.sprite = callingData.icon;
                                callingIcon_Duelist.gameObject.SetActive(true);
                            }
                            break;
                        case "mage":
                            if (callingIcon_Mage != null)
                            {
                                callingIcon_Mage.sprite = callingData.icon;
                                callingIcon_Mage.gameObject.SetActive(true);
                            }
                            break;
                        case "marksman":
                            if (callingIcon_Marksman != null)
                            {
                                callingIcon_Marksman.sprite = callingData.icon;
                                callingIcon_Marksman.gameObject.SetActive(true);
                            }
                            break;
                        case "guardian":
                            if (callingIcon_Guardian != null)
                            {
                                callingIcon_Guardian.sprite = callingData.icon;
                                callingIcon_Guardian.gameObject.SetActive(true);
                            }
                            break;
                        case "controller":
                            if (callingIcon_Controller != null)
                            {
                                callingIcon_Controller.sprite = callingData.icon;
                                callingIcon_Controller.gameObject.SetActive(true);
                            }
                            break;
                        case "specialist":
                            if (callingIcon_Specialist != null)
                            {
                                callingIcon_Specialist.sprite = callingData.icon;
                                callingIcon_Specialist.gameObject.SetActive(true);
                            }
                            break;
                        default:
                            // Unknown callingId, do nothing (all icons remain disabled)
                            break;
                    }
                }
                else
                {
                    SmartLogger.Log($"[UnitInfoPanel UpdateAllInfo] Hiding calling icons (missing data, icon, or ID)", LogCategory.UI, this);
                    // DisableAllCallingIconImages(); // Already called at the start of the method
                }

                UpdateHPDisplay(currentUnit.CurrentHealth, concreteUnit.MaxHealth);
                UpdateAuraDisplay(concreteUnit.GetCurrentUnitAura(), concreteUnit.GetMaxUnitAura());
                // ClearStatusEffects(); // Moved to start of method
                SmartLogger.Log($"[UnitInfoPanel UpdateAllInfo] Updating status effects display. Current effect count on unit: {concreteUnit.GetStatusEffects()?.Count ?? 0}", LogCategory.UI, this);
                foreach (var effect in concreteUnit.GetStatusEffects())
                {
                    CreateStatusEffectUI(effect);
                }
            }
            else // Handle units that are not DokkaebiUnit (e.g., potentially obstacles implementing IDokkaebiUnit?)
            {
                SmartLogger.Log($"[UnitInfoPanel UpdateAllInfo] Processing unit '{currentUnit.DisplayName}' which is not a DokkaebiUnit.", LogCategory.UI, this);
                // For non-DokkaebiUnit, hide icons and show basic HP
                if (originIconImage != null)
                {
                    originIconImage.sprite = null;
                    originIconImage.enabled = false;
                }
                // DisableAllCallingIconImages(); // Already called at start
                UpdateHPDisplay(currentUnit.CurrentHealth, currentUnit.CurrentHealth); // Show current HP as max if max is unknown
                if (auraSlider != null) auraSlider.value = 0;
                if (auraText != null) auraText.text = "N/A";
                // ClearStatusEffects(); // Already called at start
            }
            SmartLogger.Log($"[UnitInfoPanel UpdateAllInfo END] Finished updating info for unit: {currentUnit.DisplayName}", LogCategory.UI, this);
        }

        private void UpdateHPDisplay(int currentHP, int maxHP)
        {
            if (hpSlider != null)
            {
                hpSlider.maxValue = maxHP > 0 ? maxHP : 1;
                hpSlider.value = Mathf.Clamp(currentHP, 0, maxHP);
            }
            if (hpText != null)
            {
                hpText.text = $"{Mathf.Max(0, currentHP)} / {Mathf.Max(1, maxHP)}";
            }
        }

        private void HandleDamageTaken(int amount, DamageType damageType)
        {
            if (currentUnit != null && currentUnit is DokkaebiUnit concreteUnit)
            {
                SmartLogger.Log($"[UnitInfoPanel HandleDamageTaken] Updating HP display for {currentUnit.DisplayName}. Current HP: {currentUnit.CurrentHealth}", LogCategory.UI, this);
                UpdateHPDisplay(currentUnit.CurrentHealth, concreteUnit.MaxHealth);
            }
        }

        private void HandleHealingReceived(int amount)
        {
            if (currentUnit != null && currentUnit is DokkaebiUnit concreteUnit)
            {
                SmartLogger.Log($"[UnitInfoPanel HandleHealingReceived] Updating HP display for {currentUnit.DisplayName}. Current HP: {currentUnit.CurrentHealth}", LogCategory.UI, this);
                UpdateHPDisplay(currentUnit.CurrentHealth, concreteUnit.MaxHealth);
            }
        }

        private void UpdateAuraDisplay(int currentAura, int maxAura)
        {
            if (auraSlider != null)
            {
                auraSlider.maxValue = maxAura > 0 ? maxAura : 1;
                auraSlider.value = currentAura;
            }
            if (auraText != null)
            {
                auraText.text = $"{currentAura} / {maxAura}";
            }
        }

        private void HandleUnitAuraChanged(int oldAura, int newAura)
        {
            if (currentUnit != null && currentUnit is DokkaebiUnit concreteUnit)
            {
                SmartLogger.Log($"[UnitInfoPanel HandleUnitAuraChanged] Updating Aura display for {currentUnit.DisplayName}. New Aura: {newAura}", LogCategory.UI, this);
                UpdateAuraDisplay(newAura, concreteUnit.GetMaxUnitAura());
            }
        }

        private void ClearStatusEffects()
        {
            // SmartLogger.Log($"[UnitInfoPanel ClearStatusEffects] Starting clear with {activeStatusEffects.Count} active effects. Panel InstanceID: {GetInstanceID()}", LogCategory.UI, this);
            
            foreach (var kvp in activeStatusEffects)
            {
                if (kvp.Value != null)
                {
                    // SmartLogger.Log($"[UnitInfoPanel ClearStatusEffects] Destroying effect icon for {kvp.Key}, GameObject InstanceID: {kvp.Value.GetInstanceID()}, Name: {kvp.Value.name}", LogCategory.UI, this);
                    Destroy(kvp.Value);
                }
                else
                {
                    SmartLogger.LogWarning($"[UnitInfoPanel ClearStatusEffects] Null GameObject found for effect type: {kvp.Key}", LogCategory.UI, this);
                }
            }
            activeStatusEffects.Clear();
            // SmartLogger.Log($"[UnitInfoPanel ClearStatusEffects] Cleared all status effects, dictionary count is now {activeStatusEffects.Count}", LogCategory.UI, this);

            // Force the layout to rebuild immediately to ensure visual updates
            if (statusEffectContainer != null)
            {
                var rectTransform = statusEffectContainer as RectTransform;
                if (rectTransform != null)
                {
                    // SmartLogger.Log("[UnitInfoPanel ClearStatusEffects] Forcing immediate layout rebuild on status effect container", LogCategory.UI, this);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
                    // SmartLogger.Log("[UnitInfoPanel ClearStatusEffects] Layout rebuild completed", LogCategory.UI, this);
                }
                else
                {
                    // SmartLogger.LogWarning("[UnitInfoPanel ClearStatusEffects] Status effect container is not a RectTransform, cannot force layout rebuild", LogCategory.UI, this);
                }
            }
            else
            {
                // SmartLogger.LogWarning("[UnitInfoPanel ClearStatusEffects] Status effect container is null, cannot force layout rebuild", LogCategory.UI, this);
            }
        }

        private void CreateStatusEffectUI(IStatusEffectInstance effect)
        {
            // SmartLogger.Log($"[UnitInfoPanel CreateStatusEffectUI ENTRY] Creating UI for effect: {effect?.StatusEffectType}, Panel InstanceID: {GetInstanceID()}", LogCategory.UI, this);
            
            if (effect == null || statusEffectContainer == null || statusEffectPrefab == null)
            {
                SmartLogger.LogWarning("[UnitInfoPanel CreateStatusEffectUI] Invalid parameters: " +
                    $"effect is null: {effect == null}, " +
                    $"container is null: {statusEffectContainer == null}, " +
                    $"prefab is null: {statusEffectPrefab == null}", LogCategory.UI, this);
                return;
            }

            // Prevent duplicate UI elements for the same effect type
            if (activeStatusEffects.ContainsKey(effect.StatusEffectType))
            {
                SmartLogger.LogWarning($"[UnitInfoPanel CreateStatusEffectUI] Attempted to create duplicate UI for effect type: {effect.StatusEffectType}. Skipping.", LogCategory.UI, this);
                return;
            }

            GameObject effectUI = Instantiate(statusEffectPrefab, statusEffectContainer);
            // SmartLogger.Log($"[UnitInfoPanel CreateStatusEffectUI] Created effect UI for {effect.StatusEffectType}, GameObject Name: {effectUI.name}, InstanceID: {effectUI.GetInstanceID()}, Parent: {effectUI.transform.parent.name}", LogCategory.UI, this);
            
            // Try to get the StatusEffectDisplay component
            var statusEffectDisplay = effectUI.GetComponent<StatusEffectDisplay>();
            if (statusEffectDisplay != null)
            {
                statusEffectDisplay.Initialize(effect.Effect);
                // SmartLogger.Log($"[UnitInfoPanel CreateStatusEffectUI] Initialized StatusEffectDisplay component for {effect.StatusEffectType}", LogCategory.UI, this);
            }
            else
            {
                // Fallback to basic display if StatusEffectDisplay component not found
                var effectIcon = effectUI.GetComponentInChildren<Image>();
                var effectText = effectUI.GetComponentInChildren<TextMeshProUGUI>();
                
                if (effectText != null)
                {
                    effectText.text = $"{effect.RemainingTurns}";
                }
                // SmartLogger.Log("[UnitInfoPanel CreateStatusEffectUI] Using fallback display (no StatusEffectDisplay component found)", LogCategory.UI, this);
            }

            activeStatusEffects[effect.StatusEffectType] = effectUI;
            // SmartLogger.Log($"[UnitInfoPanel CreateStatusEffectUI] Added effect to activeStatusEffects dictionary. Total count: {activeStatusEffects.Count}", LogCategory.UI, this);
        }

        private void HandleStatusEffectApplied(IStatusEffectInstance effect)
        {
            // SmartLogger.Log($"[UnitInfoPanel HandleStatusEffectApplied ENTRY] Handling new effect: {effect?.StatusEffectType}, Panel InstanceID: {GetInstanceID()}", LogCategory.UI, this);
            if (effect != null)
            {
                SmartLogger.Log($"[UnitInfoPanel HandleStatusEffectApplied] Creating UI for applied effect: {effect.StatusEffectType} on {currentUnit?.DisplayName}", LogCategory.UI, this);
                CreateStatusEffectUI(effect);
            }
            else
            {
                SmartLogger.LogWarning("[UnitInfoPanel HandleStatusEffectApplied] Received null effect", LogCategory.UI, this);
            }
        }

        private void HandleStatusEffectRemoved(IStatusEffectInstance effect)
        {
            // SmartLogger.Log($"[UnitInfoPanel HandleStatusEffectRemoved ENTRY] Handling effect removal: {effect?.StatusEffectType}, Panel InstanceID: {GetInstanceID()}", LogCategory.UI, this);
            
            if (effect == null)
            {
                SmartLogger.LogWarning("[UnitInfoPanel HandleStatusEffectRemoved] Received null effect", LogCategory.UI, this);
                return;
            }

            SmartLogger.Log($"[UnitInfoPanel HandleStatusEffectRemoved] Attempting to remove UI for effect: {effect.StatusEffectType} on {currentUnit?.DisplayName}", LogCategory.UI, this);
            bool found = activeStatusEffects.TryGetValue(effect.StatusEffectType, out GameObject effectUI);
            // SmartLogger.Log($"[UnitInfoPanel HandleStatusEffectRemoved] Effect found in dictionary: {found}, UI null: {effectUI == null}", LogCategory.UI, this);

            if (found && effectUI != null)
            {
                // SmartLogger.Log($"[UnitInfoPanel HandleStatusEffectRemoved] About to destroy GameObject - Name: {effectUI.name}, InstanceID: {effectUI.GetInstanceID()}", LogCategory.UI, this);
                Destroy(effectUI);
                activeStatusEffects.Remove(effect.StatusEffectType);
                // SmartLogger.Log($"[UnitInfoPanel HandleStatusEffectRemoved] Removed from dictionary. Remaining effects: {activeStatusEffects.Count}", LogCategory.UI, this);

                // Force immediate layout rebuild to ensure visual update
                if (statusEffectContainer != null)
                {
                    var rectTransform = statusEffectContainer as RectTransform;
                    if (rectTransform != null)
                    {
                        // SmartLogger.Log($"[UnitInfoPanel HandleStatusEffectRemoved] Forcing immediate layout rebuild after removing {effect.StatusEffectType}", LogCategory.UI, this);
                        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
                        // SmartLogger.Log("[UnitInfoPanel HandleStatusEffectRemoved] Layout rebuild completed", LogCategory.UI, this);
                    }
                    else
                    {
                        SmartLogger.LogWarning("[UnitInfoPanel HandleStatusEffectRemoved] Status effect container is not a RectTransform, cannot force layout rebuild", LogCategory.UI, this);
                    }
                }
                else
                {
                    SmartLogger.LogWarning("[UnitInfoPanel HandleStatusEffectRemoved] Status effect container is null, cannot force layout rebuild", LogCategory.UI, this);
                }
            }
        }

        private void DisableAllCallingIconImages()
        {
            if (callingIcon_Duelist != null)
            {
                callingIcon_Duelist.sprite = null;
                callingIcon_Duelist.gameObject.SetActive(false);
            }
            if (callingIcon_Mage != null)
            {
                callingIcon_Mage.sprite = null;
                callingIcon_Mage.gameObject.SetActive(false);
            }
            if (callingIcon_Marksman != null)
            {
                callingIcon_Marksman.sprite = null;
                callingIcon_Marksman.gameObject.SetActive(false);
            }
            if (callingIcon_Guardian != null)
            {
                callingIcon_Guardian.sprite = null;
                callingIcon_Guardian.gameObject.SetActive(false);
            }
            if (callingIcon_Controller != null)
            {
                callingIcon_Controller.sprite = null;
                callingIcon_Controller.gameObject.SetActive(false);
            }
            if (callingIcon_Specialist != null)
            {
                callingIcon_Specialist.sprite = null;
                callingIcon_Specialist.gameObject.SetActive(false);
            }
        }
    }
} 