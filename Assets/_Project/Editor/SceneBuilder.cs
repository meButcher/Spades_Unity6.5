using System.Collections.Generic;
using System.IO;
using Spades.Unity.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spades.Editor
{
    /// <summary>
    /// Creates the playable scene and the authored assets from code.
    ///
    /// The scene ends up containing three objects and not a single wired reference, because the
    /// entire view hierarchy is constructed at runtime by GameBootstrap. That removes the most
    /// common way a Unity project breaks on someone else's machine: a prefab or scene reference
    /// that was fine locally and is null after a clone.
    /// </summary>
    public static class SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string SettingsFolder = "Assets/_Project/Settings";
        private const string LayoutAssetPath = SettingsFolder + "/LayoutSettings.asset";
        private const string Rules4PPath = SettingsFolder + "/Rules_4P_500.asset";
        private const string Rules2PPath = SettingsFolder + "/Rules_2P_500.asset";
        private const string Rules4PShortPath = SettingsFolder + "/Rules_4P_200.asset";
        private const string AutoCreateKey = "Spades.MainSceneCreated.";

        [MenuItem("Spades/Create Main Scene", priority = 0)]
        public static void CreateMainScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            CreateSettingsAssets();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCamera();
            BuildBootstrap();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            RegisterAsFirstBuildScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Spades] Created " + ScenePath + " and set it as the first build scene. Press Play.");
        }

        [MenuItem("Spades/Open Main Scene", priority = 1)]
        public static void OpenMainScene()
        {
            if (!File.Exists(ScenePath))
            {
                CreateMainScene();
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        /// <summary>
        /// Builds the scene the first time this project is opened on a machine, so a fresh clone
        /// is playable without anyone having to find a menu item first. It runs at most once per
        /// project per user and does nothing if the scene already exists.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void CreateOnFirstOpen()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isPlaying) return;
                if (File.Exists(ScenePath)) return;

                string key = AutoCreateKey + Application.dataPath.GetHashCode();
                if (EditorPrefs.GetBool(key, false)) return;
                EditorPrefs.SetBool(key, true);

                Debug.Log("[Spades] First open: building " + ScenePath + ".");
                CreateMainScene();
            };
        }

        private static void BuildCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera));
            go.tag = "MainCamera";

            var camera = go.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.21f, 0.14f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;

            // Everything is drawn by a Screen Space Overlay canvas, so this camera exists only to
            // clear the frame. The render pipeline attaches its own camera data on first render.
            camera.cullingMask = 0;
            go.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void BuildBootstrap()
        {
            var go = new GameObject("Game Bootstrap", typeof(GameBootstrap));

            var serialized = new SerializedObject(go.GetComponent<GameBootstrap>());
            AssignIfPresent(serialized, "_layoutSettings", LayoutAssetPath, typeof(LayoutSettings));
            AssignIfPresent(serialized, "_fourPlayerRules", Rules4PPath, typeof(GameRulesAsset));
            AssignIfPresent(serialized, "_twoPlayerRules", Rules2PPath, typeof(GameRulesAsset));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignIfPresent(SerializedObject serialized, string propertyName, string assetPath, System.Type type)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) return;

            property.objectReferenceValue = AssetDatabase.LoadAssetAtPath(assetPath, type);
        }

        /// <summary>
        /// Creates the three ScriptableObjects the project uses. The two-hundred-point variant is
        /// there to make the point concrete: a rule variant is a duplicated asset, not a branch.
        /// </summary>
        private static void CreateSettingsAssets()
        {
            Directory.CreateDirectory(SettingsFolder);
            AssetDatabase.Refresh();

            CreateIfMissing<LayoutSettings>(LayoutAssetPath, null);

            CreateIfMissing<GameRulesAsset>(Rules4PPath, asset =>
            {
                asset.PlayerCount = 4;
                asset.TargetScore = 500;
                asset.UsesDrawPhase = false;
            });

            CreateIfMissing<GameRulesAsset>(Rules2PPath, asset =>
            {
                asset.PlayerCount = 2;
                asset.TargetScore = 500;
                asset.UsesDrawPhase = true;
            });

            CreateIfMissing<GameRulesAsset>(Rules4PShortPath, asset =>
            {
                asset.PlayerCount = 4;
                asset.TargetScore = 200;
                asset.UsesDrawPhase = false;
            });
        }

        private static void CreateIfMissing<T>(string path, System.Action<T> configure) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return;

            var asset = ScriptableObject.CreateInstance<T>();
            configure?.Invoke(asset);

            AssetDatabase.CreateAsset(asset, path);
        }

        private static void RegisterAsFirstBuildScene()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
