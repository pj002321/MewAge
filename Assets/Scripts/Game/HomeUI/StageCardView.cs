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
    public Image           overlayImage;   // 잠금 오버레이
    public Image           highlightImage; // 현재 선택 카드 강조
    public RawImage        playerPortrait; // 플레이어 랜딩 애니메이션
    public GameObject      symbolObjects;
    public TextMeshProUGUI stageNameText;
    public TextMeshProUGUI bestWaveText;
    public TextMeshProUGUI chapterText;    // "챕터\n1-1" 형식

    [Header("펀치 대상")]
    public GameObject floatTarget;

    bool _isLocked;

    // ── 데이터 바인딩 ────────────────────────────────────────────────
    public void Bind(MainMenuPanel.StageEntry entry)
    {
        _isLocked = entry.isLocked;

        if (iconImage != null)
        {
            if (entry.icon != null)
            {
                iconImage.sprite = entry.icon;
                iconImage.color  = Color.white;
            }
            iconImage.enabled = iconImage.sprite != null;
        }
        if (overlayImage != null)
        {
            overlayImage.sprite = iconImage.sprite;
            overlayImage.gameObject.SetActive(entry.isLocked);
        }
        if (stageNameText != null) stageNameText.text = entry.stageName;
        if (bestWaveText  != null)
            bestWaveText.text = entry.bestWave > 0 ? $"최고생존 : {entry.bestWave}/{entry.totalWaves}" : "";
        if (chapterText   != null) chapterText.text = entry.chapterLabel;
    }

    // 중앙 카드: 이름·웨이브·하이라이트 표시 / 비중앙·잠금 카드: 아이콘만 표시
    public void SetCenterCard(bool isCenter)
    {
        bool showFull = isCenter && !_isLocked;
        if (stageNameText  != null) stageNameText.gameObject.SetActive(isCenter);
        if (bestWaveText   != null) bestWaveText.gameObject.SetActive(isCenter);
        if (chapterText    != null) chapterText.gameObject.SetActive(isCenter);
        if (symbolObjects  != null) symbolObjects.SetActive(showFull);
        if (playerPortrait != null) playerPortrait.gameObject.SetActive(showFull);
        if (highlightImage != null) highlightImage.gameObject.SetActive(isCenter);
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
    public void PlayLandingEffect()
    {
        if (playerPortrait != null)
            playerPortrait.GetComponent<Animator>()?.Play("enter_portrait");
    }

    void OnDestroy() => StopAllCoroutines();
}
