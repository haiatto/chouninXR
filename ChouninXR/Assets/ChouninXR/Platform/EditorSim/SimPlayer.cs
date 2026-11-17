using UnityEngine;
using UnityEngine.InputSystem;

namespace ChouninXR.EditorSim
{
    // エディタ確認用の歩き回り操作。
    //   WASD / 矢印: 移動   右ドラッグ: 見回す   Shift: 走る
    //   Space / 左クリック: セリフを送る
    public class SimPlayer : MonoBehaviour
    {
        public float walkSpeed = 1.4f;
        public float runSpeed = 5f;
        public float lookSpeed = 0.15f;
        public float eyeHeight = 1.6f;

        public ChouninSpawner spawner;
        public SimWorld world;

        private float _yaw;
        private float _pitch;

        private void Start()
        {
            var e = transform.eulerAngles;
            _yaw = e.y;
            _pitch = e.x;
            var p = transform.position;
            p.y = eyeHeight;
            transform.position = p;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null)
            {
                return;
            }

            if (mouse.rightButton.isPressed)
            {
                var d = mouse.delta.ReadValue();
                _yaw += d.x * lookSpeed;
                _pitch = Mathf.Clamp(_pitch - d.y * lookSpeed, -80f, 80f);
            }
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            var move = Vector3.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.z += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.z -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
            if (move.sqrMagnitude > 0f)
            {
                float speed = kb.leftShiftKey.isPressed ? runSpeed : walkSpeed;
                var flat = Quaternion.Euler(0f, _yaw, 0f) * move.normalized;
                transform.position += flat * speed * Time.deltaTime;
            }

            if (kb.spaceKey.wasPressedThisFrame || mouse.leftButton.wasPressedThisFrame)
            {
                spawner?.CurrentTalker()?.Advance();
            }
        }

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            style.normal.textColor = Color.white;
            string status = world != null ? world.StatusText : "";
            string help = "WASD:移動  右ドラッグ:見回す  Shift:走る  Space/クリック:セリフ送り";
            GUI.Label(new Rect(10, 10, 900, 80), status + "\n" + help, style);
        }
    }
}
