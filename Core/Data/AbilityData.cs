using UnityEngine;
using System.Collections.Generic;
using Dokkaebi.Core.Data;
using Dokkaebi.Interfaces;
using Dokkaebi.Common;

namespace Dokkaebi.Core.Data
{
    /// <summary>
    /// Defines how an ability should move the caster during execution
    /// </summary>
    public enum AbilityMovementType
    {
        None,                       // Default, no special movement
        DashToTargetAdjacent,      // Move instantly to adjacent tile before effect
        TeleportToTarget,          // Teleport directly to target position
        PullTargetTowardsCaster,   // Pull target unit towards the caster
        PushTargetAway             // Push target unit away from the caster
    }

    /// <summary>
    /// Defines the properties and behavior of a unit's ability.
    /// </summary>
    [CreateAssetMenu(fileName = "NewAbilityData", menuName = "Dokkaebi/Data/Ability Data")]
    public class AbilityData : ScriptableObject
    {
        [Header("Basic Information")]
        public string abilityId;
        public string displayName;
        public string description;
        public Sprite icon;
        public AbilityType abilityType = AbilityType.Primary;

        [Header("Costs & Cooldowns")]
        public int auraCost;
        public int cooldownTurns;
        public bool requiresOverload = false;

        [Header("Targeting")]
        public bool targetsSelf = false;
        public bool targetsAlly = false;
        public bool targetsEnemy = false;
        public bool targetsGround = false;
        public int range = 1;
        public int areaOfEffect = 0; // 0 for single target
        public AbilityMovementType movementType = AbilityMovementType.None;
        [Tooltip("Distance in tiles for Pull effects")]
        public int pullDistance = 2; // Default pull distance for abilities like Undertow
        [Tooltip("Distance in tiles for Push effects")]
        public int pushDistance = 2; // Default push distance for abilities like Repel

        [Header("Direct Effects")]
        public int damageAmount;
        public DamageType damageType;
        public int healAmount;
        public List<StatusEffectData> appliedEffects;
        public bool triggersOnBeingAttacked = false;

        [Header("Zone Creation")]
        public bool createsZone = false;
        public ZoneData zoneToCreate;
        // Zone-specific effect parameters
        public int zoneDuration; // Overrides ZoneData.defaultDuration if > 0
        public int zoneDamagePerTurn;
        public DamageType zoneDamageType;
        public int zoneHealingPerTurn;
        public List<StatusEffectData> zoneStatusEffects = new List<StatusEffectData>();
        public StatusEffectData zoneStatusEffect;
        public float zoneMovementCostMultiplier = 1.0f;
        public float zoneAbilityCostMultiplier = 1.0f;

        [Header("Overload Properties")]
        public bool hasOverloadVariant = false;
        public int overloadDamageMultiplier = 2;
        public int overloadHealMultiplier = 2;
        public float overloadEffectDurationMultiplier = 1.5f;
        public float overloadDurationMultiplier = 1.5f;
        public float overloadZoneEffectMultiplier = 1.5f;

        [Header("Theme")]
        public GameObject abilityVFXPrefab;
        public GameObject projectilePrefab;
        public Color abilityColor = Color.white;

        [Header("Audio")]
        public AudioClip castSound;
        public AudioClip hitSound;
        public AudioClip overloadSound;

        protected virtual void OnValidate()
        {
            // Ensure ID is not empty
            if (string.IsNullOrEmpty(abilityId))
            {
                abilityId = name;
            }

            // Validate targeting
            if (!targetsSelf && !targetsAlly && !targetsEnemy && !targetsGround)
            {
                Debug.LogWarning($"Ability {abilityId} has no valid targets!");
            }

            // Validate costs
            if (auraCost < 0)
            {
                Debug.LogWarning($"Ability {abilityId} has negative aura cost!");
            }

            // Validate range
            if (range < 1)
            {
                Debug.LogWarning($"Ability {abilityId} has invalid range!");
            }

            // Validate zone creation
            if (createsZone && zoneToCreate == null)
            {
                Debug.LogWarning($"Ability {abilityId} is set to create a zone but no zone type is specified!");
            }
        }
    }
}
