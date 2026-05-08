1. 2025.5.07
(1) 재현 : @SynergyInfoPanel, @EraWeaponInfoPanel 이 열렸을 때 나오는 출현확률 동기화가 안됨. 
(2) 해결 : UpgradeManager에 DrawProbs 구조체와 GetCurrentProbs() 공개 메서드 추가. EraWeaponInfoPopup.Open() 및 SynergyInfoPanel.Show() 호출 시 RefreshProbTexts()로 현재 플레이어 레벨 기반 DrawProfile 확률을 동기화. 두 패널에 등급별 확률 TextMeshProUGUI 필드(선택) 추가 — Inspector에서 연결하면 패널 열릴 때마다 실시간 확률로 갱신됨.
