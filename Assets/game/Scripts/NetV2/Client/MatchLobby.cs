using System;

namespace Shooter.NetV2
{
    // ボイスの RTC 部屋はロビーのメンバーでいる間しか使えないので、試合ロビーに入ったロビーのサービスを、
    // 見つけた検索から試合のクライアントへ渡し、試合の間は持ち続ける。
    public sealed class MatchLobby : IDisposable
    {
        private readonly ILobbyBackend _backend;
        private readonly Action<string> _log;

        public MatchLobby(ILobbyBackend backend, string lobbyId, Action<string> log)
        {
            if (string.IsNullOrEmpty(lobbyId))
            {
                throw new ArgumentException("a lobby id is required", nameof(lobbyId));
            }

            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            LobbyId = lobbyId;
        }

        public string LobbyId { get; }

        public bool IsDisposed { get; private set; }

        public void Tick()
        {
            if (!IsDisposed)
            {
                _backend.Tick();
            }
        }

        // LeaveMatchLobby の返事を待たずに閉じるのは、ILobbyBackend.Dispose が入った試合ロビーから出るため。
        // 先に LeaveMatchLobby を呼ぶと、返事が来る前に閉じたサービスへ同じロビーから 2 度出ることになる。
        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            _backend.Dispose();
            _log(MatchLobbyLog.Left(LobbyId));
        }
    }
}
