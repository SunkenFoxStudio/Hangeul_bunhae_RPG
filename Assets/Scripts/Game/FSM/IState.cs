namespace Hangeul.Game.FSM
{
    /// <summary>상태 머신이 다루는 상태. 소유자(캐릭터 등)는 구현체가 생성자로 받아 둔다.</summary>
    public interface IState
    {
        void Enter();
        void Tick(float deltaTime);
        void FixedTick(float fixedDeltaTime);
        void Exit();
    }
}
