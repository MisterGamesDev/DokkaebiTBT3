using UnityEngine;
using Dokkaebi.Core.Data;
using Dokkaebi.Interfaces;
using Dokkaebi.Common;

/// <summary>
/// ScriptableObject for the Terrain Shift ability.
/// This defines the data and configuration for the special ability that moves a zone 2 tiles in any direction.
/// </summary>
[CreateAssetMenu(fileName = "TerrainShiftAbilityData", menuName = "Dokkaebi/Data/Ability Data/Terrain Shift")]
public class TerrainShiftAbilityData : AbilityData
{
    /// <summary>
    /// Ensures the Terrain Shift ability always has the correct configuration.
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        // Set required properties for Terrain Shift
        abilityId = "TerrainShift"; // Unique identifier for this ability
        displayName = "Terrain Shift"; // Name shown in UI
        description = "Move a zone 2 tiles in any direction."; // Description for tooltips/UI
        abilityType = AbilityType.Special; // This is a special ability
        auraCost = 2; // Costs 2 Aura to use
        cooldownTurns = 2; // 2 turn cooldown
        targetsGround = true; // Targets a ground position (the destination for the zone)
        range = 4; // Can target up to 3 tiles away from the caster
        areaOfEffect = 0; // Single-target (the zone itself)
        movementType = AbilityMovementType.None; // The caster does not move; the zone is moved
        createsZone = false; // This ability moves an existing zone, it does not create a new one
    }
} 