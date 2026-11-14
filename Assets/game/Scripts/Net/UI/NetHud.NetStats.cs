using Fps.Core;
using Fps.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetHud"/> のうち、サーバーとの回線の Ping とパケットロスを出す処理 (F3 で出し入れする)。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><see cref="NetStatsActionPath"/> (F3。ゲームパッドは割り当てなし) を押すたびに出し入れし、
    /// 出すかどうかを <see cref="ControlSettings.ShowNetStats"/> として <see cref="AppliedControlSettings"/> に入れて保存する。
    /// 起動し直しても前の状態で始まる。割り当ての入力を待っている間 (<see cref="PlayerBindings.IsCapturingInput"/>) は切り替えない。</item>
    /// <item>出している間、<see cref="NetStatsRefreshSeconds"/> ごとに右上 (<see cref="NetStatsView"/>) を書き直す。
    /// Ping はトランスポート (UnityTransport など) がサーバーとの間で測る往復時間 (GetCurrentRtt)。ホストはサーバーそのものなので 0。</item>
    /// <item>ロスは、<see cref="NetStatsProbeSeconds"/> ごとに自分のプレイヤーから信頼性なしの確かめの便
    /// (<see cref="NetPlayer.SendNetStatsProbe"/>) を送り、<see cref="NetLossMeter"/> で返事が来なかった割合を数える
    /// (行き・帰りのどちらの欠けも数える)。tick は返事に載ってくるサーバーの tick の最新。
    /// 便は出している間だけ送る。</item>
    /// </list>
    /// 時刻は <see cref="Tick"/> に渡された秒を積んだもの (Edit Mode でも同じに進めるため)。
    /// </remarks>
    public sealed partial class NetHud
    {
        /// <summary>回線の表示を出し入れするアクション。</summary>
        public const string NetStatsActionPath = InputActionPaths.NetStats;

        /// <summary>出している間、表示を書き直す間隔 (秒)。</summary>
        public const float NetStatsRefreshSeconds = 1f;

        /// <summary>出している間、ロスを測る確かめの便を送る間隔 (秒)。1 秒に 20 便で、ロスは 5% 刻みになる。</summary>
        public const float NetStatsProbeSeconds = 0.05f;

        private const string _netStatsLogTag = "[Net]";

        private readonly NetLossMeter _netLossMeter = new NetLossMeter();
        private NetStatsView _netStatsView;
        private bool _netStatsReady;
        private bool _netStatsShown;
        private bool _listeningForSettings;

        private InputAction _netStatsAction;
        private bool _resolvedNetStatsAction;
        private bool _enabledNetStatsAction;

        // 返事を購読しているプレイヤー。_player が替わったら付け替え、数えたロスも捨てる
        private NetPlayer _probedPlayer;

        private double _netStatsClock;
        private float _netStatsRefreshIn;
        private float _netStatsProbeIn;
        private int? _netStatsLoss;
        private int? _netStatsServerTick;

        /// <summary>回線の表示を出しているか。</summary>
        public bool IsNetStatsShown => _netStatsShown;

        /// <summary>回線の表示のビュー。まだ作っていなければ null。</summary>
        public NetStatsView NetStats => _netStatsView;

        /// <summary>出し入れを保存する先。テストは本物の設定を壊さないよう別のキーのストアに替える。</summary>
        internal ControlSettingsStore NetStatsSettingsStore { get; set; } = new ControlSettingsStore();

        /// <summary>
        /// 回線の表示を出し入れし、<see cref="AppliedControlSettings"/> に入れて <see cref="NetStatsSettingsStore"/> に保存する。
        /// Play 中は <see cref="NetStatsActionPath"/> を押したときに <see cref="Tick"/> が呼ぶ。
        /// </summary>
        internal void ToggleNetStats()
        {
            EnsureNetStats();
            var settings = AppliedControlSettings.Current.WithShowNetStats(!_netStatsShown);
            AppliedControlSettings.Apply(settings);

            // 購読していなくても (Apply が同じ値で Changed を出さなくても) 押した結果にそろえる
            ApplyNetStatsShown(settings.ShowNetStats);
            NetStatsSettingsStore.Save(settings);
            Debug.Log(_netStatsLogTag + " net stats " + (settings.ShowNetStats ? "shown" : "hidden"), this);
        }

        /// <summary>
        /// サーバーとの往復時間 (ms)。ホストは 0。まだつながっていない・トランスポートが無ければ null。
        /// </summary>
        internal int? ReadNetStatsPing()
        {
            if (_player == null || !_player.IsSpawned)
            {
                return null;
            }

            var manager = _player.NetworkManager;
            if (manager == null || !manager.IsListening)
            {
                return null;
            }

            if (manager.IsServer)
            {
                return 0;
            }

            var transport = manager.NetworkConfig != null ? manager.NetworkConfig.NetworkTransport : null;
            if (transport == null)
            {
                return null;
            }

            return NetStatsFormat.PingFromRtt(transport.GetCurrentRtt(NetworkManager.ServerClientId));
        }

        /// <summary>試合を購読し終えたときに呼ぶ。<see cref="Match"/> は新しい試合で、試合が無くなったときは null。</summary>
        partial void NetStatsMatchBound();

        /// <summary>試合の購読を外す直前に呼ぶ。<see cref="Match"/> はまだ外す前の試合 (無ければ null)。</summary>
        /// <remarks>回線の表示は試合の同期値を使わないので、何もしない。</remarks>
        partial void NetStatsMatchUnbinding();

        /// <summary>毎フレーム呼ぶ。</summary>
        /// <param name="deltaSeconds">前のフレームからの秒数。</param>
        partial void NetStatsTick(float deltaSeconds);

        /// <summary>プレイヤー・試合の購読をすべて外した後に呼ぶ (破棄するとき)。</summary>
        partial void NetStatsReleased();

        partial void NetStatsMatchBound()
        {
            EnsureNetStats();
        }

        partial void NetStatsTick(float deltaSeconds)
        {
            EnsureNetStats();
            if (_netStatsAction != null && _netStatsAction.WasPressedThisFrame() && !PlayerBindings.IsCapturingInput)
            {
                ToggleNetStats();
            }

            if (!_netStatsShown)
            {
                return;
            }

            if (deltaSeconds > 0f && !float.IsInfinity(deltaSeconds))
            {
                _netStatsClock += deltaSeconds;
                _netStatsRefreshIn -= deltaSeconds;
                _netStatsProbeIn -= deltaSeconds;
            }

            BindNetStatsProbes();

            // 引っかかって間が空いても一度にまとめて送らない (まとめて送るとその分だけ欠けやすくなる)
            if (_netStatsProbeIn <= 0f)
            {
                _netStatsProbeIn = NetStatsProbeSeconds;
                SendNetStatsProbe();
            }

            if (_netStatsRefreshIn <= 0f)
            {
                _netStatsRefreshIn = NetStatsRefreshSeconds;
                RefreshNetStats();
            }
        }

        partial void NetStatsReleased()
        {
            if (_listeningForSettings)
            {
                AppliedControlSettings.Changed -= HandleNetStatsSettingsChanged;
                _listeningForSettings = false;
            }

            UnbindNetStatsProbes();
            ReleaseNetStatsAction();
        }

        // Create の途中 (BindMatch) と毎フレームから呼ぶので、2 回目からは何もしない
        private void EnsureNetStats()
        {
            if (_netStatsReady || _hud == null)
            {
                return;
            }

            _netStatsReady = true;
            _netStatsView = NetStatsView.AttachTo(_hud);
            AppliedControlSettings.Changed += HandleNetStatsSettingsChanged;
            _listeningForSettings = true;
            AcquireNetStatsAction();
            ApplyNetStatsShown(AppliedControlSettings.Current.ShowNetStats);
        }

        private void HandleNetStatsSettingsChanged(ControlSettings settings)
        {
            ApplyNetStatsShown(settings.ShowNetStats);
        }

        private void ApplyNetStatsShown(bool shown)
        {
            if (shown == _netStatsShown)
            {
                return;
            }

            _netStatsShown = shown;
            if (!shown)
            {
                UnbindNetStatsProbes();
                if (_netStatsView != null)
                {
                    _netStatsView.Hide();
                }

                return;
            }

            // 前に出していたときの値は古いので、分かるまで -- で出す
            _netLossMeter.Reset();
            _netStatsLoss = null;
            _netStatsServerTick = null;
            _netStatsProbeIn = 0f;
            _netStatsRefreshIn = NetStatsRefreshSeconds;
            BindNetStatsProbes();
            RefreshNetStats();
        }

        private void RefreshNetStats()
        {
            var loss = _netLossMeter.TakeLossPercent(_netStatsClock);
            if (loss.HasValue)
            {
                _netStatsLoss = loss;
            }

            if (_netStatsView == null)
            {
                return;
            }

            var ping = ReadNetStatsPing();
            _netStatsView.Show(NetStatsFormat.Text(ping, _netStatsLoss, _netStatsServerTick), NetStatsFormat.PingLevel(ping));
        }

        private void SendNetStatsProbe()
        {
            var player = _probedPlayer;
            if (player == null || !player.IsSpawned || !player.IsOwner)
            {
                return;
            }

            // ホストでは返事がこの呼び出しの中で届くことがあるので、先に覚えてから送る
            var sequence = _netLossMeter.Send(_netStatsClock);
            player.SendNetStatsProbe(sequence);
        }

        private void BindNetStatsProbes()
        {
            if (ReferenceEquals(_probedPlayer, _player))
            {
                return;
            }

            UnbindNetStatsProbes();
            _probedPlayer = _player;
            if (!ReferenceEquals(_probedPlayer, null))
            {
                _probedPlayer.NetStatsProbeEchoed += HandleNetStatsProbeEchoed;
            }
        }

        // 破棄済みのプレイヤーでも C# のイベントは残っているので、ReferenceEquals で外す
        private void UnbindNetStatsProbes()
        {
            if (!ReferenceEquals(_probedPlayer, null))
            {
                _probedPlayer.NetStatsProbeEchoed -= HandleNetStatsProbeEchoed;
                _probedPlayer = null;
            }

            _netLossMeter.Reset();
            _netStatsLoss = null;
            _netStatsServerTick = null;
        }

        private void HandleNetStatsProbeEchoed(ushort sequence, int serverTick)
        {
            if (!_netLossMeter.Acknowledge(sequence, _netStatsClock))
            {
                return;
            }

            // 信頼性なしの便は追い越すことがあるので、古い tick で戻さない
            if (!_netStatsServerTick.HasValue || serverTick > _netStatsServerTick.Value)
            {
                _netStatsServerTick = serverTick;
            }
        }

        /// <summary>プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。</summary>
        private void AcquireNetStatsAction()
        {
            // Edit Mode では破棄時に無効に戻す OnDestroy が走らないので、Play 中だけ解決して有効にする
            if (!Application.isPlaying || _resolvedNetStatsAction)
            {
                return;
            }

            _resolvedNetStatsAction = true;
            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError(
                    _netStatsLogTag + " project-wide Input Actions are not set (Project Settings > Input System Package); F3 cannot toggle the network stats",
                    this);
                return;
            }

            _netStatsAction = actions.FindAction(NetStatsActionPath);
            if (_netStatsAction == null)
            {
                Debug.LogError(
                    _netStatsLogTag + " Input Actions '" + actions.name + "' has no '" + NetStatsActionPath + "'; the network stats cannot be toggled",
                    this);
                return;
            }

            if (!_netStatsAction.enabled)
            {
                _netStatsAction.Enable();
                _enabledNetStatsAction = true;
            }
        }

        private void ReleaseNetStatsAction()
        {
            if (_enabledNetStatsAction && _netStatsAction != null)
            {
                _netStatsAction.Disable();
            }

            _enabledNetStatsAction = false;
        }
    }
}
