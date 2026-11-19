using System;

namespace Shooter.Runtime
{
    // 部位ごとの部品の型を NetV2 に見せないのは、部位を持つ乗り物を他のアセンブリへ出しても NetV2 が参照を足さずに済むようにするため。
    public interface INetVehicleParts
    {
        void Detach();

        // 部位ごとの量を一覧で渡さず読む関数にするのは、NetV2 の持ち方 (部位の Id ごとの数) をこの口へ写し替えずに済むようにするため。
        void ApplyServerTaken(Func<string, int> takenOf);
    }
}
