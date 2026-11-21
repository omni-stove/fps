using System;

namespace Shooter.Core
{
    // Session は進んでいる試合そのもので写しではないので、受け取った側は出された時に読む。
    public sealed class ExecutionStatus
    {
        public ExecutionStatus(ExecutionSession session, int? localPlayerId, int? interactTargetId)
            : this(session ?? throw new ArgumentNullException(nameof(session)), session, localPlayerId, interactTargetId)
        {
        }

        private ExecutionStatus(IExecutionView view, ExecutionSession session, int? localPlayerId, int? interactTargetId)
        {
            View = view;
            Session = session;
            LocalPlayerId = localPlayerId;
            InteractTargetId = interactTargetId;
        }

        // オフラインの試合 (ExecutionRunner) でなければ null。HUD は View を読む
        public ExecutionSession Session { get; }

        public IExecutionView View { get; }

        public int? LocalPlayerId { get; }

        public int? InteractTargetId { get; }

        // コンストラクターを分けないのは、null を渡したときにどちらの形か決まらなくなるため
        public static ExecutionStatus FromView(IExecutionView view, int? localPlayerId, int? interactTargetId)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            return new ExecutionStatus(view, view as ExecutionSession, localPlayerId, interactTargetId);
        }
    }
}
