using System;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetV2
{
    public sealed class NetV2SpectatePart : INetV2ClientPart
    {
        private readonly MatchClient _client;
        private readonly Func<IMessage, bool> _send;
        private readonly Func<NetV2LocalPlayer> _localPlayer;
        private ClientSpectate _spectate;
        private NetV2LocalPlayer _viewOwner;
        private DeathCamView _view;
        private SpectateStatus? _published;
        private bool _disposed;

        public NetV2SpectatePart(
            MatchClient client, Func<IMessage, bool> send, Func<NetV2LocalPlayer> localPlayer = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _localPlayer = localPlayer ?? (() => NetV2Client.Current != null ? NetV2Client.Current.LocalPlayer : null);
        }

        public ClientSpectate Spectate => _spectate;

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

            if (snapshot.Match.Rules.Spectating == Spectating.Off)
            {
                Stop();
                return;
            }

            // 自分の Id と体の寸法は承認されるまで決まらず、部品はつなぐ前に作られるので、マップに入ってから作る
            _spectate ??= new ClientSpectate(_client.PlayerId, _client.TickRate, _client.Motor.Stance, SendRequest);
            _spectate.Apply(snapshot);
        }

        public void Tick(float deltaSeconds)
        {
            if (_disposed || _spectate == null)
            {
                return;
            }

            _spectate.Update(Mathf.Max(0f, deltaSeconds));
            if (_view != null && _view.IsEngaged)
            {
                var step = _view.ReadSpectateStep();
                if (step != 0)
                {
                    _spectate.Step(step > 0 ? SpectateStep.Next : SpectateStep.Previous);
                }
            }

            Publish(_spectate.Status);
            ShowView();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Stop();
        }

        private void Stop()
        {
            ReleaseView();
            _spectate?.Reset();
            _spectate = null;
            Publish(null);
        }

        private bool SendRequest(IMessage message)
        {
            var sent = _send(message);
            if (message is SpectateRequestMessage request)
            {
                Debug.Log(SpectateLog.Requested(request.Step, sent));
            }

            return sent;
        }

        private void Publish(SpectateStatus? status)
        {
            if (_published == status)
            {
                return;
            }

            var previous = _published;
            _published = status;
            Debug.Log(SpectateLog.Changed(status));
            if (status.HasValue)
            {
                GameSignals.Spectate.Publish(status);
            }
            else
            {
                GameSignals.Spectate.ClearIfCurrent(previous);
            }
        }

        private void ShowView()
        {
            if (!_spectate.TryGetView(out var eye, out var yaw))
            {
                // 相手がいるのに姿がまだ無いフレームは、前のフレームの位置のまま待つ
                if (!_spectate.TargetId.HasValue)
                {
                    ReleaseView();
                }

                return;
            }

            var view = BorrowView();
            view?.FrameEye(NetV2Math.ToUnity(eye), yaw * Mathf.Rad2Deg);
        }

        private DeathCamView BorrowView()
        {
            var local = _localPlayer();
            if (local == null)
            {
                ReleaseView();
                return null;
            }

            if (_view != null && _viewOwner != local)
            {
                ReleaseView();
            }

            if (_view == null)
            {
                _viewOwner = local;
                _view = new DeathCamView(local, "the NetV2 player was built without a camera");
            }

            if (!_view.IsEngaged)
            {
                _view.Begin(local.EyePosition, local.transform.eulerAngles.y);
            }

            return _view;
        }

        private void ReleaseView()
        {
            if (_view == null)
            {
                return;
            }

            // 自分のプレイヤーが先に消えていれば、戻す視点も無いので有効に戻さない
            _view.Restore(_viewOwner != null);
            _view = null;
            _viewOwner = null;
        }
    }
}
