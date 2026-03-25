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

### Player Progression
- **PlayerStats** (singleton, `DontDestroy => false`) — gold, XP, level. `AddGold` / `SpendGold` / `AddExp` each call `SyncUI()` which updates `GameManager`. Gold and XP are separate from `PlayerController` HP.
- Level formula: `expToNext = baseExpPerLevel + (level-1) * expGrowthPerLevel`. Max level caps at 20.

### Weapon Synergy System
- **WeaponSynergyTag** (enum) + **`DB_시너지`** table keyed by `f_챕터` — each chapter row holds up to 4 synergy tags with icons/descriptions.
- **SynergyHelper** — static utility: `CountEquippedTag(tag)` checks `PlayerInventory.Shared.Equipped`, `GetTierColor(count)` returns Bronze/Silver/Gold color thresholds (1/3/5).
- **SynergyInfoPanel** — UI display of active synergies.
- **WeaponStatRegistry** — static; lazily loads stat-id templates from `DB_무기` (`f_속성ID` / `f_속성내용`). `ApplyBonus(WeaponData, WeaponStarBonus)` applies percentage-based boosts (`ATK_ADD`, `CD_RED`, `RNG_ADD`, `CRIT_ADD`, `BLEED_ADD`, `LIFESTEAL`).
- **WeaponStarBonus** — struct holding `statId`, `value`, `description`. Built via `WeaponStatRegistry.Build()`.

### Chapter Roster ScriptableObject
**ChapterRosterSO** (`Assets/Scripts/Game/ChapterRosterSO.cs`) — ScriptableObject per chapter, stored in `Assets/Data/`. Holds normal-enemy spawn entries (with weight/minWave/maxWave), boss prefab, and `WaveSpawnRule` list. Edit via **`MuAge/Enemy Preset Editor`** window. `GetRuleForWave(wave)` returns the highest-`fromWave` matching rule.

### Data / Database
Uses **BansheeGz** (BgDatabase) for tabular game data. Tables accessed as `DB_무기`, `DB_확률`, `DB_증강`, `DB_시너지`, `DB_캐릭터`, `DB_웨이브`, etc. with Korean field names prefixed `f_`. Data is loaded in `Awake` of the relevant manager.

### Structure System (new, in progress)
Tower/placement system. Key classes all in [Assets/Scripts/Game/](Assets/Scripts/Game/):
- **StructureInventory** — singleton, max 4 slots, stacking same type. `Push(DB_기물)` on orb pickup, `UseAt(slot, pos)` to deploy.
- **StructurePlacer** — handles launch animation (parabolic arc) from UI button → world position.
- **StructureZone** — grid-snap placement area using `BoxCollider2D`. `TryGetNearestCell()` / `OccupyCell()` / `ReleaseCell()`.
- **StructureOrb** — enemy-drop collectible; auto-magnets to player after delay, calls `StructureInventory.Push()` on collision.
- **StructureSkillBase** — abstract base for structure behaviors (DoT, HealZone, Fence, Paralyze, Thunder).

### Boss Waves
GameManager queries `DB_웨이브` (field `f_보스`) to find boss wave numbers. On boss wave: `StartBossWave()` freezes timer, calls `WaveEnvironmentSwitcher`. After defeat: `OnBossDefeated()` resumes `WaveClearRoutine`.

### Home Scene (`Assets/_Recovery/Home.unity`)
Lobby screen built via Editor tools — do **not** hand-edit the scene file.

- **`Tools/Build Home UI`** — creates `HomeUICanvas` with 5 panels (상점·대원·전투·장비·기물) and a bottom `TabBar`. Attaches **HomeUIManager** to the canvas. `Panel_전투` is active by default; others start inactive.
- **`Tools/Build Equipment Panel`** — populates `Panel_장비` with character view, 6 equip slots (left 3 / right 3), inner tab bar (장비/성물), and a scrollable inventory grid. Attaches **HomeEquipPanel**.
- **HomeUIManager** (`Assets/Scripts/Game/HomeUIManager.cs`) — owns `panels[]` + `tabButtons[]`, calls `ShowTab(index)`. `StartGame()` loads `stage@scene`.
- **HomeEquipPanel** (`Assets/Scripts/Game/HomeEquipPanel.cs`) — drives equip slot visuals from `PlayerInventory.Shared`, handles inner tab switching between `weaponContent` / `artifactContent` grids.
- **HomeMemberPanel** (`Assets/Scripts/Game/HomeMemberPanel.cs`) — character roster panel with 캐릭터/룬 tabs. Each slot is a **CharacterSlotView** that binds `DB_캐릭터`; owned/unowned state toggles equip vs. buy button. **CharacterGradeColor** maps `f_등급` (S/A/B/C) to slot background color.
- **HomeShopPanel** (`Assets/Scripts/Game/HomeShopPanel.cs`) — gacha/IAP shop. **GachaDrawButton** wires itself to `TryDraw(gemCost)` via parent lookup. **GachaResultPopup** overlay reparents itself to the root Canvas on open.
- **StageSelectPanel** (`Assets/Scripts/Game/StageSelectPanel.cs`) — carousel of stage cards (`StageCardView`) with dot indicators and snap animation. `OnStartClicked()` loads `stage@scene`.

