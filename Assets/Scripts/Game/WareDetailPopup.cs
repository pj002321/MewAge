using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mewage.DB;

/// <summary>
/// 기물 상세 팝업.
/// 아이콘 / 이름 / 시대·역할 / ★ 등급 / 수치 / 강화 / 분해 / 프리셋 등록
///
/// Inspector 연결:
///   iconImage, frameImage — 아이콘 영역
///   nameText, docuText, eraRoleText, starText, countText — 정보 텍스트
///   statText    — 현재 수치
///   nextStatText — 다음 ★ 강화 시 수치 미리보기
///   upgradeButton, upgradeLabel — 강화 (3개 필요)
///   dismantleButton — 분해 (+50골드)
///   presetButtons[4], presetLabels[4] — 슬롯 1~4 등록/해제
///   closeButton — 닫기
/// </summary>
public class WareDetailPopup : MonoBehaviour
{
    static readonly Color ColStar1 = Color.white;
    static readonly Color ColStar2 = new Color(1f, 0.84f, 0f, 1f);
    static readonly Color ColStar3 = new Color(0.6f, 0f,   1f, 1f);

    [Header("아이콘")]
    [SerializeField] Image    iconImage;
    [SerializeField] Image    frameImage;

    [Header("정보 텍스트")]
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text docuText;
    [SerializeField] TMP_Text eraRoleText;
    [SerializeField] TMP_Text starText;
    [SerializeField] TMP_Text countText;

    [Header("수치")]
    [SerializeField] TMP_Text statText;
    [SerializeField] TMP_Text nextStatText;

    [Header("강화")]
    [SerializeField] Button   upgradeButton;
    [SerializeField] TMP_Text upgradeLabel;

    [Header("분해")]
    [SerializeField] Button   dismantleButton;

    [Header("프리셋 슬롯 (0~3)")]
    [SerializeField] Button[]   presetButtons  = new Button[4];
    [SerializeField] TMP_Text[] presetLabels   = new TMP_Text[4];

    [Header("닫기")]
    [SerializeField] Button closeButton;

    string   _currentId;
    DB_기물  _currentData;

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

    // ── 갱신 ────────────────────────────────────────────────────────

    void Refresh()
    {
        var mgr   = StructureWareManager.Shared;
        int star  = mgr?.GetStar(_currentId) ?? 1;
        int count = mgr?.GetCount(_currentId) ?? 0;
        bool can  = mgr?.CanUpgrade(_currentId) ?? false;

        // 아이콘
        var sprite = StructureWareManager.LoadIcon(_currentId);
        if (iconImage)  { iconImage.sprite = sprite; iconImage.enabled = sprite != null; }
        if (frameImage)   frameImage.color = StarColor(star);

        // 정보
        string era = StructureWareManager.EraOf(_currentId);
        if (nameText)    nameText.text    = _currentData.f_이름;
        if (docuText)    docuText.text    = _currentData.f_설명;
        if (eraRoleText) eraRoleText.text = $"{StructureWareManager.EraName(era)}  |  {_currentData.f_역할}";
        if (starText)    starText.text    = Stars(star);
        if (countText)   countText.text   = $"보유 {count}개";

        // 수치
        float main = mgr?.GetMainValue(_currentData, star) ?? _currentData.f_주수치;
        float sub  = _currentData.f_보조수치;
        if (statText) statText.text = $"주수치  {main:F0}     보조  {sub:F1}";

        // 다음 ★ 미리보기
        if (nextStatText)
        {
            if (star < 3)
            {
                float nextMain = mgr?.GetMainValue(_currentData, star + 1) ?? main;
                nextStatText.text = $"★{star + 1} 강화 시  주수치 → {nextMain:F0}";
                nextStatText.gameObject.SetActive(true);
            }
            else
            {
                nextStatText.gameObject.SetActive(false);
            }
        }

        // 강화 버튼
        if (upgradeButton) upgradeButton.interactable = can;
        if (upgradeLabel)
            upgradeLabel.text = star >= 3
                ? "최대 강화 달성"
                : $"강화    ({count} / 3개)";

        // 프리셋 버튼
        for (int i = 0; i < presetButtons.Length && i < 4; i++)
        {
            if (presetButtons[i] == null) continue;
            bool inSlot = mgr?.GetPreset(i) == _currentId;
            if (presetLabels != null && i < presetLabels.Length && presetLabels[i] != null)
                presetLabels[i].text = inSlot ? $"슬롯 {i + 1}  해제" : $"슬롯 {i + 1}";
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
        if ((StructureWareManager.Shared?.GetCount(_currentId) ?? 0) > 0)
            Refresh();
        else
            gameObject.SetActive(false);
    }

    void OnPreset(int slot)
    {
        var mgr = StructureWareManager.Shared;
        if (mgr == null) return;
        if (mgr.GetPreset(slot) == _currentId)
            mgr.ClearPreset(slot);
        else
            mgr.SetPreset(slot, _currentId);
        Refresh();
    }

    // ── 유틸 ────────────────────────────────────────────────────────

    static Color  StarColor(int star) => star switch { 2 => ColStar2, 3 => ColStar3, _ => ColStar1 };
    static string Stars(int star)     => new string('★', star) + new string('☆', 3 - star);
}
