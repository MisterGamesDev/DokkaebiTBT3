using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Dokkaebi.Core;
using UnityEngine.SceneManagement;
using System.Collections;
using Dokkaebi.Utilities;

namespace Dokkaebi.UI
{
    /// <summary>
    /// Handles the game over UI display
    /// </summary>
    public class GameOverUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button restartButton;
        [SerializeField] private CanvasGroup panelCanvasGroup;

        [Header("Messages")]
        [SerializeField] private string player1WinsMessage = "VICTORY!";
        [SerializeField] private string player2WinsMessage = "DEFEAT!";
        [SerializeField] private string drawMessage = "DRAW!";

        [Header("Animation Settings")]
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float messageScaleDuration = 0.3f;
        [SerializeField] private float buttonFadeInDelay = 0.5f;
        [SerializeField] private Color victoryColor = new Color(1f, 0.8f, 0f); // Gold
        [SerializeField] private Color defeatColor = new Color(0.8f, 0.2f, 0.2f); // Red
        [SerializeField] private Color drawColor = new Color(0.7f, 0.7f, 0.7f); // Gray
        
        private GameController gameController;
        private Coroutine animationCoroutine;
        
        private void Awake()
        {
            // Get references
            if (gameController == null)
            {
                gameController = FindObjectOfType<GameController>();
            }
            
            // Ensure we have a CanvasGroup
            if (panelCanvasGroup == null && gameOverPanel != null)
            {
                panelCanvasGroup = gameOverPanel.GetComponent<CanvasGroup>();
                if (panelCanvasGroup == null)
                {
                    panelCanvasGroup = gameOverPanel.AddComponent<CanvasGroup>();
                }
            }
            
            // Hide panel initially
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(false);
                if (panelCanvasGroup != null)
                {
                    panelCanvasGroup.alpha = 0f;
                }
            }
            
            // Set up restart button
            if (restartButton != null)
            {
                restartButton.onClick.AddListener(RestartGame);
                // Hide button initially
                var buttonCanvasGroup = restartButton.GetComponent<CanvasGroup>();
                if (buttonCanvasGroup == null)
                {
                    buttonCanvasGroup = restartButton.gameObject.AddComponent<CanvasGroup>();
                }
                buttonCanvasGroup.alpha = 0f;
            }
        }
        
        private void Start()
        {
            // Subscribe to game over event
            if (gameController != null)
            {
                gameController.OnGameOver += HandleGameOver;
            }
        }
        
        private void OnDestroy()
        {
            // Unsubscribe from events
            if (gameController != null)
            {
                gameController.OnGameOver -= HandleGameOver;
            }
            
            // Clean up button listener
            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(RestartGame);
            }

            // Stop any running animations
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
            }
        }
        
        /// <summary>
        /// Handle game over event
        /// </summary>
        private void HandleGameOver(bool player1Wins)
        {
            SmartLogger.Log($"[GameOverUI] Handling game over. Player 1 Wins: {player1Wins}", LogCategory.UI);

            // Stop any running animations
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
            }

            // Show the panel
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
                
                // Start animation sequence
                animationCoroutine = StartCoroutine(AnimateGameOver(player1Wins));
            }
        }

        private IEnumerator AnimateGameOver(bool player1Wins)
        {
            // Set up initial state
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 0f;
            }

            if (messageText != null)
            {
                messageText.transform.localScale = Vector3.zero;
            }

            var buttonCanvasGroup = restartButton?.GetComponent<CanvasGroup>();
            if (buttonCanvasGroup != null)
            {
                buttonCanvasGroup.alpha = 0f;
            }

            // Fade in panel
            float elapsedTime = 0f;
            while (elapsedTime < fadeInDuration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float normalizedTime = elapsedTime / fadeInDuration;
                
                if (panelCanvasGroup != null)
                {
                    panelCanvasGroup.alpha = Mathf.Lerp(0f, 0.9f, normalizedTime);
                }
                
                yield return null;
            }

            // Set and animate message
            if (messageText != null)
            {
                // Set message and color
                messageText.text = player1Wins ? player1WinsMessage : player2WinsMessage;
                messageText.color = player1Wins ? victoryColor : defeatColor;

                // Scale animation
                elapsedTime = 0f;
                while (elapsedTime < messageScaleDuration)
                {
                    elapsedTime += Time.unscaledDeltaTime;
                    float normalizedTime = elapsedTime / messageScaleDuration;
                    float scale = Mathf.Lerp(0f, 1f, EaseOutBack(normalizedTime));
                    
                    messageText.transform.localScale = new Vector3(scale, scale, scale);
                    yield return null;
                }
                
                messageText.transform.localScale = Vector3.one;
            }

            // Fade in restart button
            yield return new WaitForSecondsRealtime(buttonFadeInDelay);
            
            if (buttonCanvasGroup != null)
            {
                elapsedTime = 0f;
                while (elapsedTime < fadeInDuration)
                {
                    elapsedTime += Time.unscaledDeltaTime;
                    float normalizedTime = elapsedTime / fadeInDuration;
                    
                    buttonCanvasGroup.alpha = Mathf.Lerp(0f, 1f, normalizedTime);
                    yield return null;
                }
                
                buttonCanvasGroup.alpha = 1f;
            }
        }

        private float EaseOutBack(float x)
        {
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
        
        /// <summary>
        /// Restart the game
        /// </summary>
        private void RestartGame()
        {
            SmartLogger.Log("[GameOverUI] Restarting game...", LogCategory.UI);

            // Hide the panel
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(false);
            }
            
            // Reset game state
            if (gameController != null)
            {
                gameController.ResetGame();
            }
            
            // Reload the scene
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
} 