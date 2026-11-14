using System;
using System.IO;
using Fps.Core;

namespace Fps.Server.Eos
{
    /// <summary>
    /// Unity の <c>Fps.Net.ServerLobbyRegistrar</c> を移したもの。やり直しの間隔・諦める期限・ログの行を同じにし、
    /// 運用の手順と監視をそのまま使えるようにする。
    /// </summary>
    /// <remarks>
    /// 起動してから一度も登録できないまま期限を過ぎたら、認証情報や設定の誤りとみなして諦める。
    /// 一度登録できた後は試合を止めないよう、期限を過ぎてもエラーを出しながら作り直しを続ける。
    /// </remarks>
    internal sealed class ServerLobbyRegistrar
    {
        /// <summary>退出の通知が来ないままロビーが消えていることがあるので、更新がこの回数続けて失敗したら作り直す。</summary>
        public const int MaxUpdateFailures = 3;

        private const string _logPrefix = ServerLobbyService.LogPrefix;

        private readonly ServerLobbyService _service;
        private readonly string _endpoint;
        private readonly string _version;
        private readonly RetrySchedule _schedule;
        private readonly TextWriter _log;
        private Action<string> _failed;
        private Step _step;
        private float _now;
        private int _players;
        private int _publishedPlayers;
        private int _updateFailures;
        private int _lobbyGeneration;
        private float _nextUpdateTime;
        private string _lastError;
        private bool _failedLeaveDone;
        private Action _failedLeaveWaiters;

        /// <param name="service">ログイン前のロビーのサービス。</param>
        /// <param name="endpoint">クライアントがつなぐ公開アドレス (<see cref="NetLaunchOptions.RegisterLobbyEndpoint"/>)。</param>
        /// <param name="version">サーバーの版。</param>
        /// <param name="schedule">やり直す間隔と、登録できないまま待つ期限。</param>
        /// <param name="log">ログの出力先。</param>
        public ServerLobbyRegistrar(ServerLobbyService service, string endpoint, string version, RetrySchedule schedule, TextWriter log)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            if (!NetLaunchOptions.TryParseEndpoint(endpoint, out _, out _))
            {
                throw new ArgumentException("must be an IPv4 address:port that clients can connect to (got '" + endpoint + "')", nameof(endpoint));
            }

            _endpoint = endpoint;
            _version = version;
            _service.Removed += HandleRemoved;
            _service.LoggedOut += HandleLoggedOut;
        }

        private enum Step
        {
            Idle,
            Waiting,
            LoggingIn,
            Creating,
            Registered,
            Closed,
            Failed,
        }

        public bool IsRegistered => _step == Step.Registered;

        public bool HasRegistered { get; private set; }

        public bool HasBegun => _step != Step.Idle;

        public int Players => _players;

        /// <summary>ロビーの属性に載っている人数。まだ載っていなければ -1。</summary>
        public int PublishedPlayers => IsRegistered ? _publishedPlayers : -1;

        /// <param name="now">今の時刻 (秒)。<see cref="Tick"/> と同じ時計で渡す。</param>
        /// <param name="failed">一度も登録できないまま諦めたときに理由を受け取る。</param>
        public void Begin(float now, Action<string> failed)
        {
            if (_step != Step.Idle)
            {
                throw new InvalidOperationException("the server lobby registrar has already begun");
            }

            _failed = failed ?? throw new ArgumentNullException(nameof(failed));
            _now = now;
            _schedule.Start(now);
            _step = Step.Waiting;
            _log.WriteLine(_logPrefix + "registering server=" + _endpoint + " bucket=" + ServerLobby.BucketId(_version));
            Attempt();
        }

        public void SetPlayers(int players)
        {
            if (players < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(players), players, "must not be negative");
            }

            if (players == _players)
            {
                return;
            }

