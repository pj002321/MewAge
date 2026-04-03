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
- **`EquipmentSlotView`** — reusable slot UI; `Bind(data, callback)` updates icon/grade frame and wires click. `StartSpin(degreesPerSecond)` / `StopSpin()` activate a spin Image overlay (gacha result popup 전용).

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

- **`isInstallation` bool** (`[SerializeField] protected bool isInstallation`) — if `false`, `StructureController` immediately disables all child `SpriteRenderer`s after `Activate()`. Set `true` for skills that need persistent visuals (FireSpreadSkill, StoneTotemSkill).
- **`StructureController.Duration`** — public property exposing the duration field. Skills that auto-complete (MeteorSkill) read this to divide work evenly within the lifespan.
- **`OnDestroy()` cleanup rule** — any skill that spawns objects or modifies global state (e.g., `Light2D.intensity`) must implement `OnDestroy()` to restore state, because `StructureController` destroys the GameObject when duration expires, killing all coroutines mid-run.

Concrete skills (all extensible — freeze/slow/projectile patterns reusable by other skills):
| 스킬 | 클래스 | 동작 |
|------|--------|------|
| 메테오 | `MeteorSkill` | `Camera.main` 뷰포트 내 랜덤 위치에 운석 낙하. `StructureController.Duration`으로 interval 자동 계산. 시작/종료 시 Global Light2D 어둡게. `OnDestroy`에서 조명 즉시 복구 |
| 번개 | `ThunderSkill` | 모든 적 위치에 번개 스폰 (적 자식으로 부착 → 적 사망 시 자동 소멸). Global Light2D 페이드 |
| 얼음 | `IceSkill` | **살아있는 모든 적** (`FindObjectsByType<EnemyController>`)에 필중 동결. `GetComponentInChildren<SpriteRenderer>()` 로 색상 변경. `IceSkill.FreezeEnemy()` 정적 코루틴으로 다른 스킬에서 재사용 가능 |
| 돌벼락 | `StoneThunderSkill` | 돌이 좌→우로 굴러가며 맞은 적에게 피해 + 방향으로 밀침(Rigidbody2D AddForce) |
| 둔화 | `SlowSkill` | 범위 내 적 이동속도 subValue% 감소, subValue 초 지속 |
| 돌창토템 | `StoneTotemSkill` | 설치 후 일정 주기로 projectileCount개 방사형 투사체 발사. `_activeProjectiles` 목록 관리 → `OnDestroy` 시 미비행 투사체 전부 제거 |

기물 아이콘 프리팹: `Resources/Ware/{Chapter}/{id.lower()}.prefab`
설치 프리팹: `Resources/Ware/{Chapter}/{id.lower()}_s.prefab`

**기물 드롭 (`ItemDropManager.SpawnStructureOrb`)**: `DB_기물`을 `f_기물ID.StartsWith($"C{currentChapter}_")`로 필터, 아이콘/오브 프리팹은 `Resources/Ware/C{chapter}/{id.ToLower()}` 경로로 `Resources.Load`.

#### 기물 장기 설계 (확립된 방향)

**챕터 구성**: 구석기·중석기·신석기·중세·근대·현대·우주 (7챕터). 챕터당 기물 4개, 총 28개. 개발 순서는 3챕터씩 검증 후 확장.

**시대별 역할 분담** — 각 시대가 고유 역할을 담당해 신챕터에서도 구형 기물이 필요하도록 설계:
| 시대 | 역할 |
|------|------|
| 구석기 | CC (빙결·둔화) |
| 중석기 | 광역 버스트 |
| 신석기 | 지속 피해·회복 |
| 중세 | 방어·막기 |
| 근대 | 광역 둔화·함정 |
| 현대 | 버프·생산 |
| 우주 | 고급 디버프·차원 |

**범용성**: 모든 기물은 모든 챕터에서 사용 가능. `StructureSkillBase.Activate()` 전에 웨이브 배율(`1f + wave * 0.05f`)을 `mainValue`에 적용해 스탯이 챕터에 뒤처지지 않게 유지.

**강화 시스템**: 동일 기물 3개 합성 → ★ 상승 (★1~★3). 스탯 수치만 증가, 외형 스프라이트 교체 없음. ★ 등급별로 머티리얼/쉐이더 테두리 글로우 색상만 변경 (★1=기본, ★2=골드 글로우, ★3=퍼플 글로우). `DB_기물`에 `f_강화등급`, `f_S1/S2/S3`(등급별 수치) 필드 추가.

