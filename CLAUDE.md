# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MuAge is a 2D wave-based survival roguelite built in **Unity 6000.3.9f1 (Unity 6)** (C#). The player fights waves of enemies, earns gold/XP, and spends wave-clear time in a weapon shop. Between waves the player can also pick augments (증강) and use structures (기물).

---

## Script Folder Structure

All C# game scripts live under `Assets/Scripts/Game/` and are organized by feature:

```
Assets/Scripts/Game/
  Core/        — 씬 전환, 게임 루프, 로딩
  World/       — 카메라, 맵, 환경, 스폰존, 아이템 드롭
  Player/      — 플레이어 컨트롤, 스탯, 인벤토리, 조이스틱
  Character/   — 캐릭터 선택, 고유 스킬 (CharacterSkillBase 서브클래스)
  Enemy/       — 적 컨트롤러, AI 비헤이비어, 스폰
  Boss/        — 보스 컨트롤러, 보스 스킬
  Weapon/      — 무기 데이터/로직, 투사체, 시너지 (무기)
  Structure/   — 기물 인벤토리/배치, 기물 스킬
  Equipment/   — 장비 아이템, 뽑기, 인벤토리
  Augment/     — 증강(임프로브) 시스템
  HomeUI/      — 홈 씬 패널 전체 (장비/기물/캐릭터/상점/스테이지)
  GameUI/      — 인게임 UI (업그레이드, 게임오버, 스탯패널 등)
  VFX/         — FXManager, 히트 이펙트, 조명 디머
  Audio/       — SoundManager, 버튼 사운드
  (Editor/)    — 에디터 전용 빌더·인스펙터 (Scripts/Editor/)
  (Extension/) — MonoBehaviourSingleton, 유틸 확장 (Scripts/Extension/)
```

### 폴더별 주요 파일

| 폴더 | 핵심 파일 |
|------|----------|
| Core | GameManager, SceneLoadManager, SceneLoadingCanvas, IntroLoadingManager |
| World | CameraFollow, WaveEnvironmentSwitcher, StageEnvironmentSO, ItemDropManager, SpawnMarker/Zone |
| Player | PlayerController, PlayerStatManager, PlayerStats, PlayerInventory, StatType, StatPanelUI |
| Character | PlayerCharacterManager, CharacterSkillBase/Manager, SkillBerserker, SkillNormal |
| Enemy | EnemyController, EnemyBehaviourBase, IEnemyHost, EnemyCombatRoutines, EnemySpawner, EnemyPool, ChapterRosterSO |
| Boss | BossController, BossPatternBehaviour, BossSkillBase, 7개 Boss 스킬 |
| Weapon | WeaponData/Manager/Point, Projectile, SynergyHelper, SynergyEffectManager |
| Structure | StructureInventory/Controller/Ware, WareSynergySystem, 14개 기물 스킬 |
| Equipment | EquipmentData/Inventory/Gacha, EquipSlot, InvenSlot |
| Augment | ImprovManager/Panel/Card/Data |
| HomeUI | HomeUIManager, HomeMemberPanel, HomeShopPanel, HomeWarePanel, HomeEquipPanel |
| GameUI | UpgradeManager/Panel/Card, GameOverPanel/ResultPanel, GameClearPanel, DebugPanel |
| VFX | FXManager, HitEffect/Flash/Shake, GlobalLightDimmer, EnemyFX, SpriteYSort, SlimeBobAnimation |
| Audio | SoundManager, SoundKey, UIButtonSoundAutoAttach |

---

## Architecture

### Singleton Pattern
`MonoBehaviourSingleton<T>` ([Assets/Scripts/Extension/MonoBehaviourSingleton.cs](Assets/Scripts/Extension/MonoBehaviourSingleton.cs)) → 접근은 `ClassName.Shared`. `PlayerController` / `WeaponManager`는 수동 `static Instance` 사용.

씬 재로드 시 초기화가 필요한 싱글톤: `DontDestroy => false` 오버라이드.

---

## Core Game Loop

1. **GameManager** (`Core/`) — wave 타이머, game-over, UI 슬라이더. 루프: wave start → 타이머 카운트다운 → `WaveClearRoutine` → 증강 패널 → 업그레이드 패널 → 다음 wave.
2. **EnemySpawner** (`Enemy/`) — wave당 적 스폰; GameManager가 정지/재개.
3. **PlayerController** (`Player/`) — VirtualJoystick 이동, HP, 전투 스탯. 사망 시 `GameManager.TriggerGameOver()`.
4. **SceneLoadManager** (`Core/`) — `SceneLoadManager.LoadScene("stage@scene")` 로 로딩 오버레이 전환. `loading@scene`을 Additive 로드 → Single 모드 타겟씬 로드 → `GameManager.NotifyStageReady()` 대기 → `SceneLoadingCanvas.ThrowOut()` 연출.

