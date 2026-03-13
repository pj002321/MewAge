using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 전체 사운드를 관리하는 싱글톤 매니저.
/// Inspector에서 key-clip 쌍을 등록한 후 Play(key)로 재생합니다.
/// minInterval로 같은 사운드의 중복 재생을 방지합니다.
/// </summary>
public class SoundManager : MonoBehaviourSingleton<SoundManager>
{
    [System.Serializable]
    public class SoundEntry
    {
        public SoundKey key;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("같은 사운드 최소 재생 간격(초). 0이면 제한 없음")]
        public float minInterval = 0.05f;
    }

    [SerializeField] private SoundEntry[] sounds;

    private AudioSource audioSource;
    private readonly Dictionary<SoundKey, float> lastPlayTime = new();

    protected override void Awake()
    {
        base.Awake();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    /// <summary>등록된 키에 해당하는 사운드를 재생합니다. minInterval 안에 같은 키가 오면 무시합니다.</summary>
    public void Play(SoundKey key)
    {
        foreach (var entry in sounds)
        {
            if (entry.key != key || entry.clip == null) continue;

            if (entry.minInterval > 0f &&
                lastPlayTime.TryGetValue(key, out float last) &&
                Time.unscaledTime - last < entry.minInterval)
                return;

            audioSource.PlayOneShot(entry.clip, entry.volume);
            lastPlayTime[key] = Time.unscaledTime;
            return;
        }
        Debug.LogWarning($"[SoundManager] 사운드를 찾을 수 없음: '{key}'");
    }
}
