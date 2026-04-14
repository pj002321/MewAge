using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviourSingleton<GameManager>
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private Image waveIconImg;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Slider          expSlider;
    [SerializeField] private Slider          hpSlider;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private GameObject         gameOverPanel;       // 부활 패널
    [SerializeField] private GameOverResultPanel gameOverResultPanel; // 게임오버 결과 패널
    [SerializeField] private GameClearPanel  gameClearPanel;

    [Header("Wave 타이머")]
    // Normal wave durations: Wave 1~9=20s, 11~19=30s, 21~29=35s. Boss wave: 90s.
    [SerializeField] private float bossWaveTimeLimit = 90f;  // 보스 웨이브 제한 시간 (초)

    [Header("Wave Clear 노티")]
    [SerializeField] private GameObject waveClearPanel;
    [SerializeField] private float      waveClearDuration = 1f;

    [Header("타이머 색상")]
    [SerializeField] private Color timerNormalColor = Color.white;
    [SerializeField] private Color timerWarningColor = Color.red;
    [SerializeField] private float timerWarningThreshold = 5f;

    [Header("웨이브 아이콘 색상")]
    [SerializeField] private Color normalWaveColor = Color.cyan;
    [SerializeField] private Color bossWaveColor = Color.red;

    [Header("게임 시작 낙하 연출")]
    [SerializeField] private float         dropHeight       = 6f;    // 맵 중앙 위로 몇 유닛 위에서 시작
    [SerializeField] private float         dropGravity      = 28f;   // 낙하 가속도
    [SerializeField] private TextMeshProUGUI notifyLabel;            // 알림 텍스트 레이블
    [SerializeField] private float         notifyFadeIn     = 0.35f;
    [SerializeField] private float         notifyHold       = 1.0f;
    [SerializeField] private float         notifyFadeOut    = 0.45f;

    private int   wave = 0;
    private float waveTimer;
    private bool  isGameOver;
    private bool  waitingForUpgrade = true; // 트랜지션/초기화 기간 중 0초 달성으로 증강이 뜨는 것 방지
    private bool  _pauseTimer;              // 웨이브 시작 연출 중 타이머 일시 정지
    private bool  _isCountingDown;          // 카운트다운 중 타이머 UI 업데이트 방지
    private bool  bossWaveActive;
    private string currentChapterID = "C1_S1";

    // Session stats (accumulated during a single run for the Clear Panel)
    private int sessionExpGained  = 0;
    private int sessionGoldGained = 0;

    // 앱 전환 / 알림바 등 시스템 인터럽트 대응
    private bool  _systemPaused;
    private float _savedTimeScale;

    protected override bool DontDestroy => false;

    private void Start()
    {
        StartCoroutine(InitAndStartRoutine());
    }

    private IEnumerator InitAndStartRoutine()
    {
        // 1. 유니티 엔진과 모바일 기기(Safe Area, 해상도 등)가 씬 로딩 직후 초기화될 시간을 1프레임 대기합니다.
        yield return null;

        // 2. 가장 민감한 화면 비율(Aspect)과 관련된 크기 계산을 명시적으로 실행합니다.
        CameraFollow.Shared?.Init();

        var fitSprites = FindObjectsOfType<FitSpriteToScreen>();
        foreach (var fs in fitSprites)
        {
            fs.Fit();
        }

        // 3. 게임 시스템 및 UI 초기화
        if (gameOverPanel)               gameOverPanel.SetActive(false);
        if (waveClearPanel)              waveClearPanel.SetActive(false);
        if (gameClearPanel != null)      gameClearPanel.gameObject.SetActive(false);

        // Activate VirtualJoystick for in-game control
        if (VirtualJoystick.Instance != null)
            VirtualJoystick.Instance.gameObject.SetActive(true);

        currentChapterID = PlayerPrefs.GetString("SelectedChapterID", "C1_S1");
        Debug.Log($"[GameManager] 입장 — ChapterID: {currentChapterID}");
        UpgradeManager.Shared?.SetCurrentChapterID(currentChapterID);
        WaveEnvironmentSwitcher.Shared?.SetStage(currentChapterID);
        EnemySpawner.Shared?.SetChapterStage(currentChapterID);

        // 이전 판 증강 수치 초기화 후 캐릭터 기초 스탯 + 기물 시너지 일괄 반영
        // BeginBatch ~ EndBatch 사이 SetBase/AddPctMod는 마지막 EndBatch에서 1회만 적용됨
        var sm = PlayerStatManager.Shared;
        sm?.ResetMods();
        sm?.BeginBatch();
        PlayerCharacterManager.Shared?.ApplyCharacterStats();
        WareSynergySystem.ApplySynergies();
        sm?.EndBatch();

        // 시작 시 Normal 무기 1개 지급 준비
        UpgradeManager.Shared?.GiveStarterWeapon();

        // GameManager.Shared가 준비된 시점에 HP UI 강제 동기화
        if (PlayerController.Instance != null)
            UpdateHP(PlayerController.Instance.CurrentHP, PlayerController.Instance.MaxHPValue);

        // 4. 모든 인게임 초기화가 끝났으므로 로딩 씬에게 내려가도 좋다고 신호를 보냅니다.
        SceneLoadManager.NotifyStageReady();

        // BGM 재생
        SoundManager.Shared?.PlayGameBGM();

        // 5. 오버레이 연출 중 낙하 입장 연출을 시작합니다.
        //    ThrowOut이 끝나기 전에 이미 연출이 진행되므로 자연스럽게 이어집니다.
        StartCoroutine(PlayerLandingIntroRoutine());
    }

    private void Update()
    {
        if (isGameOver) return;

        if (!_pauseTimer) waveTimer -= Time.deltaTime;
        UpdateTimerUI();

        if (waveTimer <= 0f && !waitingForUpgrade)
        {
            if (bossWaveActive)
            {
                // Boss wave timeout → Game Over
                this.DLog("[GameManager] Boss wave time limit reached! Game Over.");
                TriggerGameOver();
            }
            else
            {
                waitingForUpgrade = true;
                StartCoroutine(WaveClearRoutine());
            }
        }
    }

    private IEnumerator PauseAnimAfterSeconds(Animator anim, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (anim != null) anim.speed = 0f;
    }
    private IEnumerator WaveClearRoutine()
    {
        if (waveClearPanel) waveClearPanel.SetActive(true);
        EnemySpawner.Shared?.StopSpawning();

        // 적 사망 연출 + 기물 스케일다운 후 파괴 (DieRoutine 기준 0.45s)
        yield return StartCoroutine(ClearAllWithScaleDown(0.45f));

        FXManager.Shared?.ClearAllFX();          // 투사체·파편 즉시 제거
        GlobalLightDimmer.ForceReset();           // 조명 즉시 복구

        yield return new WaitForSeconds(waveClearDuration);

        // 남은 orb 위로 튕겨 올리고 잠깐 대기 후 패널 전환
        ItemDropManager.Shared?.ThrowUpAll();
        yield return new WaitForSeconds(0.55f);

        if (waveClearPanel) waveClearPanel.SetActive(false);

        // 최종 웨이브 클리어 → 게임 클리어 패널
        if (wave >= GetMaxWave())
        {
            if (gameClearPanel != null)
                gameClearPanel.Show(wave, sessionExpGained, sessionGoldGained);
            else
                GoHome();
            yield break;
        }

        if (ImprovManager.Shared != null)
            ImprovManager.Shared.ShowPanel(wave);
        else if (UpgradeManager.Shared != null)
            UpgradeManager.Shared.ShowPanel(wave);
        else
            StartNextWave();
    }

    /// <summary>
    /// 적: 기존 사망 연출(흰색 실루엣·파편·날아가기) 재생.
    /// 기물: 스케일다운 후 파괴.
    /// duration은 DieRoutine(0.4f)보다 여유 있게 설정할 것.
    /// </summary>
    private IEnumerator ClearAllWithScaleDown(float duration)
    {
        // 적 — ForceDie()로 사망 연출 시작 (DieRoutine이 pool 반환까지 처리)
        for (int i = EnemyController.All.Count - 1; i >= 0; i--)
        {
            var e = EnemyController.All[i];
            if (e != null) e.ForceDie();
        }

        // 기물 — 스케일다운
        var targets    = new System.Collections.Generic.List<Transform>();
        var origScales = new System.Collections.Generic.List<Vector3>();
        foreach (var sc in StructureController.All)
        {
            if (sc != null)
            {
                targets.Add(sc.transform);
                origScales.Add(sc.transform.localScale);
            }
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < targets.Count; i++)
                if (targets[i] != null)
                    targets[i].localScale = Vector3.Lerp(origScales[i], Vector3.zero, t);
            yield return null;
        }

        // 기물 파괴 + 아직 남은 적 강제 정리
        StructureController.DestroyAll();
        EnemySpawner.Shared?.ClearAllEnemies();
    }

    /// <summary>웨이브 번호에 따른 제한 시간 반환. 보스 웨이브는 호출 전에 별도 처리.</summary>
    private float WaveDuration(int w)
    {
        // Boss wave: handled separately in StartNextWave
        if      (w >= 21) return 35f;  // Wave 21~29+
        else if (w >= 11) return 30f;  // Wave 11~19
        else              return 20f;  // Wave 1~9
    }

    private void UpdateTimerUI()
    {
        if (timerText == null || _isCountingDown) return;
        int secs = Mathf.CeilToInt(Mathf.Max(0f, waveTimer));
        timerText.text  = $"{secs / 60:00}:{secs % 60:00}";
        timerText.color = waveTimer <= timerWarningThreshold ? timerWarningColor : timerNormalColor;
    }

    private static readonly Vector3 WaveTextNormalScale = Vector3.one;
    private static readonly Vector3 WaveTextBigScale   = new Vector3(1.8f, 1.8f, 1f);
    private Coroutine _waveTextAnim;

    private Sprite _sprWaveNormal;
    private Sprite _sprWaveBoss;

    private void UpdateWaveUI(bool isBossWave = false)
    {
        if (waveText)
        {
            waveText.text = $"{wave}";
            if (_waveTextAnim != null) StopCoroutine(_waveTextAnim);
            _waveTextAnim = StartCoroutine(WaveTextBumpAnim());
        }
        if (timerText) timerText.color = timerNormalColor;

        // 웨이브 아이콘 교체
        if (waveIconImg != null)
        {
            if (isBossWave)
            {
                if (_sprWaveBoss == null)
                    _sprWaveBoss = Resources.Load<Sprite>("UI/wave_boss");
                if (_sprWaveBoss != null) waveIconImg.sprite = _sprWaveBoss;
                waveIconImg.color = bossWaveColor;
            }
            else
            {
                if (_sprWaveNormal == null)
                    _sprWaveNormal = Resources.Load<Sprite>("UI/wave_normal");
                if (_sprWaveNormal != null) waveIconImg.sprite = _sprWaveNormal;
                waveIconImg.color = normalWaveColor;
            }
        }
    }

    private IEnumerator WaveTextBumpAnim()
    {
        const float upTime   = 0.08f;
        const float downTime = 0.18f;

        var t = waveText.transform;
        float elapsed = 0f;
        // 확대
        while (elapsed < upTime)
        {
            elapsed += Time.unscaledDeltaTime;
            t.localScale = Vector3.Lerp(WaveTextNormalScale, WaveTextBigScale, elapsed / upTime);
            yield return null;
        }
        t.localScale = WaveTextBigScale;

        elapsed = 0f;
        // 원본 복구
        while (elapsed < downTime)
        {
            elapsed += Time.unscaledDeltaTime;
            t.localScale = Vector3.Lerp(WaveTextBigScale, WaveTextNormalScale, elapsed / downTime);
            yield return null;
        }
        t.localScale = WaveTextNormalScale;
        _waveTextAnim = null;
    }

    public void UpdateHP(float current, float max)
    {
        if (hpSlider)
        {
            hpSlider.minValue = 0f;
            hpSlider.maxValue = Mathf.Max(1f, max);
            hpSlider.value    = current;
        }
        if (hpText) hpText.text = $"{Mathf.CeilToInt(current)}";
    }

    public void UpdateGoldUI(int gold)
    {
        if (goldText) goldText.text = $"{gold}";
    }

    public void UpdateExpUI(int exp, int expMax, int level)
    {
        if (levelText)        levelText.text         = $"LV.{level}";
        if (expSlider)        expSlider.value        = (float)exp / expMax;
    }

    public void TriggerGameOver()
    {
        if (isGameOver) return;
        isGameOver = true;
        EnemySpawner.Shared?.StopSpawning();

        // 최고 웨이브 저장
        string prefKey = $"BestWave_{currentChapterID}";
        int currentBest = PlayerPrefs.GetInt(prefKey, 0);
        if (wave > currentBest)
        {
            PlayerPrefs.SetInt(prefKey, wave);
            PlayerPrefs.Save();
        }

        if (gameOverPanel) gameOverPanel.SetActive(true);
        // GameOverPanel.cs will count down 5 s, shrink itself, then call ShowGameClearAfterGameOver()
    }

    /// <summary>
    /// 부활 패널 카운트다운 종료 후 호출 — 게임오버 결과 패널 표시.
    /// </summary>
    public void ShowGameOverResult()
    {
        if (gameOverResultPanel != null)
            gameOverResultPanel.Show(wave);
        else
            GoHome();
    }

    public void Revive()
    {
        if (!isGameOver) return;
        isGameOver = false;
        if (gameOverPanel)       gameOverPanel.SetActive(false);
        if (gameOverResultPanel) gameOverResultPanel.gameObject.SetActive(false);
        PlayerController.Instance?.Revive();
        EnemySpawner.Shared?.ResumeSpawning(wave);
    }

    public void RestartGame()
    {
        SceneLoadManager.LoadScene("stage@scene");
    }

    public void GoHome()
    {
        Time.timeScale = 1f;
        SceneLoadManager.LoadScene("home@scene");
    }

    public void StartNextWave()
    {
        wave++;
        waitingForUpgrade = false;

        // 웨이브 시작 시 MaxHP의 10% 회복 (만땅이어도 텍스트 표시)
        var player = PlayerController.Instance;
        if (player != null)
        {
            float waveHeal = player.MaxHPValue * 0.1f;
            player.HealHP(waveHeal);
            FXManager.Shared?.SpawnPlayerText("+10%", Color.green);
        }

        // WaveHealHP 패시브 — 추가 축적된 회복량
        if (player != null && player.WaveHealAmount > 0f)
            player.HealHP(player.WaveHealAmount);

        bool isBossWave    = EnemySpawner.Shared?.GetNextBossWave(wave - 1) == wave;
        bool needsExitGate = isBossWave;

        // Boss wave gets its own time limit; normal waves use stepped duration
        waveTimer = isBossWave ? bossWaveTimeLimit : WaveDuration(wave);

        this.DLog($"[GameManager] StartNextWave wave={wave} | isBossWave={isBossWave} | needsGate={needsExitGate} | timer={waveTimer}s");

        UpdateWaveUI(isBossWave);

        // 보스 전/후: 업그레이드 패널 닫힘 → 게이트 활성화 → 플레이어 이동 → 실제 웨이브 시작
        if (needsExitGate)
        {
            waitingForUpgrade = true;  // 타이머 카운트다운 억제

            if (notifyLabel != null)
                StartCoroutine(ShowNotifyRoutine("길을 따라 이동하세요"));

            WaveEnvironmentSwitcher.Shared?.ActivateExitGate(() =>
            {
                waitingForUpgrade = false;
                DoStartWave(isBossWave);
            });
            return;
        }

        DoStartWave(isBossWave);
    }

    private void DoStartWave(bool isBossWave)
    {
        if (isBossWave && BossIntroSequence.Shared != null)
        {
            WaveEnvironmentSwitcher.Shared?.SetWave(wave); // 일반 맵 유지 (출구 연출용)
            BossIntroSequence.Shared.Play(() =>
            {
                EnemySpawner.Shared?.ResumeSpawning(wave);
            });
            return;
        }

        // 일반 웨이브 시작 시 플레이어를 맵 중앙으로 이동
        TeleportPlayerToMapCenter();

        EnemySpawner.Shared?.ResumeSpawning(wave);
        WaveEnvironmentSwitcher.Shared?.SetWave(wave);

        // 타이머 텍스트 확대→복귀 연출 후 카운트다운 시작
        StartCoroutine(TimerStartAnimation());
    }

    private IEnumerator TimerStartAnimation()
    {
        if (timerText == null) yield break;

        _pauseTimer = true;
        var rt = timerText.rectTransform;
        Vector3 baseScale = rt.localScale;
        Vector3 bigScale  = baseScale * 1.4f;

        // 즉시 확대
        rt.localScale = bigScale;

        // 빠르게 원본으로 복귀 (0.35s)
        float elapsed = 0f;
        const float dur = 0.35f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(bigScale, baseScale, Mathf.Clamp01(elapsed / dur));
            yield return null;
        }
        rt.localScale = baseScale;

        _pauseTimer = false;
    }

    private void TeleportPlayerToMapCenter()
    {
        var player = PlayerController.Instance;
        if (player == null) return;

        if (MovementBounds.Shared != null)
        {
            MovementBounds.Shared.GetPlayerBoundsMinMax(out Vector2 min, out Vector2 max);
            player.transform.position = (Vector3)((min + max) * 0.5f);
        }
        else
        {
            player.transform.position = Vector3.zero;
        }
    }

    // ── 게임 시작 낙하 입장 연출 ─────────────────────────────────────

    /// <summary>
    /// 첫 웨이브 시작 전 플레이어가 맵 중앙 위에서 낙하하는 입장 연출.
    /// 착지 후 노티 텍스트를 표시하고 StartNextWave()를 호출합니다.
    /// </summary>
    private IEnumerator PlayerLandingIntroRoutine()
    {
        if (timerText) timerText.gameObject.SetActive(false);

        var player = PlayerController.Instance;
        if (player == null) { StartNextWave(); yield break; }

        player.InputLocked = true;

        // floatinganchor를 찾아 플레이어를 자식으로 설정하고 로컬 포지션 0,0,0으로 설정
        var floatingAnchor = GameObject.Find("floatinganchor");
        if (floatingAnchor != null)
        {
            player.transform.SetParent(floatingAnchor.transform, false);
            player.transform.localPosition = Vector3.zero;
        }

        // 맵 중앙 착지 위치 계산
        Vector2 landPos = Vector2.zero;
        if (MovementBounds.Shared != null)
        {
            MovementBounds.Shared.GetPlayerBoundsMinMax(out Vector2 bMin, out Vector2 bMax);
            landPos = (bMin + bMax) * 0.5f;
        }

        // 1) 대기하기 '전에' 플레이어를 미리 공중에 띄워둠 (바닥에 서 있는 모습 노출 방지)
        var rb = player.GetComponent<Rigidbody2D>();
        Vector2 startPos = landPos + Vector2.up * dropHeight;
        if (rb != null) { rb.position = startPos; rb.linearVelocity = Vector2.zero; }
        else             player.transform.position = startPos;

        // 2) 로딩 오버레이가 완전히 걷힐 때까지 대기
        while (SceneLoadManager.IsTransitioning)
        {
            // 대기 중 물리 엔진이나 다른 로직으로 인해 떨어질 가능성을 대비하여 위치 고정
            if (rb != null) rb.position = startPos;
            else            player.transform.position = startPos;
            yield return null;
        }

        if (timerText) timerText.gameObject.SetActive(true);

        // 3) 가속하며 낙하 시작
        float fallSpeed = 0f;
        while (true)
        {
            yield return new WaitForFixedUpdate();
            fallSpeed += dropGravity * Time.fixedDeltaTime;
            Vector2 cur  = rb != null ? rb.position : (Vector2)player.transform.position;
            Vector2 next = cur + Vector2.down * fallSpeed * Time.fixedDeltaTime;
            if (next.y <= landPos.y)
            {
                if (rb != null) { rb.MovePosition(landPos); rb.linearVelocity = Vector2.zero; }
                else              player.transform.position = landPos;
                break;
            }
            if (rb != null) rb.MovePosition(next);
            else             player.transform.position = next;
        }

        // 착지 스쿼시 연출
        yield return StartCoroutine(LandingSquishRoutine(player.transform));

        // 카운트다운
        yield return StartCoroutine(CountdownRoutine());

        player.InputLocked = false;
        StartNextWave();
    }

    /// <summary>착지 시 Y 찌그러짐 → 복귀 바운스 연출.</summary>
    private IEnumerator LandingSquishRoutine(Transform t)
    {
        Vector3 orig    = t.localScale;
        Vector3 squish  = new Vector3(orig.x * 1.35f, orig.y * 0.5f,  orig.z);
        Vector3 stretch = new Vector3(orig.x * 0.88f, orig.y * 1.22f, orig.z);

        // 찌그러짐
        for (float e = 0f; e < 0.07f; e += Time.deltaTime)
        {
            t.localScale = Vector3.Lerp(orig, squish, e / 0.07f);
            yield return null;
        }
        t.localScale = squish;

        // 위로 튀어오름
        for (float e = 0f; e < 0.10f; e += Time.deltaTime)
        {
            t.localScale = Vector3.Lerp(squish, stretch, e / 0.10f);
            yield return null;
        }
        t.localScale = stretch;

        // 원본 복귀
        for (float e = 0f; e < 0.14f; e += Time.deltaTime)
        {
            t.localScale = Vector3.Lerp(stretch, orig, e / 0.14f);
            yield return null;
        }
        t.localScale = orig;
    }

    /// <summary>노티 패널 페이드인 → 유지 → 페이드아웃.</summary>
    private IEnumerator ShowNotifyRoutine(string customMsg = null)
    {
        if (notifyLabel == null) yield break;

        if (!string.IsNullOrEmpty(customMsg))
            notifyLabel.text = customMsg;

        notifyLabel.gameObject.SetActive(true);

        Color c = notifyLabel.color;
        c.a = 0f;
        notifyLabel.color = c;

        for (float e = 0f; e < notifyFadeIn; e += Time.unscaledDeltaTime)
        {
            c.a = e / notifyFadeIn;
            notifyLabel.color = c;
            yield return null;
        }
        c.a = 1f;
        notifyLabel.color = c;

        yield return new WaitForSecondsRealtime(notifyHold);

        for (float e = 0f; e < notifyFadeOut; e += Time.unscaledDeltaTime)
        {
            c.a = 1f - e / notifyFadeOut;
            notifyLabel.color = c;
            yield return null;
        }
        c.a = 0f;
        notifyLabel.color = c;
        notifyLabel.gameObject.SetActive(false);
    }

    /// <summary>게임 시작 전 카운트다운 (3 → 0).</summary>
    private IEnumerator CountdownRoutine()
    {
        if (timerText == null) yield break;

        _isCountingDown = true;
        timerText.gameObject.SetActive(true);
        Color c = timerText.color;
        c.a = 1f;
        timerText.color = c;

        for (int i = 3; i >= 0; i--)
        {
            timerText.text = i == 0 ? "SURVIVE!" : $"0{i}";
            yield return new WaitForSecondsRealtime(1f);
        }

        if (notifyLabel != null) notifyLabel.gameObject.SetActive(false);
        _isCountingDown = false;
    }

    public bool IsGameOver  => isGameOver;
    public int  CurrentWave => wave;

    /// <summary>현재 챕터+스테이지의 DB_웨이브에서 최대 웨이브 번호를 반환합니다.</summary>
    private int GetMaxWave()
    {
        // currentChapterID 형식: "C1_S1"
        string prefix = currentChapterID + "_W";
        int max = 0;
        Mewage.DB.DB_웨이브.ForEachEntity(row =>
        {
            if (row.f_웨이브ID != null && row.f_웨이브ID.StartsWith(prefix))
            {
                string numStr = row.f_웨이브ID.Substring(prefix.Length);
                if (int.TryParse(numStr, out int n) && n > max)
                    max = n;
            }
        });
        return max > 0 ? max : 30; // DB에 없으면 30 fallback
    }

    // ── 모바일 앱 생명주기 ────────────────────────────────────────
    // OnApplicationFocus : 알림바 내리기, 알림바 롱프레스 미리보기 등 포커스 손실 처리
    // OnApplicationPause : 앱 전환(백그라운드) 처리
    // 두 이벤트 모두 _systemPaused 플래그로 중복 호출 방지
    private void OnApplicationFocus(bool hasFocus)  => HandleSystemPause(!hasFocus);
    private void OnApplicationPause(bool pauseStatus) => HandleSystemPause(pauseStatus);

    private void HandleSystemPause(bool pausing)
    {
        if (pausing)
        {
            if (_systemPaused) return;           // 이미 시스템 일시정지 상태
            _systemPaused   = true;
            _savedTimeScale = Time.timeScale;
            Time.timeScale  = 0f;
            AudioListener.pause = true;
        }
        else
        {
            if (!_systemPaused) return;          // 시스템 일시정지가 아니면 무시
            _systemPaused  = false;
            Time.timeScale = _savedTimeScale;    // 패널이 열려있었으면 0 복원, 게임 중이었으면 1 복원
            AudioListener.pause = false;
        }
    }

    /// <summary>현재 챕터의 ChapterRosterSO에서 fromWave보다 큰 가장 가까운 보스 웨이브 번호를 반환합니다. 없으면 -1.</summary>
    public int GetNextBossWave(int fromWave)
    {
        return EnemySpawner.Shared?.GetNextBossWave(fromWave) ?? -1;
    }

    // ── 보스 웨이브 ──────────────────────────────────────────────
    public void StartBossWave()
    {
        bossWaveActive = true;
        WaveEnvironmentSwitcher.Shared?.SetBoss();
    }

    [Header("보스 처치 후 Orb 수집 대기")]
    [SerializeField] private float orbCollectWait = 5f;   // 업그레이드 패널 전 대기 시간(초)

    public void OnBossDefeated()
    {
        bossWaveActive = false;
        WaveEnvironmentSwitcher.Shared?.SetNormal();
        EnemySpawner.Shared?.StopSpawning(); // 보스 죽음 즉시 스폰 중지
        if (waitingForUpgrade) return;
        waitingForUpgrade = true;

        int nextBoss = GetNextBossWave(wave);
        this.DLog($"[GameManager] OnBossDefeated wave={wave} nextBoss={nextBoss}");

        // Orb 수집 대기 카운트다운 후 분기
        StartCoroutine(BossOrbCollectRoutine(nextBoss));
    }

    /// <summary>
    /// 보스 처치 후 orbCollectWait 초 동안 타이머 UI로 카운트다운을 보여주고,
    /// 종료 후 중간 보스면 WaveClearRoutine, 마지막 보스면 ShowGameClear를 호출합니다.
    /// </summary>
    private IEnumerator BossOrbCollectRoutine(int nextBoss)
    {
        // 타이머를 수집 대기 시간으로 덮어써 Update가 카운트다운 UI를 그리게 함
        waveTimer = orbCollectWait;

        this.DLog($"[GameManager] Orb 수집 대기 {orbCollectWait}s 시작");
        yield return new WaitForSeconds(orbCollectWait);

        if (nextBoss > 0)
            StartCoroutine(WaveClearRoutine());   // 중간 보스 → 업그레이드 패널
        else
            ShowGameClear();                      // 마지막 보스 → 게임 클리어
    }

    private void ShowGameClear()
    {
        EnemySpawner.Shared?.StopSpawning();
        EnemySpawner.Shared?.ClearAllEnemies();

        if (gameClearPanel != null)
        {
            gameClearPanel.Show(wave, sessionExpGained, sessionGoldGained);
        }
        else
        {
            this.DLog("[GameManager] GameClearPanel not assigned — showing GameOver as fallback.");
            if (gameOverPanel) gameOverPanel.SetActive(true);
        }
    }

    /// <summary>인게임 획득 골드/경험치를 누적합니다. ItemDropManager, EXP 지급 측에서 호출하세요.</summary>
    public void AddSessionGold(int amount) => sessionGoldGained += amount;
    public void AddSessionExp (int amount) => sessionExpGained  += amount;

    // ── 디버그 전용 ──────────────────────────────────────────────
    /// <summary>현재 웨이브를 즉시 종료합니다. 패널이 열려 있어도 강제 작동합니다.</summary>
    public void DebugSkipWave()
    {
        Debug.Log($"[Debug] SkipWave 호출 — wave={wave}, isGameOver={isGameOver}, waitingForUpgrade={waitingForUpgrade}, timeScale={Time.timeScale}");
        if (isGameOver) { Debug.LogWarning("[Debug] isGameOver=true → 중단"); return; }

        StopAllCoroutines();
        Time.timeScale    = 1f;
        waitingForUpgrade = false;

        // 보스 웨이브 중 스킵 시 보스 상태 초기화
        if (bossWaveActive)
        {
            bossWaveActive = false;
            WaveEnvironmentSwitcher.Shared?.SetNormal();
            BossHUDPanel.Shared?.Hide();
        }
        WaveEnvironmentSwitcher.Shared?.DeactivateExitGate();

        EnemySpawner.Shared?.StopSpawning();
        EnemySpawner.Shared?.ClearAllEnemies();
        FXManager.Shared?.ClearAllFX();

        Debug.Log("[Debug] 증강 스킵하고 바로 웨이브 시작");
        StartNextWave();
    }

    /// <summary>지정한 웨이브로 즉시 이동합니다.</summary>
    public void DebugJumpToWave(int targetWave)
    {
        if (isGameOver) return;

        StopAllCoroutines();
        Time.timeScale = 1f;
        EnemySpawner.Shared?.StopSpawning();
        EnemySpawner.Shared?.ClearAllEnemies();

        if (bossWaveActive)
        {
            bossWaveActive = false;
            WaveEnvironmentSwitcher.Shared?.SetNormal();
            BossHUDPanel.Shared?.Hide();
        }

        bossWaveActive    = false;
        wave              = targetWave - 1;
        waitingForUpgrade = false;

        // 보스 입장 게이트(Exit Point) 연출을 띄우려면, 활성화된 배경 맵이 해당 층(예: 9웨이브)의 맵이어야 합니다.
        // 직전 맵으로 강제 세팅하여 보스 문이 정상 활성화되도록 패치
        WaveEnvironmentSwitcher.Shared?.SetWave(wave);

        StartNextWave();
        Debug.Log($"[Debug] → Wave {targetWave}로 점프");
    }

    /// <summary>DB_웨이브에서 현재 웨이브보다 큰 가장 가까운 보스 웨이브로 이동합니다.</summary>
    public void DebugJumpToBossWave()
    {
        Debug.Log($"[Debug] JumpToBossWave 호출 — wave={wave}, isGameOver={isGameOver}, timeScale={Time.timeScale}");
        if (isGameOver) { Debug.LogWarning("[Debug] isGameOver=true → 중단"); return; }

        int targetWave = EnemySpawner.Shared?.GetNextBossWave(wave) ?? -1;

        if (targetWave < 0)
        {
            Debug.LogWarning("[Debug] 현재 웨이브보다 큰 보스 웨이브 항목이 없습니다.");
            return;
        }

        Debug.Log($"[Debug] → Boss Wave {targetWave}로 점프");

        StopAllCoroutines();
        Time.timeScale    = 1f;
        EnemySpawner.Shared?.StopSpawning();
        EnemySpawner.Shared?.ClearAllEnemies();

        bossWaveActive    = false;
        wave              = targetWave - 1;
        waitingForUpgrade = false;

        // 보스 입장 게이트(Exit Point) 연출을 띄우려면, 활성화된 배경 맵이 해당 층(예: 9웨이브)의 맵이어야 합니다.
        WaveEnvironmentSwitcher.Shared?.SetWave(wave);

        StartNextWave();
    }
}
