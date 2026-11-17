using Shooter.Protocol;

namespace Shooter.Server
{
    internal interface IServerControl
    {
        bool IsDraining { get; }

        // drain を待たないのは、続けられない失敗だから。
        void Fail(int exitCode, string message);

        // モジュールのフックの最中に呼んでよいよう、切るのは今の tick の残りのモジュールを回した後。
        void Kick(ConnectionId connection, string reason, string logText);
    }
}
