using System;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetV2
{
    // 当たりとダメージはサーバー (ServerMelee) が決めるので、ここは押し始めに振る見た目を出すだけにする。
    // 見た目は自分の体に付いた差し替えの口 (IMeleePresenter) に任せ、オフラインの MeleeController と同じ口で渡す。
    public sealed class NetV2MeleePart : INetV2ClientPart
    {
        private readonly MatchClient _client;
        private readonly Func<NetV2LocalPlayer> _localPlayer;
        private readonly Action<string> _log;
        private readonly ClientMelee _melee;
        private bool _canSwing;
        private bool _disposed;

        // 調整値はオフラインと同じ melee の JSON (MeleeActionInstaller.Tuning) を使い、サーバーの server-data/tuning/melee.json と揃える
        public NetV2MeleePart(MatchClient client, MeleeSpec spec = null, Func<NetV2LocalPlayer> localPlayer = null, Action<string> log = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _localPlayer = localPlayer ?? (() => NetV2Client.Current != null ? NetV2Client.Current.LocalPlayer : null);
            _log = log ?? (line => Debug.Log(line));
            _melee = new ClientMelee(spec ?? MeleeActionInstaller.Tuning);
            _melee.SwingStarted += HandleSwingStarted;
        }

        public ClientMelee Melee => _melee;

        public bool Handle(IMessage message)
        {
            return false;
        }

        // 振れるか (ルールで近接が On・生きていてダウンしていない) は、サーバーが同じ値で判定するスナップショットの値で決める
        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null)
            {
                return;
            }

            var canSwing = snapshot.Match.Rules.Melee == Shooter.Core.Melee.On && snapshot.Match.State == GameModeState.Playing;
            var own = snapshot.Players.IndexOf(_client.PlayerId);
            if (own < 0)
            {
                canSwing = false;
            }
            else
            {
                var row = snapshot.Players[own];
                var alive = row.Health > 0 && !row.Downed.IsDowned;
                if (!alive && _canSwing)
                {
                    // 倒れて出直したときにサーバーも振りと待ち時間を捨てるので、手元も合わせる
                    _melee.Reset();
                }

                canSwing &= alive;
            }

            _canSwing = canSwing;
        }

        public void Tick(float deltaSeconds)
        {
            if (_disposed)
            {
                return;
            }

            var local = _localPlayer();
            var seconds = float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) ? 0f : Mathf.Max(0f, deltaSeconds);
            _melee.Update(local != null && local.IsMeleeSent, _canSwing && local != null, seconds);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _melee.SwingStarted -= HandleSwingStarted;
            _melee.Reset();
        }

        private void HandleSwingStarted(MeleeSpec spec)
        {
            var local = _localPlayer();
            if (local == null)
            {
                return;
            }

            _log(MeleeLog.Swing(_client.PlayerId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            var presenter = local.GetComponent<IMeleePresenter>();
            presenter?.OnSwingStarted(local.gameObject, spec);
        }
    }
}
