using System.Collections.Generic;
using UnityEngine;

namespace Hangeul.Game.Combat
{
    /// <summary>
    /// 피격 판정: 공격(적의 몸통 접촉, 투사체, 근접 베기)이 "맞는" 영역. 벽에 부딪히는 몸통 콜라이더(보통 발밑의 납작한 상자)와 따로
    /// 몸 모양에 맞춘 Trigger 콜라이더를 둘 수 있게 한다 — 이 컴포넌트가 붙은 콜라이더(자식 오브젝트에 두고 <c>Is Trigger</c>를 켠다)에만 맞고,
    /// 같은 대상의 다른 콜라이더(벽 충돌용 몸통)에는 맞지 않는다. 대상(<see cref="IDamageable"/>, 보통 <see cref="Health"/>)의 자식이어야 한다.
    /// 대상에 이 컴포넌트가 하나도 없으면 예전처럼 대상의 모든 콜라이더에 맞는다(그대로 동작).
    /// 플레이어는 <c>Hangeul ▸ Setup ▸ Player Hurtbox</c> 메뉴가 만들어 준다. 크기와 위치는 Scene 뷰에서 콜라이더를 보며 조절한다.
    /// </summary>
    [AddComponentMenu("Hangeul/Hurtbox")]
    [RequireComponent(typeof(Collider2D))]
    public sealed class Hurtbox : MonoBehaviour
    {
        // 피격 판정이 따로 있는 대상들(켜져 있는 동안만). 공격이 맞은 콜라이더를 걸러 낼 때 O(1)로 확인한다.
        static readonly HashSet<IDamageable> WithHurtbox = new HashSet<IDamageable>();

        // 에디터에서 도메인 다시 불러오기를 끄고 실행해도 지난 실행의 것을 붙잡지 않도록
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => WithHurtbox.Clear();

        IDamageable _owner;
        Collider2D _collider;

        /// <summary>이 피격 판정의 콜라이더.</summary>
        public Collider2D Collider => _collider != null ? _collider : (_collider = GetComponent<Collider2D>());

        void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        void OnEnable() => Register();

        // 대상의 Health가 실행 중에 붙는 경우(플레이어가 자동으로 붙인다)를 위해 모든 Awake·OnEnable이 끝난 Start에서 한 번 더 찾는다
        void Start()
        {
            if (_owner == null && !Register())
                Debug.LogWarning("[Hurtbox] 부모에서 Health 같은 IDamageable을 찾지 못했습니다. 피격 판정은 대상의 자식 오브젝트에 두세요.", this);
        }

        bool Register()
        {
            _owner = GetComponentInParent<IDamageable>();
            if (_owner == null) return false;
            WithHurtbox.Add(_owner);
            return true;
        }

        void OnDisable()
        {
            if (_owner != null && !HasAnotherEnabled(_owner)) WithHurtbox.Remove(_owner);
        }

        // 한 대상에 피격 판정이 여러 개일 때, 하나를 꺼도 다른 것이 켜져 있으면 계속 "피격 판정이 따로 있는 대상"이다
        bool HasAnotherEnabled(IDamageable owner)
        {
            if (!(owner is Component c)) return false;
            foreach (var other in c.GetComponentsInChildren<Hurtbox>())
                if (other != this && other.isActiveAndEnabled) return true;
            return false;
        }

        /// <summary>
        /// 이 콜라이더에 맞은 공격이 target을 때려도 되는지. 대상에 피격 판정이 따로 없으면 어느 콜라이더든 되고,
        /// 있으면 피격 판정 콜라이더(<see cref="Hurtbox"/>가 붙은 것)만 된다. 공격하는 쪽이 맞은 콜라이더마다 부른다.
        /// </summary>
        public static bool Accepts(IDamageable target, Collider2D collider)
        {
            if (target == null || collider == null) return false;
            if (!WithHurtbox.Contains(target)) return true;
            return collider.GetComponent<Hurtbox>() != null;
        }
    }
}