### Wave 타이머 규칙
일반 웨이브: 1~9=20s, 11~19=30s, 21~29=35s. 보스 웨이브: `bossWaveTimeLimit=90s`.

---

## Player System (`Player/`)

### PlayerStatManager
14개 전투 스탯의 중심 허브. `PlayerStats` (골드/XP/레벨)와 별개.

- **`StatType`** enum — `ATK, DEF, MaxHP, BleedPower, HPRegen, MoveSpeed, MagnetRadius, AtkSpeed, Evasion, CritChance, CritDmg, StructureCooldown, Knockback, HealOnKill`
- `AddPctMod(stat, pct)` — % 보너스 누적. `SetEquipFlat(stat, value)` — 장비 플랫 보너스.
- 최종값 공식: `(base + equipFlat) × (1 + totalPct / 100)`
- 수정마다 `PlayerController.ApplyStatSheet(this)` + `StatPanelUI.Shared?.Refresh()` 호출.

**배치 모드 (여러 스탯 한번에 변경 시 필수):**
```csharp
sm.BeginBatch();
sm.SetBase(StatType.MaxHP, hp);
sm.AddPctMod(StatType.ATK, pct);
sm.EndBatch();  // 이 시점에 ApplyStatSheet 1회 실행
```
`GameManager.Start()`의 캐릭터 스탯 + 기물 시너지 적용이 이 패턴을 따름.

### PlayerController
- `ApplyStatSheet(PlayerStatManager)` — 자동 호출됨. `attack`, `defense`, `moveSpeed`, `dodge`, crit, `_hpRegenRate`, `_knockbackPower`, `_magnetRadius` 갱신.
- 방어 공식: `damage × (1 - defense/100)`. DEF=10 → 10% 감소.
- `SetSynergyBonus(atk, def, speed, maxHp, bleed, crit)` — `SynergyEffectManager`에서 호출.
- 사망 시 `StopAllCoroutines()` + `Instance = null` (씬 재로드 stale 참조 방지).

### PlayerStats
골드/XP/레벨 관리 (`DontDestroy => false`). `AddGold` / `AddExp` → `SyncUI()` → GameManager 업데이트.
- 레벨 공식: `expToNext = baseExpPerLevel + (level-1) × expGrowthPerLevel`. 최대 레벨 20.

### StatPanelUI / StatFieldUI
`StatPanelUI` (`DontDestroy => false`) — Awake에 모든 자식 `StatFieldUI` 수집, `Show()`/`Refresh()` 시 `field.Refresh(sm)` 호출.
`StatFieldUI` — Inspector에서 `statType` 설정; 자식 `stat_value` (TextMeshProUGUI) 자동 탐색.

---

## Character System (`Character/`)

### PlayerCharacterManager
`DontDestroy => true`, 홈→게임 씬 유지. `PlayerPrefs`에 장착 캐릭터 ID + 증강 티어 저장.

`ApplyCharacterStats()` — 게임 씬 시작 시 1회 호출 (배치 모드 내에서). 등급 기반 기초 스탯 (C=100/10/10, B=150/20/20, A=200/30/30, S=250/50/50, SS=300/70/70 HP/DEF/ATK) + 누적 티어 보너스 적용. 캐릭터 스프라이트: `Resources/Character/{캐릭ID.ToLower()}`.

`CharacterSkillManager.Initialize(skillId)` 호출 → 캐릭터별 스킬 로드.

### CharacterSkillBase / CharacterSkillManager
캐릭터 고유 스킬 시스템.

- **`CharacterSkillBase`** — abstract. `SkillId`, `Activate()`, `Deactivate()`, `duration`, `pointsRequired`.
- **`CharacterSkillManager`** (`DontDestroy => false`) — 적 처치 시 `AddPoint(1)` 호출. 게이지 충전 시 자동 발동 (`OnGaugeFull` → `TryActivate`). 프리팹: `Resources/Skills/{skillId.ToLower()}.prefab`.
- 구현체: `SkillBerserker` (공격속도 +%), `SkillNormal` (공격속도 ×1.5).

**호출 경로:** `EnemyController.Die()` → `CharacterSkillManager.Shared?.AddPoint(1)`

---

## Enemy System (`Enemy/`)

