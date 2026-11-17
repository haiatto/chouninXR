using System;
using System.IO;
using UnityEngine;

namespace ChouninXR
{
    // 現地で町人を直接置く・動かす編集機能（入力に依存しない部分）。
    // 入力はプラットフォームごとのスクリプトが SetPointer と各操作を呼ぶ：
    //   EditorSim：マウスの光線とクリック／キー
    //   AURA：手の光線とピンチ
    // 高さは「その場の面」で決める（altitudeMode = surface）。地図の標高や Geospatial の高度は当てにしない。
    public class PlacementEditor : MonoBehaviour
    {
        public ChouninSpawner spawner;
        public float maxDistance = 30f;
        public float reticleSize = 0.35f;

        public bool IsEditing { get; private set; }
        public ChouninActor Held { get; private set; }
        public ChouninActor Pointed { get; private set; }
        public bool HasHit { get; private set; }
        public SurfaceHit Hit { get; private set; }
        public string Message { get; private set; } = "";

        public event Action<ChouninData> Saved;

        private Ray _pointer;
        private bool _pointerValid;
        private float _yawOffset;
        private Transform _heldOriginalParent;
        private Vector3 _heldOriginalLocalPos;
        private Quaternion _heldOriginalLocalRot;
        private Transform _reticle;
        private Material _reticleMat;

        private IChouninPlacementWorld World => spawner != null ? spawner.World as IChouninPlacementWorld : null;

        public string StatusText
        {
            get
            {
                if (!IsEditing)
                {
                    return "";
                }
                string kind = !HasHit ? "面なし" : Hit.kind switch
                {
                    SurfaceKind.Mesh => "メッシュ",
                    SurfaceKind.Plane => "平面",
                    SurfaceKind.GroundEstimate => "地面（推定）",
                    _ => "?",
                };
                string target = Held != null ? $"移動中：{Held.Data.name}"
                    : Pointed != null ? $"指している：{Pointed.Data.name}"
                    : "";
                return $"【編集】高さの基準：{kind}  {target}\n{Message}";
            }
        }

        public void SetEditing(bool editing)
        {
            if (IsEditing == editing)
            {
                return;
            }
            if (!editing && Held != null)
            {
                Cancel();
            }
            IsEditing = editing;
            Message = editing ? "編集を始めました" : "";
            if (_reticle != null)
            {
                _reticle.gameObject.SetActive(false);
            }
        }

        public void SetPointer(Ray ray, bool valid = true)
        {
            _pointer = ray;
            _pointerValid = valid;
        }

        private void Update()
        {
            if (!IsEditing)
            {
                return;
            }

            Pointed = null;
            HasHit = false;
            var world = World;
            if (_pointerValid && world != null)
            {
                if (Held == null && Physics.Raycast(_pointer, out var ah, maxDistance, ~0, QueryTriggerInteraction.Ignore))
                {
                    Pointed = ah.collider.GetComponentInParent<ChouninActor>();
                }
                if (world.Surface != null && world.Surface.Raycast(_pointer, maxDistance, out var hit))
                {
                    HasHit = true;
                    Hit = hit;
                }
            }

            if (Held != null && HasHit)
            {
                var t = Held.transform;
                t.position = Hit.point + Vector3.up * Held.Data.placement.heightAboveSurface;
                t.rotation = FacingViewer(t.position) * Quaternion.Euler(0f, _yawOffset, 0f);
            }

            UpdateReticle();
        }

        private Quaternion FacingViewer(Vector3 pos)
        {
            var to = spawner.World.ViewerPosition - pos;
            to.y = 0f;
            return to.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(to, Vector3.up) : Quaternion.identity;
        }

        // 決定（クリック／ピンチ）：持っていれば置く、町人を指していれば持ち上げる
        public void Primary()
        {
            if (!IsEditing)
            {
                return;
            }
            if (Held != null)
            {
                Drop();
            }
            else if (Pointed != null)
            {
                PickUp(Pointed);
            }
        }

        public void PickUp(ChouninActor actor)
        {
            if (Held != null || actor == null)
            {
                return;
            }
            actor.EndTalk(false);
            actor.enabled = false;
            Held = actor;
            _yawOffset = 0f;
            var t = actor.transform;
            _heldOriginalParent = t.parent;
            _heldOriginalLocalPos = t.localPosition;
            _heldOriginalLocalRot = t.localRotation;
            t.SetParent(null, true);
            if (actor.Body != null)
            {
                actor.Body.localRotation = Quaternion.identity;
            }
            Message = $"{actor.Data.name} を持ち上げました。置きたい場所を指して決定";
        }

        // 持っている町人をいまの場所に置いて保存する
        public void Drop()
        {
            if (Held == null)
            {
                return;
            }
            if (!HasHit)
            {
                Message = "置ける面を指してください";
                return;
            }
            var world = World;
            var t = Held.transform;
            if (world == null || !world.TryGetGeoPose(t.position, t.rotation, out var geo))
            {
                Message = "位置合わせがまだなので置けません";
                return;
            }

            var data = Held.Data;
            ApplyGeoPose(data.placement, geo, Hit.kind);
            double lat = geo.lat, lng = geo.lng;
            try
            {
                Village.Save(data);
            }
            catch (Exception e)
            {
                Message = "保存に失敗しました：" + e.Message;
                Debug.LogException(e);
                return;
            }

            var actor = Held;
            Held = null;
            actor.enabled = true;
            spawner.Relocate(actor);
            Message = $"{data.name} を置いて保存しました（{lat:F6}, {lng:F6}）";
            Saved?.Invoke(data);
        }

