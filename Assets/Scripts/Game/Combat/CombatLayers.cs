using UnityEngine;

namespace Hangeul.Game.Combat
{
    /// <summary>
    /// 전투에 쓰는 물리 레이어 이름과 마스크. 레이어는 <c>Hangeul ▸ Setup ▸ Combat Layers</c> 메뉴가 프로젝트에 추가한다.
    /// 플레이어/적 몸은 각자의 레이어, 공격(투사체 등)은 PlayerAttack/EnemyAttack 레이어에 둔다.
    /// 벽 같은 월드 물체는 Default 레이어 그대로다.
    /// </summary>
    public static class CombatLayers
    {
        public const string Player = "Player";
        public const string Enemy = "Enemy";
        public const string PlayerAttack = "PlayerAttack";
        public const string EnemyAttack = "EnemyAttack";

        public static readonly string[] All = { Player, Enemy, PlayerAttack, EnemyAttack };

        /// <summary>레이어가 프로젝트에 있으면 그 번호, 없으면 0(Default).</summary>
        public static int LayerOf(string name)
        {
            int layer = LayerMask.NameToLayer(name);
            return layer >= 0 ? layer : 0;
        }

        /// <summary>
        /// 이 편의 공격이 맞출 수 있는 몸의 마스크(플레이어 편 → 적, 적 편 → 플레이어).
        /// 부술 수 있는 물건처럼 Default 레이어에 있는 것도 포함한다. 레이어가 아직 없으면 Default만 남는다.
        /// </summary>
        public static int TargetMask(Team attacker)
        {
            switch (attacker)
            {
                case Team.Player: return LayerMask.GetMask(Enemy, "Default");
                case Team.Enemy: return LayerMask.GetMask(Player, "Default");
                default: return LayerMask.GetMask(Player, Enemy, "Default");
            }
        }
    }
}
