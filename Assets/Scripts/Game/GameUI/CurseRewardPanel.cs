using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Mewage.DB;

/// <summary>
/// 저주/축복 패널. 엘리트 처치 후 DB_증강 저주+축복 카드 1장을 표시합니다.
/// Inspector 연결: card (CurseRewardCard), skipButton
/// 블러 캡처는 GameManager에서 처리 후 ShowPanel() 호출.
/// </summary>
public class CurseRewardPanel : MonoBehaviour
{
    [SerializeField] CurseRewardCard card;
    [SerializeField] Button          skipButton;
    [SerializeField] Button statButton;

    private Action _onConfirm;
    private DB_증강 _pending;

    void Awake()
    {
        var btn = card?.GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(OnAccept);
        skipButton?.onClick.AddListener(OnSkip);
        gameObject.SetActive(false);

        statButton.onClick.AddListener(() =>
        {
            var panel = StatPanelUI.Shared;
            if (panel == null) return;
            if (panel.gameObject.activeSelf) panel.Hide();
            else panel.Show();
        });
    }

    void OnEnable()
    {
        foreach (var anim in GetComponentsInChildren<Animator>(true))
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    /// <summary>데이터 세팅만. GameManager가 blur 캡처 후 ShowPanel()을 호출합니다.</summary>
    public void Prepare(DB_증강 data, Action onConfirm)
    {
        _pending   = data;
        _onConfirm = onConfirm;
        card?.Setup(data);
    }

    /// <summary>패널이 이미 SetActive(true) 된 뒤 호출. 팝인 애니메이션 시작.</summary>
    public void ShowPanel()
    {
        StopAllCoroutines();
        StartCoroutine(PopInRoutine());
    }

    IEnumerator PopInRoutine()
    {
        transform.localScale = Vector3.zero;
        float t = 0f;
        while (t < 0.22f)
        {
            t += Time.unscaledDeltaTime;
            transform.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.22f));
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    void OnAccept()
    {
        if (_pending != null)
        {
            var sm = PlayerStatManager.Shared;
            if (sm != null)
            {
                sm.BeginBatch();
                ApplyEffect(sm, _pending.f_저주, _pending.f_저주수치, curse: true);
                ApplyEffect(sm, _pending.f_축복, _pending.f_축복수치, curse: false);
                sm.EndBatch();
            }
        }
        card?.PlaySelectEffect();
        StartCoroutine(CloseAfterEffect());
    }

    IEnumerator CloseAfterEffect()
    {
        yield return new WaitForSecondsRealtime(0.45f);
        Close();
    }

    void OnSkip() => Close();

    void Close()
    {
        StopAllCoroutines();
        gameObject.SetActive(false);
        Time.timeScale = 1f;
        var cb = _onConfirm;
        _onConfirm = null;
        cb?.Invoke();
    }

    static void ApplyEffect(PlayerStatManager sm, string augId, float value, bool curse)
    {
        AugmentHelper.ApplyStatMod(sm, augId, value, positive: !curse);
    }
}
