using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // とどめの進み具合は ServerMelee の技 (ContextMeleeRun) にしか無いので、ルール (MatchRulesModule) が書いたダウンの欄に、
    // 刺している人と進み具合を書き足す。ルールより後に回す必要がある (ServerModules の並び)
    internal sealed partial class ServerMelee : ISnapshotContributor
    {
        public void Contribute(SnapshotMessage snapshot, ServerPlayer receiver)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (_takedowns.Count == 0)
            {
                return;
            }

            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                if (!row.Downed.IsDowned || !TryGetExecution(row.PlayerId, out var executorId, out var progress))
                {
                    continue;
                }

                row.Downed = row.Downed.WithExecutor(executorId, (byte)HudFormat.RevivePercent(progress));
            }
        }

        // ダウン中の相手に刺している技 (TargetDowned を要る技) だけを数える。立っている相手へのテイクダウンはダウンの欄に載せない。
        // 同じ相手を 2 人が刺すことは TryTakedown が IsTakedownTarget で断るので、見つけた最初の 1 つを返す
        internal bool TryGetExecution(int targetId, out int executorId, out float progress)
        {
            foreach (var pair in _takedowns)
            {
                var run = pair.Value.Run;
                if (pair.Value.TargetId != targetId || !run.IsActive || !NeedsDowned(run.Move))
                {
                    continue;
                }

                executorId = pair.Key;
                var total = run.Move.TotalSeconds;
                progress = total > 0f ? Math.Clamp(run.Elapsed / total, 0f, 1f) : 1f;
                return true;
            }

            executorId = PlayerIds.None;
            progress = 0f;
            return false;
        }
    }
}
