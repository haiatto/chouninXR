using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace ChouninXR.AndroidXR
{
    // AURA の面：
    //   メッシュ … ARMeshManager が作るメッシュコライダー（Android XR の Scene Meshing）
    //   平面   … ARRaycastManager で検出済みの平面に当てる
    //   地面相当 … いちばん低い上向き平面、無ければ「頭の高さ − 目の高さ」
    // AURA がメッシュや平面をどこまで出せるかは実機で確認する。出なくても地面相当で置ける。
    public class AuraSurface : SurfaceRaycasterBase
    {
        public ARRaycastManager raycastManager;
        public ARPlaneManager planeManager;
        public Camera viewerCamera;
        [Tooltip("平面が無いときの、地面から目までの高さ[m]")]
        public float eyeHeight = 1.55f;

        private readonly List<ARRaycastHit> _arHits = new List<ARRaycastHit>();

        private Camera Cam => viewerCamera != null ? viewerCamera : Camera.main;

        protected override bool TryRaycastPlane(Ray ray, float maxDistance, out SurfaceHit hit)
        {
            hit = default;
            if (raycastManager == null || !raycastManager.enabled)
            {
                return false;
            }
            if (!raycastManager.Raycast(ray, _arHits, TrackableType.PlaneWithinPolygon) || _arHits.Count == 0)
            {
                return false;
            }
            var h = _arHits[0]; // 近い順
            if (h.distance > maxDistance)
            {
                return false;
            }
            hit = new SurfaceHit { point = h.pose.position, normal = h.pose.up, kind = SurfaceKind.Plane };
            return true;
        }

        public override float GroundHeight(Vector3 near)
        {
            var cam = Cam;
            float head = cam != null ? cam.transform.position.y : near.y;
            float best = float.MaxValue;
            if (planeManager != null)
            {
                foreach (var plane in planeManager.trackables)
                {
                    if (plane.alignment != PlaneAlignment.HorizontalUp)
                    {
                        continue;
                    }
                    float y = plane.transform.position.y;
                    // 頭より 1m 以上低い面だけを床・地面の候補にする（机などを除く）
                    if (y < head - 1f && y < best)
                    {
                        best = y;
                    }
                }
            }
            return best < float.MaxValue ? best : head - eyeHeight;
        }
    }
}
