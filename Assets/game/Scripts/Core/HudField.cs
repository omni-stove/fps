namespace Fps.Core
{
    /// <summary>HUD の表示項目。<see cref="HudModel.Changed"/> でどれが変わったかを伝える。</summary>
    public enum HudField
    {
        Health,
        Ammo,
        Time,

        /// <summary>一時的な案内 (キャンペーンのセーブ・再開までの秒数、リスポーン・次の試合までの秒数)。</summary>
        Notice,

        /// <summary>チームの点数 (自分のチームが先)。チーム戦でなければ出さない。</summary>
        Score,

        /// <summary>試合結果 (勝ったチームか引き分け)。試合が終わっている間だけ出す。</summary>
        Result,

        /// <summary>持っている武器の表示名。武器が無ければ出さない。</summary>
        WeaponName,

        /// <summary>アクティブリロードの進捗と、成功になる区間。リロード中でなければ出さない。</summary>
        ReloadProgress,
    }
}
