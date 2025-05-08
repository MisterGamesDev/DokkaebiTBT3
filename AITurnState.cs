using Dokkaebi.Common;
using Dokkaebi.Utilities; // Assuming TurnPhase is here

namespace Dokkaebi.AI.Data
{
    /// <summary>
    /// Represents the state of the turn system from the AI's perspective.
    /// </summary>
    public class AITurnState
    {
        public TurnPhase CurrentPhase; // The current phase of the turn
        public int ActivePlayerId; // The ID of the player whose turn it currently is
        // TODO: Add other relevant turn data, like turn number, phase timer, etc.

        /// <summary>
        /// Creates a deep clone of the current AITurnState.
        /// </summary>
        /// <returns>A deep copy of the AITurnState.</returns>
        public AITurnState Clone()
        {
            return new AITurnState
            {
                CurrentPhase = this.CurrentPhase,
                ActivePlayerId = this.ActivePlayerId
                // TODO: Clone other fields if added
            };
        }

        /// <summary>
        /// Determines whether this AITurnState is equal to another object.
        /// </summary>
        public override bool Equals(object obj)
        {
            SmartLogger.Log("[AITurnState.Equals] Entering Equals.", LogCategory.AI);
            SmartLogger.Log("[AITurnState.Equals] Step 1.1 - Before null/type checks.", LogCategory.AI);
            try
            {
                if (ReferenceEquals(this, obj))
                {
                    SmartLogger.Log("[AITurnState.Equals] Step 1.2 - ReferenceEquals true (same object).", LogCategory.AI);
                    return true;
                }
                if (obj == null || GetType() != obj.GetType())
                {
                    SmartLogger.Log("[AITurnState.Equals] Step 1.3 - Null or wrong type.", LogCategory.AI);
                    return false;
                }
                SmartLogger.Log("[AITurnState.Equals] Step 1.4 - After null/type checks.", LogCategory.AI);

                var other = (AITurnState)obj;
                SmartLogger.Log("[AITurnState.Equals] Step 1.5 - After casting to AITurnState.", LogCategory.AI);

                SmartLogger.Log($"[AITurnState.Equals] Step 1.6 - Before CurrentPhase comparison. This: {CurrentPhase}, Other: {other.CurrentPhase}", LogCategory.AI);
                if (!object.Equals(CurrentPhase, other.CurrentPhase))
                {
                    SmartLogger.Log($"[AITurnState.Equals] Step 1.7 - Not equal: CurrentPhase differs. This: {CurrentPhase}, Other: {other.CurrentPhase}", LogCategory.AI);
                    return false;
                }
                SmartLogger.Log("[AITurnState.Equals] Step 1.8 - After CurrentPhase comparison.", LogCategory.AI);

                SmartLogger.Log($"[AITurnState.Equals] Step 1.9 - Before ActivePlayerId comparison. This: {ActivePlayerId}, Other: {other.ActivePlayerId}", LogCategory.AI);
                if (!object.Equals(ActivePlayerId, other.ActivePlayerId))
                {
                    SmartLogger.Log($"[AITurnState.Equals] Step 1.10 - Not equal: ActivePlayerId differs. This: {ActivePlayerId}, Other: {other.ActivePlayerId}", LogCategory.AI);
                    return false;
                }
                SmartLogger.Log("[AITurnState.Equals] Step 1.11 - After ActivePlayerId comparison.", LogCategory.AI);

                SmartLogger.Log("[AITurnState.Equals] Step 1.12 - TurnStates are equal.", LogCategory.AI);
                return true;
            }
            catch (System.Exception ex)
            {
                SmartLogger.LogError($"[AITurnState.Equals] --- CAUGHT EXCEPTION --- Exception: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.AI);
                return false;
            }
        }

        /// <summary>
        /// Returns a hash code for this AITurnState.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + CurrentPhase.GetHashCode();
                hash = hash * 31 + ActivePlayerId.GetHashCode();
                return hash;
            }
        }
    }
} 