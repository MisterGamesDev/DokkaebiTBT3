using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Dokkaebi.Core;
using Dokkaebi.Common;
using Dokkaebi.Interfaces;
using Dokkaebi.Utilities;

namespace Dokkaebi.UI
{
    public class TurnPhaseUI : MonoBehaviour
    {
        [Header("Phase Display")]
        [SerializeField] private TextMeshProUGUI phaseText;
        [SerializeField] private TextMeshProUGUI turnNumberText;
        [SerializeField] private TextMeshProUGUI activePlayerText;
        // [SerializeField] private TextMeshProUGUI phaseTimerText; // Replaced by icon + seconds text

        [Header("Phase Timer Visuals")]
        [SerializeField] private Image phaseTimerIconImage;
        [SerializeField] private TextMeshProUGUI phaseTimerSecondsText;

        [Header("Phase Icons")]
        [SerializeField] private GameObject[] phaseIcons;
        [SerializeField] private Color activePhaseColor = Color.yellow;
        [SerializeField] private Color inactivePhaseColor = Color.gray;

        private ITurnSystem turnSystem;

        private void Awake()
        {
            turnSystem = FindObjectOfType<DokkaebiTurnSystemCore>();
            if (turnSystem == null)
            {
                SmartLogger.LogError("TurnPhaseUI: TurnSystem not found in scene!", LogCategory.UI, this);
            }
        }

        private void OnEnable()
        {
            if (turnSystem != null)
            {
                // SmartLogger.Log("[TurnPhaseUI] Subscribing to turn system events", LogCategory.TurnSystem);
                turnSystem.OnPhaseChanged += HandlePhaseStart;
                turnSystem.OnTurnChanged += HandleTurnStart;
                turnSystem.OnActivePlayerChanged += HandleActivePlayerChanged;
            }
        }

        private void Start()
        {
            // Display initial state when UI becomes active
            if (turnSystem != null)
            {
                // Get initial state from turn system
                TurnPhase initialPhase = turnSystem.CurrentPhase;
                int initialTurn = turnSystem.CurrentTurn;
                int initialActivePlayer = turnSystem.ActivePlayerId;

                // Update UI with initial state
                HandlePhaseStart(initialPhase);
                HandleTurnStart(initialTurn);
                HandleActivePlayerChanged(initialActivePlayer);
            }
        }

        private void Update()
        {
            if (turnSystem != null)
            {
                UpdatePhaseTimer(turnSystem.GetRemainingPhaseTime());
            }
        }

        private void OnDisable()
        {
            if (turnSystem != null)
            {
                // SmartLogger.Log("[TurnPhaseUI] Unsubscribing from turn system events", LogCategory.TurnSystem);
                turnSystem.OnPhaseChanged -= HandlePhaseStart;
                turnSystem.OnTurnChanged -= HandleTurnStart;
                turnSystem.OnActivePlayerChanged -= HandleActivePlayerChanged;
            }
        }

        private void HandlePhaseStart(TurnPhase phase)
        {
            UpdatePhaseDisplay(phase);
            UpdatePhaseIcons(phase);
        }

        private void HandleTurnStart(int turnNumber)
        {
            UpdateTurnDisplay(turnNumber, turnSystem.ActivePlayerId);
        }

        private void HandleActivePlayerChanged(int playerId)
        {
            // SmartLogger.Log($"[TurnPhaseUI] HandleActivePlayerChanged called with playerId: {playerId}", LogCategory.TurnSystem);
            UpdateTurnDisplay(turnSystem.CurrentTurn, playerId);
        }

        private void UpdatePhaseDisplay(TurnPhase phase)
        {
            if (phaseText != null)
            {
                string phaseName = FormatPhaseName(phase);
                phaseText.text = phaseName;
                
                // Color the phase text based on the phase type
                Color phaseColor = GetPhaseColor(phase);
                phaseText.color = phaseColor;
                
                // SmartLogger.Log($"[TurnPhaseUI] Updated phase display to: {phaseName}", LogCategory.TurnSystem);
            }
        }

        private void UpdateTurnDisplay(int turnNumber, int activePlayerNumber)
        {
            // SmartLogger.Log($"[TurnPhaseUI] UpdateTurnDisplay called. Phase: {turnSystem?.CurrentPhase}, Received activePlayerNumber: {activePlayerNumber}", LogCategory.TurnSystem);
            
            if (turnNumberText != null)
            {
                turnNumberText.text = $"Round {turnNumber}";
            }

            if (activePlayerText != null)
            {
                if (activePlayerNumber == 1)
                {
                    activePlayerText.text = "Player: 1";
                }
                else if (activePlayerNumber == 2)
                {
                    activePlayerText.text = "Player: 2";
                }
                else // activePlayerNumber is likely 0
                {
                    // Check if it's the movement phase for the special "Both" case
                    if (turnSystem != null && turnSystem.CurrentPhase == TurnPhase.MovementPhase)
                    {
                        activePlayerText.text = "Player: Both";
                    }
                    else
                    {
                        activePlayerText.text = "Player: "; // Default for other non-player-specific phases
                    }
                }
                
                // Color the active player text based on the player number
                activePlayerText.color = activePlayerNumber == 1 ? new Color(0f, 0.886f, 1f) : 
                                        activePlayerNumber == 2 ? new Color(1f, 0.298f, 0.298f) : 
                                        Color.white;
            }
        }

