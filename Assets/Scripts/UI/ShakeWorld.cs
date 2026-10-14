using System.Collections.Generic;
using Hangeul.App;
using UnityEngine;

namespace Hangeul.UI
{
    /// <summary>
    /// 가방 흔들기의 물리 월드. <b>Unity Physics2D</b>로 카드를 시뮬레이션한다: 카드마다 Rigidbody2D + BoxCollider2D(사각형 그대로 구르고 쌓임),
    /// 격자 영역은 정적 벽 4개로 막는다. 한 칸 = 1 유니티 단위.
    /// 주사위를 쥐고 위로 흔들듯이, 흔드는 시간 동안 몇 번 카드를 위로 던져 올리고 나머지는 중력과 충돌에 맡긴다
    /// (카드를 계속 떠는 진동은 주지 않는다). 벽은 움직이지 않으므로 카드가 화면의 격자 밖으로 나갈 일이 없다.
    /// 게임 월드와 부딪히지 않도록 멀리 떨어진 좌표(<c>origin</c>)에 만들고, 끝나면 모두 지운다.
    /// 조립/회전/칸 맞추기는 <see cref="ShakeSession"/>이 모델에 반영한다. 화면(CardView)은 <see cref="ShakeSession.Bodies"/>를 읽어 그린다.
    /// </summary>
    public sealed class ShakeWorld : MonoBehaviour
    {
        const float CardSize = 0.92f;       // 충돌 크기(칸). 카드 화면 크기(칸의 92%)와 같다
        const float WallThickness = 3f;
        const float CalmSpeed = 0.3f, CalmSpin = 20f;   // 이보다 느리면 멈춘 것으로 본다(칸/초, 도/초)

        sealed class Entry
        {
            public ShakeBody Body;
            public Rigidbody2D Rb;
            public GameObject Go;
            public float RawPrev;   // 직전 프레임의 각도(0~360). 누적 각도를 만들 때 쓴다
        }

        ShakeSession _session;
        ShakeSettings _s;
        Vector2 _origin;
        float _time;                // 물리 시간(FixedUpdate로 흐른다)
        float _startReal;
        float _nextToss;            // 다음에 던질 시각(물리 시간)
        readonly List<Entry> _entries = new List<Entry>();
        readonly Dictionary<int, Entry> _byCard = new Dictionary<int, Entry>();
        readonly Queue<(int a, int b)> _contacts = new Queue<(int, int)>();

        public bool Finished => _session.Finished;

        /// <summary>
        /// 물리 월드를 만든다. origin은 게임 월드와 멀리 떨어진 좌표여야 한다(격자 왼쪽 위가 이 점). cols/rows는 격자 크기(칸).
        /// </summary>
        public static ShakeWorld Create(ShakeSession session, Vector2 origin, int cols, int rows)
        {
            var go = new GameObject("ShakeWorld (인벤토리 흔들기)");
            go.transform.position = origin;
            var w = go.AddComponent<ShakeWorld>();
            w._session = session;
            w._s = session.Settings;
            w._origin = origin;
            w._startReal = Time.unscaledTime;
            w.Build(cols, rows);
            return w;
        }

        void Build(int cols, int rows)
        {
            var mat = new PhysicsMaterial2D("ShakeCard") { friction = _s.Friction, bounciness = _s.Bounce };
            BuildWalls(cols, rows, mat);
            foreach (var body in _session.Bodies) BuildCard(body, mat);
        }

        Vector2 WorldOf(float x, float y) => _origin + new Vector2(x, -y);   // 격자(y 아래) → 월드(y 위)

        void BuildWalls(int cols, int rows, PhysicsMaterial2D mat)
        {
            var go = new GameObject("Walls");
            go.transform.SetParent(transform, false);   // 리지드바디가 없으면 정적 충돌체
            float t = WallThickness;
            AddBox(go, new Vector2(cols / 2f, -rows - t / 2f), new Vector2(cols + 2f * t, t), mat);   // 바닥
            AddBox(go, new Vector2(cols / 2f, t / 2f), new Vector2(cols + 2f * t, t), mat);           // 천장
            AddBox(go, new Vector2(-t / 2f, -rows / 2f), new Vector2(t, rows + 2f * t), mat);         // 왼쪽
            AddBox(go, new Vector2(cols + t / 2f, -rows / 2f), new Vector2(t, rows + 2f * t), mat);   // 오른쪽
        }

        static void AddBox(GameObject go, Vector2 center, Vector2 size, PhysicsMaterial2D mat)
        {
            var box = go.AddComponent<BoxCollider2D>();
            box.offset = center;
            box.size = size;
            box.sharedMaterial = mat;
        }