### IEnemyHost 인터페이스
`EnemyController`와 `BossController` 양쪽이 구현하는 공통 적 호스트 API.
`SelfTransform`, `Rb`, `EnemyAnimator`, `PlayerTransform`, `MoveSpeed`, `MoveInDirection`, `FlipToward`, `MoveTowardPlayer` 노출.
`EnemyCombatRoutines` 등 공유 행동 로직이 이 인터페이스를 통해 호스트에 접근합니다.

### EnemyController
- `static List<EnemyController> All` — 오브젝트 풀 패턴. `WakeFromPool()`에서 `All.Add`, `SleepToPool()`에서 스왑백 제거.
- `OnDeath` 이벤트 — `BossController`, `SynergyEffectManager` 등에서 구독.
- HP/데미지 성장: 스테이지 델타와 웨이브 내 델타를 분리 계산 (`EnemySpawner`, `SpawnMarker` 참조).
- `IEnemyHost` 구현체. 스폰 연출: `EnemyFX.SpawnBounce()` (DOTween).

### EnemyBehaviourBase
추상 기반. 오버라이드: `OnAttackUpdate()` (Update), `OnMoveFixedUpdate()` (FixedUpdate), `ApplyWaveScaling(float)`.

구현체: `MeleeBehaviour`, `RangedBehaviour`, `MagicBehaviour`, `HealerBehaviour`, `TankBehaviour`, `ChargeBehaviour`, `SlamBehaviour`, `HordeBehaviour`, `DashComboBehaviour`, `TeleportBehaviour`.

### EnemyCombatRoutines (공유 행동 라이브러리)
Boss · NonBoss 양쪽에서 꺼내 쓰는 정적 전투 루틴 모음. **새 공통 행동은 이 파일에 추가합니다.**

| 항목 | 설명 |
|------|------|
| `TeleportConfig` struct | 텔레포트 파라미터 묶음 |
| `TeleportSequence(host, renderers, cfg, digDone, spawnDone)` | PreDig → FadeOut → 이동 → FadeIn 전체 시퀀스 |
| `WaitForAnimEnd(condition, timeout)` | 애니메이션 이벤트 대기 (타임아웃 안전장치 포함) |
| `PickSpawnPosition(host, minR, maxR)` | 플레이어 기준 랜덤 스폰 위치 |
| `WarningLine.Spawn/UpdateLength/Destroy` | Charge 류 경고 LineRenderer 헬퍼 |

사용 예:
```csharp
yield return StartCoroutine(EnemyCombatRoutines.TeleportSequence(
    Owner, _renderers, cfg, () => _digAnimEnded, () => _spawnAnimEnded));
```

### ChapterRosterSO
ScriptableObject (`Assets/Data/`). 챕터별 일반 적 스폰 항목(가중치/minWave/maxWave), 보스 프리팹, `WaveSpawnRule` 목록. **`MuAge/Enemy Preset Editor`** 창으로 편집.

---

## Boss System (`Boss/`)

`BossController` / `BossBehaviour` — 적 패턴과 동일한 구조. `IEnemyHost` 구현체. 입장 낙하 squash/stretch: `EnemyFX.ScaleY()` (DOTween). 보스 스킬은 `BossSkillBase`를 상속:

| 스킬 | 클래스 |
|------|--------|
| 슬램 | `BossSlamSkill` |
| 돌진 | `BossChargeSkill` |
| 대시 콤보 | `BossDashComboSkill` |
| 연속 베기 | `BossComboSlashSkill` |
| 일제 사격 | `BossVolleySkill` |
| 고속 추격 | `BossFastChaseSkill` |
| 투사체 사이클 | `BossProjectileCycleSkill` (직선연사→추격→부채꼴→추격 반복) |

**BossIntroSequence 타이밍**: `CurrentBossEnterPoint`는 `WaveEnvironmentSwitcher.SetBoss()` 이후에만 유효.

보스 웨이브: `GameManager`가 `DB_웨이브` (`f_보스`)로 확인 → `StartBossWave()` → `WaveEnvironmentSwitcher` → 처치 후 `OnBossDefeated()` → `WaveClearRoutine` 재개.

---

## Weapon System (`Weapon/`)

- **WeaponData** — `DB_무기`에서 로드된 런타임 데이터.
- **PlayerInventory** (singleton) — 인벤토리(bag) + 장착 슬롯(최대 6). `OnChanged` 이벤트.
- **WeaponManager** — `PlayerInventory.OnChanged` 구독, `weaponAnchors` 하위에 무기 프리팹 인스턴스화.
- **WeaponPoint** — 무기 프리팹에 부착된 공격 로직.
  - 보스 감지: `TryGetComponent` → `GetComponentInParent` (자식 콜라이더 대응).
  - `MultiplyAttackSpeed(mult)` / `SetStatSpeedPct(pct)` — 캐릭터 스킬에서 속도 조작.

