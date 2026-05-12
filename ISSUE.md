6. 2026.5.12 ✅ 해결
버그 수정 (9건)

(1) 장착 무기 클릭 후 팝업 닫아도 슬롯 크기가 커진 상태로 유지되는 현상
- 원인: WeaponInfoPanel이 열릴 때 EquipSlot의 scale을 키우고, 닫힐 때 복구하는 콜백이 등록되지 않음.
- 해결: WeaponInfoPanel.Open()에서 슬롯 원본 scale 캐시 후 onClose 콜백에서 복구.

(2) 웨이브 시작 전 포메이션 존이 즉시 활성화되는 현상 (증강 패널보다 먼저 트리거)
- 원인: WaveEnvironmentSwitcher.SetWave() 내부에서 TriggerFormationsForWave()를 호출하여, 게임 초기화(SetStage → SetWave(1)) 시점에도 포메이션이 발동됨.
- 해결: SetWave()에서 포메이션 호출 제거. GameManager.DoStartWave()에서 SetWave() 이후 명시적으로 TriggerFormations(wave) 호출.

(3) 앱 일시정지(timeScale=0) 중 피격 시 HitFlash가 흰색으로 굳는 현상
- 원인: FlashRoutine의 WaitForSeconds가 timeScale의 영향을 받아 정지됨.
- 해결: WaitForSeconds → WaitForSecondsRealtime으로 교체.

(4) 업그레이드 패널의 EXP 게이지 수치가 실제와 다른 현상
- 원인: UpgradePanel.RefreshExpUI()가 baseExpPerLevel/expGrowthPerLevel 상수를 직접 하드코딩하여 계산, PlayerStats 내부 값과 불일치.
- 해결: PlayerStats에 ExpToNext 프로퍼티 추가. RefreshExpUI()에서 PlayerStats.Shared.ExpToNext를 참조.

(5) 씬 재로드 후 EnemyInvincible 디버그 플래그가 ON으로 유지되는 현상
- 원인: TestMonsterSpawner.EnemyInvincible이 static 필드로 씬 전환 후에도 초기화되지 않음.
- 해결: TestMonsterSpawner.ResetDebugFlags() 정적 메서드 추가. GameManager.InitAndStartRoutine() 초기화 구간에서 호출.

(6) 오브젝트 풀에서 재사용된 적에게 이전 상태 VFX(빙결·스턴·화염)가 잔존하는 현상
- 원인: EnemyController.WakeFromPool()에서 _freezeVfxInst / _stunVfxInst / _fireVfxInst를 Destroy하지 않고, 코루틴 레퍼런스도 null 처리하지 않음. 재사용된 적에게 VFX 오브젝트가 남아있고 ApplyBleed 등이 블로킹됨.
- 해결: WakeFromPool() 앞부분에서 세 VFX 인스턴스 Destroy + null, bleedCoroutine / _fireCoroutine / _slowCoroutine / knockbackCoroutine / _fireTintCoroutine null 처리.

(7) 패널 열림(timeScale=0) 중 무기가 공격 대기 상태에 고착되는 현상
- 원인: WeaponPoint의 MaxStateTime 타임아웃 타이머가 Time.deltaTime으로 증가하여, timeScale=0 구간에서 카운트가 멈춤.
- 해결: _stateTimer += Time.deltaTime → Time.unscaledDeltaTime.

(8) 플레이어 사망 후 부활 시 보스가 중복 스폰되는 현상
- 원인: 보스 마커 카운트다운 중 플레이어가 사망하면 마커가 자체 소멸(GameOver 체크)하고 BossController.Current가 null로 남음. 부활 후 ResumeSpawning 시 SpawnBoss()가 다시 실행되어 두 번째 마커 생성.
- 해결: EnemySpawner에 _pendingBossMarker 필드 추가. 마커 생성 시 저장, OnSpawnComplete 이벤트로 완료 시 해제. SpawnBoss() 진입부에서 _pendingBossMarker != null이면 중복 차단. StopSpawning()에서 필드 null 처리. BossSpawnMarker에 OnSpawnComplete 이벤트 추가.

(9) 보스 맵(MovementBounds 없음)에서 돌진 스킬 사용 시 보스가 맵 밖으로 이탈하는 현상
- 원인: BossChargeSkill의 경계 체크가 MovementBounds.Shared != null인 경우에만 작동. 보스 전용 배경에는 MovementBounds가 없어 경계 체크 전체가 무시됨.
- 해결: MovementBounds가 null일 때 SpawnZone.BoundsCenter/BoundsSize로 AABB 폴백 경계 계산 후 Clamp 처리.


