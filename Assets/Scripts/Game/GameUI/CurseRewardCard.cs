using UnityEngine;
using TMPro;
using Mewage.DB;
using UnityEngine.UI;
using System.Collections;
using System.Text.RegularExpressions;

/// <summary>
/// 저주/축복 카드 UI.
/// Inspector 연결: titleText, blessingText, curseText, iconImg, highlightImg (하이라이트 이미지)
/// </summary>
public class CurseRewardCard : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text blessingText;
    [SerializeField] private TMP_Text curseText;
    [SerializeField] private Image iconImg;

    [Header("선택 애니메이션")]
    [SerializeField] private float spinDuration = 0.35f;
    [SerializeField] private float selectScale  = 1.08f;

    private Coroutine _spinCoroutine;

    private void OnDisable()
    {
        if (_spinCoroutine != null) { StopCoroutine(_spinCoroutine); _spinCoroutine = null; }
        transform.localEulerAngles = Vector3.zero;
        transform.localScale       = Vector3.one;
    }

    public void PlaySelectEffect()
    {
        if (_spinCoroutine != null) StopCoroutine(_spinCoroutine);
        _spinCoroutine = StartCoroutine(SpinRoutine());
    }

    private IEnumerator SpinRoutine()
    {
        float elapsed    = 0f;
        Vector3 endScale = Vector3.one * selectScale;

        while (elapsed < spinDuration)
        {
            float t    = elapsed / spinDuration;
            float ease = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

            transform.localEulerAngles = new Vector3(0f, Mathf.Lerp(0f, 360f, ease), 0f);
            transform.localScale       = Vector3.Lerp(Vector3.one, endScale, ease);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        transform.localEulerAngles = Vector3.zero;
        transform.localScale       = endScale;
        _spinCoroutine = null;
    }

    public void Setup(DB_증강 data)
    {
        if (data == null) return;
        if (titleText)    titleText.text    = data.f_이름;

        if (iconImg)
        {
            string augIdStr = data.f_증강ID.ToString();
            Sprite sprite = ImprovVisualConfig.Shared?.GetSprite(augIdStr)
                         ?? Resources.Load<Sprite>("AUG/" + augIdStr.ToLower());
            iconImg.sprite  = sprite;
            iconImg.enabled = sprite != null;
        }

        if (blessingText) 
        {
            blessingText.text = ColorizeNumbers(FormatEffect(data.f_축복, data.f_축복수치, curse: false), false);
            blessingText.color = Color.white;
        }
        if (curseText)    
        {
            curseText.text    = ColorizeNumbers(FormatEffect(data.f_저주,  data.f_저주수치, curse: true), true);
            curseText.color = Color.white;
        }

        var button = GetComponent<Button>();
        if (button) button.interactable = true;
    }

    static string FormatEffect(string augId, float value, bool curse)
    {
        if (value <= 0f || string.IsNullOrEmpty(augId)) return "-";
        string statName = PlayerStatManager.GetStatName(AugIdToStat(augId));
        string label = curse ? "감소" : "증가";
        return $"{statName}  {value:0.#}% {label}";
    }

    public static StatType AugIdToStat(string id) => id switch
    {
        "AUG_ATK"          => StatType.ATK,
        "AUG_DEF"          => StatType.DEF,
        "AUG_MAX_HP"       => StatType.MaxHP,
        "AUG_SIZE_HP_UP"   => StatType.MaxHP,
        "AUG_SIZE_HP_DOWN" => StatType.MaxHP,
        "AUG_SPD_MOV"      => StatType.MoveSpeed,
        "AUG_SPD_ATK"      => StatType.AtkSpeed,
        "AUG_EVA"          => StatType.Evasion,
        "AUG_CRI_RATE"     => StatType.CritChance,
        "AUG_CRI_DMG"      => StatType.CritDmg,
        "AUG_REWARD_RNG"   => StatType.MagnetRadius,
        "AUG_DOT_DMG"      => StatType.BleedPower,
        _                  => StatType.ATK,
    };

    /// <summary>텍스트의 숫자를 축복은 녹색, 저주는 빨강으로 채색합니다.</summary>
    private static string ColorizeNumbers(string text, bool curse)
    {
        return Regex.Replace(text, @"\d+(?:\.\d+)?%", match =>
        {
            string color = curse ? "#FF5555" : "#55FF88";
            return $"<color={color}>{match.Value}</color>";
        });
    }
}
