using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Fps.Core;
using Fps.Protocol;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.NetV2
{
    /// <summary>
    /// <c>-mode client -netstack v2</c> のクライアント。Fps.Protocol と LiteNetLib (<see cref="UdpMatchTransport"/>) で .NET のサーバーへつなぎ、
    /// 承認されたらサーバーのマップのシーンへ移って、自分のプレイヤー (<see cref="NetV2LocalPlayer"/>)・他のプレイヤー
    /// (<see cref="NetV2RemotePlayer"/>)・HUD (<see cref="NetV2Hud"/>) を作る。シーンをまたいで残る。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 自分の移動は Core の移動で予測して入力を送り、スナップショットで照合する。他のプレイヤーは補間して出す (<see cref="ClientMatch"/>)。
    /// 当たりは Netcode のクライアントと同じく、マップの <c>.fpsmap</c> (<see cref="MapCollisionFiles"/>) で取る。
    /// 移動の値はサーバーのルールのファイルに書き出す元 (Resources の <see cref="MotorSettings"/> と、対戦のプレイヤーの寸法の既定
    /// <see cref="NetMotorSettings.Default"/>) から作る。
    /// </para>
    /// <para>
    /// 射撃・リロード・グレネード・印は押したときに要求を送り、承認されたら保存してあるロードアウト (<see cref="LoadoutPreferenceStore"/>) を頼む。
    /// サーバーのイベントは、オフラインと Netcode のクライアントと同じ <see cref="CombatEvents"/> と HUD に流し、<c>[Combat]</c> の行を出す。
    /// </para>
    /// <para>
    /// サーバーとの接続を失ったら (つながらない・断られたを含む)、Netcode のクライアントと同じ理由の文 (<see cref="DisconnectNotice"/>) を添えてメニューへ戻る。
    /// batchmode のプレイヤーでは Netcode のクライアントと同じ終了コードで終わる。
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class NetV2Client : MonoBehaviour
    {
        public const string ObjectName = "NetV2Client";
        public const string LogTag = "[Net]";
        public const string CombatLogTag = "[Combat]";

        // Netcode のクライアント (NetBootstrap) と同じ終了コード
        public const int ExitDisconnected = 0;
        public const int ExitInvalidArguments = 2;
        public const int ExitMisconfigured = 3;
        public const int ExitConnectFailed = 5;
        public const int ExitTransportFailure = 6;

        private const string _sceneExtension = ".unity";

        // サーバー (ServerMotorSettings.DefaultKillPlaneY) と Netcode のクライアント (NetworkPlayerMotor.KillPlaneY) と同じ高さ
        private const float _killPlaneY = -50f;

        /// <summary>このプロセスの端末の Id。Netcode のクライアントと同じく、同じプロセスのままつなぎ直したときだけ成績を引き継ぐ。</summary>
        private static readonly string _deviceKey = RejoinKey.NewDeviceKey();

        private readonly Dictionary<int, NetV2RemotePlayer> _remotes = new Dictionary<int, NetV2RemotePlayer>();
        private readonly FireRequestMessage _fire = new FireRequestMessage();
        private readonly ThrowGrenadeRequestMessage _grenade = new ThrowGrenadeRequestMessage();
        private readonly PingRequestMessage _ping = new PingRequestMessage();
        private readonly WeaponActionRequestMessage _weaponAction = new WeaponActionRequestMessage();
        private readonly LoadoutRequestMessage _loadout = new LoadoutRequestMessage();
        private NetLaunchOptions _options;
        private MatchClient _client;
        private ClientAntiCheat _antiCheat;
        private ClientMatch _match;
        private NetMotorSettings _motor;
        private NetV2LocalPlayer _local;
        private NetV2Hud _hud;
        private Transform _remoteRoot;
        private IMenuNavigator _menu;
        private AsyncOperation _mapLoad;
        private int _shotIndex;
        private int _shotGeneration;
        private bool _finished;

        public MatchClient Client => _client;

        public ClientMatch Match => _match;

        /// <summary><c>-netstack v2</c> で起動されたときだけ、最初のシーンを読んだ後にクライアントを作る。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LaunchWhenRequested()
        {
            if (NetStartup.IsDedicatedServerBuild || !NetStackLaunch.RequestsV2(Environment.GetCommandLineArgs()))
            {
                return;
            }

            var host = new GameObject(ObjectName);
            DontDestroyOnLoad(host);
            host.AddComponent<NetV2Client>();
        }

        private void Awake()
        {
            // サーバーと同じマシンで並べて動かすので、裏に回っても止めない
            Application.runInBackground = true;
            if (NetStartup.IsHeadlessPlayer(Application.isEditor, Application.isBatchMode, false))
            {
                Application.targetFrameRate = 60;
            }

            _menu = GameUi.Menu;
            if (_menu != null)
            {
                _menu.ReturningToMenu += HandleReturningToMenu;
            }

            if (!NetStackLaunch.TryResolveV2Client(Environment.GetCommandLineArgs(), out _options, out var error))
            {
                Leave(ExitInvalidArguments, LogTag + " invalid arguments: " + error);
                return;
            }

            if (!BuildVersion.TryNormalize(Application.version, out var version, out var versionError))
            {
                Leave(ExitMisconfigured, LogTag + " cannot read the build version (Application.version): " + versionError);
                return;
            }

            if (!AntiCheatSettings.TryResolve(Environment.GetCommandLineArgs(), Application.isEditor, _options, out var antiCheat, out var antiCheatError))
            {
                Leave(ExitInvalidArguments, AntiCheatSettings.LogTag + " invalid arguments: " + antiCheatError);
                return;
            }

            Debug.Log(antiCheat.Describe(), this);
            _motor = new NetMotorSettings(MotorSettings.ResolveOrDefault(null), NetMotorSettings.Default.Stance, NetMotorSettings.Default.Mover);
            _client = new MatchClient(new UdpMatchTransport(), version, _deviceKey, "", line => Debug.Log(line, this));
            _client.Accepted += HandleAccepted;
            _client.SnapshotReceived += HandleSnapshot;
            _client.MessageReceived += HandleMessage;
            _client.Disconnected += HandleDisconnected;
            if (!NetV2AntiCheat.TryCreate(antiCheat, _client, HandleAntiCheatRemoved, out _antiCheat, out antiCheatError))
            {
                Leave(ExitMisconfigured, AntiCheatSettings.LogTag + " " + antiCheatError);
                return;
            }

            Debug.Log(LogTag + " starting " + _options + " transport=udp " + NetStackLaunch.Flag + " " + NetStackLaunch.V2Name, this);
            try
            {
                _client.Connect(_options.Address, _options.Port);
            }
            catch (Exception e) when (e is InvalidOperationException || e is ArgumentException)
            {
                Leave(ExitConnectFailed, LogTag + " failed to start " + _options + ": " + e.Message);
            }
        }

        private void Update()
        {
            if (_client == null || _finished)
            {
                return;
            }

            _client.Poll();
            if (_finished)
            {
                return;
            }

            _antiCheat.Tick(Time.realtimeSinceStartupAsDouble);
            if (_finished)
            {
                return;
            }

            if (_mapLoad != null && _mapLoad.isDone)
            {
                _mapLoad = null;
                EnterMap();
            }

            if (_match == null)
            {
                return;
            }

            StepOwner(Time.deltaTime);
            ShowRemotes();
            SendRequests();
        }

        private void OnDestroy()
        {
            if (_menu != null)
            {
                _menu.ReturningToMenu -= HandleReturningToMenu;
                _menu = null;
            }

            _match?.Detach();
            _antiCheat?.End();
            _client?.Dispose();
            DestroyViews();
        }

        private void HandleAccepted()
        {
            if (!_antiCheat.ServerConnected(Time.realtimeSinceStartupAsDouble, out var antiCheatError))
            {
                Leave(ExitTransportFailure, AntiCheatSettings.LogTag + " " + antiCheatError + NetV2AntiCheat.LaunchHint);
                return;
            }

            SendSavedLoadout();
            var map = _client.Map;
            if (SceneManager.GetActiveScene().name == map)
            {
                EnterMap();
                return;
            }

            if (!TryFindScene(map, out var path))
            {
                Leave(ExitMisconfigured, LogTag + " the server's map " + map + " is not a scene in this build; build the map and rebuild");
                return;
            }

            Debug.Log(LogTag + " loading the server's map " + map + " (" + path + ")", this);
            _mapLoad = SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
        }

        private void EnterMap()
        {
            var map = _client.Map;
            if (!TryLoadWorld(map, out var world, out var error))
            {
                Leave(ExitMisconfigured, error);
                return;
            }

            var body = new CharacterBody(world, _motor.Mover, _motor.Stance);
            var simulation = new CharacterMotorSimulation(
                body, new PlayerMotor(_motor.Motor), 1f / _client.TickRate, _killPlaneY);
            _match = new ClientMatch(_client, simulation);
            _match.PlayerAdded += AddRemote;
            _match.PlayerRemoved += RemoveRemote;
            _match.Reconciled += HandleReconciled;
            _remoteRoot = new GameObject("NetV2Players").transform;
            foreach (var id in _match.RemotePlayerIds)
            {
                AddRemote(id);
            }

            try
            {
                _hud = NetV2Hud.Create(_client.PlayerId, _client.MaxHealth);
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError(LogTag + " cannot create the HUD: " + e.Message, this);
            }

            if (_client.LatestSnapshot != null)
            {
                _hud?.Apply(_client.LatestSnapshot);
            }

            Debug.Log(LogTag + " synchronized with the server; map " + map, this);
        }

        private bool TryLoadWorld(string map, out IWorldCollision world, out string error)
        {
            world = null;
            var path = MapCollisionFiles.PathOf(map);
            if (!File.Exists(path))
            {
                error = LogTag + " map " + map + " has no collision: " + path
                    + " does not exist; export it with make export-server-data (Fps/Net/Export Server Data) and rebuild";
                return false;
            }

            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var file = FpsMapFile.Read(stream);
                    world = new BvhWorldCollision(file);
                    Debug.Log(LogTag + " map collision " + map + " colliders=" + file.Colliders.Count.ToString(CultureInfo.InvariantCulture), this);
                }
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                error = LogTag + " cannot read " + path + ": " + e.Message;
                return false;
            }

            error = null;
            return true;
        }

        private void StepOwner(float deltaSeconds)
        {
            var ticks = _match.Advance(deltaSeconds);
            if (!_match.IsPredicting)
            {
                return;
            }

            if (_local == null)
            {
                var start = _match.LocalState;
                _local = NetV2LocalPlayer.Create(
                    _motor.Stance, _match.Prediction.Simulation.Body.HasHeadroomToStand, NetV2Math.ToUnity(start.Position), OwnYaw());
            }

            for (var i = 0; i < ticks; i++)
            {
                _local.Sample(_match.LocalState.IsGrounded, out var move, out var yaw, out var buttons);
                _match.Step(NetV2Math.ToNumerics(move), yaw, buttons);
            }

            _match.SendInput();
            _local.Show(_match.LocalState);
        }

        private float OwnYaw()
        {
            var latest = _client.LatestSnapshot;
            var own = latest != null ? latest.Players.IndexOf(_client.PlayerId) : -1;
            return own >= 0 ? latest.Players[own].Yaw : 0f;
        }

        private void ShowRemotes()
        {
            foreach (var pair in _remotes)
            {
                if (_match.TrySampleRemote(pair.Key, out var pose))
                {
                    pair.Value.Show(pose);
                }
            }
        }

        private void SendRequests()
        {
            if (_local == null)
            {
                return;
            }

            var direction = NetV2Math.ToNumerics(_local.AimDirection);
            if (_local.TakeAttack())
            {
                SendFire(direction);
            }

            if (_local.TakeReload())
            {
                _weaponAction.Action = WeaponAction.Reload;
                _client.Send(_weaponAction, TransportChannel.Reliable);
            }

            if (_local.TakeGrenade())
            {
                _grenade.Direction = direction;
                _client.Send(_grenade, TransportChannel.Reliable);
            }

            if (_local.TakePing())
            {
                _ping.Direction = direction;
                _client.Send(_ping, TransportChannel.Reliable);
            }
        }

        // 撃てるか (弾・間隔) と当たりはサーバーが決める。表示している tick を載せ、サーバーがその時点へ巻き戻して当てる
        private void SendFire(System.Numerics.Vector3 direction)
        {
            var latest = _client.LatestSnapshot;
            if (latest == null || !latest.HasOwner)
            {
                return;
            }

            var owner = latest.Owner;
            var index = owner.Weapon.EquippedIndex;
            if (index >= owner.EquippedWeaponIds.Count)
            {
                return;
            }

            var renderTick = Math.Max(0f, _match.Interpolation.RenderTick);
            _fire.WeaponId = owner.EquippedWeaponIds[index];
            _fire.Origin = NetV2Math.ToNumerics(_local.EyePosition);
            _fire.Direction = direction;
            _fire.ClientTick = (int)renderTick;
            _fire.InterpolationAlpha = renderTick - (int)renderTick;
            _fire.ShotGeneration = _shotGeneration;
            _fire.ShotIndex = _shotIndex;
            _shotIndex = unchecked(_shotIndex + 1);
            _client.Send(_fire, TransportChannel.Reliable);
            CombatEvents.RaiseShotFired(_local.gameObject, _local.EyePosition, _local.AimDirection, _fire.WeaponId);
        }

        private void SendSavedLoadout()
        {
            IReadOnlyList<string> saved;
            try
            {
                saved = new LoadoutPreferenceStore().Load();
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                Debug.LogWarning(LogTag + " ignored the saved loadout: " + e.Message, this);
                return;
            }

            if (saved == null || saved.Count == 0)
            {
                return;
            }

            _loadout.WeaponIds.Clear();
            foreach (var id in saved)
            {
                if (_loadout.WeaponIds.Count == _loadout.WeaponIds.MaxCount)
                {
                    break;
                }

                _loadout.WeaponIds.Add(id);
            }

            _client.Send(_loadout, TransportChannel.Reliable);
        }

        private void HandleSnapshot(SnapshotMessage snapshot)
        {
            if (_hud != null)
            {
                _hud.Apply(snapshot);
            }
        }

        private void HandleReconciled(ReconcileResult result)
        {
            if (result.Outcome == ReconcileOutcome.Corrected)
            {
                Debug.Log(LogTag + " reconciled tick=" + result.Tick.ToString(CultureInfo.InvariantCulture)
                    + " error=" + result.Error.ToString("F3", CultureInfo.InvariantCulture)
                    + " resimulated=" + result.ResimulatedTicks.ToString(CultureInfo.InvariantCulture), this);
            }
        }

        private void HandleMessage(IMessage message)
        {
            switch (message)
            {
                case AntiCheatToClientMessage antiCheat:
                    _antiCheat.HandleMessage(antiCheat, Time.realtimeSinceStartupAsDouble);
                    break;
                case KilledMessage killed:
                    var entry = new KillFeedEntry(killed.KillerId, killed.VictimId, killed.WeaponId, killed.Headshot, killed.Friendly);
                    Debug.Log(CombatLogTag + " Killed " + entry, this);
                    if (killed.VictimId == _client.PlayerId && _local != null)
                    {
                        _local.ReleaseHolds(CrouchInput.ReasonDeath);
                    }

                    CombatEvents.RaiseKilled(entry);
                    break;
                case DamageTakenMessage damage:
                    Vector3? source = damage.HasSource ? NetV2Math.ToUnity(damage.SourcePosition) : (Vector3?)null;
                    Debug.Log(CombatLogTag + " DamageTaken amount=" + damage.Amount.ToString(CultureInfo.InvariantCulture)
                        + " source=" + (source.HasValue ? source.Value.ToString("F2", CultureInfo.InvariantCulture) : "none"), this);
                    CombatEvents.RaiseDamageTaken(source, Math.Max(0, damage.Amount));
                    break;
                case HitConfirmMessage hit:
                    var confirm = new HitConfirm(Math.Max(0, hit.Damage), hit.Zone, hit.Killed, hit.Downed);
                    Debug.Log(CombatLogTag + " HitConfirmed damage=" + confirm.Damage.ToString(CultureInfo.InvariantCulture)
                        + " zone=" + confirm.Zone + " killed=" + confirm.Killed + " downed=" + confirm.Downed, this);
                    CombatEvents.RaiseHitConfirmed(confirm);
                    break;
                case ShotMessage shot:
                    ShowShot(shot);
                    break;
                case ExplosionMessage explosion:
                    var center = NetV2Math.ToUnity(explosion.Center);
                    Debug.Log(CombatLogTag + " Explosion weapon=" + explosion.WeaponId + " at=" + center.ToString("F2", CultureInfo.InvariantCulture)
                        + " thrower=" + (explosion.HasThrower ? explosion.ThrowerId.ToString(CultureInfo.InvariantCulture) : "none"), this);
                    ThrownGrenade.PlayExplosionEffects(center);
                    CombatEvents.RaiseExploded(center, explosion.WeaponId);
                    break;
                case TeleportedMessage teleported:
                    HandleTeleported(teleported);
                    break;
                case ShotIndexCorrectedMessage corrected:
                    // Netcode のクライアント (NetShotSequence.Correct) と同じく、まだサーバーが数えていない弾の分も含めて同じだけずらす
                    if (corrected.Generation != _shotGeneration)
                    {
                        _shotIndex = unchecked(_shotIndex + (corrected.ServerIndex - corrected.ClientIndex));
                        _shotGeneration = corrected.Generation;
                    }

                    break;
                case IdleWarningMessage idle:
                    if (idle.Active)
                    {
                        _hud?.ShowIdleWarning(idle.RemainingSeconds);
                    }
                    else
                    {
                        _hud?.ClearIdleWarning();
                    }

                    break;
                case LoadoutAnswerMessage answer:
                    Debug.Log("[Loadout] server " + (answer.Accepted ? "accepted the loadout" : "refused the loadout: " + answer.Reason), this);
                    break;
                case PingPlacedMessage placed:
                    Debug.Log("[Ping] placed id=" + placed.PingId.ToString(CultureInfo.InvariantCulture) + " kind=" + placed.Kind
                        + " at=" + NetV2Math.ToUnity(placed.Position).ToString("F2", CultureInfo.InvariantCulture), this);
                    break;
                case PingRemovedMessage removed:
                    Debug.Log("[Ping] removed id=" + removed.PingId.ToString(CultureInfo.InvariantCulture) + " reason=" + removed.Reason, this);
                    break;
            }
        }

        private void ShowShot(ShotMessage shot)
        {
            GameObject shooter = null;
            if (shot.HasShooter && _remotes.TryGetValue(shot.ShooterId, out var remote))
            {
                shooter = remote.gameObject;
            }

            CombatEvents.RaiseShotFired(shooter, NetV2Math.ToUnity(shot.Origin), NetV2Math.ToUnity(shot.Direction), shot.WeaponId);
            if (shot.HasImpact)
            {
                HitZone? zone = shot.ImpactIsCharacter && shot.HasImpactZone ? shot.ImpactZone : (HitZone?)null;
                CombatEvents.RaiseImpact(new ImpactInfo(
                    NetV2Math.ToUnity(shot.ImpactPoint), NetV2Math.ToUnity(shot.ImpactNormal), null, shot.ImpactMaterialId, shot.ImpactIsCharacter, zone));
            }
        }

        private void HandleTeleported(TeleportedMessage teleported)
        {
            if (teleported.PlayerId != _client.PlayerId)
            {
                _match?.ForgetRemoteHistory(teleported.PlayerId);
                return;
            }

            // サーバーは置き直した位置を落ちたときに戻す先にもする (ServerMovement.Teleport)
            if (_match != null)
            {
                _match.Prediction.Simulation.SpawnPosition = teleported.Position;
            }

            _local?.ReleaseHolds(CrouchInput.ReasonRespawn);
        }

        private void AddRemote(int playerId)
        {
            if (_remoteRoot == null || _remotes.ContainsKey(playerId))
            {
                return;
            }

            _remotes.Add(playerId, NetV2RemotePlayer.Create(playerId, _motor.Stance, _remoteRoot));
            Debug.Log(LogTag + " player " + playerId.ToString(CultureInfo.InvariantCulture) + " is in view", this);
        }

        private void RemoveRemote(int playerId)
        {
            if (!_remotes.TryGetValue(playerId, out var remote))
            {
                return;
            }

            _remotes.Remove(playerId);
            if (remote != null)
            {
                Destroy(remote.gameObject);
            }

            Debug.Log(LogTag + " player " + playerId.ToString(CultureInfo.InvariantCulture) + " left", this);
        }

        // サーバーに切られる前にこちらから抜けても、サーバーが EAC で切ったときと同じ符号で同じ文を出す
        private void HandleAntiCheatRemoved(string reason)
        {
            if (_finished || _client == null)
            {
                return;
            }

            var wasConnected = _client.State == MatchClientState.Connected;
            _client.Leave();
            HandleDisconnected(DisconnectNotice.Classify(
                DisconnectNotice.RejectedReason(AntiCheatSettings.RejectedDetail), wasConnected, _client.ClientVersion));
        }

        private void HandleDisconnected(DisconnectNotice notice)
        {
            _antiCheat?.End();
            if (!notice.WasConnected)
            {
                Leave(ExitConnectFailed, LogTag + " failed to connect to " + _options.Endpoint + " " + notice.Describe(), notice);
                return;
            }

            Debug.Log(LogTag + " disconnected from server " + notice.Describe(), this);
            Finish(ExitDisconnected, notice.Message);
        }

        private void HandleReturningToMenu()
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            Debug.Log(LogTag + " leaving for the menu; shutting down the network", this);
            _client?.Leave();
            Destroy(gameObject);
        }

        // 接続を失ったのでなければ、Netcode のクライアントを起動引数で動かしたときと同じく、このシーンに留まってログだけ残す
        private void Leave(int exitCode, string message, DisconnectNotice notice = null)
        {
            Debug.LogError(message, this);
            _antiCheat?.End();
            _client?.Leave();
            if (notice != null)
            {
                Finish(exitCode, notice.Message);
                return;
            }

            _finished = true;
            if (NetStartup.IsHeadlessPlayer(Application.isEditor, Application.isBatchMode, false))
            {
                Application.Quit(exitCode);
            }
        }

        // メニューへ戻る。起動引数でつないだときも、残っても遊べないので戻す (batchmode では先に終了する)
        private void Finish(int exitCode, LocalizedText notice)
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            if (NetStartup.IsHeadlessPlayer(Application.isEditor, Application.isBatchMode, false))
            {
                Application.Quit(exitCode);
                return;
            }

            var menu = _menu ?? GameUi.Menu;
            if (menu == null)
            {
                Debug.LogError(LogTag + " cannot return to the menu: no menu navigator is registered with GameUi.Menu; staying in this scene", this);
                return;
            }

            // 戻る前に消す。メニューへ戻る知らせ (ReturningToMenu) で自分を消し直さないため
            if (_menu != null)
            {
                _menu.ReturningToMenu -= HandleReturningToMenu;
                _menu = null;
            }

            menu.ReturnToMenu(notice);
            Destroy(gameObject);
        }

        private void DestroyViews()
        {
            if (_local != null)
            {
                Destroy(_local.gameObject);
            }

            if (_hud != null)
            {
                Destroy(_hud.gameObject);
            }

            if (_remoteRoot != null)
            {
                Destroy(_remoteRoot.gameObject);
            }

            _remotes.Clear();
        }

        private static bool TryFindScene(string map, out string path)
        {
            for (var i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var candidate = SceneUtility.GetScenePathByBuildIndex(i);
                var normalized = candidate.Replace('\\', '/');
                if (normalized.EndsWith(_sceneExtension, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(Path.GetFileNameWithoutExtension(normalized), map, StringComparison.Ordinal))
                {
                    path = candidate;
                    return true;
                }
            }

            path = null;
            return false;
        }
    }
}
