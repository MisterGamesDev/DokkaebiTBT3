using UnityEngine;
using Dokkaebi.AI.Data;
using Dokkaebi.Utilities;
using Dokkaebi.Interfaces;
using Dokkaebi.Core;

namespace Dokkaebi.AI.Data.Actions
{
    [CreateAssetMenu(fileName = "EndTurnAction", menuName = "Dokkaebi/AI/Actions/End Turn")]
    public class EndTurnAction : AIAction
    {
        private void OnValidate()
        {
            ActionName = "End Turn";
            Cost = 0;
        }

        public override bool CheckPreconditions(AIWorldState currentState, IDokkaebiUnit agentUnit)
        {
            SmartLogger.Log($"[EndTurnAction] CheckPreconditions ENTRY for unit {agentUnit.UnitId}", LogCategory.AI, this);
            if (agentUnit == null || !agentUnit.IsAlive)
            {
                SmartLogger.Log("[EndTurnAction] Agent unit is null or not alive.", LogCategory.AI, this);
                return false;
            }
            // TODO: Add checks for pending movement, phase, and if already ended turn
            SmartLogger.Log("[EndTurnAction] Preconditions met (basic checks only).", LogCategory.AI, this);
            return true;
        }

        public override AIWorldState ApplyEffects(AIWorldState currentState, IDokkaebiUnit agentUnit)
        {
            SmartLogger.Log($"[EndTurnAction] ApplyEffects ENTRY for unit {agentUnit.UnitId}", LogCategory.AI, this);
            // For now, just return a clone. Could set a flag in AIUnitState if needed.
            return currentState.Clone();
        }

        public override void Execute(IDokkaebiUnit agentUnit, AIWorldState currentState)
        {
            SmartLogger.Log($"[EndTurnAction] Execute ENTRY for unit {agentUnit.UnitId}", LogCategory.AI, this);
            PlayerActionManager.Instance.ExecuteEndTurnCommand();
            SmartLogger.Log("[EndTurnAction] ExecuteEndTurnCommand called.", LogCategory.AI, this);
        }
    }
} 