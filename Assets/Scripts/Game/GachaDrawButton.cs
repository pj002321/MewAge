using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 뽑기 버튼에 붙이는 컴포넌트.
/// Start() 시 부모 계층에서 HomeShopPanel 을 찾아 TryDraw() 를 연결.
/// </summary>
[RequireComponent(typeof(Button))]
public class GachaDrawButton : MonoBehaviour
{
    public int         gemCost;
    public EquipBoxType boxType = EquipBoxType.Normal;

    void Awake()
    {
        // 부모 계층에서 HomeShopPanel 탐색 (inactive 포함)
        var panel = GetComponentInParent<HomeShopPanel>(true);
        if (panel == null)
        {
            Debug.LogWarning($"[GachaDrawButton] HomeShopPanel 을 찾을 수 없음: {gameObject.name}");
            return;
        }
        GetComponent<Button>().onClick.AddListener(() => panel.TryDraw(gemCost, boxType));
    }
}
