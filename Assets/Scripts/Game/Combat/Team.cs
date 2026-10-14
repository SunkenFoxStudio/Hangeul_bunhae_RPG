namespace Hangeul.Game.Combat
{
    /// <summary>누구 편인지. 같은 편끼리는 피해를 주고받지 않는다(Neutral은 누구에게나 맞는다: 부술 수 있는 물건 등).</summary>
    public enum Team
    {
        Neutral,
        Player,
        Enemy,
    }
}
