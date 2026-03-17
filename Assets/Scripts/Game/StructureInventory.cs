using System.Collections.Generic;
using UnityEngine;
using Mewage.DB;

/// <summary>
/// 획득한 기물을 Stack으로 관리합니다.
/// 가장 최근 획득한 기물이 먼저 사용됩니다.
/// </summary>
public class StructureInventory : MonoBehaviourSingleton<StructureInventory>
{
    private readonly Stack<DB_기물> stack = new();

    protected override bool DontDestroy => false;

    public int Count => stack.Count;
    public DB_기물 Peek => stack.Count > 0 ? stack.Peek() : null;

    public event System.Action OnChanged;

    /// <summary>기물 구슬 수집 시 호출.</summary>
    public void Push(DB_기물 data)
    {
        stack.Push(data);
        OnChanged?.Invoke();
    }

    /// <summary>버튼 눌릴 때 호출 — 플레이어 위치에 설치.</summary>
    public void UseTop()
    {
        if (stack.Count == 0) return;

        var player = PlayerController.Instance;
        if (player == null) return;

        var data = stack.Pop();
        if (data?.f_설치프리팹 != null)
            Instantiate(data.f_설치프리팹, player.Position, Quaternion.identity);

        OnChanged?.Invoke();
    }
}
