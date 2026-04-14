using System.Collections.Generic;
using Mewage.DB;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 런타임 중 Inspector에서 원하는 무기를 즉시 장착하거나 인벤토리에 추가하는 디버그 컴포넌트.
/// 씬의 아무 GameObject에 붙인 뒤 플레이 모드에서 Inspector를 통해 사용합니다.
/// </summary>
public class WeaponDebugInspector : MonoBehaviour
{
    [BoxGroup("무기 선택")]
    [ValueDropdown(nameof(GetWeaponOptions))]
    [LabelText("무기")]
    public string selectedWeaponId;

    [BoxGroup("무기 선택")]
    [LabelText("★ 등급"), PropertyRange(1, 5)]
    public int star = 1;

    [Button("장착하기", ButtonSizes.Large), GUIColor(0.3f, 0.9f, 0.3f)]
    private void Equip() => GiveWeapon(toInventory: false);

    [Button("인벤토리에 추가", ButtonSizes.Medium)]
    private void AddToInventory() => GiveWeapon(toInventory: true);

    // ── 내부 ──────────────────────────────────────────────────────

    private void GiveWeapon(bool toInventory)
    {
        if (string.IsNullOrEmpty(selectedWeaponId))
        {
            Debug.LogWarning("[WeaponDebug] 무기를 먼저 선택하세요.");
            return;
        }

        var pi = PlayerInventory.Shared;
        if (pi == null)
        {
            Debug.LogWarning("[WeaponDebug] PlayerInventory.Shared가 null입니다.");
            return;
        }

        DB_무기 row = null;
        DB_무기.ForEachEntity(r => { if (r.f_무기ID == selectedWeaponId) row = r; });
        if (row == null)
        {
            Debug.LogWarning($"[WeaponDebug] ID '{selectedWeaponId}' 를 DB에서 찾을 수 없습니다.");
            return;
        }

        var data = WeaponData.FromDB(row);
        if (star > 1)
            data = data.UpgradedTo(star);

        if (toInventory)
        {
            if (pi.TryAddToInventory(data))
                Debug.Log($"[WeaponDebug] {row.f_이름} {star}★ → 인벤토리에 추가됨");
            else
                Debug.LogWarning("[WeaponDebug] 인벤토리가 가득 찼습니다 (최대 10개).");
            return;
        }

        // 장착: 인벤 경유 후 즉시 EquipFromInventory
        if (!pi.TryAddToInventory(data))
            pi.ForceAddToInventory(data);

        pi.EquipFromInventory(pi.Inventory.Count - 1);
        Debug.Log($"[WeaponDebug] {row.f_이름} {star}★ → 장착됨");
    }

    private IEnumerable<ValueDropdownItem<string>> GetWeaponOptions()
    {
        var list = new List<ValueDropdownItem<string>>();
        DB_무기.ForEachEntity(row =>
            list.Add(new ValueDropdownItem<string>(
                $"[Ch{row.f_챕터}] {row.f_등급} | {row.f_이름}", row.f_무기ID)));
        return list;
    }
}
