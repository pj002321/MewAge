using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 분신 태그 컴포넌트.
/// BossCloneSkill 이 Boss.gameObject 를 Instantiate 한 뒤 추가합니다.
/// 사망 시 검정 실루엣으로 전환 후 알파 페이드아웃으로 소멸합니다.
/// </summary>
public class BossClone : MonoBehaviour
{
    public static readonly List<BossClone> All = new();

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => All.Clear();

    private const float FadeDuration = 0.6f;

    private BossController   _bc;
    private SpriteRenderer[] _srs;

    public void Init(float lifetime)
    {
        _bc  = GetComponent<BossController>();
        _srs = GetComponentsInChildren<SpriteRenderer>(true);

        if (_bc != null) _bc.OnDeath += OnCloneDeath;
        All.Add(this);
        StartCoroutine(LifeRoutine(lifetime));
    }

    private void OnCloneDeath()
    {
        StopAllCoroutines();
        StartCoroutine(SilhouetteFadeRoutine());
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

    private IEnumerator SilhouetteFadeRoutine()
    {
        // 모든 SpriteRenderer를 검정 실루엣으로 전환
        var silhouetteMat = new Material(Shader.Find("Sprites/Default"));
        foreach (var sr in _srs)
        {
            if (sr == null) continue;
            sr.material = silhouetteMat;
            sr.color    = Color.black;
        }

        // 알파 페이드아웃
        for (float t = 0f; t < FadeDuration; t += Time.deltaTime)
        {
            float alpha = Mathf.Lerp(1f, 0f, t / FadeDuration);
            foreach (var sr in _srs)
            {
                if (sr == null) continue;
                var c = sr.color;
                c.a      = alpha;
                sr.color = c;
            }
            yield return null;
        }

        Destroy(gameObject);
    }
}
