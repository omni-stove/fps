using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Shooter.Core;

namespace Shooter.Server.Eos
{
    // やり直しの間隔・諦める期限・ログの行を Unity の Shooter.Net.ServerLobbyRegistrar と同じにし、運用の手順と監視をそのまま使えるようにする。
    // 起動してから一度も登録できないまま期限を過ぎたら、認証情報や設定の誤りとみなして諦める。
    // 一度登録できた後は試合を止めないよう、期限を過ぎてもエラーを出しながら作り直しを続ける。
    internal sealed class ServerLobbyRegistrar
    {
        // 退出の通知が来ないままロビーが消えていることがあるので、更新がこの回数続けて失敗したら作り直す。
        public const int MaxUpdateFailures = 3;

        private const string _logPrefix = ServerLobbyService.LogPrefix;

        private readonly ServerLobbyService _service;
        private readonly string _endpoint;
        private readonly string _version;
        private readonly int _maxPlayers;
        private readonly NetMatchSettings _match;
        private readonly RetrySchedule _schedule;
        private readonly TextWriter _log;
        private Action<string> _failed;
        private string _region;
        private Step _step;
        private float _now;
        private int _players;
        private int _publishedPlayers;
        private int? _skillAverage;
        private int? _publishedSkillAverage;
        private string _map;
        private string _publishedMap;
        private string _mode;
        private string _publishedMode;
        private int _updateFailures;
        private int _lobbyGeneration;
        private float _nextUpdateTime;
        private string _lastError;
        private bool _failedLeaveDone;
        private Action _failedLeaveWaiters;

        public ServerLobbyRegistrar(
            ServerLobbyService service, string endpoint, string version, int maxPlayers, RetrySchedule schedule, TextWriter log)
            : this(service, endpoint, version, maxPlayers, NetMatchSettings.Default, schedule, log)
        {
        }

        public ServerLobbyRegistrar(
            ServerLobbyService service, string endpoint, string version, NetMatchSettings match, RetrySchedule schedule, TextWriter log)
            : this(service, endpoint, version, (match ?? throw new ArgumentNullException(nameof(match))).MaxPlayers, match, schedule, log)
        {
        }

        private ServerLobbyRegistrar(
            ServerLobbyService service, string endpoint, string version, int maxPlayers, NetMatchSettings match, RetrySchedule schedule, TextWriter log)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _match = match;
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            if (!NetLaunchOptions.TryParseEndpoint(endpoint, out _, out _))
            {
                throw new ArgumentException("must be an IPv4 address:port that clients can connect to (got '" + endpoint + "')", nameof(endpoint));
            }

