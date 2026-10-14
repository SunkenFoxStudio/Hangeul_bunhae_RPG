namespace Hangeul.Game.Combat
{
    /// <summary>피해를 받을 수 있는 것. 공격(근접 판정, 투사체, 함정)은 맞은 콜라이더에서 이것을 찾아 <see cref="TakeDamage"/>를 부른다.</summary>
    public interface IDamageable
    {
        Team Team { get; }

        /// <summary>피해를 준다. 실제로 맞았으면 true(같은 편이거나 무적이거나 이미 죽었으면 false).</summary>
        bool TakeDamage(DamageInfo info);
    }
}
