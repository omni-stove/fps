using System;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // サーバーは誘導弾がまだ自分 (乗っている乗り物) を追っている間だけ Locked を送り、外れたら None を送る (ServerLockOnWarnings)。
    // 向きは受け取った側でしか出せないので、届いた弾の位置を覚え、スナップショットに同じ弾があれば今の位置に寄せてから、自分の向きで出す。
    public sealed class ClientLockOnWarning
    {
        public LockOnWarningStage Stage { get; private set; }

        public int ProjectileId { get; private set; } = LockOnWarningMessage.NoProjectile;

        // 脅威 (誘導弾) の最後に分かった位置。
        public Vector3 Source { get; private set; }

        public bool IsWarning => Stage != LockOnWarningStage.None;

        public bool Handle(IMessage message)
        {
            if (!(message is LockOnWarningMessage warning))
            {
                return false;
            }

            if (warning.Stage == LockOnWarningStage.None)
            {
                Clear();
                return true;
            }

            Stage = warning.Stage;
            ProjectileId = warning.ProjectileId;
            Source = warning.Source;
            return true;
        }

        // 試合が終わって始め直されるとサーバーは解いたことを送らずに覚えを捨てるので、試合が終わった・自分が倒れている間は手元でも解く。
        public void Apply(SnapshotMessage snapshot, int localPlayerId)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!IsWarning)
            {
                return;
            }

            var own = snapshot.Players.IndexOf(localPlayerId);
            if (snapshot.Match.State == GameModeState.Ended || own < 0 || snapshot.Players[own].Health <= 0)
            {
                Clear();
                return;
            }

            // 見えない弾はスナップショットに載らないことがあるので、そのときは最後に分かった位置のまま向きを出す
            var at = ProjectileId >= 0 ? snapshot.Projectiles.IndexOf(ProjectileId) : -1;
            if (at >= 0)
            {
                Source = snapshot.Projectiles[at].Position;
            }
        }

        // 向きは自分の正面から時計回りの度。真上・真下の弾や向きの分からないときは正面とする (LockOnWarningPublisher と同じ)。
        public LockOnWarningStatus? StatusFrom(Vector3 self, float headingDegrees)
        {
            if (!IsWarning)
            {
                return null;
            }

            var bearing = 0f;
            if (IsFinite(self) && !float.IsNaN(headingDegrees) && !float.IsInfinity(headingDegrees)
                && MinimapProjection.TryBearingDegrees(self, Source, out var world))
            {
                bearing = MinimapProjection.RelativeDegrees(headingDegrees, world);
            }

            return new LockOnWarningStatus(Stage, bearing);
        }

        public void Clear()
        {
            Stage = LockOnWarningStage.None;
            ProjectileId = LockOnWarningMessage.NoProjectile;
            Source = Vector3.Zero;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.X) && !float.IsInfinity(value.X)
                && !float.IsNaN(value.Y) && !float.IsInfinity(value.Y)
                && !float.IsNaN(value.Z) && !float.IsInfinity(value.Z);
        }
    }
}
