using System;

namespace Hangeul.Game.Interaction
{
    /// <summary>상호작용 결과를 화면에 알리는 가벼운 통로. 대상이 Show로 문구를 보내면 안내 UI가 잠깐 띄운다.</summary>
    public static class InteractionFeedback
    {
        public static event Action<string> Message;

        public static void Show(string message) => Message?.Invoke(message);
    }
}
