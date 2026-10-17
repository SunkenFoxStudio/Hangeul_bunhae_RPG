using Hangeul.App;
using Hangeul.Game.Combat;
using UnityEngine;

namespace Hangeul.Game.Character
{
    /// <summary>
    /// 플레이어의 원거리 공격(활). 장착한 무기 단어의 공격 방식이 원거리일 때 <see cref="PlayerAttackState"/>가 시위를 당긴 뒤(준비 시간)
    /// <see cref="Fire"/>를 불러 화살(<see cref="Projectile"/>) 하나를 쏜다. 바라보는(공격하는) 4방향 그대로 곧게 날아간다.
    /// 피해량·밀어내기·경직·시간(시위·쉬는 시간)은 장착한 활 단어의 수치(DB 관리 씬이나 게임 데이터 에셋에서 정한다)에서 오고, 화살의 속도·사거리는 화살 프리팹의 <see cref="Projectile"/> 컴포넌트에서 오며,
    /// 활에 붙은 접두어('강화활')와 쏘는 화살에 붙은 접두어('불화살')의 효과가 겹쳐 적용된다 — 피해량은 활 → 화살 순서로 더하거나 곱하고,
    /// 속성 묻히기는 맞을 때 각자 확률을 굴린다. 어떤 화살을 쏠지는 <see cref="PlayerItemUser"/>가 가방에서 고른다.
    /// 근접 공격(<see cref="PlayerMeleeAttack"/>)과 달리 수치를 따로 정하지 않은 무기는 원거리가 아니다(이 컴포넌트에는 기본 수치가 없다).
    /// 평소에는 코드가 자동으로 붙이고, 직접 붙이면 아래 값을 인스펙터에서 조절할 수 있다.
    /// </summary>
    [AddComponentMenu("Hangeul/Player Ranged Attack")]
    public sealed class PlayerRangedAttack : MonoBehaviour
    {
        [Header("발사")]
        [Tooltip("화살이 나오는 자리를 몸 한가운데에서 더 옮기고 싶을 때(월드 단위). 0이면 정확히 한가운데. 몸 한가운데는 피격 판정(Hurtbox)의 중심이고, 없으면 플레이어 그림의 중심이다.")]
        [SerializeField] Vector2 centerOffset;
        [Tooltip("화살이 무엇에 맞는지 가리는 둥근 판정의 반지름(유니티 단위).")]
        [SerializeField, Min(0.01f)] float arrowRadius = 0.06f;

        [Header("그래픽 (비어 있어도 된다)")]
        [Tooltip("화살 프리팹. 프리팹의 오른쪽(+X)이 날아가는 방향이 되도록 돌려서 날린다(그림이 다른 쪽을 향하면 프리팹 안에서 그림이 든 자식을 돌려 맞춘다). 비워 두면 임시 화살이 나온다.")]
        [SerializeField] GameObject arrowPrefab;
        [Tooltip("화살은 플레이어 그림과 같은 정렬 레이어에, 이 값만큼 위의 순서로 그린다(같은 레이어의 적·몸 위로 보이게). 프리팹의 Projectile 컴포넌트에서 Match Shooter Sorting을 끄면 프리팹에 정한 값을 쓴다.")]
        [SerializeField] int sortingOrderAboveBody = 2;

        SpriteRenderer _body;       // 플레이어 그림(화살의 정렬 레이어·순서를 맞출 기준)
        bool _bodySearched;
        Hurtbox _hurtbox;           // 피격 판정(몸 한가운데의 기준)
        bool _hurtboxSearched;

        // 장착한 활의 수치(원거리일 때만). null이면 원거리 무기가 아니다.
        ItemStats _bow;
        // 활 자신에게 붙은 접두어의 효과(없으면 null)
        WeaponModifier _bowModifier;

        /// <summary>
        /// 장착한 무기를 알린다. stats가 원거리 수치면 활이 되고, 아니면(null 포함) 원거리 무기가 아니다. modifier는 무기 단어에 붙은 접두어 효과.
        /// </summary>
        public void UseWeapon(ItemStats stats, WeaponModifier modifier)
        {
            bool ranged = stats != null && stats.attackStyle == AttackStyle.Ranged;
            _bow = ranged ? stats : null;
            _bowModifier = ranged ? modifier : null;
        }

        /// <summary>지금 원거리 무기(활)를 장착하고 있는지.</summary>
        public bool IsBow => _bow != null;

        /// <summary>
        /// 활 한 번의 시간표: 준비(시위를 당김) → 쏘는 순간 → 마무리(쏜 뒤 쉼). 화살은 준비가 끝나는 순간 나가고,
        /// 근접의 "판정 시간"은 쓰지 않는다. 활이 아니면 기본 시간표.
        /// </summary>
        public AttackTimeline Timeline => _bow != null ? new AttackTimeline(_bow.windup, 0f, _bow.recovery) : new AttackTimeline(0.08f, 0f, 0.18f);

        /// <summary>
        /// 화살 하나를 facing 방향으로 쏜다. arrowModifier는 쏘는 화살에 붙은 접두어의 효과(없으면 null).
        /// 활을 장착하고 있지 않으면 아무것도 하지 않고 false.
        /// </summary>
        public bool Fire(Facing facing, WeaponModifier arrowModifier)
        {
            if (_bow == null) return false;

            var modifiers = new[] { _bowModifier, arrowModifier };   // 활 → 화살 순서
            Vector2 dir = FacingUtil.ToVector(facing);
            var body = Body();
            var shot = new ProjectileShot
            {
                Origin = BodyCenter() + centerOffset,
                Direction = dir,
                Radius = arrowRadius,
                Team = Team.Player,
                Damage = WeaponModifier.ApplyDamage(_bow.damage, modifiers),
                Knockback = _bow.knockback,
                StunSeconds = _bow.stun,
                Modifiers = modifiers,
                Source = gameObject,
                Prefab = arrowPrefab,
                // 땅(Grounds)보다 위, 플레이어와 같은 레이어에 그린다. 플레이어 그림을 못 찾으면 Default 레이어(땅 아래라 안 보일 수 있다).
                SortingLayerId = body != null ? body.sortingLayerID : 0,
                SortingOrder = body != null ? body.sortingOrder + sortingOrderAboveBody : sortingOrderAboveBody,
            };
            Projectile.Spawn(shot);
            return true;
        }

        // 몸 한가운데(월드): 피격 판정(Hurtbox)이 있으면 그 중심, 없으면 플레이어 그림의 중심, 그것도 없으면 플레이어 위치.
        // 피격 판정은 움직임·프레임과 상관없이 늘 같은 자리라서 먼저 쓴다.
        Vector2 BodyCenter()
        {
            if (_hurtbox == null && !_hurtboxSearched)
            {
                _hurtboxSearched = true;
                _hurtbox = GetComponentInChildren<Hurtbox>();
            }
            if (_hurtbox != null && _hurtbox.Collider != null) return _hurtbox.Collider.bounds.center;

            var body = Body();
            return body != null ? (Vector2)body.bounds.center : (Vector2)transform.position;
        }

        // 플레이어 그림(공격 이펙트의 것이 아니라). 못 찾으면 null.
        SpriteRenderer Body()
        {
            if (_body == null && !_bodySearched)
            {
                _bodySearched = true;
                var animator = GetComponentInChildren<PlayerSpriteAnimator>();
                _body = animator != null ? animator.GetComponent<SpriteRenderer>() : null;
            }
            return _body;
        }
    }
}
