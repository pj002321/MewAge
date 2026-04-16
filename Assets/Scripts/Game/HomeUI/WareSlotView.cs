using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Mewage.DB;

/// <summary>
/// 기물 슬롯 하나를 표시하는 재사용 컴포넌트.
/// 프리셋 슬롯 / 인벤토리 목록 양쪽에서 사용합니다.
///
/// Inspector 연결:
///   backgroundImage — 배경 (시대 색상 틴트)
///   frameImage      — 등급 프레임 Image (WareVisualConfig 스프라이트 + 등급 색상)
///   iconImage       — 기물 아이콘 (WareVisualConfig ID별 할당)
///   roleImage       — 역할 아이콘 Image (WareVisualConfig 스프라이트)
///   eraText         — 시대 약자 텍스트 (구석기→구, 현대→현 등)
///   gradeText       — 등급 텍스트 (일반 / 레어 / 에픽 / 신화 / S등급)
///   nameText        — 기물 이름
///   starText        — 강화 레벨 (Lv.N / Lv.MAX)
///   countText       — 보유 수량 (×3)
///   button          — 클릭
/// </summary>
[RequireComponent(typeof(Button))]
public class WareSlotView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    static readonly Color ColStar1 = Color.white;
    static readonly Color ColStar2 = new Color(1f, 0.84f, 0f, 1f);
    static readonly Color ColStar3 = new Color(0.6f, 0f,   1f, 1f);

    [Header("슬롯 UI (EquipmentSlotView 스타일)")]
    public Image           backgroundImage; // 전체적인 배경 (선택 사항)
    public Image           frameImage;      // 등급 프레임 (WareVisualConfig 스프라이트)
    public Image           iconImage;       // 기물 아이콘
    public Image           roleImage;       // 역할 아이콘 (WareVisualConfig 스프라이트)
    public TextMeshProUGUI eraText;         // 시대 표시 (구, 신, 현 …)
    public TextMeshProUGUI gradeText;       // 등급 표시 (Normal, Rare, Epic …)
    public TextMeshProUGUI nameText;        // 기물 이름
    public TextMeshProUGUI starText;        // ★ 표시 (★★★)
    public TextMeshProUGUI countText;       // 보유 수량 (x10)

    string         _id;
    Action<string> _onClick;
    public int     PresetSlotIndex = -1; // 프리셋 슬롯일 경우 0-3, 인벤토리일 경우 -1

    public string Id => _id;

    private CanvasGroup _canvasGroup;
    private Transform _draggingIcon;

    private Coroutine _scaleUpCoroutine;

    /// <summary>스케일업 애니메이션 실행 (장비 뽑기처럼)</summary>
    public void PlayScaleUp(float delay = 0f, float duration = 0.3f, float maxScale = 1.5f)
    {
        if (_scaleUpCoroutine != null) StopCoroutine(_scaleUpCoroutine);
        _scaleUpCoroutine = StartCoroutine(ScaleUpRoutine(delay, duration, maxScale));
    }

    private System.Collections.IEnumerator ScaleUpRoutine(float delay, float duration, float maxScale)
    {
        transform.localScale = Vector3.zero;
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one * maxScale, t);
            yield return null;
        }

        elapsed = 0f;
        float bounceDuration = duration * 0.5f;
        while (elapsed < bounceDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / bounceDuration);
            transform.localScale = Vector3.Lerp(Vector3.one * maxScale, Vector3.one, t);
            yield return null;
        }

        transform.localScale = Vector3.one;
    }

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() => _onClick?.Invoke(_id));
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    // ── 바인딩 ──────────────────────────────────────────────────────

    /// <summary>기물 데이터를 슬롯에 바인딩합니다.</summary>
    public void Bind(string id, Action<string> onClick, int? countOverride = null, int? starOverride = null)
    {
        _id      = id;
        _onClick = onClick;

        // id가 null이면 빈 슬롯 처리
        if (string.IsNullOrEmpty(id))
        {
            BindEmpty(-1);
            return;
        }

        var mgr   = StructureWareManager.Shared;
        int star  = starOverride ?? (mgr?.GetLevel(id) ?? 1);
        int count = mgr?.GetCount(id) ?? 0;
        string era = StructureWareManager.EraOf(id);

        // DB에서 기물 데이터 직접 로드 (FindEntity 사용)
        DB_기물 data = DB_기물.FindEntity(row => row.f_기물ID == id);
        if (data == null)
        {
            Debug.LogWarning($"[WareSlotView] DB_기물을 찾을 수 없음: {id}");
            BindEmpty(-1);
            return;
        }

        // 1. 배경
        if (backgroundImage)
        {
            backgroundImage.enabled = true;
            backgroundImage.color   = EraColor(era) * new Color(1, 1, 1, 0.2f) + new Color(0.1f, 0.1f, 0.2f, 0.8f);
        }

        // 2. 프레임 등급 (WareVisualConfig 스프라이트, 색상 없음)
        if (frameImage)
        {
            var cfg         = WareVisualConfig.Shared;
            var wareGrade   = WareVisualConfig.ParseGrade(data.f_등급);
            var frameSprite = PresetSlotIndex >= 0
                ? cfg?.GetPresetFrame(wareGrade)
                : cfg?.GetGradeFrame(wareGrade);
            frameImage.sprite  = frameSprite;
            frameImage.color   = Color.white;
            frameImage.enabled = frameSprite != null;
        }
        else Debug.LogWarning($"[WareSlotView] {gameObject.name}: frameImage가 연결되지 않았습니다.");

        // 3. 아이콘 (WareVisualConfig ID별 할당)
        var iconSprite = WareVisualConfig.Shared?.GetIcon(id);
        if (iconImage)
        {
            iconImage.sprite  = iconSprite;
            iconImage.enabled = iconSprite != null;
        }

        // 4. 시대 텍스트
        if (eraText)
        {
            eraText.enabled = true;
            eraText.text    = EraShort(era);
            eraText.color   = EraColor(era);
        }

        // 5. 등급 텍스트
        if (gradeText && data != null)
        {
            gradeText.enabled = true;
            gradeText.text    = GradeKorean(data.f_등급);
            gradeText.color   = GradeColor(data.f_등급);
        }
        else if (gradeText) gradeText.enabled = false;

        // 6. 역할 아이콘
        if (roleImage)
        {
            var wareRole   = WareVisualConfig.ParseRole(data.f_역할);
            var roleSprite = WareVisualConfig.Shared?.GetRoleIcon(wareRole);
            roleImage.sprite  = roleSprite;
            roleImage.enabled = roleSprite != null;
        }

        // 7. 기물 이름
        if (nameText)
        {
            nameText.enabled = true;
            nameText.text    = data.f_이름;
        }

        // 8. 강화 레벨
        if (starText)
        {
            starText.enabled = true;
            starText.text    = LevelText(star);
            starText.color   = LevelColor(star);
        }

        // 9. 수량
        if (countText)
        {
            int displayCount = countOverride ?? count;
            countText.text    = displayCount > 1 ? $"×{displayCount}" : "";
            countText.enabled = displayCount > 1;
        }

        gameObject.SetActive(true);
    }

    /// <summary>빈 프리셋 슬롯 표시.</summary>
    public void BindEmpty(int presetSlot)
    {
        _id = null;
        // _onClick은 유지 (프리셋 편집 용도)

        if (backgroundImage) backgroundImage.color   = new Color(0.1f, 0.1f, 0.15f, 0.5f);
        if (frameImage)    { frameImage.sprite = null; frameImage.color = Color.white; frameImage.enabled = false; }
        if (iconImage)       iconImage.enabled    = false;
        if (roleImage)       roleImage.enabled    = false;
        if (eraText)         eraText.text = "";
        if (gradeText)       gradeText.enabled = false;
        if (nameText)        nameText.text = "(선택 안 함)";
        if (nameText)        nameText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
        if (starText)        starText.text = "";
        if (countText)       countText.text = "";

        gameObject.SetActive(true);
    }

    // ── 드래그 앤 드롭 ────────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(_id)) return; // 빈 슬롯은 드래그 불가

        _canvasGroup.alpha = 0.5f;

        // 드래그 아이콘 생성 (아이콘 복제)
        if (iconImage != null && iconImage.sprite != null)
        {
            _draggingIcon = Instantiate(iconImage.gameObject, transform.parent.parent).transform; // Canvas 아래
            _draggingIcon.GetComponent<Image>().sprite = iconImage.sprite;
            _draggingIcon.GetComponent<Image>().raycastTarget = false;
            _draggingIcon.localScale = Vector3.one * 1.2f; // 약간 크게
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_draggingIcon != null)
        {
            _draggingIcon.position = eventData.position;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _canvasGroup.alpha = 1f;

        if (_draggingIcon != null)
        {
            Destroy(_draggingIcon.gameObject);
            _draggingIcon = null;
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        WareSlotView draggedSlot = eventData.pointerDrag?.GetComponent<WareSlotView>();
        if (draggedSlot != null && !string.IsNullOrEmpty(draggedSlot._id) && PresetSlotIndex >= 0)
        {
            // 프리셋 슬롯에 드롭 시 장착
            StructureWareManager.Shared?.SetPreset(PresetSlotIndex, draggedSlot._id);
            // HomeWarePanel 갱신 필요하지만, OnChanged 이벤트로 자동 갱신됨
        }
    }

    // ── 정적 유틸 ────────────────────────────────────────────────────

    // ── 레벨 표시 유틸 ───────────────────────────────────────────────

    /// <summary>레벨 구간에 따른 텍스트 색상 (1-7=흰, 8-14=골드, 15-20=퍼플)</summary>
    public static Color LevelColor(int level)
    {
        if (level >= 15) return ColStar3;
        if (level >= 8)  return ColStar2;
        return ColStar1;
    }

    static string LevelText(int level) =>
        level >= StructureWareManager.MaxLevel ? $"Lv.MAX" : $"Lv.{level}";

    public static Color GradeColor(string grade) => grade switch
    {
        "NORMAL" => new Color(0.2f, 0.8f, 0.2f, 1f),   // 녹색
        "RARE"   => new Color(0.2f, 0.5f, 1.0f, 1f),   // 파랑
        "EPIC"   => new Color(0.8f, 0.4f, 1.0f, 1f),   // 보라
        "LEGEND" => new Color(1.0f, 1.0f, 0.0f, 1f),   // 노랑
        "S"      => new Color(1.0f, 0.4f, 0.1f, 1f),   // 주황-레드 (최상위)
        _        => new Color(0.8f, 0.8f, 0.8f, 1f),   // 기본 (흰색)
    };

    public static string GradeKorean(string grade) => grade switch
    {
        "NORMAL" => "일반",
        "RARE"   => "레어",
        "EPIC"   => "에픽",
        "LEGEND" => "신화",
        "S"      => "S등급",
        _        => "일반",
    };
    public static Color EraColor(string era) => era switch
    {
        "C1" => new Color(0.72f, 0.50f, 0.28f, 1f),   // 구석기
        "C2" => new Color(0.40f, 0.65f, 0.35f, 1f),   // 중석기
        "C3" => new Color(0.20f, 0.65f, 0.60f, 1f),   // 신석기
        "C4" => new Color(0.45f, 0.30f, 0.65f, 1f),   // 중세
        "C5" => new Color(0.25f, 0.45f, 0.75f, 1f),   // 근대
        "C6" => new Color(0.10f, 0.75f, 0.90f, 1f),   // 현대
        "C7" => new Color(0.55f, 0.20f, 0.90f, 1f),   // 우주
        _    => new Color(0.40f, 0.40f, 0.40f, 1f),
    };

    public static string EraShort(string era) => era switch
    {
        "C1" => "구", "C2" => "중석", "C3" => "신",
        "C4" => "중세", "C5" => "근", "C6" => "현", "C7" => "우",
        _ => era
    };

    public static Color RoleColor(string role) => role switch
    {
        "CC"   => new Color(0.20f, 0.55f, 0.90f, 1f),
        "버스트" => new Color(0.90f, 0.35f, 0.15f, 1f),
        "지속"  => new Color(0.20f, 0.75f, 0.40f, 1f),
        "버프"  => new Color(0.85f, 0.75f, 0.10f, 1f),
        _      => new Color(0.45f, 0.45f, 0.45f, 1f),
    };
}


