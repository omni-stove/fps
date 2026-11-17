using System;

namespace Shooter.Core
{
    // Session は進んでいる試合そのもので写しではないので、受け取った側は出された時に読む。
    public sealed class ExecutionStatus
    {
        public ExecutionStatus(ExecutionSession session, int? localPlayerId, int? interactTargetId)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            LocalPlayerId = localPlayerId;
            InteractTargetId = interactTargetId;
        }

        public ExecutionSession Session { get; }

        public int? LocalPlayerId { get; }

        public int? InteractTargetId { get; }
    }
}
