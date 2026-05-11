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
