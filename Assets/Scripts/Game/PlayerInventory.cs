using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어의 장착 무기(최대 6)와 인벤토리(최대 10)를 관리합니다.
/// </summary>
public class PlayerInventory : MonoBehaviourSingleton<PlayerInventory>
{
    public const int MaxEquipped  = 6;
    public const int MaxInventory = 10;

    private readonly List<WeaponData> equipped  = new();
    private readonly List<WeaponData> inventory = new();

    /// <summary>장착/인벤 변경 시 발행됩니다. UpgradePanel이 구독합니다.</summary>
    public System.Action OnChanged;

    protected override bool DontDestroy => false;

    public IReadOnlyList<WeaponData> Equipped  => equipped;
    public IReadOnlyList<WeaponData> Inventory => inventory;
    public bool InventoryFull => inventory.Count >= MaxInventory;

    // ────────────────────────────────────────────────────────────────
    // 인벤토리 추가
    // ────────────────────────────────────────────────────────────────
    public bool TryAddToInventory(WeaponData data)
    {
        if (inventory.Count >= MaxInventory) return false;
        inventory.Add(data);
        OnChanged?.Invoke();
        return true;
    }

    // ────────────────────────────────────────────────────────────────
    // 인벤토리 → 장착 (장착 슬롯 꽉 차면 마지막 슬롯과 교체)
    // ────────────────────────────────────────────────────────────────
    public void EquipFromInventory(int invenIndex)
    {
        if (invenIndex < 0 || invenIndex >= inventory.Count) return;
        var weapon = inventory[invenIndex];

        // 첫 번째 빈(null) 슬롯 탐색, 없으면 MaxEquipped 이내 append
        int emptySlot = -1;
        for (int i = 0; i < equipped.Count; i++)
            if (equipped[i] == null) { emptySlot = i; break; }
        if (emptySlot < 0 && equipped.Count < MaxEquipped)
            emptySlot = equipped.Count;

        if (emptySlot >= 0)
        {
            if (emptySlot == equipped.Count) equipped.Add(weapon);
            else equipped[emptySlot] = weapon;
            inventory.RemoveAt(invenIndex);
        }
        else
        {
            // 6칸 모두 가득 찬 경우 — 마지막 슬롯과 교체
            var displaced = equipped[equipped.Count - 1];
            equipped[equipped.Count - 1] = weapon;
            inventory[invenIndex] = displaced;
        }
        OnChanged?.Invoke();
    }

    // ────────────────────────────────────────────────────────────────
    // 인벤토리 → 팔기 (판매가 = price / 2)
    // ────────────────────────────────────────────────────────────────
    public int SellFromInventory(int invenIndex)
    {
        if (invenIndex < 0 || invenIndex >= inventory.Count) return 0;
        int sellPrice = Mathf.Max(1, inventory[invenIndex].price);
        inventory.RemoveAt(invenIndex);
        PlayerStats.Shared?.AddGold(sellPrice);
        OnChanged?.Invoke();
        return sellPrice;
    }

    // ────────────────────────────────────────────────────────────────
    // 장착 해제 → 인벤토리로
    // ────────────────────────────────────────────────────────────────
    /// <summary>장착 슬롯 → 인벤토리로 해제. 인벤토리 꽉 찼으면 false.</summary>
    public bool UnequipToInventory(int equipIndex)
    {
        if (equipIndex < 0 || equipIndex >= equipped.Count) return false;
        if (inventory.Count >= MaxInventory) return false;
        inventory.Add(equipped[equipIndex]);
        equipped.RemoveAt(equipIndex);
        OnChanged?.Invoke();
        return true;
    }

    // ────────────────────────────────────────────────────────────────
    // 장착 슬롯 → 팔기
    // ────────────────────────────────────────────────────────────────
    public int SellFromEquipped(int equipIndex)
    {
        if (equipIndex < 0 || equipIndex >= equipped.Count) return 0;
        int sellPrice = Mathf.Max(1, equipped[equipIndex].price);
        equipped.RemoveAt(equipIndex);
        PlayerStats.Shared?.AddGold(sellPrice);
        OnChanged?.Invoke();
        return sellPrice;
    }

