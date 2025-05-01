using UnityEngine;
using UnityEngine.UI;
using TMPro; // Add this if using TextMeshPro
using Dokkaebi.Units; // Assuming DokkaebiUnit is in this namespace
using Dokkaebi.Interfaces; // For IDokkaebiUnit, IUnit, IUnitEventHandler
using Dokkaebi.Utilities; // For SmartLogger
using Dokkaebi.Common; // For DamageType
using System; // For Action

namespace Dokkaebi.UI
{
    /// <summary>
    /// Represents a single unit's status display item in the Team Status UI overlay.
    /// Manages the portrait, health bar, and aura bar for a specific unit.
    /// </summary>
    public class UnitStatusOverlayItem : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private TextMeshProUGUI healthText; // Optional: Assign if used
        [SerializeField] private Slider auraSlider;
        [SerializeField] private TextMeshProUGUI auraText;   // Optional: Assign if used

        // We need to store the unit as IDokkaebiUnit to subscribe to its events
        private IDokkaebiUnit _unit;
        private bool _isSubscribed = false;

        /// <summary>
        /// Sets up this UI item to display the status of a specific unit.
        /// </summary>
        /// <param name="unit">The unit whose status this item will display.</param>
        public void Setup(IDokkaebiUnit unit) // Changed parameter type to IDokkaebiUnit
        {
            SmartLogger.Log($"[UnitStatusOverlayItem] Setting up UI for unit: {unit?.DisplayName ?? "NULL Unit"}", LogCategory.UI, this);

            if (unit == null)
            {
                SmartLogger.LogError("[UnitStatusOverlayItem] Setup called with null unit!", LogCategory.UI, this);
                // Hide or clear the UI elements if setting to null
                ClearDisplay();
                UnsubscribeFromUnitEvents(); // Ensure previous unit (if any) is unsubscribed
                _unit = null;
                gameObject.SetActive(false); // Hide this item if unit is invalid
                return;
            }

            // Unsubscribe from previous unit's events if necessary
            UnsubscribeFromUnitEvents();

            _unit = unit;

            // Initial UI Setup
            UpdatePortrait();
            UpdateHealth();
            UpdateAura();

            // Subscribe to unit events
            SubscribeToUnitEvents();

            // Ensure the GameObject is active when a valid unit is assigned
             gameObject.SetActive(true);

            SmartLogger.Log($"[UnitStatusOverlayItem] Setup complete for unit: {_unit.DisplayName}", LogCategory.UI, this);
        }

        private void OnDestroy()
        {
            // Unsubscribe from events to prevent memory leaks
            UnsubscribeFromUnitEvents();
            SmartLogger.Log($"[UnitStatusOverlayItem] OnDestroy called for item displaying unit: {_unit?.DisplayName ?? "NULL"}", LogCategory.UI, this);
        }

        /// <summary>
        /// Clears the display elements.
        /// </summary>
        private void ClearDisplay()
        {
            if (portraitImage != null) portraitImage.sprite = null;
            if (healthSlider != null) healthSlider.value = 0;
            if (healthText != null) healthText.text = "0/0";
            if (auraSlider != null) auraSlider.value = 0;
            if (auraText != null) auraText.text = "0/0";
        }


