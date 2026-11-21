using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // Unity の Mine と同じ流れ (有効になるまで待つ → 敵が触れる距離に入ったら消してから爆発させる) で動かす。
    // 有効になるまでの時間と、誰で起きるかはオフラインと同じ MineState が決める
    internal sealed partial class ServerDeployables
    {
        // 足元や貼った面ちょうどから爆発させると、相手へ引く遮りの線が面に沿って面に当たることがあるので、面から少し離す (Unity の Mine.BlastLift と同じ)
        public const float BlastLift = 0.1f;

        private void TickMine(Placed placed, float deltaSeconds)
        {
            var state = placed.Mine;

            // 時計が進まなかった tick は待ちを進めないが、有効な地雷は踏まれたかを見続ける
            if (deltaSeconds > 0f && float.IsFinite(deltaSeconds) && state.Tick(deltaSeconds))
            {
                _log.WriteLine(MineLog.Armed(ServerDeployableLog.Owner(placed.OwnerId), placed.Type.Id, placed.Id));
            }

            if (!state.IsArmed || !TryFindIntruder(placed, out var intruderId) || !state.TryTrigger(true))
            {
                return;
            }

            var center = BlastCenterOf(placed);
            _log.WriteLine(MineLog.Triggered(
                ServerDeployableLog.Owner(placed.OwnerId), placed.Type.Id, placed.Id, ServerDeployableLog.Owner(intruderId), center));

            // 自分の耐久も爆発の範囲に入り、先に爆発させると「壊された」で消えるので、消してから爆発させる (Unity の Mine.Detonate と同じ)
            Remove(placed, DeployRemoval.Detonated);
            _combat.Explode(center, placed.Type.Mine.Explosion, placed.OwnerId, placed.Type.Id);
        }

        // Unity の Mine.FindIntruder は地雷の位置を中心にした球に触れた体を探すので、体のカプセルの面までの距離で測る。
        // タレットと同じく試合中だけ起き、倒された人では起きない (Unity の Mine.FindIntruder も死んだ体を飛ばす)
        private bool TryFindIntruder(Placed placed, out int intruderId)
        {
            intruderId = 0;
            var session = _rules.Session;
            if (session == null || session.State != GameModeState.Playing)
            {
                return false;
            }

            var radius = placed.Type.Mine.TriggerRadius;
            foreach (var body in _bodies.All)
            {
                var id = body.PlayerId;
                if (!body.HasSpawned || !_combat.IsHittable(id) || !IsTriggeredBy(placed, id))
                {
                    continue;
                }

                body.Capsule(out var bottom, out var top);
                if (ServerCombat.DistanceToCapsule(placed.Position, bottom, top, body.Radius) <= radius)
                {
                    intruderId = id;
                    return true;
                }
            }

            return false;
        }

        private bool IsTriggeredBy(Placed placed, int playerId)
        {
            return MineState.IsTriggeredBy(playerId == placed.OwnerId, Side.FromTeam(placed.Team), SideOf(playerId), BotRoster.IsBotId(playerId));
        }

        private static Vector3 BlastCenterOf(Placed placed)
        {
            return placed.Position + (placed.SurfaceNormal * BlastLift);
        }
    }
}
