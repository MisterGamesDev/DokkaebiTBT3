using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Dokkaebi.Units;
using Dokkaebi.Core.Data;
using Dokkaebi.Core;
using Dokkaebi.Grid;
using Dokkaebi.Interfaces;

namespace Dokkaebi.UI
{
    public class AbilitySelectionUI : MonoBehaviour
    {
        [System.Serializable]
        public class AbilityButton
        {
            public Button button;
            public Image icon;
            public Image cooldownOverlay;
            public TextMeshProUGUI cooldownText;
            public TextMeshProUGUI auraCostText;
            public int abilityIndex;
        }

        [Header("References")]
        [SerializeField] private DokkaebiTurnSystemCore turnSystem;
        [SerializeField] private AbilityButton[] abilityButtons;
        [SerializeField] private GameObject tooltipPrefab;
        [SerializeField] private GameObject targetingIndicator;
        [SerializeField] private TextMeshProUGUI targetingInstructionsText;

        [Header("Unit Info")]
        public TextMeshProUGUI unitNameText;

        [Header("Button Visuals")]
        [SerializeField] private Color enabledColor = Color.white;
        [SerializeField] private Color disabledColor = Color.gray;
        [SerializeField] private Vector2 tooltipOffset = new Vector2(10f, 10f);

        private DokkaebiUnit currentUnit;
        private GameObject currentTooltip;
        private AbilityButton hoveredButton;
        private PlayerActionManager playerActionManager;
        private bool isTargeting = false;

        private void Awake()
        {
            playerActionManager = PlayerActionManager.Instance;
            if (playerActionManager == null)
            {
                Debug.LogError("PlayerActionManager not found in scene!");
                return;
            }

            if (turnSystem == null)
            {
                turnSystem = FindObjectOfType<DokkaebiTurnSystemCore>();
                if (turnSystem == null)
                {
                    Debug.LogError("DokkaebiTurnSystemCore not found in scene!");
                }
            }

            // Subscribe to events
            playerActionManager.OnCommandResult += HandleCommandResult;
            playerActionManager.OnAbilityTargetingStarted += HandleAbilityTargetingStarted;
            playerActionManager.OnAbilityTargetingCancelled += HandleAbilityTargetingCancelled;
        }

        private void OnEnable()
        {
            if (currentUnit != null)
            {
                UpdateAbilityButtons();
            }
        }

        private void OnDisable()
        {
            HideTooltip();
            if (isTargeting)
            {
                playerActionManager.CancelAbilityTargeting();
            }
        }

        private void OnDestroy()
        {
            if (playerActionManager != null)
            {
                playerActionManager.OnCommandResult -= HandleCommandResult;
                playerActionManager.OnAbilityTargetingStarted -= HandleAbilityTargetingStarted;
                playerActionManager.OnAbilityTargetingCancelled -= HandleAbilityTargetingCancelled;
            }
        }

        private void Update()
        {
            // Cancel targeting if escape is pressed
            if (isTargeting && Input.GetKeyDown(KeyCode.Escape))
            {
                playerActionManager.CancelAbilityTargeting();
            }
        }

        public void SetUnit(DokkaebiUnit unit)
        {
            gameObject.SetActive(unit != null);
            if (currentUnit != null)
            {
                // Unsubscribe from previous unit's events if needed
                currentUnit.OnUnitAuraChanged -= HandleUnitAuraChanged;
                // If you have a cooldown event, unsubscribe here
            }
            currentUnit = unit;

            if (unitNameText != null)
            {
                unitNameText.text = (unit != null) ? unit.DisplayName : "--";
            }

            if (currentUnit != null)
            {
                // Subscribe to unit events to update UI
                currentUnit.OnUnitAuraChanged += HandleUnitAuraChanged;
                // If you have a cooldown event, subscribe here
            }
            UpdateAbilityButtons();
        }

        private void HandleUnitAuraChanged(int oldAura, int newAura)
        {
            UpdateAbilityButtons();
        }

