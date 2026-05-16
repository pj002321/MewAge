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
    [LabelText("챕터 필터 (0 = 전체)"), PropertyRange(0, 10)]
    public int filterChapter = 0;

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

    [Button("장착 무기 전체 해제", ButtonSizes.Medium), GUIColor(0.9f, 0.4f, 0.4f)]
    private void UnequipAll()
    {
        var pi = PlayerInventory.Shared;
        if (pi == null) { Debug.LogWarning("[WeaponDebug] PlayerInventory.Shared가 null입니다."); return; }
        int count = pi.Equipped.Count;
        for (int i = count - 1; i >= 0; i--)
            pi.UnequipToInventory(i);
        Debug.Log($"[WeaponDebug] 장착 무기 {count}개 전체 해제됨");
    }

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

        // 장착: 인벤 경유 없이 직접 장착 (OnChanged 1회만 발행)
        pi.QuietEquip(data);
        Debug.Log($"[WeaponDebug] {row.f_이름} {star}★ → 장착됨");
    }

    private IEnumerable<ValueDropdownItem<string>> GetWeaponOptions()
    {
        var list = new List<ValueDropdownItem<string>>();
        DB_무기.ForEachEntity(row =>
        {
            int ch = row.f_챕터 > 0 ? row.f_챕터 : ChapterFromId(row.f_무기ID);
            if (filterChapter > 0 && ch != filterChapter) return;
            list.Add(new ValueDropdownItem<string>(
                $"Ch{ch}/{row.f_등급} | {row.f_이름} ({row.f_무기ID})", row.f_무기ID));
        });
        list.Sort((a, b) => string.Compare(a.Text, b.Text, System.StringComparison.Ordinal));
        return list;
    }

    private static int ChapterFromId(string weaponId)
    {
        if (string.IsNullOrEmpty(weaponId)) return 0;
        var part = weaponId.ToLower().Split('_')[0];
        if (part.StartsWith("ch") && int.TryParse(part.Substring(2), out int n)) return n;
        return 0;
    }
}