### Wave-Clear Screens (순서)
1. **ImprovManager/ImprovPanel** (`Augment/`) — `DB_증강` 가중치 랜덤 3개. 스탯형 → `PlayerStatManager.AddPctMod()`. AUG_HEAL → `MaxHP × f_주수치%` 즉시 회복.
2. **UpgradeManager/UpgradePanel** (`GameUI/`) — `DB_무기` 무기 카드 4장. `statPanelButton` → `StatPanelUI.Shared?.Show()`.

**증강 % 규칙:** `f_주수치`=10 → +10% ATK. `AUG_SKL` → `-f_주수치%` StructureCooldown.

### Weapon Synergy (`Weapon/`)
- **WeaponSynergyTag** (enum) + `DB_시너지` 챕터별 행 (f_SY1~4, f_S1~4).
- **SynergyHelper** — `CountEquippedTag(tag)`, `TagToID(tag)`. 티어: 1개=동, 3개=은, 5개=금.
- **SynergyEffectManager** (singleton, `DontDestroy => false`) — `PlayerInventory.OnChanged` 구독 → `RecalculateSynergies()`. `PlayerController.SetSynergyBonus()` + `PlayerStats.SetSynergyBonus()` 호출.
- `SynergyStat` enum: `Attack, Defense, MoveSpeed, MaxHP, GoldBonus, ExpBonus, BleedBonus, CritBonus`.

**태그 → 스탯 기본 매핑:** 야수=ATK, 채집=ExpBonus, 사냥꾼=BleedBonus, 지혜=CritBonus.

---

## Structure / Ware System (`Structure/`)

### 인게임 기물 (StructureInventory)
`DontDestroy => false`, 최대 4슬롯. 같은 ID 스태킹.
- `Push(DB_기물)` — 오브 픽업 시. `UseAt(slot, pos)` — 배치.
- 스폰: `Resources/Ware/{Chapter}/{id.lower()}_s.prefab`.

### 아웃게임 기물 컬렉션 (StructureWareManager)
`DontDestroy => true`, `PlayerPrefs` 저장.
- 강화: 카드 수집으로 레벨업 (최대 Lv.20). Lv.1-5: 2장/레벨, 6-10: 3장, 11-15: 4장, 16-19: 5장.
- 수치 공식: `f_주수치 × (1 + (level-1) × 0.15)`. Lv.20 = ×3.85.
- 합성: 같은 시대 카드 3장→다음시대 1개, 5장→2개, 10장→5개. C7은 같은 시대 폴백.
- 프리셋: 4슬롯. 인게임 시작 시 `StructureInventory`에 로드.
- `EraOf(id)` — `"C1_W0" → "C1"`. `EraName("C1")` → `"구석기"`.
- 아이콘: `Resources/Ware/{chapter}/{id.ToLower()}_icon`.

### 기물 가챠 (StructureGacha)
`WareBoxType`: `Normal` (SKL_NORMAL), `Epic` (SKL_EPIC). 챕터 1~현재 풀. Epic 뽑기: 5% 확률 다음 챕터 기물.

### 기물 시너지 (WareSynergySystem + StructureSynergyFlags)
`WareSynergySystem.ApplySynergies()` — 게임 씬 시작 1회 호출. 프리셋 태그+시대 시너지 적용.

**역할 태그 시너지 (3개 이상):**
| 태그 | 효과 |
|------|------|
| CC | `StructureSynergyFlags.CCSynergyActive = true` → 15% 확률 보스 그로기 |
| 버스트 | ATK +25% |
| 지속 | HPRegen +5% |
| 버프 | ATK +20% |
| 둔화 | `StructureSynergyFlags.SlowSynergyBonus = 0.30f` |

**시대 시너지:** 동일 시대 2개=쿨타임-15%, 4개=쿨타임-30%+수치+20%, 2시대 혼합=HPRegen+3%, 3시대=쿨타임-20%, 4시대=ATK+50%.

`StructureSynergyFlags` — static 플래그. `Reset()`은 반드시 씬 시작 전 호출.

### 기물 스킬 (`Structure/`)
`StructureSkillBase` — abstract. `mainValue`, `subValue`, `radius`, `tickInterval`. `Activate()` → `StructureController.Start()`에서 호출.

