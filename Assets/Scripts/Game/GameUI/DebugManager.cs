using System;
using UnityEngine;

/// <summary>
/// 디버그 모드 ON/OFF 상태를 씬 전환 후에도 유지하는 전역 싱글톤.
/// PlayerPrefs "DebugMode" (1=ON, 0=OFF) 를 읽고 저장합니다.
/// </summary>
public class DebugManager : MonoBehaviourSingleton<DebugManager>
{
    protected override bool DontDestroy => true;

    public bool IsEnabled { get; private set; }

    /// <summary>Toggle() 호출 후 발행.</summary>
    public event Action OnChanged;

    protected override void Awake()
    {
        base.Awake();
        if (Shared != this) return;
        IsEnabled = PlayerPrefs.GetInt("DebugMode", 1) == 1;
    }

    public void Toggle()
    {
        IsEnabled = !IsEnabled;
        PlayerPrefs.SetInt("DebugMode", IsEnabled ? 1 : 0);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    /// <summary>DebugManager 인스턴스가 없을 때 자동 생성. 각 패널 Awake에서 호출.</summary>
    public static void EnsureExists()
    {
        if (Shared == null) Create();
    }
}
