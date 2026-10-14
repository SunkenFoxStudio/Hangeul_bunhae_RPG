using System;
using System.Collections.Generic;
using System.Linq;
using Hangeul.Domain;

namespace Hangeul.App
{
    /// <summary>
    /// 가방 흔들기(물리) 설정. 길이 단위는 "칸"(한 칸 = 1), 시간은 초, 각도는 도.
    /// 물리 값(TossSpeed~Friction)은 Unity Physics2D 쪽(<c>ShakeWorld</c>)이 쓰고, 조립 확률은 <see cref="ShakeSession"/>이 쓴다.
    /// </summary>
    [Serializable]
    public sealed class ShakeSettings
    {
        /// <summary>부딪힌 두 카드가 합칠 수 있을 때 실제로 조립될 확률(0~1). 새로 부딪힐 때마다 한 번 굴린다.</summary>
        public float MergeChance = 1f;
        /// <summary>카드를 던져 올리는 시간(초). 이 시간 동안 <see cref="TossInterval"/>마다 한 번씩 던진다.</summary>
        public float ShakeSeconds = 1.6f;
        /// <summary>흔들기가 끝난 뒤 카드가 멈추기를 기다리는 최대 시간(초). 이 안에 멈추지 않아도 강제로 칸에 맞춘다.</summary>
        public float MaxSettleSeconds = 2.5f;
        /// <summary>던질 때 카드가 위로 튀어 오르는 속도(칸/초). 0이면 던지지 않고 중력으로 떨어지기만 한다.</summary>
        public float TossSpeed = 9f;
        /// <summary>던지는 간격(초). 던지는 사이에는 중력으로 떨어지며 서로 부딪힌다(진동처럼 떨지 않는다).</summary>
        public float TossInterval = 0.7f;
        /// <summary>던질 때 좌우로 흩어지는 정도(위로 던지는 속도에 대한 비율).</summary>
        public float Sideways = 0.35f;
        /// <summary>던질 때 카드가 도는 세기(도/초). 0이면 돌지 않는다(충돌로 도는 것은 그대로).</summary>
        public float Spin = 360f;
        /// <summary>아래로 당기는 중력(칸/초²). 0이면 둥둥 떠다닌다. 중력이 있으면 끝났을 때 카드가 아래쪽에 쌓인다.</summary>
        public float Gravity = 12f;
        /// <summary>카드와 벽이 튕기는 정도(0이면 붙고 1이면 그대로 튕김).</summary>
        public float Bounce = 0.35f;
        /// <summary>카드와 벽 사이 마찰.</summary>
        public float Friction = 0.35f;
    }

    /// <summary>흔들리는 동안 카드 한 장의 상태. 위치는 카드 중심(칸 단위, 격자 왼쪽 위가 원점, y는 아래 방향).</summary>
    public sealed class ShakeBody
    {
        public Card Card;
        public float X, Y;
        /// <summary>물리로 돈 누적 각도(도, 화면에서 시계방향 +).</summary>
        public float Angle;
        /// <summary>자모의 "원래 모양"이 똑바로 서 있던 각도. 90° 단위 회전 횟수는 (Angle − AngleBase)로 센다(조립 직후 새로 시작).</summary>
        public float AngleBase;
        public bool Fixed;                // 완성된 단어의 카드: 움직이지 않고 장애물로만 있다
        public bool Dead;                 // 다른 카드에 조립돼 사라짐
        public RotationSession Rotation;  // 자모 카드만

        /// <summary>
        /// 화면에 그릴 글자면의 회전각(시계방향 도). 90°를 넘을 때마다 자모가 바뀌므로, 바뀐 만큼(Rn·90°)은 글자가 맡고
        /// 나머지만 돌려 그린다 — 글자가 바뀌어도 끊기지 않고 물리 각도 그대로 보인다. 자모가 아닌 카드는 물리 각도 그대로 돈다.
        /// </summary>
        public float DisplayAngleDeg => Rotation != null ? Angle - Rotation.Rn * 90f : Angle;
    }

    /// <summary>
    /// 가방 흔들기(물리)의 규칙 쪽. 물리 계산은 Unity Physics2D(<c>ShakeWorld</c>)가 하고, 이 클래스는 그 결과를 인벤토리 모델에 반영한다:
    /// 부딪힌 카드의 조립(<see cref="TryMerge"/>), 도는 각도에 따른 자모 모양(<see cref="SetPose"/>),
    /// 끝났을 때 가장 가까운 빈 칸에 맞추기(<see cref="Finish"/>). 순수 C#이라 물리 없이도 EditMode에서 검증한다.
    /// 완성된 단어를 이루는 카드(<see cref="InventoryModel.CompletedWordCardIds"/>)는 <see cref="ShakeBody.Fixed"/>로 가만히 있고 합쳐지지도 않는다.
    /// 흔드는 동안 모델 카드의 칸(Col/Row)은 그대로이고, <see cref="Finish"/>에서 한꺼번에 바뀐다.
    /// </summary>
    public sealed class ShakeSession
    {
        readonly InventoryModel _model;
        readonly ShakeSettings _s;
        readonly Random _rng;
        readonly List<ShakeBody> _bodies = new List<ShakeBody>();
        readonly int _cols, _rows;

        public bool Finished { get; private set; }
        /// <summary>조립돼 사라진 카드 수.</summary>
        public int Merged { get; private set; }
        public IReadOnlyList<ShakeBody> Bodies => _bodies;
        public ShakeSettings Settings => _s;