            _log.WriteLine(_logPrefix + "players " + _players + " -> " + players);
            _players = players;
            PublishPlayers();
        }

        public void Tick(float now)
        {
            _now = now;
            _service.Tick();

            switch (_step)
            {
                case Step.Waiting:
                    if (_schedule.IsExpired(now))
                    {
                        Expire();
                    }
                    else if (_schedule.IsDue(now))
                    {
                        Attempt();
                    }

                    break;
                case Step.LoggingIn:
                case Step.Creating:
                    if (_schedule.IsExpired(now))
                    {
                        Expire();
                    }

                    break;
                case Step.Registered:
                    PublishPlayers();
                    break;
            }
        }

        /// <summary>
        /// やり直しをやめ、作ったロビーを閉じる。閉じ終えたら (閉じるものが無ければすぐ) <paramref name="done"/> を呼ぶ。
        /// 何度呼んでもよい。
        /// </summary>
        public void Close(Action done = null)
        {
            if (_step == Step.Failed && !_failedLeaveDone)
            {
                // 作成中に期限が来たときは、作り終えたロビーを閉じ終えるまで完了にしない
                _failedLeaveWaiters += done;
                return;
            }

            if (_step == Step.Closed || _step == Step.Failed)
            {
                done?.Invoke();
                return;
            }

            var wasRegistered = _step == Step.Registered;
            _step = Step.Closed;
            Unsubscribe();
            if (wasRegistered && _service.Current != null)
            {
                _log.WriteLine(_logPrefix + "unregistering id=" + _service.Current.Id);
            }

            _service.LeaveLobby(_ => done?.Invoke());
        }

        private void Attempt()
        {
            // 失ったロビーを閉じている途中は、その完了を待ってから作る
            if (_service.IsBusy)
            {
                _schedule.Wait(_now);
                return;
            }

            if (!_service.IsLoggedIn)
            {
                _step = Step.LoggingIn;
                _service.Login(HandleLoggedIn);
                return;
            }

            Create();
        }

        private void HandleLoggedIn(string userId, string error)
        {
            if (_step != Step.LoggingIn)
            {
                return;
            }

            if (error != null)
            {
                Retry("EOS login failed: " + error);
                return;
            }

            Create();
        }

        private void Create()
        {
            if (_service.Current != null)
            {
                // 作り直しの前に閉じ損ねたロビーが残っている。2 つ並べて載せないよう、閉じてからやり直す
                var stale = _service.Current.Id;
                _service.LeaveLobby();
                Retry("closing the stale lobby " + stale + " before re-creating");
                return;
            }

            _step = Step.Creating;
            var players = _players;
            _service.CreateLobby(
                ServerLobby.MaxPlayers,
                ServerLobby.Attributes(_endpoint, _version, players),
                (lobby, error) => HandleCreated(lobby, error, players));
        }

        private void HandleCreated(LobbyInfo lobby, string error, int players)
        {
            if (_step != Step.Creating)
            {
                return;
            }

            if (error != null)
            {
                Retry("creating the server lobby failed: " + error);
                return;
            }

            _step = Step.Registered;
            _lobbyGeneration++;
            _publishedPlayers = players;
            _updateFailures = 0;
            _nextUpdateTime = _now;
            _lastError = null;
            HasRegistered = true;
            _log.WriteLine(_logPrefix + "registered id=" + lobby.Id + " server=" + _endpoint + " players=" + players);
            PublishPlayers();
        }

        private void PublishPlayers()
        {
            if (_step != Step.Registered || _service.IsUpdating || _players == _publishedPlayers || _now < _nextUpdateTime)
            {
                return;
            }

            var players = _players;
            var generation = _lobbyGeneration;
            _service.UpdateAttributes(
                new[] { ServerLobby.PlayersAttribute(players) }, error => HandleUpdated(error, players, generation));
        }

        private void HandleUpdated(string error, int players, int generation)
        {
            // 失ったロビーへの更新の結果を、作り直したロビーの人数として数えない
            if (_step != Step.Registered || generation != _lobbyGeneration)
            {
                PublishPlayers();
                return;
            }

            if (error == null)
            {
                _publishedPlayers = players;
                _updateFailures = 0;
                PublishPlayers();
                return;
            }

            _updateFailures++;
            if (_updateFailures < MaxUpdateFailures)
            {
                _nextUpdateTime = _now + _schedule.IntervalSeconds;
                return;
            }

            _service.LeaveLobby();
            Lose("updating the players attribute failed " + _updateFailures + " times in a row: " + error);
        }

        private void HandleRemoved(string reason)
        {
            if (_step == Step.Registered)
            {
                Lose(reason);
            }
        }

        private void HandleLoggedOut(string reason)
        {
            // ロビーに入っていれば Removed で作り直しが始まっている。ログイン・作成の途中なら、その失敗で作り直す
            if (_step == Step.Registered)
            {
                Lose("logged out: " + reason);
            }
        }

        private void Lose(string reason)
        {
            _step = Step.Waiting;
            _lastError = reason;
            _schedule.Start(_now);
            _log.WriteLine(_logPrefix + "lost the server lobby (" + reason + "); re-creating it");
        }

        private void Retry(string reason)
        {
            _step = Step.Waiting;
            _lastError = reason;
            _schedule.Wait(_now);
            _log.WriteLine(_logPrefix + reason + "; trying again in " + _schedule.IntervalSeconds + "s");
        }

        private void Expire()
        {
            var reason = "the server is not registered after " + _schedule.TimeoutSeconds + "s"
                + (_lastError == null ? "" : " (last error: " + _lastError + ")");
            if (!HasRegistered)
            {
                _step = Step.Failed;
                Unsubscribe();
                _service.LeaveLobby(_ =>
                {
                    _failedLeaveDone = true;
                    var waiting = _failedLeaveWaiters;
                    _failedLeaveWaiters = null;
                    waiting?.Invoke();
                });
                _failed(reason);
                return;
            }

            _log.WriteLine(_logPrefix + reason + "; clients cannot find this server until it is; still trying");
            _schedule.Start(_now);
            if (_step == Step.Waiting)
            {
                Attempt();
            }
        }

        private void Unsubscribe()
        {
            _service.Removed -= HandleRemoved;
            _service.LoggedOut -= HandleLoggedOut;
        }
    }
}
