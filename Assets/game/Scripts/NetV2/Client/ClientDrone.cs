using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 自分のドローンをスナップショットの置いた物の行 (DeployableKind.Drone) から見つけ、操縦の入力を DroneControlMessage で送る。
    // 飛ばすのはサーバーなので、位置と向きは次のスナップショットで知る (手元では先に動かさない)。
    // 新しく現れた自分のドローンは、置いた直後と同じく操縦を始める合図 (Appeared) を出す (オフラインの DroneDeployDefinition.OnPlaced と同じ)
    public sealed class ClientDrone
    {
        // サーバーは届いた最後の入力を ServerDeployables.DroneInputHoldSeconds だけ使い続けるので、それより短い間隔で送り直す。
        // 落ちた送信で止まらないよう、間に 2 回は送れる長さにする
        public const float ResendSeconds = 0.1f;

        private readonly Action<IMessage, TransportChannel> _send;
        private readonly Action<string> _log;
        private readonly DroneControlMessage _control = new DroneControlMessage();
        private readonly HashSet<int> _owned = new HashSet<int>();
        private readonly HashSet<int> _listed = new HashSet<int>();
        private int _sequence;
        private float _sinceSent;
        private bool _sentMoving;
        private bool _unconfirmed;
        private Vector2 _sentMove;
        private float _sentClimb;
        private float _sentYaw;
        private string _ownerText = "-";

        public ClientDrone(Action<IMessage, TransportChannel> send, Action<string> log = null)
        {
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _log = log;
        }

        // 引数は現れたドローンの Id
        public event Action<int> Appeared;

        // 引数は消えたドローンの Id
        public event Action<int> Gone;

        public int? DroneId { get; private set; }

        public string TypeId { get; private set; } = "";

        public Vector3 Position { get; private set; }

        public float Yaw { get; private set; }

        // 届く距離はサーバーと同じく、最初に見えた所 (置いた所) から測る
        public Vector3 Launch { get; private set; }

        public bool IsPiloting { get; private set; }

        public int Sequence => _sequence;

        public void Apply(SnapshotMessage snapshot, int localPlayerId)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!snapshot.IsComplete)
            {
                return;
            }

            _ownerText = localPlayerId.ToString(CultureInfo.InvariantCulture);
            _listed.Clear();
            int? appeared = null;
            var rows = snapshot.Deployables;
            for (var i = 0; i < rows.Count; i++)
            {
                ref var row = ref rows[i];
                if (row.Kind != DeployableKind.Drone || !row.HasOwner || row.OwnerId != localPlayerId)
                {
                    continue;
                }

                _listed.Add(row.DeployableId);
                if (!_owned.Contains(row.DeployableId))
                {
                    // 1 回のスナップショットに新しい物が 2 つ載ったら、Id の大きい方 (後に置いた方) を操縦する
                    if (!appeared.HasValue || row.DeployableId > appeared.Value)
                    {
                        appeared = row.DeployableId;
                    }
                }

                if (DroneId == row.DeployableId)
                {
                    Position = row.Position;
                    Yaw = row.Yaw;
                }
            }

            if (DroneId.HasValue && !_listed.Contains(DroneId.Value))
            {
                Lose();
            }

            _owned.Clear();
            foreach (var id in _listed)
            {
                _owned.Add(id);
            }

            if (appeared.HasValue)
            {
                Follow(snapshot, appeared.Value);
            }
        }

        public bool Begin()
        {
            if (!DroneId.HasValue || IsPiloting)
            {
                return false;
            }

            IsPiloting = true;
            _sinceSent = ResendSeconds;
            _sentMoving = false;
            _unconfirmed = false;
            _sentYaw = Yaw;
            _log?.Invoke(DroneLog.PilotStarted(_ownerText, TypeId, DroneId.Value));
            return true;
        }

        // 止まる入力を確実に届く経路で送ってから降りる。サーバーは届かなくても入力の保ち時間で止めるが、それより早く止めるため
        public bool Leave(DronePilotEnd reason)
        {
            if (!IsPiloting)
            {
                return false;
            }

            IsPiloting = false;
            if (DroneId.HasValue)
            {
                if (_sentMoving)
                {
                    Send(Vector2.Zero, 0f, _sentYaw, false, TransportChannel.Reliable);
                }

                _log?.Invoke(DroneLog.PilotEnded(_ownerText, TypeId, DroneId.Value, reason));
            }

            return true;
        }

        // move は x が右・y が前で長さ 1 まで、climb は上へ 1・下へ -1、yaw は向く角度 (度)。
        // 入力が変わったときと、動かしている間は ResendSeconds ごとに送る。止まっていて向きも変わらなければ送らない
        public bool Steer(Vector2 move, float climb, float yaw, float deltaSeconds)
        {
            if (!IsFinite(move) || !float.IsFinite(climb) || !float.IsFinite(yaw))
            {
                throw new ArgumentException("drone input must be finite");
            }

            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative and must be finite");
            }

            if (!IsPiloting || !DroneId.HasValue)
            {
                return false;
            }

            var length = move.Length();
            if (length > 1f)
            {
                move /= length;
            }

            climb = Math.Clamp(climb, -1f, 1f);
            _sinceSent += deltaSeconds;
            var moving = move != Vector2.Zero || climb != 0f;
            var changed = move != _sentMove || climb != _sentClimb || !yaw.Equals(_sentYaw);
            // 止まって向きだけ変えた最後の入力は届きにくい経路で 1 回しか送っていないので、落ちても向きがずれ続けないよう、
            // 変わらなくなって ResendSeconds 後に確実に届く経路で 1 回だけ送り直す (毎フレーム変わるマウスの向きを Reliable で送らないため)
            if (!changed && !moving && _unconfirmed && _sinceSent >= ResendSeconds)
            {
                Send(move, climb, yaw, false, TransportChannel.Reliable);
                return true;
            }

            if (!changed && (!moving || _sinceSent < ResendSeconds))
            {
                return false;
            }

            Send(move, climb, yaw, false, TransportChannel.Unreliable);
            return true;
        }

        public bool Recall()
        {
            if (!DroneId.HasValue)
            {
                return false;
            }

            var id = DroneId.Value;
            Leave(DronePilotEnd.DroneGone);
            Send(Vector2.Zero, 0f, _sentYaw, true, TransportChannel.Reliable);
            _log?.Invoke(DroneLog.Recalled(_ownerText, TypeId, id, Position));
            return true;
        }

        // 試合やマップが替わって置いた物が消えるときと同じく、知っているドローンを忘れる。降りる知らせは送らない
        public void Reset()
        {
            IsPiloting = false;
            DroneId = null;
            TypeId = "";
            _owned.Clear();
            _listed.Clear();
        }

        private void Follow(SnapshotMessage snapshot, int id)
        {
            if (IsPiloting)
            {
                Leave(DronePilotEnd.DroneGone);
            }

            var rows = snapshot.Deployables;
            for (var i = 0; i < rows.Count; i++)
            {
                ref var row = ref rows[i];
                if (row.DeployableId != id)
                {
                    continue;
                }

                DroneId = id;
                TypeId = row.TypeId ?? "";
                Position = row.Position;
                Launch = row.Position;
                Yaw = row.Yaw;
                _sentMove = Vector2.Zero;
                _sentClimb = 0f;
                _sentMoving = false;
                _unconfirmed = false;
                _sentYaw = row.Yaw;
                Appeared?.Invoke(id);
                return;
            }
        }

        private void Lose()
        {
            var id = DroneId.Value;
            if (IsPiloting)
            {
                IsPiloting = false;
                _log?.Invoke(DroneLog.PilotEnded(_ownerText, TypeId, id, DronePilotEnd.DroneGone));
            }

            DroneId = null;
            TypeId = "";
            Gone?.Invoke(id);
        }

        private void Send(Vector2 move, float climb, float yaw, bool recall, TransportChannel channel)
        {
            _sequence = _sequence == int.MaxValue ? 0 : _sequence + 1;
            _control.Deployable = DroneId.Value;
            _control.Sequence = _sequence;
            _control.Move = move;
            _control.Climb = climb;
            _control.Yaw = yaw;
            _control.Recall = recall;
            _send(_control, channel);
            _sinceSent = 0f;
            _sentMove = move;
            _sentClimb = climb;
            _sentYaw = yaw;
            _sentMoving = move != Vector2.Zero || climb != 0f;
            _unconfirmed = channel == TransportChannel.Unreliable;
        }

        private static bool IsFinite(Vector2 value)
        {
            return float.IsFinite(value.X) && float.IsFinite(value.Y);
        }
    }
}
