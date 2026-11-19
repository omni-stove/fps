using System;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.NetV2
{
    // オフラインの StreakController と同じ GameSignals.Streak に報酬の枠を出し、同じ UseStreak の入力で使う要求を送る。
    // UAV の敵の印はサーバーが PingPlaced で送るので、表示は印の仕組み (ClientPings) に任せ、ここでは受け持たない。
    public sealed class NetV2StreakPart : INetV2ClientPart
    {
        private readonly MatchClient _client;
        private readonly Func<IMessage, bool> _send;
        private readonly Func<Streaks> _rule;
        private readonly Action<string> _log;
        private readonly StreakSpec _spec;
        private readonly Func<NetV2LocalPlayer> _localPlayer;
        private readonly Func<bool> _usePressed;
        private readonly Func<bool> _switchPressed;
        private readonly Func<StreakTargeting> _targetingHost;
        private ClientStreaks _streaks;
        private StreakTargeting _targeting;
        private int _targetingEndedFrame = -1;
        private InputAction _useAction;
        private bool _lookedUpAction;
        private InputAction _switchAction;
        private bool _lookedUpSwitchAction;
        private StreakStatus? _published;
        private bool _disposed;

        // 自分の Id は承認されるまで決まらず、部品はつなぐ前に作られるので、数えるのは最初のスナップショットが届いてから始める。
        // ストリークの規則は MatchRulesSync に載らないので、つなぐ側が知っている試合の規則を引く口で受け取る。
        // 押したかを引く口と地点を選ぶ部品を引く口は、テストや別の入力から差し替えられるよう受け取る。
        // 無ければ InputSystem の UseStreak と SwitchStreak を読み、自分の体 (NetV2LocalPlayer) の StreakTargeting で選ぶ
        public NetV2StreakPart(
            MatchClient client,
            Func<IMessage, bool> send,
            Func<Streaks> rule,
            Action<string> log = null,
            StreakSpec spec = null,
            Func<NetV2LocalPlayer> localPlayer = null,
            Func<bool> usePressed = null,
            Func<StreakTargeting> targeting = null,
            Func<bool> switchPressed = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _rule = rule ?? throw new ArgumentNullException(nameof(rule));
            _log = log ?? (line => Debug.Log(line));
            _spec = spec ?? StreakController.Tuning;
            _localPlayer = localPlayer ?? (() => NetV2Client.Current != null ? NetV2Client.Current.LocalPlayer : null);
            _usePressed = usePressed ?? ReadUsePressed;
            _targetingHost = targeting ?? FindTargeting;
            _switchPressed = switchPressed ?? ReadSwitchPressed;
        }

        public ClientStreaks Counter => _streaks;

        public bool IsTargeting => _targeting != null && _targeting.IsActive;

        public bool Handle(IMessage message)
        {
            if (!(message is StreakActivatedMessage activated))
            {
                return false;
            }

            if (_disposed || _streaks == null)
            {
                return true;
            }

            _streaks.Handle(activated);
            Publish();
            return true;
        }

        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null)
            {
                return;
            }

            if (_rule() != Shooter.Core.Streaks.On)
            {
                CancelTargeting(StreakTargeting.CancelledByRules);
                Withdraw();
                return;
            }

            _streaks ??= new ClientStreaks(_client.PlayerId, _spec, _send, _log);
            _streaks.Apply(snapshot);
            Publish();
            if (IsTargeting && !CanAct(snapshot))
            {
                CancelTargeting(StreakTargeting.CancelledByDeath);
            }
        }

        // 倒れた・試合が止まった後に選び終えても、サーバーは断るだけなので、見下ろしたまま待たせずに閉じる
        private bool CanAct(SnapshotMessage snapshot)
        {
            if (snapshot.Match.State != GameModeState.Playing)
            {
                return false;
            }

            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                var row = players[i];
                if (row.PlayerId == _client.PlayerId)
                {
                    return row.Health > 0 && !row.Downed.IsDowned;
                }
            }

            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (_disposed || _streaks == null)
            {
                return;
            }

            _streaks.Tick(Mathf.Max(0f, deltaSeconds));

            // 選んでいる間と選び終えたフレームの UseStreak は StreakTargeting が取消に使うので、ここでは読まない (オフラインと同じ)
            if (IsTargeting || Time.frameCount == _targetingEndedFrame)
            {
                return;
            }

            // ポーズ画面のボタンを押したつもりで報酬を使わないよう、開いている間は読まない (オフラインと同じ)
            if (GameUi.IsPauseOpen || !_published.HasValue)
            {
                return;
            }

            if (_switchPressed() && _streaks.SelectNext())
            {
                Publish();
            }

            if (!_usePressed())
            {
                return;
            }

            var next = _streaks.NextRewardId;
            if (next != null && StreakSpec.NeedsTarget(next))
            {
                BeginTargeting(next);
                return;
            }

            _streaks.TryUse(null, out _);
        }

        public bool CancelTargeting(string reason)
        {
            return _targeting != null && _targeting.Cancel(reason);
        }

        // 地点の高さと射程は、サーバーが自分の世界で決め直して確かめるので、手元で選んだ値をそのまま送る
        private bool BeginTargeting(string rewardId)
        {
            var targeting = _targetingHost();
            if (targeting == null)
            {
                _log(StreakLog.RewardUnavailable(ClientStreakLog.Id(_client.PlayerId), rewardId, nameof(StreakTargeting)));
                return false;
            }

            if (!ReferenceEquals(targeting, _targeting))
            {
                Unhook();
                _targeting = targeting;
                _targeting.Confirmed += HandleConfirmed;
                _targeting.Cancelled += HandleCancelled;
            }

            var local = _localPlayer();
            var look = local != null ? local.Look : null;
            var origin = local != null ? local.transform.position : targeting.transform.position;
            var heading = look != null && look.IsInitialized ? look.Yaw : targeting.transform.eulerAngles.y;
            var reward = _spec.FindReward(rewardId);
            var aim = new StreakAim(
                rewardId,
                reward != null ? reward.DisplayNameKey : string.Empty,
                NetV2Math.ToNumerics(origin),
                heading,
                _spec.Airstrike.Range,
                _spec.Airstrike.LineLength);
            var handoffs = local != null ? new[] { local.Handoff } : Array.Empty<ControlHandoff>();
            targeting.LogId = ClientStreakLog.Id(_client.PlayerId);
            return targeting.Begin(aim, _spec.Targeting, handoffs, look);
        }

        private void HandleConfirmed(StreakTargetingStatus target)
        {
            _targetingEndedFrame = Time.frameCount;
            if (!_disposed && _streaks != null)
            {
                _streaks.TryUse(target, out _);
            }
        }

        private void HandleCancelled(string rewardId, string reason)
        {
            _targetingEndedFrame = Time.frameCount;
        }

        private void Unhook()
        {
            if (_targeting == null)
            {
                return;
            }

            _targeting.Confirmed -= HandleConfirmed;
            _targeting.Cancelled -= HandleCancelled;
            _targeting = null;
        }

        private StreakTargeting FindTargeting()
        {
            var local = _localPlayer();
            if (local == null)
            {
                return null;
            }

            var targeting = local.GetComponent<StreakTargeting>();
            return targeting != null ? targeting : local.gameObject.AddComponent<StreakTargeting>();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CancelTargeting(StreakTargeting.CancelledByRules);
            Unhook();
            _streaks?.Reset();
            Withdraw();
        }

        private bool ReadUsePressed()
        {
            if (!_lookedUpAction)
            {
                _lookedUpAction = true;
                _useAction = InputSystem.actions != null ? InputSystem.actions.FindAction(InputActionPaths.UseStreak) : null;
            }

            return _useAction != null && _useAction.WasPressedThisFrame();
        }

        private bool ReadSwitchPressed()
        {
            if (!_lookedUpSwitchAction)
            {
                _lookedUpSwitchAction = true;
                _switchAction = InputSystem.actions != null ? InputSystem.actions.FindAction(InputActionPaths.SwitchStreak) : null;
            }

            return _switchAction != null && _switchAction.WasPressedThisFrame();
        }

        // Publish は同じ値でも購読者を呼ぶので、変わったときだけ出す
        private void Publish()
        {
            var status = _streaks.Status;
            if (!status.HasValue)
            {
                Withdraw();
                return;
            }

            if (_published.HasValue && _published.Value.Equals(status.Value))
            {
                return;
            }

            _published = status;
            GameSignals.Streak.Publish(status);
        }

        // 他の側が後から出した値まで消さないよう、自分が出した値のときだけ消す
        private void Withdraw()
        {
            if (_published.HasValue)
            {
                GameSignals.Streak.ClearIfCurrent(_published);
                _published = null;
            }
        }
    }
}
