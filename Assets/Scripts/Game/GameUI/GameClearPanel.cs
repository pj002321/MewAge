using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Game Clear Panel — shown when the player defeats the final Boss.
/// 
/// UI Elements:
///   - finalWaveText        : "WAVE 10 CLEAR" headline
///   - expGainedIcon        : Image of EXP icon
///   - expGainedText        : Acquired in-game EXP amount
///   - goldGainedIcon       : Image of Gold icon
///   - goldGainedText       : Acquired in-game Gold amount
///   - outgameExpSlider     : Out-game (persistent) user EXP progress bar
///   - outgameLevelText     : Out-game user level label  "Lv. 5"
///   - outgameExpText       : Out-game EXP fraction label "120 / 200" (optional)
/// 
/// # Unity Setup:
///   1. Create a Canvas child: Panel > GameClearPanel (add CanvasGroup for fade if desired).
///   2. Attach this script to that Panel GameObject.
///   3. Map each [SerializeField] in the Inspector.
///   4. In GameManager Inspector, assign this component to the "gameClearPanel" slot.
///   5. Disable the Panel GameObject by default (Show() activates it at runtime).
/// </summary>
public class GameClearPanel : MonoBehaviour
{
    [Header("Clear Headline")]
    [SerializeField] private TextMeshProUGUI finalWaveText;   // "WAVE {n} CLEAR"

    [Header("획득 경험치")]
    [SerializeField] private Image           expGainedIcon;   // EXP 아이콘 이미지
    [SerializeField] private TextMeshProUGUI expGainedText;   // 획득 EXP 숫자

    [Header("획득 골드")]
    [SerializeField] private Image           goldGainedIcon;  // Gold 아이콘 이미지
    [SerializeField] private TextMeshProUGUI goldGainedText;  // 획득 Gold 숫자

    [Header("아웃게임 유저 경험치 현황")]
    [SerializeField] private Slider          outgameExpSlider; // 누적 유저 EXP 슬라이더
    [SerializeField] private TextMeshProUGUI outgameLevelText; // "Lv. 5"
    [SerializeField] private TextMeshProUGUI outgameExpText;   // "120 / 200" (optional)

    [Header("버튼")]
    [SerializeField] private Button homeButton;    // 홈으로 이동
    [SerializeField] private Button restartButton; // 다시 시작

    [Header("등장 애니메이션")]
    [SerializeField] private float popDuration   = 0.35f;  // 전체 팝인 시간(초)
    [SerializeField] private float overshootScale = 1.1f;  // 잠깐 커졌다 돌아오는 비율

    private Vector3 originalScale;

    private void Awake()
    {
        // Panel is deactivated by GameManager.Start() at runtime.
        // Do NOT call gameObject.SetActive(false) here — it is unreliable when the object starts inactive.
        originalScale = transform.localScale;  // 원본 스케일 기억
        if (homeButton    != null) homeButton.onClick.AddListener(OnHomeClicked);
        if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
    }

    /// <summary>
    /// GameManager.OnBossDefeated() → ShowGameClear() → 이 메서드를 호출합니다.
    /// </summary>
    /// <param name="finalWave">클리어한 웨이브 번호</param>
    /// <param name="expGained">이번 런에서 획득한 총 EXP</param>
    /// <param name="goldGained">이번 런에서 획득한 총 골드</param>
    public void Show(int finalWave, int expGained, int goldGained)
    {
        gameObject.SetActive(true);
        Time.timeScale = 0f; // 게임 일시정지

        // ── Headline ──────────────────────────────────
        if (finalWaveText != null)
            finalWaveText.text = $"WAVE {finalWave}";

        // ── 획득 EXP / Gold ───────────────────────────
        if (expGainedText  != null) expGainedText.text  = $"+{expGained}";
        if (goldGainedText != null) goldGainedText.text = $"+{goldGained}";

        // ── 아웃게임 유저 EXP 현황 ─────────────────────
        RefreshOutgameExp(expGained);

        // ── 팝인 애니메이션: 0 → overshoot → 원본 스케일 ──
        StopAllCoroutines();
        StartCoroutine(PopInRoutine());

        this.DLog($"[GameClearPanel] Shown — wave:{finalWave} exp:{expGained} gold:{goldGained}");
    }

    private IEnumerator PopInRoutine()
    {
        // Phase 1: 0 → overshootScale  (70% 구간)
        float growTime  = popDuration * 0.7f;
        float elapsed   = 0f;
        transform.localScale = Vector3.zero;

        while (elapsed < growTime)
        {
            elapsed += Time.unscaledDeltaTime;  // timeScale=0 대비
            float t  = Mathf.Clamp01(elapsed / growTime);
            // Ease-out: 처음엔 빠르게, 끝엔 천천히
            float eased = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.LerpUnclamped(Vector3.zero,
                originalScale * overshootScale, eased);
            yield return null;
        }

        // Phase 2: overshootScale → 원본 스케일  (30% 구간)
        float shrinkTime = popDuration * 0.3f;
        elapsed = 0f;
        Vector3 fromScale = transform.localScale;

        while (elapsed < shrinkTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t  = Mathf.Clamp01(elapsed / shrinkTime);
            transform.localScale = Vector3.Lerp(fromScale, originalScale, t);
            yield return null;
        }

        transform.localScale = originalScale;
    }

    /// <summary>
    /// 아웃게임 누적 유저 EXP를 읽고 슬라이더/텍스트를 갱신합니다.
    /// 실제 저장 시스템과 연동 시 이 메서드를 수정하세요.
    /// </summary>
    private void RefreshOutgameExp(int bonusExp)
    {
        // ─── 실제 저장 시스템 연동 예시 ───────────────────────────────
        // int level      = UserSaveManager.Shared.Level;
        // int currentExp = UserSaveManager.Shared.Exp + bonusExp;
        // int requiredExp = LevelTable.GetRequiredExp(level);
        // ──────────────────────────────────────────────────────────────

        // 임시 더미 데이터 (저장 시스템 연동 전까지 사용)
        int level       = 1;
        int currentExp  = bonusExp;
        int requiredExp = 100;

        if (outgameLevelText != null)
            outgameLevelText.text = $"{level}";

        if (outgameExpSlider != null)
        {
            outgameExpSlider.minValue = 0f;
            outgameExpSlider.maxValue = 1f;
            outgameExpSlider.value    = requiredExp > 0 ? (float)currentExp / requiredExp : 0f;
        }

        if (outgameExpText != null)
            outgameExpText.text = $"{currentExp} / {requiredExp}";
    }

    // ── 버튼 콜백 ───────────────────────────────────────────────────
    private void OnHomeClicked()
    {
        Time.timeScale = 1f;
        GameManager.Shared?.GoHome();
    }

    private void OnRestartClicked()
    {
        Time.timeScale = 1f;
        GameManager.Shared?.RestartGame();
    }
}
