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
        private ClientStreaks _streaks;
        private InputAction _useAction;
        private bool _lookedUpAction;
        private StreakStatus? _published;
        private bool _disposed;

        // 自分の Id は承認されるまで決まらず、部品はつなぐ前に作られるので、数えるのは最初のスナップショットが届いてから始める。
        // ストリークの規則は MatchRulesSync に載らないので、つなぐ側が知っている試合の規則を引く口で受け取る。
        // 押したかを引く口は、テストや別の入力から差し替えられるよう受け取り、無ければ InputSystem の UseStreak を読む
        public NetV2StreakPart(
            MatchClient client,
            Func<IMessage, bool> send,
            Func<Streaks> rule,
            Action<string> log = null,
            StreakSpec spec = null,
            Func<NetV2LocalPlayer> localPlayer = null,
            Func<bool> usePressed = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _rule = rule ?? throw new ArgumentNullException(nameof(rule));
            _log = log ?? (line => Debug.Log(line));
            _spec = spec ?? StreakController.Tuning;
            _localPlayer = localPlayer ?? (() => NetV2Client.Current != null ? NetV2Client.Current.LocalPlayer : null);
            _usePressed = usePressed ?? ReadUsePressed;
        }

        public ClientStreaks Counter => _streaks;

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
                Withdraw();
                return;
            }

            _streaks ??= new ClientStreaks(_client.PlayerId, _spec, _send, _log);
            _streaks.Apply(snapshot);
            Publish();
        }

        public void Tick(float deltaSeconds)
        {
            if (_disposed || _streaks == null)
            {
                return;
            }

            _streaks.Tick(Mathf.Max(0f, deltaSeconds));

            // ポーズ画面のボタンを押したつもりで報酬を使わないよう、開いている間は読まない (オフラインと同じ)
            if (!_usePressed() || GameUi.IsPauseOpen || !_published.HasValue)
            {
                return;
            }

            var local = _localPlayer();
            var direction = local != null ? local.AimDirection : Vector3.forward;
            _streaks.TryUse(NetV2Math.ToNumerics(direction), out _);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
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
