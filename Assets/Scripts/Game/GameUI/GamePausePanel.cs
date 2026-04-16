using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 인게임 일시정지 팝업.
/// Inspector 연결: resumeButton, homeButton, improvIconPrefab, improvContents
/// 퍼즈 버튼(HUD)의 OnClick → GameManager.Shared.TogglePause() 연결
/// </summary>
public class GamePausePanel : MonoBehaviourSingleton<GamePausePanel>
{
    protected override bool DontDestroy => false;

    [Header("버튼")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button homeButton;

    [Header("획득 증강 목록")]
    [SerializeField] private GameObject improvIconPrefab;
    [SerializeField] private Transform  improvContents;

    private readonly List<GameObject> _improvIcons = new();

    protected override void Awake()
    {
        base.Awake();
        resumeButton?.onClick.AddListener(Hide);
        homeButton  ?.onClick.AddListener(OnHome);
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        RefreshAugments();
    }

    public void Show()
    {
        gameObject.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Hide()
    {
        Time.timeScale = 1f;
        gameObject.SetActive(false);
    }

    private void OnHome()
    {
        Time.timeScale = 1f;
        GameManager.Shared?.GoHome();
    }

    // ── 증강 아이콘 갱신 (StatPanelUI와 동일 패턴) ──────────────────

    private void RefreshAugments()
    {
        if (improvIconPrefab == null) { Debug.LogWarning("[GamePausePanel] improvIconPrefab 미연결"); return; }
        if (improvContents  == null) { Debug.LogWarning("[GamePausePanel] improvContents 미연결");  return; }

        var mgr      = ImprovManager.Shared;
        var acquired = mgr?.Acquired;
        Debug.Log($"[GamePausePanel] ImprovManager={mgr} (id={mgr?.GetInstanceID()}), Acquired count={acquired?.Count ?? -1}");

        int idx = 0;

        if (acquired != null)
        {
            foreach (var kv in acquired)
            {
                var (data, count) = kv.Value;

                GameObject icon;
                if (idx < _improvIcons.Count)
                {
                    icon = _improvIcons[idx];
                    icon.SetActive(true);
                }
                else
                {
                    icon = Instantiate(improvIconPrefab, improvContents);
                    _improvIcons.Add(icon);
                }

                var img = icon.GetComponentInChildren<Image>(true);
                if (img != null)
                {
                    string augId = data.f_증강ID.ToString();
                    Sprite sprite = ImprovVisualConfig.Shared?.GetSprite(augId)
                                 ?? Resources.Load<Sprite>("AUG/" + augId.ToLower());
                    img.sprite  = sprite;
                    img.enabled = sprite != null;
                }

                var tmp = icon.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null)
                    tmp.text = count > 1 ? $"×{count}" : "";

                idx++;
            }
        }

        for (int i = idx; i < _improvIcons.Count; i++)
            _improvIcons[i].SetActive(false);
    }
}
