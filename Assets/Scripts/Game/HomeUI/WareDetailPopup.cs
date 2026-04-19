using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mewage.DB;

/// <summary>
/// 기물 상세 팝업. 정보 확인·강화 담당.
/// 장착은 장착 버튼 → 팝업 닫힘 → HomeWarePanel 프리셋 선택 모드로 처리합니다.
///
/// Inspector 연결:
///   iconImage, frameImage — 아이콘 영역
///   nameText, docuText, eraRoleText — 기본 정보
///   levelText    — "Lv.5 / 20"
///   countText    — "보유 3장"
///   statText     — 현재 주수치
///   nextStatText — 다음 레벨 강화 후 수치
///   upgradeButton, upgradeLabel — 강화 버튼
///   equipButton  — 장착 버튼 (누르면 팝업 닫히고 프리셋 슬롯 선택 모드 진입)
///   synergyText  — 현재 프리셋 시너지 설명
///   closeButton  — 닫기
///   warePanel    — HomeWarePanel 참조 (반드시 연결)
/// </summary>
public class WareDetailPopup : MonoBehaviour
{
    [Header("아이콘")]
    [SerializeField] Image    iconImage;
    [SerializeField] Image    frameImage;

    [Header("정보 텍스트")]
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text docuText;
    [SerializeField] TMP_Text eraRoleText;
    [SerializeField] TMP_Text levelText;
    [SerializeField] TMP_Text countText;

    [Header("수치")]
    [SerializeField] TMP_Text statText;
    [SerializeField] TMP_Text nextStatText;

    [Header("강화")]
    [SerializeField] Button   upgradeButton;
    [SerializeField] TMP_Text upgradeLabel;

    [Header("장착")]
    [SerializeField] Button   equipButton;

    [Header("시너지")]
    [SerializeField] TMP_Text synergyText;

    [Header("닫기")]
    [SerializeField] Button closeButton;

    [Header("연결")]
    [SerializeField] HomeWarePanel warePanel;

    string  _currentId;
    DB_기물 _currentData;
    Coroutine _closeRoutine;

    void Awake()
    {
        closeButton?.onClick.AddListener(() => gameObject.SetActive(false));
        upgradeButton?.onClick.AddListener(OnUpgrade);
        equipButton?.onClick.AddListener(OnEquip);
    }

    // ── 공개 진입점 ──────────────────────────────────────────────────

    public void Show(string id)
    {
        var data = DB_기물.FindEntity(r => r.f_기물ID == id);
        if (data == null) return;
        _currentId   = id;
        _currentData = data;
        transform.localScale = Vector3.one;
        gameObject.SetActive(true);
        Refresh();
    }

    // ── 갱신 ─────────────────────────────────────────────────────────

    void Refresh()
    {
        var mgr   = StructureWareManager.Shared;
        int level = mgr?.GetLevel(_currentId) ?? 1;
        int count = mgr?.GetCount(_currentId) ?? 0;
        bool canUpg  = mgr?.CanUpgrade(_currentId) ?? false;
        bool isMaxLv = level >= StructureWareManager.MaxLevel;

        // 아이콘 (WareVisualConfig)
        var iconSprite = WareVisualConfig.Shared?.GetIcon(_currentId);
        if (iconImage) { iconImage.sprite = iconSprite; iconImage.enabled = iconSprite != null; }

        // 프레임 (WareVisualConfig 등급 스프라이트)
        if (frameImage && _currentData != null)
        {
            var wareGrade   = WareVisualConfig.ParseGrade(_currentData.f_등급);
            var frameSprite = WareVisualConfig.Shared?.GetGradeFrame(wareGrade);
            frameImage.sprite  = frameSprite;
            frameImage.color   = Color.white;
            frameImage.enabled = frameSprite != null;
        }

        // 정보
        string era = StructureWareManager.EraOf(_currentId);
        if (nameText)    nameText.text    = _currentData.f_이름;
        if (docuText)    docuText.text    = _currentData.f_설명;
        if (eraRoleText) eraRoleText.text = $"{StructureWareManager.EraName(era)}  |  {_currentData.f_역할}";
        if (levelText)   levelText.text   = isMaxLv ? "Lv.MAX" : $"Lv.{level} / {StructureWareManager.MaxLevel}";
        if (countText)   countText.text   = $"보유 {count}장";

        // 수치
        float main = mgr?.GetMainValue(_currentData, level) ?? _currentData.f_주수치;
        if (statText) statText.text = $"주수치  {main:F0}     보조  {_currentData.f_보조수치:F1}";

        // 다음 레벨 미리보기
        if (nextStatText)
        {
            if (!isMaxLv)
            {
                float nextMain = mgr?.GetMainValue(_currentData, level + 1) ?? main;
                nextStatText.text = $"Lv.{level + 1} 강화 시  →  {nextMain:F0}";
                nextStatText.gameObject.SetActive(true);
            }
            else
            {
                nextStatText.gameObject.SetActive(false);
            }
        }

        // 강화 버튼
        int upgCost = StructureWareManager.GetUpgradeCost(level);
        if (upgradeButton) upgradeButton.interactable = canUpg;
        if (upgradeLabel)
            upgradeLabel.text = isMaxLv
                ? "최대 레벨"
                : $"강화  ({count} / {upgCost}장)";

        // 시너지 정보
        if (synergyText)
        {
            var sb = new System.Text.StringBuilder();
            string eraSyn = mgr?.GetEraSynergyDescription();
            if (!string.IsNullOrEmpty(eraSyn)) sb.AppendLine(eraSyn);
            var tagSyns = mgr?.GetRoleTagSynergies();
            if (tagSyns != null) foreach (var s in tagSyns) sb.AppendLine(s);
            synergyText.text = sb.Length > 0 ? sb.ToString().TrimEnd() : "시너지 없음";
        }
    }

    // ── 버튼 핸들러 ──────────────────────────────────────────────────

    void OnUpgrade()
    {
        if (StructureWareManager.Shared?.TryUpgrade(_currentId) == true)
            Refresh();
    }

    void OnEquip()
    {
        if (_closeRoutine != null) StopCoroutine(_closeRoutine);
        _closeRoutine = StartCoroutine(EquipAndClose());
    }

    IEnumerator EquipAndClose()
    {
        Vector3 original = transform.localScale;

        // 뿅! — 살짝 커졌다가 0으로
        float t = 0f;
        while (t < 0.08f)
        {
            t += Time.unscaledDeltaTime;
            transform.localScale = original * Mathf.Lerp(1f, 1.12f, t / 0.08f);
            yield return null;
        }
        t = 0f;
        while (t < 0.14f)
        {
            t += Time.unscaledDeltaTime;
            transform.localScale = original * Mathf.Lerp(1.12f, 0f, t / 0.14f);
            yield return null;
        }

        gameObject.SetActive(false);
        transform.localScale = original;

        warePanel?.BeginPresetSelect(_currentId);
    }
}
