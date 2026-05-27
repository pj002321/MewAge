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
    [Header("사운드 라이브러리")]
    [SerializeField] private SoundLibrarySO library;

    [System.Serializable]
    public class ChapterBGMEntry
    {
        [Tooltip("이 챕터 번호 이상이면 아래 BGM 재생 (예: 1→챕터1부터, 5→챕터5부터)")]
        public int       fromChapter;
        [Tooltip("홈 화면 BGM")]
        public AudioClip homeBGM;
    }

    [Header("BGM")]
    [SerializeField] private AudioClip homeBGM;
    [SerializeField] private AudioClip gameBGM;
    [SerializeField] private AudioClip bossBGM;
    [SerializeField] private float bgmFadeDuration = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float defaultBGMVolume = 1f;

    [Header("홈 BGM 임계값 목록 (fromChapter 오름차순으로 입력)")]
    [SerializeField] private List<ChapterBGMEntry> chapterBGMs = new();

    private readonly Dictionary<int, float> lastPlayTime = new();

    private AudioSource sfxSource;
    private AudioSource bgmSource;
    private Coroutine bgmFadeCoroutine;

    private AudioClip currentBGMClip;

    void OnValidate()
    {
        BGMVolume = defaultBGMVolume;
        if (bgmSource != null)
            bgmSource.volume = BGMEnabled ? BGMVolume : 0f;
    }

    protected override void Awake()
    {
        base.Awake();

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;

        BGMVolume = defaultBGMVolume;
        SFXVolume = PlayerPrefs.GetFloat(K_SFXVolume, 1f);
        sfxSource.volume = 1f; // volumeScale은 PlayOneShot에서 직접 처리

        // BGM 켜기/끄기 상태 복원
        BGMEnabled = PlayerPrefs.GetInt("Setting_BGM", 1) == 1;
    }

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
        if (library == null) return;
        PlayEntry(library.Get(key), (int)key);
    }


    public void PlayWeaponShoot(string weaponId)
    {
        if (library == null) return;
        var (clip, vol) = library.GetWeaponShoot(weaponId);
        PlayClipWithInterval(clip, vol, weaponId.GetHashCode());
    }

    public void PlayWeaponHit(string weaponId)
    {
        if (library == null) return;
        var (clip, vol) = library.GetWeaponHit(weaponId);
        PlayClipWithInterval(clip, vol, weaponId.GetHashCode() + 1);
    }

    private void PlayEntry(SoundLibrarySO.Entry entry, int intervalKey)
    {
        if (entry?.clip == null) return;
        PlayClipWithInterval(entry.clip, entry.volume, intervalKey, entry.minInterval);
    }

    private void PlayClipWithInterval(AudioClip clip, float volume, int intervalKey, float minInterval = 0.05f)
    {
        if (clip == null) return;
        if (minInterval > 0f &&
            lastPlayTime.TryGetValue(intervalKey, out float last) &&
            Time.unscaledTime - last < minInterval)
            return;
        // entry.volume을 절대 볼륨으로 취급: sfxSource.volume은 1로 고정, SFX 마스터는 volumeScale로 전달
        sfxSource.PlayOneShot(clip, volume * SFXVolume);
        lastPlayTime[intervalKey] = Time.unscaledTime;
    }

    // ─────────────────────────────────────────────────
    // BGM
    // ─────────────────────────────────────────────────

    public void PlayHomeBGM()                                    => PlayBGMWithFade(homeBGM);
    public void PlayGameBGM()                                    => PlayBGMWithFade(gameBGM);
    public void PlayChapterHomeBGM(string chapterID)             => PlayBGMWithFade(ResolveHomeBGM(chapterID));
    public void PlayChapterGameBGM(string chapterID)             => PlayBGMWithFade(gameBGM);
    public void PlayChapterBossBGM(string chapterID = null)      => PlayBGMWithFade(bossBGM);

    /// <summary>챕터 번호를 파악해 해당하는 홈 BGM 반환. 매칭 없으면 기본 homeBGM.</summary>
    private AudioClip ResolveHomeBGM(string chapterID)
    {
        int chNum = ParseChapterNumber(chapterID);

        AudioClip result = homeBGM;
        foreach (var entry in chapterBGMs)
        {
            if (entry.homeBGM != null && chNum >= entry.fromChapter)
                result = entry.homeBGM;
        }
        return result;
    }

    /// <summary>"C3_S1" → 3. 파싱 실패 시 0 반환.</summary>
    private static int ParseChapterNumber(string chapterID)
    {
        if (string.IsNullOrEmpty(chapterID)) return 0;
        int under = chapterID.IndexOf('_');
        string prefix = under >= 0 ? chapterID.Substring(0, under) : chapterID;
        if (prefix.Length >= 2 && prefix[0] == 'C' &&
            int.TryParse(prefix.Substring(1), out int n)) return n;
        return 0;
    }

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
            bgmSource.volume = Mathf.Lerp(0f, BGMVolume, elapsed / bgmFadeDuration);
            yield return null;
        }
        bgmSource.volume = BGMVolume;
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

    private const string K_SFXVolume = "Setting_SFXVolume";

    public float BGMVolume { get; private set; } = 1f;
    public float SFXVolume { get; private set; } = 1f;
    public bool  BGMEnabled { get; private set; } = true;

    public void SetBGMVolume(float volume)
    {
        BGMVolume = Mathf.Clamp01(volume);
        if (bgmSource != null && BGMEnabled)
            bgmSource.volume = BGMVolume;
    }

    public void SetSFXVolume(float volume)
    {
        SFXVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(K_SFXVolume, SFXVolume);
        PlayerPrefs.Save();
        // sfxSource.volume은 1 고정 — volumeScale은 PlayOneShot에서 직접 처리
    }

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
