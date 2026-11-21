using System;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // 入力によらずに体を置き直す・飛ばす・座席に乗せる口。ルール・戦闘・乗り物のモジュールが呼ぶ
    internal sealed partial class ServerMovement
    {
        public KnockbackSpec Knockback => _knockback;

        public bool Teleport(int playerId, Vector3 position)
        {
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            if (!_byPlayer.TryGetValue(playerId, out var mover))
            {
                return false;
            }

            // 掛けた先は置き直す前の場所なので、縄は外す
            ReleaseGrapple(mover, GrappleEnd.Interrupted);
            mover.Simulation.SpawnPosition = position;
            mover.State = new CharacterMotorState(position, Vector3.Zero, false, false);
            mover.Simulation.Place(mover.State);
            Publish(mover);
            return true;
        }

        // 指定の高さに置き、入力で開くときと同じルールと高さの判定でパラシュートを開いて降ろす (Battle Royale の降下など)。
        // Teleport と違って出現の位置を替えないのは、落下の下限で戻す先を空の上にしないため。
        // 開けなかった (ルールで切ってある・低すぎる) ときもその位置に置いたまま落とし、置けた人がいれば true を返す。
        public bool Drop(int playerId, Vector3 position)
        {
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            if (!_byPlayer.TryGetValue(playerId, out var mover))
            {
                return false;
            }

            ReleaseGrapple(mover, GrappleEnd.Interrupted);
            var simulation = mover.Simulation;
            mover.State = simulation.Deploy(
                new CharacterMotorState(position, Vector3.Zero, false, false), _settings.Rules.Actions.Parachute == Parachute.On);
            simulation.Place(mover.State);
            if (simulation.LastParachuteVerdict.HasValue)
            {
                _log.WriteLine(ServerMovementLog.ParachuteOpen(
                    playerId, mover.LastProcessedTick, simulation.LastParachuteVerdict.Value, simulation.LastParachuteHeight));
            }

            Publish(mover);
            return true;
        }

        // 持ち主の予測がその状態から入力をやり直せるよう、最後に処理した入力の後の状態を吹き飛ばし、動くのは次の入力からにする。
        public bool Launch(int playerId, Vector3 velocity)
        {
            if (!float.IsFinite(velocity.X) || !float.IsFinite(velocity.Y) || !float.IsFinite(velocity.Z))
            {
                throw new ArgumentOutOfRangeException(nameof(velocity), velocity, "velocity must be finite");
            }

            if (!_byPlayer.TryGetValue(playerId, out var mover))
            {
                return false;
            }

            // 吹き飛ばしはオフラインでも縄より強い (GrappleMovement.OverridePriority) ので、縄を外して飛ばす
            ReleaseGrapple(mover, GrappleEnd.Interrupted);
            mover.State = CharacterMotorSimulation.Launch(mover.State, velocity);
            _log.WriteLine(ServerMovementLog.KnockbackLaunched(playerId, mover.LastProcessedTick, velocity));
            return true;
        }

        // 座席への置き直しを Teleport にしないのは、巻き戻しの記録を捨てずに、乗っている人にも遅れを補って当てるため。
        public bool Ride(int playerId, Vector3 position, Vector3 velocity, bool crouched)
        {
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            if (!_byPlayer.TryGetValue(playerId, out var mover))
            {
                return false;
            }

            ReleaseGrapple(mover, GrappleEnd.Interrupted);
            mover.Riding = true;
            mover.State = new CharacterMotorState(position, velocity, true, crouched);
            mover.Simulation.Place(mover.State);
            Publish(mover);
            return true;
        }

        public bool Dismount(int playerId)
        {
            if (!_byPlayer.TryGetValue(playerId, out var mover) || !mover.Riding)
            {
                return false;
            }

            mover.Riding = false;
            mover.State = new CharacterMotorState(mover.State.Position, Vector3.Zero, false, false);
            mover.Simulation.Place(mover.State);
            Publish(mover);
            return true;
        }

        public bool IsRiding(int playerId)
        {
            return _byPlayer.TryGetValue(playerId, out var mover) && mover.Riding;
        }

        public bool SetDowned(int playerId, bool downed)
        {
            if (!_byPlayer.TryGetValue(playerId, out var mover))
            {
                return false;
            }

            mover.Downed = downed;
            if (downed)
            {
                ReleaseGrapple(mover, GrappleEnd.Interrupted);
            }

            return true;
        }
    }
}
