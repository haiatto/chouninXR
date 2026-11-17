using System;
using System.Collections.Generic;
using Google.XR.Extensions;
using UnityEngine;
using UnityEngine.XR.OpenXR;

namespace ChouninXR.AndroidXR
{
    // XREAL AURA（Android XR）用。Android XR Extensions の Geospatial で町人を置く。
    // 参考：com.google.xr.extensions の Samples~/Geospatial
    // ※ 実機未検証の初版。AURA 到着後に調整する。
    [RequireComponent(typeof(AndroidXRPermissionUtil))]
    public class AndroidXrWorld : MonoBehaviour, IChouninPlacementWorld
    {
        public XRGeospatialManager geospatialManager;
        public Camera viewerCamera;
        public AuraSurface surface;

        public IChouninSurface Surface => surface;

        [Tooltip("この精度[m]より良くなったら町人を置き始める")]
        public double requiredHorizontalAccuracy = 5.0;
        [Tooltip("この方位精度[度]より良くなったら町人を置き始める")]
        public double requiredYawAccuracy = 10.0;

        private AndroidXRPermissionUtil _permissions;
        private bool _setupDone;
        private string _authStatus = "認証前";
        private XRGeospatialPoseResult _lastPose;
        private bool _hasPose;
        private readonly HashSet<Transform> _anchors = new HashSet<Transform>();

        public bool IsReady =>
            _hasPose &&
            _lastPose.HorizontalAccuracy <= requiredHorizontalAccuracy &&
            _lastPose.OrientationYawAccuracy <= requiredYawAccuracy;

        public string StatusText =>
            $"Geospatial {XRGeospatialManager.State} / 認証 {_authStatus}\n" +
            (_hasPose
                ? $"{_lastPose.Pose.Latitude:F6}, {_lastPose.Pose.Longitude:F6}  " +
                  $"水平±{_lastPose.HorizontalAccuracy:F1}m  方位±{_lastPose.OrientationYawAccuracy:F1}°"
                : "位置未取得");

        public Vector3 ViewerPosition => Cam != null ? Cam.transform.position : Vector3.zero;

        private Camera Cam => viewerCamera != null ? viewerCamera : Camera.main;

        private void Awake()
        {
            _permissions = GetComponent<AndroidXRPermissionUtil>();
            if (geospatialManager != null)
            {
                // 権限がそろうまで止めておく（サンプルと同じ流れ）
                geospatialManager.enabled = false;
            }
        }

        private void OnEnable()
        {
            XRGoogleCloudFeature.OnAuthResultReceived += OnAuthResult;
        }

        private void OnDisable()
        {
            XRGoogleCloudFeature.OnAuthResultReceived -= OnAuthResult;
            Input.location.Stop();
        }

        private void Update()
        {
            if (!_setupDone && _permissions.AllPermissionGranted())
            {
                _setupDone = true;
                StartGeospatial();
            }

            _hasPose = false;
            if (geospatialManager != null && geospatialManager.Running && Cam != null)
            {
                var camPose = new Pose(Cam.transform.position, Cam.transform.rotation);
                if (geospatialManager.TryLocate(camPose, out XRGeospatialPoseResult r) && r.IsPositionValid)
                {
                    _lastPose = r;
                    _hasPose = true;
                }
            }
        }

        private void StartGeospatial()
        {
            // GPS（VPS の初期位置に使われる）
            if (Input.location.isEnabledByUser)
            {
                Input.location.Start();
            }
            else
            {
                Debug.LogWarning("[Chounin] 端末の位置情報がオフです");
            }

            // 認証は OpenXR 機能「Android XR: Google Cloud」の設定（Keyless）で
            // セッション開始時に自動で行われる。ここでは結果を受け取るだけ。
            var cloud = OpenXRSettings.Instance.GetFeature<XRGoogleCloudFeature>();
            if (cloud == null || !cloud.enabled)
            {
                _authStatus = "Google Cloud 機能が無効";
            }
            else if (_authStatus == "認証前")
            {
                _authStatus = "自動（Keyless）";
            }

            if (geospatialManager != null)
            {
                geospatialManager.enabled = true;
            }
        }

