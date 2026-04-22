using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityToolbarExtender;

namespace Hades.Tool
{
    [InitializeOnLoad]
    public static class BuildToolBar
    {
        private static GUIStyle _debugStyle;
        private static GUIStyle _releaseStyle;

        static BuildToolBar()
        {
            ToolbarExtender.LeftToolbarGUI.Add(OnToolbarGUI);
        }

        private static void EnsureStyles()
        {
            if (_debugStyle != null) return;

            _debugStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 10,
                fontStyle = FontStyle.Bold,
                padding   = new RectOffset(10, 10, 2, 2),
            };
            _debugStyle.normal.textColor = new Color(1f, 0.65f, 0.1f);

            _releaseStyle = new GUIStyle(_debugStyle);
            _releaseStyle.normal.textColor = new Color(0.4f, 1f, 0.4f);
        }

        private static void OnToolbarGUI()
        {
            EnsureStyles();

            if (GUILayout.Button(new GUIContent("Debug Build", "MEWAGE_DEBUG 포함 빌드"), _debugStyle))
                Build(debug: true);

            if (GUILayout.Button(new GUIContent("Release Build", "MEWAGE_DEBUG 없는 릴리즈 빌드"), _releaseStyle))
                Build(debug: false);
        }

        private static void Build(bool debug)
        {
            string folder = EditorUtility.SaveFolderPanel(
                debug ? "Debug 빌드 폴더 선택" : "Release 빌드 폴더 선택", "", "");
            if (string.IsNullOrEmpty(folder)) return;

            // MEWAGE_DEBUG 심볼 설정/해제
            var target  = EditorUserBuildSettings.selectedBuildTargetGroup;
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
            var list    = new List<string>(defines.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries));
            list.Remove("MEWAGE_DEBUG");
            if (debug) list.Add("MEWAGE_DEBUG");
            PlayerSettings.SetScriptingDefineSymbolsForGroup(target, string.Join(";", list));

            // 빌드 설정에 등록된 씬 수집
            var scenes = new List<string>();
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled) scenes.Add(s.path);

            string ext        = EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64 ? ".exe" : "";
            string outputPath = Path.Combine(folder, PlayerSettings.productName + ext);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes           = scenes.ToArray(),
                locationPathName = outputPath,
                target           = EditorUserBuildSettings.activeBuildTarget,
                options          = BuildOptions.None,
            });

            Debug.Log($"[Build] {(debug ? "Debug" : "Release")} → {report.summary.result} : {outputPath}");
        }
    }
}