5. 2026.5.11 ✅ 해결
버그 수정 (5건)

(1) 디버그 패널 무기 장착 후 공격 안 하는 현상
- 원인 A: DebugPanel에서 ForceAddToInventory + EquipFromInventory 두 번 호출 → SyncWeapons 2회 실행, 첫 번째 무기 인스턴스 즉시 파괴됨.
- 원인 B: WeaponPoint.Init()은 WeaponManager가 Instantiate 직후에 호출하지만, _legendarySkill/_mythicSkill 참조는 Start()에서 가져오므로 Init 시점에 null → LegendarySkill/MythicSkill 초기화 누락.
- 원인 C: Start()에서 _baseCooldown = attackCooldown 덮어쓰기 → ApplyStatSheet가 Init~Start 사이에 RecalcCooldown을 이미 실행했으면 감소된 값이 _baseCooldown에 저장되어 이후 쿨타임 2중 감소.
- 해결: WeaponPoint.Init() 첫 줄에서 GetComponentInChildren으로 _legendarySkill/_mythicSkill/_weaponSr 직접 캐싱 + _initialized=true 설정. Start()는 !_initialized일 때만 _baseCooldown 설정, _initialized이면 legendary/mythic Initialize+OnEquip 재호출. PlayerInventory.QuietEquip() 추가(OnChanged 1회). DebugPanel.OnWeaponDropdownChanged → QuietEquip 1회 호출로 변경.

(2) 플레이어 피격이 안 될 때가 있는 현상
- 원인: hitInvincibleDuration=0.3f로 인해 피격 후 0.3초간 다른 소스 피격 무시. 각 피해 소스(보스 접촉, 스파이크, 투사체)가 자체 쿨타임을 이미 관리하므로 플레이어 측 무적시간이 중복으로 작용.
- 해결: PlayerController.hitInvincibleDuration을 0으로 변경.

(3) BossPound 스킬로 플레이어가 띄워질 때 무기 공격이 멈추지 않는 현상
- 원인: WeaponPoint.UpdateIdle()이 InputLocked 상태를 확인하지 않음.
- 해결: UpdateIdle() 진입부에 InputLocked 체크 추가.

(4) 1레벨 플레이어에게 6레벨 확률이 표시되는 현상
- 원인: GetCurrentProfile()에서 drawProfiles[0]을 기본 폴백으로 사용하는데, drawProfiles가 minLevel 내림차순이면 [0]이 최고레벨 프로필임.
- 해결: 폴백을 "minLevel이 가장 낮은 프로필"로 변경. 플레이어 레벨을 충족하는 프로필 중 minLevel이 가장 높은 것을 선택하되, 충족하는 프로필이 없으면 minLevel 최솟값 프로필 반환.

(5) BOSS_SPIN 6개 복제본이 피해를 못 입히는 현상
- 원인: 궤도 공전 스킬 자체가 없었음 (BossOrbitSkill 미구현).
- 해결: BossOrbitSkill.cs 신규 생성. cloneCount개 오브젝트를 플레이어 중심으로 orbitRadius/orbitSpeed로 공전, 위치 기반 거리 판정으로 hitCooldown마다 데미지 적용. 보스 프리팹에 컴포넌트 추가 후 clonePrefab Inspector 연결 필요.

(6) 그로기 상태 스프라이트 빨간색으로 나오는 현상 (모바일)
- 원인: Custom/SpriteGrogyColor 셰이더를 material로 교체하여 그로기 비주얼을 구현했으나 일부 모바일 GPU에서 셰이더가 지원되지 않아 빨간색으로 렌더링됨.
- 해결: 셰이더/material 교체 로직 제거. sr.color = GrogyTint(회색) 직접 세팅으로 대체. _originalMaterials/_grogyMatInstance 필드 및 GetGrogyMaterial() 메서드 삭제.


1. 2025.5.07
(1) 재현 : @SynergyInfoPanel, @EraWeaponInfoPanel 이 열렸을 때 나오는 출현확률 동기화가 안됨. 
(2) 해결 : UpgradeManager에 DrawProbs 구조체와 GetCurrentProbs() 공개 메서드 추가. EraWeaponInfoPopup.Open() 및 SynergyInfoPanel.Show() 호출 시 RefreshProbTexts()로 현재 플레이어 레벨 기반 DrawProfile 확률을 동기화. 두 패널에 등급별 확률 TextMeshProUGUI 필드(선택) 추가 — Inspector에서 연결하면 패널 열릴 때마다 실시간 확률로 갱신됨.


