# Dokkaebi - Project Status

**Last Updated:** [DATE - Please update this]

## Overall Status

**Phase:** Local Prototype Implementation (In Progress)

**Current Focus:** Implementing core combat mechanics (`AbilityManager`) and resource management (`AuraManager`). The codebase is stable and compiling cleanly. Foundational systems including the Turn System, basic movement, simultaneous movement resolution, and event connections are functional. Zone system implementation is partial (merging/resonance logic present , but basic effect application needs review/completion ). UI element connection to live data is pending completion of underlying systems. The prototype scope remains focused on **local play only**.

## Key Objectives (Local Prototype)

* Implement core gameplay loop (DTFS, Movement, Combat Abilities, Zones) for local play.
* Data-driven design using ScriptableObjects.
* Stable and performant core systems.
* Functional basic UI for gameplay feedback.

## Current Tasks & Blockers

* **Task:** Implement Core Combat Mechanics (`AbilityManager`)
    * **Status:** **To Do / In Progress** * **Details:** Implementing execution logic for core ability types (damage, healing, status effects), including cost deduction (requires AuraManager) and cooldown application.
* **Task:** Implement Resource Management (`AuraManager`)
    * **Status:** **To Do** * **Details:** Creating the system to track and modify player Aura/MP resources, required for ability costs.
* **Task:** Verify/Complete `ZoneManager` Basic Effects
    * **Status:** **To Do / Needs Verification** * **Details:** Confirm or implement the application of basic effects (like damage/healing per turn) defined in `ZoneData`. Merging/resonance is functional.
* **Task:** Connect Core UI Elements to Live Data
    * **Status:** **To Do** * **Details:** Hooking up UI panels (HUD, Unit Info, Ability Bar, Resource Bars) to display live data from Unit, Ability, and Resource systems once they are functional.
* **Task:** Implement/Verify Win/Loss Conditions (`GameController`)
    * **Status:** **To Do / Needs Verification** * **Details:** Ensure game correctly identifies and triggers end-state based on unit elimination for local play.
* **Blockers:**
    * Combat implementation (`AbilityManager`) depends on `AuraManager` for costs.
    * UI connection depends on functional Combat (`AbilityManager`) and Resource (`AuraManager`) systems.
    * Win/Loss condition testing depends on combat completion.

## Completed Milestones (Recent)

* Core codebase refactoring completed (compilation errors resolved, dependencies stabilized). * Functional Turn System (`DokkaebiTurnSystemCore`).
* Functional Basic Movement (`DokkaebiMovementHandler` integrated with A*).
* Implemented Simultaneous Movement Resolution logic (local play). * C# event connections between major systems implemented. * Zone merging and resonance mechanics implemented. * Initial setup of core systems (Grid, Units, Core, UI, etc.).
* Definition of core interfaces (`Dokkaebi.Interfaces`).
* Establishment of data structures (`Dokkaebi.Core.Data`).

## Known Issues / Technical Debt

* **Inconsistent Dependency Injection:** Mix of `[SerializeField]`, `FindObjectOfType`, etc. still likely present. Needs standardization (recommendation: `[SerializeField]` + Service Locator).
* **Potential Duplicate Code/Classes:** `GridServices` potentially duplicated. Requires investigation. *(Note: `AbilityManager` duplication previously noted is resolved)*.
* **Obsolete Code:** Some classes marked as deprecated (e.g., `DokkaebiGridConverter`). Need cleanup.
* **Documentation Sync:** Some parts of the documentation may be out of sync with recent code changes.

*(Add other known major bugs or impediments here)*

## Next Steps (Local Prototype Focus)

* Implement `AbilityManager` (core combat effects). * Implement `AuraManager`. * Verify/Complete `ZoneManager` basic effects.
* Connect UI elements to display live game data.
* Implement and test `GameController` win/loss conditions.
* Write unit and integration tests for implemented features.
* (Post-Prototype) Re-evaluate networking implementation if desired.