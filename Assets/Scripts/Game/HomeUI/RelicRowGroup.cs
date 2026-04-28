using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// rowGroupPrefab에 부착. slotPrefab과 contents만 Inspector에서 할당.
/// </summary>
public class RelicRowGroup : MonoBehaviour
{
    [SerializeField] private RelicSlot slotPrefab;
    [SerializeField] private Transform contents;

    private readonly List<RelicSlot> _slots = new();

    public IReadOnlyList<RelicSlot> Slots => _slots;

    public void Populate(IReadOnlyList<RelicData> relics, int startIdx, Action<RelicData> onClicked)
    {
        foreach (Transform child in contents) Destroy(child.gameObject);
        _slots.Clear();

        for (int i = 0; i < 4; i++)
        {
            int idx  = startIdx + i;
            var slot = Instantiate(slotPrefab, contents);

            if (idx < relics.Count)
            {
                slot.Refresh(relics[idx]);
                slot.OnClicked += onClicked;
            }
            else
            {
                slot.gameObject.SetActive(false);
            }

            _slots.Add(slot);
        }
    }

    public void Refresh(IReadOnlyList<RelicData> relics, int startIdx)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            int idx = startIdx + i;
            if (idx < relics.Count) _slots[i].Refresh(relics[idx]);
        }
    }
}
