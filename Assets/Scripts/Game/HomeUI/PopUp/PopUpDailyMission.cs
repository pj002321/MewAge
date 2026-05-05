using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 일일 미션 팝업.
/// Inspector: contentsRoot, slotPrefab, rewardTransform,
///            questGauge, chests(6개)
/// </summary>
public class PopUpDailyMission : MonoBehaviour
{
    [Header("슬롯 생성")]
    [SerializeField] Transform         contentsRoot;
    [SerializeField] DailyMissionSlot  slotPrefab;
    [SerializeField] RectTransform     rewardTransform;  // 고스트 이미지 도착 지점

    [Header("퀘스트 전체 게이지")]
    [SerializeField] Slider questGauge;

    [Header("상자 이미지 6개")]
    [SerializeField] Image[]  chestImages;
    [SerializeField] Sprite   chestOpenSprite;
    [SerializeField] Sprite   chestCloseSprite;

    List<DailyMissionData> _missions;
    int _totalBadges;
    int _earnedBadges;

    private ScalablePopup _popup;
    ScalablePopup Popup => _popup != null ? _popup : (_popup = GetComponent<ScalablePopup>());

    public void Show()
    {
        Popup.Show();
        LoadTempMissions();
        BuildSlots();
        RefreshGauge();
    }

    public void Hide() => Popup.Hide();

    // ── 임시 데이터 ──────────────────────────────────────────────────

    void LoadTempMissions()
    {
        _missions = new List<DailyMissionData>
        {
            new DailyMissionData { id="m1", description="적 10마리 처치",  badgeCount=5,  current=10, goal=10  },
            new DailyMissionData { id="m2", description="웨이브 5 클리어", badgeCount=10, current=3,  goal=5   },
            new DailyMissionData { id="m3", description="골드 500 획득",   badgeCount=5,  current=500,goal=500 },
            new DailyMissionData { id="m4", description="장비 1개 뽑기",   badgeCount=15, current=0,  goal=1   },
            new DailyMissionData { id="m5", description="게임 3회 시작",   badgeCount=10, current=2,  goal=3   },
            new DailyMissionData { id="m6", description="기물 1회 배치",   badgeCount=5,  current=1,  goal=1   },
        };
        _totalBadges  = 0;
        _earnedBadges = 0;
        foreach (var m in _missions)
        {
            _totalBadges += m.badgeCount;
            if (m.claimed) _earnedBadges += m.badgeCount;
        }
    }

    // ── 슬롯 생성 ────────────────────────────────────────────────────

    void BuildSlots()
    {
        if (contentsRoot == null || slotPrefab == null) return;

        foreach (Transform child in contentsRoot)
            Destroy(child.gameObject);

        foreach (var data in _missions)
        {
            var slot = Instantiate(slotPrefab, contentsRoot);
            slot.Setup(data, rewardTransform);
            slot.OnClaimed += OnSlotClaimed;
        }
    }

    // ── 수령 콜백 ────────────────────────────────────────────────────

    void OnSlotClaimed(DailyMissionSlot slot)
    {
        _earnedBadges += slot.Data.badgeCount;
        RefreshGauge();
    }

    // ── 게이지 & 상자 ────────────────────────────────────────────────

    void RefreshGauge()
    {
        float ratio = _totalBadges > 0 ? Mathf.Clamp01((float)_earnedBadges / _totalBadges) : 0f;

        if (questGauge != null)
        {
            questGauge.minValue = 0f;
            questGauge.maxValue = 1f;
            questGauge.value    = ratio;
        }

        if (chestImages == null) return;
        int count = chestImages.Length;
        for (int i = 0; i < count; i++)
        {
            if (chestImages[i] == null) continue;
            float threshold = (float)(i + 1) / count;
            bool  open      = ratio >= threshold;
            var   spr       = open ? chestOpenSprite : chestCloseSprite;
            if (spr != null) chestImages[i].sprite = spr;
        }
    }
}
