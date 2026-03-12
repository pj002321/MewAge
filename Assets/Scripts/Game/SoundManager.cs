using UnityEngine;

/// <summary>
/// 게임 전체 사운드를 관리하는 싱글톤 매니저.
/// Inspector에서 key-clip 쌍을 등록한 후 Play(key)로 재생합니다.
/// </summary>
public class SoundManager : MonoBehaviourSingleton<SoundManager>
{
    [System.Serializable]
    public class SoundEntry
    {
        public string key;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [SerializeField] private SoundEntry[] sounds;

    private AudioSource audioSource;

    protected override void Awake()
    {
        base.Awake();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    /// <summary>등록된 키에 해당하는 사운드를 재생합니다.</summary>
    public void Play(string key)
    {
        foreach (var entry in sounds)
        {
            if (entry.key == key && entry.clip != null)
            {
                audioSource.PlayOneShot(entry.clip, entry.volume);
                return;
            }
        }
        Debug.LogWarning($"[SoundManager] 사운드를 찾을 수 없음: '{key}'");
    }
}
