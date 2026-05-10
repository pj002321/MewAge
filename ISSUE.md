1. 2025.5.07
(1) 재현 : @SynergyInfoPanel, @EraWeaponInfoPanel 이 열렸을 때 나오는 출현확률 동기화가 안됨. 
(2) 해결 : UpgradeManager에 DrawProbs 구조체와 GetCurrentProbs() 공개 메서드 추가. EraWeaponInfoPopup.Open() 및 SynergyInfoPanel.Show() 호출 시 RefreshProbTexts()로 현재 플레이어 레벨 기반 DrawProfile 확률을 동기화. 두 패널에 등급별 확률 TextMeshProUGUI 필드(선택) 추가 — Inspector에서 연결하면 패널 열릴 때마다 실시간 확률로 갱신됨.


3. 2025.5.11 ✅ 해결
보스 그로기 시스템 개선
- 그로기 상태일 경우 모든 공격과 이동이 중단 → MoveTowardPlayer/MoveInDirection에 IsGrogy 가드 추가
- Flash 피격 연출 때 회색으로 보스 SpriteRenderer 색상 유지, 해제 시 원래 색 복구 → _originalSrColors 캐시 + StopGrogyVisuals 복원
- 그로기 시작 시 sprite 파동 하이라이트 → GrogyWaveEffect 코루틴 (확장 페이드아웃 링) + GrogyEntryHighlight 흰→회색 번쩍
- 그로기 시작 시 플레이어 공격속도 8배 → ApplyGrogyAtkSpeed(mult)로 WeaponPoint.MultiplyAttackSpeed 적용

