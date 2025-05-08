using UnityEngine;
using System.Collections.Generic;
using Dokkaebi.AI.Data; // Required for AIWorldState, AIGoal, AIAction
using Dokkaebi.Utilities; // Required for SmartLogger
using System.Linq; // Required for LINQ
using Dokkaebi.Interfaces; // <-- Add this for GridPosition
using System; // Added for InvalidCastException
using Dokkaebi.Units; // Required for DokkaebiUnit
using Dokkaebi.Core; // Required for UnitManager
using Dokkaebi.Core.Data; // Required for AbilityData
using Dokkaebi.AI.Data.Goals;

namespace Dokkaebi.AI
{
    /// <summary>
    /// TEMPORARY PLACEHOLDER for the AIPlanner singleton.
    /// Will be replaced with a dedicated planner script implementing A* or similar.
    /// </summary>
    public class AIPlanner : MonoBehaviour
    {
        [Header("Planner Settings")]
        [Tooltip("Maximum number of nodes the A* search will explore before giving up.")]
        [SerializeField] private int maxNodesToExplore = 1000; // Limit the search space

        [Tooltip("Maximum number of actions allowed in a plan.")]
        [SerializeField] private int maxPlanDepth = 5; // Limit the length of the plan

        [Tooltip("Maximum time (in seconds) the planner can run for a single agent's plan.")]
        [SerializeField] private float maxPlanningTime = 0.05f; // Time limit per agent (50ms)

        // --- ADDED: Debug flag for deep planner logging ---
        [Tooltip("Enable detailed debug logging for the A* planner (very verbose, use only for debugging)")]
        [SerializeField] private bool debugPlannerSearch = false;

        // Tile reservation system for this AI turn
        private static HashSet<GridPosition> reservedTiles = new HashSet<GridPosition>();
        public static void ClearReservations()
        {
            reservedTiles.Clear();
            SmartLogger.Log("[AIPlanner] Cleared all reserved tiles for new AI turn.", LogCategory.AI);
        }

        // Helper to find the closest valid alternative tile to the goal
        private static GridPosition? FindClosestValidAlternative(
            AIWorldState worldState,
            GridPosition startPos,
            int movementRange,
            GridPosition desiredGoalPos,
            int unitId,
            Dictionary<int, GridPosition> plannedMoves)
        {
            SmartLogger.Log($"[AIPlanner.FindClosestValidAlternative] StartPos: {startPos}, MoveRange: {movementRange}, DesiredGoalPos: {desiredGoalPos}", LogCategory.AI);
            var visited = new HashSet<GridPosition>();
            var queue = new Queue<(GridPosition pos, int depth)>();
            queue.Enqueue((startPos, 0));
            visited.Add(startPos);
            GridPosition? bestAlternative = null;
            int bestDistance = int.MaxValue;

            while (queue.Count > 0)
            {
                var (current, depth) = queue.Dequeue();
                // Don't check the start tile itself as an alternative
                if (current != startPos)
                {
                    // Validation checks
                    bool isWalkable;
                    if (!worldState.GridState.IsWalkable.TryGetValue(current, out isWalkable))
                    {
                        isWalkable = false; // Ensure walkable is assigned if TryGetValue returns false
                    }
                    bool isOccupied = worldState.IsTileOccupied(current);
                    bool isReserved = reservedTiles.Contains(current);
                    bool isTargeted = worldState.IsTileTargetedByOtherAI(current, unitId, plannedMoves);
                    bool isValid = isWalkable && !isOccupied && !isReserved && !isTargeted;
                    int distToGoal = CalculateGridDistance(current, desiredGoalPos);
                    SmartLogger.Log($"[AIPlanner.FindClosestValidAlternative] Checking tile {current}: Depth={depth}, Walkable={isWalkable}, Occupied={isOccupied}, Reserved={isReserved}, Targeted={isTargeted}, Valid={isValid}, DistToGoal={distToGoal}", LogCategory.AI);
                    if (isValid)
                    {
                        if (distToGoal < bestDistance)
                        {
                            bestAlternative = current;
                            bestDistance = distToGoal;
                            SmartLogger.Log($"[AIPlanner.FindClosestValidAlternative] New best alternative: {current} (DistToGoal={distToGoal})", LogCategory.AI);
                        }
                    }
                }
                // BFS: enqueue neighbors if within movement range
                if (depth < movementRange)
                {
                    foreach (var neighbor in GetOrthogonalNeighbors(current))
                    {
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue((neighbor, depth + 1));
                        }
                    }
                }
            }
            if (bestAlternative.HasValue)
            {
                SmartLogger.Log($"[AIPlanner.FindClosestValidAlternative] Final selected alternative: {bestAlternative.Value}", LogCategory.AI);
            }
            else
            {
                SmartLogger.Log($"[AIPlanner.FindClosestValidAlternative] No valid alternative found.", LogCategory.AI);
            }
            return bestAlternative;
        }

        // Helper to get orthogonal neighbors (up, down, left, right)
        private static IEnumerable<GridPosition> GetOrthogonalNeighbors(GridPosition pos)
        {
            yield return new GridPosition(pos.x + 1, pos.z);
            yield return new GridPosition(pos.x - 1, pos.z);
            yield return new GridPosition(pos.x, pos.z + 1);
            yield return new GridPosition(pos.x, pos.z - 1);
        }

