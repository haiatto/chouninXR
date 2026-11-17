using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Google.XR.Extensions;
using Google.XR.Extensions.SpatialEntities;
using Unity.XR.CoreUtils;
using Unity.XR.CoreUtils.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace ChouninXR.AndroidXR.EditorTools
{
    // XREAL AURA（Android XR）向けの設定とシーンをコードで作る。
    // メニュー: ChouninXR > AURA > ...
    // バッチ: -buildTarget Android -executeMethod ChouninXR.AndroidXR.EditorTools.AndroidXrSetup.RunAll
    public static class AndroidXrSetup
    {
        public const string PackageName = "io.github.haiatto.chouninxr";
        private const string ScenePath = "Assets/ChouninXR/Scenes/Aura.unity";
        private const string BuildDir = "Builds/Aura";

        // 有効にする OpenXR 機能（型名で指定。パッケージ側の名前が変わっても探しやすいように）
        private static readonly string[] Features =
        {
            // Unity Android XR
            "AndroidXRSupportFeature",
            "ARSessionFeature",
            "ARAnchorFeature",
            // 直接配置の高さ合わせ（メッシュ → 平面 → 地面相当）
            "ARMeshFeature",
            "ARPlaneFeature",
            "ARRaycastFeature",
            // Google Android XR Extensions
            "XRSessionFeature",
            "XRGoogleCloudFeature",
            "XRGeospatialFeature",
            "SpatialShareableAnchorFeature",
            "XRUnboundedRefSpaceFeature",
            // XR Hands
            "HandTracking",
        };

        // 使わない（明示的に無効にする）機能
        // ・AndroidXRTrackpadGesturesInteraction：Google 側で実験的扱い（Play ストアに出せない）。
        //   AURA のトラックパッド対応は実機で調べてから決める
        private static readonly string[] DisabledFeatures =
        {
            "AndroidXRTrackpadGesturesInteraction",
        };

        [MenuItem("ChouninXR/AURA/Run All")]
        public static void RunAll()
        {
            ConfigurePlayer();
            ConfigureXr();
            FixValidationIssues();
            CreateScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[ChouninXR] AURA の設定が完了しました");
        }

        [MenuItem("ChouninXR/AURA/Player 設定")]
        public static void ConfigurePlayer()
        {
            var t = NamedBuildTarget.Android;
            PlayerSettings.companyName = "haiatto";
            PlayerSettings.productName = "町人XR";
            PlayerSettings.SetApplicationIdentifier(t, PackageName);
            PlayerSettings.SetScriptingBackend(t, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            // Geospatial は GPS を使う
            PlayerSettings.Android.forceInternetPermission = true;
        }

        [MenuItem("ChouninXR/AURA/OpenXR 設定")]
        public static void ConfigureXr()
        {
            // XR Plug-in Management: Android で OpenXR を使う
            var perTarget = GetOrCreatePerBuildTargetSettings();
            if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
            {
                perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            }
            var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            general.InitManagerOnStart = true;
            XRPackageMetadataStore.AssignLoader(general.AssignedSettings,
                "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android);
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(perTarget);

            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null)
            {
                Debug.LogError("[ChouninXR] Android の OpenXR 設定が見つかりません");
                return;
            }

            var all = settings.GetFeatures();
            foreach (var name in Features)
            {
                var f = all.FirstOrDefault(x => x.GetType().Name == name);
                if (f == null)
                {
                    Debug.LogWarning($"[ChouninXR] OpenXR 機能 {name} が見つかりません");
                    continue;
                }
                f.enabled = true;
                EditorUtility.SetDirty(f);
            }
            foreach (var f in all.Where(x => DisabledFeatures.Contains(x.GetType().Name)))
            {
                f.enabled = false;
                EditorUtility.SetDirty(f);
            }

            // Geospatial アンカー（地形・屋上アンカー）を使うには Shareable Anchors の Geospatial Host が要る
            var anchors = settings.GetFeature<SpatialShareableAnchorFeature>();
            if (anchors != null)
            {
                anchors.EnableGeospatialHost = true;
                EditorUtility.SetDirty(anchors);
            }

            // 認証は Keyless（API キーをアプリに入れない）。セッション開始時に自動で行われる
            var cloud = settings.GetFeature<XRGoogleCloudFeature>();
            if (cloud != null)
            {
                cloud.TrySetStrategy(XRAuthenticationStrategy.Keyless, string.Empty);
                EditorUtility.SetDirty(cloud);
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            foreach (var f in settings.GetFeatures().Where(x => x.enabled))
            {
                Debug.Log($"[ChouninXR] OpenXR(Android) 有効: {f.GetType().Name}");
            }
        }

        private static XRGeneralSettingsPerBuildTarget GetOrCreatePerBuildTargetSettings()
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,
                out XRGeneralSettingsPerBuildTarget perTarget);
            if (perTarget != null)
            {
                return perTarget;
            }
            perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            Directory.CreateDirectory("Assets/XR");
            AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            return perTarget;
        }

        // XR Plug-in Management > Project Validation の「自動で直せる問題」をまとめて直す
        [MenuItem("ChouninXR/AURA/Project Validation を自動修正")]
        public static void FixValidationIssues()
        {
            FixOpenXrIssues();
            var prop = typeof(BuildValidator).GetProperty("PlatformRules",
                BindingFlags.NonPublic | BindingFlags.Static);
            var dict = prop?.GetValue(null) as Dictionary<BuildTargetGroup, List<BuildValidationRule>>;
            if (dict == null || !dict.TryGetValue(BuildTargetGroup.Android, out var rules))
            {
                Debug.LogWarning("[ChouninXR] Project Validation のルールが取れませんでした");
                return;
            }

            for (int pass = 0; pass < 3; pass++)
            {
                int fixedCount = 0;
                foreach (var r in rules)
                {
                    if (!SafeBool(r.IsRuleEnabled, true) || r.SceneOnlyValidation)
                    {
                        continue;
                    }
                    if (SafeBool(r.CheckPredicate, true))
                    {
                        continue;
                    }
                    if (r.FixIt != null && r.FixItAutomatic)
                    {
                        try
                        {
                            r.FixIt();
                            fixedCount++;
                            Debug.Log($"[ChouninXR] 自動修正: {r.Category} / {r.Message}");
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[ChouninXR] 修正失敗: {r.Message}\n{e.Message}");
                        }
                    }
                }
                if (fixedCount == 0)
                {
                    break;
                }
            }

            foreach (var r in rules)
            {
                if (SafeBool(r.IsRuleEnabled, true) && !r.SceneOnlyValidation && !SafeBool(r.CheckPredicate, true))
                {
                    Debug.LogWarning($"[ChouninXR] 未解決（{(r.Error ? "エラー" : "警告")}）: {r.Category} / {r.Message}");
                }
            }
        }

        // OpenXR 機能ごとの検証（ビルド時に止まるのはこちら）
        private static void FixOpenXrIssues()
        {
            for (int pass = 0; pass < 3; pass++)
            {
                var issues = new List<OpenXRFeature.ValidationRule>();
                UnityEditor.XR.OpenXR.OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Android);
                var fixable = issues.Where(i => i.fixIt != null && i.fixItAutomatic).ToList();
                foreach (var i in fixable)
                {
                    try
                    {
                        i.fixIt();
                        Debug.Log($"[ChouninXR] OpenXR 自動修正: {i.message}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[ChouninXR] OpenXR 修正失敗: {i.message}\n{e.Message}");
                    }
                }
                if (fixable.Count == 0)
                {
                    foreach (var i in issues)
                    {
                        Debug.LogWarning($"[ChouninXR] OpenXR 未解決（{(i.error ? "エラー" : "警告")}）: {i.message}");
                    }
                    break;
                }
            }
        }

        private static bool SafeBool(Func<bool> f, bool fallback)
        {
            try
            {
                return f == null ? fallback : f();
            }
            catch
            {
                return fallback;
            }
        }

        [MenuItem("ChouninXR/AURA/シーン作成")]
        public static void CreateScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 太陽（影の向きは拡張で時刻と位置から計算する予定）
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            new GameObject("AR Session").AddComponent<ARSession>();

            // XR Origin
            var originGo = new GameObject("XR Origin");
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(originGo.transform, false);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(offset.transform, false);
            var cam = camGo.AddComponent<Camera>();
            // AURA は光学シースルー：透明（黒）で塗ると現実がそのまま見える
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1000f;
            camGo.AddComponent<AudioListener>();
            var tpd = camGo.AddComponent<TrackedPoseDriver>();
            tpd.positionInput = new InputActionProperty(MakeAction("Position", "<XRHMD>/centerEyePosition"));
            tpd.rotationInput = new InputActionProperty(MakeAction("Rotation", "<XRHMD>/centerEyeRotation"));
            tpd.trackingStateInput = new InputActionProperty(MakeAction("Tracking State", "<XRHMD>/trackingState"));

            var origin = originGo.AddComponent<XROrigin>();
            origin.Camera = cam;
            origin.CameraFloorOffsetObject = offset;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            origin.CameraYOffset = 0f;

            // Geospatial（サンプルと同じく XR Origin に付ける）
            var permissions = originGo.AddComponent<AndroidXRPermissionUtil>();
            permissions.GenernalAndroidPermissions = new List<string> { "android.permission.ACCESS_FINE_LOCATION" };
            // Fine はメッシュ・レイキャストに必要（Coarse を含む）。ハンドトラッキングは編集操作に使う
            permissions.AndroidXRPermissions = new List<AndroidXRPermission>
            {
                AndroidXRPermission.SceneUnderstandingFine,
                AndroidXRPermission.HandTracking,
            };
            permissions.PermissionRationale = "町人を現実の場所に立たせるため、位置情報と周囲の認識を使います。";
            var geo = originGo.AddComponent<XRGeospatialManager>();
            geo.enabled = false;
            originGo.AddComponent<ARShareableAnchorManager>();

            // 面：平面検出・レイキャスト・環境メッシュ（ARMeshManager は XR Origin の子に置く決まり）
            var planes = originGo.AddComponent<ARPlaneManager>();
            planes.requestedDetectionMode = UnityEngine.XR.ARSubsystems.PlaneDetectionMode.Horizontal;
            var raycasts = originGo.AddComponent<ARRaycastManager>();
            var meshingGo = new GameObject("Meshing");
            meshingGo.transform.SetParent(originGo.transform, false);
            var meshing = meshingGo.AddComponent<ARMeshManager>();
            meshing.meshPrefab = GetOrCreateMeshPrefab();
            meshing.normals = true;
            var surface = originGo.AddComponent<AuraSurface>();
            surface.raycastManager = raycasts;
            surface.planeManager = planes;
            surface.viewerCamera = cam;

            var world = originGo.AddComponent<AndroidXrWorld>();
            world.geospatialManager = geo;
            world.viewerCamera = cam;
            world.surface = surface;

            var spawnerGo = new GameObject("ChouninSpawner");
            var spawner = spawnerGo.AddComponent<ChouninSpawner>();
            spawner.world = world;
            // 送り操作（ピンチ・トラックパッド）は実機で調べてから。それまでは自動で送る
            spawner.autoAdvanceSeconds = 4f;
            // 光学シースルー向け：暗い板は透けるので、濃い色の板＋明るい文字にする
            spawner.bubbleStyle = new SpeechBubble.Style
            {
                panelColor = new Color(0.10f, 0.12f, 0.30f, 0.85f),
                textColor = Color.white,
            };

            var editor = new GameObject("PlacementEditor").AddComponent<PlacementEditor>();
            editor.spawner = spawner;
            var hands = new GameObject("HandInput").AddComponent<AuraHandInput>();
            hands.editor = editor;
            hands.spawner = spawner;
            hands.viewerCamera = cam;

            var hud = new GameObject("StatusHud").AddComponent<AuraStatusHud>();
            hud.editor = editor;
            hud.world = world;
            hud.viewerCamera = cam;

            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // 環境メッシュ用のプレハブ：当たり判定だけ（光学シースルーなので描画しない）
        private const string MeshPrefabPath = "Assets/ChouninXR/Platform/AndroidXR/Prefabs/EnvironmentMesh.prefab";

        private static MeshFilter GetOrCreateMeshPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(MeshPrefabPath);
            if (existing != null)
            {
                return existing.GetComponent<MeshFilter>();
            }
            Directory.CreateDirectory(Path.GetDirectoryName(MeshPrefabPath));
            var go = new GameObject("EnvironmentMesh");
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshCollider>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, MeshPrefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab.GetComponent<MeshFilter>();
        }

        private static InputAction MakeAction(string name, string binding)
        {
            var a = new InputAction(name, InputActionType.Value, binding);
            return a;
        }

        // バッチ: -buildTarget Android -executeMethod ChouninXR.AndroidXR.EditorTools.AndroidXrSetup.BuildApk
        [MenuItem("ChouninXR/AURA/APK ビルド")]
        public static void BuildApk()
        {
            Directory.CreateDirectory(BuildDir);
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(BuildDir, "ChouninXR-Aura.apk"),
                target = BuildTarget.Android,
                options = BuildOptions.Development,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[ChouninXR] ビルド結果: {report.summary.result} " +
                      $"{report.summary.totalSize / (1024 * 1024)}MB {report.summary.totalTime}");
            if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
