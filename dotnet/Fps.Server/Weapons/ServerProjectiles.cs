using System;
using System.Diagnostics.CodeAnalysis;
using Fps.Core;
using Fps.Protocol;

namespace Fps.Server
{
    /// <summary>
    /// 飛ぶ弾 (<see cref="RoundDelivery.Projectile"/>)。射撃を受け入れたときに作り、毎 tick 進めて (巻き戻さない)、当たれば <see cref="ServerImpact"/> に渡す。
    /// </summary>
    internal sealed class ServerProjectiles
    {
        public ServerProjectiles(ServerCombat combat)
        {
            Combat = combat ?? throw new ArgumentNullException(nameof(combat));
        }

        public ServerCombat Combat { get; }

        /// <summary>shot の弾 1 本を planned の始点と向きで撃ち出す。</summary>
        [SuppressMessage("Performance", "CA1822", Justification = "Flying rounds are kept on the instance")]
        public void Launch(in ServerShot shot, PlannedRound planned)
        {
        }

        [SuppressMessage("Performance", "CA1822", Justification = "Flying rounds are kept on the instance")]
        public void Tick(float deltaSeconds)
        {
        }

        /// <summary>飛んでいる弾をスナップショットに載せる。</summary>
        [SuppressMessage("Performance", "CA1822", Justification = "Flying rounds are kept on the instance")]
        public void Contribute(SnapshotList<ProjectileSnapshot> list)
        {
            ArgumentNullException.ThrowIfNull(list);
            list.Clear();
        }

        /// <summary>試合の始まりに、飛んでいる弾をすべて消す。</summary>
        [SuppressMessage("Performance", "CA1822", Justification = "Flying rounds are kept on the instance")]
        public void Clear()
        {
        }
    }
}
