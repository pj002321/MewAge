using System.Collections;
using System.Collections.Generic;
using Mewage.DB;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스테이지 카드의 정보 버튼을 눌러 여는 팝업.
/// 해당 챕터의 몬스터 목록 / 무기 목록을 탭으로 전환하여 표시합니다.
/// 탭 전환 시 슬롯들이 납작하게 모인 상태에서 위→아래로 쫙 펼쳐집니다.
/// </summary>
[DefaultExecutionOrder(-100)]   // ScalablePopup.Awake(SetActive=false) 전에 Shared 등록 보장
public class StageInfoPopup : MonoBehaviour
{
    public static StageInfoPopup Shared { get; private set; }

    [Header("헤더")]
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] Button          closeButton;

    [Header("탭 버튼")]
    [SerializeField] Button monsterTabButton;
    [SerializeField] Button weaponTabButton;

    [Header("공용 스크롤")]
    [SerializeField] ScrollRect    scroll;
    [SerializeField] RectTransform content;
    [SerializeField] RectTransform bossContent;

    [Header("슬롯 프리팹")]
    [SerializeField] EraEnemySlot   monsterSlotPrefab;
    [SerializeField] StageWeaponSlot weaponSlotPrefab;

    ScalablePopup _popup;
    int    _chapter;
    string _stageId;

    // ── 초기화 ────────────────────────────────────────────────────────────

    void Awake()
    {
        Shared = this;
        _popup = GetComponent<ScalablePopup>();
        monsterTabButton?.onClick.AddListener(() => ShowTab(0));
        weaponTabButton?.onClick.AddListener(() => ShowTab(1));
        // 닫기 버튼은 ScalablePopup이 처리
    }

    void OnDestroy()
    {
        if (Shared == this) Shared = null;
    }

    // ── 공개 API ──────────────────────────────────────────────────────────

    public void Open(int chapter, string stageId)
    {
        _chapter = chapter;
        _stageId = stageId;
        StopAllCoroutines();
        ShowTab(1);
        _popup?.Show();
    }

    public void Close()
    {
        StopAllCoroutines();
        _popup.Hide();
    }

    // ── 탭 전환 + 스프레드 애니메이션 ────────────────────────────────────

    void ShowTab(int tab)
    {
        StopAllCoroutines();
        ClearContent();

        if (titleText != null)
            titleText.text = tab == 0 ? "출현하는 적" : $"{ChapterToEraName(_chapter)} 무기";

        if (tab == 0) BuildMonsters(_chapter);
        else          BuildWeapons(_chapter);

        StartCoroutine(SpreadRoutine());
    }

    IEnumerator SpreadRoutine()
    {
        // 현재 자식 전부 납작하게 (삭제 대기 포함)
        foreach (var ct in new[] { content, bossContent })
        {
            if (ct == null) continue;
            for (int i = 0; i < ct.childCount; i++)
                ct.GetChild(i).localScale = new Vector3(1f, 0f, 1f);
        }

        yield return null;  // 이 시점에 Destroy 처리 완료 → 실제 신규 슬롯만 남음

        // 살아있는 슬롯 전부 캡처 (content + bossContent)
        int baseCount  = content != null ? content.childCount : 0;
        int bossCount  = bossContent != null ? bossContent.childCount : 0;
        var slots      = new Transform[baseCount + bossCount];
        for (int i = 0; i < baseCount; i++)
            slots[i] = content.GetChild(i);
        for (int i = 0; i < bossCount; i++)
            slots[baseCount + i] = bossContent.GetChild(i);

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        Canvas.ForceUpdateCanvases();
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;

        const float stagger = 0.008f;
        const float dur     = 0.05f;

        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            float startTime = Time.unscaledTime + i * stagger;

            // 이 슬롯의 딜레이 대기
            while (Time.unscaledTime < startTime)
                yield return null;

            // 펼치기
            for (float elapsed = 0f; elapsed < dur; elapsed += Time.unscaledDeltaTime)
            {
                if (slot == null) break;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
                slot.localScale = new Vector3(1f, t, 1f);
                yield return null;
            }

            if (slot != null) slot.localScale = Vector3.one;
        }
    }

    // ── 몬스터 빌드 ───────────────────────────────────────────────────────

    void BuildMonsters(int chapter)
    {
        if (content == null || monsterSlotPrefab == null)
        {
            this.DError($"BuildMonsters 실패: content={content}, prefab={monsterSlotPrefab}");
            return;
        }

        var visualCfg = UnitVisualConfig.Shared;

        // DB_웨이브에서 이 스테이지에 출현하는 유닛 ID 수집 (중복 제거)
        string wavePrefix = _stageId + "_W";
        var unitIdSet = new HashSet<string>();
        DB_웨이브.ForEachEntity(row =>
        {
            if (row == null || string.IsNullOrEmpty(row.f_웨이브ID)) return;
            if (!row.f_웨이브ID.StartsWith(wavePrefix)) return;
            if (!string.IsNullOrEmpty(row.f_유닛1)) unitIdSet.Add(row.f_유닛1);
            if (!string.IsNullOrEmpty(row.f_유닛2)) unitIdSet.Add(row.f_유닛2);
            if (!string.IsNullOrEmpty(row.f_유닛3)) unitIdSet.Add(row.f_유닛3);
            if (!string.IsNullOrEmpty(row.f_유닛4)) unitIdSet.Add(row.f_유닛4);
        });

        // 챕터 내 유닛 순서 맵 (unitId → chapterIndex)
        var unitIndexMap = new Dictionary<string, int>();
        int idx = 0;
        DB_유닛.ForEachEntity(row =>
        {
            if (row == null || string.IsNullOrEmpty(row.f_유닛ID)) return;
            if (!row.f_유닛ID.StartsWith($"C{chapter}_")) return;
            unitIndexMap[row.f_유닛ID] = idx++;
        });

        int count = 0;
        foreach (var unitId in unitIdSet)
        {
            int unitIdx = unitIndexMap.TryGetValue(unitId, out int i) ? i : -1;
            var slot = Instantiate(monsterSlotPrefab, content);
            var sprite = (unitIdx >= 0 && visualCfg != null) ? visualCfg.GetSprite(chapter, unitIdx) : null;
            slot.Bind(sprite, false);
            count++;
        }

        // DB_보스에서 이 스테이지에 출현하는 보스 수집
        if (bossContent != null)
        {
            DB_보스.ForEachEntity(row =>
            {
                if (row == null || string.IsNullOrEmpty(row.f_보스ID)) return;
                if (!row.f_보스ID.StartsWith(_stageId + "_")) return;

                var icon     = visualCfg != null ? visualCfg.GetBossIcon(row.f_보스ID) : null;
                var bossSlot = Instantiate(monsterSlotPrefab, bossContent);
                bossSlot.Bind(icon, true);
                count++;
            });
        }

        this.DLog($"스테이지{_stageId} 몬스터 {count}개 생성");
    }

    // ── 무기 빌드 ─────────────────────────────────────────────────────────

    void BuildWeapons(int chapter)
    {
        if (content == null || weaponSlotPrefab == null) return;

        var weapons = new List<WeaponData>();
        DB_무기.ForEachEntity(row =>
        {
            if (row == null || string.IsNullOrEmpty(row.f_무기ID)) return;
            var data = WeaponData.FromDB(row);
            if (data != null && data.chapter == chapter) weapons.Add(data);
        });
        weapons.Sort((a, b) => a.grade.CompareTo(b.grade));

        foreach (var w in weapons)
        {
            var slot = Instantiate(weaponSlotPrefab, content);
            slot.Bind(w);
        }
    }

    // ── 공통 유틸 ─────────────────────────────────────────────────────────

    void ClearContent()
    {
        if (content != null)
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);

        if (bossContent != null)
            for (int i = bossContent.childCount - 1; i >= 0; i--)
                Destroy(bossContent.GetChild(i).gameObject);
    }

    static string ChapterToEraName(int chapter) => chapter switch
    {
        1  => "원시",
        2  => "신석기",
        3  => "청동",
        4  => "철기",
        5  => "중세",
        6  => "근대",
        7  => "산업",
        8  => "현대",
        9  => "도시",
        10 => "궤도",
        11 => "우주",
        _  => $"{chapter}챕터",
    };
}
