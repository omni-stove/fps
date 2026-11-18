using System;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetV2
{
    // オフラインの KillCamReplay と同じ GameSignals.KillCam に再生の状態を出し、カメラは観戦・リプレイと同じ DeathCamView で借りる。
    // ClientKillCam は Unity を参照できないアセンブリにあるので、出すのとカメラを動かすのはこの部品が受け持つ。
    public sealed class NetV2KillCamPart : INetV2ClientPart
    {
        private readonly MatchClient _client;
        private readonly Func<Component> _cameraOwner;
        private readonly Action<string> _log;
        private readonly float _speed;
        private ClientKillCam _killCam;
        private KillCamStatus? _published;
        private Component _viewOwner;
        private DeathCamView _view;
        private bool _disposed;

        // 自分の Id は承認されるまで決まらず、部品はつなぐ前に作られるので、作るのは最初に届いたときにする
        public NetV2KillCamPart(MatchClient client, float speed = 1f, Func<Component> cameraOwner = null, Action<string> log = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            if (!KillCamPlayback.IsValidSpeed(speed))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "speed must be positive and finite");
            }

            _speed = speed;
            _cameraOwner = cameraOwner ?? (() => NetV2Client.Current != null ? NetV2Client.Current.LocalPlayer : null);
            _log = log ?? (line => Debug.Log(line));
        }

        public ClientKillCam KillCam => _killCam;

        // Killed は他の部品も受け取れるよう false を返す。クリップはこの部品だけが使うので true
        public bool Handle(IMessage message)
        {
            if (_disposed)
            {
                return message is KillCamClipMessage;
            }

            switch (message)
            {
                case KilledMessage killed:
                    Ensure().Apply(killed);
                    return false;
                case KillCamClipMessage clip:
                    var killCam = Ensure();
                    if (killCam.Apply(clip))
                    {
                        Publish();
                    }

                    if (killCam.IsPlaying && killCam.Status.HasValue)
                    {
                        _log(KillCamLog.Replaying(
                            NetV2Client.LogTag, BotRoster.KillFeedName(clip.KillerId), killCam.Status.Value.DurationSeconds, _speed));
                    }

                    return true;
                default:
                    return false;
            }
        }

        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null)
            {
                return;
            }

            if (Ensure().Apply(snapshot))
            {
                Publish();
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (_disposed || _killCam == null)
            {
                return;
            }

            var seconds = float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) ? 0f : Mathf.Max(0f, deltaSeconds);
            if (_killCam.Update(seconds))
            {
                Publish();
            }

            if (!_killCam.TryGetView(out var eye, out var yaw))
            {
                ReleaseView();
                return;
            }

            var view = BorrowView();
            view?.FrameEye(NetV2Math.ToUnity(eye), yaw * Mathf.Rad2Deg);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _killCam?.Reset();
            ReleaseView();
            Withdraw();
        }

        private ClientKillCam Ensure()
        {
            return _killCam ??= new ClientKillCam(_client.PlayerId, _speed);
        }

        // 他の側が後から出した値まで消さないよう、自分が出した値のときだけ消す
        private void Publish()
        {
            var status = _killCam.Status;
            if (!status.HasValue)
            {
                Withdraw();
                return;
            }

            _published = status;
            GameSignals.KillCam.Publish(status);
        }

        private void Withdraw()
        {
            if (_published.HasValue)
            {
                GameSignals.KillCam.ClearIfCurrent(_published);
                _published = null;
            }
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
                _view = new DeathCamView(owner, "give the kill cam a player that has a camera under its body");
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
