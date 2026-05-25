using System.Collections;
using UnityEngine;

/// <summary>
/// 스킬 이펙트 프리팹 루트에 부착.
/// 활성화 시 자식의 Animator 재생 → 종료 후 Animator 비활성화, SpriteAnimation 활성화.
/// </summary>
public class SkillEffectController : MonoBehaviour
{
    private Animator        _animator;
    private SpriteAnimation _spriteAnimation;
    private Coroutine       _routine;

    private void Awake()
    {
        _animator        = GetComponentInChildren<Animator>();
        _spriteAnimation = GetComponentInChildren<SpriteAnimation>();
    }

    private void OnEnable()
    {
        _animator.enabled        = true;
        _spriteAnimation.enabled = false;

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(WaitForAnimEnd());
    }

    private void OnDisable()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
    }

    private IEnumerator WaitForAnimEnd()
    {
        yield return null; // Animator 초기화 대기
        float length = _animator.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(length);

        _animator.enabled        = false;
        _spriteAnimation.enabled = true;
        _routine = null;
    }
}
