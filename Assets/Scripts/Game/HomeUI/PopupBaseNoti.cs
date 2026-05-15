using System.Collections;
using TMPro;
using UnityEngine;

/// 화면 중앙에 텍스트를 Fade In → Hold → Fade Out으로 잠깐 띄우는 범용 노티.
/// Show(message)만 호출하면 되며 중복 호출 시 이전 연출을 즉시 교체.
/// CanvasGroup 필수 — Inspector에서 Alpha=0, Blocks Raycasts=false 로 설정해둘 것.
[RequireComponent(typeof(CanvasGroup))]
public class PopupBaseNoti : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI messageText;

    const float FadeIn  = 0.2f;
    const float Hold    = 1.5f;
    const float FadeOut = 0.3f;

    CanvasGroup _cg;
    Coroutine   _routine;

    void Awake()
    {
        _cg                = GetComponent<CanvasGroup>();
        _cg.alpha          = 0f;
        _cg.blocksRaycasts = false;
        _cg.interactable   = false;
    }

    public void Show(string message)
    {
        if (messageText != null) messageText.text = message;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ShowRoutine());
    }

    IEnumerator ShowRoutine()
    {
        float elapsed = 0f;
        while (elapsed < FadeIn)
        {
            elapsed    += Time.unscaledDeltaTime;
            _cg.alpha   = Mathf.Clamp01(elapsed / FadeIn);
            yield return null;
        }
        _cg.alpha = 1f;

        elapsed = 0f;
        while (elapsed < Hold) { elapsed += Time.unscaledDeltaTime; yield return null; }

        elapsed = 0f;
        while (elapsed < FadeOut)
        {
            elapsed    += Time.unscaledDeltaTime;
            _cg.alpha   = 1f - Mathf.Clamp01(elapsed / FadeOut);
            yield return null;
        }
        _cg.alpha = 0f;
        _routine  = null;
    }
}
