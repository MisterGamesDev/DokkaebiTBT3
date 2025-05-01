using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Dokkaebi.Units;
using Dokkaebi.Core.Data;
using Dokkaebi.Interfaces;
using Dokkaebi.Common;
using Dokkaebi.Utilities;
// using Dokkaebi.Events;
using System.Linq;
using Dokkaebi.Core;
//using UnityEngine.UI.Extensions;

namespace Dokkaebi.UI
{
    /// <summary>
    /// Handles UI elements for displaying unit status, cooldowns, and effects
    /// </summary>
    public class UnitStatusUI : MonoBehaviour
    {
        [Header("Health UI Components")]
        [SerializeField] private Slider healthBar;
        [SerializeField] private TextMeshProUGUI healthText;

        [Header("Aura UI Components")]
        [SerializeField] private Slider auraSlider;
        [SerializeField] private TextMeshProUGUI auraText;

        [Header("Status Effect UI")]
        [SerializeField] private Transform statusEffectContainer;
        [SerializeField] private GameObject statusEffectIconPrefab;

        [Header("Cooldown UI")]
        [SerializeField] private Transform cooldownContainer;
        [SerializeField] private GameObject cooldownDisplayPrefab;

        private IDokkaebiUnit unit;
        private Dictionary<string, GameObject> statusEffectIcons = new Dictionary<string, GameObject>();
        private Dictionary<string, GameObject> cooldownDisplays = new Dictionary<string, GameObject>();
        private bool isSubscribed = false; // Track subscription status

        private void OnEnable()
        {
            // SmartLogger.Log($"[UnitStatusUI OnEnable] Panel enabled. InstanceID: {GetInstanceID()}, Current Unit: {(unit != null ? unit.DisplayName : "NULL")}, Called from SetUnit: {Time.frameCount}", LogCategory.UI, this);
        }

        private void OnDisable()
        {
            // SmartLogger.Log($"[UnitStatusUI OnDisable] Panel disabled. InstanceID: {GetInstanceID()}, Current Unit: {(unit != null ? unit.DisplayName : "NULL")}, Frame: {Time.frameCount}", LogCategory.UI, this);
            if (unit != null)
            {
                // SmartLogger.Log($"[UnitStatusUI OnDisable] Unsubscribing from events for unit: {unit.DisplayName}", LogCategory.UI, this);
                UnsubscribeFromUnitEvents();
            }
        }

        private void OnDestroy()
        {
            // SmartLogger.Log($"[UnitStatusUI OnDestroy] Panel being destroyed. InstanceID: {GetInstanceID()}, Current Unit: {(unit != null ? unit.DisplayName : "NULL")}", LogCategory.UI, this);
            if (unit != null)
            {
                // SmartLogger.Log($"[UnitStatusUI OnDestroy] Unsubscribing from events for unit: {unit.DisplayName}", LogCategory.UI, this);
                UnsubscribeFromUnitEvents();
            }
        }

        private void Awake()
        {
            // Try to find IDokkaebiUnit component on the same GameObject
            var unitComponent = GetComponent<IDokkaebiUnit>();
            if (unitComponent != null)
            {
                SetUnit(unitComponent); // SetUnit handles subscription if GameObject is active
            }
            
            InitializeComponents();
        }

        private void Start()
        {
            if (unit == null)
            {
                SmartLogger.LogWarning("[UnitStatusUI] No unit assigned on Start!", LogCategory.UI, this);
                return;
            }

            InitializeHealthDisplay();
            InitializeAuraDisplay();
        }

        private void InitializeComponents()
        {
            if (healthBar == null || healthText == null)
            {
                SmartLogger.LogError("[UnitStatusUI] Health UI components not assigned!", LogCategory.UI, this);
            }

            if (auraSlider == null || auraText == null)
            {
                SmartLogger.LogError("[UnitStatusUI] Aura UI components not assigned!", LogCategory.UI, this);
            }

            if (statusEffectContainer == null || statusEffectIconPrefab == null)
            {
                SmartLogger.LogError("[UnitStatusUI] Status effect UI components not assigned!", LogCategory.UI, this);
            }

            if (cooldownContainer == null || cooldownDisplayPrefab == null)
            {
                SmartLogger.LogError("[UnitStatusUI] Cooldown UI components not assigned!", LogCategory.UI, this);
            }
        }

