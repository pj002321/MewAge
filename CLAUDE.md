# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MuAge is a 2D wave-based survival roguelite built in Unity (C#). The player fights waves of enemies, earns gold/XP, and spends wave-clear time in a weapon shop. Between waves the player can also pick augments (증강).

## Architecture

### Singleton Pattern
Most manager classes extend `MonoBehaviourSingleton<T>` (in [Assets/Scripts/Extension/MonoBehaviourSingleton.cs](Assets/Scripts/Extension/MonoBehaviourSingleton.cs)) and are accessed via `ClassName.Shared`. `PlayerController` and `WeaponManager` use a manual `static Instance` instead.

Override `DontDestroy => false` in scene-specific singletons (managers that should reset on scene reload).

### Core Game Loop
1. **GameManager** — wave timer, game-over, UI text/sliders. Drives the loop: wave start → timer countdown → `WaveClearRoutine` → augment panel → upgrade panel → next wave.
2. **EnemySpawner** — spawns enemies per wave; stopped/resumed by GameManager.
3. **PlayerController** — movement via VirtualJoystick, HP, combat stats. Calls `GameManager.TriggerGameOver()` on death.

### Weapon System
- **WeaponData** — ScriptableObject-like runtime data loaded from BansheeGz DB (`DB_무기`).
- **PlayerInventory** (singleton) — inventory (bag) + equipment slots (max 6). Fires `OnChanged` event.
- **WeaponManager** — listens to `PlayerInventory.OnChanged`, instantiates weapon prefabs under `weaponAnchors` transforms on the player.
- **WeaponPoint** — per-weapon attack logic attached to weapon prefabs.

### Wave-Clear Screens (shown in order)
1. **ImprovManager / ImprovPanel** — augment picker, 3 random buffs drawn from `DB_증강` by weighted random.
2. **UpgradeManager / UpgradePanel** — weapon shop; draws 4 weapon cards from `DB_무기` using grade probabilities from `DB_확률`.

### Data / Database
Uses **BansheeGz** (BgDatabase) for tabular game data. Tables accessed as `DB_무기`, `DB_확률`, `DB_증강`, etc. with Korean field names prefixed `f_`. Data is loaded in `Awake` of the relevant manager.

### Structure System (new, in progress)
`StructureController`, `StructureZone`, `StructureInventory`, `StructureOrb`, `StructureSlot`, `StructureButton` — a placement/structure system being added alongside the wave gameplay.

## Key Conventions
- Singletons accessed via `.Shared`; `PlayerController` / `WeaponManager` use `.Instance`.
- UI updates go through GameManager public methods (`UpdateHP`, `UpdateGoldUI`, `UpdateExpUI`).
- `Time.timeScale = 0f` to pause during panels; restore to `1f` on close.
- Scene reload for restart: `SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex)`.
- Debug logging helpers `this.DLog` / `this.DError` are extension methods from [Assets/Scripts/Extension/StringExtension.cs](Assets/Scripts/Extension/StringExtension.cs).
