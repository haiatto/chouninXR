using UnityEngine;

namespace ChouninXR
{
    // 面の当たり判定の共通部分。
    //   1. 物理コライダー（環境メッシュ。EditorSim では地面や箱）
    //   2. 平面（プラットフォームが TryRaycastPlane を実装したとき）
    //   3. どちらも無ければ「地面相当」の水平面（GroundHeight）
    // 1 と 2 は近いほうを採る。町人自身のコライダーは無視する。
    public abstract class SurfaceRaycasterBase : MonoBehaviour, IChouninSurface
    {
        [Tooltip("環境メッシュとして扱うレイヤー")]
        public LayerMask meshLayers = ~0;

        private readonly RaycastHit[] _hits = new RaycastHit[16];

        // 地面相当の高さ（Unity ワールドの y）。near は調べたい場所の近く。
        public abstract float GroundHeight(Vector3 near);

        protected virtual bool TryRaycastPlane(Ray ray, float maxDistance, out SurfaceHit hit)
        {
            hit = default;
            return false;
        }

        public bool Raycast(Ray ray, float maxDistance, out SurfaceHit hit)
        {
            bool hasMesh = TryRaycastMesh(ray, maxDistance, out var meshHit);
            bool hasPlane = TryRaycastPlane(ray, maxDistance, out var planeHit);
            if (hasMesh && hasPlane)
            {
                float dm = Vector3.Distance(ray.origin, meshHit.point);
                float dp = Vector3.Distance(ray.origin, planeHit.point);
                hit = dp + 0.05f < dm ? planeHit : meshHit;
                return true;
            }
            if (hasMesh)
            {
                hit = meshHit;
                return true;
            }
            if (hasPlane)
            {
                hit = planeHit;
                return true;
            }
            return TryRaycastGround(ray, maxDistance, out hit);
        }

        private bool TryRaycastMesh(Ray ray, float maxDistance, out SurfaceHit hit)
        {
            hit = default;
            int n = Physics.RaycastNonAlloc(ray, _hits, maxDistance, meshLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (h.distance >= best || h.collider.GetComponentInParent<ChouninActor>() != null)
                {
                    continue;
                }
                best = h.distance;
                hit = new SurfaceHit { point = h.point, normal = h.normal, kind = SurfaceKind.Mesh };
            }
            return best < float.MaxValue;
        }

        private bool TryRaycastGround(Ray ray, float maxDistance, out SurfaceHit hit)
        {
            hit = default;
            float y = GroundHeight(ray.origin);
            if (Mathf.Abs(ray.direction.y) < 1e-4f)
            {
                return false;
            }
            float t = (y - ray.origin.y) / ray.direction.y;
            if (t < 0f || t > maxDistance)
            {
                return false;
            }
            hit = new SurfaceHit
            {
                point = ray.origin + ray.direction * t,
                normal = Vector3.up,
                kind = SurfaceKind.GroundEstimate,
            };
            return true;
        }
    }
}
