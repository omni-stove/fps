namespace Fps.Core
{
    /// <summary>はしごの 1 Tick の結果 (<see cref="LadderClimb.Step"/>) と、はしごを離れた理由。</summary>
    public enum LadderOutcome
    {
        /// <summary>つかまったまま。</summary>
        Climbing,

        /// <summary>上の端まで上り、上の足場に降りた。</summary>
        ExitTop,

        /// <summary>下の端まで下り (または途中で足が地面に着き)、地面に降りた。</summary>
        ExitBottom,

        /// <summary>ジャンプで離れた。</summary>
        JumpOff,

        /// <summary>ルールが Off になった・はしごが無くなったなど、外から離された。<see cref="LadderClimb.Step"/> は返さない。</summary>
        Released,
    }
}