- **`isInstallation` bool** — `false`면 `StructureController`가 `Activate()` 후 모든 SpriteRenderer 비활성화.
- **`StructureController.Duration`** — public. `MeteorSkill` 등에서 interval 계산에 사용.
- **`OnDestroy()` 규칙** — 전역 상태(Light2D 등) 수정 시 `OnDestroy()`에서 복구 필수.

| 스킬 | 클래스 |
|------|--------|
| 메테오 | `MeteorSkill` — 뷰포트 내 랜덤 낙하, GlobalLight 어둡게, OnDestroy에서 즉시 복구 |
| 번개 | `ThunderSkill` |
| 얼음 | `IceSkill` — `FreezeEnemy()` 정적 코루틴으로 외부 재사용 가능 |
| 돌벼락 | `StoneThunderSkill` |
| 둔화 | `SlowSkill` — `StructureSynergyFlags.SlowSynergyBonus` 배율 적용 |
| 돌창토템 | `StoneTotemSkill` — 방사형 투사체, `OnDestroy()`에서 미비행 투사체 전부 제거 |
| 화염 확산 | `FireSpreadSkill` |
| 치유 구역 | `HealZoneSkill` |
| 방어벽 | `FenceSkill` |
| 피해 구역 | `DamageZoneSkill` |
| 지속 피해 | `DoTSkill` |
| 방사형 발사 | `RadialShotSkill` |
| 마비 | `ParalyzeSkill` |
| 박격포탄 | `MortarShell` |

**기물 드롭:** `ItemDropManager.SpawnStructureOrb` → `DB_기물`을 `f_기물ID.StartsWith($"C{chapter}_")`로 필터.
**기물 아이콘 프리팹:** `Resources/Ware/{Chapter}/{id.lower()}.prefab`

**시대별 역할 분담:**
| 시대 | 역할 |
|------|------|
| C1 구석기 | CC (빙결·둔화) |
| C2 중석기 | 광역 버스트 |
| C3 신석기 | 지속 피해·회복 |
| C4 중세 | 방어·막기 |
| C5 근대 | 광역 둔화·함정 |
| C6 현대 | 버프·생산 |
| C7 우주 | 고급 디버프·차원 |

---

## Equipment System (`Equipment/`)

`DontDestroy => true`, 홈→게임 씬 유지.

- **`EquipmentData`** — `DB_장비` 런타임 복사본. `StatIdToType(statId)` 매핑. 카테고리: `RING/NECK/FOOT/TOP/CLOAK/HEAD`. 아이콘: `Resources/Equip/{CivEng}/{Category}/{equipId.lower()}`.
- **`EquipmentInventory`** — 6개 고정 슬롯(카테고리 기반) + 무제한 bag. `EquipFromBag(data)` → `RecalculateEquipStats()` → `PlayerStatManager.SetEquipFlat()`.
- **`EquipmentGacha`** — `Draw(EquipBoxType)` / `DrawMulti`. 박스: `Normal`(EQ_NORMAL), `Rare`(EQ_RARE), `Epic`(EQ_EPIC). Epic 2단계 pity (`_pityCount[3][2]`).

**DB 스탯 ID → StatType:**
`STAT_ATK→ATK`, `STAT_DEF→DEF`, `STAT_HP→MaxHP`, `STAT_CRI_DMG→CritDmg`, `STAT_CRI_RATE→CritChance`, `STAT_EVA_RATE→Evasion`, `STAT_SPD_MOV→MoveSpeed`, `STAT_SPD_ATK→AtkSpeed`, `STAT_SKL_CD→StructureCooldown`, `STAT_DOT_DMG→BleedPower`

---

## Home Scene UI (`HomeUI/`)

씬 파일 (`_Recovery/home@scene.unity`)은 **절대 직접 편집 금지**. Editor 도구 사용.