        void BuildCard(ShakeBody body, PhysicsMaterial2D mat)
        {
            var go = new GameObject("Card " + body.Card.Id);
            go.transform.SetParent(transform, false);
            go.transform.position = WorldOf(body.X, body.Y);

            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(CardSize, CardSize);
            box.sharedMaterial = mat;

            if (body.Fixed) return;   // 완성된 단어의 카드: 리지드바디 없이 정적 장애물

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;                  // 중력은 아래에서 직접 준다(프로젝트의 Physics2D 중력 설정과 무관하게)
            rb.mass = 1f;
            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.2f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            var contact = go.AddComponent<ShakeContact>();
            contact.World = this;
            contact.CardId = body.Card.Id;

            var entry = new Entry { Body = body, Rb = rb, Go = go, RawPrev = go.transform.eulerAngles.z };
            _entries.Add(entry);
            _byCard[body.Card.Id] = entry;
        }

        // 중력 + 던지기: ShakeSeconds 동안 TossInterval마다 카드를 위로(와 약간 옆으로) 던져 올린다. 던지는 사이에는 중력으로 떨어지며 부딪힌다.
        // 카드를 계속 떠는 힘(진동)은 주지 않는다.
        void FixedUpdate()
        {
            if (_session == null || _session.Finished) return;
            _time += Time.fixedDeltaTime;

            if (_s.TossSpeed > 0f && _time < _s.ShakeSeconds && _time >= _nextToss)
            {
                _nextToss = _time + Mathf.Max(0.2f, _s.TossInterval);
                Toss();
            }

            var gravity = new Vector2(0f, -_s.Gravity);
            foreach (var e in _entries)
            {
                if (e.Body.Dead || e.Rb == null) continue;
                e.Rb.AddForce(gravity * e.Rb.mass);
            }
        }

        // 모든 움직이는 카드를 한꺼번에 던져 올린다(주사위를 쥐고 위로 흔들듯이). 속도와 방향을 카드마다 조금씩 다르게 한다.
        void Toss()
        {
            foreach (var e in _entries)
            {
                if (e.Body.Dead || e.Rb == null) continue;
                float up = _s.TossSpeed * (0.75f + 0.5f * Random.value);
                float side = (Random.value * 2f - 1f) * _s.TossSpeed * _s.Sideways;
                e.Rb.linearVelocity = new Vector2(side, up);
                e.Rb.angularVelocity += (Random.value * 2f - 1f) * _s.Spin;
            }
        }

        internal void OnContact(int a, int b)
        {
            if (a < b) _contacts.Enqueue((a, b));   // 같은 충돌이 양쪽에서 한 번씩 오므로 한쪽만 받는다
        }

        /// <summary>매 프레임: 부딪힌 카드를 조립하고, 카드 위치/각도를 시뮬레이션에서 읽어 세션에 알리고, 멈췄는지 본다.</summary>
        public void Tick()
        {
            if (_session.Finished) return;

            ProcessContacts();
            SyncPoses();

            bool shaking = _time < _s.ShakeSeconds;
            bool timeout = _time >= _s.ShakeSeconds + _s.MaxSettleSeconds
                || Time.unscaledTime - _startReal > _s.ShakeSeconds + _s.MaxSettleSeconds + 2f;   // 물리가 안 돌아도(timeScale 0 등) 끝낸다
            if (!shaking && (timeout || AllCalm())) _session.Finish();
        }

        /// <summary>도중에 강제로 끝낸다(모드가 바뀌었을 때 등). 그 자리에서 가장 가까운 칸에 맞춘다.</summary>
        public void Abort()
        {
            if (_session.Finished) return;
            SyncPoses();
            _session.Finish();
        }

        void ProcessContacts()
        {
            while (_contacts.Count > 0)
            {
                var (ida, idb) = _contacts.Dequeue();
                if (!_byCard.TryGetValue(ida, out var a) || !_byCard.TryGetValue(idb, out var b)) continue;
                if (a.Body.Dead || b.Body.Dead) continue;

                var velocity = (a.Rb.linearVelocity + b.Rb.linearVelocity) / 2f;   // 합쳐진 카드는 두 카드의 평균 속도로 이어서 움직인다
                if (!_session.TryMerge(a.Body, b.Body)) continue;

                var dead = a.Body.Dead ? a : b;
                var alive = dead == a ? b : a;
                alive.Rb.linearVelocity = velocity;
                _byCard.Remove(dead.Body.Card.Id);
                Destroy(dead.Go);
            }
        }

        void SyncPoses()
        {
            foreach (var e in _entries)
            {
                if (e.Body.Dead || e.Rb == null) continue;

                var t = e.Rb.transform;   // 보간된 위치/각도(프레임마다 부드럽게)
                float raw = t.eulerAngles.z;
                float angle = e.Body.Angle - Mathf.DeltaAngle(e.RawPrev, raw);   // 월드는 반시계가 +, 화면 회전은 시계가 +
                e.RawPrev = raw;

                _session.SetPose(e.Body, t.position.x - _origin.x, -(t.position.y - _origin.y), angle);
            }
        }

        bool AllCalm()
        {
            foreach (var e in _entries)
            {
                if (e.Body.Dead || e.Rb == null) continue;
                if (e.Rb.IsSleeping()) continue;
                if (e.Rb.linearVelocity.magnitude > CalmSpeed || Mathf.Abs(e.Rb.angularVelocity) > CalmSpin) return false;
            }
            return true;
        }
    }
}
