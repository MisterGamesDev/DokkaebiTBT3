using UnityEngine;
using Dokkaebi.Core.Data;
using Dokkaebi.Interfaces;
using Dokkaebi.Common;

[CreateAssetMenu(fileName = "WindStepAbilityData", menuName = "Dokkaebi/Data/Ability Data/Wind Step")]
public class WindStepAbilityData : AbilityData
{
    protected override void OnValidate()
    {
        base.OnValidate();

        // Set specific properties for the Wind Step ability
        abilityId = "WindStep";
        displayName = "Wind Step";
        description = "Move 2 tiles instantly, ignoring obstacles.";
        icon = null; // Assign a sprite in the Unity Editor
        abilityType = AbilityType.Special; // Wind Step is a special utility ability

        auraCost = 1; // Assuming a low aura cost for a utility move
        cooldownTurns = 2; // 2-turn cooldown as per design doc

        // Targeting and movement
        targetsSelf = true; // Self-targeted
        targetsAlly = false;
        targetsEnemy = false;
        targetsGround = true; // Targets a ground position to move to
        range = 2; // Can move up to 2 tiles away
        areaOfEffect = 0; // Single target position

        // Use TeleportToTarget movement type for instant movement
        movementType = AbilityMovementType.TeleportToTarget;

        // No direct damage or healing
        damageAmount = 0;
        healAmount = 0;

        // No applied status effects from the ability itself
        appliedEffects = new System.Collections.Generic.List<StatusEffectData>();

        // No zone creation
        createsZone = false;
    }
} 