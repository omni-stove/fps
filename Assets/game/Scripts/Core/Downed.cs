namespace Fps.Core
{
    /// <summary>
    /// 体力が 0 になったプレイヤーをすぐ倒すか、ダウンさせて蘇生を待つか。
    /// </summary>
    public enum Downed
    {
        /// <summary>体力が 0 になったらすぐ倒れる。</summary>
        Off,

        /// <summary>体力が 0 になったらダウンし、出血で倒れるまでの間に味方が起こせる。</summary>
        Revive,
    }
}
