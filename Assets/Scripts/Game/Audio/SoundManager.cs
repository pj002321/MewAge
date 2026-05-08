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

    [System.Serializable]
    public class ChapterBGMEntry
    {
        [Tooltip("챕터 접두어 (예: C1, C2)")]
        public string chapterId;
        public AudioClip homeBGM;
        public AudioClip gameBGM;
        public AudioClip bossBGM;
    }

    [Header("BGM — 기본 (챕터 미지정 폴백)")]
    [SerializeField] private AudioClip homeBGM;
    [SerializeField] private AudioClip gameBGM;
    [SerializeField] private AudioClip bossBGM;
    [SerializeField] private float bgmFadeDuration = 1f;

    [Header("BGM — 챕터별")]
    [SerializeField] private List<ChapterBGMEntry> chapterBGMList = new();

    private readonly Dictionary<SoundKey, SoundEntry> soundDict = new();
    private readonly Dictionary<SoundKey, float> lastPlayTime = new();

    private AudioSource sfxSource;
    private AudioSource bgmSource;
    private Coroutine bgmFadeCoroutine;

    private string currentScene;
    private string currentChapterId;
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
    public void PlayHomeBGM() => PlayChapterHomeBGM(null);
    public void PlayGameBGM() => PlayChapterGameBGM(null);

    /// <summary>chapterID 예: "C1_S1" → "C1" 접두어로 챕터별 홈 BGM 재생.</summary>
    public void PlayChapterHomeBGM(string chapterID)
    {
        currentChapterId = ChapterPrefix(chapterID);
        var clip = FindBGM(currentChapterId, BgmType.Home) ?? homeBGM;
        PlayBGMWithFade(clip, "home");
    }

    /// <summary>chapterID 예: "C1_S1" → "C1" 접두어로 챕터별 인게임 BGM 재생.</summary>
    public void PlayChapterGameBGM(string chapterID)
    {
        currentChapterId = ChapterPrefix(chapterID);
        var clip = FindBGM(currentChapterId, BgmType.Game) ?? gameBGM;
        PlayBGMWithFade(clip, "game");
    }

    /// <summary>보스 웨이브 BGM 재생. 없으면 일반 게임 BGM 유지.</summary>
    public void PlayChapterBossBGM(string chapterID = null)
    {
        var prefix = chapterID != null ? ChapterPrefix(chapterID) : currentChapterId;
        var clip   = FindBGM(prefix, BgmType.Boss) ?? bossBGM;
        if (clip == null) return;
        PlayBGMWithFade(clip, "boss");
    }

    private enum BgmType { Home, Game, Boss }

    private static string ChapterPrefix(string chapterID)
    {
        if (string.IsNullOrEmpty(chapterID)) return null;
        int under = chapterID.IndexOf('_');
        return under > 0 ? chapterID.Substring(0, under) : chapterID;
    }

    private AudioClip FindBGM(string prefix, BgmType type)
    {
        if (string.IsNullOrEmpty(prefix)) return null;
        foreach (var e in chapterBGMList)
            if (e.chapterId == prefix)
                return type switch
                {
                    BgmType.Home => e.homeBGM,
                    BgmType.Boss => e.bossBGM,
                    _            => e.gameBGM,
                };
        return null;
    }

    private void PlayBGMWithFade(AudioClip clip, string sceneName)
    {
        currentScene = sceneName;

        if (clip == null) return;
        if (!BGMEnabled) return;

        // 같은 클립이 재생 중이면 스킵
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        // 페이드인으로 재생 (로딩 중에도 즉시 전환)
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
        bgmSource.Pause();
    }

    private void ResumeBGM()
    {
        if (!BGMEnabled) return;
        if (isLoadingIndicatorActive) return;

        // 이미 재생 중이면 아무것도 하지 않음 (PlayBGMWithFade가 로딩 중에 이미 전환)
        if (bgmSource.isPlaying) return;

        // 일시정지된 클립이 있으면 이어서 재생
        if (bgmSource.clip != null)
        {
            if (bgmFadeCoroutine != null) StopCoroutine(bgmFadeCoroutine);
            bgmFadeCoroutine = StartCoroutine(FadeInFromPause());
        }
        else if (currentScene == "home")
            PlayChapterHomeBGM(currentChapterId);
        else if (currentScene == "boss")
            PlayChapterBossBGM();
        else if (currentScene == "game")
            PlayChapterGameBGM(currentChapterId);
    }

    private IEnumerator FadeInFromPause()
    {
        bgmSource.volume = 0f;
        bgmSource.UnPause();
        float elapsed = 0f;
        while (elapsed < bgmFadeDuration)
        {
            elapsed += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(0f, 1f, elapsed / bgmFadeDuration);
            yield return null;
        }
        bgmSource.volume = 1f;
    }

    // ─────────────────────────────────────────────────
    // 설정 (뮤트)
    // ─────────────────────────────────────────────────
    public bool BGMEnabled { get; private set; } = true;

    public void SetBGMEnabled(bool enabled)
    {
        BGMEnabled = enabled;
        if (!enabled)
            StopBGM();
        else
            ResumeBGM();
    }

    public void SetSFXEnabled(bool enabled)
    {
        sfxSource.mute = !enabled;
    }
}
