using UnityEngine;
using UnityEditor;

/// <summary>
/// TeleportBehaviour 에서 사용하는 AnimationEnd 이벤트를
/// 숨기/스폰 클립의 마지막 프레임에 자동으로 주입하는 에디터 도구.
///
/// 메뉴 위치: Tools/MuAge/Teleport Anim Event Injector
/// </summary>
public class TeleportAnimEventInjector : EditorWindow
{
    private AnimationClip _digClip;
    private AnimationClip _spawnClip;

    [MenuItem("Tools/MuAge/Teleport Anim Event Injector")]
    public static void Open() =>
        GetWindow<TeleportAnimEventInjector>("텔레포트 애님 이벤트 주입");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("TeleportBehaviour 이벤트 자동 주입", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.HelpBox(
            "각 클립의 마지막 프레임에 아래 Animation Event를 삽입합니다.\n\n" +
            "  숨기 클립 → OnAnimationEnd(\"dig\")\n" +
            "  스폰  클립 → OnAnimationEnd(\"spawn\")\n\n" +
            "TeleportBehaviour.OnAnimationEnd(string) 메서드가 수신합니다.",
            MessageType.Info);

        EditorGUILayout.Space(8);

        _digClip = (AnimationClip)EditorGUILayout.ObjectField(
            new GUIContent("숨기 애니메이션 클립", "bDigging=true 로 전환될 때 재생되는 클립"),
            _digClip, typeof(AnimationClip), false);

        _spawnClip = (AnimationClip)EditorGUILayout.ObjectField(
            new GUIContent("스폰 애니메이션 클립", "bDigging=false 로 전환될 때 재생되는 클립"),
            _spawnClip, typeof(AnimationClip), false);

        EditorGUILayout.Space(8);

        bool canInject = _digClip != null || _spawnClip != null;
        EditorGUI.BeginDisabledGroup(!canInject);

        if (GUILayout.Button("이벤트 주입", GUILayout.Height(30)))
        {
            int count = 0;
            if (_digClip   != null) { InjectEvent(_digClip,   "dig");   count++; }
            if (_spawnClip != null) { InjectEvent(_spawnClip, "spawn"); count++; }
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "주입 완료",
                $"{count}개 클립에 AnimationEnd 이벤트를 주입했습니다.",
                "확인");
        }

        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(4);
        if (GUILayout.Button("현재 이벤트 확인"))
            PreviewEvents();
    }

    // ── 이벤트 주입 ──────────────────────────────────────────

    private static void InjectEvent(AnimationClip clip, string param)
    {
        // 기존 이벤트 중 같은 함수+파라미터 조합은 제거 후 재삽입 (중복 방지)
        var existing = AnimationUtility.GetAnimationEvents(clip);
        int removed  = 0;
        var filtered = System.Array.FindAll(existing, e =>
        {
            bool isDuplicate = e.functionName == "OnAnimationEnd" && e.stringParameter == param;
            if (isDuplicate) removed++;
            return !isDuplicate;
        });

        var newEvent = new AnimationEvent
        {
            functionName    = "OnAnimationEnd",
            stringParameter = param,
            // clip.length에서 극소량 앞 — 정확히 마지막 프레임에 발동
            time            = Mathf.Max(0f, clip.length - 1f / GetFrameRate(clip)),
        };

        var result = new AnimationEvent[filtered.Length + 1];
        filtered.CopyTo(result, 0);
        result[filtered.Length] = newEvent;

        AnimationUtility.SetAnimationEvents(clip, result);
        EditorUtility.SetDirty(clip);

        string msg = removed > 0
            ? $"[Injector] '{clip.name}' — 기존 이벤트 {removed}개 교체 → OnAnimationEnd(\"{param}\") @ {newEvent.time:0.000}s"
            : $"[Injector] '{clip.name}' — OnAnimationEnd(\"{param}\") 주입 완료 @ {newEvent.time:0.000}s";
        Debug.Log(msg);
    }

    private static float GetFrameRate(AnimationClip clip)
    {
        float fps = clip.frameRate;
        return fps > 0f ? fps : 30f;
    }

    // ── 이벤트 미리보기 ──────────────────────────────────────

    private void PreviewEvents()
    {
        PrintClipEvents(_digClip,   "숨기");
        PrintClipEvents(_spawnClip, "스폰");
    }

    private static void PrintClipEvents(AnimationClip clip, string label)
    {
        if (clip == null) return;
        var events = AnimationUtility.GetAnimationEvents(clip);
        if (events.Length == 0)
        {
            Debug.Log($"[Injector] [{label}] '{clip.name}': 등록된 이벤트 없음");
            return;
        }
        foreach (var e in events)
            Debug.Log($"[Injector] [{label}] '{clip.name}' @{e.time:0.000}s → {e.functionName}(\"{e.stringParameter}\")");
    }
}
