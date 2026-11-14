using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 乗り物に乗れる者 (プレイヤー・敵・Bot)。<see cref="Vehicle"/> が座席を決め、乗った・移った・降りたことをここへ知らせる。
    /// 体を座席に留める・操作を手放す・降りる場所に置き直すのは乗る側の役目。
    /// 乗り降りを始めるのは <see cref="Vehicle.TryEnter(IVehicleRider)"/>・<see cref="Vehicle.TryExit"/> (プレイヤーは Interact、敵と Bot はコードから)。
    /// </summary>
    public interface IVehicleRider
    {
        /// <summary>ログに出す名前。</summary>
        string RiderName { get; }

        /// <summary>体の根元。乗り込める距離を測り、降りる場所の塞がりを調べるときに自分の Collider を除くのに使う。</summary>
        Transform RiderRoot { get; }

        /// <summary>根元から足元までの高さ (m)。座る位置・降りる位置は足元の位置なので、根元はこれだけ上に置く。</summary>
        float FeetBelowRoot { get; }

        /// <summary>降りる場所が塞がっているかを調べるカプセルの半径 (m)。</summary>
        float ClearanceRadius { get; }

        /// <summary>降りる場所が塞がっているかを調べるカプセルの高さ (m)。</summary>
        float ClearanceHeight { get; }

        /// <summary>今乗れるか (倒れていないかなど)。</summary>
        bool CanBoard { get; }

        /// <summary>今乗っている座席。乗っていなければ null。</summary>
        VehicleSeat Seat { get; }

        /// <summary>seat に座った。</summary>
        void OnBoarded(VehicleSeat seat);

        /// <summary>同じ乗り物の中で from から to へ移った。</summary>
        void OnSeatChanged(VehicleSeat from, VehicleSeat to);

        /// <summary>seat から降りた。足元を feetPosition、向きを rotation に置き直す。</summary>
        void OnLeft(VehicleSeat seat, VehicleLeaveReason reason, Vector3 feetPosition, Quaternion rotation);
    }
}
