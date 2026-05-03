using System.Collections;
using System.Collections.Generic;
using Mewage.DB;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 시너지 정보 팝업 — DB_시너지에서 f_출현시작챕터 ≤ 현재 챕터인 시너지를 모두 스크롤로 표시합니다.
///
/// Open(chapter) 호출 시 해당 챕터까지 등장하는 시너지를 등급 순으로 나열합니다.
/// DB_시너지 필요 필드: f_시너지ID, f_이름, f_조건설명, f_등급, f_효과, f_출현시작챕터
/// </summary>
public class SynergyInfoPanel : MonoBehaviourSingleton<SynergyInfoPanel>
{
    [Header("타이틀")]
    public TextMeshProUGUI titleText;

    [Header("스크롤 콘텐츠")]
    public ScrollRect    scrollRect;
    public RectTransform content;
    public SynergySlot   slotPrefab;

    [Header("닫기 버튼")]
    public Button closeButton;

    protected override bool DontDestroy => false;

    // ── 초기화 ────────────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        closeButton?.onClick.AddListener(Hide);
        gameObject.SetActive(false);
    }

    // ── 공개 API ──────────────────────────────────────────────────────

    /// <summary>챕터 번호로 해당 챕터까지 등장하는 시너지 팝업을 엽니다.</summary>
    public void Show(int chapter)
    {
        ClearSlots();

        if (titleText != null)
            titleText.text = "시너지 목록";

        var rows = LoadSynergies(chapter);

        foreach (var row in rows)
        {
            var slot = Instantiate(slotPrefab, content);
            slot.Bind(row);
        }

        gameObject.SetActive(true);
        StartCoroutine(ScrollToTop());
    }

    /// <summary>패널이 열린 상태일 때 갱신이 필요한 경우 호출 (현재는 정적 표시이므로 no-op)</summary>
    public void Refresh() { }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // ── 내부 ──────────────────────────────────────────────────────────

    List<DB_시너지> LoadSynergies(int chapter)
    {
        var result = new List<DB_시너지>();
        DB_시너지.ForEachEntity(row =>
        {
            if (row == null || string.IsNullOrEmpty(row.f_시너지ID)) return;
            if (row.f_출현시작챕터 <= chapter)
                result.Add(row);
        });
        // 등급 오름차순 정렬 (S > A > B 순으로 위에 표시)
        result.Sort((a, b) => string.Compare(b.f_등급, a.f_등급, System.StringComparison.Ordinal));
        return result;
    }

    void ClearSlots()
    {
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
    }

    IEnumerator ScrollToTop()
    {
        yield return null; // 레이아웃 갱신 대기
        Canvas.ForceUpdateCanvases();
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
    }
}
