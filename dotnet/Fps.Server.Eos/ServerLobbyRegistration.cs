using System;
using System.Globalization;
using System.IO;
using Fps.Core;

namespace Fps.Server.Eos
{
    /// <summary>
    /// <c>-register-lobby</c> のサーバーの一生 (待ち受け後に登録・人数の更新・drain で閉じる・止まる前に閉じ終えるのを待つ) を、
    /// Unity の <c>NetBootstrap</c> と同じ順にまとめる。サーバーのモジュールはこれに tick を渡すだけにする。
    /// </summary>
    internal sealed class ServerLobbyRegistration : IDisposable
    {
        /// <summary>閉じる要求を出してすぐ SDK を解放すると要求が届かず、つながらないサーバーのロビーが検索に残るので、この秒数まで待つ。</summary>
        public const float CloseWaitSeconds = 3f;

        public const int CloseWaitPollMilliseconds = 10;

        private readonly ServerLobbyService _service;
        private readonly ServerLobbyRegistrar _registrar;
        private readonly TextWriter _log;
        private readonly Func<float> _clock;
        private readonly Action<int> _sleep;
        private readonly Action<string> _failed;
        private bool _closing;
        private bool _closed;
        private bool _disposed;

        /// <param name="backend">ロビーのサービスを呼ぶ側。これが Dispose する。</param>
        /// <param name="endpoint">クライアントがつなぐ公開アドレス。</param>
        /// <param name="version">サーバーの版。</param>
        /// <param name="log">ログの出力先。</param>
        /// <param name="clock">今の時刻 (秒)。</param>
        /// <param name="sleep">閉じ終えるのを待つ間に眠るミリ秒を受け取る。</param>
        /// <param name="failed">一度も登録できないまま諦めたときに理由を受け取る。</param>
        /// <param name="schedule">やり直す間隔と期限。null なら <see cref="ServerLobby"/> の値。</param>
        public ServerLobbyRegistration(
            IServerLobbyBackend backend,
            string endpoint,
            string version,
            TextWriter log,
            Func<float> clock,
            Action<int> sleep,
            Action<string> failed,
            RetrySchedule schedule = null)
        {
            ArgumentNullException.ThrowIfNull(backend);
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _sleep = sleep ?? throw new ArgumentNullException(nameof(sleep));
            _failed = failed ?? throw new ArgumentNullException(nameof(failed));
            _service = new ServerLobbyService(backend, ServerLobby.BucketId(version), log);
            _registrar = new ServerLobbyRegistrar(
                _service,
                endpoint,
                version,
                schedule ?? new RetrySchedule(ServerLobby.RetryIntervalSeconds, ServerLobby.TimeoutSeconds),
                log);
        }

        public ServerLobbyService Service => _service;

        public ServerLobbyRegistrar Registrar => _registrar;

        /// <summary>
        /// 待ち受けを始めた後の tick ごとに呼ぶ。待ち受けていないサーバーを見つけさせないよう、最初の呼び出しで登録を始める。
        /// drain に入ったら、待っている間にクイックマッチで選ばれないようロビーを閉じ始める。
        /// </summary>
        public void Tick(int players, bool draining)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (draining)
            {
                BeginClose();
            }

            if (!_closing && !_registrar.HasBegun)
            {
                _registrar.Begin(_clock(), _failed);
            }

            if (!_closing)
            {
                _registrar.SetPlayers(players);
            }

            _registrar.Tick(_clock());
        }

        /// <summary>ロビーを閉じ、閉じ終えるか <see cref="CloseWaitSeconds"/> が過ぎるまで SDK を進めて待つ。</summary>
        public void CloseAndWait()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            BeginClose();
            if (_closed)
            {
                return;
            }

            var deadline = _clock() + CloseWaitSeconds;
            _log.WriteLine("[Lobby] closing the server lobby before quitting (up to "
                + CloseWaitSeconds.ToString(CultureInfo.InvariantCulture) + "s)");
            while (!_closed && _clock() < deadline)
            {
                _service.Tick();
                if (!_closed)
                {
                    _sleep(CloseWaitPollMilliseconds);
                }
            }

            if (!_closed)
            {
                _log.WriteLine("[Lobby] the server lobby did not close within "
                    + CloseWaitSeconds.ToString(CultureInfo.InvariantCulture) + "s; quitting anyway");
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _service.Dispose();
        }

        private void BeginClose()
        {
            if (_closing)
            {
                return;
            }

            _closing = true;
            _registrar.Close(() => _closed = true);
        }
    }
}