    // ────────────────────────────────────────────────────────────────
    // 인벤토리 슬롯 → 특정 장착 슬롯 이동/교체
    // ────────────────────────────────────────────────────────────────
    public void MoveInvenToEquip(int invenIndex, int equipIndex)
    {
        if (invenIndex < 0 || invenIndex >= inventory.Count) return;
        if (equipIndex < 0 || equipIndex >= MaxEquipped) return;
        var weapon = inventory[invenIndex];

        // equipIndex까지 null로 확장 (빈 슬롯 포함)
        while (equipped.Count <= equipIndex) equipped.Add(null);

        if (equipped[equipIndex] != null)
        {
            // 장착 슬롯에 무기가 있으면 인벤과 교체
            inventory[invenIndex] = equipped[equipIndex];
            equipped[equipIndex]  = weapon;
        }
        else
        {
            // 빈 장착 슬롯에 이동 (정확한 equipIndex 위치로)
            equipped[equipIndex] = weapon;
            inventory.RemoveAt(invenIndex);
        }
        // 뒤의 null 정리
        while (equipped.Count > 0 && equipped[equipped.Count - 1] == null)
            equipped.RemoveAt(equipped.Count - 1);

        OnChanged?.Invoke();
    }

    // ────────────────────────────────────────────────────────────────
    // 인벤토리 슬롯 ↔ 인벤토리 슬롯 교체
    // ────────────────────────────────────────────────────────────────
    public void SwapInvenToInven(int a, int b)
    {
        if (a < 0 || b < 0 || a == b) return;
        if (a >= inventory.Count || b >= inventory.Count) return;
        (inventory[a], inventory[b]) = (inventory[b], inventory[a]);
        OnChanged?.Invoke();
    }

    // ────────────────────────────────────────────────────────────────
    // 합성 (3 same type+grade → 1 next grade)
    // ────────────────────────────────────────────────────────────────
    /// <summary>인벤토리에 같은 이름+등급 무기가 3개 이상 있는지 확인합니다.</summary>
    public bool CanMerge(string weaponName, int star)
    {
        int count = 0;
        foreach (var w in inventory)
            if (w != null && w.weaponName == weaponName && w.star == star)
                if (++count >= 3) return true;
        return false;
    }

    /// <summary>인벤 + 장착 슬롯 합산으로 3개 이상 있는지 확인합니다.</summary>
    public bool CanMergeAll(string weaponName, int star)
        => CountAll(weaponName, star) >= 3;

    /// <summary>인벤 + 장착 슬롯 합산 개수를 반환합니다.</summary>
    public int CountAll(string weaponName, int star)
    {
        int count = 0;
        foreach (var w in inventory)
            if (w != null && w.weaponName == weaponName && w.star == star) count++;
        foreach (var w in equipped)
            if (w != null && w.weaponName == weaponName && w.star == star) count++;
        return count;
    }

