using UnityEngine;
using System.Collections.Generic;

namespace Dokkaebi.AI.Data
{
    /// <summary>
    /// ScriptableObject representing an AI configuration profile for enemy units.
    /// Defines the set of possible goals and available actions for a specific AI type.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyAIProfile", menuName = "Dokkaebi/AI/Enemy AI Profile")]
    public class EnemyAIProfile : ScriptableObject
    {
        [Tooltip("List of possible goals this AI profile can pursue, ordered by priority (highest first).")]
        public List<AIGoal> PossibleGoals;

        [Tooltip("List of actions available to units with this AI profile.")]
        public List<AIAction> AvailableActions;

        // TODO: Add other AI configuration parameters here, e.g.,
        // - Decision making frequency
        // - Aggression level
        // - Preferred targets
    }
} 