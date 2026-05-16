using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 런 종료 결과 패널 — 클리어/게임오버 통합.
///
/// Inspector 연결:
///   waveText        — "WAVE 15"
///   bestWaveText    — "최고웨이브 : 20"
///   levelText       — 계정 레벨 "22"
///   expSlider       — EXP 게이지
///   expFractionText — "11038 / 15000"
///   rewardContents  — RewardSlot들이 생성될 부모 Transform
///   slotPrefab      — RewardSlot 프리팹
///   restartButton   — 재시작
///   homeButton      — 홈으로
///   panelRect       — 팝인 애니메이션 대상 RectTransform
/// </summary>
public class RunResultPanel : MonoBehaviour
{
    [Header("결과 정보")]
    [SerializeField] private TextMeshProUGUI waveText;      // "WAVE 15"
    [SerializeField] private TextMeshProUGUI bestWaveText;  // "최고웨이브 : 20"

    [Header("계정 레벨 바")]
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Slider          expSlider;
    [SerializeField] private TextMeshProUGUI expFractionText;

    [Header("보상 슬롯")]
    [SerializeField] private Transform  rewardContents;
    [SerializeField] private RewardSlot slotPrefab;

    [Header("버튼")]
    [SerializeField] private Button restartButton;
    [SerializeField] private Button homeButton;

    [Header("팝인 애니메이션")]
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private float         popDuration    = 0.35f;
    [SerializeField] private float         overshootScale = 1.08f;

    private Vector3 _originalScale;

    // ── 라이프사이클 ──────────────────────────────────────────────────────
    private void Awake()
    {
        _originalScale = panelRect != null ? panelRect.localScale : Vector3.one;
        gameObject.SetActive(false);

        restartButton?.onClick.AddListener(() => { Time.timeScale = 1f; GameManager.Shared?.RestartGame(); });
        homeButton?.onClick.AddListener(   () => { Time.timeScale = 1f; GameManager.Shared?.GoHome(); });
    }

    // ── 공개 API ──────────────────────────────────────────────────────────
    public void Show(int wave, int sessionGold, bool isClear)
    {
        gameObject.SetActive(true);
        Time.timeScale = 0f;

        // 현재 웨이브
        if (waveText) waveText.text = $"WAVE {wave}";

        // 최고 웨이브 갱신 & 표시
        string chapterId = PlayerPrefs.GetString("SelectedChapterID", "C1_S1");
        string prefKey   = $"BestWave_{chapterId}";
        int    prevBest  = PlayerPrefs.GetInt(prefKey, 0);
        if (wave > prevBest) { PlayerPrefs.SetInt(prefKey, wave); PlayerPrefs.Save(); prevBest = wave; }
        if (bestWaveText) bestWaveText.text = $"최고웨이브 : {prevBest}";

// 보상 계산 & 지급
         var reward = RunRewardCalculator.Calculate(sessionGold, wave);
         RewardManager.Shared?.GiveReward(reward);

         // 챕터 클리어 저장
         if (isClear)
         {
             PlayerPrefs.SetInt($"ChapterCleared_{chapterId}", 1);
             PlayerPrefs.Save();
         }

        // 보상 슬롯 생성
        SpawnRewardSlots(reward);

        // 계정 레벨 바
        RefreshLevelBar();

        StopAllCoroutines();
        StartCoroutine(PopIn());
    }

    public void Hide()
    {
        Time.timeScale = 1f;
        gameObject.SetActive(false);
    }

    // ── 보상 슬롯 ─────────────────────────────────────────────────────────
    private void SpawnRewardSlots(RunReward reward)
    {
        if (rewardContents == null || slotPrefab == null) return;

        foreach (Transform child in rewardContents)
            Destroy(child.gameObject);

        var cfg = RewardVisualConfig.Shared;
        SpawnSlot(cfg, RewardVisualConfig.RewardType.Gold, reward.gold.ToString());
        SpawnSlot(cfg, RewardVisualConfig.RewardType.Exp,  reward.exp.ToString());
        if (reward.ruby > 0)
            SpawnSlot(cfg, RewardVisualConfig.RewardType.Ruby, reward.ruby.ToString());
    }

    private void SpawnSlot(RewardVisualConfig cfg, RewardVisualConfig.RewardType type, string amount)
    {
        var slot = Instantiate(slotPrefab, rewardContents);
        slot.Setup(cfg?.GetIcon(type), amount);
    }

    // ── 계정 레벨 바 ──────────────────────────────────────────────────────
    private void RefreshLevelBar()
    {
        var rw = RewardManager.Shared;
        if (rw == null) return;

        int level    = rw.Level;
        int cur      = rw.Exp;
        int required = rw.ExpToNext(level);

        if (levelText)       levelText.text      = level.ToString();
        if (expFractionText) expFractionText.text = $"{cur} / {required}";
        if (expSlider)
        {
            expSlider.minValue = 0f;
            expSlider.maxValue = 1f;
            expSlider.value    = required > 0 ? (float)cur / required : 0f;
        }
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────────
    private IEnumerator PopIn()
    {
        var rt = panelRect != null ? panelRect : (RectTransform)transform;
        rt.localScale = Vector3.zero;

        float growTime = popDuration * 0.7f;
        float elapsed  = 0f;
        while (elapsed < growTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t     = Mathf.Clamp01(elapsed / growTime);
            float eased = 1f - (1f - t) * (1f - t);
            rt.localScale = Vector3.LerpUnclamped(Vector3.zero, _originalScale * overshootScale, eased);
            yield return null;
        }

        float shrinkTime = popDuration * 0.3f;
        elapsed = 0f;
        Vector3 from = rt.localScale;
        while (elapsed < shrinkTime)
        {
            elapsed += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(from, _originalScale, Mathf.Clamp01(elapsed / shrinkTime));
            yield return null;
        }
        rt.localScale = _originalScale;
    }
}
