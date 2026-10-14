using System.Collections.Generic;
using Hangeul.App;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hangeul.UI
{
    /// <summary>
    /// 가방 흔들기의 물리 월드. <b>Unity Physics2D</b>로 카드를 시뮬레이션한다: 카드마다 Rigidbody2D + BoxCollider2D(사각형 그대로 구르고 쌓임),
    /// 격자 영역은 정적 벽 4개로 막는다. 한 칸 = 1 유니티 단위.
    /// 주사위를 쥐고 위로 흔들듯이, 흔드는 시간 동안 몇 번 카드를 위로 던져 올리고 나머지는 중력과 충돌에 맡긴다
    /// (카드를 계속 떠는 진동은 주지 않는다). 벽은 움직이지 않으므로 카드가 화면의 격자 밖으로 나갈 일이 없다.
    /// <para>
    /// <b>독립된 물리 씬</b>(<see cref="LocalPhysicsMode.Physics2D"/>)에서 <b>직접 한 걸음씩</b> 돌린다(<see cref="Tick"/>). 그래서
    /// ① 게임 월드의 물체와 절대 부딪히지 않고, ② 인벤토리를 열어 게임 시간이 멈춰 있어도(<c>Time.timeScale = 0</c>) 물리가 돈다
    /// (독립 물리 씬은 자동으로 시뮬레이션되지 않고, 여기서는 멈추지 않는 시간 <c>Time.unscaledDeltaTime</c>으로 진행한다). 끝나면 씬째 지운다.
    /// </para>
    /// 조립/회전/칸 맞추기는 <see cref="ShakeSession"/>이 모델에 반영한다. 화면(CardView)은 <see cref="ShakeSession.Bodies"/>를 읽어 그린다.
    /// </summary>
    public sealed class ShakeWorld : MonoBehaviour
    {
        const float CardSize = 0.92f;       // 충돌 크기(칸). 카드 화면 크기(칸의 92%)와 같다
        const float WallThickness = 3f;
        const float CalmSpeed = 0.3f, CalmSpin = 20f;   // 이보다 느리면 멈춘 것으로 본다(칸/초, 도/초)
        const float StepSeconds = 1f / 120f;            // 물리 한 걸음
        const int MaxStepsPerTick = 24;

        sealed class Entry
        {
            public ShakeBody Body;
            public Rigidbody2D Rb;
            public GameObject Go;
            public float RawPrev;   // 직전 프레임의 각도. 누적 각도를 만들 때 쓴다
        }

        ShakeSession _session;
        ShakeSettings _s;
        Scene _scene;
        PhysicsScene2D _physics;
        float _time;                // 물리 시간
        float _acc;                 // 아직 쓰지 않은 실제 시간
        float _startReal;
        float _nextToss;            // 다음에 던질 시각(물리 시간)
        bool _disposed;
        readonly List<Entry> _entries = new List<Entry>();
        readonly Dictionary<int, Entry> _byCard = new Dictionary<int, Entry>();
        readonly Queue<(int a, int b)> _contacts = new Queue<(int, int)>();

        public bool Finished => _session.Finished;

        /// <summary>물리 월드를 만든다. cols/rows는 격자 크기(칸). 격자 왼쪽 위가 월드의 (0,0), y는 아래쪽이 −.</summary>
        public static ShakeWorld Create(ShakeSession session, int cols, int rows)
        {
            var scene = SceneManager.CreateScene("InventoryShakePhysics", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            var go = new GameObject("ShakeWorld (인벤토리 흔들기)");
            SceneManager.MoveGameObjectToScene(go, scene);   // 물리 컴포넌트를 붙이기 전에 옮긴다(독립 물리 씬에 등록되도록)

            var w = go.AddComponent<ShakeWorld>();
            w._session = session;
            w._s = session.Settings;
            w._scene = scene;
            w._physics = scene.GetPhysicsScene2D();
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

        static Vector2 WorldOf(float x, float y) => new Vector2(x, -y);   // 격자(y 아래) → 월드(y 위)

        void BuildWalls(int cols, int rows, PhysicsMaterial2D mat)
        {
            var go = new GameObject("Walls");
            go.transform.SetParent(transform, false);   // 부모와 같은 씬으로 옮겨진다. 리지드바디가 없으면 정적 충돌체
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
            rb.interpolation = RigidbodyInterpolation2D.None;   // 직접 한 걸음씩 돌리므로 보간하지 않는다(걸음이 짧아 부드럽다)

            var contact = go.AddComponent<ShakeContact>();
            contact.World = this;
            contact.CardId = body.Card.Id;

            var entry = new Entry { Body = body, Rb = rb, Go = go, RawPrev = rb.rotation };
            _entries.Add(entry);
            _byCard[body.Card.Id] = entry;
        }

        // 물리 한 걸음: 던지기 + 중력을 주고 Simulate. ShakeSeconds 동안 TossInterval마다 카드를 위로(와 약간 옆으로) 던져 올린다.
        // 던지는 사이에는 중력으로 떨어지며 부딪힌다. 카드를 계속 떠는 힘(진동)은 주지 않는다.
        void StepPhysics(float dt)
        {
            _time += dt;

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
            _physics.Simulate(dt);
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

        /// <summary>
        /// 매 프레임(게임 시간이 멈춰 있어도): 흐른 실제 시간만큼 물리를 돌리고, 부딪힌 카드를 조립하고,
        /// 카드 위치/각도를 시뮬레이션에서 읽어 세션에 알리고, 멈췄는지 본다.
        /// </summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (_session.Finished || _disposed) return;

            _acc += Mathf.Min(unscaledDeltaTime, 0.1f);
            for (int n = 0; _acc >= StepSeconds && n < MaxStepsPerTick; n++)
            {
                _acc -= StepSeconds;
                StepPhysics(StepSeconds);
            }

            ProcessContacts();
            SyncPoses();

            bool shaking = _time < _s.ShakeSeconds;
            bool timeout = _time >= _s.ShakeSeconds + _s.MaxSettleSeconds
                || Time.unscaledTime - _startReal > _s.ShakeSeconds + _s.MaxSettleSeconds + 2f;   // 물리가 안 돌아도 끝낸다
            if (!shaking && (timeout || AllCalm())) _session.Finish();
        }

        /// <summary>도중에 강제로 끝낸다(모드가 바뀌었을 때 등). 그 자리에서 가장 가까운 칸에 맞춘다.</summary>
        public void Abort()
        {
            if (_session.Finished || _disposed) return;
            SyncPoses();
            _session.Finish();
        }

        /// <summary>물리 씬째 지운다. 흔들기가 끝난 뒤(또는 도중에 인벤토리가 사라질 때) 부른다.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_scene.IsValid() && _scene.isLoaded) SceneManager.UnloadSceneAsync(_scene);   // 안의 오브젝트도 함께 사라진다
            else if (this != null) Destroy(gameObject);
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

                var p = e.Rb.position;
                float raw = e.Rb.rotation;
                float angle = e.Body.Angle - Mathf.DeltaAngle(e.RawPrev, raw);   // 월드는 반시계가 +, 화면 회전은 시계가 +
                e.RawPrev = raw;

                _session.SetPose(e.Body, p.x, -p.y, angle);
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
