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
Tower/placement system. Key classes all in [Assets/Scripts/Game/](Assets/Scripts/Game/):
- **StructureInventory** — singleton, max 4 slots, stacking same type. `Push(DB_기물)` on orb pickup, `UseAt(slot, pos)` to deploy.
- **StructurePlacer** — handles launch animation (parabolic arc) from UI button → world position.
- **StructureZone** — grid-snap placement area using `BoxCollider2D`. `TryGetNearestCell()` / `OccupyCell()` / `ReleaseCell()`.
- **StructureOrb** — enemy-drop collectible; auto-magnets to player after delay, calls `StructureInventory.Push()` on collision.
- **StructureSkillBase** — abstract base for structure behaviors (DoT, HealZone, Fence, Paralyze, Thunder).

### Boss Waves
GameManager queries `DB_웨이브` (field `f_보스`) to find boss wave numbers. On boss wave: `StartBossWave()` freezes timer, calls `WaveEnvironmentSwitcher`. After defeat: `OnBossDefeated()` resumes `WaveClearRoutine`.

### Editor Tools
[Assets/Scripts/Editor/SceneToolMenu.cs](Assets/Scripts/Editor/SceneToolMenu.cs) adds a Unity toolbar with quick-launch buttons for `home@scene`, `loading@scene`, `stage@scene`, and `dev@*` scenes.

### Debug Panel
[Assets/Scripts/Game/DebugPanel.cs](Assets/Scripts/Game/DebugPanel.cs) — in-game debug UI. Calls `GameManager.DebugSkipWave()` and `GameManager.DebugJumpToBossWave()`. Methods on DebugPanel that are connected via Inspector Button OnClick must be `public`.

## Key Conventions
- Singletons accessed via `.Shared`; `PlayerController` / `WeaponManager` use `.Instance`.
- UI updates go through GameManager public methods (`UpdateHP`, `UpdateGoldUI`, `UpdateExpUI`).
- `Time.timeScale = 0f` to pause during panels; restore to `1f` on close.
- Scene reload for restart: `SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex)`.
- Debug logging helpers `this.DLog` / `this.DError` are extension methods from [Assets/Scripts/Extension/StringExtension.cs](Assets/Scripts/Extension/StringExtension.cs).
- Methods called by Unity Inspector Button OnClick events must be `public` — private methods are not invokable from the Inspector event system.
