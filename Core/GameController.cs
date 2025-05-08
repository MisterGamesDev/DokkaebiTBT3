using UnityEngine;
using Dokkaebi.Core;
using Dokkaebi.Core.Data;
using Dokkaebi.Utilities;
using Dokkaebi.Units;
using Dokkaebi.Interfaces;
using Dokkaebi.Common;
using Dokkaebi.UI;

namespace Dokkaebi.Core
{
    public class GameController : MonoBehaviour {
        // Game state
        public enum GameState {
            Playing,
            GameOver
        }
        
        private GameState currentState = GameState.Playing;
        private bool player1Wins = false;
        
        // Optional references
        [SerializeField] private UnitManager unitManager;
        [SerializeField] private DokkaebiTurnSystemCore turnSystem;
        [SerializeField] private TeamStatusUI teamStatusUI;
        
        // Events
        public System.Action<bool> OnGameOver; // bool parameter is true if player 1 wins
        
        void Awake()
        {
            SmartLogger.Log("[GameController.Awake] Method entered.", LogCategory.Game, this);
            
            // Ensure manager and UI references are assigned (use Inspector or Find)
            if (unitManager == null) unitManager = FindAnyObjectByType<UnitManager>();
            if (turnSystem == null) turnSystem = FindAnyObjectByType<DokkaebiTurnSystemCore>();
            if (teamStatusUI == null) teamStatusUI = FindAnyObjectByType<TeamStatusUI>();

            SmartLogger.Log($"[GameController.Awake] References: unitManager null={unitManager == null}, turnSystem null={turnSystem == null}, teamStatusUI null={teamStatusUI == null}", LogCategory.Game, this);
            
            SmartLogger.Log("[GameController.Awake] Method exiting.", LogCategory.Game, this);
        }
        
        void Start()
        {
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] GameController: ENTER {nameof(Start)}");
            SmartLogger.Log("[GameController.Start] Method entered.", LogCategory.Game, this);

            // Initialization logic moved to InitializeGame()

            SmartLogger.Log("[GameController.Start] About to call InitializeGame().", LogCategory.Game, this);
            InitializeGame();

