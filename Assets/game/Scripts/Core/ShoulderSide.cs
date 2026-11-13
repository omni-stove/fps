namespace Fps.Core
{
    /// <summary>
    /// 三人称のカメラをキャラのどちらの肩越しに置くか。
    /// </summary>
    public enum ShoulderSide
    {
        /// <summary>右肩越し。三人称のオフセットをそのまま使う。</summary>
        Right,

        /// <summary>左肩越し。三人称のオフセットの x (左右) を反転する。</summary>
        Left,
    }
}
