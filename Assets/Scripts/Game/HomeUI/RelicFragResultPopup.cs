using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 유물 조각 뽑기 결과 팝업.
/// ShowSingle / ShowMulti 호출로 표시.
/// </summary>
public class RelicFragResultPopup : MonoBehaviour
{
    public static RelicFragResultPopup Shared { get; private set; }
    public struct Entry
    {
        public RelicData data;
        public int       count;
        public Entry(RelicData d, int c) { data = d; count = c; }
    }

    [SerializeField] Button            closeHitArea;
    [SerializeField] TMP_Text          titleText;
    [SerializeField] GameObject        singleAnchor;   // 1회 결과 영역
    [SerializeField] GameObject        multiContent;   // 다회 결과 그리드
    [SerializeField] RelicFragSlotView slotPrefab;

    readonly List<RelicFragSlotView> _spawnedSlots = new();

    void Awake()
    {
        Shared = this;
        closeHitArea?.onClick.AddListener(Close);
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Shared == this) Shared = null;
    }

    // ── 공개 API ─────────────────────────────────────────────────────

    public void ShowSingle(RelicData data, int count)
    {
        if (data == null) return;
        Setup(new List<Entry> { new Entry(data, count) }, single: true);
    }

    public void ShowMulti(List<Entry> results)
    {
        if (results == null || results.Count == 0) return;
        Setup(results, single: results.Count == 1);
    }

    // ── 내부 ─────────────────────────────────────────────────────────

    void Setup(List<Entry> results, bool single)
    {
        gameObject.SetActive(true);

        ClearSlots();
        singleAnchor?.SetActive(single);
        multiContent?.SetActive(!single);

        if (titleText)
            titleText.text = results.Count == 1 ? "유물 조각 획득!" : $"유물 조각 {results.Count}개 획득!";

        Transform container = single ? singleAnchor?.transform : multiContent?.transform;
        if (container == null || slotPrefab == null) return;

        for (int i = 0; i < results.Count; i++)
        {
            var entry = results[i];
            if (entry.data == null) continue;

            var slot = Instantiate(slotPrefab, container);
            slot.gameObject.SetActive(true);
            slot.Bind(entry.data, entry.count);
            slot.PlayScaleUp(delay: i * 0.1f);
            _spawnedSlots.Add(slot);
        }
    }

    void ClearSlots()
    {
        foreach (var s in _spawnedSlots)
            if (s) Destroy(s.gameObject);
        _spawnedSlots.Clear();
    }

    public void Close()
    {
        ClearSlots();
        gameObject.SetActive(false);
    }

}
