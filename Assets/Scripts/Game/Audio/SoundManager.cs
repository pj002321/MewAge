using System.Collections;
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

    [Header("사운드 딕셔너리")]
    [SerializeField] private List<SoundEntry> soundList = new();

    [Header("BGM")]
    [SerializeField] private AudioClip homeBGM;
    [SerializeField] private AudioClip gameBGM;
    [SerializeField] private float bgmFadeDuration = 1f;

    private readonly Dictionary<SoundKey, SoundEntry> soundDict = new();
    private readonly Dictionary<SoundKey, float> lastPlayTime = new();

    private AudioSource sfxSource;
    private AudioSource bgmSource;
    private Coroutine bgmFadeCoroutine;

    private string currentScene;
    private bool isLoadingIndicatorActive;

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

    private void OnValidate()
    {
        RebuildDictionary();
    }

    private void Update()
    {
        // 로딩 인디케이터 감지 (SceneLoadManager의 로딩 스크린)
        bool loading = SceneLoadManager.IsLoading;
        
        if (loading != isLoadingIndicatorActive)
        {
            isLoadingIndicatorActive = loading;
            if (isLoadingIndicatorActive)
                StopBGM();
            else
                ResumeBGM();
        }
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
    public void PlayHomeBGM() => PlayBGMWithFade(homeBGM, "home");
    public void PlayGameBGM() => PlayBGMWithFade(gameBGM, "game");

    private void PlayBGMWithFade(AudioClip clip, string sceneName)
    {
        if (clip == null) return;

        currentScene = sceneName;

        // 로딩 중에는 재생하지 않음
        if (isLoadingIndicatorActive) return;

        // 같은 클립이 재생 중이면 스킵
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        // 페이드인으로 재생
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
            elapsed += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(0f, 1f, elapsed / bgmFadeDuration);
            yield return null;
        }
        bgmSource.volume = 1f;
    }

    private void StopBGM()
    {
        if (bgmFadeCoroutine != null) StopCoroutine(bgmFadeCoroutine);
        bgmFadeCoroutine = StartCoroutine(FadeOutBGM());
    }

    private IEnumerator FadeOutBGM()
    {
        float startVolume = bgmSource.volume;
        float elapsed = 0f;
        while (elapsed < bgmFadeDuration)
        {
            elapsed += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / bgmFadeDuration);
            yield return null;
        }
        bgmSource.volume = 0f;
        bgmSource.Stop();
    }

    private void ResumeBGM()
    {
        if (currentScene == "home")
            PlayBGMWithFade(homeBGM, "home");
        else if (currentScene == "game")
            PlayBGMWithFade(gameBGM, "game");
    }
}
