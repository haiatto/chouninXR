using System;
using UnityEngine;

namespace ChouninXR
{
    public static class GeoMath
    {
        private const double EarthRadius = 6378137.0; // WGS84 長半径[m]

        // origin を原点とした局所座標（+X=東, +Z=北, メートル）へ。
        // 数 km 程度の範囲なら十分な精度。EditorSim と地図表示で使う。
        public static Vector2 ToLocalEastNorth(double originLat, double originLng, double lat, double lng)
        {
            double dLat = (lat - originLat) * Math.PI / 180.0;
            double dLng = (lng - originLng) * Math.PI / 180.0;
            double east = dLng * EarthRadius * Math.Cos(originLat * Math.PI / 180.0);
            double north = dLat * EarthRadius;
            return new Vector2((float)east, (float)north);
        }

        public static void FromLocalEastNorth(double originLat, double originLng, Vector2 eastNorth,
            out double lat, out double lng)
        {
            lat = originLat + eastNorth.y / EarthRadius * 180.0 / Math.PI;
            lng = originLng + eastNorth.x / (EarthRadius * Math.Cos(originLat * Math.PI / 180.0)) * 180.0 / Math.PI;
        }

        // 方位（北=0、時計回り）→ Unity の Y 軸回転。
        // Unity の +Z を北とする座標系（EditorSim）用。
        public static Quaternion HeadingToRotationNorthZ(float heading)
            => Quaternion.AngleAxis(heading, Vector3.up);

        // Geospatial の EUS 座標系（+X=東, +Y=上, +Z=南）用。
        public static Quaternion HeadingToRotationEus(float heading)
            => Quaternion.AngleAxis(180f - heading, Vector3.up);
    }
}
