using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class DailyMissionData
{
    public string id;
    public string description;
    public int    badgeCount;  // 수령 시 획득 배지 수
    public int    current;
    public int    goal;
    public bool   claimed;

    public bool IsComplete => current >= goal;
}

/// <summary>
/// 일일 미션 슬롯 한 칸.
/// Inspector: rewardImg, progressSlider, infoText, claimButton, goButton, claimedOverlay
/// </summary>
public class DailyMissionSlot : MonoBehaviour
{
    [Header("슬롯 구성 요소")]
    [SerializeField] Image           rewardImg;
    [SerializeField] Slider          progressSlider;
    [SerializeField] TextMeshProUGUI infoText;       // 설명 텍스트
    [SerializeField] TextMeshProUGUI progressText;   // "10/10" 수치 텍스트
    [SerializeField] TextMeshProUGUI badgeCountText; // 획득 배지 수 텍스트
    [SerializeField] Button          claimButton;
    [SerializeField] Button          goButton;
    [SerializeField] GameObject      claimedOverlay;  // 수령 후 표시 이미지

    public System.Action<DailyMissionSlot> OnClaimed;
    public System.Action<DailyMissionSlot> OnGo;

    public DailyMissionData Data => _data;

    DailyMissionData _data;
    RectTransform    _rewardTransform;

    void Awake()
    {
        claimButton?.onClick.AddListener(Claim);
        goButton?.onClick.AddListener(() => OnGo?.Invoke(this));
    }

    public void Setup(DailyMissionData data, RectTransform rewardTransform)
    {
        _data            = data;
        _rewardTransform = rewardTransform;

        if (progressSlider)
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.value    = data.goal > 0 ? Mathf.Clamp01((float)data.current / data.goal) : 0f;
        }

        if (infoText)
            infoText.text = data.description;

        if (progressText)
            progressText.text = $"{data.current}/{data.goal}";

        if (badgeCountText)
            badgeCountText.text = data.badgeCount.ToString();

        bool canClaim = data.IsComplete && !data.claimed;

        if (claimButton)
        {
            claimButton.gameObject.SetActive(canClaim);
        }
        if (goButton)
        {
            goButton.gameObject.SetActive(!canClaim && !data.claimed);
        }

        if (claimedOverlay)
            claimedOverlay.SetActive(data.claimed);
    }

    void Claim()
    {
        if (_data == null || _data.claimed || !_data.IsComplete) return;
        _data.claimed = true;

        if (claimButton)    claimButton.gameObject.SetActive(false);
        if (goButton)       goButton.gameObject.SetActive(false);
        if (claimedOverlay) claimedOverlay.SetActive(true);

        if (rewardImg != null && _rewardTransform != null)
            StartCoroutine(BadgesFly(_data.badgeCount));

        OnClaimed?.Invoke(this);
    }

    IEnumerator BadgesFly(int count)
    {
        var canvasRoot = rewardImg.canvas.transform;
        Vector3 origin = rewardImg.rectTransform.position;
        Vector2 size   = rewardImg.rectTransform.sizeDelta;

        // 뿌리기 반경
        const float scatterRadius = 60f;
        const float scatterTime   = 0.25f;
        const float flyTime       = 0.45f;
        const float staggerStep   = 0.06f;

        // 뱃지 이미지 + 목표 위치 생성
        var ghosts = new Image[count];
        var scatterTargets = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            var ghost = Instantiate(rewardImg, canvasRoot);
            ghost.raycastTarget = false;
            ghost.rectTransform.position  = origin;
            ghost.rectTransform.sizeDelta = size;
            ghost.color = Color.white;
            ghosts[i] = ghost;

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float dist  = Random.Range(scatterRadius * 0.4f, scatterRadius);
            scatterTargets[i] = origin + new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist, 0f);
        }

        // 1단계: 흩뿌리기
        float elapsed = 0f;
        while (elapsed < scatterTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / scatterTime);
            float ease = t * t * (3f - 2f * t);
            for (int i = 0; i < count; i++)
                if (ghosts[i] != null)
                    ghosts[i].rectTransform.position = Vector3.Lerp(origin, scatterTargets[i], ease);
            yield return null;
        }

        // 2단계: 순차적으로 빨아들이기
        Vector3 end = _rewardTransform.position;
        for (int i = 0; i < count; i++)
            StartCoroutine(FlyOne(ghosts[i], scatterTargets[i], end, flyTime, i * staggerStep));
    }

    IEnumerator FlyOne(Image ghost, Vector3 start, Vector3 end, float duration, float delay)
    {
        if (delay > 0f)
        {
            float d = 0f;
            while (d < delay) { d += Time.unscaledDeltaTime; yield return null; }
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (ghost == null) yield break;
            elapsed += Time.unscaledDeltaTime;
            float t    = Mathf.Clamp01(elapsed / duration);
            float ease = t * t * (3f - 2f * t);
            ghost.rectTransform.position = Vector3.Lerp(start, end, ease);
            ghost.color = new Color(1f, 1f, 1f, 1f - t);
            yield return null;
        }

        if (ghost != null) Destroy(ghost.gameObject);
    }
}