        public void SetUnit(IDokkaebiUnit newUnit)
        {
            // SmartLogger.Log($"[UnitInfoPanel SetUnit ENTRY] New unit: {(newUnit != null ? newUnit.GetUnitName() : "NULL")}, Current unit: {(unit != null ? unit.DisplayName : "NULL")}, Panel InstanceID: {GetInstanceID()}, Frame: {Time.frameCount}, Current activeSelf: {gameObject.activeSelf}", LogCategory.UI, this);
            if (newUnit == unit)
            {
                // SmartLogger.Log("[UnitInfoPanel SetUnit] Same unit, returning early", LogCategory.UI, this);
                return;
            }

            UnsubscribeFromUnitEvents();
            // Clear existing status effect icons
            // Ensure this is done every time SetUnit is called
            ClearStatusEffectIcons(); // Use the existing method to clear visual icons

            unit = newUnit;

            if (unit != null)
            {
                // Subscribe to events to handle future effect changes
                if (gameObject.activeInHierarchy)
                {
                    SubscribeToUnitEvents();
                }

                // NOW, update the displayed status effects based on the new unit's CURRENT effects
                if (unit.GetStatusEffects() != null)
                {
                    SmartLogger.Log($"[UnitStatusUI.SetUnit] Populating status effects for unit: {unit.DisplayName}. Found {unit.GetStatusEffects().Count} effects.", LogCategory.UI, this);
                    foreach (var effect in unit.GetStatusEffects())
                    {
                        HandleStatusEffectAdded(effect); // Create UI for each existing effect
                    }
                }

                InitializeHealthDisplay(); // These should also be updated for the new unit
                InitializeAuraDisplay(); // These should also be updated for the new unit
            }
            else
            {
                SmartLogger.LogWarning("[UnitStatusUI.SetUnit] Clearing unit reference (null unit provided)", LogCategory.UI, this);
            }

            // SmartLogger.Log($"[UnitInfoPanel SetUnit EXIT] Setup complete for unit: {(unit != null ? unit.DisplayName : "NULL")}, Panel activeSelf: {gameObject.activeSelf}, Frame: {Time.frameCount}", LogCategory.UI, this);
        }

        private void SubscribeToUnitEvents()
        {
            if (unit == null || isSubscribed)
            {
                SmartLogger.LogWarning($"[UnitStatusUI.Subscribe] InstanceID: {this.GetInstanceID()} - Cannot subscribe - " + 
                    $"Unit is {(unit == null ? "null" : "not null")}, isSubscribed: {isSubscribed}", LogCategory.UI, this);
                return;
            }

            if (unit is IUnitEventHandler eventHandler)
            {
                eventHandler.OnDamageTaken += HandleUnitDamageTaken;
                eventHandler.OnHealingReceived += HandleUnitHealingReceived;
                eventHandler.OnStatusEffectApplied += HandleStatusEffectAdded;
                eventHandler.OnStatusEffectRemoved += HandleStatusEffectRemoved;
            }
            
            if (unit is DokkaebiUnit dokkaebiUnit)
            {
                dokkaebiUnit.OnUnitAuraChanged += HandleUnitAuraChanged;
            }

            isSubscribed = true;
        }

        private void UnsubscribeFromUnitEvents()
        {
            if (unit == null || !isSubscribed)
            {
                SmartLogger.LogWarning($"[UnitStatusUI.Unsubscribe] InstanceID: {this.GetInstanceID()} - Cannot unsubscribe - " + 
                    $"Unit is {(unit == null ? "null" : "not null")}, isSubscribed: {isSubscribed}", LogCategory.UI, this);
                return;
            }

            if (unit is IUnitEventHandler eventHandler)
            {
                eventHandler.OnDamageTaken -= HandleUnitDamageTaken;
                eventHandler.OnHealingReceived -= HandleUnitHealingReceived;
                eventHandler.OnStatusEffectApplied -= HandleStatusEffectAdded;
                eventHandler.OnStatusEffectRemoved -= HandleStatusEffectRemoved;
            }
            
            if (unit is DokkaebiUnit dokkaebiUnit)
            {
                dokkaebiUnit.OnUnitAuraChanged -= HandleUnitAuraChanged;
            }

            isSubscribed = false;
        }

        private void InitializeHealthDisplay()
        {
            if (unit == null || healthBar == null) return;

            if (unit is IUnit baseUnit)
            {
                healthBar.maxValue = Mathf.Max(1, baseUnit.MaxHealth);
                healthBar.value = Mathf.Clamp(baseUnit.CurrentHealth, 0, baseUnit.MaxHealth);

                UpdateHealthText();
            }
        }