### Editor Tools
[Assets/Scripts/Editor/SceneToolMenu.cs](Assets/Scripts/Editor/SceneToolMenu.cs) adds a Unity toolbar with quick-launch buttons for `home@scene`, `loading@scene`, `stage@scene`, and `dev@*` scenes.

[Assets/Scripts/Editor/HomeUIBuilder.cs](Assets/Scripts/Editor/HomeUIBuilder.cs) — editor-only scene builders for the Home UI (see Home Scene above).

### Enemy System
- **EnemyBehaviourBase** — abstract base; subclass to add new enemy types. Requires `EnemyController` on the same prefab. Overridable hooks: `OnAttackUpdate()` (Update), `OnMoveFixedUpdate()` (FixedUpdate), `ApplyWaveScaling(float)`.
- Concrete behaviors: `MeleeBehaviour`, `RangedBehaviour`, `MagicBehaviour`, `HealerBehaviour`, `TankBehaviour`, `ChargeBehaviour`, `SlamBehaviour`, `HordeBehaviour`, `DashComboBehaviour`.
- **BossController / BossBehaviour** — boss variant of the same pattern. Boss skills extend `BossSkillBase`: `BossSlamSkill`, `BossChargeSkill`, `BossDashComboSkill`, `BossComboSlashSkill`, `BossVolleySkill`.

### Sound & FX
- **SoundManager** (singleton) — `SoundManager.Shared.Play(SoundKey)`. Keys defined in `SoundKey.cs` enum. Key-clip pairs registered via Inspector. `minInterval` prevents rapid duplicate plays.
- **FXManager** — pooled visual effects spawned on hit/death etc.
- `UIButtonSound` — component for button click SFX, attaches to UI buttons.

### Debug Panel
[Assets/Scripts/Game/DebugPanel.cs](Assets/Scripts/Game/DebugPanel.cs) — in-game debug UI. Calls `GameManager.DebugSkipWave()` and `GameManager.DebugJumpToBossWave()`. Methods on DebugPanel that are connected via Inspector Button OnClick must be `public`.

## Key Packages
- **UniTask** (`com.cysharp.unitask`) — preferred async library; use `UniTask` instead of `Task`/coroutines for async flows.
- **BansheeGz BgDatabase** — tabular game data; not in the standard package manifest (installed separately).
- **Unity URP** — Universal Render Pipeline; shaders must target URP.
- **Cinemachine** — camera follow system; `CameraFollow.cs` wraps it for the game camera.
- **Unity Input System** — `VirtualJoystick` uses the new Input System package.

## Key Conventions
- Singletons accessed via `.Shared`; `PlayerController` / `WeaponManager` use `.Instance`.
- UI updates go through GameManager public methods (`UpdateHP`, `UpdateGoldUI`, `UpdateExpUI`).
- `Time.timeScale = 0f` to pause during panels; restore to `1f` on close.
- Scene reload for restart: `SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex)`.
- Debug logging helpers `this.DLog` / `this.DError` are extension methods from [Assets/Scripts/Extension/StringExtension.cs](Assets/Scripts/Extension/StringExtension.cs).
- Methods called by Unity Inspector Button OnClick events must be `public` — private methods are not invokable from the Inspector event system.
- **Editor scripts searching for inactive GameObjects** must use `Resources.FindObjectsOfTypeAll<GameObject>()` filtered by `go.scene`, not `GameObject.Find()` (which skips inactive objects).
- Scene files follow the `name@scene` naming convention (e.g., `home@scene.unity`, `stage@scene.unity`).
- `MainMenuHud.cs` lives in `Assets/` root (not `Assets/Scripts/Game/`); exit logic uses `Application.Quit()` in builds and `EditorApplication.isPlaying = false` in editor.
