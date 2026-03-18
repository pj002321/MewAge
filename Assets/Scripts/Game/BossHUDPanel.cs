using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 보스 웨이브 동안 화면에 표시되는 HUD 패널.
/// Inspector: panel(루트 GameObject), bossImage, hpSlider, bossNameText(선택)
/// GameManager.StartBossWave() → Show(BossController)
/// GameManager.OnBossDefeated() → Hide()
/// </summary>
public class BossHUDPanel : MonoBehaviourSingleton<BossHUDPanel>
{
    [Header("UI 연결")]
    [SerializeField] private GameObject      panel;
    [SerializeField] private Image           bossImage;
    [SerializeField] private Slider          hpSlider;
    [SerializeField] private TextMeshProUGUI bossNameText;

    private BossController trackedBoss;

    protected override bool DontDestroy => false;

    protected override void Awake()
    {
        base.Awake();   // 싱글턴 Shared 등록
        if (panel) panel.SetActive(false);
    }

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>보스 웨이브 시작 시 GameManager or BossController에서 호출합니다.</summary>
    public void Show(BossController boss)
    {
        trackedBoss = boss;

        if (bossImage != null && boss != null)
        {
            var sr = boss.GetComponent<SpriteRenderer>();
            if (sr != null) bossImage.sprite = sr.sprite;
        }

        if (bossNameText != null)
            bossNameText.text = trackedBoss != null ? trackedBoss.BossName : "BOSS";

        if (hpSlider) hpSlider.value = 1f;
        if (panel)    panel.SetActive(true);
    }

    /// <summary>보스 사망 시 GameManager에서 호출합니다.</summary>
    public void Hide()
    {
        trackedBoss = null;
        if (panel) panel.SetActive(false);
    }

    // ── 매 프레임 HP 동기화 ──────────────────────────────────────
    private void Update()
    {
        if (trackedBoss == null) return;
        if (hpSlider) hpSlider.value = trackedBoss.HPRatio;
    }
}
