# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MuAge is a 2D wave-based survival roguelite built in **Unity 6000.3.9f1 (Unity 6)** (C#). The player fights waves of enemies, earns gold/XP, and spends wave-clear time in a weapon shop. Between waves the player can also pick augments (증강).

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
1. **ImprovManager / ImprovPanel** — augment picker, 3 random buffs drawn from `DB_증강` by weighted random. On pick, routes all stat-type augments through `PlayerStatManager.AddPctMod()`. Non-stat augments (heal, heal-on-kill, heal-orb) call `PlayerController` directly.
2. **UpgradeManager / UpgradePanel** — weapon shop; draws 4 weapon cards from `DB_무기` using grade probabilities from `DB_확률`. Has a `statPanelButton` wired to `StatPanelUI.Shared?.Show()`.

### Player Progression
- **PlayerStats** (singleton, `DontDestroy => false`) — gold, XP, level. `AddGold` / `SpendGold` / `AddExp` each call `SyncUI()` which updates `GameManager`. Gold and XP are separate from `PlayerController` HP.
- Level formula: `expToNext = baseExpPerLevel + (level-1) * expGrowthPerLevel`. Max level caps at 20.
- **PlayerCharacterManager** (singleton, `DontDestroy => true`, persists home→game) — stores equipped character ID + augment tier in `PlayerPrefs`. `ApplyCharacterStats()` is called at game scene start (before `ImprovManager` init) to apply base stats and cumulative tier bonuses (Tiers 1–3 from `DB_캐릭터` `f_속성1ID`/`f_S1` …). Grade-based base stats: C=100/10/10, B=150/20/20, A=200/30/30, S=250/50/50, SS=300/70/70 (HP/DEF/ATK). Character sprite loaded from `Resources/Character/{캐릭ID.ToLower()}`.

### Player Stat System
Central hub for all 14 character combat stats, separate from `PlayerStats` (gold/XP/level).

- **`StatType`** (enum) — `ATK, DEF, MaxHP, BleedPower, HPRegen, MoveSpeed, MagnetRadius, AtkSpeed, Evasion, CritChance, CritDmg, StructureCooldown, Knockback, HealOnKill`
- **`PlayerStatManager`** (singleton, `DontDestroy => true`) — holds `baseXxx` SerializeFields + `_pctMods[]` + `_equipFlat[]` arrays. `AddPctMod(stat, pct)` accumulates % bonuses. `SetEquipFlat(stat, value)` / `ResetEquipFlat()` manage flat equipment bonuses. Final value formula: `(base + equipFlat) × (1 + totalPct / 100)`. Every modification calls `PlayerController.ApplyStatSheet(this)` and `StatPanelUI.Shared?.Refresh()`.
- **`PlayerController.ApplyStatSheet(PlayerStatManager)`** — called automatically on every mod change; updates `attack`, `defense`, `moveSpeed`, `dodge`, crit fields, `_hpRegenRate`, `_knockbackPower`, `_magnetRadius`. `AtkSpeed` and `StructureCooldown` are read by their respective systems via `PlayerStatManager.Shared.GetFinal()`.
- **`StatPanelUI`** (singleton, `DontDestroy => false`) — popup that collects all child `StatFieldUI` components at Awake and calls `field.Refresh(sm)` on show/update.
- **`StatFieldUI`** — attach to each stat row GameObject; set `statType` in Inspector; auto-finds child `stat_value` (TextMeshProUGUI).

**Augment % convention (`DB_증강`):** `f_주수치` = primary % bonus, `f_보조수치` = secondary. Example: `AUG_ATK` 주수치=10 → `+10%` attack. `AUG_HEAL` 주수치=20 → instant heal `MaxHP × 20%`. `AUG_SKL` sends `-주수치%` to reduce StructureCooldown.

### Equipment System
Persistent across home→game scenes. All files in [Assets/Scripts/Game/](Assets/Scripts/Game/).

