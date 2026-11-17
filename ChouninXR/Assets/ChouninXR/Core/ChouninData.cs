using System;

namespace ChouninXR
{
    // 町人データ（chounin.json）。JsonUtility で読めるようにフィールドで持つ。
    // スキーマは docs/DESIGN.md と schema/ を正とする。

    [Serializable]
    public class ChouninData
    {
        public string schema = "chouninxr/chounin@1";
        public string id;
        public string name;
        public ChouninModel model = new ChouninModel();
        public ChouninPlacement placement = new ChouninPlacement();
        public ChouninTalk talk = new ChouninTalk();
        public ChouninLlm llm = new ChouninLlm();

        // 読み込み元フォルダ（JSON には含めない）
        [NonSerialized] public string baseDirectory;
    }

    [Serializable]
    public class ChouninModel
    {
        // chounin.json からの相対パス、または絶対パス
        public string vrm;
        // VRM の身長に関係なく見た目の大きさを合わせたいとき用（1 = そのまま）
        public float scale = 1f;
    }

    public static class AltitudeModes
    {
        // 地面（Geospatial の Terrain アンカー）からの高さ
        public const string Terrain = "terrain";
        // 建物の屋上（Rooftop アンカー）からの高さ
        public const string Rooftop = "rooftop";
        // WGS84 楕円体高（Geospatial が返す altitude と同じ基準）
        public const string Wgs84 = "wgs84";
        // その場で見つかった面（メッシュ → 平面 → 地面相当の順）に立たせる。
        // 地図の標高や Geospatial の高度は数 m ずれることがあるので、現地で直接置いた町人はこれを使う。
        // altitude には置いたときの WGS84 高度を「目安」として残す（面が見つかるまでの仮の高さ）。
        public const string Surface = "surface";
    }

    // 座標の基準は GeoPose と docs/COORDINATES.md：
    //   lat/lng = WGS84 [度]、wgs84/surface の altitude = WGS84 楕円体高 [m]、heading = 真北から時計回り [度]
    [Serializable]
    public class ChouninPlacement
    {
        public double lat;
        public double lng;
        public string altitudeMode = AltitudeModes.Terrain;
        // terrain / rooftop では面からのオフセット[m]、wgs84 では楕円体高[m]、
        // surface では置いたときの WGS84 高度（目安）
        public double altitude;
        // surface のとき、見つかった面からの高さ[m]（台の上に立たせたいときなど）
        public float heightAboveSurface;
        // 向いている方位（北=0、時計回り、度）
        public float heading;
        // どうやって置いたかの記録（精度・取得元・日時）。位置ずれを調べるときに使う
        public ChouninPlacementRecord placedWith = new ChouninPlacementRecord();
    }

    [Serializable]
    public class ChouninPlacementRecord
    {
        // GeoSources（geospatial-vps / editor-sim / map）。空なら不明
        public string source;
        // 置いたときの精度 [m] / [度]。負の値は不明
        public double horizontalAccuracy = -1;
        public double verticalAccuracy = -1;
        public double headingAccuracy = -1;
        // ISO 8601（UTC）
        public string placedAtUtc;
        // 高さを決めた面（mesh / plane / ground-estimate）
        public string surface;
    }

    [Serializable]
    public class ChouninTalk
    {
        // この距離[m]まで近づくと話し始める
        public float triggerRadius = 3f;
        // この距離[m]より離れると会話を終える
        public float leaveRadius = 6f;
        public string[] lines = Array.Empty<string>();
    }

    [Serializable]
    public class ChouninLlm
    {
        public bool enabled;
        public string prompt;
    }

    [Serializable]
    public class VillageData
    {
        public string schema = "chouninxr/village@1";
        public string id;
        public string name;
        public string author;
        public string description;
    }
}
