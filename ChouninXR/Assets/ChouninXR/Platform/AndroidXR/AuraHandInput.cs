using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace ChouninXR.AndroidXR
{
    // AURA の手入力（仮）。XR Hands の関節からピンチと光線を作る。
    // ※ 実機で使い心地を確かめて決め直す前提の初版。
    //   右手ピンチ（短く）  … 編集中：持ち上げ／置く   通常：セリフ送り
    //   左手ピンチ（短く）  … 編集中：持っていれば 30° 回転、持っていなければ新しい町人を追加
    //   左手ピンチ（長押し）… 編集モードの切り替え
    //   右手ピンチ（長押し）… 編集中：持ち上げを取り消す
    //   両手ピンチ（長押し）… 編集中：指している／持っている町人を消す
    public class AuraHandInput : MonoBehaviour
    {
        public PlacementEditor editor;
        public ChouninSpawner spawner;
        public Camera viewerCamera;

        public float pinchOn = 0.02f;
        public float pinchOff = 0.04f;
        public float tapMax = 0.4f;
        public float holdTime = 1.0f;

        private class Pinch
        {
            public bool down;
            public float since;
            public bool holdFired;
        }

        private readonly Pinch _left = new Pinch();
        private readonly Pinch _right = new Pinch();
        private readonly List<XRHandSubsystem> _subsystems = new List<XRHandSubsystem>();
        private XRHandSubsystem _hands;
        private LineRenderer _line;

        private Camera Cam => viewerCamera != null ? viewerCamera : Camera.main;

        private void Update()
        {
            if (_hands == null || !_hands.running)
            {
                SubsystemManager.GetSubsystems(_subsystems);
                _hands = _subsystems.Find(s => s.running);
                if (_hands == null)
                {
                    return;
                }
            }
            var cam = Cam;
            if (cam == null)
            {
                return;
            }
            // 手の関節は XR Origin（Camera Offset）基準
            var space = cam.transform.parent;

            bool rightValid = TryPinch(_hands.rightHand, space, out float rightDist, out Vector3 rightAim);
            bool leftValid = TryPinch(_hands.leftHand, space, out float leftDist, out _);

            UpdatePinch(_right, rightValid, rightDist, out bool rightTap, out bool rightHold);
            UpdatePinch(_left, leftValid, leftDist, out bool leftTap, out bool leftHold);

            if (editor != null)
            {
                if (rightValid)
                {
                    var shoulder = ShoulderPosition(cam.transform, true);
                    var dir = (rightAim - shoulder).normalized;
                    editor.SetPointer(new Ray(rightAim, dir));
                    DrawRay(rightAim, dir);
                }
                else
                {
                    editor.SetPointer(default, false);
                    HideRay();
                }

                // 両手長押し → 削除（片手の長押しより先に判定）
                if (editor.IsEditing && _left.down && _right.down && !_left.holdFired && !_right.holdFired &&
                    Time.time - _left.since > holdTime * 1.5f && Time.time - _right.since > holdTime * 1.5f)
                {
                    _left.holdFired = _right.holdFired = true;
                    editor.Delete();
                    return;
                }
                if (leftHold && !_right.down)
                {
                    editor.SetEditing(!editor.IsEditing);
                }
                if (rightHold && !_left.down && editor.IsEditing)
                {
                    editor.Cancel();
                }
            }

            if (editor != null && editor.IsEditing)
            {
                if (rightTap)
                {
                    editor.Primary();
                }
                if (leftTap)
                {
                    if (editor.Held != null)
                    {
                        editor.Rotate(30f);
                    }
                    else
                    {
                        editor.AddNew();
                    }
                }
            }
            else
            {
                HideRay();
                if (rightTap)
                {
                    spawner?.CurrentTalker()?.Advance();
                }
            }
        }

        private static bool TryPinch(XRHand hand, Transform space, out float dist, out Vector3 aim)
        {
            dist = float.MaxValue;
            aim = default;
            if (!hand.isTracked)
            {
                return false;
            }
            if (!hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumb) ||
                !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var index) ||
                !hand.GetJoint(XRHandJointID.IndexProximal).TryGetPose(out var knuckle))
            {
                return false;
            }
            var t = space != null ? space.TransformPoint(thumb.position) : thumb.position;
            var i = space != null ? space.TransformPoint(index.position) : index.position;
            aim = space != null ? space.TransformPoint(knuckle.position) : knuckle.position;
            dist = Vector3.Distance(t, i);
            return true;
        }

        private void UpdatePinch(Pinch p, bool valid, float dist, out bool tap, out bool hold)
        {
            tap = false;
            hold = false;
            bool now = valid && (p.down ? dist < pinchOff : dist < pinchOn);
            if (now && !p.down)
            {
                p.down = true;
                p.since = Time.time;
                p.holdFired = false;
            }
            else if (!now && p.down)
            {
                p.down = false;
                tap = !p.holdFired && Time.time - p.since <= tapMax;
            }
            else if (now && !p.holdFired && Time.time - p.since >= holdTime)
            {
                p.holdFired = true;
                hold = true;
            }
        }

        // 光線の根元（肩のあたり）を頭の向きから推定する
        private static Vector3 ShoulderPosition(Transform head, bool right)
        {
            var yaw = Quaternion.Euler(0f, head.eulerAngles.y, 0f);
            return head.position + yaw * new Vector3(right ? 0.18f : -0.18f, -0.15f, 0f);
        }

        private void DrawRay(Vector3 from, Vector3 dir)
        {
            if (editor == null || !editor.IsEditing)
            {
                HideRay();
                return;
            }
            if (_line == null)
            {
                _line = gameObject.AddComponent<LineRenderer>();
                _line.positionCount = 2;
                _line.widthMultiplier = 0.004f;
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                _line.sharedMaterial = new Material(shader) { color = new Color(0.6f, 1f, 0.8f) };
            }
            _line.enabled = true;
            float len = editor.HasHit ? Vector3.Distance(from, editor.Hit.point) : 3f;
            _line.SetPosition(0, from);
            _line.SetPosition(1, from + dir * len);
        }

        private void HideRay()
        {
            if (_line != null)
            {
                _line.enabled = false;
            }
        }
    }
}