- **`EquipmentData`** — runtime copy of a `DB_장비` row. Static `FromDB(row)` builder, `StatIdToType(statId)` maps DB stat IDs (`STAT_ATK` etc.) to `StatType`. Categories: `RING/NECK/FOOT/TOP/CLOAK/HEAD`. Icon loaded from `Resources/Equip/{CivEng}/{Category}/{equipId.lower()}`.
- **`EquipmentInventory`** (singleton, `DontDestroy => true`) — 6 fixed category-based slots (`equipSlots[0]=RING … [5]=HEAD`, index = `(int)EquipCategory`) + unlimited bag. `EquipFromBag(data)` places item in its category slot, displaces existing item to bag. `UnequipToBag(data)` by category slot. Every change calls `RecalculateEquipStats()` → `PlayerStatManager.SetEquipFlat()`.
- **`EquipmentGacha`** — static class. `Draw(EquipBoxType)` / `DrawMulti(count, EquipBoxType)`. Reads weights and pity from `DB_확률` (keyed by `f_가챠타입`). Three box types: `Normal` (EQ_NORMAL), `Rare` (EQ_RARE), `Epic` (EQ_EPIC). Epic box has two-tier pity (epic pity / legendary pity) via static `_pityCount[3][2]` counters. Falls back to hardcoded weights if DB row missing.
- **`GachaDrawButton`** — reads unit cost from `DB_상점` (`f_상품ID` match), computes multi-draw price as `unitCost × (n−1)` (1 free). Routes click to `HomeShopPanel.TryDrawMulti()`.
- **`GachaResultPopup`** — overlay that reparents to root Canvas on open. Shows single or multi-draw results.
- **`EquipmentDetailPopup`** — shown on bag/equip slot click; "장착" calls `EquipmentInventory.EquipFromBag()`, "장착 해제" calls `UnequipToBag()`.
- **`EquipmentSlotView`** — reusable slot UI; `Bind(data, callback)` updates icon/grade frame and wires click.

**DB stat ID → StatType mapping:**
`STAT_ATK→ATK`, `STAT_DEF→DEF`, `STAT_HP→MaxHP`, `STAT_CRI_DMG→CritDmg`, `STAT_CRI_RATE→CritChance`, `STAT_EVA_RATE→Evasion`, `STAT_SPD_MOV→MoveSpeed`, `STAT_SPD_ATK→AtkSpeed`, `STAT_SKL_CD→StructureCooldown`, `STAT_DOT_DMG→BleedPower`

### Weapon Synergy System
- **WeaponSynergyTag** (enum) + **`DB_시너지`** table keyed by `f_챕터` — each chapter row holds up to 4 synergy tags with icons/descriptions.
- **SynergyHelper** — static utility: `CountEquippedTag(tag)` checks `PlayerInventory.Shared.Equipped`, `GetTierColor(count)` returns Bronze/Silver/Gold color thresholds (1/3/5).
- **SynergyInfoPanel** — UI display of active synergies.
- **WeaponStatRegistry** — static; lazily loads stat-id templates from `DB_무기` (`f_속성ID` / `f_속성내용`). `ApplyBonus(WeaponData, WeaponStarBonus)` applies percentage-based boosts (`ATK_ADD`, `CD_RED`, `RNG_ADD`, `CRIT_ADD`, `BLEED_ADD`, `LIFESTEAL`).
- **WeaponStarBonus** — struct holding `statId`, `value`, `description`. Built via `WeaponStatRegistry.Build()`.

### Chapter Roster ScriptableObject
**ChapterRosterSO** (`Assets/Scripts/Game/ChapterRosterSO.cs`) — ScriptableObject per chapter, stored in `Assets/Data/`. Holds normal-enemy spawn entries (with weight/minWave/maxWave), boss prefab, and `WaveSpawnRule` list. Edit via **`MuAge/Enemy Preset Editor`** window. `GetRuleForWave(wave)` returns the highest-`fromWave` matching rule.

### Data / Database
Uses **BansheeGz** (BgDatabase) for tabular game data. Generated classes in `Assets/BansheeGz/BgData.cs`, namespace `Mewage.DB`. All scripts that use DB classes need `using Mewage.DB;`.

