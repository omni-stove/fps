using System.Numerics;

namespace Shooter.Server
{
    // ルールのモジュールは置ける物のモジュールより先に作られ、置ける物の側がルールを受け取るので、ビーコンを引く口は置ける物の側から差し込む
    // (戦闘の当たりに置いた物を渡す ServerCombat.Deployables と同じつなぎ方)。
    internal interface IServerRespawnBeacons
    {
        // 使えるビーコンがあれば 1 回分を使い、そのビーコンの前の足元の位置と向きを返す。使い切ったビーコンはここで消える
        bool TryUseBeacon(int playerId, out Vector3 position, out float yaw);
    }
}
