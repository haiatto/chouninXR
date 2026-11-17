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
    }

    [Serializable]
    public class ChouninPlacement
    {
        public double lat;
        public double lng;
        public string altitudeMode = AltitudeModes.Terrain;
        // terrain / rooftop では面からのオフセット[m]、wgs84 では楕円体高[m]
        public double altitude;
        // 向いている方位（北=0、時計回り、度）
        public float heading;
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
