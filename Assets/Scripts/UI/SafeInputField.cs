using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hangeul.UI
{
    /// <summary>
    /// 유니티 <see cref="InputField"/>의 알려진 문제를 피하는 입력칸.
    /// 칸에 포커스가 가면 기존 글자가 전부 선택되는데, 이때 한글 입력기(IME)가 조합 중인 글자가 있으면 InputField가
    /// 글자 수를 넘는 위치로 문자열을 잘라 내려다 <c>ArgumentOutOfRangeException</c>(Substring)을 낸다
    /// (글자가 들어 있는 숫자칸에 한글을 연타하면 콘솔에 에러가 쌓인다). 게임이 멈추지는 않지만 입력이 사라지고 에러가 난다.
    /// <list type="bullet">
    /// <item><see cref="NumbersOnly"/>: 숫자칸은 입력기를 꺼서 조합 자체가 시작되지 않게 하고, 숫자 모양이 아닌 글자는 받지 않는다.</item>
    /// <item>그 밖의 칸: 같은 예외가 나면 그 글자만 버리고 넘어간다(에러 로그를 남기지 않는다).</item>
    /// </list>
    /// </summary>
    public sealed class SafeInputField : InputField
    {
        /// <summary>숫자(0-9, 소수점, 부호)만 받는 칸. 입력기를 꺼서 한글이 조합되지 않는다.</summary>
        public bool NumbersOnly;

        public override void OnUpdateSelected(BaseEventData eventData)
        {
            if (NumbersOnly && isFocused) DisableIme();
            base.OnUpdateSelected(eventData);
        }

        protected override void Append(char input)
        {
            if (NumbersOnly && !IsNumberChar(input)) return;
            try { base.Append(input); }
            catch (System.ArgumentOutOfRangeException) { /* 위 설명의 알려진 문제: 이 글자만 버린다 */ }
        }

        static bool IsNumberChar(char c) => (c >= '0' && c <= '9') || c == '.' || c == ',' || c == '-';

        // InputField가 포커스를 줄 때마다 입력기를 켜므로(IMECompositionMode.On), 숫자칸에서는 매번 다시 끈다. 칸을 벗어나면 InputField가 알아서 되돌린다.
        static void DisableIme()
        {
            var module = EventSystem.current != null ? EventSystem.current.currentInputModule : null;
            var input = module != null ? module.input : null;
            if (input != null && input.imeCompositionMode != IMECompositionMode.Off) input.imeCompositionMode = IMECompositionMode.Off;
        }
    }
}
