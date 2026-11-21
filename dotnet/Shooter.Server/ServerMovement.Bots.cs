using System;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // Bot の体。接続を持たないので、Bot のモジュールが加え・外し・入力を直に渡す
    internal sealed partial class ServerMovement
    {
        // Bot は接続を持たないので、Bot のモジュールが試合に加えた直後にここで体を作る。
        public void AddBot(ServerPlayer player)
        {
            ArgumentNullException.ThrowIfNull(player);
            if (!player.IsBot)
            {
                throw new ArgumentException("player " + player.PlayerId + " is not a bot", nameof(player));
            }

            if (_byPlayer.ContainsKey(player.PlayerId))
            {
                throw new ArgumentException("bot " + player.PlayerId + " already moves", nameof(player));
            }

            Add(player);
        }

        public bool RemoveBot(int playerId)
        {
            return BotRoster.IsBotId(playerId) && _byPlayer.Remove(playerId);
        }

        // 人の入力の予算で縛らないのは、Bot はサーバーの tick ごとに 1 つしか作らないため。
        public bool ApplyBotInput(int playerId, InputFrame frame)
        {
            if (!BotRoster.IsBotId(playerId) || !_byPlayer.TryGetValue(playerId, out var mover))
            {
                return false;
            }

            if (frame.Tick <= mover.LastProcessedTick || !CharacterMotorInput.IsValid(frame.Move, frame.Yaw))
            {
                throw new ArgumentException("bot " + playerId + " input for tick " + frame.Tick + " is stale or not finite", nameof(frame));
            }

            Apply(mover, frame);
            return true;
        }

        // 突進のように歩きの速さを超えて動かす口。人の体の動き (PlayerMotor) は走る速さより速く進めないので、カプセルを直に動かす。
        // 下へも同じだけ寄せ、坂や段を下りても床に沿わせる。横の動きが壁に当たったら blocked を true にし、横に進んだ距離を返す
        public float DashBot(int playerId, Vector3 horizontal, float yawRadians, out bool blocked)
        {
            blocked = false;
            if (!float.IsFinite(horizontal.X) || !float.IsFinite(horizontal.Z) || !float.IsFinite(yawRadians))
            {
                throw new ArgumentOutOfRangeException(nameof(horizontal), horizontal, "dash and yaw must be finite");
            }

            if (!BotRoster.IsBotId(playerId) || !_byPlayer.TryGetValue(playerId, out var mover) || mover.Riding)
            {
                return 0f;
            }

            var flat = new Vector3(horizontal.X, 0f, horizontal.Z);
            var simulation = mover.Simulation;
            CancelMantle(mover);
            simulation.Place(mover.State);
            var before = simulation.Body.Position;
            var sides = simulation.Body.Move(flat);
            blocked = (sides & CapsuleContacts.Sides) != 0;
            var below = simulation.Body.Move(new Vector3(0f, -flat.Length(), 0f));
            var after = simulation.Body.Position;
            var moved = new Vector3(after.X - before.X, 0f, after.Z - before.Z).Length();
            var velocity = _tickSeconds > 0f ? (after - before) / _tickSeconds : Vector3.Zero;
            mover.State = new CharacterMotorState(after, velocity, (below & CapsuleContacts.Below) != 0, false);
            simulation.Place(mover.State);
            mover.Yaw = yawRadians;
            mover.Sprinting = false;
            mover.Aiming = false;
            Publish(mover);
            return moved;
        }
    }
}
