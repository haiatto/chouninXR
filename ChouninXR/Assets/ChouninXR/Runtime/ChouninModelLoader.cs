using System;
using System.IO;
using System.Threading.Tasks;
using UniVRM10;
using UnityEngine;

namespace ChouninXR
{
    // VRM（1.0 / 0.x）を実行時に読み込む。無いときは仮の体（カプセル）を返す。
    public static class ChouninModelLoader
    {
        public static async Task<GameObject> LoadAsync(ChouninData chounin)
        {
            var path = Village.ResolveVrmPath(chounin);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    // canLoadVrm0X: 旧アプリ時代の VRM 0.x もそのまま読めるようにする
                    var instance = await Vrm10.LoadPathAsync(path, canLoadVrm0X: true, showMeshes: true);
                    if (instance != null)
                    {
                        var go = instance.gameObject;
                        float s = chounin.model?.scale > 0f ? chounin.model.scale : 1f;
                        go.transform.localScale = Vector3.one * s;
                        return go;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Chounin] VRM を読めませんでした ({path}): {e.Message}");
                }
            }
            else if (!string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"[Chounin] VRM が見つかりません: {path}（仮の体で表示します）");
            }

            return CreatePlaceholder();
        }

        private static GameObject CreatePlaceholder()
        {
            var root = new GameObject("PlaceholderBody");
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Capsule";
            capsule.transform.SetParent(root.transform, false);
            capsule.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            capsule.transform.localScale = new Vector3(0.45f, 0.8f, 0.45f);
            UnityEngine.Object.Destroy(capsule.GetComponent<Collider>());

            // 向きが分かるように顔の位置に目印
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Front";
            nose.transform.SetParent(root.transform, false);
            nose.transform.localPosition = new Vector3(0f, 1.4f, 0.22f);
            nose.transform.localScale = new Vector3(0.12f, 0.06f, 0.06f);
            UnityEngine.Object.Destroy(nose.GetComponent<Collider>());
            return root;
        }

        // 吹き出しを出す高さ（頭の少し上）
        public static float EstimateHeadHeight(GameObject body)
        {
            var animator = body.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null)
                {
                    return body.transform.InverseTransformPoint(head.position).y * body.transform.localScale.y + 0.35f;
                }
            }

            var renderers = body.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers)
                {
                    b.Encapsulate(r.bounds);
                }
                return b.max.y - body.transform.position.y + 0.15f;
            }
            return 1.9f;
        }
    }
}