- **`Tools/Build Home UI`** — HomeUICanvas, 5개 패널(상점·대원·전투·장비·기물), TabBar 생성. **HomeUIManager** 부착.
- **`Tools/Build Equipment Panel`** — Panel_장비에 캐릭터 뷰 + 6개 장비 슬롯 + 내부 탭바 + 인벤 그리드 생성.
- **HomeUIManager** — `panels[]` + `tabButtons[]`, `ShowTab(index)`. `StartGame()` → `stage@scene` 로드.
- **HomeMemberPanel** — 캐릭터 목록. `CharacterSlotView` 각 슬롯. `CharacterGradeColor`로 등급 배경색.
- **HomeShopPanel** — 장비 가챠/IAP. `EquipmentGacha.Draw/DrawMulti`, `GachaDrawButton`, `GachaResultPopup`.
- **HomeEquipPanel** — `EquipmentInventory.Shared`에서 장비 슬롯 시각화. `equipSlots[0~5]` Inspector 연결 필수.
- **HomeWarePanel** — 기물 패널. 프리셋 탭(4슬롯) ↔ 합성 탭(10 재료슬롯) 전환. `WareSynergySystem` 시너지 미리보기. `StructureWareManager.TrySynthesize()` 호출.
- **StageSelectPanel** — 스테이지 카드 캐러셀. `OnStartClicked()` → `PlayerPrefs.SetString("SelectedChapterID", ...)` 저장 후 `SceneLoadManager.LoadScene("stage@scene")`.
- **HomeUserInfoBar** — 상단 유저 정보 바 (레벨, EXP, 재화).

---

## In-Game UI (`GameUI/`)

- **UpgradeManager/UpgradePanel/UpgradeCard** — 무기 상점. `statPanelButton` → `StatPanelUI.Shared?.Show()`.
- **GameOverPanel** — 부활 카운트다운. 종료 시 `GameOverResultPanel.Show(wave)`.
- **GameClearPanel** — 최종 보스 처치 후. 획득 EXP/골드, 아웃게임 EXP 슬라이더 표시. 팝인 애니메이션.
- **StatPanelUI / StatFieldUI** — 전투 스탯 팝업.
- **DebugPanel** — `GameManager.DebugSkipWave()`, `DebugJumpToBossWave()`. Inspector 연결 메서드는 `public` 필수.

---

## VFX (`VFX/`)

- **FXManager** — 풀링. `_prefabScales` 딕셔너리로 프리팹별 원본 스케일 캐시.
- **GlobalLightDimmer** — static. 레퍼런스 카운트 방식으로 여러 스킬이 동시에 조명 어둡게 할 때 충돌 없이 관리. `Register()` / `Release()` / `SetIntensity()`.
  - 사용법: 스킬 시작 시 `GlobalLightDimmer.Register()`, 종료/파괴 시 `GlobalLightDimmer.Release()`.
- **EnemyFX** — 적·보스 공통 DOTween 이펙트 헬퍼 (정적). `using DG.Tweening;` 필요.
  - `FadeSprites(srs, to, duration)` → `Sequence` — SpriteRenderer 배열 알파 페이드.
  - `SetAlpha(srs, alpha)` — 알파 즉시 설정 (코루틴 완료 후 보정용).
  - `SpawnBounce(transform, spawnScale)` → `Sequence` — 스폰 팝인 바운스 (0.48s).
  - `ScaleY(transform, originalScale, targetYMult, duration)` → `Tween` — Y축만 squash/stretch.
  - 반환값이 `Tween`/`Sequence`이므로 `yield return tween.WaitForCompletion()` 또는 fire-and-forget 둘 다 가능.
- **SpriteYSort** — Y좌표 기반 sortingOrder 자동 정렬. 적·플레이어·보스 프리팹 루트에 부착.
- **SlimeBobAnimation** — VisualRoot 스케일 조작 슬라임 바운스. `fixBottom=true` 시 하단 고정. Rigidbody 없는 VisualRoot에서만 Y 보정 (`_rb == null` 가드). 풀 재사용 시 `ResetOrigin()` 호출 필수.

---

## Data / Database

**BansheeGz** (BgDatabase). 생성 클래스: `Assets/BansheeGz/BgData.cs`, 네임스페이스 `Mewage.DB`. DB 클래스 사용 스크립트에 `using Mewage.DB;` 필수.

| 테이블 | 주요 필드 | 용도 |
|--------|----------|------|
| `DB_장비` | `f_장비ID`, `f_카테고리`, `f_등급`, `f_스탯ID`, `f_수치`, `f_문명` | 장비 아이템 |
| `DB_기물` | `f_기물ID` (형식: `C1_W0`), `f_등급`, `f_주수치`, `f_역할` | 기물 카드 |
| `DB_확률` | `f_가챠타입`, `f_G일반/레어/에픽/전설`, `f_필중횟수(List<int>)` | 뽑기 확률 |
| `DB_상점` | `f_상품ID`, `f_가격`, `f_수량` | 상점 상품 |
| `DB_무기` | `f_무기ID`, `f_등급`, `f_속성ID/내용` | 무기 |
| `DB_증강` | `f_증강ID`, `f_주수치`, `f_보조수치` | 증강 |
| `DB_캐릭터` | `f_캐릭ID`, `f_등급`, `f_속성1ID`, `f_S1`, `f_스킬ID` | 캐릭터 |
| `DB_시너지` | `f_챕터`, `f_SY1~4`, `f_S1~4(List<int>)` | 무기 시너지 등급별 수치 |
| `DB_웨이브` | `f_보스` | 보스 웨이브 번호 |