Key tables:
| 테이블 | 주요 필드 | 용도 |
|--------|----------|------|
| `DB_장비` | `f_장비ID`, `f_카테고리`, `f_등급`, `f_스탯ID`, `f_수치`, `f_문명` | 장비 아이템 |
| `DB_확률` | `f_가챠타입`, `f_G일반/레어/에픽/전설`, `f_필중횟수(List<int>)` | 뽑기 확률 |
| `DB_상점` | `f_상품ID`, `f_가격`, `f_수량`, `f_행수`, `f_챕터상품ID`, `f_T1~T4`, `f_V1~V4` | 상점 상품 |
| `DB_기물` | `f_기물ID` (형식: `C1_W0`), `f_등급`, `f_설명` | 기물 |
| `DB_무기` | `f_무기ID`, `f_등급`, `f_속성ID/내용` | 무기 |
| `DB_증강` | `f_증강ID`, `f_주수치`, `f_보조수치` | 증강 |
| `DB_캐릭터` | `f_캐릭ID`, `f_등급`, `f_속성1ID`, `f_S1` | 캐릭터 |
| `DB_웨이브` | `f_보스` | 보스 웨이브 번호 |

### Structure System
Tower/placement system. Key classes all in [Assets/Scripts/Game/](Assets/Scripts/Game/):
- **StructureInventory** — singleton (`DontDestroy => false`), max 4 slots, stacking same type. `Push(DB_기물)` on orb pickup, `UseAt(slot, pos)` to deploy. Spawns `Resources/Ware/{Chapter}/{id.lower()}_s.prefab` (e.g., `C1_W0 → Ware/C1/c1_w0_s`).
- **StructurePlacer** — handles launch animation (parabolic arc) from UI button → world position.
- **StructureZone** — grid-snap placement area using `BoxCollider2D`. `TryGetNearestCell()` / `OccupyCell()` / `ReleaseCell()`.
- **StructureOrb** — enemy-drop collectible; auto-magnets to player after delay, calls `StructureInventory.Push()` on collision.

#### Structure Skill Extension
`StructureSkillBase` — abstract base. Fields: `mainValue` (damage/heal), `subValue` (secondary), `radius`, `tickInterval`. Implement `Activate()` which is called by `StructureController.Start()`.

Concrete skills (all extensible — freeze/slow/projectile patterns reusable by other skills):
| 스킬 | 클래스 | 동작 |
|------|--------|------|
| 메테오 | `MeteorSkill` | 사선으로 meteorsCount개 운석 낙하, 착지 시 범위 피해 |
| 얼음 | `IceSkill` | 범위 적 파란색으로 동결(subValue초), 해제 후 mainValue 피해. `IceSkill.FreezeEnemy()` 정적 코루틴으로 다른 스킬에서 재사용 가능 |
| 돌벼락 | `StoneThunderSkill` | 돌이 좌→우로 굴러가며 맞은 적에게 피해 + 방향으로 밀침(Rigidbody2D AddForce) |
| 둔화 | `SlowSkill` | 범위 내 적 이동속도 subValue% 감소, subValue 초 지속 |
| 돌창토템 | `StoneTotemSkill` | 설치 후 일정 주기로 projectileCount개 방사형 투사체 발사 |

기물 아이콘 프리팹: `Resources/Ware/{Chapter}/{id.lower()}.prefab`
설치 프리팹: `Resources/Ware/{Chapter}/{id.lower()}_s.prefab`

### Boss Waves
GameManager queries `DB_웨이브` (field `f_보스`) to find boss wave numbers. On boss wave: `StartBossWave()` freezes timer, calls `WaveEnvironmentSwitcher`. After defeat: `OnBossDefeated()` resumes `WaveClearRoutine`.

### Home Scene (`Assets/_Recovery/Home.unity`)
Lobby screen built via Editor tools — do **not** hand-edit the scene file.

