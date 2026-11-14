namespace Fps.Core
{
    /// <summary><see cref="ParachuteMotion.Evaluate"/> の、パラシュートを開けるかの判定。</summary>
    public enum ParachuteOpenVerdict
    {
        /// <summary>開ける。</summary>
        Open,

        /// <summary>ルールの <see cref="Parachute"/> が Off なので開けない。</summary>
        RuleOff,

        /// <summary>もう開いている。</summary>
        AlreadyOpen,

        /// <summary>ほかの動き (はしごなど) が移動を受け持っているので開けない。</summary>
        Busy,

        /// <summary>足元の地面まで <see cref="ParachuteMotion.MinOpenHeight"/> に足りない (接地しているときも含む)。</summary>
        TooLow,
    }
}
