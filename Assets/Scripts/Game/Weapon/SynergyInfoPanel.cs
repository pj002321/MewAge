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

    [Header("등급 출현확률 텍스트 (선택)")]
    public TextMeshProUGUI normalProbText;
    public TextMeshProUGUI rareProbText;
    public TextMeshProUGUI epicProbText;
    public TextMeshProUGUI legendaryProbText;
    public TextMeshProUGUI mythicProbText;

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

        RefreshProbTexts();

        gameObject.SetActive(true);
        StartCoroutine(ScrollToTop());
    }

    public void Refresh() { }

    public void RefreshProbTexts()
    {
        var probs = UpgradeManager.Shared?.GetCurrentProbs() ?? default;
        if (normalProbText)    normalProbText.text    = $"{probs.common:0.#}%";
        if (rareProbText)      rareProbText.text      = $"{probs.rare:0.#}%";
        if (epicProbText)    { epicProbText.text      = $"{probs.epic:0.#}%";      epicProbText.color    = WeaponGradeColors.Get(WeaponGrade.Epic); }
        if (legendaryProbText) { legendaryProbText.text = $"{probs.legendary:0.#}%"; legendaryProbText.color = WeaponGradeColors.Get(WeaponGrade.Legendary); }
        if (mythicProbText)  { mythicProbText.text   = $"{probs.mythic:0.#}%";    mythicProbText.color  = WeaponGradeColors.Get(WeaponGrade.Mythic); }
    }

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

        var equipped = PlayerInventory.Shared?.Equipped;
        var achieved = SynergyConditionChecker.CheckAll(equipped);

        // 달성 여부 우선, 같으면 등급 내림차순
        result.Sort((a, b) =>
        {
            bool achA = achieved.Contains(a.f_시너지ID);
            bool achB = achieved.Contains(b.f_시너지ID);
            if (achA != achB) return achA ? -1 : 1;
            return string.Compare(b.f_등급, a.f_등급, System.StringComparison.Ordinal);
        });
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
