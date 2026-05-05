using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 유물 조각 뽑기 버튼.
/// Inspector: boxType, drawCount, resultPopup, costText 연결.
/// </summary>
[RequireComponent(typeof(Button))]
public class RelicFragDrawButton : MonoBehaviour
{
    [SerializeField] RelicFragBoxType    boxType     = RelicFragBoxType.Normal;
    [SerializeField] int                 drawCount   = 1;
    [SerializeField] RelicFragResultPopup resultPopup;

    [Tooltip("버튼 안의 비용 텍스트 (선택).")]
    [SerializeField] TextMeshProUGUI costText;

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);

        if (resultPopup == null)
            resultPopup = GetComponentInParent<RelicFragResultPopup>();

        int unitCost  = RelicFragGacha.GetCost(boxType);
        int totalCost = drawCount <= 1 ? unitCost : unitCost * drawCount;
        if (costText != null)
            costText.text = $"{totalCost}";
    }

    void OnClick()
    {
        if (resultPopup != null && resultPopup.gameObject.activeSelf) return;

        if (drawCount <= 1)
        {
            var entry = RelicFragGacha.Draw(boxType);
            if (entry.data != null)
                resultPopup?.ShowSingle(entry.data, entry.count);
        }
        else
        {
            var results = RelicFragGacha.DrawMulti(boxType, drawCount);
            resultPopup?.ShowMulti(results);
        }
    }
}