        // Helper for Manhattan distance
        private static int CalculateGridDistance(GridPosition a, GridPosition b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.z - b.z);
        }

        /// <summary>
        /// Finds a sequence of actions (a plan) to achieve the selected goal from the current world state.
        /// Implements the A* search algorithm for GOAP.
        /// </summary>
        /// <param name="startState">The current AIWorldState (should be a clone).</param>
        /// <param name="agentUnitId">The ID of the agent unit.</param>
        /// <param name="goals">A list containing the single highest-priority unachieved goal.</param>
        /// <param name="availableActions">A list of actions available to the agent.</param>
        /// <returns>A list of AIAction instances representing the plan, or null if no plan is found.</returns>
        public List<AIAction> FindPlan(AIWorldState startState, int agentUnitId, List<AIGoal> goals, List<AIAction> availableActions)
        {
            SmartLogger.Log($"[AI] [AIPlanner.FindPlan] Called for goal: {goals?.FirstOrDefault()?.GoalName ?? "NULL"} with {availableActions?.Count ?? 0} actions.", LogCategory.AI, this);
            SmartLogger.Log($"[AI] [AIPlanner.FindPlan] Started planning for Goal: {goals?.FirstOrDefault()?.GoalName ?? "<none>"} (AgentId: {agentUnitId})", LogCategory.AI, this);
            SmartLogger.Log("Planner Init Step 1.1 - After initial log", LogCategory.AI, this);
            SmartLogger.Log($"[AI] [AIPlanner.FindPlan] Unit {agentUnitId}: Value of startState before check: {(startState != null ? startState.ToString() : "NULL")}", LogCategory.AI, this);
            if (startState == null)
            {
                SmartLogger.LogError($"[AI] [AIPlanner.FindPlan] Unit {agentUnitId}: ERROR: startState is NULL inside the check at line 140!", LogCategory.AI, this);
                SmartLogger.Log("Planner Init Step 1.2 - startState is NULL", LogCategory.AI, this);
                SmartLogger.Log($"[AI] [AIPlanner.FindPlan] Unit {agentUnitId}: ERROR: startState is NULL!", LogCategory.AI, this);
                return null;
            }
            SmartLogger.Log("Planner Init Step 1.3 - startState is not null", LogCategory.AI, this);
            if (goals == null)
            {
                SmartLogger.Log("Planner Init Step 1.4 - goals is NULL", LogCategory.AI, this);
                SmartLogger.Log($"[AI] [AIPlanner.FindPlan] Unit {agentUnitId}: ERROR: goals is NULL!", LogCategory.AI, this);
                return null;
            }
            SmartLogger.Log("Planner Init Step 1.5 - goals is not null", LogCategory.AI, this);
            if (goals.Count == 0)
            {
                SmartLogger.Log("Planner Init Step 1.6 - goals.Count == 0", LogCategory.AI, this);
                SmartLogger.Log($"[AI] [AIPlanner.FindPlan] Unit {agentUnitId}: ERROR: goals.Count == 0!", LogCategory.AI, this);
                return null;
            }
            SmartLogger.Log("Planner Init Step 1.7 - goals.Count > 0", LogCategory.AI, this);
            var selectedGoal = goals[0];
            SmartLogger.Log("Planner Init Step 1.8 - selectedGoal assigned", LogCategory.AI, this);
            if (selectedGoal == null)
            {
                SmartLogger.Log("Planner Init Step 1.9 - selectedGoal is NULL", LogCategory.AI, this);
                SmartLogger.Log($"[AI] [AIPlanner.FindPlan] Unit {agentUnitId}: WARNING: selectedGoal is NULL!", LogCategory.AI, this);
            }
            SmartLogger.Log("Planner Init Step 1.10 - selectedGoal is not null", LogCategory.AI, this);
            if (selectedGoal.GoalName == null)
            {
                SmartLogger.Log("Planner Init Step 1.11 - selectedGoal.GoalName is NULL", LogCategory.AI, this);
                SmartLogger.Log($"[AI] [AIPlanner.FindPlan] Unit {agentUnitId}: WARNING: selectedGoal.GoalName is NULL!", LogCategory.AI, this);
            }
            SmartLogger.Log("Planner Init Step 1.12 - selectedGoal.GoalName checked", LogCategory.AI, this);
            if (availableActions == null)
            {
                SmartLogger.Log("Planner Init Step 1.13 - availableActions is NULL", LogCategory.AI, this);
                SmartLogger.Log($"[AI] [AIPlanner.FindPlan] Unit {agentUnitId}: ERROR: availableActions is NULL!", LogCategory.AI, this);
                return null;
            }
            SmartLogger.Log("Planner Init Step 1.14 - availableActions is not null", LogCategory.AI, this);
            // --- Begin core planner initialization ---
            SmartLogger.Log("Planner Init Step 2.1 - Before openSet initialization", LogCategory.AI, this);
            var openSet = new PriorityQueue<Node>();
            SmartLogger.Log("Planner Init Step 2.2 - After openSet initialization", LogCategory.AI, this);
            SmartLogger.Log("Planner Init Step 2.3 - Before closedSet initialization", LogCategory.AI, this);
            var closedSet = new HashSet<AIWorldState>();
            SmartLogger.Log("Planner Init Step 2.4 - After closedSet initialization", LogCategory.AI, this);
            SmartLogger.Log("Planner Init Step 2.5 - Before cameFrom initialization", LogCategory.AI, this);
            var cameFrom = new Dictionary<AIWorldState, Node>();
            SmartLogger.Log("Planner Init Step 2.6 - After cameFrom initialization", LogCategory.AI, this);
            SmartLogger.Log("Planner Init Step 2.7 - Before startNode creation", LogCategory.AI, this);
            Node startNode = new Node(startState, null, null, 0f, CalculateHeuristic(startState, selectedGoal), 0);
            SmartLogger.Log("Planner Init Step 2.8 - After startNode creation", LogCategory.AI, this);
            SmartLogger.Log("Planner Init Step 2.9 - Before openSet.Enqueue(startNode, startNode.costF)", LogCategory.AI, this);
            openSet.Enqueue(startNode, startNode.costF);
            SmartLogger.Log("Planner Init Step 2.10 - After openSet.Enqueue(startNode, startNode.costF)", LogCategory.AI, this);
            SmartLogger.Log("Planner Init Step 2.11 - Before cameFrom[startState] = startNode", LogCategory.AI, this);
            cameFrom[startState] = startNode;
            SmartLogger.Log("Planner Init Step 2.12 - After cameFrom[startState] = startNode", LogCategory.AI, this);
            SmartLogger.Log("Planner Init Step 2.13 - Before try block", LogCategory.AI, this);
            using (new PerformanceScope($"AIPlanner.FindPlan for Unit {agentUnitId}"))
            {
                try
                {
                    SmartLogger.Log($"[AIPlanner.FindPlan] ========== PLANNING START ==========", LogCategory.AI, this);
                    SmartLogger.Log($"[AIPlanner.FindPlan] Planning for Unit {agentUnitId}", LogCategory.AI, this);
                    
                    if (goals != null && goals.Count > 0)
                    {
                        SmartLogger.Log($"[AIPlanner.FindPlan] Goal Details:", LogCategory.AI, this);
                        SmartLogger.Log($"- Goal Name: {goals[0].GoalName}", LogCategory.AI, this);
                        SmartLogger.Log($"- Goal Type: {goals[0].GetType().Name}", LogCategory.AI, this);
                        SmartLogger.Log($"- Goal Priority: {goals[0].Priority}", LogCategory.AI, this);
                    }

                    // Log summary of incoming AIWorldState
                    SmartLogger.Log($"[AIPlanner.FindPlan] Initial World State:", LogCategory.AI, this);
                    var agentState = startState.GetUnitState(agentUnitId);
                    if (agentState != null)
                    {
                        SmartLogger.Log($"Agent Unit:", LogCategory.AI, this);
                        SmartLogger.Log($"- Unit ID: {agentState.UnitId}", LogCategory.AI, this);
                        SmartLogger.Log($"- Position: {agentState.Position}", LogCategory.AI, this);
                        SmartLogger.Log($"- HP: {agentState.CurrentHP}/{agentState.MaxHP}", LogCategory.AI, this);
                        SmartLogger.Log($"- Aura: {agentState.CurrentAura}", LogCategory.AI, this);
                        SmartLogger.Log($"- Movement Range: {agentState.MovementRange}", LogCategory.AI, this);
                        SmartLogger.Log($"- Available Abilities: [{string.Join(", ", agentState.AvailableAbilityIds)}]", LogCategory.AI, this);
                        SmartLogger.Log($"- Active Effects: [{string.Join(", ", agentState.ActiveStatusEffects)}]", LogCategory.AI, this);
                    }

                    SmartLogger.Log($"Other Units:", LogCategory.AI, this);
                    foreach (var unit in startState.UnitStates.Where(u => u.UnitId != agentUnitId))
                    {
                        SmartLogger.Log($"- Unit ID: {unit.UnitId}", LogCategory.AI, this);
                        SmartLogger.Log($"  Team: {unit.TeamId}", LogCategory.AI, this);
                        SmartLogger.Log($"  Position: {unit.Position}", LogCategory.AI, this);
                        SmartLogger.Log($"  HP: {unit.CurrentHP}/{unit.MaxHP}", LogCategory.AI, this);
                        SmartLogger.Log($"  Alive: {unit.IsAlive}", LogCategory.AI, this);
                    }

                    SmartLogger.Log($"Grid State:", LogCategory.AI, this);
                    SmartLogger.Log($"- Walkable Tiles: {startState.GridState.IsWalkable.Count}", LogCategory.AI, this);
                    SmartLogger.Log($"- Occupied Tiles: {startState.GridState.OccupyingUnitId.Count}", LogCategory.AI, this);

                    float startTime = Time.realtimeSinceStartup;
                    if (startState == null || goals == null || goals.Count == 0 || availableActions == null)
                    {
                        SmartLogger.LogWarning("[AIPlanner.FindPlan] Invalid input: startState, goals, or availableActions is null or empty.", LogCategory.AI, this);
                        SmartLogger.Log($"[AI] [AIPlanner.FindPlan] No plan found for Goal: {goals?.FirstOrDefault()?.GoalName ?? "<none>"} (invalid input)", LogCategory.AI, this);
                        SmartLogger.Log($"[AIPlanner.FindPlan] ========== PLANNING FAILED ==========", LogCategory.AI, this);
                        return null;
                    }

                    AIUnitState agentUnitState = startState.UnitStates.FirstOrDefault(u => u.UnitId == agentUnitId);
                    if (agentUnitState == null)
                    {
                        SmartLogger.LogError($"[AIPlanner.FindPlan] Agent unit state not found for ID {agentUnitId}.", LogCategory.AI, this);
                        SmartLogger.Log($"[AI] [AIPlanner.FindPlan] No plan found for Goal: {goals?.FirstOrDefault()?.GoalName ?? "<none>"} (agent state not found)", LogCategory.AI, this);
                        SmartLogger.Log($"[AIPlanner.FindPlan] ========== PLANNING FAILED ==========", LogCategory.AI, this);
                        return null;
                    }

                    SmartLogger.Log($"[AIPlanner.FindPlan] Available Actions:", LogCategory.AI, this);
                    foreach (var action in availableActions)
                    {
                        SmartLogger.Log($"- Action: {action.ActionName}", LogCategory.AI, this);
                        SmartLogger.Log($"  Cost: {action.Cost}", LogCategory.AI, this);
                        if (action is UseAbilityAction abilityAction && abilityAction.AbilityToUse != null)
                        {
                            SmartLogger.Log($"  Ability: {abilityAction.AbilityToUse.displayName}", LogCategory.AI, this);
                            SmartLogger.Log($"    Damage: {abilityAction.AbilityToUse.damageAmount}", LogCategory.AI, this);
                            SmartLogger.Log($"    Range: {abilityAction.AbilityToUse.range}", LogCategory.AI, this);
                            SmartLogger.Log($"    Aura Cost: {abilityAction.AbilityToUse.auraCost}", LogCategory.AI, this);
                            SmartLogger.Log($"    Targets Allies: {abilityAction.AbilityToUse.targetsAlly}", LogCategory.AI, this);
                            SmartLogger.Log($"    Targets Enemies: {abilityAction.AbilityToUse.targetsEnemy}", LogCategory.AI, this);
                            SmartLogger.Log($"    Targets Ground: {abilityAction.AbilityToUse.targetsGround}", LogCategory.AI, this);
                        }
                        else if (action is MoveAction moveAction)
                        {
                            SmartLogger.Log($"  Target Position: {moveAction.TargetPosition}", LogCategory.AI, this);
                        }
                    }

                    int nodesExplored = 0;
                    int maxDepthReached = 0;

                    SmartLogger.Log("[AI] [AIPlanner.FindPlan] Entering main search loop.", LogCategory.AI, this);
                    while (openSet.Count > 0)
                    {
                        SmartLogger.Log($"[AI] [AIPlanner.FindPlan] Unit {agentUnitId}: A* Loop Iteration. OpenSet size: {openSet.Count}. Current Goal: {selectedGoal.GoalName}", LogCategory.AI, this);
                        nodesExplored++;
                        if (nodesExplored > maxNodesToExplore)
                        {
                            SmartLogger.LogWarning($"[AIPlanner.FindPlan] Search limit reached: Explored {maxNodesToExplore} nodes. Terminating search.", LogCategory.AI, this);
                            SmartLogger.Log($"[AIPlanner.FindPlan] ========== PLANNING FAILED ==========", LogCategory.AI, this);
                            return null;
                        }
                        if (Time.realtimeSinceStartup - startTime > maxPlanningTime)
                        {
                            SmartLogger.LogWarning($"[AIPlanner.FindPlan] Time limit reached: Planning took longer than {maxPlanningTime:F2} seconds. Terminating search.", LogCategory.AI, this);
                            SmartLogger.Log($"[AIPlanner.FindPlan] ========== PLANNING FAILED ==========", LogCategory.AI, this);
                            return null;
                        }

                        SmartLogger.Log($"[AIPlanner.FindPlan] Step 5.1 - Before Dequeue. OpenSet count: {openSet.Count}", LogCategory.AI, this);
                        var currentNode = openSet.Dequeue();
                        SmartLogger.Log("[AIPlanner.FindPlan] Step 5.2 - After Dequeue. currentNode obtained.", LogCategory.AI, this);
                        SmartLogger.Log($"[AIPlanner.FindPlan] Step 5.3 - Dequeued Node details: Action = {currentNode?.action?.ActionName ?? "NULL"}, Depth = {currentNode?.depth ?? -1}, CostF = {currentNode?.costF ?? -1.0f}", LogCategory.AI, this);
                        SmartLogger.Log($"[AIPlanner.FindPlan] Step 5.4 - Dequeued Node State hash: {currentNode?.state?.GetHashCode() ?? 0}", LogCategory.AI, this);
                        SmartLogger.Log("[AIPlanner.FindPlan] Step 5.5 - Before Goal Achieved check.", LogCategory.AI, this);
                        SmartLogger.Log($"[AIPlanner.FindPlan] Step 5.6 - Checking selectedGoal validity. Is null: {selectedGoal == null}. Goal Name: {selectedGoal?.GoalName ?? "NULL"}", LogCategory.AI, this);

                        maxDepthReached = Mathf.Max(maxDepthReached, currentNode.depth);

                        if (debugPlannerSearch)
                        {
                            SmartLogger.Log($"[AIPlanner.FindPlan] Exploring Node:", LogCategory.AI, this);
                            SmartLogger.Log($"- Depth: {currentNode.depth}", LogCategory.AI, this);
                            SmartLogger.Log($"- Action: {currentNode.action?.ActionName ?? "START"}", LogCategory.AI, this);
                            SmartLogger.Log($"- G Cost: {currentNode.costG:F2}", LogCategory.AI, this);
                            SmartLogger.Log($"- H Cost: {currentNode.costH:F2}", LogCategory.AI, this);
                            SmartLogger.Log($"- F Cost: {currentNode.costF:F2}", LogCategory.AI, this);
                        }

                        // Check if goal is achieved in current state
                        SmartLogger.Log($"[AI] [AIPlanner.FindPlan] Unit {agentUnitId}: Checking if Goal is Achieved.", LogCategory.AI, this);
                        bool goalAchieved = selectedGoal.IsGoalAchieved(currentNode.state, agentUnitId);
                        SmartLogger.Log($"[AI] [AIPlanner.FindPlan] Unit {agentUnitId}: Goal Achieved result: {goalAchieved}", LogCategory.AI, this);
                        if (goalAchieved)
                        {
                            SmartLogger.Log("[AIPlanner.FindPlan] Step 5.7 - Goal is achieved.", LogCategory.AI, this);
                            SmartLogger.Log($"[AIPlanner.FindPlan] Goal Achieved!", LogCategory.AI, this);
                            SmartLogger.Log($"- Nodes Explored: {nodesExplored}", LogCategory.AI, this);
                            SmartLogger.Log($"- Max Depth: {maxDepthReached}", LogCategory.AI, this);
                            SmartLogger.Log($"- Planning Time: {Time.realtimeSinceStartup - startTime:F3}s", LogCategory.AI, this);

                            var plan = ReconstructPlan(currentNode);
                            if (plan != null)
                            {
                                SmartLogger.Log($"[AIPlanner.FindPlan] Plan Found:", LogCategory.AI, this);
                                foreach (var action in plan)
                                {
                                    SmartLogger.Log($"- Action: {action.ActionName}", LogCategory.AI, this);
                                    if (action is UseAbilityAction abilityAction)
                                    {
                                        SmartLogger.Log($"  Ability: {abilityAction.AbilityToUse?.displayName}", LogCategory.AI, this);
                                        SmartLogger.Log($"  Target Position: {abilityAction.TargetPosition}", LogCategory.AI, this);
                                        SmartLogger.Log($"  Target Unit ID: {abilityAction.TargetUnitId}", LogCategory.AI, this);
                                        SmartLogger.Log($"  Second Target Unit ID: {abilityAction.SecondTargetUnitId}", LogCategory.AI, this);
                                    }
                                    else if (action is MoveAction moveAction)
                                    {
                                        SmartLogger.Log($"  Target Position: {moveAction.TargetPosition}", LogCategory.AI, this);
                                    }
                                }
                                SmartLogger.Log($"[AI] [AIPlanner.FindPlan] Plan found for Goal: {selectedGoal.GoalName}. Plan Length: {plan.Count}.", LogCategory.AI, this);
                                SmartLogger.Log($"[AIPlanner.FindPlan] ========== PLANNING SUCCEEDED ==========", LogCategory.AI, this);
                                SmartLogger.Log($"[AI] [AIPlanner.FindPlan] Search finished. Plan found: {(plan != null && plan.Count > 0 ? "YES" : "NO")}", LogCategory.AI, this);
                                return plan;
                            }
                        }

                        closedSet.Add(currentNode.state);
                        SmartLogger.Log($"[AI] [AIPlanner.FindPlan] Unit {agentUnitId}: Successfully added current state to closedSet.", LogCategory.AI, this);

                        // Try each available action
                        foreach (var action in availableActions)
                        {
                            if (debugPlannerSearch)
                            {
                                SmartLogger.Log($"[AIPlanner.FindPlan] Considering Action:", LogCategory.AI, this);
                                SmartLogger.Log($"- Name: {action.ActionName}", LogCategory.AI, this);
                                SmartLogger.Log($"- Depth: {currentNode.depth}", LogCategory.AI, this);
                            }

                            // Log the simulated AIWorldState before evaluating the action
                            if (debugPlannerSearch)
                            {
                                var agentStateBefore = currentNode.state.GetUnitState(agentUnitId);
                                SmartLogger.Log($"State Before Action:", LogCategory.AI, this);
                                SmartLogger.Log($"- Agent Position: {agentStateBefore?.Position}", LogCategory.AI, this);
                                SmartLogger.Log($"- Agent HP: {agentStateBefore?.CurrentHP}/{agentStateBefore?.MaxHP}", LogCategory.AI, this);
                                SmartLogger.Log($"- Agent Aura: {agentStateBefore?.CurrentAura}", LogCategory.AI, this);
                            }

                            SmartLogger.Log($"[AI] [AIPlanner.FindPlan] Unit {agentUnitId}: Calling ParameterizeAction for {action.ActionName}", LogCategory.AI, this);
                            if (!ParameterizeAction(action, currentNode.state, selectedGoal, agentUnitId))
                            {
                                if (debugPlannerSearch)
                                {
                                    SmartLogger.Log($"Failed to parameterize action {action.ActionName}", LogCategory.AI, this);
                                }
                                continue;
                            }

                            SmartLogger.Log($"[AI] [AIPlanner.FindPlan] Unit {agentUnitId}: Calling CheckPreconditions for Action: {action.ActionName}", LogCategory.AI, this);
                            bool preconditionsPassed = action.CheckPreconditions(currentNode.state, UnitManager.Instance.GetUnitById(agentUnitId));
                            SmartLogger.Log($"[AI] [AIPlanner.FindPlan] Unit {agentUnitId}: CheckPreconditions returned {preconditionsPassed} for Action: {action.ActionName}", LogCategory.AI, this);
                            if (debugPlannerSearch)
                            {
                                SmartLogger.Log($"Preconditions Check Result: {preconditionsPassed}", LogCategory.AI, this);
                            }
                            if (!preconditionsPassed)
                            {
                                if (debugPlannerSearch)
                                {
                                    SmartLogger.Log($"Preconditions Failed:", LogCategory.AI, this);
                                    if (action is UseAbilityAction abilityActionFail)
                                    {
                                        SmartLogger.Log($"- Ability: {abilityActionFail.AbilityToUse?.displayName ?? "NULL"}", LogCategory.AI, this);
                                        SmartLogger.Log($"- Target Unit ID: {abilityActionFail.TargetUnitId}", LogCategory.AI, this);
                                        SmartLogger.Log($"- Second Target Unit ID: {abilityActionFail.SecondTargetUnitId}", LogCategory.AI, this);
                                        SmartLogger.Log($"- Target Position: {abilityActionFail.TargetPosition}", LogCategory.AI, this);
                                    }
                                    else if (action is MoveAction moveActionFail)
                                    {
                                        SmartLogger.Log($"- Target Position: {moveActionFail.TargetPosition}", LogCategory.AI, this);
                                    }
                                }
                                continue;
                            }

                            SmartLogger.Log($"[AI] [AIPlanner.FindPlan] Unit {agentUnitId}: Cloning state before ApplyEffects for Action: {action.ActionName}", LogCategory.AI, this);
                            var newState = action.ApplyEffects(currentNode.state.Clone(), UnitManager.Instance.GetUnitById(agentUnitId));
                            SmartLogger.Log($"[AI] [AIPlanner.FindPlan] Unit {agentUnitId}: ApplyEffects complete for Action: {action.ActionName}", LogCategory.AI, this);
                            if (debugPlannerSearch)
                            {
                                var agentStateAfter = newState.GetUnitState(agentUnitId);
                                SmartLogger.Log($"State After Action:", LogCategory.AI, this);
                                SmartLogger.Log($"- Agent Position: {agentStateAfter?.Position}", LogCategory.AI, this);
                                SmartLogger.Log($"- Agent HP: {agentStateAfter?.CurrentHP}/{agentStateAfter?.MaxHP}", LogCategory.AI, this);
                                SmartLogger.Log($"- Agent Aura: {agentStateAfter?.CurrentAura}", LogCategory.AI, this);
                            }

                            if (closedSet.Contains(newState))
                            {
                                if (debugPlannerSearch)
                                {
                                    SmartLogger.Log($"State already explored. Skipping.", LogCategory.AI, this);
                                }
                                continue;
                            }

                            SmartLogger.Log($"[AI] [AIPlanner.FindPlan] Unit {agentUnitId}: Calculating heuristic cost for new state.", LogCategory.AI, this);
                            float newG = currentNode.costG + action.Cost;
                            float newH = CalculateHeuristic(newState, selectedGoal);
                            SmartLogger.Log($"[AI] [AIPlanner.FindPlan] Unit {agentUnitId}: Heuristic cost calculated: G={newG}, H={newH}, F={newG + newH}", LogCategory.AI, this);
                            float newF = newG + newH;

                            if (debugPlannerSearch)
                            {
                                SmartLogger.Log($"Action Costs:", LogCategory.AI, this);
                                SmartLogger.Log($"- New G: {newG:F2}", LogCategory.AI, this);
                                SmartLogger.Log($"- New H: {newH:F2}", LogCategory.AI, this);
                                SmartLogger.Log($"- New F: {newF:F2}", LogCategory.AI, this);
                            }

                            // Create new node
                            var newNode = new Node(newState, currentNode, action, newG, newH, currentNode.depth + 1);

                            // Check if this state is already in open set with a better path
                            SmartLogger.Log("[AIPlanner.FindPlan] Step 3.1 - Before FindNodeByState call on openSet.", LogCategory.AI, this);
                            var existingNode = FindNodeByState(openSet.Items.Select(x => x.item), newState);
                            SmartLogger.Log($"[AIPlanner.FindPlan] Step 3.2 - After FindNodeByState call. existingNode is null: {existingNode == null}", LogCategory.AI, this);
                            if (existingNode != null)
                            {
                                SmartLogger.Log("[AIPlanner.FindPlan] Step 3.3 - Existing node found in open set.", LogCategory.AI, this);
                                if (existingNode.costF <= newF)
                                {
                                    SmartLogger.Log("[AIPlanner.FindPlan] Step 3.4 - Existing path is better or equal. Skipping update.", LogCategory.AI, this);
                                    if (debugPlannerSearch)
                                    {
                                        SmartLogger.Log($"Better path exists (F: {existingNode.costF:F2} <= {newF:F2})", LogCategory.AI, this);
                                    }
                                    continue;
                                }
                                SmartLogger.Log("[AIPlanner.FindPlan] Step 3.5 - New path is better. Updating priority.", LogCategory.AI, this);
                                openSet.UpdatePriority(existingNode, newF);
                                SmartLogger.Log("[AIPlanner.FindPlan] Step 3.6 - After UpdatePriority.", LogCategory.AI, this);
                                cameFrom[newState] = newNode;
                                SmartLogger.Log("[AIPlanner.FindPlan] Step 3.7 - After updating cameFrom.", LogCategory.AI, this);
                            }
                            else
                            {
                                SmartLogger.Log("[AIPlanner.FindPlan] Step 3.8 - State not found in open set. Enqueuing new node.", LogCategory.AI, this);
                                openSet.Enqueue(newNode, newF);
                                SmartLogger.Log("[AIPlanner.FindPlan] Step 3.9 - After Enqueue.", LogCategory.AI, this);
                                cameFrom[newState] = newNode;
                                SmartLogger.Log("[AIPlanner.FindPlan] Step 3.10 - After setting cameFrom for new node.", LogCategory.AI, this);
                                if (debugPlannerSearch)
                                {
                                    SmartLogger.Log($"Added new node to open set with action {action.ActionName}", LogCategory.AI, this);
                                }
                            }
                        }
                        SmartLogger.Log($"[AIPlanner.FindPlan] Step 4.1 - End of while loop iteration. OpenSet count: {openSet.Count}, ClosedSet count: {closedSet.Count}", LogCategory.AI, this);
                    }

                    SmartLogger.LogWarning("[AIPlanner.FindPlan] No plan found.", LogCategory.AI, this);
                    SmartLogger.Log($"- Nodes Explored: {nodesExplored}", LogCategory.AI, this);
                    SmartLogger.Log($"- Max Depth: {maxDepthReached}", LogCategory.AI, this);
                    SmartLogger.Log($"- Planning Time: {Time.realtimeSinceStartup - startTime:F3}s", LogCategory.AI, this);
                    SmartLogger.Log("[AIPlanner.FindPlan] ========== PLANNING FAILED ==========", LogCategory.AI, this);
                    SmartLogger.Log($"[AI] [AIPlanner.FindPlan] No plan found for Goal: {selectedGoal.GoalName}.", LogCategory.AI, this);
                    return null;
                }
                catch (Exception ex)
                {
                    SmartLogger.Log($"[AI] [AIPlanner.FindPlan] --- CAUGHT EXCEPTION ({ex.GetType().Name}) during planning for Goal: {goals?.FirstOrDefault()?.GoalName ?? "<none>"} --- Exception: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.AI, this);
                    return null;
                }
            }
        }

        // Placeholder for singleton instance (basic example)
        private static AIPlanner _instance;
        public static AIPlanner Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<AIPlanner>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("AIPlanner (Placeholder)");
                        _instance = go.AddComponent<AIPlanner>();
                        DontDestroyOnLoad(go); // Optional: Persist across scenes
                    }
                }
                return _instance;
            }
        }

        // --- BEGIN: Nested Node class ---
        private class Node
        {
            public AIWorldState state;
            public Node parent;
            public AIAction action;
            public float costG;
            public float costH;
            public float costF => costG + costH;
            public int depth;
            public Node(AIWorldState state, Node parent, AIAction action, float costG, float costH, int depth = 0)
            {
                this.state = state;
                this.parent = parent;
                this.action = action;
                this.costG = costG;
                this.costH = costH;
                this.depth = depth;
            }
        }
        // --- END: Nested Node class ---

        // --- BEGIN: Nested PriorityQueue<T> class ---
        private class PriorityQueue<T> where T : class
        {
            private List<(T item, float priority)> elements = new List<(T, float)>();
            public int Count => elements.Count;
            public IEnumerable<(T item, float priority)> Items => elements;
            public void Enqueue(T item, float priority)
            {
                elements.Add((item, priority));
                int index = elements.Count - 1;
                while (index > 0)
                {
                    int parentIndex = (index - 1) / 2;
                    if (elements[index].priority < elements[parentIndex].priority)
                    {
                        (elements[index], elements[parentIndex]) = (elements[parentIndex], elements[index]);
                        index = parentIndex;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            public T Dequeue()
            {
                SmartLogger.Log("[PriorityQueue.Dequeue] DQ1 - Entering Dequeue. Elements count: " + elements.Count, LogCategory.AI);
                try
                {
                    if (elements.Count == 0) throw new InvalidOperationException("Priority queue is empty.");
                    SmartLogger.Log("[PriorityQueue.Dequeue] DQ2 - Before accessing elements[0] (root item).", LogCategory.AI);
                    T item = elements[0].item;
                    int lastIndex = elements.Count - 1;
                    SmartLogger.Log($"[PriorityQueue.Dequeue] DQ3 - Before moving last element to root. lastIndex: {lastIndex}", LogCategory.AI);
                    elements[0] = elements[lastIndex];
                    elements.RemoveAt(lastIndex);
                    int index = 0;
                    SmartLogger.Log("[PriorityQueue.Dequeue] DQ4 - Before heap restructuring.", LogCategory.AI);
                    while (true)
                    {
                        int leftChildIndex = 2 * index + 1;
                        int rightChildIndex = 2 * index + 2;
                        int smallestIndex = index;
                        if (leftChildIndex < elements.Count && elements[leftChildIndex].priority < elements[smallestIndex].priority)
                        {
                            smallestIndex = leftChildIndex;
                        }
                        if (rightChildIndex < elements.Count && elements[rightChildIndex].priority < elements[smallestIndex].priority)
                        {
                            smallestIndex = rightChildIndex;
                        }
                        if (smallestIndex != index)
                        {
                            (elements[index], elements[smallestIndex]) = (elements[smallestIndex], elements[index]);
                            index = smallestIndex;
                        }
                        else
                        {
                            break;
                        }
                    }
                    SmartLogger.Log("[PriorityQueue.Dequeue] DQ5 - After heap restructuring. Returning item.", LogCategory.AI);
                    return item;
                }
                catch (Exception ex)
                {
                    SmartLogger.LogError($"[PriorityQueue.Dequeue] --- CAUGHT EXCEPTION --- Exception: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.AI);
                    throw;
                }
            }
            public void UpdatePriority(T item, float newPriority)
            {
                int index = -1;
                for (int i = 0; i < elements.Count; i++)
                {
                    if (elements[i].item.Equals(item))
                    {
                        index = i;
                        break;
                    }
                }
                if (index == -1)
                {
                    SmartLogger.LogWarning($"[PriorityQueue.UpdatePriority] Item not found in queue: {item}", LogCategory.AI);
                    return;
                }
                elements[index] = (item, newPriority);
                int currentIndex = index;
                while (currentIndex > 0)
                {
                    int parentIndex = (currentIndex - 1) / 2;
                    if (elements[currentIndex].priority < elements[parentIndex].priority)
                    {
                        (elements[currentIndex], elements[parentIndex]) = (elements[parentIndex], elements[currentIndex]);
                        currentIndex = parentIndex;
                    }
                    else
                    {
                        break;
                    }
                }
                if (currentIndex == index)
                {
                    while (true)
                    {
                        int leftChildIndex = 2 * currentIndex + 1;
                        int rightChildIndex = 2 * currentIndex + 2;
                        int smallestIndex = currentIndex;
                        if (leftChildIndex < elements.Count && elements[leftChildIndex].priority < elements[smallestIndex].priority)
                        {
                            smallestIndex = leftChildIndex;
                        }
                        if (rightChildIndex < elements.Count && elements[rightChildIndex].priority < elements[smallestIndex].priority)
                        {
                            smallestIndex = rightChildIndex;
                        }
                        if (smallestIndex != currentIndex)
                        {
                            (elements[currentIndex], elements[smallestIndex]) = (elements[smallestIndex], elements[currentIndex]);
                            currentIndex = smallestIndex;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            public bool Contains(T item)
            {
                return elements.Any(e => e.item.Equals(item));
            }
        }
        // --- END: Nested PriorityQueue<T> class ---

        // --- BEGIN: CalculateHeuristic method ---
        private float CalculateHeuristic(AIWorldState currentState, AIGoal goal)
        {
            using (new PerformanceScope($"AIPlanner CalculateHeuristic for {goal?.GoalName ?? "NULL"}"))
            {
                if (goal == null) return 0f;
                AIUnitState agentState = currentState.UnitStates.FirstOrDefault(u => u.UnitId == goal.TargetUnitId);
                if (agentState == null)
                {
                    return float.PositiveInfinity;
                }
                // --- REFINED HEURISTICS ---
                if (goal is ReachPositionGoal reachGoal)
                {
                    // Manhattan distance is still admissible for movement
                    return GridPosition.GetManhattanDistance(agentState.Position, reachGoal.DesiredPosition);
                }
                if (goal is DefeatPlayerUnitGoal || goal is AttackNearestEnemyGoal)
                {
                    // Combine distance to lowest-HP enemy and their HP
                    int minDistance = int.MaxValue;
                    int minHP = int.MaxValue;
                    foreach (var unitState in currentState.UnitStates)
                    {
                        if (unitState.IsAlive && unitState.TeamId != agentState.TeamId)
                        {
                            int distance = GridPosition.GetManhattanDistance(agentState.Position, unitState.Position);
                            minDistance = Mathf.Min(minDistance, distance);
                            minHP = Mathf.Min(minHP, unitState.CurrentHP);
                        }
                    }
                    // Lower HP and closer = better (closer to goal)
                    // Heuristic: weighted sum (admissible if weights <= 1)
                    return minDistance + (minHP * 0.5f);
                }
                if (goal is HealSelfGoal)
                {
                    // Missing HP for self
                    return agentState.MaxHP - agentState.CurrentHP;
                }
                if (goal is HealAllyGoal)
                {
                    // Total missing HP for all allies
                    float totalMissing = 0f;
                    foreach (var unitState in currentState.UnitStates)
                    {
                        if (unitState.IsAlive && unitState.TeamId == agentState.TeamId)
                        {
                            totalMissing += (unitState.MaxHP - unitState.CurrentHP);
                        }
                    }
                    return totalMissing;
                }
                if (goal is ApplyBuffToSelfGoal applyBuffGoal)
                {
                    // 0 if buff present, else 10 (arbitrary, but admissible if cost to apply buff >= 10)
                    bool hasBuff = agentState.ActiveStatusEffects.Contains(applyBuffGoal.BuffEffectId);
                    return hasBuff ? 0f : 10f;
                }
                if (goal is UseAbilityGoal useAbilityGoal && useAbilityGoal.DesiredAbility != null)
                {
                    // For generic ability use, combine distance to best target and their HP (if attack), or missing HP (if heal)
                    if (useAbilityGoal.DesiredAbility.targetsEnemy)
                    {
                        int minDistance = int.MaxValue;
                        int minHP = int.MaxValue;
                        foreach (var unitState in currentState.UnitStates)
                        {
                            if (unitState.IsAlive && unitState.TeamId != agentState.TeamId)
                            {
                                int distance = GridPosition.GetManhattanDistance(agentState.Position, unitState.Position);
                                minDistance = Mathf.Min(minDistance, distance);
                                minHP = Mathf.Min(minHP, unitState.CurrentHP);
                            }
                        }
                        return minDistance + (minHP * 0.5f);
                    }
                    if (useAbilityGoal.DesiredAbility.healAmount > 0)
                    {
                        float totalMissing = 0f;
                        foreach (var unitState in currentState.UnitStates)
                        {
                            if (unitState.IsAlive && unitState.TeamId == agentState.TeamId)
                            {
                                totalMissing += (unitState.MaxHP - unitState.CurrentHP);
                            }
                        }
                        return totalMissing;
                    }
                    // For buffs/debuffs, count number of units missing the effect
                    if (useAbilityGoal.DesiredAbility.appliedEffects != null && useAbilityGoal.DesiredAbility.appliedEffects.Count > 0)
                    {
                        string effectId = useAbilityGoal.DesiredAbility.appliedEffects[0].effectId;
                        int missingCount = 0;
                        foreach (var unitState in currentState.UnitStates)
                        {
                            if (unitState.IsAlive && !unitState.ActiveStatusEffects.Contains(effectId))
                                missingCount++;
                        }
                        return missingCount;
                    }
                    // Default: distance to nearest targetable tile
                    return 1f;
                }
                return 0f;
            }
        }
        // --- END: CalculateHeuristic method ---

        // --- BEGIN: ReconstructPlan method ---
        private List<AIAction> ReconstructPlan(Node goalNode)
        {
            SmartLogger.Log("[AI] [AIPlanner.ReconstructPlan] Reconstructing plan...", LogCategory.AI, this);
            var plan = new List<AIAction>();
            var current = goalNode;
            while (current != null && current.action != null)
            {
                plan.Add(current.action);
                current = current.parent;
            }
            plan.Reverse();
            SmartLogger.Log($"[AI] [AIPlanner.ReconstructPlan] Plan built with {plan.Count} actions: {string.Join(", ", plan.Select(a => a.ActionName))}", LogCategory.AI, this);
            return plan;
        }
        // --- END: ReconstructPlan method ---

        // Helper to find a Node by state in a collection
        private Node FindNodeByState(IEnumerable<Node> nodes, AIWorldState state)
        {
            SmartLogger.Log("[AIPlanner.FindNodeByState] Micro-log F1.1 - Entering method, about to search for node by state.", LogCategory.AI, this);
            Node existingNode = null;
            try
            {
                SmartLogger.Log("[AIPlanner.FindNodeByState] Micro-log F1.2 - Before FirstOrDefault.", LogCategory.AI, this);
                existingNode = nodes.FirstOrDefault(n => {
                    SmartLogger.Log("[AIPlanner.FindNodeByState] Micro-log F1.3 - Inside predicate, before state.Equals.", LogCategory.AI, this);
                    bool result = n.state.Equals(state);
                    SmartLogger.Log($"[AIPlanner.FindNodeByState] Micro-log F1.4 - After state.Equals. Result: {result}", LogCategory.AI, this);
                    return result;
                });
                SmartLogger.Log($"[AIPlanner.FindNodeByState] Micro-log F1.5 - After FirstOrDefault. existingNode is null: {existingNode == null}", LogCategory.AI, this);
            }
            catch (Exception ex)
            {
                SmartLogger.LogError($"[AIPlanner.FindNodeByState] --- CAUGHT EXCEPTION --- Exception: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.AI, this);
            }
            return existingNode;
        }

        /// <summary>
        /// Parameterizes an action with valid targets based on the current world state and goal.
        /// </summary>
        private bool ParameterizeAction(AIAction action, AIWorldState currentState, AIGoal goal, int agentUnitId)
        {
            SmartLogger.Log($"[AIPlanner.ParameterizeAction] Parameterizing action: {action.ActionName}", LogCategory.AI, this);
            
            var agentState = currentState.GetUnitState(agentUnitId);
            if (agentState == null)
            {
                SmartLogger.LogWarning($"[AIPlanner.ParameterizeAction] Agent state not found for unit {agentUnitId}", LogCategory.AI, this);
                return false;
            }

            if (action is MoveAction moveAction)
            {
                return ParameterizeMoveAction(moveAction, currentState, goal, agentState);
            }
            else if (action is UseAbilityAction abilityAction)
            {
                return ParameterizeAbilityAction(abilityAction, currentState, goal, agentState);
            }

            return true; // Actions that don't need parameterization
        }

        private bool ParameterizeMoveAction(MoveAction moveAction, AIWorldState currentState, AIGoal goal, AIUnitState agentState)
        {
            SmartLogger.Log($"[AIPlanner.ParameterizeMoveAction] Finding valid target position for move action", LogCategory.AI, this);
            
            // If goal is a position goal, try to find a valid position near the goal
            if (goal is ReachPositionGoal reachGoal)
            {
                var desiredPos = reachGoal.DesiredPosition;
                var validPos = FindClosestValidAlternative(
                    currentState,
                    agentState.Position,
                    agentState.MovementRange,
                    desiredPos,
                    agentState.UnitId,
                    new Dictionary<int, GridPosition>() // No planned moves yet
                );

                if (validPos.HasValue)
                {
                    moveAction.TargetPosition = validPos.Value;
                    SmartLogger.Log($"[AIPlanner.ParameterizeMoveAction] Found valid target position: {validPos.Value}", LogCategory.AI, this);
                    return true;
                }
            }
            // For other goals, find the best position to achieve the goal
            else
            {
                // Find the best position based on the goal type
                GridPosition? bestPos = null;
                float bestScore = float.MinValue;

                // Get all walkable positions within range
                var visited = new HashSet<GridPosition>();
                var queue = new Queue<(GridPosition pos, int steps)>();
                queue.Enqueue((agentState.Position, 0));
                visited.Add(agentState.Position);

                while (queue.Count > 0)
                {
                    var (current, steps) = queue.Dequeue();
                    if (steps >= agentState.MovementRange) continue;

                    // Calculate position score based on goal
                    float score = EvaluatePositionScore(current, currentState, goal, agentState);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPos = current;
                    }

                    // Add neighbors
                    foreach (var neighbor in GetOrthogonalNeighbors(current))
                    {
                        if (!visited.Contains(neighbor) && 
                            currentState.GridState.IsWalkable.TryGetValue(neighbor, out bool walkable) && 
                            walkable && 
                            !currentState.IsTileOccupied(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue((neighbor, steps + 1));
                        }
                    }
                }

                if (bestPos.HasValue)
                {
                    moveAction.TargetPosition = bestPos.Value;
                    SmartLogger.Log($"[AIPlanner.ParameterizeMoveAction] Found best position for goal: {bestPos.Value} (Score: {bestScore})", LogCategory.AI, this);
                    return true;
                }
            }

            SmartLogger.LogWarning($"[AIPlanner.ParameterizeMoveAction] Failed to find valid target position", LogCategory.AI, this);
            return false;
        }

        private bool ParameterizeAbilityAction(UseAbilityAction abilityAction, AIWorldState currentState, AIGoal goal, AIUnitState agentState)
        {
            SmartLogger.Log($"[AIPlanner.ParameterizeAbilityAction] Finding valid targets for ability: {abilityAction.AbilityToUse?.displayName}", LogCategory.AI, this);

            if (abilityAction.AbilityToUse == null)
            {
                SmartLogger.LogWarning($"[AIPlanner.ParameterizeAbilityAction] No ability assigned", LogCategory.AI, this);
                return false;
            }

            // Special case for Karmic Tether
            if (abilityAction.AbilityToUse.abilityId == "KarmicTether")
            {
                return ParameterizeKarmicTether(abilityAction, currentState, goal, agentState);
            }

            // Find valid targets based on ability type
            if (abilityAction.AbilityToUse.targetsEnemy || abilityAction.AbilityToUse.targetsAlly)
            {
                // Find best unit target
                int? bestTargetId = null;
                float bestScore = float.MinValue;

                foreach (var unitState in currentState.UnitStates)
                {
                    if (!unitState.IsAlive) continue;

                    bool isTargetAlly = unitState.TeamId == agentState.TeamId;
                    if ((isTargetAlly && !abilityAction.AbilityToUse.targetsAlly) ||
                        (!isTargetAlly && !abilityAction.AbilityToUse.targetsEnemy))
                        continue;

                    // Check range
                    int distance = GridPosition.GetManhattanDistance(agentState.Position, unitState.Position);
                    int effectiveRange = AbilityManager.Instance != null ? 
                        AbilityManager.Instance.GetEffectiveRange(abilityAction.AbilityToUse, UnitManager.Instance.GetUnitById(agentState.UnitId)) : 
                        abilityAction.AbilityToUse.range;

                    if (distance > effectiveRange) continue;

                    // Log arguments before scoring
                    SmartLogger.Log($"[AIPlanner.ParameterizeAbilityAction] Scoring target: targetState.UnitId={unitState.UnitId}, goalType={goal?.GetType().Name}, goal.TargetUnitId={goal?.TargetUnitId}, abilityId={abilityAction.AbilityToUse.abilityId}", LogCategory.AI, this);

                    // Calculate target score based on ability type and goal
                    float score = EvaluateTargetScore(unitState, currentState, goal, abilityAction.AbilityToUse);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestTargetId = unitState.UnitId;
                    }
                }

                if (bestTargetId.HasValue)
                {
                    abilityAction.TargetUnitId = bestTargetId.Value;
                    abilityAction.TargetPosition = currentState.GetUnitState(bestTargetId.Value).Position;
                    SmartLogger.Log($"[AIPlanner.ParameterizeAbilityAction] Found best unit target: {bestTargetId.Value} (Score: {bestScore})", LogCategory.AI, this);
                    return true;
                }
            }
            else if (abilityAction.AbilityToUse.targetsGround)
            {
                // Find best ground target position
                GridPosition? bestPos = null;
                float bestScore = float.MinValue;

                // Get all positions within range
                var visited = new HashSet<GridPosition>();
                var queue = new Queue<(GridPosition pos, int steps)>();
                queue.Enqueue((agentState.Position, 0));
                visited.Add(agentState.Position);

                while (queue.Count > 0)
                {
                    var (current, steps) = queue.Dequeue();
                    if (steps >= abilityAction.AbilityToUse.range) continue;

                    // Log arguments before scoring
                    SmartLogger.Log($"[AIPlanner.ParameterizeAbilityAction] Scoring ground target: pos={current}, goalType={goal?.GetType().Name}, goal.TargetUnitId={goal?.TargetUnitId}, abilityId={abilityAction.AbilityToUse.abilityId}", LogCategory.AI, this);

                    // Calculate position score based on ability and goal
                    float score = EvaluateGroundTargetScore(current, currentState, goal, abilityAction.AbilityToUse, agentState.UnitId);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPos = current;
                    }

                    // Add neighbors
                    foreach (var neighbor in GetOrthogonalNeighbors(current))
                    {
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue((neighbor, steps + 1));
                        }
                    }
                }

                if (bestPos.HasValue)
                {
                    abilityAction.TargetPosition = bestPos.Value;
                    SmartLogger.Log($"[AIPlanner.ParameterizeAbilityAction] Found best ground target: {bestPos.Value} (Score: {bestScore})", LogCategory.AI, this);
                    return true;
                }
            }

            SmartLogger.LogWarning($"[AIPlanner.ParameterizeAbilityAction] Failed to find valid targets", LogCategory.AI, this);
            return false;
        }

        private bool ParameterizeKarmicTether(UseAbilityAction abilityAction, AIWorldState currentState, AIGoal goal, AIUnitState agentState)
        {
            SmartLogger.Log($"[AIPlanner.ParameterizeKarmicTether] Finding targets for Karmic Tether", LogCategory.AI, this);

            // Find best primary target (enemy)
            int? bestPrimaryTargetId = null;
            float bestPrimaryScore = float.MinValue;

            // Find best secondary target (ally)
            int? bestSecondaryTargetId = null;
            float bestSecondaryScore = float.MinValue;

            foreach (var unitState in currentState.UnitStates)
            {
                if (!unitState.IsAlive) continue;

                // Check range
                int distance = GridPosition.GetManhattanDistance(agentState.Position, unitState.Position);
                int effectiveRange = AbilityManager.Instance != null ? 
                    AbilityManager.Instance.GetEffectiveRange(abilityAction.AbilityToUse, UnitManager.Instance.GetUnitById(agentState.UnitId)) : 
                    abilityAction.AbilityToUse.range;

                if (distance > effectiveRange) continue;

                bool isTargetAlly = unitState.TeamId == agentState.TeamId;
                float score = EvaluateTargetScore(unitState, currentState, goal, abilityAction.AbilityToUse);

                if (isTargetAlly)
                {
                    if (score > bestSecondaryScore)
                    {
                        bestSecondaryScore = score;
                        bestSecondaryTargetId = unitState.UnitId;
                    }
                }
                else
                {
                    if (score > bestPrimaryScore)
                    {
                        bestPrimaryScore = score;
                        bestPrimaryTargetId = unitState.UnitId;
                    }
                }
            }

            if (bestPrimaryTargetId.HasValue && bestSecondaryTargetId.HasValue)
            {
                abilityAction.TargetUnitId = bestPrimaryTargetId.Value;
                abilityAction.SecondTargetUnitId = bestSecondaryTargetId.Value;
                abilityAction.TargetPosition = currentState.GetUnitState(bestPrimaryTargetId.Value).Position;
                SmartLogger.Log($"[AIPlanner.ParameterizeKarmicTether] Found targets - Primary: {bestPrimaryTargetId.Value}, Secondary: {bestSecondaryTargetId.Value}", LogCategory.AI, this);
                return true;
            }

            SmartLogger.LogWarning($"[AIPlanner.ParameterizeKarmicTether] Failed to find valid targets for Karmic Tether", LogCategory.AI, this);
            return false;
        }

        private float EvaluatePositionScore(GridPosition pos, AIWorldState state, AIGoal goal, AIUnitState agentState)
        {
            float score = 0f;

            // Base score on distance to goal position if applicable
            if (goal is ReachPositionGoal reachGoal)
            {
                int distToGoal = GridPosition.GetManhattanDistance(pos, reachGoal.DesiredPosition);
                score -= distToGoal; // Closer is better
            }

            // Consider strategic positioning
            foreach (var unit in state.UnitStates)
            {
                if (!unit.IsAlive) continue;

                int distToUnit = GridPosition.GetManhattanDistance(pos, unit.Position);
                if (unit.TeamId != agentState.TeamId)
                {
                    // Prefer positions closer to enemies for attack goals
                    if (goal is AttackNearestEnemyGoal || goal is DefeatPlayerUnitGoal)
                    {
                        score += 10f / (distToUnit + 1); // Avoid division by zero
                    }
                    // Prefer positions further from enemies for defensive goals
                    else
                    {
                        score += distToUnit;
                    }
                }
                else
                {
                    // Prefer positions near allies for support goals
                    if (goal is HealAllyGoal)
                    {
                        score += 5f / (distToUnit + 1);
                    }
                }
            }

            return score;
        }

        private float EvaluateTargetScore(AIUnitState targetState, AIWorldState currentState, AIGoal goal, AbilityData ability)
        {
            // Null checks for all arguments
            if (targetState == null)
            {
                SmartLogger.LogWarning("[AIPlanner.EvaluateTargetScore] targetState is null!", LogCategory.AI, this);
                return float.MinValue;
            }
            if (currentState == null)
            {
                SmartLogger.LogWarning("[AIPlanner.EvaluateTargetScore] currentState is null!", LogCategory.AI, this);
                return float.MinValue;
            }
            if (goal == null)
            {
                SmartLogger.LogWarning("[AIPlanner.EvaluateTargetScore] goal is null!", LogCategory.AI, this);
                return float.MinValue;
            }
            if (ability == null)
            {
                SmartLogger.LogWarning("[AIPlanner.EvaluateTargetScore] ability is null!", LogCategory.AI, this);
                return float.MinValue;
            }

            float score = 0f;

            // Base score on HP (lower HP = better target for damage, higher HP = better for healing)
            if (ability.damageAmount > 0)
            {
                // For damage abilities, prefer lower HP targets
                score += (targetState.MaxHP - targetState.CurrentHP) * 2f;
            }
            else if (ability.healAmount > 0)
            {
                // For healing abilities, prefer lower HP allies
                score += (targetState.MaxHP - targetState.CurrentHP) * 2f;
            }

            // Consider goal-specific factors
            if (goal is AttackNearestEnemyGoal)
            {
                // For attack goals, heavily weight damage potential
                if (ability.damageAmount > 0)
                {
                    score += ability.damageAmount * 3f;
                }
            }
            else if (goal is HealSelfGoal)
            {
                // For healing goals, prefer self-targeting
                if (targetState.UnitId == goal.TargetUnitId)
                {
                    score += 1000f;
                }
            }
            else if (goal is DefeatPlayerUnitGoal)
            {
                // For defeat goals, prefer targets that are closer to defeat
                if (ability.damageAmount > 0)
                {
                    float hpPercentage = (float)targetState.CurrentHP / targetState.MaxHP;
                    score += (1f - hpPercentage) * 1000f;
                }
            }

            // Consider status effects
            if (ability.appliedEffects != null && ability.appliedEffects.Count > 0)
            {
                foreach (var effect in ability.appliedEffects)
                {
                    // If target doesn't have the effect, increase score
                    if (!targetState.ActiveStatusEffects.Contains(effect.effectId))
                    {
                        score += 500f;
                    }
                }
            }

            // Consider distance (closer targets are slightly preferred)
            AIUnitState goalTargetState = null;
            if (goal.TargetUnitId != -1)
            {
                goalTargetState = currentState.GetUnitState(goal.TargetUnitId);
                if (goalTargetState == null)
                {
                    SmartLogger.LogWarning($"[AIPlanner.EvaluateTargetScore] Goal target unit (ID: {goal.TargetUnitId}) not found in simulated state!", LogCategory.AI, this);
                    return score - 10000f; // Penalize this target
                }
            }
            if (goalTargetState != null)
            {
                // --- ADDED LOG FOR DEBUGGING NRE AT 1131 ---
                SmartLogger.Log($"[AI] DEBUG NRE 1108: targetState={(targetState != null ? targetState.UnitId.ToString() : "null")} at {targetState?.Position}, goalTargetState={(goalTargetState != null ? goalTargetState.UnitId.ToString() : "null")} at {goalTargetState?.Position}", LogCategory.AI);
                // --- END DEBUG LOG ---
                int distance = GridPosition.GetManhattanDistance(goalTargetState.Position, targetState.Position);
                score -= distance * 0.5f;
            }

            return score;
        }

        private float EvaluateGroundTargetScore(GridPosition pos, AIWorldState currentState, AIGoal goal, AbilityData ability, int agentUnitId)
        {
            // Null checks for all arguments
            if (currentState == null)
            {
                SmartLogger.LogWarning("[AIPlanner.EvaluateGroundTargetScore] currentState is null!", LogCategory.AI, this);
                return float.MinValue;
            }
            if (goal == null)
            {
                SmartLogger.LogWarning("[AIPlanner.EvaluateGroundTargetScore] goal is null!", LogCategory.AI, this);
                return float.MinValue;
            }
            if (ability == null)
            {
                SmartLogger.LogWarning("[AIPlanner.EvaluateGroundTargetScore] ability is null!", LogCategory.AI, this);
                return float.MinValue;
            }

            SmartLogger.Log($"[AIPlanner.EvaluateGroundTargetScore] Scoring pos={pos}, agentUnitId={agentUnitId}, goalType={goal.GetType().Name}, abilityId={ability.abilityId}", LogCategory.AI, this);

            float score = 0f;

            // Count affected units
            int affectedEnemies = 0;
            int affectedAllies = 0;
            int totalAffected = 0;

            // Check all units in range of the target position
            foreach (var unitState in currentState.UnitStates)
            {
                if (unitState == null)
                {
                    SmartLogger.LogWarning("[AIPlanner.EvaluateGroundTargetScore] Encountered null unitState in UnitStates!", LogCategory.AI, this);
                    continue;
                }
                if (!unitState.IsAlive) continue;

                int distance = GridPosition.GetManhattanDistance(pos, unitState.Position);
                if (distance <= ability.range)
                {
                    SmartLogger.Log($"[AIPlanner.EvaluateGroundTargetScore] AoE check: unitId={unitState.UnitId}, pos={unitState.Position}, team={unitState.TeamId}, distance={distance}", LogCategory.AI, this);
                    totalAffected++;
                    if (goal.TargetUnitId != -1)
                    {
                        var goalTargetState = currentState.GetUnitState(goal.TargetUnitId);
                        if (goalTargetState == null)
                        {
                            SmartLogger.LogWarning($"[AIPlanner.EvaluateGroundTargetScore] Goal target unit (ID: {goal.TargetUnitId}) not found in simulated state!", LogCategory.AI, this);
                            // Penalize this target, but still count for stats
                        }
                        if (unitState.TeamId == goalTargetState?.TeamId)
                        {
                            affectedAllies++;
                        }
                        else
                        {
                            affectedEnemies++;
                        }
                    }
                    else
                    {
                        // If no specific goal target, just count as enemy (default logic)
                        affectedEnemies++;
                    }
                }
            }

            // Base score on number of affected units
            score += totalAffected * 100f;

            // Adjust score based on ability type and goal
            if (ability.damageAmount > 0)
            {
                // For damage abilities, prefer positions affecting more enemies
                score += affectedEnemies * 200f;
                score -= affectedAllies * 300f; // Heavily penalize hitting allies
            }
            else if (ability.healAmount > 0)
            {
                // For healing abilities, prefer positions affecting more allies
                score += affectedAllies * 200f;
                score -= affectedEnemies * 100f; // Slightly penalize hitting enemies
            }

            // Consider goal-specific factors
            if (goal is AttackNearestEnemyGoal)
            {
                // For attack goals, heavily weight damage potential
                if (ability.damageAmount > 0)
                {
                    score += ability.damageAmount * affectedEnemies * 3f;
                }
            }
            else if (goal is DefeatPlayerUnitGoal)
            {
                // For defeat goals, prefer positions that can hit multiple enemies
                if (ability.damageAmount > 0)
                {
                    score += affectedEnemies * 500f;
                }
            }

            // Corrected: Only use agentState.Position for distance calculation
            AIUnitState agentState = currentState.GetUnitState(agentUnitId);
            if (agentState == null)
            {
                SmartLogger.LogWarning($"[AIPlanner.EvaluateGroundTargetScore] Agent unit state is null for unit {agentUnitId} in simulated state. Cannot calculate distance from caster.", LogCategory.AI, this);
                return float.MinValue; // Cannot evaluate without agent position
            }
            int distanceFromCaster = GridPosition.GetManhattanDistance(agentState.Position, pos);
            score -= distanceFromCaster * 0.5f;

            SmartLogger.Log($"[AIPlanner.EvaluateGroundTargetScore] Final score for pos={pos}: {score}", LogCategory.AI, this);
            return score;
        }
    }
} 