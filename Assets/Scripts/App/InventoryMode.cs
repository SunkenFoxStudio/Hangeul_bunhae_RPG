namespace Hangeul.App
{
    /// <summary>
    /// 인벤토리 화면 모드. 조립: 카드를 분해·결합·회전·이동해 단어를 만든다.
    /// 가방: 일반 인벤토리처럼 쓴다 — 완성된 단어는 한 덩어리로 보이고 덩어리째 이동하며, 카드 조작(분해·결합·회전)은 막힌다.
    /// 모드는 모델 상태를 바꾸지 않고 화면 표현과 조작 허용 범위만 바꾼다(세이브에는 저장).
    /// </summary>
    public enum InventoryMode
    {
        Assemble,
        Bag,
    }
}