        // 現地で置いた位置を書き込む（高さは面で決める surface。altitude は目安として残す）
        private static void ApplyGeoPose(ChouninPlacement p, GeoPose geo, SurfaceKind kind)
        {
            p.lat = Math.Round(geo.lat, 8);   // 約 1mm
            p.lng = Math.Round(geo.lng, 8);
            p.altitudeMode = AltitudeModes.Surface;
            p.altitude = Math.Round(geo.altitude, 2);
            p.heading = Mathf.Round(geo.heading * 10f) / 10f;
            p.placedWith = new ChouninPlacementRecord
            {
                source = geo.source,
                horizontalAccuracy = Math.Round(geo.horizontalAccuracy, 2),
                verticalAccuracy = Math.Round(geo.verticalAccuracy, 2),
                headingAccuracy = Math.Round(geo.headingAccuracy, 1),
                placedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                surface = kind switch
                {
                    SurfaceKind.Mesh => "mesh",
                    SurfaceKind.Plane => "plane",
                    SurfaceKind.GroundEstimate => "ground-estimate",
                    _ => "none",
                },
            };
        }

        // 持ち上げをやめて元の場所に戻す
        public void Cancel()
        {
            if (Held == null)
            {
                return;
            }
            var t = Held.transform;
            if (_heldOriginalParent != null)
            {
                t.SetParent(_heldOriginalParent, false);
                t.localPosition = _heldOriginalLocalPos;
                t.localRotation = _heldOriginalLocalRot;
            }
            Held.enabled = true;
            Held = null;
            Message = "元に戻しました";
        }

        public void Rotate(float degrees)
        {
            if (Held != null)
            {
                _yawOffset = Mathf.Repeat(_yawOffset + degrees, 360f);
            }
        }

        // 新しい町人を作って、すぐ持った状態にする
        public void AddNew()
        {
            if (!IsEditing || Held != null)
            {
                return;
            }
            var world = World;
            var village = spawner != null ? spawner.Village : null;
            if (village == null || world == null || !HasHit)
            {
                Message = "置ける面を指してから追加してください";
                return;
            }
            if (!world.TryGetGeoPose(Hit.point, FacingViewer(Hit.point), out var geo))
            {
                Message = "位置合わせがまだなので追加できません";
                return;
            }

            string id = "c-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var data = new ChouninData
            {
                id = id,
                name = "新しい町人",
                baseDirectory = Path.Combine(village.rootDirectory, "chounin", id),
                placement = new ChouninPlacement(),
                talk = new ChouninTalk { lines = new[] { "こんにちは！" } },
            };
            ApplyGeoPose(data.placement, geo, Hit.kind);
            village.chounins.Add(data);
            Message = "新しい町人を作っています…";
            spawner.Spawn(data, actor =>
            {
                if (IsEditing && Held == null)
                {
                    PickUp(actor);
                    Message = "新しい町人です。置きたい場所を指して決定";
                }
            });
        }

        // 持っている（無ければ指している）町人を消す。フォルダは .trash に移すだけ
        public void Delete()
        {
            var target = Held != null ? Held : Pointed;
            if (!IsEditing || target == null)
            {
                return;
            }
            var data = target.Data;
            if (Held == target)
            {
                Held = null;
            }
            try
            {
                var village = spawner.Village;
                if (!string.IsNullOrEmpty(data.baseDirectory) && Directory.Exists(data.baseDirectory) && village != null)
                {
                    var trash = Path.Combine(village.rootDirectory, ".trash");
                    Directory.CreateDirectory(trash);
                    Directory.Move(data.baseDirectory,
                        Path.Combine(trash, $"{Path.GetFileName(data.baseDirectory)}-{DateTime.Now:yyyyMMddHHmmss}"));
                }
                village?.chounins.Remove(data);
            }
            catch (Exception e)
            {
                Message = "削除に失敗しました：" + e.Message;
                Debug.LogException(e);
                return;
            }
            spawner.Despawn(target);
            Message = $"{data.name} を消しました（.trash に移動）";
        }

        private void UpdateReticle()
        {
            if (_reticle == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "PlacementReticle";
                Destroy(go.GetComponent<Collider>());
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                _reticleMat = new Material(shader);
                go.GetComponent<MeshRenderer>().sharedMaterial = _reticleMat;
                _reticle = go.transform;
                _reticle.SetParent(transform, false);
            }
            bool show = IsEditing && HasHit;
            _reticle.gameObject.SetActive(show);
            if (!show)
            {
                return;
            }
            _reticle.position = Hit.point + Hit.normal * 0.01f;
            _reticle.rotation = Quaternion.FromToRotation(Vector3.up, Hit.normal);
            _reticle.localScale = new Vector3(reticleSize, 0.003f, reticleSize);
            _reticleMat.color = Hit.kind switch
            {
                SurfaceKind.Mesh => new Color(0.3f, 1f, 0.4f),
                SurfaceKind.Plane => new Color(0.3f, 0.8f, 1f),
                _ => new Color(1f, 0.85f, 0.2f),
            };
        }

        private void OnDestroy()
        {
            if (_reticleMat != null)
            {
                Destroy(_reticleMat);
            }
        }
    }
}
