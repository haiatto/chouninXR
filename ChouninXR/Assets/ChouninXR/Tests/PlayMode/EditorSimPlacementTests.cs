using System.Collections;
using System.IO;
using System.Linq;
using ChouninXR.EditorSim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChouninXR.Tests
{
    // 現地での直接配置（EditorSim）：高さはメッシュ（コライダー）に当て、無ければ地面相当。
    public class EditorSimPlacementTests
    {
        private string _village;

        [SetUp]
        public void SetUp()
        {
            // リポジトリの見本を汚さないよう、一時フォルダにコピーして使う
            var src = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "villages", "sample"));
            _village = Path.Combine(Path.GetTempPath(), "chouninxr-test-village-" + System.Guid.NewGuid().ToString("N"));
            CopyDir(src, _village);
            ChouninSpawner.VillagePathOverride = _village;
        }

        [TearDown]
        public void TearDown()
        {
            ChouninSpawner.VillagePathOverride = null;
            if (Directory.Exists(_village))
            {
                Directory.Delete(_village, true);
            }
        }

        [UnityTest]
        public IEnumerator DirectPlacement_UsesMeshHeight_FallsBackToGround_AndSaves()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/ChouninXR/Scenes/EditorSim.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("エディタ専用");
            yield break;
#endif
            var spawner = Object.FindAnyObjectByType<ChouninSpawner>();
            var world = Object.FindAnyObjectByType<SimWorld>();
            var editor = Object.FindAnyObjectByType<PlacementEditor>();
            Assert.NotNull(editor, "PlacementEditor がシーンに無い");
            // マウス操作がテストの光線を上書きしないように止める
            world.viewer.GetComponent<SimPlayer>().enabled = false;

            float t = 0f;
            while (spawner.Actors.Count < 2 && t < 10f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(2, spawner.Actors.Count);

            editor.SetEditing(true);

            // 1) 台（高さ 0.5m のコライダー）を真上から指す → メッシュ扱いで y=0.5
            var stageTop = new Vector3(-2.5f, 0.5f, 3f);
            editor.SetPointer(new Ray(stageTop + Vector3.up * 5f, Vector3.down));
            yield return null;
            Assert.IsTrue(editor.HasHit);
            Assert.AreEqual(SurfaceKind.Mesh, editor.Hit.kind);
            Assert.AreEqual(0.5f, editor.Hit.point.y, 0.01f);

            // 2) 新しい町人を追加 → 持った状態 → 台の上に置いて保存
            editor.AddNew();
            t = 0f;
            while (editor.Held == null && t < 5f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Assert.NotNull(editor.Held, "追加した町人を持てない");
            var added = editor.Held;
            editor.Rotate(30f);
            yield return null;
            editor.Primary();
            Assert.IsNull(editor.Held, "置けていない");

            var json = Path.Combine(added.Data.baseDirectory, "chounin.json");
            Assert.IsTrue(File.Exists(json), "保存されていない");
            var saved = JsonUtility.FromJson<ChouninData>(File.ReadAllText(json));
            Assert.AreEqual(AltitudeModes.Surface, saved.placement.altitudeMode);
            Assert.AreEqual(GeoSources.EditorSim, saved.placement.placedWith.source);
            Assert.AreEqual("mesh", saved.placement.placedWith.surface);
            // 高度は WGS84 楕円体高（EditorSim は原点 40m + 台 0.5m）
            Assert.AreEqual(world.originAltitude + 0.5, saved.placement.altitude, 0.02);
            var en = GeoMath.ToLocalEastNorth(world.originLat, world.originLng, saved.placement.lat, saved.placement.lng);
            Assert.AreEqual(-2.5f, en.x, 0.05f);
            Assert.AreEqual(3f, en.y, 0.05f);

            // 置き直したあとも、高さは台の上（面に吸着）
            yield return new WaitForSeconds(0.7f);
            Assert.AreEqual(0.5f, added.transform.position.y, 0.02f, "台の高さに吸着していない");

            // 3) 何も無いところ（地面の外）→ 地面相当（y=0）
            editor.SetPointer(new Ray(new Vector3(3000f, 5f, 0f), Vector3.down));
            yield return null;
            Assert.IsTrue(editor.HasHit);
            Assert.AreEqual(SurfaceKind.GroundEstimate, editor.Hit.kind);
            Assert.AreEqual(0f, editor.Hit.point.y, 0.01f);

            // 4) 既存の町人を指して持ち上げ、地面へ動かして保存
            var panda = spawner.Actors.First(a => a.Data.id == "eki-panda");
            var eye = world.viewer.position;
            var target = panda.transform.position + Vector3.up * 1f;
            editor.SetPointer(new Ray(eye, (target - eye).normalized));
            yield return null;
            Assert.AreEqual(panda, editor.Pointed, "町人を指せない");
            editor.Primary();
            Assert.AreEqual(panda, editor.Held);

            editor.SetPointer(new Ray(new Vector3(2f, 5f, 4f), Vector3.down));
            yield return null;
            yield return new WaitForSeconds(0.2f);
            Capture(world.viewer.GetComponent<Camera>(), "editorsim_placement.png");
            editor.Primary();
            var pandaSaved = JsonUtility.FromJson<ChouninData>(
                File.ReadAllText(Path.Combine(panda.Data.baseDirectory, "chounin.json")));
            Assert.AreEqual(AltitudeModes.Surface, pandaSaved.placement.altitudeMode);
            Assert.AreEqual(new[] { "パンダにあいたいかー！！！", "この先にいるぞ！", "えいえいおー" },
                pandaSaved.talk.lines, "セリフが保存で失われた");

            // 5) 追加した町人を消す → フォルダは .trash へ
            yield return new WaitForSeconds(0.2f);
            var addedTarget = added.transform.position + Vector3.up * 1f;
            editor.SetPointer(new Ray(eye, (addedTarget - eye).normalized));
            yield return null;
            Assert.AreEqual(added, editor.Pointed, "追加した町人を指せない");
            var addedDir = added.Data.baseDirectory;
            editor.Delete();
            yield return null;
            Assert.IsFalse(Directory.Exists(addedDir));
            Assert.IsTrue(Directory.GetDirectories(Path.Combine(_village, ".trash")).Length == 1);
            Assert.AreEqual(2, spawner.Actors.Count(a => a != null));
        }

        private static void CopyDir(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src))
            {
                File.Copy(f, Path.Combine(dst, Path.GetFileName(f)));
            }
            foreach (var d in Directory.GetDirectories(src))
            {
                CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
            }
        }

        private static void Capture(Camera cam, string fileName)
        {
            if (cam == null)
            {
                return;
            }
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, fileName), tex.EncodeToPNG());
            Object.Destroy(rt);
            Object.Destroy(tex);
        }
    }
}
