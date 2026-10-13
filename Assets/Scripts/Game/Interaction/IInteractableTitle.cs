namespace Hangeul.Game.Interaction
{
    /// <summary>
    /// 상호작용 안내에 제목(예: 아이템 단어)을 같이 띄우고 싶은 대상이 <see cref="IInteractable"/>과 함께 구현한다.
    /// 안내 UI는 Title을 크게, Prompt(동사)를 "[E] 줍기"처럼 키 이름과 함께 작게 보여준다. 구현하지 않으면 "[E] Prompt"만 보인다.
    /// </summary>
    public interface IInteractableTitle
    {
        string Title { get; }
    }
}
