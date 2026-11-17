using System.IO;
using ChouninXR.EditorSim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChouninXR.EditorTools
{
    // プロジェクトの初期設定をコードで再現できるようにしておく。
    // メニュー: ChouninXR > Setup > ...  /  バッチ: -executeMethod ChouninXR.EditorTools.ProjectSetup.RunAll
    public static class ProjectSetup
    {
        private const string SettingsDir = "Assets/ChouninXR/Settings";
        private const string ScenesDir = "Assets/ChouninXR/Scenes";
        private const string SimScenePath = ScenesDir + "/EditorSim.unity";

        [MenuItem("ChouninXR/Setup/Run All")]
        public static void RunAll()
        {
            EnsureUrp();
            EnsureTmpEssentials();
            CreateEditorSimScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[ChouninXR] Setup 完了");
        }

        [MenuItem("ChouninXR/Setup/URP")]
        public static void EnsureUrp()
        {
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset)
            {
                return;
            }
            Directory.CreateDirectory(SettingsDir);

            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, SettingsDir + "/ChouninURP_Renderer.asset");

            var urp = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(urp, SettingsDir + "/ChouninURP.asset");

            GraphicsSettings.defaultRenderPipeline = urp;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("ChouninXR/Setup/TMP Essentials")]
        public static void EnsureTmpEssentials()
        {
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            {
                return;
            }
            TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
        }

        [MenuItem("ChouninXR/Setup/EditorSim Scene")]
        public static void CreateEditorSimScene()
        {
            Directory.CreateDirectory(ScenesDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(100f, 1f, 100f);

            // 高さの吸着を試すための台（高さ 0.5m）
            var stage = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stage.name = "Stage";
            stage.transform.position = new Vector3(-2.5f, 0.25f, 3f);
            stage.transform.localScale = new Vector3(1.2f, 0.5f, 1.2f);

            var worldGo = new GameObject("SimWorld");
            var world = worldGo.AddComponent<SimWorld>();
            world.surface = worldGo.AddComponent<SimSurface>();

            var spawnerGo = new GameObject("ChouninSpawner");
            var spawner = spawnerGo.AddComponent<ChouninSpawner>();
            spawner.world = world;
            world.spawner = spawner;

            var editor = new GameObject("PlacementEditor").AddComponent<PlacementEditor>();
            editor.spawner = spawner;

            var camGo = new GameObject("Viewer");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 1.6f, 0f);
            var player = camGo.AddComponent<SimPlayer>();
            player.spawner = spawner;
            player.world = world;
            player.editor = editor;
            world.viewer = camGo.transform;

            EditorSceneManager.SaveScene(scene, SimScenePath);
            if (!System.Linq.Enumerable.Any(EditorBuildSettings.scenes, s => s.path == SimScenePath))
            {
                var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes)
                {
                    new EditorBuildSettingsScene(SimScenePath, true),
                };
                EditorBuildSettings.scenes = list.ToArray();
            }
        }
    }
}
