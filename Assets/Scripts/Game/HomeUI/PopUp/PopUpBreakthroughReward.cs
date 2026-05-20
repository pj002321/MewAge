using System.Collections;
using System.Collections.Generic;
using Mewage.DB;
using TMPro;
using UnityEngine;

/// <summary>
/// 스테이지 돌파 보상 팝업 (10웨이브 단위 마일스톤).
///
/// DB_상점 f_상점타입="BREAKTHROUGH" 행을 읽어 수령 가능한 목록을 구성합니다.
/// 수령 버튼 클릭 → 보상 지급 → 다음 항목으로 수직 스크롤 전환.
///
/// DB 필드 매핑 (임시):
///   f_V3  = 달성 필요 웨이브 (10·20·30…)
///   f_V4  = 챕터 번호 (1·2·3…)
///   f_V1  = 보상 수량
///
/// Inspector 연결:
///   slotPrefab   — RewardSlot 프리팹
///   headerText   — "챕터 1-1 · 10웨이브 달성" 텍스트
///   headerRect   — 스크롤 애니메이션 대상 RectTransform
/// </summary>
public class PopUpBreakthroughReward : PopUpRewardBase
{
    [SerializeField] private RewardSlot      slotPrefab;
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private RectTransform   headerRect;

    private const float ScrollDuration = 0.3f;

    private List<DB_상점> _claimable = new();
    private int           _currentIndex;

    protected override void Awake()
    {
        base.Awake();
    }

    public new void Show()
    {
        LoadClaimable();

        if (_claimable.Count == 0)
        {
            ShowDummy();
            base.Show();
            return;
        }

        _currentIndex = 0;
        ShowEntry(_claimable[0]);
        base.Show();
    }

    private void ShowDummy()
    {
        ClearRewardContents();
        SpawnSlots(new[]
        {
            new RewardItem("COIN",      1000),
            new RewardItem("KEY_NORMAL",   1),
        }, slotPrefab);
        if (headerText != null) headerText.text = "챕터 1-1 · 10웨이브 달성";
    }

    protected override void OnClaim()
    {
        var entry = _claimable[_currentIndex];
        var item  = RewardItem.FromDB(entry);

        RewardManager.Shared?.GiveReward(item);
        MarkClaimed(ClaimKey(entry));

        ClearRewardContents();

        _currentIndex++;
        if (_currentIndex < _claimable.Count)
        {
            StartCoroutine(ScrollToNext(_claimable[_currentIndex]));
            if (claimButton != null) claimButton.interactable = true;
        }
        else
        {
            StartCoroutine(HideAfterEffect());
        }
    }

    private System.Collections.IEnumerator HideAfterEffect()
    {
        while (Effect.IsPlaying)
            yield return null;
        Hide();
    }

    // ── 내부 ────────────────────────────────────────────────────────────

    private void LoadClaimable()
    {
        _claimable.Clear();
        string chapterId = PlayerPrefs.GetString("SelectedChapterID", "C1_S1");
        int    chapter   = ParseChapterNumber(chapterId);
        int    bestWave  = PlayerPrefs.GetInt($"BestWave_{chapterId}", 0);

        DB_상점.ForEachEntity(row =>
        {
            if (string.IsNullOrEmpty(row.f_C상품ID) || !row.f_C상품ID.StartsWith("BT_")) return;
            if (row.f_V4 != chapter) return;
            if (row.f_V3 > bestWave) return;
            if (IsClaimed(ClaimKey(row))) return;
            _claimable.Add(row);
        });

        _claimable.Sort((a, b) => a.f_V3.CompareTo(b.f_V3));
    }

    private void ShowEntry(DB_상점 row)
    {
        ClearRewardContents();

        var item = RewardItem.FromDB(row);
        SpawnSlots(new[] { item }, slotPrefab);

        if (headerText != null)
        {
            string chapterId = PlayerPrefs.GetString("SelectedChapterID", "C1_S1");
            headerText.text = $"챕터 {FormatChapter(chapterId)} · {row.f_V3}웨이브 달성";
        }
    }

    private IEnumerator ScrollToNext(DB_상점 next)
    {
        if (headerRect == null)
        {
            ShowEntry(next);
            yield break;
        }

        float startY = headerRect.anchoredPosition.y;
        float endY   = startY + 60f;

        for (float t = 0f; t < ScrollDuration; t += Time.unscaledDeltaTime)
        {
            float ratio = Mathf.Clamp01(t / ScrollDuration);
            headerRect.anchoredPosition = new Vector2(
                headerRect.anchoredPosition.x,
                Mathf.Lerp(startY, endY, ratio));
            yield return null;
        }

        ShowEntry(next);
        headerRect.anchoredPosition = new Vector2(headerRect.anchoredPosition.x, startY - 60f);

        for (float t = 0f; t < ScrollDuration; t += Time.unscaledDeltaTime)
        {
            float ratio = Mathf.Clamp01(t / ScrollDuration);
            headerRect.anchoredPosition = new Vector2(
                headerRect.anchoredPosition.x,
                Mathf.Lerp(startY - 60f, startY, ratio));
            yield return null;
        }

        headerRect.anchoredPosition = new Vector2(headerRect.anchoredPosition.x, startY);
    }

    // ── 헬퍼 ────────────────────────────────────────────────────────────

    private static string ClaimKey(DB_상점 row) =>
        $"BT_{PlayerPrefs.GetString("SelectedChapterID", "C1_S1")}_{row.f_V3}_Claimed";

    private static int ParseChapterNumber(string chapterId)
    {
        var parts = chapterId.Split('_');
        if (parts.Length > 0 && parts[0].Length >= 2 &&
            int.TryParse(parts[0].Substring(1), out int n)) return n;
        return 1;
    }

    private static string FormatChapter(string chapterId)
    {
        var parts = chapterId.Split('_');
        if (parts.Length < 2) return chapterId;
        string c = parts[0].Length >= 2 ? parts[0].Substring(1) : parts[0];
        string s = parts[1].Length >= 2 ? parts[1].Substring(1) : parts[1];
        return $"{c}-{s}";
    }
}