        private void OnAuthResult(XRGoogleCloudAuthResult result)
        {
            _authStatus = result.CloudAuthError == XRGoogleCloudAuthError.None
                ? "OK"
                : $"エラー {result.CloudAuthError}";
            Debug.Log($"[Chounin] Google Cloud 認証: {result}");
        }

        public async void RequestPlacement(ChouninData chounin, Action<Transform> onPlaced)
        {
            var p = chounin.placement;
            var rotation = GeoMath.HeadingToRotationEus(p.heading);

            try
            {
                UnityEngine.XR.ARSubsystems.Result<ARShareableAnchor> result;
                float offsetY = 0f;
                switch (p.altitudeMode)
                {
                    case AltitudeModes.Wgs84:
                    case AltitudeModes.Surface:
                        // surface は altitude（置いたときの高度）を仮の高さにし、
                        // 実際の高さは SurfaceSnapper がメッシュ・平面・地面相当から決める
                        result = geospatialManager.TryAttachGeospatialAnchor(
                            new XRGeospatialPose(rotation, p.lat, p.lng, p.altitude));
                        break;
                    case AltitudeModes.Rooftop:
                        offsetY = (float)p.altitude;
                        result = await geospatialManager.TryAttachSurfaceAnchorAsync(
                            XRGeospatialSurfaceType.Rooftop,
                            new XRGeospatialPose(rotation, p.lat, p.lng, 0));
                        break;
                    default:
                        offsetY = (float)p.altitude;
                        result = await geospatialManager.TryAttachSurfaceAnchorAsync(
                            XRGeospatialSurfaceType.Terrain,
                            new XRGeospatialPose(rotation, p.lat, p.lng, 0));
                        break;
                }

                if (!result.status.IsSuccess() || result.value == null)
                {
                    Debug.LogWarning($"[Chounin] {chounin.id} のアンカー作成に失敗: {result.status}");
                    onPlaced(null);
                    return;
                }

                var anchor = result.value.transform;
                _anchors.Add(anchor);

                // 面からの高さオフセットは子にずらして表現する
                var holder = new GameObject("Offset").transform;
                holder.SetParent(anchor, false);
                holder.localPosition = new Vector3(0f, offsetY, 0f);
                onPlaced(holder);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                onPlaced(null);
            }
        }

        // Android XR Geospatial：緯度経度 = WGS84、Altitude = WGS84 楕円体高、
        // Rotation = EUS（+X=東、+Y=上、+Z=南）。町人XR の基準（GeoPose）とは回転だけ直す
        public bool TryGetGeoPose(Vector3 position, Quaternion rotation, out GeoPose pose)
        {
            pose = default;
            if (!IsReady || !geospatialManager.TryLocate(new Pose(position, rotation), out XRGeospatialPoseResult r) ||
                !r.IsPositionValid)
            {
                return false;
            }
            pose.lat = r.Pose.Latitude;
            pose.lng = r.Pose.Longitude;
            pose.altitude = r.Pose.Altitude;
            // EUS の Y 軸回転 → 真北基準の方位（HeadingToRotationEus の逆）
            pose.heading = Mathf.Repeat(180f - r.Pose.Rotation.eulerAngles.y, 360f);
            pose.horizontalAccuracy = r.HorizontalAccuracy;
            pose.verticalAccuracy = r.VerticalAccuracy;
            pose.headingAccuracy = r.OrientationYawAccuracy;
            pose.source = GeoSources.GeospatialVps;
            return true;
        }

        public void ReleasePlacement(Transform placed)
        {
            if (placed == null)
            {
                return;
            }
            var anchor = placed.parent != null && _anchors.Contains(placed.parent) ? placed.parent : placed;
            _anchors.Remove(anchor);
            Destroy(anchor.gameObject);
        }
    }
}
