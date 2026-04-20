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
    public Image           overlayImage;  // 잠금 오버레이
    public Image           highlightImage; // 현재 선택 카드 강조
    public RawImage        playerPortrait; // 플레이어 랜딩 애니메이션
    public GameObject      symbolObjects;
    public TextMeshProUGUI stageNameText;
    public TextMeshProUGUI bestWaveText;

    [Header("플로팅 대상 (지정한 GameObject 묶음에만 효과 적용)")]
    public GameObject floatTarget;

    [Header("플로팅 설정")]
    public float floatAmplitude  = 8f;    // Y 이동 픽셀
    public float floatScaleRange = 0.03f; // 스케일 진폭 (0.03 = ±3%)
    public float floatSpeed      = 1.8f;  // 사인파 속도

    bool      _isLocked;
    Coroutine _floatCoroutine;
    Vector3   _floatOriginPos;
    Vector3   _floatOriginScale;

    // ── 데이터 바인딩 ────────────────────────────────────────────────
    public void Bind(StageSelectPanel.StageEntry entry)
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
            bestWaveText.text = entry.bestWave > 0 ? $"최고 웨이브: {entry.bestWave}" : "최고 웨이브: -";
    }

    // 중앙 카드: 이름·웨이브·하이라이트 표시 / 비중앙·잠금 카드: 아이콘만 표시
    public void SetCenterCard(bool isCenter)
    {
        bool showFull = isCenter && !_isLocked;
        if (stageNameText  != null) stageNameText.gameObject.SetActive(isCenter);
        if (bestWaveText   != null) bestWaveText.gameObject.SetActive(isCenter);
        if (symbolObjects  != null) symbolObjects.SetActive(showFull);
        if (playerPortrait != null) playerPortrait.gameObject.SetActive(showFull);
        if (highlightImage != null) highlightImage.gameObject.SetActive(isCenter);
    }

    // ── 플로팅 ───────────────────────────────────────────────────────
    public void PlayFloat()
    {
        StopFloat();
        if (floatTarget == null) return;
        _floatOriginPos   = floatTarget.transform.localPosition;
        _floatOriginScale = floatTarget.transform.localScale;
        _floatCoroutine   = StartCoroutine(FloatCoroutine());
    }

    public void StopFloat()
    {
        if (_floatCoroutine == null) return;
        StopCoroutine(_floatCoroutine);
        _floatCoroutine = null;
        if (floatTarget != null)
        {
            floatTarget.transform.localPosition = _floatOriginPos;
            floatTarget.transform.localScale    = _floatOriginScale;
        }
    }

    // 스냅 완료 시 펀치 후 플로팅 재개
    public void PlayPunch(System.Action onComplete = null)
    {
        StopFloat();
        if (floatTarget == null) { onComplete?.Invoke(); return; }
        _floatOriginPos   = floatTarget.transform.localPosition;
        _floatOriginScale = floatTarget.transform.localScale;
        StartCoroutine(PunchCoroutine(onComplete));
    }

    IEnumerator FloatCoroutine()
    {
        float t = 0f;
        while (floatTarget != null)
        {
            t += Time.unscaledDeltaTime * floatSpeed;
            float sin = Mathf.Sin(t);
            // Y 위로 갈수록 살짝 커지고, 아래로 갈수록 살짝 작아짐
            floatTarget.transform.localPosition = _floatOriginPos + new Vector3(0f, sin * floatAmplitude, 0f);
            floatTarget.transform.localScale    = _floatOriginScale * (1f + sin * floatScaleRange);
            yield return null;
        }
    }

    IEnumerator PunchCoroutine(System.Action onComplete)
    {
        float punchUp  = 0.12f;
        float upTime   = 0.08f;
        float downTime = 0.14f;
        float baseS    = _floatOriginScale.x;

        for (float t = 0f; t < upTime; t += Time.unscaledDeltaTime)
        {
            if (floatTarget == null) yield break;
            float s = Mathf.Lerp(baseS, baseS * (1f + punchUp), t / upTime);
            floatTarget.transform.localScale = Vector3.one * s;
            yield return null;
        }
        for (float t = 0f; t < downTime; t += Time.unscaledDeltaTime)
        {
            if (floatTarget == null) yield break;
            float s = Mathf.Lerp(baseS * (1f + punchUp), baseS, t / downTime);
            floatTarget.transform.localScale = Vector3.one * s;
            yield return null;
        }
        if (floatTarget != null) floatTarget.transform.localScale = _floatOriginScale;

        onComplete?.Invoke();
    }

    // ── 기타 ────────────────────────────────────────────────────────
    public void PlayLandingEffect()
    {
        if (playerPortrait != null)
            playerPortrait.GetComponent<Animator>()?.Play("enter_portrait");
    }

    void OnDestroy() => StopFloat();
}
