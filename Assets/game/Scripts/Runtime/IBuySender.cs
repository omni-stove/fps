namespace Shooter.Runtime
{
    public interface IBuySender
    {
        // 値段を渡さないのは、値段と買えるかどうかはサーバーが自分のデータで決めるため。
        void Buy(string itemId);
    }
}
