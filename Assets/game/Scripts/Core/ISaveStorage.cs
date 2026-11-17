namespace Shooter.Core
{
    // 差し込み口にするのは、Core はファイルに触れず、実際の読み書きを Runtime 側で実装するため。
    public interface ISaveStorage
    {
        bool TryRead(string slot, out string text);

        // 実装は、途中で失敗しても前の内容が壊れないように書く。
        void Write(string slot, string text);

        void Delete(string slot);
    }
}