            if (maxPlayers < LobbyLaunchOptions.MinMaxPlayers || maxPlayers > LobbyLaunchOptions.MaxMaxPlayers)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxPlayers), maxPlayers,
                    "must be between " + LobbyLaunchOptions.MinMaxPlayers + " and " + LobbyLaunchOptions.MaxMaxPlayers);
            }

            _endpoint = endpoint;
            _version = version;
            _maxPlayers = maxPlayers;
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

        public int MaxPlayers => _maxPlayers;

        public int PublishedPlayers => IsRegistered ? _publishedPlayers : -1;

        public int? SkillAverage => _skillAverage;

        public int? PublishedSkillAverage => IsRegistered ? _publishedSkillAverage : null;

        public string Map => _map;

        public string PublishedMap => IsRegistered ? _publishedMap : null;

        public string Mode => _mode;

        public string PublishedMode => IsRegistered ? _publishedMode : null;

        // 地域は作るときにだけ載せ、PublishChanges では送らない。起動の間は変わらない値を、人が入るたびに書き直さないため。
        public string Region
        {
            get => _region;
            set
            {
                if (_step != Step.Idle)
                {
                    throw new InvalidOperationException("set Region before the server lobby registrar begins");
                }

                if (value != null && !ServerLobby.IsRegionName(value))
                {
                    throw new ArgumentException("'" + value + "' is not a server region (" + ServerLobby.RegionRule + ")", nameof(value));
                }

                _region = value;
            }
        }

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
            _log.WriteLine(_logPrefix + "registering server=" + _endpoint + " bucket=" + ServerLobby.BucketId(_version) + RegionText());
            Attempt();
        }

        public void SetPlayers(int players)
        {
            SetPlayers(players, null);
        }

        // 人数と平均を一度に受けるのは、人が入るたびに属性の更新を 2 回に分けて送らないようにするため。
        // EOS の属性を消す口が無いので、値のある人が誰もいなくなった (null) ときは前の平均を載せたままにする。
        public void SetPlayers(int players, int? skillAverage)
        {
            if (players < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(players), players, "must not be negative");
            }

            var changed = false;
            if (players != _players)
            {
                _log.WriteLine(_logPrefix + "players " + _players + " -> " + players);
                _players = players;
                changed = true;
            }

            if (skillAverage.HasValue && skillAverage != _skillAverage)
            {
                _log.WriteLine(_logPrefix + "skill average " + SkillText(_skillAverage) + " -> " + SkillText(skillAverage));
                _skillAverage = skillAverage;
                changed = true;
            }

            if (changed)
            {
                PublishChanges();
            }
        }

        // サーバー一覧に今の地図を出すため、地図が替わるたびに書き直す。
        // 人数と同じく EOS の属性を消す口が無いので、null (地図の無いサーバー) は前の値を載せたままにする。
        public void SetMap(string map)
        {
            if (map == null || string.Equals(map, _map, StringComparison.Ordinal))
            {
                return;
            }

            if (!NetLaunchOptions.IsMapName(map))
            {
                throw new ArgumentException("'" + map + "' is not a map name", nameof(map));
            }

            _log.WriteLine(_logPrefix + "map " + (_map ?? "none") + " -> " + map);
            _map = map;
            PublishChanges();
        }

        // サーバー一覧に今のモードを出すため、次の試合のルールで目的が替わるたびに書き直す。
        // 地図と同じく null は前の値を載せたままにする。一度も渡されていなければ ServerLobby.Attributes が TeamDeathmatchMode を載せる。
        public void SetMode(string mode)
        {
            if (mode == null || string.Equals(mode, _mode, StringComparison.Ordinal))
            {
                return;
            }

            if (!ServerLobby.IsModeName(mode))
            {
                throw new ArgumentException("'" + mode + "' is not a server mode (" + ServerLobby.ModeRule + ")", nameof(mode));
            }

            _log.WriteLine(_logPrefix + "mode " + (_mode ?? "none") + " -> " + mode);
            _mode = mode;
            PublishChanges();
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
                    PublishChanges();
                    break;
            }
        }

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
            var skillAverage = _skillAverage;
            var map = _map;
            var mode = _mode;
            // ServerLobby.Attributes が voice_enabled=1 を載せるので、RTC 部屋もいつも開く
            _service.CreateLobby(
                ServerLobby.MaxMembers(_maxPlayers),
                true,
                ServerLobby.Attributes(_endpoint, _version, players, _match, skillAverage, _region, map, mode),
                (lobby, error) => HandleCreated(lobby, error, players, skillAverage, map, mode));
        }

        private void HandleCreated(LobbyInfo lobby, string error, int players, int? skillAverage, string map, string mode)
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
            _publishedSkillAverage = skillAverage;
            _publishedMap = map;
            // 渡されていないときも ServerLobby.Attributes が載せた値を覚え、後から同じモードが渡されても送り直さない
            _publishedMode = mode ?? ServerLobby.TeamDeathmatchMode;
            _updateFailures = 0;
            _nextUpdateTime = _now;
            _lastError = null;
            HasRegistered = true;
            _log.WriteLine(_logPrefix + "registered id=" + lobby.Id + " server=" + _endpoint + " players=" + players
                + (skillAverage.HasValue ? " skill_average=" + SkillText(skillAverage) : "")
                + RegionText()
                + (map != null ? " map=" + map : "")
                + (mode != null ? " mode=" + mode : ""));
            PublishChanges();
        }

        // 変わった属性だけを送るのは、人数だけが変わるたびに同じ平均を書き直さないようにするため。
        private void PublishChanges()
        {
            if (_step != Step.Registered || _service.IsUpdating || _now < _nextUpdateTime)
            {
                return;
            }

            var players = _players;
            var skillAverage = _skillAverage;
            var map = _map;
            var mode = _mode;
            var attributes = new List<LobbyAttribute>(4);
            if (players != _publishedPlayers)
            {
                attributes.Add(ServerLobby.PlayersAttribute(players));
            }

            if (skillAverage.HasValue && skillAverage != _publishedSkillAverage)
            {
                attributes.Add(ServerLobby.SkillAverageAttribute(skillAverage.Value));
            }

            if (map != null && !string.Equals(map, _publishedMap, StringComparison.Ordinal))
            {
                attributes.Add(ServerLobby.MapAttribute(map));
            }

            if (mode != null && !string.Equals(mode, _publishedMode, StringComparison.Ordinal))
            {
                attributes.Add(ServerLobby.ModeAttribute(mode));
            }

            if (attributes.Count == 0)
            {
                return;
            }

            var generation = _lobbyGeneration;
            _service.UpdateAttributes(attributes, error => HandleUpdated(error, players, skillAverage, map, mode, generation));
        }

        private void HandleUpdated(string error, int players, int? skillAverage, string map, string mode, int generation)
        {
            // 失ったロビーへの更新の結果を、作り直したロビーの値として数えない
            if (_step != Step.Registered || generation != _lobbyGeneration)
            {
                PublishChanges();
                return;
            }

            if (error == null)
            {
                _publishedPlayers = players;
                if (skillAverage.HasValue)
                {
                    _publishedSkillAverage = skillAverage;
                }

                if (map != null)
                {
                    _publishedMap = map;
                }

                if (mode != null)
                {
                    _publishedMode = mode;
                }

                _updateFailures = 0;
                PublishChanges();
                return;
            }

            _updateFailures++;
            if (_updateFailures < MaxUpdateFailures)
            {
                _nextUpdateTime = _now + _schedule.IntervalSeconds;
                return;
            }

            _service.LeaveLobby();
            Lose("updating the lobby attributes failed " + _updateFailures + " times in a row: " + error);
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

        private string RegionText()
        {
            return _region == null ? "" : " region=" + _region;
        }

        private static string SkillText(int? skillAverage)
        {
            return skillAverage.HasValue ? skillAverage.Value.ToString(CultureInfo.InvariantCulture) : "-";
        }

        private void Unsubscribe()
        {
            _service.Removed -= HandleRemoved;
            _service.LoggedOut -= HandleLoggedOut;
        }
    }
}
