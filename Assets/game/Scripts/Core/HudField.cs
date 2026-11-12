namespace Fps.Core
{
    /// <summary>HUD の表示項目。<see cref="HudModel.Changed"/> でどれが変わったかを伝える。</summary>
    public enum HudField
    {
        Health,
        Ammo,
        Time,

        /// <summary>一時的な案内 (キャンペーンのセーブ・再開までの秒数)。</summary>
        Notice,
    }
}