**아웃게임 프리셋 시너지**: 아웃게임 기물 패널에서 4슬롯 구성 시 시대 조합에 따라 시너지 효과 미리보기 표시. 동일 시대 2개=쿨타임-15%, 4개=쿨타임-30%+수치+20%, 2시대 혼합=체력회복, 3시대=전역쿨타임-20%, 4시대("시대의 수렴")=전체+50%. 시너지는 인게임 시작 시 `PlayerStatManager`에 1회 반영.

**잉여 기물 처리**: 합성 재료 우선. 합성 불가 잉여분은 골드로 분해.

**가챠 풀**: 아웃게임 상점에 시대별 캡슐 분리 (`DB_확률` `f_가챠타입` = `EQ_WARE_C1` 등). 현재 챕터 캡슐 외 과거 챕터 캡슐도 항상 구매 가능.

### Boss Waves
GameManager queries `DB_웨이브` (field `f_보스`) to find boss wave numbers. On boss wave: `StartBossWave()` freezes timer, calls `WaveEnvironmentSwitcher`. After defeat: `OnBossDefeated()` resumes `WaveClearRoutine`.

### Home Scene (`Assets/_Recovery/Home.unity`)
Lobby screen built via Editor tools — do **not** hand-edit the scene file.

- **`Tools/Build Home UI`** — creates `HomeUICanvas` with 5 panels (상점·대원·전투·장비·기물) and a bottom `TabBar`. Attaches **HomeUIManager** to the canvas. `Panel_전투` is active by default; others start inactive.
- **`Tools/Build Equipment Panel`** — populates `Panel_장비` with character view, 6 equip slots (left 3 / right 3), inner tab bar (장비/성물), and a scrollable inventory grid. Attaches **HomeEquipPanel**.
- **HomeUIManager** (`Assets/Scripts/Game/HomeUIManager.cs`) — owns `panels[]` + `tabButtons[]`, calls `ShowTab(index)`. `StartGame()` loads `stage@scene`.
- **HomeEquipPanel** (`Assets/Scripts/Game/HomeEquipPanel.cs`) — drives equip slot visuals from `EquipmentInventory.Shared`. `equipSlots[0~5]` must be wired in Inspector matching category order (RING=0 … HEAD=5).
- **HomeMemberPanel** (`Assets/Scripts/Game/HomeMemberPanel.cs`) — character roster panel with 캐릭터/룬 tabs. Each slot is a **CharacterSlotView** that binds `DB_캐릭터`; owned/unowned state toggles equip vs. buy button. On `Select()`, equip button text changes to **"장착중"** if `PlayerCharacterManager.Shared.EquippedCharID == data.f_캐릭ID`. **CharacterGradeColor** maps `f_등급` (S/A/B/C) to slot background color.
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
- **`UIButtonSoundAutoAttach`** — attach to a Canvas root to auto-wire all child `Button`s with click SFX via `btn.onClick.AddListener`. **Do not use `IPointerClickHandler` for sound on buttons** — it fires once per collider in the hierarchy causing duplicate sounds. `onClick.AddListener` fires exactly once per click.

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
- **Global Light2D 어둡게 패턴**: `Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None)`로 탐색 후 `l.lightType == Light2D.LightType.Global` 체크. `using UnityEngine.Rendering.Universal;` 필요. 조명을 변경하는 스킬은 반드시 `OnDestroy()`에서 `intensity = 1f`로 복구.
- **무기 상점에서 특정 등급이 안 나올 때**: `DB_확률`에서 해당 타입 행의 등급별 확률 필드(`f_에픽` 등)가 0인지 확인. BansheeGz 에디터에서 직접 수정 필요.
- **`StructureInventory.Push()` 스태킹**: 동일 `f_기물ID`인 슬롯이 있으면 count 증가, 없으면 새 슬롯 생성.

## Memory Management Conventions

메모리 누수 방지를 위한 프로젝트 규약. 새 컴포넌트 작성 시 반드시 준수.

### 이벤트 구독 규칙
- `+=` 구독은 반드시 대칭하는 `-=` 해제가 있어야 함
- **생명주기별 패턴**:
  - `Awake/Start`에서 구독 → `OnDestroy`에서 해제
  - `OnEnable`에서 구독 → `OnDisable`에서 해제 (패널·팝업류)
