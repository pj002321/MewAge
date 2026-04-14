using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityToolbarExtender;
using Debug = UnityEngine.Debug;


namespace Hades.Tool
{
    [InitializeOnLoad]
    public class SceneToolBar
    {
        public static GUIStyle commandButtonStyle;
        static void CreateStyle()
        {
            commandButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageAbove,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(251 / 255f, 200 / 255f, 255 / 255f, 1.0f) },

            };

            var bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(126 / 255f, 160 / 255f, 185 / 255f, 0.7f));
            bgTex.Apply();
            commandButtonStyle.normal.background = bgTex;

            commandButtonStyle.padding = new RectOffset(10, 10, 2, 2);
        }
        static SceneToolBar()
        {
            ToolbarExtender.LeftToolbarGUI.Add(BuildToolbarLeft);
            ToolbarExtender.RightToolbarGUI.Add(BuildToolbarRight);
        }

        private static void BuildToolbarLeft()
        {
            if (commandButtonStyle == null) CreateStyle();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Run Game", "Start Run Game"), commandButtonStyle))
            {
                SceneHelper.StartScene("introloading@scene");
            }
          
        }

        private static void BuildToolbarRight()
        {
            if (commandButtonStyle == null) CreateStyle();
            if (GUILayout.Button(new GUIContent("Intro", "open Intro scene"), commandButtonStyle))
            {
                SceneHelper.OpenScene("introloading@scene");
            }
            if (GUILayout.Button(new GUIContent("Loading", "open loading scene"), commandButtonStyle))
            {
                SceneHelper.OpenScene("loading@scene");
            }
            if (GUILayout.Button(new GUIContent("Home", "open Home scene"), commandButtonStyle))
            {
                SceneHelper.OpenScene("home@scene");
            }
            if (GUILayout.Button(new GUIContent("Stage", "open Stage scene"), commandButtonStyle))
            {
                SceneHelper.OpenScene("stage@scene");
            }
           

            DrawSceneDropdown("dev", "open dev scene");
            GUILayout.FlexibleSpace();
        }

        static void DrawSceneDropdown(string label, string tooltip)
        {
            if (!GUILayout.Button(new GUIContent(label, tooltip), commandButtonStyle))
                return;
            var menu = new GenericMenu();
            var sceneNames = SceneHelper.GetSceneNamesByPrefix(label);
            foreach (var sceneName in sceneNames)
            {
                var name = sceneName;
                menu.AddItem(new GUIContent(sceneName), false, () => SceneHelper.OpenScene(name));
            }
            if (sceneNames.Length == 0)
                menu.AddDisabledItem(new GUIContent("(No scenes found)"));
            menu.ShowAsContext();
        }
    }

    static class SceneHelper
    {
        static string sceneToOpen;

        public static void StartScene(string sceneName)
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }

            sceneToOpen = sceneName;
            EditorApplication.update += OnUpdate;
        }

        static void OnUpdate()
        {
            if (sceneToOpen == null ||
                EditorApplication.isPlaying || EditorApplication.isPaused ||
                EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EditorApplication.update -= OnUpdate;

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                // need to get scene via search because the path to the scene
                // file contains the package version so it'll change over time
                string[] guids = AssetDatabase.FindAssets("t:scene " + sceneToOpen, null);
                if (guids.Length == 0)
                {
                    Debug.LogWarning("Couldn't find scene file");
                }
                else
                {
                    string scenePath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    EditorSceneManager.OpenScene(scenePath);
                    EditorApplication.isPlaying = true;
                }
            }
            sceneToOpen = null;
        }

        /// <summary>접두사로 검색한 씬 이름 목록 반환.</summary>
        public static string[] GetSceneNamesByPrefix(string prefix)
        {
            var guids = AssetDatabase.FindAssets("t:scene " + prefix, null);
            var list = new List<string>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".unity"))
                    list.Add(Path.GetFileNameWithoutExtension(path));
            }
            return list.OrderBy(x => x).ToArray();
        }

        public static void OpenScene(string sceneName)
        {
            string[] guids = AssetDatabase.FindAssets("t:scene " + sceneName, null);
            if (guids.Length == 0)
            {
                Debug.LogWarning("Couldn't find scene file");
            }
            else
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(guids[0]);
                EditorSceneManager.OpenScene(scenePath);
            }
        }
    }
}