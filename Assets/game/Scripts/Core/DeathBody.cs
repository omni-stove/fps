namespace Fps.Core
{
    /// <summary>
    /// 倒されたプレイヤーの体をどう見せるか。
    /// </summary>
    public enum DeathBody
    {
        /// <summary>倒された時点で体を消す。</summary>
        Vanish,

        /// <summary>体をその場に倒れさせる。</summary>
        Collapse,
    }
}
