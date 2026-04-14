using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mewage.DB;

/// <summary>
/// 기물 상세 팝업. 정보 확인·강화·분해·프리셋 등록 담당.
/// 합성은 HomeWarePanel 합성 탭에서 처리합니다.
///
/// Inspector 연결:
///   iconImage, frameImage — 아이콘 영역
///   nameText, docuText, eraRoleText — 기본 정보
///   levelText    — "Lv.5 / 20"
///   countText    — "보유 3장"
///   statText     — 현재 주수치
///   nextStatText — 다음 레벨 강화 후 수치
///   upgradeButton, upgradeLabel — 강화 버튼
///   dismantleButton — 분해 (+50골드)
///   synergyText  — 현재 프리셋 시너지 설명
///   presetButtons[4], presetLabels[4] — 슬롯 1~4 등록/해제
///   closeButton  — 닫기
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

    [Header("분해")]
    [SerializeField] Button   dismantleButton;

    [Header("시너지")]
    [SerializeField] TMP_Text synergyText;

    [Header("프리셋 슬롯 (0~3)")]
    [SerializeField] Button[]   presetButtons = new Button[4];
    [SerializeField] TMP_Text[] presetLabels  = new TMP_Text[4];

    [Header("닫기")]
    [SerializeField] Button closeButton;

    string  _currentId;
    DB_기물 _currentData;

    void Awake()
    {
        closeButton?.onClick.AddListener(() => gameObject.SetActive(false));
        upgradeButton?.onClick.AddListener(OnUpgrade);
        dismantleButton?.onClick.AddListener(OnDismantle);

        for (int i = 0; i < presetButtons.Length && i < 4; i++)
        {
            int slot = i;
            presetButtons[i]?.onClick.AddListener(() => OnPreset(slot));
        }
    }

    // ── 공개 진입점 ──────────────────────────────────────────────────

    public void Show(string id)
    {
        var data = DB_기물.FindEntity(r => r.f_기물ID == id);
        if (data == null) return;
        _currentId   = id;
        _currentData = data;
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

        // 아이콘
        var sprite = StructureWareManager.LoadIcon(_currentId);
        if (iconImage)  { iconImage.sprite = sprite; iconImage.enabled = sprite != null; }
        if (frameImage) frameImage.color   = WareSlotView.LevelColor(level);

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

        // 프리셋 버튼
        for (int i = 0; i < presetButtons.Length && i < 4; i++)
        {
            if (presetButtons[i] == null) continue;
            bool inSlot = mgr?.GetPreset(i) == _currentId;
            if (presetLabels != null && i < presetLabels.Length && presetLabels[i] != null)
                presetLabels[i].text = inSlot ? $"슬롯{i + 1} 해제" : $"슬롯{i + 1}";
        }
    }

    // ── 버튼 핸들러 ──────────────────────────────────────────────────

    void OnUpgrade()
    {
        if (StructureWareManager.Shared?.TryUpgrade(_currentId) == true)
            Refresh();
    }

    void OnDismantle()
    {
        StructureWareManager.Shared?.Dismantle(_currentId);
        bool stillOwned = (StructureWareManager.Shared?.GetCount(_currentId) ?? 0) > 0
                       || (StructureWareManager.Shared?.GetLevel(_currentId) ?? 1) > 1;
        if (stillOwned) Refresh();
        else            gameObject.SetActive(false);
    }

    void OnPreset(int slot)
    {
        var mgr = StructureWareManager.Shared;
        if (mgr == null) return;
        if (mgr.GetPreset(slot) == _currentId) mgr.ClearPreset(slot);
        else                                    mgr.SetPreset(slot, _currentId);
        Refresh();
    }
}
