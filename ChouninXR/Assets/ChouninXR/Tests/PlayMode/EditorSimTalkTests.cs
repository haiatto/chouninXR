using System.Collections;
using System.IO;
using System.Linq;
using ChouninXR.EditorSim;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChouninXR.Tests
{
    public class EditorSimTalkTests
    {
        [UnityTest]
        public IEnumerator Chounin_SpawnsAndTalks_WhenViewerApproaches()
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
            Assert.NotNull(spawner);
            Assert.NotNull(world);

            // 町人の出現を待つ
            float t = 0f;
            while (spawner.Actors.Count == 0 && t < 10f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Assert.Greater(spawner.Actors.Count, 0, "町人が出現しない");

            var actor = spawner.Actors.First(a => a.Data.id == "eki-panda");
            Assert.IsFalse(actor.IsTalking, "離れているのに話している");

            // 町人の正面 2m に立つ
            var viewer = world.viewer;
            var front = actor.transform.position + actor.Body.forward * 2f;
            viewer.position = new Vector3(front.x, 1.6f, front.z);
            viewer.LookAt(actor.transform.position + Vector3.up * 1.5f);
            yield return null;
            yield return null;

            Assert.IsTrue(actor.IsTalking, "近づいても話し始めない");
            var text = actor.GetComponentInChildren<TextMeshPro>(true);
            Assert.AreEqual("パンダにあいたいかー！！！", text.text);

            yield return new WaitForSeconds(0.5f);
            Capture(viewer.GetComponent<Camera>(), "editorsim_talk.png");

            actor.Advance();
            Assert.AreEqual("この先にいるぞ！", text.text);
            actor.Advance();
            actor.Advance();
            Assert.IsFalse(actor.IsTalking, "最後まで送っても終わらない");

            // 話し終えた直後は、離れるまで話しかけ直さない
            yield return null;
            Assert.IsFalse(actor.IsTalking);

            // 離れてから戻ると、また話す
            viewer.position = front + actor.Body.forward * 10f;
            yield return null;
            viewer.position = front;
            yield return null;
            Assert.IsTrue(actor.IsTalking, "戻っても話し直さない");
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
            var dir = Path.Combine(Application.dataPath, "..", "Logs");
            File.WriteAllBytes(Path.Combine(dir, fileName), tex.EncodeToPNG());
            Object.Destroy(rt);
            Object.Destroy(tex);
        }
    }
}
