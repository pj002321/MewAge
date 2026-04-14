using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mewage.DB;

/// <summary>
/// 기물 뽑기 결과 팝업.
/// ShowSingle / ShowMulti 호출로 표시.
/// </summary>
public class WareResultPopup : MonoBehaviour
{
    [SerializeField] Button        closeHitArea;
    [SerializeField] TMP_Text      titleText;

    [SerializeField] GameObject    singleAnchor;      // 1회 결과 영역
    [SerializeField] GameObject    multiContent;       // 10회 결과 그리드

    [SerializeField] WareSlotView  slotPrefab;

    readonly List<WareSlotView> _spawnedSlots = new();

    void Awake()
    {
        closeHitArea?.onClick.AddListener(Close);
        gameObject.SetActive(false);
    }

    // ── 공개 API ─────────────────────────────────────────────────────

    public void ShowSingle(DB_기물 result)
    {
        if (result == null) return;
        Setup(new List<DB_기물> { result }, single: true);
    }

    public void ShowMulti(List<DB_기물> results)
    {
        if (results == null || results.Count == 0) return;
        Setup(results, single: results.Count == 1);
    }

    // ── 내부 ─────────────────────────────────────────────────────────

    void Setup(List<DB_기물> results, bool single)
    {
        ClearSlots();

        singleAnchor?.SetActive(single);
        multiContent?.SetActive(!single);

        if (titleText)
            titleText.text = results.Count == 1 ? "기물 획득!" : $"기물 {results.Count}개 획득!";

        Transform container = single
            ? singleAnchor?.transform
            : multiContent?.transform;

        if (container == null || slotPrefab == null) {
            // Reparent to root Canvas to ensure it's active
            var rootCanvas = GameObject.Find("Canvas");
            if (rootCanvas != null) transform.SetParent(rootCanvas.transform, false);
            gameObject.SetActive(true);
            return;
        }

        // Reparent to root Canvas to ensure it's active
        var canvas = GameObject.Find("Canvas");
        if (canvas != null) transform.SetParent(canvas.transform, false);

        gameObject.SetActive(true);
        StartCoroutine(SpawnSlotsWithAnimation(results, container));
    }

    void ClearSlots()
    {
        foreach (var s in _spawnedSlots)
            if (s) Destroy(s.gameObject);
        _spawnedSlots.Clear();
    }

    void Close()
    {
        ClearSlots();
        gameObject.SetActive(false);
    }

    private System.Collections.IEnumerator SpawnSlotsWithAnimation(System.Collections.Generic.List<DB_기물> results, Transform container)
    {
        for (int i = 0; i < results.Count; i++)
        {
            var data = results[i];
            if (data == null) continue;
            var slot = Instantiate(slotPrefab, container);
            slot.gameObject.SetActive(true);
            slot.Bind(data.f_기물ID, null, 1, 1);
            // 한 프레임 기다려서 active 상태가 되도록 함
            yield return null;
            slot.PlayScaleUp(delay: i * 0.1f); // 순차적으로 0.1초 간격
            _spawnedSlots.Add(slot);
        }
    }
}
