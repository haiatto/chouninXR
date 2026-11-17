using UnityEngine;

namespace ChouninXR
{
    // altitudeMode = surface の町人を、真下に見つかった面の高さへ合わせる。
    // 水平位置はアンカーのまま、高さだけ面から決める（地図・Geospatial の高度は当てにしない）。
    // メッシュや平面は後から見つかることが多いので、ときどき測り直す。
    public class SurfaceSnapper : MonoBehaviour
    {
        public IChouninSurface surface;
        public IChouninWorld world;
        public float heightAboveSurface;
        public float interval = 0.5f;

        public SurfaceKind CurrentKind { get; private set; }

        private float _timer;

        private void Start()
        {
            Snap();
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer >= interval)
            {
                _timer = 0f;
                Snap();
            }
        }

        public void Snap()
        {
            if (surface == null || transform.parent == null)
            {
                return;
            }
            var anchor = transform.parent.position;
            float top = anchor.y;
            if (world != null)
            {
                top = Mathf.Max(top, world.ViewerPosition.y);
            }
            // 頭より少し上から真下へ
            var ray = new Ray(new Vector3(anchor.x, top + 2f, anchor.z), Vector3.down);
            if (!surface.Raycast(ray, 30f, out var hit))
            {
                return;
            }
            // 一度メッシュや平面が見つかったら、推定値には戻さない
            if (hit.kind < CurrentKind && hit.kind == SurfaceKind.GroundEstimate)
            {
                return;
            }
            CurrentKind = hit.kind;
            transform.position = new Vector3(anchor.x, hit.point.y + heightAboveSurface, anchor.z);
        }
    }
}
