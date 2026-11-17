using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ChouninXR
{
    // 村を読み込み、位置合わせが済んだら町人を出す。
    // どの世界（EditorSim / AndroidXR / Phone）に置くかは world に渡すコンポーネントで決まる。
    public class ChouninSpawner : MonoBehaviour
    {
        [Tooltip("IChouninWorld を実装したコンポーネント")]
        public MonoBehaviour world;

        [Tooltip("村フォルダ。相対パスはエディタではリポジトリ直下、実機では persistentDataPath から")]
        public string villagePath = "villages/sample";

        [Tooltip("吹き出しの自動送り秒数（0 で手動送りのみ）")]
        public float autoAdvanceSeconds = 0f;

        public SpeechBubble.Style bubbleStyle = new SpeechBubble.Style();

        public Village Village { get; private set; }
        public IReadOnlyList<ChouninActor> Actors => _actors;

        private readonly List<ChouninActor> _actors = new List<ChouninActor>();
        private IChouninWorld _world;
        private bool _spawned;

        public static string ResolveVillagePath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return path;
            }
#if UNITY_EDITOR
            // Assets の 2 つ上 = リポジトリ直下
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", path));
#else
            return Path.Combine(Application.persistentDataPath, path);
#endif
        }

        private System.Collections.IEnumerator Start()
        {
            _world = world as IChouninWorld;
            if (_world == null)
            {
                Debug.LogError("[Chounin] world に IChouninWorld を実装したコンポーネントを指定してください");
                enabled = false;
                yield break;
            }

            var dir = ResolveVillagePath(villagePath);
            yield return VillageInstaller.EnsureInstalled(villagePath, dir);
            Village = Village.LoadFromDirectory(dir);
            Debug.Log($"[Chounin] 村「{Village.data.name}」を読み込みました（{Village.chounins.Count}人） {dir}");
        }

        private void Update()
        {
            if (!_spawned && _world != null && _world.IsReady && Village != null)
            {
                _spawned = true;
                foreach (var c in Village.chounins)
                {
                    Spawn(c);
                }
            }
        }

        private void Spawn(ChouninData data)
        {
            _world.RequestPlacement(data, async anchor =>
            {
                if (anchor == null)
                {
                    Debug.LogWarning($"[Chounin] {data.id} を置けませんでした");
                    return;
                }

                var root = new GameObject();
                root.transform.SetParent(anchor, false);
                var actor = root.AddComponent<ChouninActor>();

                var body = await ChouninModelLoader.LoadAsync(data);
                if (root == null)
                {
                    // 読み込み中にアンカーごと消えた
                    Destroy(body);
                    return;
                }
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = Vector3.zero;
                body.transform.localRotation = Quaternion.identity;

                actor.Initialize(data, _world, body.transform);
                actor.autoAdvanceSeconds = autoAdvanceSeconds;
                SpeechBubble.Attach(actor, ChouninModelLoader.EstimateHeadHeight(body), bubbleStyle);
                _actors.Add(actor);
            });
        }

        // いちばん近くで会話中の町人（送り操作の対象）
        public ChouninActor CurrentTalker()
        {
            ChouninActor best = null;
            float bestD = float.MaxValue;
            foreach (var a in _actors)
            {
                if (a == null || !a.IsTalking)
                {
                    continue;
                }
                float d = a.DistanceToViewer();
                if (d < bestD)
                {
                    bestD = d;
                    best = a;
                }
            }
            return best;
        }
    }
}
