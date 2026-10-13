using System;

namespace Hangeul.Game.FSM
{
    /// <summary>
    /// 상태 하나만 활성화하는 단순 FSM. 상태 전이는 상태 안에서 <see cref="ChangeState"/>를 호출해 요청한다.
    /// Enter/Exit 도중 다시 전이를 요청해도 안전하다(대기열에 넣었다가 현재 전이가 끝난 뒤 처리).
    /// </summary>
    public sealed class StateMachine
    {
        public IState Current { get; private set; }
        public IState Previous { get; private set; }

        /// <summary>(이전 상태, 새 상태). 이전 상태는 최초 진입 시 null.</summary>
        public event Action<IState, IState> StateChanged;

        bool _changing;
        IState _pending;

        public void Initialize(IState first) => ChangeState(first);

        public void ChangeState(IState next)
        {
            if (next == null || (next == Current && !_changing)) return;
            if (_changing) { _pending = next; return; }

            _changing = true;
            while (next != null)
            {
                var old = Current;
                old?.Exit();
                Previous = old;
                Current = next;
                next = null;
                Current.Enter();
                StateChanged?.Invoke(old, Current);

                if (_pending != null)
                {
                    next = _pending == Current ? null : _pending;
                    _pending = null;
                }
            }
            _changing = false;
        }

        public void Tick(float deltaTime) => Current?.Tick(deltaTime);
        public void FixedTick(float fixedDeltaTime) => Current?.FixedTick(fixedDeltaTime);
    }
}
