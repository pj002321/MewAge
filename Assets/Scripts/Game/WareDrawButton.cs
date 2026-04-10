using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class WareDrawButton : MonoBehaviour
{
    public WareBoxType     boxType   = WareBoxType.Normal;
    public int             drawCount = 1;
    public WareResultPopup resultPopup;

    [Tooltip("버튼 안의 비용 텍스트 (선택). 연결하면 DB 가격이 자동 표시됩니다.")]
    public TextMeshProUGUI costText;

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);
        if (resultPopup == null)
            resultPopup = GetComponentInParent<WareResultPopup>();

        int unitCost  = StructureGacha.GetCost(boxType);
        int totalCost = drawCount <= 1 ? unitCost : unitCost * (drawCount - 1);
        if (costText != null)
            costText.text = $"{totalCost}";
    }

    void OnClick()
    {
        if (resultPopup != null && resultPopup.gameObject.activeSelf) return;

        if (drawCount <= 1)
        {
            var result = StructureGacha.Draw(boxType);
            resultPopup?.ShowSingle(result);
        }
        else
        {
            var results = StructureGacha.DrawMulti(boxType, drawCount);
            resultPopup?.ShowMulti(results);
        }
    }
}