            SmartLogger.Log("[GameController.Start] Method exiting.", LogCategory.Game, this);
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] GameController: EXIT {nameof(Start)}");
        }
        
        void OnDestroy() {
            // Unsubscribe from events
            if (unitManager != null) {
                unitManager.OnUnitDefeated -= HandleUnitDefeated;
            }
            
            if (turnSystem != null) {
                turnSystem.OnTurnResolutionEnd -= CheckWinLossConditions;
            }
        }
        
        void BeginGame() {
            // Reference to the turn system
            if (turnSystem != null) {
                // Make sure unitManager reference is valid
                if (unitManager != null)
                {
                    SmartLogger.Log("[GameController.BeginGame] Calling UnitManager.SpawnUnitsFromConfiguration...", LogCategory.Game, this);
                    unitManager.SpawnUnitsFromConfiguration();
                }
                else
                {
                    SmartLogger.LogError("[GameController.BeginGame] Cannot spawn units: UnitManager reference is null!", LogCategory.Game, this);
                }

                // Register all units (This will now find the newly spawned units)
                var units = FindObjectsOfType<DokkaebiUnit>();
                SmartLogger.Log($"[GameController.BeginGame] Found {units.Length} units to register", LogCategory.Game, this);
                foreach (var unit in units) {
                    SmartLogger.Log($"[GameController.BeginGame] Registering unit {unit.GetUnitName()} (ID: {unit.UnitId})", LogCategory.Game, this);
                    turnSystem.RegisterUnit(unit);
                }

                // Log that the game has begun
                SmartLogger.Log("Game has begun!", LogCategory.TurnSystem, this);
            }
        }
        
        /// <summary>
        /// Handle when a unit is defeated
        /// </summary>
        void HandleUnitDefeated(DokkaebiUnit unit) {
            SmartLogger.Log($"Unit defeated: {unit.GetUnitName()} (Player: {unit.IsPlayer()})", LogCategory.Game);
            
            // Check win/loss conditions whenever a unit is defeated
            CheckWinLossConditions();
        }
        
        /// <summary>
        /// Check if either player has won or lost
        /// </summary>
        public void CheckWinLossConditions() {
            // Skip if game is already over
            if (currentState == GameState.GameOver) return;
            
            if (unitManager == null) {
                SmartLogger.LogWarning("Cannot check win/loss conditions: UnitManager not found", LogCategory.Game);
                return;
            }
            
            // Check if each player has remaining units
            bool player1HasUnits = unitManager.HasRemainingUnits(true);
            bool player2HasUnits = unitManager.HasRemainingUnits(false);
            
            SmartLogger.Log($"Checking win/loss conditions - Player 1 has units: {player1HasUnits}, Player 2 has units: {player2HasUnits}", LogCategory.Game);
            
            // Check win conditions
            if (!player2HasUnits && player1HasUnits) {
                // Player 1 wins (all enemy units defeated)
                SmartLogger.Log("GAME OVER - Player 1 WINS! All enemy units defeated", LogCategory.Game);
                HandleGameOver(true);
            } 
            else if (!player1HasUnits && player2HasUnits) {
                // Player 2 wins (all player units defeated)
                SmartLogger.Log("GAME OVER - Player 2 WINS! All player units defeated", LogCategory.Game);
                HandleGameOver(false);
            }
            else if (!player1HasUnits && !player2HasUnits) {
                // Draw condition - both players have no units left
                SmartLogger.Log("GAME OVER - DRAW! Both players have no units remaining", LogCategory.Game);
                HandleGameOver(false); // Default to player 2 win for draw condition
            }
        }
        
        /// <summary>
        /// Handle the game over state
        /// </summary>
        private void HandleGameOver(bool player1Wins) {
            // Skip if already in game over state
            if (currentState == GameState.GameOver) return;

            // Set game state
            currentState = GameState.GameOver;
            this.player1Wins = player1Wins;
            
            // Log the result
            string winnerText = player1Wins ? "Player 1" : "Player 2";
            SmartLogger.Log($"GAME OVER - {winnerText} Wins!", LogCategory.Game);
            
            // Pause the game
            Time.timeScale = 0;
            
            // Trigger event
            OnGameOver?.Invoke(player1Wins);
            
            // Display win message with color
            SmartLogger.Log($"<color=yellow><b>GAME OVER - {winnerText} WINS!</b></color>", LogCategory.Game);

            // If turn system exists, set it to game over phase
            if (turnSystem != null)
            {
                turnSystem.ForceTransitionTo(TurnPhase.GameOver);
            }
        }
        
        /// <summary>
        /// Return the current state of the game
        /// </summary>
        public GameState GetGameState() {
            return currentState;
        }
        
        /// <summary>
        /// Check if the game is over
        /// </summary>
        public bool IsGameOver() {
            return currentState == GameState.GameOver;
        }
        
        /// <summary>
        /// Get the winner of the game
        /// </summary>
        public int GetWinner() {
            if (currentState != GameState.GameOver) return 0;
            return player1Wins ? 1 : 2;
        }
        
        /// <summary>
        /// Reset the game
        /// </summary>
        public void ResetGame() {
            // Reset game state
            currentState = GameState.Playing;
            player1Wins = false;
            
            // Reset time scale
            Time.timeScale = 1;
            
            // Reset turn system if available
            if (turnSystem != null)
            {
                turnSystem.ResetTurnSystem();
            }
            
            // Reset unit manager if available
            if (unitManager != null)
            {
                unitManager.RemoveAllUnits();
                unitManager.SpawnUnitsFromConfiguration();
            }
            
            SmartLogger.Log("Game has been reset", LogCategory.Game);
        }

        private void SpawnPlayerUnit(UnitSpawnConfig unitInfo)
        {
            var gridPosition = GridPosition.FromVector2Int(unitInfo.spawnPosition);
            SmartLogger.Log($"[GAMECONTROLLER] Requesting UnitManager to spawn Player Unit: {unitInfo.unitDefinition.displayName} at {gridPosition}.", LogCategory.Unit, this);
            var spawnedUnit = UnitManager.Instance.SpawnAndRegisterUnit(unitInfo.unitDefinition, gridPosition, 1); // TeamId 1 for player
            SmartLogger.Log($"[GAMECONTROLLER] UnitManager spawned Player Unit: ID={spawnedUnit?.UnitId ?? -1}, Name={spawnedUnit?.DisplayName ?? "NULL"}, TeamId={spawnedUnit?.TeamId ?? -1}", LogCategory.Unit, this);
        }

        private void SpawnEnemyUnit(UnitSpawnConfig unitInfo)
        {
            var gridPosition = GridPosition.FromVector2Int(unitInfo.spawnPosition);
            SmartLogger.Log($"[GAMECONTROLLER] Requesting UnitManager to spawn Enemy Unit: {unitInfo.unitDefinition.displayName} at {gridPosition}.", LogCategory.Unit, this);
            var spawnedUnit = UnitManager.Instance.SpawnAndRegisterUnit(unitInfo.unitDefinition, gridPosition, 2); // TeamId 2 for enemy
            SmartLogger.Log($"[GAMECONTROLLER] UnitManager spawned Enemy Unit: ID={spawnedUnit?.UnitId ?? -1}, Name={spawnedUnit?.DisplayName ?? "NULL"}, TeamId={spawnedUnit?.TeamId ?? -1}", LogCategory.Unit, this);
        }

        private void SpawnUnits()
        {
            var spawnData = DataManager.Instance.GetUnitSpawnData();
            if (spawnData == null)
            {
                SmartLogger.LogError("No UnitSpawnData assigned in DataManager!", LogCategory.Game, this);
                return;
            }

            // Spawn all configured units
            foreach (var unitInfo in spawnData.playerUnitSpawns)
            {
                SpawnPlayerUnit(unitInfo);
            }

            foreach (var unitInfo in spawnData.enemyUnitSpawns)
            {
                SpawnEnemyUnit(unitInfo);
            }
        }

        public void InitializeGame()
        {
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] GameController: ENTER {nameof(InitializeGame)}");
            SmartLogger.Log("[GameController.InitializeGame] Method entered.", LogCategory.Game, this);
            
            // 1. Null check critical managers (Assigned in Awake)
            if (unitManager == null)
            {
                SmartLogger.LogError("[GameController.InitializeGame] Cannot initialize: UnitManager reference is null! Ensure it's assigned in Inspector or found in Awake.", LogCategory.Game, this);
                return;
            }
            if (turnSystem == null)
            {
                SmartLogger.LogError("[GameController.InitializeGame] Cannot initialize: DokkaebiTurnSystemCore reference is null! Ensure it's assigned in Inspector or found in Awake.", LogCategory.Game, this);
                return;
            }
            // DataManager is checked before spawning

            // 2. Spawn Units
            SmartLogger.Log("[GameController.InitializeGame] Spawning units...", LogCategory.Game, this);
            // Ensure DataManager and UnitManager instances are available
            if (DataManager.Instance != null && unitManager != null)
            {
                SpawnUnits();
            }
            else
            {
                SmartLogger.LogError($"[GameController.InitializeGame] Cannot spawn units: DataManager.Instance is null ({DataManager.Instance == null}) or unitManager is null ({unitManager == null})!", LogCategory.Game, this);
            }

            // 3. Subscribe Events
            SmartLogger.Log("[GameController.InitializeGame] Subscribing to events...", LogCategory.Game, this);
            // Ensure managers are not null before subscribing (checked earlier)
            turnSystem.OnTurnResolutionEnd += CheckWinLossConditions;
            unitManager.OnUnitDefeated += HandleUnitDefeated;

            // 4. Populate Team Status UI
            SmartLogger.Log($"[GameController.InitializeGame] Populating TeamStatusUI. teamStatusUI is null: {teamStatusUI == null}", LogCategory.Game, this);
            teamStatusUI?.PopulateTeamStatusUI();

            // 5. Register Units with Turn System
            var units = unitManager.GetAliveUnits(); // Use UnitManager field
            SmartLogger.Log($"[GameController.InitializeGame] Found {units.Count} units to register with TurnSystem.", LogCategory.Game, this);
            foreach (var unit in units)
            {
                SmartLogger.Log($"[GameController.InitializeGame] Registering unit {unit.GetUnitName()} (ID: {unit.UnitId})", LogCategory.Game, this);
                turnSystem.RegisterUnit(unit); // Use TurnSystem field
            }

            // 6. Start the Turn System
            SmartLogger.Log("[GameController.InitializeGame] Starting the turn system...", LogCategory.Game, this);
            if (turnSystem != null)
            {
                SmartLogger.Log($"[GameController.InitializeGame] BEFORE turnSystem.NextPhase(). Current DTSCore Phase: {turnSystem.GetCurrentPhase()}, Current Turn: {turnSystem.GetCurrentTurn()}", LogCategory.Game, this);
            }
            else
            {
                SmartLogger.LogError("[GameController.InitializeGame] BEFORE turnSystem.NextPhase(). turnSystem is NULL!", LogCategory.Game, this);
            }
            //turnSystem.NextPhase(); // Use TurnSystem field

            SmartLogger.Log("[GameController.InitializeGame] Initialization complete.", LogCategory.Game, this);
            UnityEngine.Debug.LogError($"[DEBUG_FREEZE] GameController: EXIT {nameof(InitializeGame)}");
        }
    }
}
