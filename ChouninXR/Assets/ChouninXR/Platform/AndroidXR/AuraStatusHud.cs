using TMPro;
using UnityEngine;

namespace ChouninXR.AndroidXR
{
    // 視界の下のほうに状態（認証・Geospatial の精度など）を出す。実機での確認用。
    // 頭に固定すると酔いやすいので、少し遅れて追いかける（lazy follow）。
    public class AuraStatusHud : MonoBehaviour
    {
        public MonoBehaviour world;
        public PlacementEditor editor;
        public Camera viewerCamera;
        public float distance = 1.2f;
        public float downAngle = 20f;
        public float followSpeed = 3f;
        public float fontSize = 0.035f;

        private IChouninWorld _world;
        private TextMeshPro _text;
        private float _nextUpdate;

        private Camera Cam => viewerCamera != null ? viewerCamera : Camera.main;

        private void Start()
        {
            _world = world as IChouninWorld;
            _text = gameObject.AddComponent<TextMeshPro>();
            var font = JapaneseFont.Get();
            if (font != null)
            {
                _text.font = font;
            }
            _text.fontSize = fontSize * 10f;
            // 光学シースルーでは黒が透けるので、明るい色の文字にする
            _text.color = new Color(0.6f, 1f, 0.8f, 1f);
            _text.alignment = TextAlignmentOptions.Center;
            _text.rectTransform.sizeDelta = new Vector2(0.9f, 0.3f);
        }

        private void LateUpdate()
        {
            var cam = Cam;
            if (cam == null)
            {
                return;
            }

            var t = cam.transform;
            var flatForward = Vector3.ProjectOnPlane(t.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 1e-4f)
            {
                flatForward = t.up;
            }
            flatForward.Normalize();
            var dir = Quaternion.AngleAxis(downAngle, Vector3.Cross(Vector3.up, flatForward)) * flatForward;
            var target = t.position + dir * distance;
            float k = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, target, k);
            transform.rotation = Quaternion.LookRotation(transform.position - t.position, Vector3.up);

            if (Time.unscaledTime >= _nextUpdate && _text != null)
            {
                _nextUpdate = Time.unscaledTime + 0.25f;
                _text.text = (_world != null ? _world.StatusText : "world 未設定") +
                             (editor != null && editor.IsEditing ? "\n" + editor.StatusText : "");
            }
        }
    }
}