    /// <summary>인벤 내 같은 이름+성 3개를 제거하고 result를 인벤에 추가합니다.
    /// 소모된 2개(result로 대체되지 않는 만큼)의 판매가를 골드로 지급합니다.</summary>
    public bool TryMerge(string weaponName, int star, WeaponData result)
    {
        if (result == null) return false;

        var indices = new List<int>();
        for (int i = 0; i < inventory.Count && indices.Count < 3; i++)
        {
            var w = inventory[i];
            if (w != null && w.weaponName == weaponName && w.star == star)
                indices.Add(i);
        }

        if (indices.Count < 3) return false;

        // 합성 결과물 가격 = 소모 3개 합산
        int totalPrice = 0;
        foreach (int i in indices) totalPrice += Mathf.Max(1, inventory[i].price);
        result.price = totalPrice;

        // 높은 인덱스부터 제거해야 인덱스 밀림 없음
        for (int i = indices.Count - 1; i >= 0; i--)
            inventory.RemoveAt(indices[i]);

        inventory.Add(result);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>인벤 + 장착 슬롯을 합산해 3개를 소모하고 result를 배치합니다.
    /// 장착 슬롯에서 소모된 경우 result는 그 슬롯에 우선 배치됩니다.</summary>
    public bool TryMergeAll(string weaponName, int star, WeaponData result)
    {
        if (result == null) return false;

        // 인벤 먼저, 그 다음 장착에서 최대 3개 수집
        var invenIdx = new List<int>();
        var equipIdx = new List<int>();

        for (int i = 0; i < inventory.Count && invenIdx.Count + equipIdx.Count < 3; i++)
        {
            var w = inventory[i];
            if (w != null && w.weaponName == weaponName && w.star == star)
                invenIdx.Add(i);
        }
        for (int i = 0; i < equipped.Count && invenIdx.Count + equipIdx.Count < 3; i++)
        {
            var w = equipped[i];
            if (w != null && w.weaponName == weaponName && w.star == star)
                equipIdx.Add(i);
        }

        if (invenIdx.Count + equipIdx.Count < 3) return false;

        // 소모 3개 목록 (가격용)
        var consumed = new List<WeaponData>();
        foreach (int i in invenIdx) consumed.Add(inventory[i]);
        foreach (int i in equipIdx) consumed.Add(equipped[i]);

        // 합성 결과물 가격 = 소모 3개 합산
        int totalPrice = 0;
        foreach (var w in consumed) totalPrice += Mathf.Max(1, w.price);
        result.price = totalPrice;

        // 인벤 제거 (인덱스 밀림 방지: 높은 순)
        for (int i = invenIdx.Count - 1; i >= 0; i--)
            inventory.RemoveAt(invenIdx[i]);

        // 장착 제거 (null 처리) + 결과 배치 슬롯 기억
        int resultEquipSlot = equipIdx.Count > 0 ? equipIdx[0] : -1;
        for (int i = equipIdx.Count - 1; i >= 0; i--)
            equipped[equipIdx[i]] = null;

        // 결과 배치: 장착 슬롯에서 소모됐으면 그 슬롯에, 아니면 인벤에
        if (resultEquipSlot >= 0 && resultEquipSlot < equipped.Count)
            equipped[resultEquipSlot] = result;
        else
            inventory.Add(result);

        // 뒤의 null 장착 슬롯 정리
        while (equipped.Count > 0 && equipped[equipped.Count - 1] == null)
            equipped.RemoveAt(equipped.Count - 1);

        OnChanged?.Invoke();
        return true;
    }

    /// <summary>장착 슬롯의 무기를 교체합니다 (증강 등 외부 업그레이드용).</summary>
    public void ReplaceEquipped(int index, WeaponData data)
    {
        if (index < 0 || index >= equipped.Count) return;
        equipped[index] = data;
        OnChanged?.Invoke();
    }

    /// <summary>용량 무시하고 인벤에 추가합니다. 합성으로 슬롯이 바로 확보되는 경우에만 사용하세요.</summary>
    public void ForceAddToInventory(WeaponData data)
    {
        inventory.Add(data);
        OnChanged?.Invoke();
    }

    // ────────────────────────────────────────────────────────────────
    // 드래그 & 드롭 교체
    // ────────────────────────────────────────────────────────────────
    /// <summary>장착 슬롯 A ↔ 장착 슬롯 B 교체</summary>
    public void SwapEquipToEquip(int a, int b)
    {
        if (a < 0 || b < 0 || a == b) return;
        // 빈 슬롯까지 허용 (equipped 범위 외이면 무시)
        if (a >= equipped.Count && b >= equipped.Count) return;

        // equipped 크기를 최소한 max(a,b)+1까지 null로 확장
        while (equipped.Count <= Mathf.Max(a, b)) equipped.Add(null);
        (equipped[a], equipped[b]) = (equipped[b], equipped[a]);
        // 뒤의 null 제거
        while (equipped.Count > 0 && equipped[equipped.Count - 1] == null)
            equipped.RemoveAt(equipped.Count - 1);

        OnChanged?.Invoke();
    }

    /// <summary>장착 슬롯 → 인벤토리 슬롯 이동/교체</summary>
    public void MoveEquipToInven(int equipIndex, int invenIndex)
    {
        if (equipIndex < 0 || equipIndex >= equipped.Count) return;
        var weapon = equipped[equipIndex];

        if (invenIndex < inventory.Count)
        {
            equipped[equipIndex] = inventory[invenIndex];
            inventory[invenIndex] = weapon;
        }
        else if (inventory.Count < MaxInventory)
        {
            inventory.Add(weapon);
            equipped.RemoveAt(equipIndex);
        }
        // 뒤의 null 장착 정리
        while (equipped.Count > 0 && equipped[equipped.Count - 1] == null)
            equipped.RemoveAt(equipped.Count - 1);

        OnChanged?.Invoke();
    }
}