**무기 상점 특정 등급 안 나올 때**: `DB_확률` 해당 타입 행의 등급 확률 필드가 0인지 확인. BansheeGz 에디터에서 직접 수정.

---

## Memory Management Conventions

### 이벤트 구독 규칙
- `+=` 구독 → 반드시 대칭 `-=` 해제.
- `Awake/Start` 구독 → `OnDestroy` 해제.
- `OnEnable` 구독 → `OnDisable` 해제 (패널·팝업류).
- static 이벤트 / 싱글톤 이벤트 (`PlayerInventory.OnChanged`, `BossController.OnEnraged`) 특히 주의.

### static 컬렉션 규칙
- `EnemyController.All` 패턴: `WakeFromPool()` → `All.Add`, `SleepToPool()` → 스왑백 제거, `OnDestroy` → 안전망.

### 코루틴 규칙
- 무한 루프 코루틴 → `OnDestroy()`에서 `StopAllCoroutines()`.
- `static Instance` 싱글톤 → `OnDestroy()`에서 `Instance = null`.

### 오브젝트 풀 규칙
- `Queue<T> _pool` + `List<T> _active`. 꺼낼 때 `SetActive(true)`, 반환 시 `SetActive(false)` + Enqueue.
- `List.RemoveAt` → 순서 무관 시 스왑백으로 O(1) 처리.

### 배치 스탯 변경
여러 스탯 한번에 변경 시 `BeginBatch()` / `EndBatch()` 필수 (호출마다 `ApplyStatSheet` 실행 방지).

### Resources.Load 규칙
- 동일 경로 반복 로드 → 로컬 필드에 캐시 (`_cachedSprite` 등).
- `DontDestroy => true` 싱글톤은 씬 전환 후에도 캐시 유지됨.

---

## Key Conventions

- 싱글톤: `.Shared`. PlayerController/WeaponManager: `.Instance`.
- DB 클래스: `using Mewage.DB;`.
- 씬 네이밍: `name@scene.unity`.
- 씬 전환 (로딩 오버레이): `SceneLoadManager.LoadScene("stage@scene")`.
- `Time.timeScale = 0f` 패널 일시정지 → 닫을 때 `1f` 복구.
- Debug 헬퍼: `this.DLog()` / `this.DError()` ([Assets/Scripts/Extension/StringExtension.cs](Assets/Scripts/Extension/StringExtension.cs)).
- Inspector Button OnClick 타겟 메서드 → `public` 필수.
- 버튼 SFX: `IPointerClickHandler` 금지 → `UIButtonSoundAutoAttach`의 `onClick.AddListener` 사용.
- **버튼 리스너 등록 규칙**: Inspector OnClick 연결 금지. 버튼 레퍼런스는 최상위 부모 스크립트에서 `[SerializeField]`로 선언하고, `Awake()`에서 `onClick.AddListener()`로 연결. 자식/하위 컴포넌트에 버튼 레퍼런스를 분산하지 않음.
- **Global Light2D 어둡게 패턴**: `GlobalLightDimmer.Register()` / `Release()` 사용. 직접 수정 금지.
- **에디터에서 비활성 GameObject 탐색**: `Resources.FindObjectsOfTypeAll<GameObject>()` + `go.scene` 필터 사용 (`GameObject.Find()` 비활성 스킵).
- **SpriteRenderer**: 플레이어 루트가 아닌 자식 오브젝트에 있음 → `GetComponentInChildren<SpriteRenderer>()` 사용.

---

## 진행 사항 (변경 이력)

### 폴더 구조 재편 (2026-04-14)
`Assets/Scripts/Game/` 내 ~170개 파일을 14개 기능 서브폴더로 분류.
Core / World / Player / Character / Enemy / Boss / Weapon / Structure / Equipment / Augment / HomeUI / GameUI / VFX / Audio.

### 캐릭터 스킬 시스템 추가
`CharacterSkillBase` / `CharacterSkillManager`. 적 처치 → 게이지 충전 → 자동 발동. 프리팹: `Resources/Skills/{skillId.ToLower()}`. 구현체: `SkillBerserker` (ATK 속도 +%), `SkillNormal` (속도 ×1.5).

