using UnityEngine;

[System.Serializable]
public class EnemyEffectEntry
{
    [Tooltip("생성할 VFX 프리팹")]
    public GameObject vfxPrefab;
    [Tooltip("스폰 오프셋 (로컬 좌표 기준)")]
    public Vector2    spawnOffset;
    [Tooltip("재생할 사운드 클립")]
    public AudioClip  sfxClip;
    [Range(0f, 1f), Tooltip("볼륨")]
    public float      volume = 1f;
}

/// <summary>
/// 적 프리팹에 붙이는 VFX / SFX 설정 컴포넌트.
/// EnemyPresetWindow 에서 통합 편집합니다.
/// </summary>
public class EnemyFXConfig : MonoBehaviour
{
    [Header("공격 이펙트")]  public EnemyEffectEntry onAttack = new();
    [Header("피격 이펙트")]  public EnemyEffectEntry onHit    = new();
    [Header("사망 이펙트")]  public EnemyEffectEntry onDeath  = new();
    [Header("스폰 이펙트")]  public EnemyEffectEntry onSpawn  = new();

    /// <summary>VFX 스폰 + SFX 재생 유틸 (런타임 사용).</summary>
    public void Play(EnemyEffectEntry entry)
    {
        if (entry == null) return;

        if (entry.vfxPrefab != null)
        {
            Vector3 pos = transform.position + (Vector3)entry.spawnOffset;
            Instantiate(entry.vfxPrefab, pos, Quaternion.identity);
        }

        if (entry.sfxClip != null)
            AudioSource.PlayClipAtPoint(entry.sfxClip, transform.position, entry.volume);
    }
}