        public ShakeSession(InventoryModel model, ShakeSettings settings = null, Random rng = null)
        {
            _model = model;
            _s = settings ?? new ShakeSettings();
            _rng = rng ?? new Random();
            _cols = model.Grid.Cols;
            _rows = model.Grid.Rows;

            var guarded = model.CompletedWordCardIds();
            foreach (var c in model.Grid.Cards)
            {
                var b = new ShakeBody { Card = c, X = c.Col + 0.5f, Y = c.Row + 0.5f, Fixed = guarded.Contains(c.Id) };
                if (!b.Fixed && c.Kind == CardKind.Jamo) b.Rotation = model.BeginRotation(c);
                _bodies.Add(b);
            }
            if (!_bodies.Any(b => !b.Fixed)) Finished = true;   // 흔들 카드가 없다
        }

        /// <summary>
        /// 부딪힌 두 카드를 조립한다(합칠 수 있고 확률을 통과했을 때). 한쪽 카드가 사라지고(<see cref="ShakeBody.Dead"/>) 다른 카드가 남는다.
        /// 움직이지 않는 카드(완성된 단어)나 이미 사라진 카드는 조립하지 않는다. 조립했으면 true.
        /// </summary>
        public bool TryMerge(ShakeBody a, ShakeBody b)
        {
            if (Finished || a == b || a.Fixed || b.Fixed || a.Dead || b.Dead || _s.MergeChance <= 0f) return false;

            ShakeBody dragged, target;
            if (CardOps.TryCombine(a.Card, b.Card, false, out _) == CombineOutcome.Ok) { dragged = a; target = b; }
            else if (CardOps.TryCombine(b.Card, a.Card, false, out _) == CombineOutcome.Ok) { dragged = b; target = a; }
            else return false;

            if (_rng.NextDouble() >= _s.MergeChance) return false;
            if (_model.TryMerge(dragged.Card, target.Card) != CombineOutcome.Ok) return false;

            dragged.Dead = true;
            target.AngleBase = target.Angle;   // 새 카드는 지금 각도를 "똑바로"로 보고 거기서부터 다시 돈다
            target.Rotation = target.Card.Kind == CardKind.Jamo ? _model.BeginRotation(target.Card) : null;
            Merged++;
            return true;
        }

        /// <summary>
        /// 카드의 지금 위치(중심, 칸)와 누적 회전각(시계방향 도)을 알려 준다. 90° 경계를 넘으면 자모가 바뀐다.
        /// </summary>
        public void SetPose(ShakeBody b, float x, float y, float angleDeg)
        {
            if (Finished || b.Fixed || b.Dead) return;
            b.X = x; b.Y = y; b.Angle = angleDeg;
            if (b.Rotation == null) return;

            int steps = (int)Math.Round((angleDeg - b.AngleBase) / 90f);
            if (steps != b.Rotation.Steps) _model.ApplyRotation(b.Card, b.Rotation, steps);
        }

        /// <summary>
        /// 흔들기를 끝낸다: 도는 중이던 자모를 가장 가까운 90° 모양으로 확정하고, 카드를 가장 가까운 빈 칸에 맞춘다.
        /// 아직 끝나지 않았어도 부르면 그 자리에서 멈춘다(모드가 바뀌었을 때 등).
        /// </summary>
        public void Finish()
        {
            if (Finished) return;
            Finished = true;

            foreach (var b in _bodies)
                if (!b.Fixed && !b.Dead && b.Rotation != null) _model.FinishRotation(b.Card, b.Rotation);

            _model.ApplyLayout(ComputeLayout());
        }

        // 흔들던 카드마다 자기 위치에서 가장 가까운 빈 칸(중심 거리 기준)을 정한다. 자기 칸 중심에 가까운 카드부터 고른다.
        List<(Card card, int col, int row)> ComputeLayout()
        {
            var dyn = _bodies.Where(b => !b.Fixed && !b.Dead).ToList();
            var moving = new HashSet<Card>(dyn.Select(b => b.Card));
            var taken = new HashSet<(int, int)>();
            foreach (var c in _model.Grid.Cards)
                if (!moving.Contains(c)) taken.Add((c.Col, c.Row));   // 움직이지 않는 카드(완성된 단어 등)의 칸

            (int col, int row) Desired(ShakeBody b)
                => (GridModel.Clamp((int)Math.Floor(b.X), 0, _cols - 1), GridModel.Clamp((int)Math.Floor(b.Y), 0, _rows - 1));

            float Dist2(ShakeBody b, int col, int row)
            {
                float dx = b.X - (col + 0.5f), dy = b.Y - (row + 0.5f);
                return dx * dx + dy * dy;
            }

            var result = new List<(Card, int, int)>();
            foreach (var b in dyn.OrderBy(b => { var (c, r) = Desired(b); return Dist2(b, c, r); }).ToList())
            {
                var want = Desired(b);
                if (taken.Add(want)) { result.Add((b.Card, want.col, want.row)); continue; }

                float best = float.MaxValue;
                (int col, int row) cell = (-1, -1);
                for (int r = 0; r < _rows; r++)
                    for (int c = 0; c < _cols; c++)
                    {
                        if (taken.Contains((c, r))) continue;
                        float d = Dist2(b, c, r);
                        if (d < best) { best = d; cell = (c, r); }
                    }
                if (cell.col < 0) continue;   // 빈 칸이 없다(있을 수 없는 경우)
                taken.Add(cell);
                result.Add((b.Card, cell.col, cell.row));
            }
            return result;
        }
    }
}
