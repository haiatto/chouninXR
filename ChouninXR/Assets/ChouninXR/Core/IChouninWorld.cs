using System;
using UnityEngine;

namespace ChouninXR
{
    // 町人側が「最低限」必要とする窓口。
    // ここに載らないプラットフォーム固有機能（遮蔽、平面、ハンド、トラックパッドなど）は
    // 各プラットフォームの実装が直接扱う。この窓口を機能の上限にしないこと。
    public interface IChouninWorld
    {
        // 町人を置ける状態か（位置合わせが済んでいるか）
        bool IsReady { get; }

        // 人に見せる状態表示（デバッグ・案内用）
        string StatusText { get; }

        // プレイヤー（頭）の Unity ワールド位置
        Vector3 ViewerPosition { get; }

        // 町人を置く。親にすべき Transform（アンカー等）を非同期で返す。
        // 失敗時は null。プラットフォームによっては位置が後から補正される。
        void RequestPlacement(ChouninData chounin, Action<Transform> onPlaced);

        // 置いた町人を取り除く（アンカーの解放など）
        void ReleasePlacement(Transform placed);
    }
}
