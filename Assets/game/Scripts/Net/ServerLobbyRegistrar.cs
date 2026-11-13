using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// 専用サーバーを EOS のロビーに登録しておく (<c>-register-lobby</c>)。ログインして
    /// <see cref="ServerLobby.Attributes"/> を付けたロビーを作り、人数が変わるたびに人数の属性を更新する。
    /// ロビーやログインを失ったら <see cref="RetrySchedule"/> の間隔で作り直し、<see cref="Close"/> でロビーを閉じる。
    /// </summary>
    /// <remarks>
    /// 起動してから一度も登録できないまま期限を過ぎたら、認証情報や設定の誤りとみなして諦める。
    /// 一度登録できた後は試合を止めないよう、期限を過ぎてもエラーを出しながら作り直しを続ける。
    /// </remarks>
    public sealed class ServerLobbyRegistrar
    {
        /// <summary>属性の更新がこの回数続けて失敗したら、ロビーを失ったとみなして作り直す。</summary>
        public const int MaxUpdateFailures = 3;

        private readonly LobbyService _service;
        private readonly string _endpoint;
        private readonly string _version;
        private readonly RetrySchedule _schedule;
        private Action<string> _failed;
        private Step _step;
        private float _now;
        private int _players;
        private int _publishedPlayers;
        private int _updateFailures;
        private int _lobbyGeneration;
        private float _nextUpdateTime;
        private string _lastError;

        /// <param name="service">ログイン前のロビーのサービス。</param>
        /// <param name="endpoint">クライアントがつなぐ公開アドレス (<see cref="NetLaunchOptions.RegisterLobbyEndpoint"/>)。</param>
        /// <param name="version">ビルドの版 (Application.version)。</param>
        public ServerLobbyRegistrar(LobbyService service, string endpoint, string version)
            : this(service, endpoint, version, new RetrySchedule(ServerLobby.RetryIntervalSeconds, ServerLobby.TimeoutSeconds))
        {
        }

        /// <param name="service">ログイン前のロビーのサービス。</param>
        /// <param name="endpoint">クライアントがつなぐ公開アドレス。</param>
        /// <param name="version">ビルドの版。</param>
        /// <param name="schedule">やり直す間隔と、登録できないまま待つ期限。</param>
        public ServerLobbyRegistrar(LobbyService service, string endpoint, string version, RetrySchedule schedule)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
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

        /// <summary>今ロビーに登録されているか。</summary>
        public bool IsRegistered => _step == Step.Registered;

        /// <summary>一度でも登録できたか。</summary>
        public bool HasRegistered { get; private set; }

        /// <summary>ロビーに載せたい今の人数。</summary>
        public int Players => _players;

        /// <summary>ロビーの属性に載っている人数。まだ載っていなければ -1。</summary>
        public int PublishedPlayers => IsRegistered ? _publishedPlayers : -1;

        /// <summary>登録を始める。すぐにログインする。</summary>
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
            Debug.Log(LobbyService.LogPrefix + "registering server=" + _endpoint + " bucket=" + ServerLobby.BucketId(_version));
            Attempt();
        }

        /// <summary>サーバーに接続している人数を知らせる。変わっていれば人数の属性を更新する。</summary>
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

            Debug.Log(LobbyService.LogPrefix + "players " + _players + " -> " + players);
            _players = players;
            PublishPlayers();
        }

        /// <summary>サービスを進め、待っているやり直しと人数の更新を行う。毎フレーム呼ぶ。</summary>
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
                Debug.Log(LobbyService.LogPrefix + "unregistering id=" + _service.Current.Id);
            }

            // 作成の途中なら、LobbyService が作り終えたところで閉じる
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
            Debug.Log(LobbyService.LogPrefix + "registered id=" + lobby.Id + " server=" + _endpoint + " players=" + players);
            PublishPlayers();
        }

        /// <summary>人数が属性と違えば更新を送る。更新中・失敗の直後は待つ。</summary>
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

            // 退出の通知が来ないままロビーが消えていることがある。古いロビーを閉じてから作り直す
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

        /// <summary>登録していたロビーを失った。期限を数え直し、すぐに作り直す。</summary>
        private void Lose(string reason)
        {
            _step = Step.Waiting;
            _lastError = reason;
            _schedule.Start(_now);
            Debug.LogWarning(LobbyService.LogPrefix + "lost the server lobby (" + reason + "); re-creating it");
        }

        private void Retry(string reason)
        {
            _step = Step.Waiting;
            _lastError = reason;
            _schedule.Wait(_now);
            Debug.LogWarning(LobbyService.LogPrefix + reason + "; trying again in " + _schedule.IntervalSeconds + "s");
        }

        private void Expire()
        {
            var reason = "the server is not registered after " + _schedule.TimeoutSeconds + "s"
                + (_lastError == null ? "" : " (last error: " + _lastError + ")");
            if (!HasRegistered)
            {
                _step = Step.Failed;
                Unsubscribe();
                _service.LeaveLobby();
                _failed(reason);
                return;
            }

            Debug.LogError(LobbyService.LogPrefix + reason + "; clients cannot find this server until it is; still trying");
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
