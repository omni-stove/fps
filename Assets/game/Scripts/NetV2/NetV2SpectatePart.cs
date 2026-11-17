using System;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetV2
{
    /// <summary>
    /// ネット対戦のクライアントの観戦の部品。自分が倒れてから戻るまでと、ラウンドの合間に、生きている味方の目から見る。
    /// 誰をいつ見るかは <see cref="ClientSpectate"/> が決め、ここは自分のカメラを借りて (<see cref="DeathCamView"/>) 相手の目に置き、
    /// Next / Previous で相手を替え、状態を <see cref="GameSignals.Spectate"/> に出す。
    /// 見られる味方がいなければカメラを借りず (借りていれば返し)、倒れたときのカメラのままにする。
    /// </summary>
    public sealed class NetV2SpectatePart : INetV2ClientPart
    {
        private readonly MatchClient _client;
        private readonly Func<IMessage, bool> _send;
        private readonly Func<NetV2LocalPlayer> _localPlayer;
        private readonly Func<RoundPhase?> _roundPhase;
        private ClientSpectate _spectate;
        private NetV2LocalPlayer _viewOwner;
        private DeathCamView _view;
        private SpectateStatus? _published;
        private bool _disposed;

        /// <param name="client">つないだクライアント。承認された後の自分の Id・tick の頻度・体の寸法を読む。</param>
        /// <param name="send">サーバーへ確実に届く経路で送る口。</param>
        /// <param name="localPlayer">カメラを借りる自分のプレイヤー。null なら今のクライアント (<see cref="NetV2Client.Current"/>) のもの。</param>
        /// <param name="roundPhase">ラウンドで進むモードの今の進み。null か、null を返せばラウンドの合間の観戦はしない。</param>
        /// <exception cref="ArgumentNullException">client か send が null。</exception>
        public NetV2SpectatePart(
            MatchClient client, Func<IMessage, bool> send, Func<NetV2LocalPlayer> localPlayer = null, Func<RoundPhase?> roundPhase = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _localPlayer = localPlayer ?? (() => NetV2Client.Current != null ? NetV2Client.Current.LocalPlayer : null);
            _roundPhase = roundPhase;
        }

        /// <summary>観戦の中身。最初のスナップショットを受けるまでは null。</summary>
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

            var round = _roundPhase != null ? _roundPhase() : null;
            _spectate.Update(Mathf.Max(0f, deltaSeconds), round);
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
            ReleaseView();
            _spectate?.Reset();
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
