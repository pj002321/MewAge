using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Mewage.DB;

/// <summary>
/// 인게임 무기 상점 — 시대별 무기 정보 팝업.
/// Open(chapter) 호출 시 해당 챕터의 모든 무기를 등급 내림차순으로 스크롤 뷰에 나열합니다.
/// </summary>
public class EraWeaponInfoPopup : MonoBehaviour
{
    [Header("타이틀")]
    public TextMeshProUGUI titleText;

    [Header("스크롤 콘텐츠")]
    public ScrollRect      scrollRect;
    public RectTransform   content;
    public EraWeaponSlot   slotPrefab;

    [Header("닫기 버튼")]
    public Button          closeButton;

    [Header("등급 출현확률 텍스트 (선택)")]
    public TextMeshProUGUI normalProbText;
    public TextMeshProUGUI rareProbText;
    public TextMeshProUGUI epicProbText;
    public TextMeshProUGUI legendaryProbText;
    public TextMeshProUGUI mythicProbText;

    // ── 초기화 ────────────────────────────────────────────────────────
    void Awake()
    {
        closeButton?.onClick.AddListener(Close);
        gameObject.SetActive(false);
    }

    // ── 공개 API ──────────────────────────────────────────────────────

    /// <summary>챕터 번호로 해당 시대 무기 팝업을 엽니다.</summary>
    public void Open(int chapter)
    {
        ClearSlots();

        if (titleText != null)
            titleText.text = $"{ChapterToEraName(chapter)} 무기";

        var weapons = LoadWeaponsForChapter(chapter);
        // 등급 내림차순 (Mythic → Common)
        weapons.Sort((a, b) => a.grade.CompareTo(b.grade));

        foreach (var w in weapons)
        {
            var slot = Instantiate(slotPrefab, content);
            slot.Bind(w);
        }

        RefreshProbTexts();

        gameObject.SetActive(true);
        StartCoroutine(ScrollToTop());
    }

    public void RefreshProbTexts()
    {
        var probs = UpgradeManager.Shared?.GetCurrentProbs() ?? default;
        if (normalProbText)    normalProbText.text    = $"{probs.common:0.#}%";
        if (rareProbText)      rareProbText.text      = $"{probs.rare:0.#}%";
        if (epicProbText)    { epicProbText.text      = $"{probs.epic:0.#}%";      epicProbText.color    = WeaponGradeColors.Get(WeaponGrade.Epic); }
        if (legendaryProbText) { legendaryProbText.text = $"{probs.legendary:0.#}%"; legendaryProbText.color = WeaponGradeColors.Get(WeaponGrade.Legendary); }
        if (mythicProbText)  { mythicProbText.text   = $"{probs.mythic:0.#}%";    mythicProbText.color  = WeaponGradeColors.Get(WeaponGrade.Mythic); }
    }

    IEnumerator ScrollToTop()
    {
        yield return null; // 레이아웃 갱신 대기
        Canvas.ForceUpdateCanvases();
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    // ── 내부 ──────────────────────────────────────────────────────────

    List<WeaponData> LoadWeaponsForChapter(int chapter)
    {
        var result = new List<WeaponData>();
        DB_무기.ForEachEntity(row =>
        {
            if (row == null || string.IsNullOrEmpty(row.f_무기ID)) return;
            var data = WeaponData.FromDB(row);
            if (data != null && data.chapter == chapter)
                result.Add(data);
        });
        return result;
    }

    void ClearSlots()
    {
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
    }

    static string ChapterToEraName(int chapter) => chapter switch
    {
        1 => "원시시대",
        2 => "신석기 시대",
        3 => "청동시대",
        4 => "철기",
        5 => "중세",
        6 => "근대",
        7 => "현대",
        8 => "미래",
        9 => "우주",
        _ => $"{chapter}챕터",
    };
}
