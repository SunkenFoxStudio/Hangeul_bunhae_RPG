using UnityEngine;

namespace Hangeul.UI
{
    /// <summary>
    /// 흔들기 물리 월드의 카드 몸체에 붙어, 다른 카드와 새로 부딪혔을 때 <see cref="ShakeWorld"/>에 알린다.
    /// (Unity는 충돌 콜백을 이 컴포넌트가 붙은 오브젝트로만 보내므로 별도 컴포넌트가 필요하다.)
    /// </summary>
    public sealed class ShakeContact : MonoBehaviour
    {
        internal ShakeWorld World;
        internal int CardId;

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (World == null) return;
            var other = collision.collider.GetComponentInParent<ShakeContact>();
            if (other != null) World.OnContact(CardId, other.CardId);   // 양쪽에서 호출되므로 World가 중복을 걸러낸다
        }
    }
}
