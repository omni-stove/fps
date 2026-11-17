using System;
using Shooter.Core;

namespace Shooter.NetV2
{
    // ボイスは試合に無くても遊べるので、入れなかったときは検索を失敗にせず、ロビー無し (null) を返して UDP でつながせる。
    public sealed class MatchLobbyJoin
    {
        private readonly ILobbyBackend _backend;
        private readonly Action<string> _log;
        private Action<MatchLobby> _done;
        private string _lobbyId;
        private bool _finished;

        public MatchLobbyJoin(ILobbyBackend backend, Action<string> log)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public bool IsJoining => _done != null && !_finished;

        public void Begin(LobbyInfo lobby, Action<MatchLobby> done)
        {
            if (lobby == null)
            {
                throw new ArgumentNullException(nameof(lobby));
            }

            if (_done != null)
            {
                throw new InvalidOperationException("the match lobby join has already begun");
            }

            _done = done ?? throw new ArgumentNullException(nameof(done));
            _lobbyId = lobby.Id;
            if (string.IsNullOrEmpty(_lobbyId) || !lobby.VoiceEnabled)
            {
                _log(MatchLobbyLog.NoVoiceRoom(_lobbyId));
                Finish(null);
                return;
            }

            _log(MatchLobbyLog.Joining(_lobbyId));
            _backend.JoinMatchLobby(_lobbyId, HandleJoined);
        }

        // 遅れて入れても、ロビーのサービスを閉じれば出るので、ここでは出ずに結果だけ捨てる
        public void GiveUp(string reason)
        {
            if (!IsJoining)
            {
                return;
            }

            _log(MatchLobbyLog.NotJoined(_lobbyId, reason));
            Finish(null);
        }

        public void Cancel()
        {
            _finished = true;
        }

        private void HandleJoined(LobbyInfo lobby, string error)
        {
            if (_finished)
            {
                return;
            }

            if (error == null && lobby == null)
            {
                error = "the lobby service returned no lobby";
            }

            if (error != null)
            {
                _log(MatchLobbyLog.NotJoined(_lobbyId, error));
                Finish(null);
                return;
            }

            _log(MatchLobbyLog.Joined(_lobbyId));
            Finish(new MatchLobby(_backend, _lobbyId, _log));
        }

        private void Finish(MatchLobby lobby)
        {
            _finished = true;
            _done(lobby);
        }
    }
}