### 기물 아웃게임 시스템 추가
`StructureWareManager` (DontDestroy=true) — 수집/강화/합성/프리셋. Lv.1~20, 수치 ×3.85.
`StructureGacha` — Normal/Epic 뽑기.
`HomeWarePanel` — 프리셋 탭 ↔ 합성 탭 전환 UI.
`WareSynergySystem` — 역할 태그 + 시대 시너지 인게임 적용 (게임 씬 시작 시 1회).
`StructureSynergyFlags` — static 플래그 저장소 (CCSynergyActive, SlowSynergyBonus).

### 보스 스킬 추가
`BossFastChaseSkill`, `BossProjectileCycleSkill` (직선연사→추격→부채꼴→추격 사이클).

### 게임 결과 UI
`GameClearPanel` — 최종 보스 처치 시 팝인 연출 + 획득 수치 표시.
`GameOverResultPanel` — 부활 카운트다운 종료 후 도달 웨이브 표시.

### VFX 개선
`GlobalLightDimmer` — 레퍼런스 카운트 기반 Global Light2D 관리. 여러 스킬 동시 사용 시 충돌 없음.
`FXManager` — `_prefabScales` 딕셔너리로 프리팹별 원본 스케일 캐시.

### 플레이어
- SpriteRenderer → 자식 오브젝트 이동. 참조 시 `GetComponentInChildren<SpriteRenderer>()` 사용.
- 방어 공식: `damage × (1 - defense/100)`.

### WeaponPoint
- 보스 사정거리 무시 버그 수정 (`preferBoss` + `bossInRange` 체크).
- 부메랑 타겟 소멸/피해 0/반사 안됨 버그 수정.
- 보스/근접 감지: `TryGetComponent` → `GetComponentInParent`.

### UI
- 드래그 고스트: `SetActive(false)` 후 `Destroy` (프레임 말 아이콘 가림 방지).
- 별 표기: `TextMeshProUGUI starText` → `Image starImage`. `Resources/UI/star_{n}` 로드 + `SetNativeSize()`.
- 탭/패널 전환 시 원본 색상·스케일·위치 저장 후 복구.

### 게임 매니저 / 스폰
- 웨이브 종료 후 플레이어 맵 중앙 이동 (`TeleportPlayerToMapCenter()`).
- 스테이지·웨이브 HP/데미지 성장 공식 분리 (`EnemyController`, `EnemySpawner`, `SpawnMarker`).
- 스테이지 선택 저장: `StageSelectPanel.OnStartClicked()` → `SelectedChapterID` 정상 저장.
- `InitAndStartRoutine()`: 초기화 완료 후 `NotifyStageReady()` → 로딩 씬 내려감 → 플레이어 낙하 입장 연출 병행.

### 디버그
`WeaponDebugInspector` — 런타임 무기 장착용 Odin Inspector MonoBehaviour.

### 적·보스 공통 행동 라이브러리 + DOTween 전환 (2026-05-05)
**신규 파일:**
- `Enemy/IEnemyHost.cs` — EnemyController·BossController 공통 인터페이스.
- `Enemy/EnemyCombatRoutines.cs` — 공유 행동 라이브러리 (TeleportSequence, WaitForAnimEnd, PickSpawnPosition, WarningLine 헬퍼). 새 공통 행동은 이 파일에 추가.
- `VFX/EnemyFX.cs` — DOTween 이펙트 헬퍼 (SpawnBounce, FadeSprites, SetAlpha, ScaleY).

**DOTween 전환:**
- `EnemyController.BounceIn()` → `EnemyFX.SpawnBounce()` (24줄 → 1줄).
- `BossController.EntryDropRoutine()` squash/stretch → `EnemyFX.ScaleY()`. `ScaleYTo()` 제거.
- `TeleportBehaviour` / `BossTeleportSkill` 중복 `FadeRoutine` → `EnemyFX.FadeSprites()`.

**리팩터링:**
- `TeleportBehaviour` State 머신 제거 → `_inSequence` 플래그 + `EnemyCombatRoutines.TeleportSequence` 위임 (182줄 → 98줄).
- `BossTeleportSkill` 동일 중복 로직 제거 → 공유 루틴 위임 (161줄 → 90줄).

**VFX 추가:**
- `SpriteYSort` — Y좌표 기반 SpriteRenderer sortingOrder 자동 정렬.
- `SlimeBobAnimation` — VisualRoot 슬라임 바운스. Rigidbody 충돌 방지(`_rb == null`), 풀 재사용 시 `ResetOrigin()` 필수.
