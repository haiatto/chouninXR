namespace ChouninXR
{
    // 町人XR が扱う「地球上の位置と向き」。基準は 1 つに固定する（docs/COORDINATES.md）。
    //   lat / lng  : WGS84 の緯度・経度 [度]（EPSG:4326。日本の JGD2011 とは実用上同じとみなす）
    //   altitude   : WGS84 楕円体高 [m]（海抜＝標高ではない。日本では標高より約 30〜40m 大きい）
    //   heading    : 真北 = 0、時計回り [度]（磁北ではない）
    // API やプラットフォームが別の基準で返すときは、取り込む側で必ずこの基準に直す。
    public struct GeoPose
    {
        public double lat;
        public double lng;
        public double altitude;
        public float heading;

        // 測ったときの精度（不明なら負の値）
        public double horizontalAccuracy;
        public double verticalAccuracy;
        public double headingAccuracy;

        // どこから得た値か（GeoSources）
        public string source;
    }

    public static class GeoSources
    {
        // Android XR / ARCore の Geospatial（VPS）
        public const string GeospatialVps = "geospatial-vps";
        // Unity エディタ上の偽の位置（本物の測位ではない）
        public const string EditorSim = "editor-sim";
        // 地図から手で置いた
        public const string Map = "map";
    }
}
