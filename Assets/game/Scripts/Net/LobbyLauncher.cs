using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// <c>-lobby create|join|quickmatch</c> の起動手順を進める。ログインし、ロビーを作る (または探して入る) と、
    /// ネットワークの起動設定 (オーナーなら EOS の Host、メンバーならオーナーへ接続するクライアント) を返す。
    /// quickmatch では専用サーバーのロビーを探し、選んだサーバーの公開アドレスへ接続する UDP のクライアントを返す (ロビーには入らない)。
    /// どのロビーに入るか・いつ諦めるかは Fps.Core (<see cref="LobbySelector"/> / <see cref="RetrySchedule"/> /
    /// <see cref="LobbyLaunchOptions.ToNetOptions"/>) が決め、ここは順に呼ぶだけにする。
    /// </summary>
    public sealed class LobbyLauncher
    {
        private readonly LobbyService _service;
        private readonly LobbyLaunchOptions _options;
        private readonly RetrySchedule _schedule;
        private readonly string _version;
        private Action<NetLaunchOptions> _ready;
        private Action<string> _failed;
        private Step _step;
        private float _now;
        private string _lastRetryReason;

        /// <param name="service">ログイン前のロビーのサービス。</param>
        /// <param name="options">ロビーを使う設定 (<see cref="LobbyLaunchOptions.IsRequested"/>)。</param>
        /// <param name="version">自分のビルドの版 (Application.version)。quickmatch でサーバーの版と比べる。</param>
        public LobbyLauncher(LobbyService service, LobbyLaunchOptions options, string version)
            : this(service, options, version, DefaultSchedule(options))
        {
        }

        /// <param name="service">ログイン前のロビーのサービス。</param>
        /// <param name="options">ロビーを使う設定。</param>
        /// <param name="schedule">検索し直す間隔と、全体を諦めるまでの期限。</param>
        public LobbyLauncher(LobbyService service, LobbyLaunchOptions options, RetrySchedule schedule)
            : this(service, options, null, schedule)
        {
        }

        /// <param name="service">ログイン前のロビーのサービス。</param>
        /// <param name="options">ロビーを使う設定。</param>
        /// <param name="version">自分のビルドの版。空なら "0" として比べる。</param>
        /// <param name="schedule">検索し直す間隔と、全体を諦めるまでの期限。</param>
        public LobbyLauncher(LobbyService service, LobbyLaunchOptions options, string version, RetrySchedule schedule)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            _version = version;
            if (!options.IsRequested)
            {
                throw new ArgumentException("options must request a lobby (-lobby create|join|quickmatch)", nameof(options));
            }
        }

        private enum Step
        {
            Idle,
            LoggingIn,
            Creating,
            Searching,
            WaitingToSearch,
            Joining,
            Ready,
            Failed,
        }

        /// <summary>起動設定を返したか、諦めたか。</summary>
        public bool IsFinished => _step == Step.Ready || _step == Step.Failed;

        /// <summary>手順を始める。<paramref name="ready"/> か <paramref name="failed"/> のどちらかを 1 回だけ呼ぶ。</summary>
        /// <param name="now">今の時刻 (秒)。<see cref="Tick"/> と同じ時計で渡す。</param>
        /// <param name="ready">ロビーが決まったときに、ネットワークの起動設定を受け取る。</param>
        /// <param name="failed">諦めたときに理由を受け取る。</param>
        public void Begin(float now, Action<NetLaunchOptions> ready, Action<string> failed)
        {
            if (_step != Step.Idle)
            {
                throw new InvalidOperationException("the lobby launcher has already begun");
            }

            _ready = ready ?? throw new ArgumentNullException(nameof(ready));
            _failed = failed ?? throw new ArgumentNullException(nameof(failed));
            _now = now;
            _schedule.Start(now);
            _step = Step.LoggingIn;
            Debug.Log(LobbyService.LogPrefix + "logging in to " + _options);
            _service.Login(HandleLoggedIn);
        }

        /// <summary>サービスを進め、待っている検索をやり直し、期限を見る。毎フレーム呼ぶ。終わった後も呼んでよい。</summary>
        public void Tick(float now)
        {
            _now = now;
            _service.Tick();

            if (IsFinished || _step == Step.Idle)
            {
                return;
            }

            if (_schedule.IsExpired(now))
            {
                if (_options.Command == LobbyCommand.QuickMatch && (_step == Step.Searching || _step == Step.WaitingToSearch))
                {
                    Fail("no quick match server found after " + _schedule.TimeoutSeconds + "s"
                        + (_lastRetryReason == null ? "" : " (" + _lastRetryReason + ")"));
                    return;
                }

                Fail("gave up after " + _schedule.TimeoutSeconds + "s while " + Describe(_step));
                return;
            }

            if (_step == Step.WaitingToSearch && _schedule.IsDue(now))
            {
                Search();
            }
        }

        private void HandleLoggedIn(string userId, string error)
        {
            if (_step != Step.LoggingIn)
            {
                return;
            }

            if (error != null)
            {
                Fail("EOS login failed: " + error);
                return;
            }

            switch (_options.Command)
            {
                case LobbyCommand.Create:
                    _step = Step.Creating;
                    _service.CreateLobby(_options.MaxPlayers, HandleCreated);
                    break;
                case LobbyCommand.Join when _options.LobbyId != null:
                    Join(_options.LobbyId);
                    break;
                default:
                    Search();
                    break;
            }
        }

        private void HandleCreated(LobbyInfo lobby, string error)
        {
            if (_step != Step.Creating)
            {
                return;
            }

            if (error != null)
            {
                Fail("creating the lobby failed: " + error);
                return;
            }

            Succeed(lobby);
        }

        private void Search()
        {
            _step = Step.Searching;
            _service.SearchLobbies(HandleSearched);
        }

        private void HandleSearched(IReadOnlyList<LobbyInfo> lobbies, string error)
        {
            if (_step != Step.Searching)
            {
                return;
            }

            if (error != null)
            {
                Retry("search failed: " + error);
                return;
            }

            if (_options.Command == LobbyCommand.QuickMatch)
            {
                ConnectToServer(lobbies);
                return;
            }

            var lobby = LobbySelector.PickJoinable(lobbies, _service.LocalUserId);
            if (lobby == null)
            {
                var own = 0;
                foreach (var found in lobbies)
                {
                    if (found != null && found.IsOwnedBy(_service.LocalUserId))
                    {
                        own++;
                    }
                }

                // Device ID のユーザーは端末・OS ユーザーごとに 1 つなので、同じ PC で並べて起動すると自分のロビーしか見えない
                var hint = own == 0
                    ? ""
                    : " (" + own + " of them are owned by this device's user; run the other instance on another machine or OS user)";
                Retry("no joinable lobby among " + lobbies.Count + " found" + hint);
                return;
            }

            Join(lobby.Id);
        }

        /// <summary>専用サーバーのロビーから 1 つ選び、その公開アドレスへつなぐ設定を返す。無ければ探し直す。</summary>
        private void ConnectToServer(IReadOnlyList<LobbyInfo> lobbies)
        {
            var lobby = LobbySelector.PickServer(lobbies, _version);
            if (lobby == null)
            {
                Retry(LobbySelector.DescribeNoServer(lobbies, _version));
                return;
            }

            lobby.TryGetText(ServerLobby.ServerKey, out var endpoint);
            NetLaunchOptions options;
            try
            {
                options = _options.ToServerNetOptions(endpoint);
            }
            catch (ArgumentException e)
            {
                // PickServer がアドレスの形を確かめているので、ここに来るのは規則が食い違ったときだけ
                Retry(e.Message);
                return;
            }

            lobby.TryGetNumber(ServerLobby.PlayersKey, out var players);
            _step = Step.Ready;
            Debug.Log(LobbyService.LogPrefix + "quick match picked id=" + lobby.Id + " server=" + endpoint
                + " players=" + players + "/" + lobby.MaxMembers + "; starting " + options);
            _ready(options);
        }

        private void Join(string lobbyId)
        {
            _step = Step.Joining;
            _service.JoinLobby(lobbyId, HandleJoined);
        }

        private void HandleJoined(LobbyInfo lobby, string error)
        {
            if (_step != Step.Joining)
            {
                return;
            }

            if (error != null)
            {
                // 指定されたロビーは他に候補が無いので諦める。探して選んだものなら、満員・解散の間に合わなかっただけかもしれない
                if (_options.LobbyId != null)
                {
                    Fail("joining lobby " + _options.LobbyId + " failed: " + error);
                    return;
                }

                Retry("joining failed: " + error);
                return;
            }

            if (lobby.IsOwnedBy(_service.LocalUserId))
            {
                Fail("lobby " + lobby.Id + " is owned by this user; a client cannot connect to itself");
                return;
            }

            Succeed(lobby);
        }

        private void Retry(string reason)
        {
            _lastRetryReason = reason;
            _step = Step.WaitingToSearch;
            _schedule.Wait(_now);
            Debug.Log(LobbyService.LogPrefix + reason + "; searching again in " + _schedule.IntervalSeconds + "s");
        }

        private void Succeed(LobbyInfo lobby)
        {
            NetLaunchOptions options;
            try
            {
                options = _options.ToNetOptions(lobby.OwnerUserId);
            }
            catch (ArgumentException e)
            {
                Fail(e.Message);
                return;
            }

            _step = Step.Ready;
            Debug.Log(LobbyService.LogPrefix + "ready id=" + lobby.Id + "; starting " + options);
            _ready(options);
        }

        private void Fail(string reason)
        {
            if (IsFinished)
            {
                return;
            }

            _step = Step.Failed;
            _service.LeaveLobby();
            _failed(reason);
        }

        private static RetrySchedule DefaultSchedule(LobbyLaunchOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            return new RetrySchedule(options.SearchInterval, options.Timeout);
        }

        private static string Describe(Step step)
        {
            switch (step)
            {
                case Step.LoggingIn:
                    return "logging in";
                case Step.Creating:
                    return "creating the lobby";
                case Step.Searching:
                case Step.WaitingToSearch:
                    return "searching for a lobby";
                case Step.Joining:
                    return "joining the lobby";
                default:
                    return step.ToString();
            }
        }
    }
}
