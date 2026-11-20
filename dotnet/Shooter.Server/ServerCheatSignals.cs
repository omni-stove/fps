using System;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // 違反の累積・統計の検知・通報の証拠が同じ出来事を読めるよう、出す側と読む側の間に 1 つ置く。
    // 戦闘と移動に直に event を足さないのは、読む側が両方のモジュールを受け取らずに済むようにするため。
    // 引数の並びは各 Raise の引数名と同じにする
    internal sealed class ServerCheatSignals
    {
        public event Action<int, Vector3, Vector3, float> ShotAccepted;

        public event Action<int, ShotRejection> ShotRejected;

        public event Action<int, int, HitZone, float> Hit;

        public event Action<int> InputOverBudget;

        // 入力は pitch を送らないので、pitch は直前に受け付けた射撃の向きから求め (PitchOf)、その後の最初の入力の 1 つにだけ載せ、他は null にする。
        // 入力に pitch を足すと通信の形が変わるため。射撃の tick は見ていた相手の時刻 (描画の tick) で入力の tick と時計が違うので、tick では合わせない
        public event Action<int, int, float, float?> Looked;

        // yaw と同じくラジアンで、上を向くと正、水平で 0 にする (VehicleWeaponAim.TryToAngles と同じ向き)。
        // 長さで割らずに Atan2 で求めるのは、射撃の向きが正規化されて届くとは限らないため
        internal static float PitchOf(Vector3 direction)
        {
            var flat = MathF.Sqrt((direction.X * direction.X) + (direction.Z * direction.Z));
            return MathF.Atan2(direction.Y, flat);
        }

        internal void RaiseShotAccepted(int shooterId, Vector3 origin, Vector3 direction, float shotTick)
        {
            ShotAccepted?.Invoke(shooterId, origin, direction, shotTick);
        }

        internal void RaiseShotRejected(int shooterId, ShotRejection reason)
        {
            ShotRejected?.Invoke(shooterId, reason);
        }

        internal void RaiseHit(int shooterId, int victimId, HitZone zone, float rewindTicks)
        {
            Hit?.Invoke(shooterId, victimId, zone, rewindTicks);
        }

        internal void RaiseInputOverBudget(int playerId)
        {
            InputOverBudget?.Invoke(playerId);
        }

        internal void RaiseLooked(int playerId, int tick, float yaw, float? pitch)
        {
            Looked?.Invoke(playerId, tick, yaw, pitch);
        }
    }
}
