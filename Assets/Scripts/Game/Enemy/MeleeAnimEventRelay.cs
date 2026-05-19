using System;
using UnityEngine;

/// <summary>
/// 근접 공격 애니메이션 이벤트 릴레이.
/// 클립에서 AnimEventMeleeHit() 하나만 호출하면,
/// 구독자(MeleeBehaviour, BossController 등)가 알아서 처리합니다.
/// </summary>
public class MeleeAnimEventRelay : MonoBehaviour
{
    public event Action OnMeleeHit;

    // Animation Event 에서 호출
    public void AnimEventMeleeHit() => OnMeleeHit?.Invoke();
}