- **`Tools/Build Home UI`** — creates `HomeUICanvas` with 5 panels (상점·대원·전투·장비·기물) and a bottom `TabBar`. Attaches **HomeUIManager** to the canvas. `Panel_전투` is active by default; others start inactive.
- **`Tools/Build Equipment Panel`** — populates `Panel_장비` with character view, 6 equip slots (left 3 / right 3), inner tab bar (장비/성물), and a scrollable inventory grid. Attaches **HomeEquipPanel**.
- **HomeUIManager** (`Assets/Scripts/Game/HomeUIManager.cs`) — owns `panels[]` + `tabButtons[]`, calls `ShowTab(index)`. `StartGame()` loads `stage@scene`.
- **HomeEquipPanel** (`Assets/Scripts/Game/HomeEquipPanel.cs`) — drives equip slot visuals from `EquipmentInventory.Shared`. `equipSlots[0~5]` must be wired in Inspector matching category order (RING=0 … HEAD=5).
- **HomeMemberPanel** (`Assets/Scripts/Game/HomeMemberPanel.cs`) — character roster panel with 캐릭터/룬 tabs. Each slot is a **CharacterSlotView** that binds `DB_캐릭터`; owned/unowned state toggles equip vs. buy button. **CharacterGradeColor** maps `f_등급` (S/A/B/C) to slot background color.
- **HomeShopPanel** (`Assets/Scripts/Game/HomeShopPanel.cs`) — gacha/IAP shop. Calls `EquipmentGacha.Draw()` / `DrawMulti()`, adds results to `EquipmentInventory`. **GachaDrawButton** reads price from `DB_상점` and wires itself to `TryDrawMulti()` via parent lookup. **GachaResultPopup** overlay reparents itself to the root Canvas on open.
- **StageSelectPanel** (`Assets/Scripts/Game/StageSelectPanel.cs`) — carousel of stage cards (`StageCardView`) with dot indicators and snap animation. `OnStartClicked()` loads `stage@scene`.

### Editor Tools
[Assets/Scripts/Editor/SceneToolMenu.cs](Assets/Scripts/Editor/SceneToolMenu.cs) adds a Unity toolbar with quick-launch buttons for `home@scene`, `loading@scene`, `stage@scene`, and `dev@*` scenes.

[Assets/Scripts/Editor/HomeUIBuilder.cs](Assets/Scripts/Editor/HomeUIBuilder.cs) — editor-only scene builders for the Home UI. `BuildGachaCardRow()` creates a 2-column HLG row with Normal/Rare gacha cards side-by-side.

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
- **BansheeGz BgDatabase** — tabular game data; not in the standard package manifest (installed separately). Generated class namespace: `Mewage.DB`.
- **Unity URP** — Universal Render Pipeline; shaders must target URP.
- **Cinemachine** — camera follow system; `CameraFollow.cs` wraps it for the game camera.
- **Unity Input System** — `VirtualJoystick` uses the new Input System package.

## Key Conventions
- Singletons accessed via `.Shared`; `PlayerController` / `WeaponManager` use `.Instance`.
- UI updates go through GameManager public methods (`UpdateHP`, `UpdateGoldUI`, `UpdateExpUI`).
- `Time.timeScale = 0f` to pause during panels; restore to `1f` on close.
- Scene reload for restart: `SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex)`.
- Scene transitions with loading screen: `SceneLoadManager.LoadScene("stage@scene")` — automatically overlays `loading@scene`, waits for load, then animates out. `loading@scene` must be registered in Build Settings.
- Debug logging helpers `this.DLog` / `this.DError` are extension methods from [Assets/Scripts/Extension/StringExtension.cs](Assets/Scripts/Extension/StringExtension.cs).
- Methods called by Unity Inspector Button OnClick events must be `public` — private methods are not invokable from the Inspector event system.
- **Editor scripts searching for inactive GameObjects** must use `Resources.FindObjectsOfTypeAll<GameObject>()` filtered by `go.scene`, not `GameObject.Find()` (which skips inactive objects).
- Scene files follow the `name@scene` naming convention (e.g., `home@scene.unity`, `stage@scene.unity`).
- `MainMenuHud.cs` lives in `Assets/` root (not `Assets/Scripts/Game/`); exit logic uses `Application.Quit()` in builds and `EditorApplication.isPlaying = false` in editor.
