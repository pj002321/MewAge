using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 전체 사운드를 관리하는 싱글톤 매니저.
/// Inspector에서 key-clip 쌍을 등록한 후 Play(key)로 재생합니다.
/// minInterval로 같은 사운드의 중복 재생을 방지합니다.
/// BGM은 각 씬 시작 시 명시적으로 Play*BGM()을 호출해야 합니다.
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

    [Header("사운드 딕셔너리")]
    [SerializeField] private List<SoundEntry> soundList = new();

    [Header("BGM")]
    [SerializeField] private AudioClip homeBGM;
    [SerializeField] private AudioClip gameBGM;
    [SerializeField] private AudioClip bossBGM;
    [SerializeField] private float bgmFadeDuration = 1f;

    private readonly Dictionary<SoundKey, SoundEntry> soundDict = new();
    private readonly Dictionary<SoundKey, float> lastPlayTime = new();

    private AudioSource sfxSource;
    private AudioSource bgmSource;
    private Coroutine bgmFadeCoroutine;

    private AudioClip currentBGMClip;

    protected override void Awake()
    {
        base.Awake();

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;

        RebuildDictionary();
    }

    private void RebuildDictionary()
    {
        soundDict.Clear();
        foreach (var entry in soundList)
        {
            if (entry != null && entry.clip != null)
                soundDict[entry.key] = entry;
        }
    }

    private void OnValidate() => RebuildDictionary();

    // ─────────────────────────────────────────────────
    // SFX
    // ─────────────────────────────────────────────────

    public void PlayClip(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, volume);
    }

    public void Play(SoundKey key)
    {
        if (!soundDict.TryGetValue(key, out var entry)) return;

        if (entry.minInterval > 0f &&
            lastPlayTime.TryGetValue(key, out float last) &&
            Time.unscaledTime - last < entry.minInterval)
            return;

        sfxSource.PlayOneShot(entry.clip, entry.volume);
        lastPlayTime[key] = Time.unscaledTime;
    }

    // ─────────────────────────────────────────────────
    // BGM
    // ─────────────────────────────────────────────────

    public void PlayHomeBGM()                                    => PlayBGMWithFade(homeBGM);
    public void PlayGameBGM()                                    => PlayBGMWithFade(gameBGM);
    public void PlayChapterHomeBGM(string chapterID)             => PlayBGMWithFade(homeBGM);
    public void PlayChapterGameBGM(string chapterID)             => PlayBGMWithFade(gameBGM);
    public void PlayChapterBossBGM(string chapterID = null)      => PlayBGMWithFade(bossBGM);

    private void PlayBGMWithFade(AudioClip clip)
    {
        if (clip == null) return;

        currentBGMClip = clip;

        if (!BGMEnabled) return;

        // 같은 클립이 재생 중이면 스킵
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        if (bgmFadeCoroutine != null) StopCoroutine(bgmFadeCoroutine);
        bgmFadeCoroutine = StartCoroutine(FadeInBGM(clip));
    }

    private IEnumerator FadeInBGM(AudioClip clip)
    {
        bgmSource.clip = clip;
        bgmSource.volume = 0f;
        bgmSource.Play();

        float elapsed = 0f;
        while (elapsed < bgmFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(0f, 1f, elapsed / bgmFadeDuration);
            yield return null;
        }
        bgmSource.volume = 1f;
    }

    public void StopBGM()
    {
        if (bgmFadeCoroutine != null) StopCoroutine(bgmFadeCoroutine);
        bgmFadeCoroutine = StartCoroutine(FadeOutBGM());
    }

    public void StopBGMImmediate()
    {
        if (bgmFadeCoroutine != null) { StopCoroutine(bgmFadeCoroutine); bgmFadeCoroutine = null; }
        bgmSource.Stop();
        bgmSource.volume  = 0f;
        currentBGMClip    = null;
    }

    private IEnumerator FadeOutBGM()
    {
        float startVolume = bgmSource.volume;
        float elapsed = 0f;
        while (elapsed < bgmFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / bgmFadeDuration);
            yield return null;
        }
        bgmSource.volume = 0f;
        bgmSource.Stop();
    }

    // ─────────────────────────────────────────────────
    // 설정 (뮤트)
    // ─────────────────────────────────────────────────

    public bool BGMEnabled { get; private set; } = true;

    public void SetBGMEnabled(bool enabled)
    {
        BGMEnabled = enabled;
        if (!enabled)
        {
            StopBGM();
        }
        else if (currentBGMClip != null)
        {
            if (bgmFadeCoroutine != null) StopCoroutine(bgmFadeCoroutine);
            bgmFadeCoroutine = StartCoroutine(FadeInBGM(currentBGMClip));
        }
    }

    public void SetSFXEnabled(bool enabled)
    {
        sfxSource.mute = !enabled;
    }
}