        /// <summary>
        /// Subscribes to the unit's relevant events.
        /// </summary>
        private void SubscribeToUnitEvents()
        {
            if (_unit == null || _isSubscribed)
            {
                SmartLogger.LogWarning($"[UnitStatusOverlayItem] InstanceID: {this.GetInstanceID()} - Cannot subscribe - " +
                    $"Unit is {(_unit == null ? "null" : "not null")}, isSubscribed: {_isSubscribed}", LogCategory.UI, this);
                return;
            }

            // Subscribe to health changes (via IUnitEventHandler)
            if (_unit is IUnitEventHandler eventHandler)
            {
                // Subscribe to both damage and healing events, and update health display in the handlers
                eventHandler.OnDamageTaken += HandleUnitHealthChanged;
                eventHandler.OnHealingReceived += HandleUnitHealthChanged;
                SmartLogger.Log($"[UnitStatusOverlayItem] Subscribed to health events for unit: {_unit.DisplayName}", LogCategory.UI, this);
            }
            else
            {
                SmartLogger.LogWarning($"[UnitStatusOverlayItem] Unit {_unit.DisplayName} does not implement IUnitEventHandler. Health updates may not work.", LogCategory.UI, this);
            }

            // Subscribe to unit-specific aura changes (via DokkaebiUnit)
            if (_unit is DokkaebiUnit dokkaebiUnit)
            {
                dokkaebiUnit.OnUnitAuraChanged += HandleUnitAuraChanged;
                 SmartLogger.Log($"[UnitStatusOverlayItem] Subscribed to aura events for unit: {_unit.DisplayName}", LogCategory.UI, this);
            }
            else
            {
                SmartLogger.LogWarning($"[UnitStatusOverlayItem] Unit {_unit.DisplayName} is not a DokkaebiUnit. Aura updates may not work.", LogCategory.UI, this);
            }

            // Subscribe to unit defeat event (assuming IDokkaebiUnit has this event)
             if (_unit is DokkaebiUnit du) // Assuming DokkaebiUnit has an OnUnitDefeated event
             {
                  du.OnUnitDefeated += HandleUnitDefeated;
                  SmartLogger.Log($"[UnitStatusOverlayItem] Subscribed to defeat event for unit: {_unit.DisplayName}", LogCategory.UI, this);
             }
             else
             {
                 SmartLogger.LogWarning($"[UnitStatusOverlayItem] Unit {_unit.DisplayName} is not a DokkaebiUnit. Defeat updates may not work.", LogCategory.UI, this);
             }


            _isSubscribed = true;
        }

        /// <summary>
        /// Unsubscribes from the unit's events.
        /// </summary>
        private void UnsubscribeFromUnitEvents()
        {
            // We need to check if _unit is not null AND if it was the correct type for subscription
            if (_unit == null || !_isSubscribed)
            {
                 // SmartLogger.LogWarning($"[UnitStatusOverlayItem] Cannot unsubscribe - Unit is {(_unit == null ? "null" : "not null")}, isSubscribed: {_isSubscribed}", LogCategory.UI, this);
                return;
            }

            // Unsubscribe from health changes
            if (_unit is IUnitEventHandler eventHandler)
            {
                eventHandler.OnDamageTaken -= HandleUnitHealthChanged;
                eventHandler.OnHealingReceived -= HandleUnitHealthChanged;
                 SmartLogger.Log($"[UnitStatusOverlayItem] Unsubscribed from health events for unit: {_unit.DisplayName}", LogCategory.UI, this);
            }

            // Unsubscribe from aura changes
            if (_unit is DokkaebiUnit dokkaebiUnit)
            {
                dokkaebiUnit.OnUnitAuraChanged -= HandleUnitAuraChanged;
                 SmartLogger.Log($"[UnitStatusOverlayItem] Unsubscribed from aura events for unit: {_unit.DisplayName}", LogCategory.UI, this);
            }

            // Unsubscribe from unit defeat event
             if (_unit is DokkaebiUnit du)
             {
                  du.OnUnitDefeated -= HandleUnitDefeated;
                  SmartLogger.Log($"[UnitStatusOverlayItem] Unsubscribed from defeat event for unit: {_unit.DisplayName}", LogCategory.UI, this);
             }


            _isSubscribed = false;
        }

        // --- Event Handlers ---

        // Combined handler for damage and healing events
        private void HandleUnitHealthChanged(int amount, DamageType type) // Matches OnDamageTaken signature
        {
            UpdateHealth(); // Update display whenever health changes
        }

        private void HandleUnitHealthChanged(int amount) // Matches OnHealingReceived signature
        {
            UpdateHealth(); // Update display whenever health changes
        }


        private void HandleUnitAuraChanged(int oldAura, int newAura) // Matches OnUnitAuraChanged signature
        {
             UpdateAura(); // Update display whenever aura changes
        }

