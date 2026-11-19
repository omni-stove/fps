using System;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;

namespace Shooter.NetV2
{
    // NetV2 の自分の体は Damageable も WeaponController も持たないので、オフラインの体が一人称の武器モデルを隠す場面 (死んだ・はしごで武器をしまった・自分の武器を撃てない席に乗った) を
    // スナップショットと予測から同じ理由 (ViewmodelPresenter.Reason*) で決める。倒れている間は撃てない (ClientFire.Decide) ので、それも隠す。
    public static class NetV2Viewmodel
    {
        public static string HiddenReason(SnapshotMessage latest, int playerId, in CharacterMotorState predicted, bool seatHidesWeapon)
        {
            var own = latest != null ? latest.Players.IndexOf(playerId) : -1;
            if (own >= 0)
            {
                var row = latest.Players[own];
                if (row.Downed.IsDowned)
                {
                    return ViewmodelPresenter.ReasonDowned;
                }

                if (row.Health <= 0)
                {
                    return ViewmodelPresenter.ReasonDead;
                }
            }

            if (seatHidesWeapon)
            {
                return ViewmodelPresenter.ReasonSeated;
            }

            return predicted.Ladder.IsClimbing ? ViewmodelPresenter.ReasonHolstered : null;
        }

        // オフラインの体 (ViewmodelMotion が CharacterController から読む) と同じく、宙にいる間は揺らさない。
        public static float HorizontalSpeed(in CharacterMotorState predicted)
        {
            if (!predicted.IsGrounded)
            {
                return 0f;
            }

            var velocity = predicted.Velocity;
            return MathF.Sqrt((velocity.X * velocity.X) + (velocity.Z * velocity.Z));
        }
    }
}
