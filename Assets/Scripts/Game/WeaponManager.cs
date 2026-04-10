using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player에 부착. 모든 WeaponPoint를 관리합니다.
/// </summary>
public class WeaponManager : MonoBehaviour
{
    public static WeaponManager Instance { get; private set; }

    [Header("무기 장착 위치 (최대 6개)")]
    [SerializeField] private Transform[] weaponAnchors;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (PlayerInventory.Shared != null)
        {
            PlayerInventory.Shared.OnChanged -= SyncWeapons; // 중복 방지
            PlayerInventory.Shared.OnChanged += SyncWeapons;
            SyncWeapons(); // 초기 동기화
        }
        else
        {
            this.DError("[WeaponManager] Start - PlayerInventory.Shared가 null입니다. 동기화 실패.");
        }
    }

    private void OnDestroy()
    {
        if (PlayerInventory.Shared != null)
        {
            PlayerInventory.Shared.OnChanged -= SyncWeapons;
        }
    }

    /// <summary>장착 슬롯과 weaponAnchors(Transform) 위치 동기화</summary>
    public void SyncWeapons()
    {
        if (PlayerInventory.Shared == null) return;
        if (weaponAnchors == null || weaponAnchors.Length == 0)
        {
            this.DError("[WeaponManager] SyncWeapons - weaponAnchors 배열이 비어있습니다. 인스펙터에서 6개의 Transform을 연결해주세요!");
            return;
        }

        var equipped = PlayerInventory.Shared.Equipped;

        for (int i = 0; i < weaponAnchors.Length; i++)
        {
            Transform anchor = weaponAnchors[i];
            if (anchor == null) continue;

            // 1. 기존 장착된 무기(자식)들 전부 제거
            foreach (Transform child in anchor)
            {
                Destroy(child.gameObject);
            }

            // 2. 장착 슬롯에 무기가 있다면 새로 생성
            if (i < equipped.Count && equipped[i] != null && equipped[i].prefab != null)
            {
                GameObject weaponGO = Instantiate(equipped[i].prefab, anchor);
                weaponGO.transform.localPosition = Vector3.zero;
                weaponGO.transform.localRotation = Quaternion.identity;
                weaponGO.transform.localScale    = Vector3.one;

                // WeaponPoint 컴포넌트가 없으면 추가, 있으면 가져와서 Init
                if (!weaponGO.TryGetComponent<WeaponPoint>(out var wp))
                    wp = weaponGO.AddComponent<WeaponPoint>();
                wp.Init(equipped[i]);

                this.DLog($"[WeaponManager] 슬롯 [{i}]: '{equipped[i].weaponName}' 장착 완료 (부모: {anchor.name})");
            }
        }

        // 무기 생성/교체 후 현재 스탯 시트를 즉시 재적용
        var sm = PlayerStatManager.Shared;
        var player = PlayerController.Instance;
        if (sm != null && player != null)
            player.ApplyStatSheet(sm);
    }

    /// <summary>현재 장착된 모든 WeaponPoint를 반환합니다.</summary>
    public List<WeaponPoint> GetActiveWeaponPoints()
    {
        var result = new List<WeaponPoint>();
        if (weaponAnchors == null) return result;
        var temp = new List<WeaponPoint>();
        foreach (var anchor in weaponAnchors)
        {
            if (anchor == null) continue;
            anchor.GetComponentsInChildren<WeaponPoint>(temp);
            result.AddRange(temp);
        }
        return result;
    }
}
