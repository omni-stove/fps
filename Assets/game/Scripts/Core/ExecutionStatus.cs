using System;

namespace Shooter.Core
{
    /// <summary>
    /// エクスキューションの Runner が <see cref="ModeStatus.Detail"/> に入れる、エクスキューション固有の状態。
    /// 試合 (<see cref="Session"/>)・この画面で操作しているプレイヤーの ID・その人がいまインタラクトを押している相手。
    /// </summary>
    /// <remarks><see cref="Session"/> は進んでいる試合そのもので、写しではない。受け取った側は出された時に読む。</remarks>
    public sealed class ExecutionStatus
    {
        /// <param name="session">進めている試合。</param>
        /// <param name="localPlayerId">この画面で操作しているプレイヤー。いなければ null。</param>
        /// <param name="interactTargetId">localPlayerId がいまインタラクトを押している相手。押していなければ null。</param>
        /// <exception cref="ArgumentNullException">session が null。</exception>
        public ExecutionStatus(ExecutionSession session, int? localPlayerId, int? interactTargetId)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            LocalPlayerId = localPlayerId;
            InteractTargetId = interactTargetId;
        }

        public ExecutionSession Session { get; }

        /// <summary>この画面で操作しているプレイヤー。いなければ null。</summary>
        public int? LocalPlayerId { get; }

        /// <summary><see cref="LocalPlayerId"/> がいまインタラクトを押している相手。押していなければ null。</summary>
        public int? InteractTargetId { get; }
    }
}