- static 이벤트나 싱글톤 이벤트(`PlayerInventory.OnChanged`, `BossController.OnEnraged` 등)는 특히 주의 — 구독 오브젝트가 파괴되어도 이벤트 홀더가 살아있으면 참조가 남음
- 수정 이력: `BossBehaviour`, `BossPatternBehaviour` → `OnDestroy`에서 `boss.OnEnraged -= ...`, `BossController` → `OnDestroy`에서 `enemy.OnDeath -= ...`

### static 컬렉션 규칙
- static `List`/`Dictionary`에 오브젝트를 추가할 때는 반드시 제거 경로를 쌍으로 작성
- 오브젝트 풀을 사용하는 경우: 풀에서 꺼낼 때(Wake) 추가, 풀로 돌려보낼 때(Sleep) 제거, `OnDestroy`는 안전망으로만 사용
- 예시: `EnemyController.All` — `WakeFromPool()`에서 `All.Add`, `SleepToPool()`에서 `AllRemoveSwapBack`, `OnDestroy`에서 안전망 제거

### 코루틴 규칙
- 무한 루프(`while(true)`) 코루틴은 반드시 `OnDestroy`에서 `StopAllCoroutines()` 또는 개별 `StopCoroutine()` 호출
- `static Instance` 패턴의 싱글톤은 `OnDestroy`에서 `Instance = null`도 함께 처리 (씬 재로드 후 stale 참조 방지)
- 수정 이력: `PlayerController.OnDestroy` — `StopAllCoroutines()` + `Instance = null`

### 동적 GameObject 생성 규칙
- 런타임에 `new GameObject()` 또는 `Instantiate()`로 생성한 오브젝트는 **오브젝트 풀**로 관리
- 풀 패턴: `Queue<T> _pool` + `List<T> _active`, 꺼낼 때 `SetActive(true)`, 반환할 때 `SetActive(false)` + `Enqueue`
- `OnDisable`에서 active → pool 이동, `OnDestroy`에서 pool 전체 `Destroy` (씬 언로드 전 명시적 정리)
- `List.RemoveAt`은 O(n) — 순서가 중요하지 않으면 스왑백(`list[i] = list[^1]; list.RemoveAt(list.Count-1)`)으로 O(1) 처리

### 배치 스탯 변경 규칙
- `PlayerStatManager.AddPctMod` / `SetBase`는 호출마다 `PlayerController.ApplyStatSheet` + `StatPanelUI.Refresh`를 실행함
- 여러 스탯을 한꺼번에 변경할 때는 반드시 배치 모드를 사용:
  ```csharp
  sm.BeginBatch();
  sm.SetBase(StatType.MaxHP, hp);
  sm.AddPctMod(StatType.ATK, pct);
  // ... 추가 변경
  sm.EndBatch(); // 이 시점에 ApplyStatSheet 1회 실행
  ```
- `GameManager.Start()`의 캐릭터 스탯 + 기물 시너지 적용이 이 패턴을 따름

### Resources.Load 규칙
- `Resources.Load`는 메인 스레드를 블로킹하는 동기 작업 — 씬 시작 시 대량 호출 금지
- 동일 경로를 반복 로드할 때는 로컬 필드에 캐시 (`_cachedSprite`, `_cachedSpriteCharId` 등)
- `DontDestroy => true` 싱글톤에서는 처음 로드 이후 씬 전환을 넘어 캐시가 유지됨

## Player Visual Effects

### GhostTrail (`Assets/Scripts/Game/GhostTrail.cs`)
플레이어 자식 오브젝트에 부착하는 이동 고스트 트레일 컴포넌트.

- `customSprite`가 null이면 부모 `SpriteRenderer` 스프라이트를 자동 복사, 지정하면 해당 스프라이트 사용 (캐릭터별 커스텀 가능)
- 내부 오브젝트 풀(`Queue<GhostItem>` + `List<GhostItem>`)로 동적 할당 최소화
- `OnDisable` → active를 pool로 이동 / `OnDestroy` → pool 전체 `Destroy`
- Inspector 주요 설정: `Spawn Interval`(생성 간격), `Ghost Lifetime`(페이드 시간), `Min Move Dist`(생성 최소 이동거리), `Ghost Color`(색조·초기 투명도)

### Dust Trail (먼지 트레일)
- `PlayerController`의 `[SerializeField] private ParticleSystem dustTrailEffect` 슬롯에 자식 ParticleSystem 연결
- 이동 시작 → `Play()`, 정지 시 → `Stop(true, StopEmitting)` (기존 파티클은 자연 소멸)
- 플레이어 좌우 반전 시 `dustTrailEffect.transform.localScale.x`도 반전 (`FlipSprite()`에서 처리)
