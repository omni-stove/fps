namespace Fps.Core
{
    /// <summary>
    /// サーバーが射撃を捨てる理由。<see cref="ShotValidator.Check"/> の結果に、連射の制限 (<see cref="ShotBudget"/>) と
    /// 弾倉の状態 (<see cref="ServerWeapon.TryFire"/>)・持ち替え中かどうか (<see cref="Loadout{T}"/>)、撃った武器の Id の検査 (<see cref="ServerLoadout"/>) を加えたもの。
    /// </summary>
    public enum ShotRejection
    {
        /// <summary>受け付ける。</summary>
        None,

        /// <summary>始点に NaN か無限大が入っている。</summary>
        InvalidOrigin,

        /// <summary>向きが 0 か、NaN / 無限大が入っている。</summary>
        InvalidDirection,

        /// <summary>時点の tick が負か、割合が 0 以上 1 未満でない。</summary>
        InvalidTime,

        /// <summary>始点が、サーバーの知る撃った本人の位置から離れすぎている。</summary>
        OriginTooFar,

        /// <summary>撃った本人の目の位置から始点までの間に遮蔽物がある (Net 側で判定する)。</summary>
        OriginBlocked,

        /// <summary>連射の間隔より速く撃っている (<see cref="ShotBudget.TryConsume"/> が false)。</summary>
        FireRateExceeded,

        /// <summary>撃った本人に武器が渡されていない (試合が武器の設定に失敗したなど)。</summary>
        NoWeapon,

        /// <summary>リロード中で撃てない。</summary>
        Reloading,

        /// <summary>弾倉が空で撃てない (予備弾があればサーバーがリロードを始める)。</summary>
        OutOfAmmo,

        /// <summary>送られてきた武器の Id が、サーバーが試合で配るロードアウトに無い (<see cref="ServerLoadout.Contains"/>)。</summary>
        UnknownWeapon,

        /// <summary>送られてきた武器の Id が、サーバーが持たせている武器と違う (<see cref="ServerLoadout.CheckWeapon"/>)。</summary>
        WeaponMismatch,

        /// <summary>武器を持ち替えている途中で撃てない (<see cref="Loadout{T}.IsSwitching"/>)。</summary>
        Switching,

        /// <summary>
        /// サーバーの知るカバーの状態ではブラインドファイアになるのに、高い壁で端にいないので撃てない
        /// (<see cref="BlindFireShot.TryResolveOrigin"/> が false)。弾は減らさない。
        /// </summary>
        BlindFireBlocked,
    }
}
