using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 엘리트 처치 보상 패널. WaveClearRoutine에서 Show() 호출 후 콜백으로 계속 진행.
/// Inspector 연결: card (EliteRewardCard), confirmButton
/// </summary>
public class EliteRewardPanel : MonoBehaviour
{
    [SerializeField] private EliteRewardCard card;
    [SerializeField] private Button          confirmButton;

    private Action _onConfirm;
    private EliteRewardEntry _pendingEntry;

    void Awake()
    {
        confirmButton?.onClick.AddListener(OnConfirm);
        gameObject.SetActive(false);
    }

    public void Show(EliteRewardEntry entry, Action onConfirm)
    {
        _pendingEntry = entry;
        _onConfirm    = onConfirm;
        card?.Setup(entry);
        Time.timeScale = 0f;
        gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(PopInRoutine());
    }

    private IEnumerator PopInRoutine()
    {
        transform.localScale = Vector3.zero;
        float t = 0f;
        while (t < 0.22f)
        {
            t += Time.unscaledDeltaTime;
            float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.22f));
            transform.localScale = Vector3.one * s;
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    private void OnConfirm()
    {
        if (_pendingEntry != null)
        {
            var sm = PlayerStatManager.Shared;
            if (sm != null)
            {
                sm.BeginBatch();
                sm.AddPctMod(_pendingEntry.buffStat,  +_pendingEntry.buffPct);
                sm.AddPctMod(_pendingEntry.debuffStat, -_pendingEntry.debuffPct);
                sm.EndBatch();
            }
        }

        gameObject.SetActive(false);
        Time.timeScale = 1f;
        var cb = _onConfirm;
        _onConfirm = null;
        cb?.Invoke();
    }
}
