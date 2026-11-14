namespace Fps.Core
{
    /// <summary>乗り物から降りた理由。</summary>
    public enum VehicleLeaveReason
    {
        /// <summary>自分で降りた。降りる場所がすべて塞がっていれば降りられない。</summary>
        Request = 0,

        /// <summary>乗り物が壊れた。</summary>
        Wrecked = 1,

        /// <summary>乗っている人が倒れた。</summary>
        RiderDied = 2,

        /// <summary>乗り物が無効になった・消えた。</summary>
        Disabled = 3,

        /// <summary>モードやスクリプトが降ろした。</summary>
        Forced = 4,

        /// <summary>乗っている人が消えた (破棄された)。置き直す相手がいないので、座席を空けるだけ。</summary>
        RiderRemoved = 5,
    }
}