        private void HandleUnitDamageTaken(int amount, DamageType damageType)
        {
            if (unit == null)
            {
                SmartLogger.LogWarning("[UnitStatusUI.HandleUnitDamageTaken] Event received but unit is null!", LogCategory.UI, this);
                return;
            }
            
            if (unit is IUnit baseUnit)
            {
            }
            
            UpdateHealthDisplay();
        }

        private void HandleUnitHealingReceived(int healAmount)
        {
            if (unit == null)
            {
                SmartLogger.LogWarning("[UnitStatusUI.HandleUnitHealingReceived] Event received but unit is null!", LogCategory.UI, this);
                return;
            }
            
            if (unit is IUnit baseUnit)
            {
            }
            
            UpdateHealthDisplay();
        }

        private void HandleStatusEffectAdded(IStatusEffectInstance effect)
        {
            if (effect == null)
            {
                SmartLogger.LogWarning($"[UnitStatusUI.HandleStatusEffectAdded] InstanceID: {this.GetInstanceID()} - Received null effect", LogCategory.UI, this);
                return;
            }

            if (unit == null)
            {
                SmartLogger.LogWarning($"[UnitStatusUI.HandleStatusEffectAdded] InstanceID: {this.GetInstanceID()} - Unit is null", LogCategory.UI, this);
                return;
            }

            if (statusEffectContainer == null)
            {
                SmartLogger.LogError($"[UnitStatusUI.HandleStatusEffectAdded] InstanceID: {this.GetInstanceID()} - StatusEffectContainer is null! Cannot instantiate icon.", LogCategory.UI, this);
                return;
            }

            if (statusEffectIconPrefab == null)
            {
                SmartLogger.LogError($"[UnitStatusUI.HandleStatusEffectAdded] InstanceID: {this.GetInstanceID()} - StatusEffectIconPrefab is null!", LogCategory.UI, this);
                return;
            }

            string effectId = effect.StatusEffectType.ToString();

            // Explicitly support new Fatebinder status effects
            if (effectId == "KarmicTether" || effectId == "DestinyShifted" || effectId == "TemporalEcho" || effectId == "ProbabilityFieldCritBuff")
            {
                // continue as normal
            }
            // For all other effects, continue as normal

            if (statusEffectIcons.TryGetValue(effectId, out GameObject existingIcon))
            {
                return;
            }

            GameObject newIcon = Instantiate(statusEffectIconPrefab, statusEffectContainer);
            newIcon.name = $"StatusEffect_{effectId}";
            
            if (newIcon.transform.parent != statusEffectContainer)
            {
                SmartLogger.LogWarning($"[UnitStatusUI.HandleStatusEffectAdded] InstanceID: {this.GetInstanceID()} - Icon parenting mismatch! Manually setting parent to {statusEffectContainer.name}", LogCategory.UI, this);
                newIcon.transform.SetParent(statusEffectContainer, false);
            }

            statusEffectIcons[effectId] = newIcon;
        }

        private void HandleStatusEffectRemoved(IStatusEffectInstance effect)
        {
            if (effect == null)
            {
                SmartLogger.LogWarning("[UnitStatusUI.HandleStatusEffectRemoved] Received null effect", LogCategory.UI, this);
                return;
            }

            string effectId = effect.StatusEffectType.ToString();

            // Explicitly support new Fatebinder status effects
            if (effectId == "KarmicTether" || effectId == "DestinyShifted" || effectId == "TemporalEcho" || effectId == "ProbabilityFieldCritBuff")
            {
                // continue as normal
            }
            // For all other effects, continue as normal

            if (statusEffectIcons.TryGetValue(effectId, out GameObject icon))
            {
                if (icon != null)
                {
                    Destroy(icon);
                }
                
                bool removed = statusEffectIcons.Remove(effectId);
            }
            else
            {
                SmartLogger.LogWarning($"[UnitStatusUI.HandleStatusEffectRemoved] No icon found for effect: {effectId} on unit: {unit?.DisplayName}", LogCategory.UI, this);
            }
        }

        private void UpdateHealthText()
        {
            if (healthText == null || unit == null) return;

            if (unit is IUnit baseUnit)
            {
                healthText.text = $"{baseUnit.CurrentHealth}/{baseUnit.MaxHealth}";
            }
            else
            {
                SmartLogger.LogWarning($"[UnitStatusUI.UpdateHealthText] Unit {unit.DisplayName} is not an IUnit - cannot update health text", LogCategory.UI, this);
            }
        }

