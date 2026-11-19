namespace Shooter.Runtime
{
    public interface IBuySender
    {
        // 値段を渡さないのは、値段と買えるかどうかはサーバーが自分のデータで決めるため。
        // 答えを返さないのは、ネット対戦では答えが後から届くため。答えは CombatEvents.PurchaseAnswered で知らせる。
        void Buy(string itemId);
    }
}
