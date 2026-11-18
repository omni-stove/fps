using System;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetV2
{
    // 再生中の見方。Next・Previous で人と自由なカメラを巡り、人を選べば観戦と同じくその人の目から、
    // 自由なカメラなら Move と視点の入力で動かす。カメラの借り方と動かし方は観戦・死亡カメラと同じ DeathCamView を使う。
    public sealed class NetV2ReplayViewPart : INetV2ClientPart
    {
        private readonly MatchClient _client;
        private readonly Func<Component> _cameraOwner;
        private readonly float _freeSpeed;
        private ClientReplayView _replayView;
        private Component _viewOwner;
        private DeathCamView _view;
        private bool _disposed;

        // freeSpeed は呼ぶ側が設定 (PlayerDeathCamera.FreeSpeed など) から渡す。
        public NetV2ReplayViewPart(MatchClient client, float freeSpeed, Func<Component> cameraOwner = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            if (!(freeSpeed >= 0f) || float.IsInfinity(freeSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(freeSpeed), freeSpeed, "freeSpeed must be zero or more and finite");
            }

            _freeSpeed = freeSpeed;
            _cameraOwner = cameraOwner ?? (() => NetV2Client.Current != null ? NetV2Client.Current.LocalPlayer : null);
        }

        public ClientReplayView View => _replayView;

        public bool Handle(IMessage message)
        {
            return false;
        }

        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null)
            {
                return;
            }

            // 記録した人の Id と体の寸法は承認で決まり、部品はつなぐ前に作られるので、マップに入ってから作る
            _replayView ??= new ClientReplayView(_client.PlayerId, _client.TickRate, _client.Motor.Stance);
            _replayView.Apply(snapshot);
        }

        public void Tick(float deltaSeconds)
        {
            if (_disposed || _replayView == null)
            {
                return;
            }

            var seconds = Mathf.Max(0f, float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) ? 0f : deltaSeconds);
            var changed = _replayView.Update(seconds);
            var view = BorrowView();
            if (view == null)
            {
                return;
            }

            var step = view.ReadSpectateStep();
            if (step != 0)
            {
                changed |= _replayView.Step(step);
            }

            if (changed)
            {
                Debug.Log(ReplayLog.ViewChanged(_replayView.TargetId));
            }

            if (_replayView.IsFree)
            {
                view.MoveFree(view.ReadMove(), view.ReadLookDegrees(seconds), _freeSpeed, seconds);
                return;
            }

            // 選んだ人の姿がまだ無いフレームは、前のフレームの位置のまま待つ
            if (_replayView.TryGetView(out var eye, out var yaw))
            {
                view.FrameEye(NetV2Math.ToUnity(eye), yaw * Mathf.Rad2Deg);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ReleaseView();
            _replayView?.Reset();
        }

        private DeathCamView BorrowView()
        {
            var owner = _cameraOwner();
            if (owner == null)
            {
                ReleaseView();
                return null;
            }

            if (_view != null && _viewOwner != owner)
            {
                ReleaseView();
            }

            if (_view == null)
            {
                _viewOwner = owner;
                _view = new DeathCamView(owner, "give the replay a player that has a camera under its body");
            }

            if (!_view.IsEngaged)
            {
                _view.Begin(PlayerDeathCamera.EyeOf(owner.gameObject), owner.transform.eulerAngles.y);
            }

            return _view;
        }

        private void ReleaseView()
        {
            if (_view == null)
            {
                return;
            }

            _view.Restore(_viewOwner != null);
            _view = null;
            _viewOwner = null;
        }
    }
}
