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

        // テストなどで村フォルダを差し替えるとき用（null なら villagePath）
        public static string VillagePathOverride;

        public Village Village { get; private set; }
        public IReadOnlyList<ChouninActor> Actors => _actors;

        private readonly List<ChouninActor> _actors = new List<ChouninActor>();
        private readonly Dictionary<ChouninActor, Transform> _placed = new Dictionary<ChouninActor, Transform>();
        private IChouninWorld _world;
        public IChouninWorld World => _world;
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

            var dir = ResolveVillagePath(VillagePathOverride ?? villagePath);
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

        // 町人を出す。onReady は見た目まで読み込めたら呼ばれる（置けなければ呼ばれない）
        public void Spawn(ChouninData data, System.Action<ChouninActor> onReady = null)
        {
            _world.RequestPlacement(data, async anchor =>
            {
                if (anchor == null)
                {
                    Debug.LogWarning($"[Chounin] {data.id} を置けませんでした");
                    return;
                }

                var root = new GameObject();
                root.transform.SetParent(MakeContainer(anchor, data), false);
                var actor = root.AddComponent<ChouninActor>();
                _placed[actor] = anchor;

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

                float head = ChouninModelLoader.EstimateHeadHeight(body);
                actor.Initialize(data, _world, body.transform);
                actor.autoAdvanceSeconds = autoAdvanceSeconds;
                SpeechBubble.Attach(actor, head, bubbleStyle);

                // 編集で指して選べるように当たり判定を付ける（面の判定からは除外される）
                var col = root.AddComponent<CapsuleCollider>();
                col.height = head + 0.2f;
                col.radius = 0.3f;
                col.center = new Vector3(0f, col.height * 0.5f, 0f);

                _actors.Add(actor);
                onReady?.Invoke(actor);
            });
        }

        // surface の町人は、アンカーの下に高さ合わせ用の入れ物をはさむ
        private Transform MakeContainer(Transform anchor, ChouninData data)
        {
            if (data.placement.altitudeMode != AltitudeModes.Surface)
            {
                return anchor;
            }
            var go = new GameObject("Surface");
            go.transform.SetParent(anchor, false);
            var snapper = go.AddComponent<SurfaceSnapper>();
            snapper.world = _world;
            snapper.surface = (_world as IChouninPlacementWorld)?.Surface;
            snapper.heightAboveSurface = data.placement.heightAboveSurface;
            return go.transform;
        }

        // 置き場所（data.placement）を変えたあとに呼ぶ。アンカーを作り直して付け替える。
        public void Relocate(ChouninActor actor)
        {
            if (actor == null)
            {
                return;
            }
            var root = actor.transform;
            root.SetParent(null, true);
            if (_placed.TryGetValue(actor, out var old))
            {
                _world.ReleasePlacement(old);
            }
            _placed.Remove(actor);

            _world.RequestPlacement(actor.Data, anchor =>
            {
                if (root == null)
                {
                    return;
                }
                if (anchor == null)
                {
                    // アンカーが作れなくても、見た目はその場に残す（次回起動時に置き直される）
                    Debug.LogWarning($"[Chounin] {actor.Data.id} のアンカーを作り直せませんでした");
                    return;
                }
                _placed[actor] = anchor;
                root.SetParent(MakeContainer(anchor, actor.Data), false);
                root.localPosition = Vector3.zero;
                root.localRotation = Quaternion.identity;
            });
        }

        public void Despawn(ChouninActor actor)
        {
            if (actor == null)
            {
                return;
            }
            _actors.Remove(actor);
            if (_placed.TryGetValue(actor, out var anchor))
            {
                _placed.Remove(actor);
                actor.transform.SetParent(null, true);
                _world.ReleasePlacement(anchor);
            }
            Destroy(actor.gameObject);
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
