# Dokkaebi - Development Tasks

## Current Sprint / Focus

**Goal:** Implement Core Combat & Resource Systems for Local Prototype.

**Tasks:**

1.  **[In Progress] Implement Core Combat Mechanics (`AbilityManager`)** * **Goal:** Complete the implementation of `AbilityManager` to handle execution of core prototype ability types (damage, healing, status effects, zone creation), including cost deduction and cooldown application.
    * **Sub-Tasks:**
        * Implement logic for different targeting types (Unit, Ground, AoE).
        * Integrate `StatusEffectSystem` application/removal via abilities.
        * Implement Zone creation logic via abilities (linking to `ZoneManager`).
        * Handle Aura cost deduction (Requires `AuraManager`).
        * Handle Cooldown application.
    * **Acceptance:** Core set of prototype abilities can be executed correctly in local play.

2.  **[To Do] Implement Resource Management (`AuraManager`)** * **Goal:** Create the system responsible for tracking player Aura/MP, handling gain per turn/action, and enabling cost deduction for abilities.
    * **Sub-Tasks:**
        * Define Aura/MP gain rules.
        * Implement methods for adding/subtracting resources.
        * Integrate with `AbilityManager` for cost checks and deductions.
    * **Acceptance:** Player resources are tracked correctly, and abilities consume the appropriate amount.

3.  **[To Do] Verify/Complete `ZoneManager` Basic Effects** * **Goal:** Ensure `ZoneManager` correctly applies basic turn-based effects (damage, healing) defined in `ZoneData`. Merging/resonance is functional.
    * **Sub-Tasks:**
        * Verify/implement `ApplyZoneEffects` logic in `ZoneInstance`.
        * Ensure `ProcessTurn` correctly triggers effects and handles duration.
        * Test interaction with units entering/leaving/staying in zones.
    * **Acceptance:** Zones apply their defined basic effects correctly over time.

4.  **[To Do] Connect Core UI Elements** * **Goal:** Link UI panels and elements (HUD, Unit Info, Ability Bar, Resource Bars) to display live data from the game systems *after* combat and resources are functional.
    * **Sub-Tasks:**
        * Connect `UnitInfoPanel` to display selected unit stats and status effects.
        * Connect `AbilitySelectionUI` to display selected unit's abilities, cooldowns, costs.
        * Connect `PlayerResourceUI` to display current Aura/MP from `AuraManager`.
        * Connect `TurnPhaseUI` to `TurnSystemCore`.
    * **Acceptance:** Core UI elements correctly display live game state information.

5.  **[To Do] Implement/Verify Win/Loss Conditions** * **Goal:** Ensure the `GameController` logic correctly identifies game end states based on unit elimination.
    * **Sub-Tasks:**
        * Implement check for remaining alive units per team.
        * Trigger game over sequence/UI.
        * Test with various scenarios.
    * **Acceptance:** Game correctly ends when one side has no units left.

## Local Prototype Implementation Tasks (Checklist)

*(Status updated based on user answers)*

* [X] Set up project structure & initial .asmdef files.
* [X] Implement core ScriptableObject data structures.
* [X] Build `GridManager` (Coords, tile data, pathfinding info provider). *(Functioning)*
* [X] Implement `DataManager`. *(Functioning)*
* [X] Create `DokkaebiUnit` prefab/component. *(Functioning)*
* [X] Implement `UnitManager` (Spawning functional).
* [X] Build `DokkaebiTurnSystemCore` state machine. *(Functioning for local play)*
* [X] Set up Input System package & `InputManager`. *(Functioning)*
* [X] Implement `PlayerActionManager` (Command Pattern, validation - Local Focus). *(Adapted)*
* [ ] ~~Select and integrate Networking Library.~~
* [ ] ~~Build authoritative server logic.~~
* [X] Implement `MovementManager` logic (simultaneous resolution, conflict handling - **Local Version**). *(Implemented)*
* [ ] Implement `AuraManager` and MP gain logic.
* [In Progress] Build `AbilityManager` (execute prototype abilities, costs, cooldowns, repositioning).
* [~] Develop `ZoneManager` (Creation, Merging/Resonance functional; Effects TBD).
* [To Do] Create basic `UIManager` (HUD, unit info, ability buttons, turn display - Hookup pending).
* [X] Implement C# event connections between systems. *(Verified)*
* [ ] Write Unit Tests for critical logic.
* [ ] ~~Implement basic network error handling/logging.~~
* [X] Setup Version Control (Git) repository.

## Backlog / Future

* Implement remaining non-prototype features (Overwatch, Calling Absorption, Dynamic Terrain, advanced abilities, etc.).
* Full UI/UX features and polish.
* AI for non-player units.
* Art asset integration.
* Performance optimization passes.
* (Optional) Re-integrate Networking for online play.