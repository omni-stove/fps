using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // 倒されて戻るときだけ、ルールの RespawnAnchor の順に出す所を選ぶ (オフラインの ModeRunnerBase.RespawnByRules と同じ順の決め方)。
    // 入った時・試合とラウンドの頭は Session が選んだ出現点のまま出す。
    internal sealed partial class MatchRulesModule
    {
        // Session は Respawning を出した直後に同じ人の Spawned を出すので、その Spawned で使ったら空に戻す
        private int? _respawningAfterDeath;

        // 置ける物のモジュール (ServerDeployables) が作られるときに差し込む。差し込まれていなければビーコンを試さず出現点に戻す
        internal IServerRespawnBeacons Beacons { get; set; }

        private void HandleRespawning(int playerId)
        {
            _respawningAfterDeath = playerId;
        }

        private bool TakeRespawningAfterDeath(int playerId)
        {
            var afterDeath = _respawningAfterDeath == playerId;
            _respawningAfterDeath = null;
            return afterDeath;
        }

        // 拠点 (Base) は Session が選んだ出現点。味方の近く (Squad) はサーバーに選ぶ手立てがまだ無いので、選べなかったとして次の順へ進める
        private RespawnAnchor ChooseRespawnAnchor(int playerId, ref Vector3 position, ref float yaw)
        {
            var beacons = Beacons;
            var beaconPosition = position;
            var beaconYaw = yaw;
            var anchor = RespawnAnchorOrder.Choose(
                _data.Rules.RespawnAnchors,
                candidate => candidate == RespawnAnchor.Beacon && beacons != null && beacons.TryUseBeacon(playerId, out beaconPosition, out beaconYaw));
            if (anchor == RespawnAnchor.Beacon)
            {
                position = beaconPosition;
                yaw = beaconYaw;
            }

            return anchor;
        }
    }
}
