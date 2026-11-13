namespace Fps.Core
{
    /// <summary>
    /// 素早く横や後ろへ飛び退けるか。
    /// </summary>
    public enum Dodge
    {
        /// <summary>飛び退けない。</summary>
        Off,

        /// <summary>飛び退ける (<see cref="MotorSpec.DodgeDistance"/>・<see cref="MotorSpec.DodgeSeconds"/>)。</summary>
        On,
    }
}
