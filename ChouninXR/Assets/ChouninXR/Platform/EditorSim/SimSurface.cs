using UnityEngine;

namespace ChouninXR.EditorSim
{
    // EditorSim の面：シーンのコライダー（地面・箱など）を「メッシュ」として扱い、
    // 何も無いところは y = groundHeight を地面相当とする。
    public class SimSurface : SurfaceRaycasterBase
    {
        public float groundHeight = 0f;

        public override float GroundHeight(Vector3 near) => groundHeight;
    }
}
