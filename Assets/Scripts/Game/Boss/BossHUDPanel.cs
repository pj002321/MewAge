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
    [SerializeField] private Slider          skillGaugeSlider;
    [SerializeField] private TextMeshProUGUI bossNameText;
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("그로기 UI")]
    [Tooltip("그로기 중에만 표시할 루트 오브젝트")]
    [SerializeField] private GameObject grogyPanel;
    [Tooltip("그로기 잔여 시간 슬라이더 (1→0 감소)")]
    [SerializeField] private Slider     grogySlider;

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
        if (grogyPanel) grogyPanel.SetActive(false);
        if (panel) panel.SetActive(true);

        if (boss != null)
        {
            boss.OnGrogyStart += HandleGrogyStart;
            boss.OnGrogyEnd   += HandleGrogyEnd;
        }
    }

    /// <summary>보스 사망 시 GameManager에서 호출합니다.</summary>
    public void Hide()
    {
        if (trackedBoss != null)
        {
            trackedBoss.OnGrogyStart -= HandleGrogyStart;
            trackedBoss.OnGrogyEnd   -= HandleGrogyEnd;
        }
        trackedBoss = null;
        if (grogyPanel) grogyPanel.SetActive(false);
        if (panel) panel.SetActive(false);
    }

    private void HandleGrogyStart() { if (grogyPanel) grogyPanel.SetActive(true); }
    private void HandleGrogyEnd()   { if (grogyPanel) grogyPanel.SetActive(false); }

    // ── 매 프레임 HP / 그로기 동기화 ────────────────────────────
    private void Update()
    {
        if (trackedBoss == null) return;

        if (hpSlider) hpSlider.value = trackedBoss.HPRatio;

        if (skillGaugeSlider)
            skillGaugeSlider.value = trackedBoss.SkillGauge / trackedBoss.MaxSkillGauge;

        if (hpText)
        {
            int cur = Mathf.CeilToInt(trackedBoss.CurrentHPValue);
            int max = Mathf.CeilToInt(trackedBoss.MaxHPValue);
            hpText.text = $"{cur}/{max}";
        }

        if (grogySlider && trackedBoss.IsGrogy)
            grogySlider.value = trackedBoss.GrogyRemaining / trackedBoss.GrogyDuration;
    }
}
