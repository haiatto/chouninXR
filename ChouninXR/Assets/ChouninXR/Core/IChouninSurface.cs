using UnityEngine;

namespace ChouninXR
{
    // 何に当たったか。数字が大きいほど信用できる。
    public enum SurfaceKind
    {
        None = 0,
        // メッシュも平面も無いときの推定（頭の高さから身長分下、など）
        GroundEstimate = 1,
        // 検出された平面
        Plane = 2,
        // 環境メッシュ（実際の形）
        Mesh = 3,
    }

    public struct SurfaceHit
    {
        public Vector3 point;
        public Vector3 normal;
        public SurfaceKind kind;
    }

    // 「どこに立たせられるか」を調べる窓口。中身（メッシュ・平面・推定）はプラットフォームごと。
    public interface IChouninSurface
    {
        // 光線を飛ばして、いちばん信用できる面に当てる（メッシュ → 平面 → 地面相当）
        bool Raycast(Ray ray, float maxDistance, out SurfaceHit hit);
    }

    // 現地で直接置くための窓口（置ける世界だけが実装する）
    public interface IChouninPlacementWorld : IChouninWorld
    {
        IChouninSurface Surface { get; }

        // Unity ワールドの位置・向き → GeoPose（WGS84・楕円体高・真北基準）。位置合わせ前は false。
        bool TryGetGeoPose(Vector3 position, Quaternion rotation, out GeoPose pose);
    }
}
