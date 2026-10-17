using System;
using System.Collections.Generic;
using Hangeul.App;
using Hangeul.Game.Feedback;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Hangeul.Game.Combat
{
    /// <summary>쏘는 쪽이 정하는 발사 한 번의 내용. <see cref="Projectile.Spawn"/>에 넘긴다.</summary>
    public struct ProjectileShot
    {
        /// <summary>발사 위치(월드). 쏘는 쪽의 몸 가운데쯤.</summary>
        public Vector2 Origin;
        /// <summary>날아가는 방향(길이는 상관없다).</summary>
        public Vector2 Direction;
        /// <summary>쏘는 쪽의 몸에서 이만큼 앞에서부터 날아간다(그 사이에 벽이 있으면 바로 막힌다).</summary>
        public float LeadIn;
        /// <summary>맞는 것을 가리는 둥근 판정의 반지름(유니티 단위).</summary>
        public float Radius;
        /// <summary>누구 편의 투사체인가(같은 편은 지나간다).</summary>
        public Team Team;
        public int Damage;
        public float Knockback;
        public float StunSeconds;
        /// <summary>맞을 때 굴리는 효과들(속성 묻히기). 비어 있어도 된다.</summary>
        public IReadOnlyList<WeaponModifier> Modifiers;
        /// <summary>쏜 오브젝트(자기 자신은 맞지 않는다).</summary>
        public GameObject Source;
        /// <summary>
        /// 날아가는 모양을 담은 프리팹(그림, 애니메이션, 꼬리 효과 등). 프리팹의 오른쪽(+X)이 날아가는 방향이 되도록 돌려서 날린다.
        /// <see cref="Projectile"/> 컴포넌트가 없으면 불러올 때 붙인다. 비어 있으면 임시 화살 그림.
        /// </summary>
        public GameObject Prefab;
        /// <summary>그림을 그릴 정렬 레이어(<c>SpriteRenderer.sortingLayerID</c>). 기본값 0은 Default 레이어로, 이 프로젝트에서는 땅 아래에 가려진다 —
        /// 보통 쏘는 쪽 그림의 레이어를 넘긴다.</summary>
        public int SortingLayerId;
        /// <summary>정렬 레이어 안에서의 순서(클수록 위).</summary>
        public int SortingOrder;
    }

    /// <summary>
    /// 날아가서 처음 닿은 것에 피해를 주고 사라지는 투사체(화살). 풀링한다 — <see cref="Spawn"/>이 쉬고 있는 것을 다시 쓰고 없으면 새로 만든다.
    /// 콜라이더·리지드바디 없이 매 프레임 앞쪽을 원으로 훑어(<c>Physics2D.CircleCast</c>)
    /// 가장 가까운 것부터 처리하므로 빨라도 얇은 벽을 뚫지 않는다. 벽(콜라이더가 있는 것)에 닿으면 사라지고,
    /// 피해를 받을 수 있는 다른 편 대상(<see cref="IDamageable"/>)에 닿으면 피해를 주고 사라진다. 줍기·체크포인트 같은 트리거 구역과 같은 편, 이미 죽은 대상은 지나간다.
    /// 맞은 대상에는 속성이 확률로 묻는다(<c>WeaponModifier.RollElement</c>) — 묻으면 머리 위에 "화염!"이 뜬다.
    /// 시간은 <c>Time.deltaTime</c> 기준이라 인벤토리를 열어 게임이 멈추면 같이 멈춘다.
    /// 모양은 <see cref="ProjectileShot.Prefab"/>이다: 프리팹을 인스턴스로 만들어 쓰고(프리팹마다 따로 풀링), 프리팹 루트의 <b>오른쪽(+X)이 날아가는 방향</b>이 되게 돌린다.
    /// 그림이 위나 아래를 향하게 그려져 있으면 프리팹 안에서 그림이 든 자식 오브젝트를 돌려 맞추면 된다.
    /// <b>이동 속도와 사거리는 이 컴포넌트를 프리팹 루트에 붙여 거기서 정한다</b>(화살은 모두 같으므로 무기마다 따로 두지 않는다). 안 붙이면 불러올 때 기본값(속도 9, 사거리 6)으로 붙는다.
    /// 프리팹 에셋의 값을 고치면 이미 만들어 둔 화살에도 바로 반영된다(플레이 중에 고르며 맞출 수 있다).
    /// 프리팹에 콜라이더나 리지드바디는 필요 없다(맞는 것은 코드가 훑어서 정한다).
    /// </summary>
    [AddComponentMenu("Hangeul/Projectile")]
    public sealed class Projectile : MonoBehaviour
    {
        const int MaxHits = 16;
        static readonly RaycastHit2D[] Hits = new RaycastHit2D[MaxHits];
        static readonly HitDistance Order = new HitDistance();
        static readonly Func<double> RollSource = () => Random.value;
        const int DefaultKey = 0;   // 프리팹이 없는 임시 화살의 풀 이름표
        static readonly Dictionary<int, Stack<Projectile>> Pools = new Dictionary<int, Stack<Projectile>>();
        static Transform s_root;
        static Sprite s_defaultSprite;

        // 에디터에서 도메인 다시 불러오기를 끄고 실행해도 지난 실행의 것을 붙잡지 않도록
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Pools.Clear();
            s_root = null;
            s_defaultSprite = null;
        }

        sealed class HitDistance : IComparer<RaycastHit2D>
        {
            public int Compare(RaycastHit2D a, RaycastHit2D b) => a.distance.CompareTo(b.distance);
        }

        [Header("비행")]
        [Tooltip("날아가는 속도(유니티 단위/초).")]
        [SerializeField, Min(0.1f)] float speed = 9f;
        [Tooltip("날아갈 수 있는 최대 거리(유니티 단위). 이만큼 가면 사라진다.")]
        [SerializeField, Min(0.1f)] float range = 6f;

        [Header("모양")]
        [Tooltip("켜면 프리팹 루트의 오른쪽(+X)이 날아가는 방향이 되도록 돌려서 날린다. 끄면 돌리지 않는다(둥근 탄 등).")]
        [SerializeField] bool rotateToDirection = true;
        [Tooltip("그림이 오른쪽이 아닌 쪽을 향하게 그려져 있을 때 맞추는 각도(도). 예) 그림이 위를 향하면 -90. 프리팹 안에서 자식을 돌려 맞춰도 된다.")]
        [SerializeField] float angleOffset;
        [Tooltip("켜면 프리팹 안의 SpriteRenderer를 쏜 사람 그림의 정렬 레이어와 순서에 맞춘다(프리팹이 Default 레이어면 땅 아래에 가려지므로). 프리팹 안의 상대적인 순서는 유지한다. 끄면 프리팹에 정한 값 그대로.")]
        [SerializeField] bool matchShooterSorting = true;

        Projectile _template;   // 이 화살을 만든 프리팹 에셋의 컴포넌트(있으면 속도·사거리를 거기서 읽는다)
        SpriteRenderer[] _renderers;
        int[] _authoredOrders;   // 프리팹에 정해 둔 순서(상대적인 순서를 지키는 데 쓴다)
        int _poolKey = DefaultKey;
        ProjectileShot _shot;
        Vector2 _dir;
        float _remaining;
        int _mask;
        bool _flying;

        /// <summary>투사체를 쏜다. 쉬고 있는 것이 있으면 다시 쓴다.</summary>
        public static Projectile Spawn(in ProjectileShot shot)
        {
            int key = shot.Prefab != null ? shot.Prefab.GetInstanceID() : DefaultKey;
            Projectile p = null;
            if (Pools.TryGetValue(key, out var pool))
                while (p == null && pool.Count > 0) p = pool.Pop();   // 장면이 바뀌어 없어진 것은 건너뛴다
            if (p == null) p = Create(shot.Prefab, key);
            p.Launch(shot);
            return p;
        }

        static Projectile Create(GameObject prefab, int key)
        {
            if (s_root == null) s_root = new GameObject("Projectiles").transform;

            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, s_root, false);
            }
            else
            {
                go = new GameObject("Arrow");   // 프리팹이 없으면 임시 화살
                go.transform.SetParent(s_root, false);
                go.AddComponent<SpriteRenderer>().sprite = DefaultSprite();
            }

            var projectile = go.GetComponent<Projectile>();
            if (projectile == null) projectile = go.AddComponent<Projectile>();   // 프리팹에 컴포넌트가 없으면 기본값으로 붙인다
            projectile._poolKey = key;
            projectile._template = prefab != null ? prefab.GetComponent<Projectile>() : null;
            if (prefab != null && projectile._template == null)
                Debug.LogWarning($"[Projectile] 화살 프리팹 '{prefab.name}'에 Projectile 컴포넌트가 없어 기본 속도 {projectile.speed}, 사거리 {projectile.range}로 날립니다. 프리팹 루트에 Projectile을 붙이면 속도와 사거리를 정할 수 있습니다.", prefab);
            projectile.CollectRenderers();
            return projectile;
        }

        void CollectRenderers()
        {
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            _authoredOrders = new int[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++) _authoredOrders[i] = _renderers[i].sortingOrder;
        }

        // 프리팹 안의 그림들을 쏜 사람의 정렬 레이어에 맞춘다. 프리팹 안에서의 위아래 순서(가장 낮은 것이 기준)는 그대로 둔다.
        void ApplySorting(in ProjectileShot shot)
        {
            if (_renderers == null || _renderers.Length == 0) return;
            int lowest = int.MaxValue;
            foreach (int order in _authoredOrders) lowest = Mathf.Min(lowest, order);
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].sortingLayerID = shot.SortingLayerId;
                _renderers[i].sortingOrder = shot.SortingOrder + (_authoredOrders[i] - lowest);
            }
        }

        void Launch(in ProjectileShot shot)
        {
            _shot = shot;
            _dir = shot.Direction.sqrMagnitude > 1e-6f ? shot.Direction.normalized : Vector2.right;
            _remaining = Mathf.Max(0.01f, Range);
            _mask = CombatLayers.TargetMask(shot.Team);

            if (_renderers == null) CollectRenderers();
            transform.position = shot.Origin;
            transform.rotation = rotateToDirection
                ? Quaternion.Euler(0f, 0f, Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg + angleOffset)   // 프리팹의 오른쪽이 날아가는 방향
                : Quaternion.identity;
            if (matchShooterSorting) ApplySorting(shot);
            gameObject.SetActive(true);
            _flying = true;

            if (shot.LeadIn > 0f) Advance(shot.LeadIn);   // 몸에서 앞으로 나온 만큼은 바로 간다(그 사이의 벽도 확인한다)
        }

        void Update()
        {
            if (!_flying) return;
            Advance(Speed * Time.deltaTime);
        }

        // 프리팹 에셋의 컴포넌트가 있으면 그 값(고치면 바로 반영), 없으면 이 인스턴스의 값
        float Speed => Mathf.Max(0.1f, _template != null ? _template.speed : speed);
        float Range => Mathf.Max(0.1f, _template != null ? _template.range : range);

        // distance만큼 앞으로 간다. 도중에 닿는 것이 있으면 그 자리에서 처리하고 끝낸다.
        void Advance(float distance)
        {
            float step = Mathf.Min(distance, _remaining);
            if (step <= 0f) return;

            Vector2 from = transform.position;
            if (TryHit(from, step, out var point, out var target))
            {
                transform.position = point;
                Land(target);
                return;
            }

            transform.position = from + _dir * step;
            _remaining -= step;
            if (_remaining <= 0f) Release();
        }

        // 앞쪽을 훑어 처음 막는 것을 찾는다. 대상이면 target에, 벽이면 target은 null.
        bool TryHit(Vector2 from, float distance, out Vector2 point, out IDamageable target)
        {
            point = from;
            target = null;

            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true };
            filter.SetLayerMask(_mask);
            int count = Physics2D.CircleCast(from, _shot.Radius, _dir, filter, Hits, distance);
            if (count <= 0) return false;
            if (count > 1) Array.Sort(Hits, 0, count, Order);   // 가까운 것부터

            for (int i = 0; i < count; i++)
            {
                var col = Hits[i].collider;
                if (col == null) continue;

                var damageable = col.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    if (!Hurtbox.Accepts(damageable, col)) continue;   // 피격 판정이 따로 있는 대상의 벽 충돌용 몸통은 벽도 대상도 아니라 지나간다
                    if (PassesThrough(damageable)) continue;
                    point = Hits[i].centroid;
                    target = damageable;
                    return true;
                }

                if (col.isTrigger) continue;   // 줍기·체크포인트 같은 구역은 지나간다
                point = Hits[i].centroid;      // 벽
                return true;
            }
            return false;
        }

        // 쏜 사람 자신, 같은 편, 이미 죽은 대상은 지나간다
        bool PassesThrough(IDamageable target)
        {
            if (target is Component c && c.gameObject == _shot.Source) return true;
            if (target.Team != Team.Neutral && target.Team == _shot.Team) return true;
            return target is Health health && health.IsDead;
        }

        void Land(IDamageable target)
        {
            if (target != null)
            {
                // 속성 묻히기: 맞은 대상에게 확률을 굴린다. 묻으면 피해 정보에 속성이 실리고 대상 머리 위에 "화염!"이 뜬다
                var landed = WeaponModifier.RollElement(_shot.Modifiers, RollSource);
                string element = landed?.Element;
                var info = new DamageInfo(_shot.Damage, _shot.Team, _dir * _shot.Knockback, _shot.StunSeconds, element, _shot.Source);
                if (target.TakeDamage(info) && element != null && target is Component hit)
                    PopupText.Spawn(hit.transform, element + "!", PopupText.ElementColor(landed));
            }
            Release();
        }

        void Release()
        {
            _flying = false;
            gameObject.SetActive(false);
            if (!Pools.TryGetValue(_poolKey, out var pool)) Pools[_poolKey] = pool = new Stack<Projectile>();
            pool.Push(this);
        }

        // ───── 임시 화살 그림 ─────
        // 그림이 들어오기 전까지 보이는 28×7 픽셀 화살(오른쪽을 향함): 깃, 대, 촉.
        static Sprite DefaultSprite()
        {
            if (s_defaultSprite != null) return s_defaultSprite;

            const int W = 28, H = 7;
            var px = new Color32[W * H];
            void Set(int x, int y, Color32 c) => px[y * W + x] = c;

            var shaft = new Color32(168, 120, 72, 255);
            var head = new Color32(222, 228, 235, 255);
            var feather = new Color32(214, 76, 76, 255);

            for (int x = 2; x <= 23; x++) Set(x, 3, shaft);
            for (int y = 1; y <= 5; y++) { Set(22, y, head); Set(23, y, head); }
            for (int y = 2; y <= 4; y++) { Set(24, y, head); Set(25, y, head); }
            Set(26, 3, head);
            Set(27, 3, head);
            Set(0, 1, feather); Set(1, 2, feather); Set(2, 2, feather);   // 깃
            Set(0, 5, feather); Set(1, 4, feather); Set(2, 4, feather);

            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s_defaultSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
            s_defaultSprite.hideFlags = HideFlags.HideAndDontSave;
            return s_defaultSprite;
        }
    }
}
