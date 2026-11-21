using System;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetV2
{
    // オフラインの LockOnWarningPublisher と同じ GameSignals.LockOnWarning に、サーバーから届いた警告 (LockOnWarningMessage) を出す。
    // 段階と弾は ClientLockOnWarning が持ち、ここは自分の位置と向きで向きの度を出し直して出すことだけを持つ。
    public sealed class NetV2LockOnWarningPart : INetV2ClientPart
    {
        private readonly Func<int> _localPlayerId;
        private readonly Func<System.Numerics.Vector3?> _selfPosition;
        private readonly Func<float> _heading;
        private readonly Action<string> _log;
        private readonly ClientLockOnWarning _warning = new ClientLockOnWarning();
        private LockOnWarningStatus? _published;
        private LockOnWarningStage _loggedStage;
        private bool _disposed;

        // selfPosition は自分の体の位置で、体が無い (倒れて見ている・まだ現れていない) 間は null。heading は自分の向き (北から時計回りの度)。
        // どちらも null なら今の NetV2Client の自分の体から引く
        public NetV2LockOnWarningPart(
            Func<int> localPlayerId,
            Func<System.Numerics.Vector3?> selfPosition = null,
            Func<float> heading = null,
            Action<string> log = null)
        {
            _localPlayerId = localPlayerId ?? throw new ArgumentNullException(nameof(localPlayerId));
            _selfPosition = selfPosition ?? (() => PositionOf(LocalPlayer(null)));
            _heading = heading ?? (() => HeadingOf(LocalPlayer(null)));
            _log = log ?? (line => Debug.Log(line));
        }

        public ClientLockOnWarning Warning => _warning;

        public LockOnWarningStatus? Published => _published;

        // 結線の登録 (NetV2ClientParts.Registered) に 1 行で足せる形。再生は録った人への警告を見返す人に出すことになるので部品を出さない
        public static INetV2ClientPart Create(NetV2ClientPartsContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.Replay)
            {
                return null;
            }

            var client = context.Client;
            var localPlayer = context.LocalPlayer;
            return new NetV2LockOnWarningPart(
                () => client.PlayerId,
                () => PositionOf(LocalPlayer(localPlayer)),
                () => HeadingOf(LocalPlayer(localPlayer)),
                context.Log);
        }

        public bool Handle(IMessage message)
        {
            if (!(message is LockOnWarningMessage))
            {
                return false;
            }

            if (!_disposed)
            {
                _warning.Handle(message);
                Refresh();
            }

            return true;
        }

        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null)
            {
                return;
            }

            _warning.Apply(snapshot, _localPlayerId());
        }

        // 弾が飛ぶ間も自分が振り向く間も向きが変わるので、毎フレーム出し直す
        public void Tick(float deltaSeconds)
        {
            if (!_disposed)
            {
                Refresh();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _warning.Clear();
            Unpublish();
            Report(null);
        }

        private void Refresh()
        {
            var self = _warning.IsWarning ? _selfPosition() : null;
            var status = self.HasValue ? _warning.StatusFrom(self.Value, _heading()) : null;
            if (!status.HasValue)
            {
                Unpublish();
                Report(null);
                return;
            }

            _published = status;

            // 同じ中身を出し直さないのは、変わらない間に UI が毎フレーム描き直さないようにするため (LockOnWarningPublisher と同じ)
            if (!(GameSignals.LockOnWarning.Value is LockOnWarningStatus current) || current != status.Value)
            {
                GameSignals.LockOnWarning.Publish(status);
            }

            Report(status);
        }

        private void Unpublish()
        {
            if (_published.HasValue)
            {
                // 後から別の部品が出した値までは消さない
                GameSignals.LockOnWarning.ClearIfCurrent(_published);
            }

            _published = null;
        }

        private void Report(LockOnWarningStatus? status)
        {
            var stage = status.HasValue ? status.Value.Stage : LockOnWarningStage.None;
            if (stage == _loggedStage)
            {
                return;
            }

            _loggedStage = stage;
            _log(LockOnWarningLog.StageChanged(status));
        }

        // 破棄済みの Unity の参照も無いものとして扱うため、?. ではなく != null で確かめる
        private static NetV2LocalPlayer LocalPlayer(Func<NetV2LocalPlayer> source)
        {
            var local = source?.Invoke();
            if (local != null)
            {
                return local;
            }

            return NetV2Client.Current != null ? NetV2Client.Current.LocalPlayer : null;
        }

        private static System.Numerics.Vector3? PositionOf(NetV2LocalPlayer local)
        {
            return local != null ? NetV2Math.ToNumerics(local.transform.position) : (System.Numerics.Vector3?)null;
        }

        private static float HeadingOf(NetV2LocalPlayer local)
        {
            return local != null ? local.HeadingDegrees : float.NaN;
        }
    }
}
