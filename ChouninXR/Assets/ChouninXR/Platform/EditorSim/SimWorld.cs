using System;
using UnityEngine;

namespace ChouninXR.EditorSim
{
    // Unity エディタ上で、偽の位置情報を使って町人を置く世界。
    // Unity 座標の原点 = (originLat, originLng)、+X=東、+Z=北、地面 y=0。
    public class SimWorld : MonoBehaviour, IChouninPlacementWorld
    {
        [Tooltip("原点の緯度。0 のときは村の最初の町人の位置を原点にする")]
        public double originLat;
        public double originLng;
        [Tooltip("原点の地面の WGS84 楕円体高[m]（目安。東京の平地ならおよそ 40m）")]
        public double originAltitude = 40.0;

        [Tooltip("プレイヤー（カメラ）")]
        public Transform viewer;

        public ChouninSpawner spawner;

        public SimSurface surface;

        public IChouninSurface Surface => surface;

        private bool _originResolved;

        public bool IsReady
        {
            get
            {
                ResolveOrigin();
                return _originResolved;
            }
        }

        public string StatusText => _originResolved
            ? $"EditorSim  原点 {originLat:F6}, {originLng:F6}\n現在地 {ViewerLatLng()}"
            : "EditorSim  村の読み込み待ち";

        public Vector3 ViewerPosition => viewer != null ? viewer.position : Vector3.zero;

        private void ResolveOrigin()
        {
            if (_originResolved)
            {
                return;
            }
            if (originLat != 0 || originLng != 0)
            {
                _originResolved = true;
                return;
            }
            var village = spawner != null ? spawner.Village : null;
            if (village == null)
            {
                return;
            }
            if (village.chounins.Count > 0)
            {
                // 最初の町人の 5m 南を原点にして、起動直後に町人が見えるようにする
                var p = village.chounins[0].placement;
                GeoMath.FromLocalEastNorth(p.lat, p.lng, new Vector2(0f, -5f), out originLat, out originLng);
            }
            _originResolved = true;
        }

        private string ViewerLatLng()
        {
            var pos = ViewerPosition;
            GeoMath.FromLocalEastNorth(originLat, originLng, new Vector2(pos.x, pos.z), out var lat, out var lng);
            return $"{lat:F6}, {lng:F6}";
        }

        public void RequestPlacement(ChouninData chounin, Action<Transform> onPlaced)
        {
            var p = chounin.placement;
            var en = GeoMath.ToLocalEastNorth(originLat, originLng, p.lat, p.lng);

            // EditorSim には地形が無いので terrain / rooftop は y=0 の地面からのオフセットとして扱う。
            // surface は y=0 に仮置きし、SurfaceSnapper が面の高さに合わせる
            float y = p.altitudeMode == AltitudeModes.Wgs84 ? (float)(p.altitude - originAltitude)
                : p.altitudeMode == AltitudeModes.Surface ? 0f
                : (float)p.altitude;

            var anchor = new GameObject($"SimAnchor_{chounin.id}").transform;
            anchor.SetParent(transform, false);
            anchor.position = new Vector3(en.x, y, en.y);
            anchor.rotation = GeoMath.HeadingToRotationNorthZ(p.heading);
            onPlaced(anchor);
        }

        // EditorSim の Unity 座標：+X=東、+Y=上、+Z=北（原点 = originLat/Lng/Altitude）
        public bool TryGetGeoPose(Vector3 position, Quaternion rotation, out GeoPose pose)
        {
            pose = default;
            if (!IsReady)
            {
                return false;
            }
            GeoMath.FromLocalEastNorth(originLat, originLng, new Vector2(position.x, position.z),
                out pose.lat, out pose.lng);
            pose.altitude = originAltitude + position.y;
            // +Z=北 なので Y 軸回転がそのまま方位
            pose.heading = Mathf.Repeat(rotation.eulerAngles.y, 360f);
            pose.horizontalAccuracy = pose.verticalAccuracy = pose.headingAccuracy = -1;
            pose.source = GeoSources.EditorSim;
            return true;
        }

        public void ReleasePlacement(Transform placed)
        {
            if (placed != null)
            {
                Destroy(placed.gameObject);
            }
        }
    }
}
