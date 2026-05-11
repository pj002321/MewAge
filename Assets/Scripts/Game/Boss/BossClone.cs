using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 분신 태그 컴포넌트.
/// BossCloneSkill 이 Boss.gameObject 를 Instantiate 한 뒤 추가합니다.
/// BossController 가 이동·스킬을 담당하며, 이 컴포넌트는 수명·사망 VFX·목록 관리만 합니다.
/// </summary>
public class BossClone : MonoBehaviour
{
    public static readonly List<BossClone> All = new();

    private BossController _bc;
    private GameObject     _deathVfxPrefab;
    private float          _vfxLifetime;

    public void Init(float lifetime, GameObject deathVfxPrefab, float vfxLifetime = 1f)
    {
        _bc             = GetComponent<BossController>();
        _deathVfxPrefab = deathVfxPrefab;
        _vfxLifetime    = vfxLifetime;

        if (_bc != null) _bc.OnDeath += OnCloneDeath;
        All.Add(this);
        StartCoroutine(LifeRoutine(lifetime));
    }

    private void OnCloneDeath()
    {
        if (_deathVfxPrefab == null) return;
        var vfx = Instantiate(_deathVfxPrefab, transform.position, Quaternion.identity);
        Destroy(vfx, _vfxLifetime);
    }

    private void OnDestroy()
    {
        All.Remove(this);
        if (_bc != null) _bc.OnDeath -= OnCloneDeath;
    }

    private IEnumerator LifeRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        _bc?.ForceDie();
    }
}
