namespace Fps.Core
{
    /// <summary>
    /// 倒されたプレイヤーがいつ戻ってくるか。
    /// </summary>
    public enum RespawnMode
    {
        /// <summary>倒された人ごとに、リスポーン待ちが終わり次第戻る。</summary>
        Individual,

        /// <summary>一定間隔の波ごとに、待っている人をまとめて戻す。</summary>
        Wave,

        /// <summary>倒されたら戻らない。</summary>
        None,
    }
}
