using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class WareDrawButton : MonoBehaviour
{
    public string          gachaKey  = StructureGacha.KeyNormal;
    public int             drawCount = 1;
    public WareResultPopup resultPopup;   // Inspector 또는 부모 탐색으로 연결

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);
        // resultPopup이 Inspector에 연결되지 않았으면 부모에서 찾기
        if (resultPopup == null)
            resultPopup = GetComponentInParent<WareResultPopup>();
    }

    void OnClick()
    {
        // 팝업이 이미 열려 있으면 중복 클릭 무시
        if (resultPopup != null && resultPopup.gameObject.activeSelf) return;

        if (drawCount <= 1)
        {
            var result = StructureGacha.Draw(gachaKey);
            resultPopup?.ShowSingle(result);
        }
        else
        {
            var results = StructureGacha.DrawMulti(gachaKey, drawCount);
            resultPopup?.ShowMulti(results);
        }
    }
}
