using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // 誰をいつ戻すかはルールのモジュール (MatchRulesModule) が決め、ここは使えるビーコンを選んで回数を数えることだけを持つ
    // (オフラインの SpawnBeacon と同じ分け方)。
    internal sealed partial class ServerDeployables
    {
        // 新しく置いた物ほど前線に近いとみなし、使える中でいちばん後に置いた物を選ぶ (オフラインの SpawnBeacon.TryFindFor と同じ)
        public bool TryUseBeacon(int playerId, out Vector3 position, out float yaw)
        {
            position = default;
            yaw = 0f;
            for (var i = _beacons.Count - 1; i >= 0; i--)
            {
                var placed = _beacons[i];
                if (placed.Removed || !CanUseBeacon(placed, playerId))
                {
                    continue;
                }

                if (!placed.Beacon.TryUse())
                {
                    return false;
                }

                position = BeaconSpawnPosition(placed);
                yaw = placed.Yaw;
                _log.WriteLine(BeaconLog.Used(
                    ServerDeployableLog.Owner(placed.OwnerId),
                    placed.Type.Id,
                    placed.Id,
                    ServerDeployableLog.Owner(playerId),
                    position,
                    placed.Beacon.UsesLeft,
                    placed.Beacon.Uses));

                var over = placed.Beacon.Over;
                if (over.HasValue)
                {
                    Remove(placed, over.Value);
                }

                return true;
            }

            return false;
        }

        internal int BeaconUsesLeft(int deployableId)
        {
            return _placed.TryGetValue(deployableId, out var placed) && placed.Beacon != null ? placed.Beacon.UsesLeft : 0;
        }

        // 置いた本人はいつでも使える。ほかの人は目的の決める味方 (分隊で戦う目的では分隊、個人戦では誰も味方でない) だけが使える
        private bool CanUseBeacon(Placed placed, int playerId)
        {
            if (placed.OwnerId == playerId)
            {
                return true;
            }

            var session = _rules.Session;
            return session != null && session.HasPlayer(playerId) && session.HasPlayer(placed.OwnerId) && session.AreAllies(placed.OwnerId, playerId);
        }

        // 足元ちょうどに出すとビーコンの箱に体が重なるので、前 (+Z) へ SpawnDistance だけ離す (オフラインの SpawnBeacon.SpawnPose と同じ)
        private static Vector3 BeaconSpawnPosition(Placed placed)
        {
            return placed.Position + (Forward(placed.Yaw) * placed.Type.Beacon.SpawnDistance);
        }
    }
}