        private void HandleUnitDefeated()
        {
            SmartLogger.Log($"[UnitStatusOverlayItem] Unit {_unit?.DisplayName ?? "NULL"} defeated. Hiding/Destroying item.", LogCategory.UI, this);
            // Option 1: Destroy the item immediately (handled by TeamStatusUI's subscription)

            // Option 2: Visually indicate defeat (e.g., fade out)
            if (TryGetComponent<CanvasGroup>(out var canvasGroup))
            {
                canvasGroup.alpha = 0.5f; // Example: Make it semi-transparent
                // Optionally disable interaction if applicable
                canvasGroup.interactable = false;
            } else {
                // Fallback if no CanvasGroup
                gameObject.SetActive(false);
            }
            // Note: TeamStatusUI is also subscribed to OnUnitDefeated and will destroy this GameObject.
            // This visual change might be brief before destruction.
        }


        // --- UI Update Methods ---

        private void UpdatePortrait()
        {
            if (portraitImage == null || _unit == null) return;

            // Assuming DokkaebiUnit provides access to UnitDefinitionData which has the icon
            if (_unit is DokkaebiUnit dokkaebiUnit && dokkaebiUnit.GetUnitDefinitionData() != null)
            {
                var unitDef = dokkaebiUnit.GetUnitDefinitionData();
                if (unitDef.icon != null)
                {
                    portraitImage.sprite = unitDef.icon;
                    portraitImage.enabled = true;
                }
                else
                {
                    portraitImage.enabled = false; // Hide if no icon assigned
                     SmartLogger.LogWarning($"[UnitStatusOverlayItem] UnitDefinition {unitDef.name} for unit {_unit.DisplayName} has no portrait icon.", LogCategory.UI, this);
                }
            }
            else
            {
                portraitImage.enabled = false; // Hide if not a DokkaebiUnit or no definition data
                 if (_unit is DokkaebiUnit du && du.GetUnitDefinitionData() == null) SmartLogger.LogWarning($"[UnitStatusOverlayItem] Unit {_unit.DisplayName} has no UnitDefinitionData assigned.", LogCategory.UI, this);
            }
        }

        private void UpdateHealth()
        {
            if (_unit == null || healthSlider == null) return;

            // Ensure the unit implements IUnit to access health properties
            if (_unit is IUnit baseUnit)
            {
                healthSlider.maxValue = Mathf.Max(1, baseUnit.MaxHealth); // Ensure max is at least 1 to avoid division by zero
                healthSlider.value = Mathf.Clamp(baseUnit.CurrentHealth, 0, baseUnit.MaxHealth);

                if (healthText != null)
                {
                    healthText.text = $"{Mathf.Max(0, baseUnit.CurrentHealth)}/{Mathf.Max(1, baseUnit.MaxHealth)}";
                }
                 SmartLogger.Log($"[UnitStatusOverlayItem] Updating Health UI for Unit {_unit.DisplayName}: {baseUnit.CurrentHealth}/{baseUnit.MaxHealth}", LogCategory.UI, this);
            }
            else
            {
                SmartLogger.LogWarning($"[UnitStatusOverlayItem] Unit {_unit.DisplayName} is not an IUnit. Cannot update health display.", LogCategory.UI, this);
            }
        }

        private void UpdateAura()
        {
             if (_unit == null || auraSlider == null) return;

            // Ensure the unit is a DokkaebiUnit to access unit-specific aura properties
            if (_unit is DokkaebiUnit dokkaebiUnit)
            {
                int maxUnitAura = dokkaebiUnit.GetMaxUnitAura();
                int currentUnitAura = dokkaebiUnit.GetCurrentUnitAura();

                auraSlider.maxValue = Mathf.Max(1, maxUnitAura); // Ensure max is at least 1
                auraSlider.value = Mathf.Clamp(currentUnitAura, 0, maxUnitAura);

                if (auraText != null)
                {
                    auraText.text = $"{currentUnitAura}/{maxUnitAura}" ;
                }
                 SmartLogger.Log($"[UnitStatusOverlayItem] Updating Aura UI for Unit {_unit.DisplayName}: {currentUnitAura}/{maxUnitAura}", LogCategory.UI, this);
            }
            else
            {
                SmartLogger.LogWarning($"[UnitStatusOverlayItem] Unit {_unit.DisplayName} is not a DokkaebiUnit. Cannot update aura display.", LogCategory.UI, this);
            }
        }
    }
}
