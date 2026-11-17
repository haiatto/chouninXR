using System;
using UnityEngine;

namespace ChouninXR
{
    // 1人の町人。近づいたら会話を始め、離れたら終える。
    // 表示（吹き出し）と入力（送り操作）は別コンポーネント／プラットフォームが担当する。
    public class ChouninActor : MonoBehaviour
    {
        public ChouninData Data { get; private set; }
        public TalkSession Talk { get; private set; }
        public bool IsTalking { get; private set; }
        public Transform Body { get; private set; }

        // 会話中にプレイヤーの方を向く速さ[度/秒]。0 で向かない。
        public float turnSpeed = 180f;

        // 0 より大きいと、この秒数で自動的に次の行へ送る
        public float autoAdvanceSeconds = 0f;

        public event Action<ChouninActor, string> LineShown;
        public event Action<ChouninActor> TalkEnded;

        private IChouninWorld _world;
        private float _lineTimer;
        private bool _finishedWhileNear;

        public void Initialize(ChouninData data, IChouninWorld world, Transform body)
        {
            Data = data;
            _world = world;
            Body = body;
            Talk = new TalkSession(data.talk?.lines);
            name = $"Chounin_{data.id}";
        }

        public float DistanceToViewer()
        {
            if (_world == null)
            {
                return float.PositiveInfinity;
            }
            var a = transform.position;
            var b = _world.ViewerPosition;
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void Update()
        {
            if (Data == null || _world == null)
            {
                return;
            }

            float d = DistanceToViewer();
            float trigger = Data.talk?.triggerRadius ?? 3f;
            float leave = Mathf.Max(trigger, Data.talk?.leaveRadius ?? trigger * 2f);

            if (!IsTalking)
            {
                // 一度話し終えたら、いったん離れるまでは話しかけ直さない
                if (_finishedWhileNear && d > leave)
                {
                    _finishedWhileNear = false;
                }
                if (!_finishedWhileNear && d <= trigger && !Talk.IsEmpty)
                {
                    BeginTalk();
                }
            }
            else
            {
                if (d > leave)
                {
                    EndTalk(false);
                }
                else if (autoAdvanceSeconds > 0f)
                {
                    _lineTimer += Time.deltaTime;
                    if (_lineTimer >= autoAdvanceSeconds)
                    {
                        Advance();
                    }
                }
            }

            if (IsTalking && turnSpeed > 0f && Body != null)
            {
                var to = _world.ViewerPosition - Body.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f)
                {
                    var target = Quaternion.LookRotation(to, Vector3.up);
                    Body.rotation = Quaternion.RotateTowards(Body.rotation, target, turnSpeed * Time.deltaTime);
                }
            }
        }

        public void BeginTalk()
        {
            Talk.Restart();
            IsTalking = true;
            _lineTimer = 0f;
            LineShown?.Invoke(this, Talk.Current);
        }

        // 送り操作（タップ、ピンチ、トラックパッドなど）から呼ぶ
        public void Advance()
        {
            if (!IsTalking)
            {
                return;
            }
            Talk.Advance();
            _lineTimer = 0f;
            if (Talk.IsFinished)
            {
                EndTalk(true);
            }
            else
            {
                LineShown?.Invoke(this, Talk.Current);
            }
        }

        public void EndTalk(bool finished)
        {
            if (!IsTalking)
            {
                return;
            }
            IsTalking = false;
            _finishedWhileNear = finished;
            TalkEnded?.Invoke(this);
        }
    }
}