        private void UpdateAbilityButtons()
        {
            if (currentUnit == null)
            {
                // Hide all ability buttons when no unit is selected
                if (abilityButtons != null)
                {
                    foreach (var button in abilityButtons)
                    {
                        if (button?.button?.gameObject != null)
                        {
                            button.button.gameObject.SetActive(false);
                        }
                    }
                }
                return;
            }

            var abilities = currentUnit.GetAbilities();
            if (abilities == null || abilityButtons == null)
            {
                Debug.LogError($"UpdateAbilityButtons: Invalid state - Abilities: {(abilities == null ? "NULL" : abilities.Count.ToString())}, Buttons: {(abilityButtons == null ? "NULL" : abilityButtons.Length.ToString())}");
                return;
            }

            for (int i = 0; i < abilityButtons.Length; i++)
            {
                var button = abilityButtons[i];
                if (button?.button == null) continue;

                if (i < abilities.Count)
                {
                    AbilityData ability = abilities[i];
                    if (ability == null)
                    {
                        button.button.gameObject.SetActive(false);
                        continue;
                    }

                    button.button.gameObject.SetActive(true);
                    
                    // Set icon
                    if (button.icon != null)
                    {
                        button.icon.sprite = ability.icon;
                    }

                    button.abilityIndex = i;

                    // Check ability state
                    bool isOnCooldown = currentUnit.IsOnCooldown(ability.abilityId);
                    bool hasEnoughAura = currentUnit.HasEnoughUnitAura(ability.auraCost);
                    bool canUseAura = turnSystem.CanUnitUseAura(currentUnit);
                    int remainingCooldown = currentUnit.GetRemainingCooldown(ability.abilityId);

                    // Update button interactability
                    button.button.interactable = !isOnCooldown && hasEnoughAura && canUseAura;

                    // Update button colors
                    var buttonImage = button.button.GetComponent<UnityEngine.UI.Image>();
                    if (buttonImage != null)
                    {
                        buttonImage.color = button.button.interactable ? enabledColor : disabledColor;
                    }
                    
                    // Update cooldown display
                    if (button.cooldownOverlay != null && button.cooldownText != null)
                    {
                        if (isOnCooldown)
                        {
                            // Show cooldown state with remaining turns
                            button.cooldownOverlay.gameObject.SetActive(true);
                            button.cooldownText.gameObject.SetActive(true);
                            button.cooldownText.text = remainingCooldown.ToString();
                            button.cooldownOverlay.color = new Color(0.2f, 0.2f, 0.8f, 0.5f); // Blue tint for cooldown
                        }
                        else if (!hasEnoughAura)
                        {
                            // Show insufficient aura state
                            button.cooldownOverlay.gameObject.SetActive(true);
                            button.cooldownText.gameObject.SetActive(true);
                            button.cooldownText.text = "!"; // Exclamation mark for insufficient aura
                            button.cooldownOverlay.color = new Color(0.8f, 0.2f, 0.2f, 0.5f); // Red tint for insufficient aura
                        }
                        else if (!canUseAura)
                        {
                            // Show cannot use aura state
                            button.cooldownOverlay.gameObject.SetActive(true);
                            button.cooldownText.gameObject.SetActive(true);
                            button.cooldownText.text = "X"; // X for cannot use aura
                            button.cooldownOverlay.color = new Color(0.8f, 0.8f, 0.2f, 0.5f); // Yellow tint for cannot use
                        }
                        else
                        {
                            // Ability is ready to use
                            button.cooldownOverlay.gameObject.SetActive(false);
                            button.cooldownText.gameObject.SetActive(false);
                        }
                    }
                    
                    // Update aura cost text
                    if (button.auraCostText != null)
                    {
                        button.auraCostText.text = ability.auraCost.ToString();
                        // Color the aura cost text based on whether the unit has enough aura
                        button.auraCostText.color = hasEnoughAura ? Color.white : Color.red;
                    }

                    // Add click handler
                    button.button.onClick.RemoveAllListeners();
                    int index = i; // Capture for lambda
                    button.button.onClick.AddListener(() => HandleAbilityButtonClick(index));

                    // Add hover handlers for tooltip
                    SetupTooltipTriggers(button, ability);
                }
                else
                {
                    button.button.gameObject.SetActive(false);
                }
            }
        }