        private void UpdateHealthDisplay()
        {
            if (unit == null || healthBar == null) return;

            if (unit is IUnit baseUnit)
            {
                healthBar.maxValue = Mathf.Max(1, baseUnit.MaxHealth);
                healthBar.value = Mathf.Clamp(baseUnit.CurrentHealth, 0, baseUnit.MaxHealth);
                UpdateHealthText();
            }
            else
            {
                SmartLogger.LogWarning($"[UnitStatusUI.UpdateHealthDisplay] Unit {unit.DisplayName} is not an IUnit - cannot update health display", LogCategory.UI, this);
            }
        }

        private void InitializeAuraDisplay()
        {
            if (unit == null || auraSlider == null) return;

            if (unit is DokkaebiUnit dokkaebiUnit)
            {
                int maxUnitAura = dokkaebiUnit.GetMaxUnitAura();
                int currentUnitAura = dokkaebiUnit.GetCurrentUnitAura();
                
                auraSlider.maxValue = Mathf.Max(1, maxUnitAura);
                auraSlider.value = Mathf.Clamp(currentUnitAura, 0, maxUnitAura);
                
                UpdateAuraText();
            }
            else
            {
                SmartLogger.LogWarning($"[UnitStatusUI.InitializeAuraDisplay] Unit {unit.DisplayName} is not a DokkaebiUnit - cannot initialize aura display", LogCategory.UI, this);
            }
        }

        private void UpdateAuraText()
        {
            if (auraText == null || unit == null) return;

            if (unit is DokkaebiUnit dokkaebiUnit)
            {
                int currentUnitAura = dokkaebiUnit.GetCurrentUnitAura();
                int maxUnitAura = dokkaebiUnit.GetMaxUnitAura();
                
                auraText.text = $"{currentUnitAura}/{maxUnitAura}";
            }
            else
            {
                SmartLogger.LogWarning($"[UnitStatusUI.UpdateAuraText] Unit {unit.DisplayName} is not a DokkaebiUnit - cannot update aura text", LogCategory.UI, this);
            }
        }

        private void UpdateAuraDisplay()
        {
            if (unit == null || auraSlider == null) return;

            if (unit is DokkaebiUnit dokkaebiUnit)
            {
                int maxUnitAura = dokkaebiUnit.GetMaxUnitAura();
                int currentUnitAura = dokkaebiUnit.GetCurrentUnitAura();
                
                auraSlider.maxValue = Mathf.Max(1, maxUnitAura);
                auraSlider.value = Mathf.Clamp(currentUnitAura, 0, maxUnitAura);
                
                UpdateAuraText();
            }
            else
            {
                SmartLogger.LogWarning($"[UnitStatusUI.UpdateAuraDisplay] Unit {unit.DisplayName} is not a DokkaebiUnit - cannot update aura display", LogCategory.UI, this);
            }
        }

        private void HandleUnitAuraChanged(int oldAura, int newAura)
        {
            if (unit == null)
            {
                SmartLogger.LogWarning("[UnitStatusUI.HandleUnitAuraChanged] Event received but unit is null!", LogCategory.UI, this);
                return;
            }
            
            if (unit is DokkaebiUnit dokkaebiUnit)
            {
                UpdateAuraDisplay();
            }
            else
            {
                SmartLogger.LogWarning($"[UnitStatusUI.HandleUnitAuraChanged] Unit {unit.DisplayName} is not a DokkaebiUnit - cannot handle aura change", LogCategory.UI, this);
            }
        }

        private void LateUpdate()
        {
            if (unit == null) return;
            
            FaceCamera();
        }
        
        private void FaceCamera()
        {
            if (UnityEngine.Camera.main != null)
            {
                transform.forward = UnityEngine.Camera.main.transform.forward;
            }
        }

        public void ClearStatusEffectIcons()
        {
            SmartLogger.Log($"[UnitStatusUI.ClearStatusEffectIcons] Clearing {statusEffectIcons.Count} status effect icons for unit: {unit?.DisplayName}", LogCategory.UI, this);
            foreach (var iconGameObject in statusEffectIcons.Values)
            {
                if (iconGameObject != null)
                {
                    Destroy(iconGameObject);
                }
            }
            statusEffectIcons.Clear();
            // Optional: Force layout update if using a layout group to ensure icons are properly removed visually
            if (statusEffectContainer != null)
            {
                if (statusEffectContainer is RectTransform rectTransform)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
                }
            }
            SmartLogger.Log("[UnitStatusUI.ClearStatusEffectIcons] Status effect icons cleared.", LogCategory.UI, this);
        }
    }
} 
