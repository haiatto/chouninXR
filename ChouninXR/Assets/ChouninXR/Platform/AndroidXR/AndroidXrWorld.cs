using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.XR.Extensions;
using UnityEngine;
using UnityEngine.XR.OpenXR;

namespace ChouninXR.AndroidXR
{
    // XREAL AURA（Android XR）用。Android XR Extensions の Geospatial で町人を置く。
    // 参考：com.google.xr.extensions の Samples~/Geospatial
    // ※ 実機未検証の初版。AURA 到着後に調整する。
    [RequireComponent(typeof(AndroidXRPermissionUtil))]
    public class AndroidXrWorld : MonoBehaviour, IChouninWorld
    {
        public XRGeospatialManager geospatialManager;
        public Camera viewerCamera;

        [Tooltip("この精度[m]より良くなったら町人を置き始める")]
        public double requiredHorizontalAccuracy = 5.0;
        [Tooltip("この方位精度[度]より良くなったら町人を置き始める")]
        public double requiredYawAccuracy = 10.0;

        private AndroidXRPermissionUtil _permissions;
        private bool _authStarted;
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
        }

        private void Update()
        {
            if (!_authStarted && _permissions.AllPermissionGranted())
            {
                _authStarted = true;
                _ = AuthenticateAsync();
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

        private async Task AuthenticateAsync()
        {
            var cloud = OpenXRSettings.Instance.GetFeature<XRGoogleCloudFeature>();
            if (cloud == null)
            {
                _authStatus = "Google Cloud 機能が無効";
                return;
            }

            // Keyless 認証（アプリの署名と Google Cloud の OAuth クライアントを紐づける方式）。
            // API キーをアプリに埋め込まずに済む。
            for (int attempt = 0; attempt < 5; attempt++)
            {
                var result = await cloud.TrySetAuthenticationAsync(XRAuthenticationStrategy.Keyless);
                if (result.IsSuccess())
                {
                    _authStatus = "OK";
                    if (geospatialManager != null && !geospatialManager.enabled)
                    {
                        geospatialManager.enabled = true;
                    }
                    return;
                }
                _authStatus = $"失敗 {result}";
                await Task.Delay(2000);
            }
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
