using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스테이지 선택 캐러셀 카드 1장.
/// StageSelectPanel이 런타임에 생성하고 Bind()로 데이터를 채웁니다.
/// </summary>
public class StageCardView : MonoBehaviour
{
    public Image           iconImage;
    public Image           overlayImage;         // 잠금 아이콘 오버레이 (lockImg)
    public Image           lockBackgroundImage;  // 잠금 배경
    public Image           highlightImage;       // 현재 선택 카드 강조
    public RawImage        playerPortrait;       // 플레이어 랜딩 애니메이션
    public Transform       symbolParent;         // 심볼 프리팹이 주입될 빈 컨테이너
    public TextMeshProUGUI stageNameText;
    public TextMeshProUGUI bestWaveText;
    public TextMeshProUGUI chapterText;    // "챕터\n1-1" 형식

    [Header("펀치 대상")]
    public GameObject floatTarget;

    [Header("정보 버튼")]
    public Button infoButton;

    bool      _isLocked;
    int       _chapter;
    string    _stageId;
    string    _chapterLabel;
    GameObject _symbolInstance;

    void Awake()
    {
        infoButton?.onClick.AddListener(OnInfoClicked);
    }

    void OnInfoClicked()
    {
        StageInfoPopup.Shared?.Open(_chapter, _stageId);
    }

    // ── 데이터 바인딩 ────────────────────────────────────────────────
    public void Bind(MainMenuPanel.StageEntry entry)
    {
        _isLocked     = entry.isLocked;
        _chapter      = ParseChapter(entry.chapterID);
        _stageId      = entry.chapterID;
        _chapterLabel = entry.chapterLabel;

        if (iconImage != null && entry.icon != null)
            iconImage.sprite = entry.icon;
        if (overlayImage != null)
            overlayImage.gameObject.SetActive(entry.isLocked);
        if (lockBackgroundImage != null)
            lockBackgroundImage.gameObject.SetActive(entry.isLocked);

        if (stageNameText != null) stageNameText.text = entry.stageName;
        if (bestWaveText  != null)
        {
            if (entry.isLocked)
                bestWaveText.text = entry.requireClearLabel;
            else
                bestWaveText.text = entry.bestWave > 0 ? $"최고생존 : {entry.bestWave}/{entry.totalWaves}" : "";
        }
        if (chapterText   != null) chapterText.text = entry.chapterLabel;
    }

    // 중앙 카드: 이름·웨이브·하이라이트 표시 / 비중앙·잠금 카드: 아이콘만 표시
    public void SetCenterCard(bool isCenter)
    {
        bool showFull = isCenter && !_isLocked;
        if (stageNameText  != null) stageNameText.gameObject.SetActive(isCenter);
        if (bestWaveText   != null) bestWaveText.gameObject.SetActive(isCenter);
        if (chapterText    != null) chapterText.gameObject.SetActive(isCenter);
        if (_symbolInstance != null) _symbolInstance.SetActive(isCenter);
        if (playerPortrait != null)
        {
            playerPortrait.gameObject.SetActive(showFull);
            if (showFull)
            {
                var anim = playerPortrait.GetComponent<Animator>();
                if (anim != null) anim.speed = 2f;
            }
        }
        if (highlightImage != null) highlightImage.gameObject.SetActive(showFull);
    }

    // 스테이지 심볼 프리팹을 symbolParent 하위에 주입
    public void BindSymbol(GameObject prefab)
    {
        if (symbolParent == null) return;
        if (_symbolInstance != null) Destroy(_symbolInstance);
        if (prefab == null) return;

        _symbolInstance = Instantiate(prefab, symbolParent);
    }

    // 스냅 완료 시 펀치 연출
    public void PlayPunch(System.Action onComplete = null)
    {
        if (floatTarget == null) { onComplete?.Invoke(); return; }
        StopAllCoroutines();
        StartCoroutine(PunchCoroutine(onComplete));
    }

    IEnumerator PunchCoroutine(System.Action onComplete)
    {
        Vector3 originScale = floatTarget.transform.localScale;
        float   baseS       = originScale.x;
        const float punchUp  = 0.12f;
        const float upTime   = 0.08f;
        const float downTime = 0.14f;

        for (float t = 0f; t < upTime; t += Time.unscaledDeltaTime)
        {
            if (floatTarget == null) yield break;
            floatTarget.transform.localScale = Vector3.one * Mathf.Lerp(baseS, baseS * (1f + punchUp), t / upTime);
            yield return null;
        }
        for (float t = 0f; t < downTime; t += Time.unscaledDeltaTime)
        {
            if (floatTarget == null) yield break;
            floatTarget.transform.localScale = Vector3.one * Mathf.Lerp(baseS * (1f + punchUp), baseS, t / downTime);
            yield return null;
        }
        if (floatTarget != null) floatTarget.transform.localScale = originScale;

        onComplete?.Invoke();
    }

    // ── 기타 ────────────────────────────────────────────────────────

    static int ParseChapter(string chapterID)
    {
        if (string.IsNullOrEmpty(chapterID)) return 1;
        var parts = chapterID.Split('_');
        if (parts.Length > 0 && parts[0].Length > 1 &&
            int.TryParse(parts[0].Substring(1), out int ch))
            return ch;
        return 1;
    }

    public void PlayLandingEffect()
    {
        if (playerPortrait != null)
            playerPortrait.GetComponent<Animator>()?.Play("enter_portrait");
    }

    void OnDestroy()
    {
        StopAllCoroutines();
        if (_symbolInstance != null) Destroy(_symbolInstance);
    }
}