        private void SetupTooltipTriggers(AbilityButton button, AbilityData ability)
        {
            var eventTrigger = button.button.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (eventTrigger == null)
            {
                eventTrigger = button.button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            }

            eventTrigger.triggers.Clear();

            // Add hover enter trigger
            var enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            enterEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
            enterEntry.callback.AddListener((data) => {
                hoveredButton = button;
                ShowTooltip(ability, button);
            });
            eventTrigger.triggers.Add(enterEntry);

            // Add hover exit trigger
            var exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            exitEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
            exitEntry.callback.AddListener((data) => {
                if (hoveredButton == button)
                {
                    hoveredButton = null;
                    HideTooltip();
                }
            });
            eventTrigger.triggers.Add(exitEntry);
        }

        private bool CanUseAbility(AbilityData ability)
        {
            if (currentUnit == null) return false;

            // Check cooldown
            if (currentUnit.IsOnCooldown(ability.abilityId))
            {
                return false;
            }

            // Check unit-specific aura cost
            if (!currentUnit.HasEnoughUnitAura(ability.auraCost))
            {
                return false;
            }

            return true;
        }

        private void HandleAbilityButtonClick(int abilityIndex)
        {
            Debug.Log($"[AbilitySelectionUI] Ability button clicked with index: {abilityIndex}");
            if (currentUnit == null) return;

            var abilities = currentUnit.GetAbilities();
            if (abilityIndex >= 0 && abilityIndex < abilities.Count)
            {
                AbilityData ability = abilities[abilityIndex];
                if (CanUseAbility(ability))
                {
                    // Start targeting mode
                    playerActionManager.StartAbilityTargeting(currentUnit, abilityIndex);
                }
            }
        }

        private void HandleAbilityTargetingStarted(AbilityData ability)
        {
            isTargeting = true;
            if (targetingIndicator != null)
            {
                targetingIndicator.SetActive(true);
            }
            if (targetingInstructionsText != null)
            {
                string instructions = GetTargetingInstructions(ability);
                targetingInstructionsText.text = instructions;
                targetingInstructionsText.gameObject.SetActive(true);
            }
        }

        private void HandleAbilityTargetingCancelled()
        {
            isTargeting = false;
            if (targetingIndicator != null)
            {
                targetingIndicator.SetActive(false);
            }
            if (targetingInstructionsText != null)
            {
                targetingInstructionsText.gameObject.SetActive(false);
            }
        }

        private string GetTargetingInstructions(AbilityData ability)
        {
            if (ability == null) return string.Empty;

            string targetTypes = "";
            if (ability.targetsSelf) targetTypes += "self, ";
            if (ability.targetsAlly) targetTypes += "allies, ";
            if (ability.targetsEnemy) targetTypes += "enemies, ";
            if (ability.targetsGround) targetTypes += "ground, ";
            
            // Remove trailing comma and space
            if (targetTypes.Length > 2)
            {
                targetTypes = targetTypes.Substring(0, targetTypes.Length - 2);
            }

            string instructions = $"Select target for {ability.displayName}\n";
            instructions += $"Range: {ability.range} tiles\n";
            if (ability.areaOfEffect > 0)
            {
                instructions += $"Area: {ability.areaOfEffect} tiles\n";
            }
            instructions += $"Can target: {targetTypes}\n";
            instructions += "Press ESC to cancel";

            return instructions;
        }

        private void HandleCommandResult(bool success, string message)
        {
            if (success)
            {
                // Clear targeting UI
                HandleAbilityTargetingCancelled();
                
                // Update ability buttons to reflect new cooldowns/costs
                UpdateAbilityButtons();
            }
            else
            {
                // Show error message
                Debug.LogWarning($"Ability command failed: {message}");
            }
        }

        private void ShowTooltip(AbilityData ability, AbilityButton button)
        {
            if (tooltipPrefab == null) return;

            hoveredButton = button;
            if (currentTooltip == null)
            {
                currentTooltip = Instantiate(tooltipPrefab, transform);
            }

            var tooltipContent = currentTooltip.GetComponent<AbilityTooltipContent>();
            if (tooltipContent != null)
            {
                tooltipContent.UpdateContent(ability);
            }

            // Position tooltip
            RectTransform buttonRect = button.button.GetComponent<RectTransform>();
            RectTransform tooltipRect = currentTooltip.GetComponent<RectTransform>();
            tooltipRect.position = buttonRect.position + (Vector3)tooltipOffset;
        }

        private void HideTooltip()
        {
            if (currentTooltip != null)
            {
                currentTooltip.SetActive(false);
            }
            hoveredButton = null;
        }
    }
} 
