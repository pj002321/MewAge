using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 장비 인벤토리 슬롯 한 칸.
/// Inspector 구성:
///   - frameImage  : 등급 색상이 적용될 테두리 이미지
///   - iconImage   : 장비 아이콘
///   - civText     : 문명 텍스트 (예: "구석기")
///   - gradeText   : 등급 텍스트 (예: "레어")
/// </summary>
[RequireComponent(typeof(Button))]
public class EquipmentSlotView : MonoBehaviour
{
    [Header("슬롯 UI")]
    public Image           frameImage;    // 등급 색상 테두리
    public Image           iconImage;     // 장비 아이콘
    public Image           categoryIcon;  // 카테고리 아이콘 (RING/NECK/…)
    public TextMeshProUGUI civText;       // 문명 표시 (구석기, 중세 …)
    public TextMeshProUGUI gradeText;     // 등급 표시

    [Header("뽑기 결과 전용")]
    public Image           spinImage;     // 결과 팝업 활성 중 Z축 회전 이미지

    EquipmentData                    _data;
    System.Action<EquipmentSlotView> _onClick;
    Coroutine                        _spinRoutine;

    static readonly Color EmptyFrame       = new Color(0.3f, 0.3f, 0.3f, 0.8f);
    static readonly Color EmptyCategoryIcon = new Color(1f, 1f, 1f, 0.25f);

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() => _onClick?.Invoke(this));
    }

    /// <summary>슬롯에 장비 데이터를 바인딩합니다.</summary>
    /// <param name="placeholderCategory">data가 null일 때 표시할 카테고리 (빈 슬롯 구분용)</param>
    public void Bind(EquipmentData data, System.Action<EquipmentSlotView> onClick,
                     EquipCategory? placeholderCategory = null)
    {
        _data    = data;
        _onClick = onClick;

        bool has = data != null;

        if (iconImage)
        {
            iconImage.enabled = has && data.Icon != null;
            if (has && data.Icon != null) iconImage.sprite = data.Icon;
        }

        if (categoryIcon)
        {
            if (has)
            {
                var catSprite = HomeEquipVisualConfig.Shared?.GetCategoryIcon(data.category);
                categoryIcon.sprite  = catSprite;
                categoryIcon.enabled = catSprite != null;
                categoryIcon.color   = EquipmentData.GradeColor(data.grade);
            }
            else
            {
                categoryIcon.enabled = false;
            }
        }

        if (frameImage)
        {
            frameImage.enabled = has;
            if (has)
            {
                var frameSprite = Resources.Load<Sprite>($"Equip/Frame/{data.grade.ToString().ToLower()}");
                if (frameSprite != null)
                {
                    frameImage.sprite = frameSprite;
                    frameImage.color  = Color.white;
                }
                else
                    frameImage.color = EquipmentData.GradeColor(data.grade);
            }
            else frameImage.color = EmptyFrame;
        }

        if (civText)
            civText.text = has ? data.civilization.ToString() : string.Empty;

        if (gradeText)
        {
            gradeText.text = has ? data.GradeLabel() : (placeholderCategory.HasValue ? CategoryLabel(placeholderCategory.Value) : string.Empty);
            if (has) gradeText.color = EquipmentData.GradeColor(data.grade);
            else     gradeText.color = new Color(1f, 1f, 1f, 0.4f);
        }
    }

    static string CategoryLabel(EquipCategory cat) => cat switch
    {
        EquipCategory.RING  => "반지",
        EquipCategory.NECK  => "목걸이",
        EquipCategory.FOOT  => "발",
        EquipCategory.TOP   => "상의",
        EquipCategory.CLOAK => "망토",
        EquipCategory.HEAD  => "머리",
        _                   => cat.ToString(),
    };

    public EquipmentData Data => _data;

    /// <summary>뽑기 결과 팝업에서 호출 — spinImage를 활성화하고 Z축 회전을 시작합니다.</summary>
    public void StartSpin(float degreesPerSecond = 90f)
    {
        if (spinImage == null) return;
        spinImage.gameObject.SetActive(true);
        if (_spinRoutine != null) StopCoroutine(_spinRoutine);
        _spinRoutine = StartCoroutine(SpinRoutine(degreesPerSecond));
    }

    /// <summary>팝업 닫힐 때 호출 — 회전 중지 및 이미지 비활성화.</summary>
    public void StopSpin()
    {
        if (_spinRoutine != null)
        {
            StopCoroutine(_spinRoutine);
            _spinRoutine = null;
        }
        if (spinImage != null) spinImage.gameObject.SetActive(false);
    }

    IEnumerator SpinRoutine(float degreesPerSecond)
    {
        while (true)
        {
            spinImage.transform.Rotate(0f, 0f, degreesPerSecond * Time.unscaledDeltaTime);
            yield return null;
        }
    }
}