3. 2025.5.11 ✅ 해결
보스 그로기 시스템 개선
- 그로기 상태일 경우 모든 공격과 이동이 중단 → MoveTowardPlayer/MoveInDirection에 IsGrogy 가드 추가
- Flash 피격 연출 때 회색으로 보스 SpriteRenderer 색상 유지, 해제 시 원래 색 복구 → _originalSrColors 캐시 + StopGrogyVisuals 복원
- 그로기 시작 시 sprite 파동 하이라이트 → GrogyWaveEffect 코루틴 (확장 페이드아웃 링) + GrogyEntryHighlight 흰→회색 번쩍
- 그로기 시작 시 플레이어 공격속도 8배 → ApplyGrogyAtkSpeed(mult)로 WeaponPoint.MultiplyAttackSpeed 적용


4. 2025.5.12
개선사항
- 디버그 초기화 버튼 : PlayerPrefs 모두 초기화, 챕터진행도 초기화.
- 출석패널 자동활성화 제거 : 오로지 버튼을 눌러서 나오게
- 부드러운 로비 스크롤 개선 필요 : @MainMenuPanel 드래그하여 스크롤 할 때 버벅임이 아예 없어야함.
- 스테이지 아이콘 숫자 개선(1-1,1-2,2-1,2-3->1,2,3,4)
- 로비,보스 BGM 1개로 통일 관련없거나 중복된 인스펙터 제거 
- 웨이브 진입 시 즉시 증강패널 활성화 
: 착지 후 증강 패널 활성화되며 선택 후 바로 시작
- 증강 스탯 뷰 터치동작 개선
기존 : 꾹 누르고 있을 때 활성화
개선 : 토글 형식

발견된 버그
- 호밍 발사체를 쓰는 보스가 무한 플립 현상과 플레이어 타겟팅 버그
-> x위치가 동일하고 y위치가 다른 플레이어가 보스와 마주했을 때, 보스의 무한 플립 현상이 생김. 그리고 호밍 투사체가 플레이어 위치까지 가지 못함. -> 보스가 항상 플레이어와 x 위치가 동떨어진 곳에서 발사를 해야되는지?

- 디버그 버튼으로 인한 동작과 실제 동작이 다른 것들이 너무나 많음.
1. 플레이어가 디버그 패널에서 무기를 선택해서 갈아끼우면 무기가 공격자체를 안하는 현상이 생김

2. 경고라인, 파동잔상,BossSummon의 스폰 프리팹 등이 디버그 버튼으로 인한 웨이브 전환 시 사라지지 않으며,보스를 죽이고도 남아있는 경우가 생김. -> 어떠한 경우에도 정리가 되어야함. 

3. 보스 타이머가 흐르지 않음 00:00으로 뜸

4. -보스가 그로기 상태일 때 스프라이트가 빨간색으로 나옴 회색으로 나와야함.-> 실제 모바일디바이스에서 셰이더가 지원이 안되는 것 같음

5. 보스가 살아있는 상태에서 웨이브 스킵 시, 보스가 계속 살아있음.

6. 보스웨이브로 이동하면 일반 몹들이 정리가 안된 상태로 남아있음.


- 플레이어의 피격이 안될 때가 있고될때가 있음. 투사체나 스파이크 프리팹에 닿을때마다 (존재하는 스파이크 프리팹 포함) 피격이 즉시 되어야함. 

- 보스가 스킬을 쓰는 간격이 너무 긺, 특히 그로기가 해제되자마자 바로 스킬을 안씀

- BossPound 스킬을 써서 플레이어가 띄워질때는 무기의 공격이 멈춰야함.

- 플레이어가 1레벨인데 확률이 이상함 1레벨의 확률이 안나옴. 1레벨인데 6레벨의 확률이 나오는 등.

- BOSS_SPIN에서 6개의 복제본이 플레이어 위치를 기준으로 빙글빙글 도는데 그 도는 객체가 피해를 못입힘

-BossGridSpike 스킬에서 스파이크 프리팹이 연결되어있음에도 프리팹이 안나옴.

- 웨이브 시작전 첫 입장때만 증강만 나오고 바로 시작해야하는데, 그 이후웨이브에서도 증강만 나옴.

- DebugPanel 초기화 버튼으로 데이터 초기화해도 프롤로그가 계속 봤던 상태로 유지되어있음. 

- 코드리뷰를 실시할 것. 모든 정리되는 구간을 특히 신경쓸 것. 
-> 코루틴 정리 , 적의 멈춤, 무기 공격의 미아 상태 등을 특히 신경 쓸 것.

