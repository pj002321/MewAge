using Mewage.DB;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 뽑기 버튼에 붙이는 컴포넌트.
/// Awake() 시 DB_상점에서 boxType에 맞는 가격을 자동으로 읽어옵니다.
/// drawCount가 1 초과면 "정가×n에서 1회 공짜" 가격을 적용합니다.
/// </summary>
[RequireComponent(typeof(Button))]
public class GachaDrawButton : MonoBehaviour
{
    public int          drawCount = 1;   // 1 또는 n
    public EquipBoxType boxType   = EquipBoxType.Normal;

    void Awake()
    {
        var panel = GetComponentInParent<HomeShopPanel>(true);
        if (panel == null)
        {
            Debug.LogWarning($"[GachaDrawButton] HomeShopPanel 을 찾을 수 없음: {gameObject.name}");
            return;
        }

        int unitCost = GetUnitCostFromDB(boxType);
        int totalCost = drawCount <= 1
            ? unitCost
            : unitCost * (drawCount - 1);  // n회 중 1회 공짜

        GetComponent<Button>().onClick.AddListener(() => panel.TryDrawMulti(totalCost, drawCount, boxType));
    }

    static int GetUnitCostFromDB(EquipBoxType boxType)
    {
        string productId = boxType switch
        {
            EquipBoxType.Rare => "EQ_RARE",
            EquipBoxType.Epic => "EQ_EPIC",
            _                 => "EQ_NORMAL",
        };

        var row = DB_상점.FindEntity(e => e.f_상품ID == productId);
        if (row != null) return row.f_가격;

        // DB 행이 없으면 코드 기본값
        return boxType switch
        {
            EquipBoxType.Rare => 200,
            EquipBoxType.Epic => 500,
            _                 => 80,
        };
    }
}