        private void UpdatePhaseTimer(float remainingTime)
        {
            // Check if the new UI elements are assigned
            bool visualsAssigned = phaseTimerIconImage != null && phaseTimerSecondsText != null;

            if (visualsAssigned)
            {
                if (remainingTime > 0)
                {
                    int seconds = Mathf.CeilToInt(remainingTime);
                    phaseTimerSecondsText.text = $"{seconds}s"; // Set only seconds
                    
                    // Color the timer text based on remaining time
                    if (seconds <= 5)
                    {
                        // Flash red for last 5 seconds
                        float flash = Mathf.PingPong(Time.time * 4, 1);
                        phaseTimerSecondsText.color = Color.Lerp(Color.red, Color.white, flash);
                    }
                    else if (seconds <= 10)
                    {
                        phaseTimerSecondsText.color = Color.yellow;
                    }
                    else
                    {
                        phaseTimerSecondsText.color = Color.white;
                    }
                    
                    // Show icon and text
                    phaseTimerIconImage.gameObject.SetActive(true);
                    phaseTimerSecondsText.gameObject.SetActive(true);
                }
                else
                {
                    // Hide icon and text
                    phaseTimerIconImage.gameObject.SetActive(false);
                    phaseTimerSecondsText.gameObject.SetActive(false);
                }
            }
            /* // Old logic using phaseTimerText
            if (phaseTimerText != null)
            {
                if (remainingTime > 0)
                {
                    int seconds = Mathf.CeilToInt(remainingTime);
                    phaseTimerText.text = $"Time: {seconds}s";
                    
                    // Color the timer based on remaining time
                    if (seconds <= 5)
                    {
                        // Flash red for last 5 seconds
                        float flash = Mathf.PingPong(Time.time * 4, 1);
                        phaseTimerText.color = Color.Lerp(Color.red, Color.white, flash);
                    }
                    else if (seconds <= 10)
                    {
                        phaseTimerText.color = Color.yellow;
                    }
                    else
                    {
                        phaseTimerText.color = Color.white;
                    }
                    
                    phaseTimerText.gameObject.SetActive(true);
                }
                else
                {
                    phaseTimerText.gameObject.SetActive(false);
                }
            }
            */
        }

        private Color GetPhaseColor(TurnPhase phase)
        {
            switch (phase)
            {
                case TurnPhase.Opening:
                    return new Color(0.7f, 0.7f, 1f); // Light blue
                case TurnPhase.MovementPhase:
                    return new Color(0.7f, 1f, 0.7f); // Light green
                case TurnPhase.AuraPhase1A:
                case TurnPhase.AuraPhase2A:
                    return new Color(0f, 0.886f, 1f); // Updated Player 1 color (#00E2FF)
                case TurnPhase.AuraPhase1B:
                case TurnPhase.AuraPhase2B:
                    return new Color(1f, 0.298f, 0.298f); // Updated Player 2 color (#FF4C4C)
                case TurnPhase.Resolution:
                    return new Color(1f, 0.7f, 0.3f); // Orange
                case TurnPhase.EndTurn:
                    return new Color(0.7f, 0.7f, 0.7f); // Gray
                case TurnPhase.GameOver:
                    return new Color(1f, 0.5f, 0f); // Gold
                default:
                    return Color.white;
            }
        }

        private void UpdatePhaseIcons(TurnPhase currentPhase)
        {
            if (phaseIcons == null) return;

            for (int i = 0; i < phaseIcons.Length; i++)
            {
                if (phaseIcons[i] != null)
                {
                    var iconImage = phaseIcons[i].GetComponent<Image>();
                    if (iconImage != null)
                    {
                        bool isCurrentPhase = (TurnPhase)i == currentPhase;
                        iconImage.color = isCurrentPhase ? activePhaseColor : inactivePhaseColor;
                        
                        // Optional: Add pulsing effect to current phase icon
                        if (isCurrentPhase)
                        {
                            float pulse = Mathf.PingPong(Time.time, 0.3f) + 0.7f; // Pulse between 70% and 100% brightness
                            iconImage.color = new Color(
                                activePhaseColor.r * pulse,
                                activePhaseColor.g * pulse,
                                activePhaseColor.b * pulse,
                                activePhaseColor.a
                            );
                        }
                    }
                }
            }
        }

        private string FormatPhaseName(TurnPhase phase)
        {
            switch (phase)
            {
                case TurnPhase.Opening:
                    return "Preparation Phase";
                case TurnPhase.MovementPhase:
                    return "Positioning Phase";
                case TurnPhase.BufferPhase:
                    return "Buffer Phase";
                case TurnPhase.AuraPhase1A:
                case TurnPhase.AuraPhase1B:
                case TurnPhase.AuraPhase2A:
                case TurnPhase.AuraPhase2B:
                    return "Command Phase";
                case TurnPhase.Resolution:
                    return "Resolution Phase";
                case TurnPhase.EndTurn:
                    return "End Turn";
                case TurnPhase.GameOver:
                    return "Game Over";
                default:
                    return phase.ToString();
            }
        }
    }
} 
