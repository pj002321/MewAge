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
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("그로기 UI")]
    [Tooltip("그로기 중에만 표시할 루트 오브젝트")]
    [SerializeField] private GameObject grogyPanel;

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
            boss.OnHPChanged  += HandleHPChanged;
            boss.OnGrogyStart += HandleGrogyStart;
            boss.OnGrogyEnd   += HandleGrogyEnd;
        }
    }

    /// <summary>보스 사망 시 GameManager에서 호출합니다.</summary>
    public void Hide()
    {
        if (trackedBoss != null)
        {
            trackedBoss.OnHPChanged  -= HandleHPChanged;
            trackedBoss.OnGrogyStart -= HandleGrogyStart;
            trackedBoss.OnGrogyEnd   -= HandleGrogyEnd;
        }
        trackedBoss = null;
        if (grogyPanel) grogyPanel.SetActive(false);
        if (panel) panel.SetActive(false);
    }

    private void HandleHPChanged(float current, float max)
    {
        if (hpSlider) hpSlider.value = max > 0f ? current / max : 0f;

        if (hpText)
        {
            hpText.text = $"{Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)}";
        }
    }

    private void HandleGrogyStart() { if (grogyPanel) grogyPanel.SetActive(true); }
    private void HandleGrogyEnd()   { if (grogyPanel) grogyPanel.SetActive(false); }

}
