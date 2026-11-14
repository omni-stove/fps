namespace Fps.Core
{
    /// <summary>はしごにどこからつかまるか (<see cref="LadderSpan.Probe"/>)。</summary>
    public enum LadderEntry
    {
        /// <summary>つかまらない。</summary>
        None,

        /// <summary>はしごの前 (地面か途中の高さ) から、はしごへ進んでつかまる。</summary>
        Front,

        /// <summary>上の端の足場から、はしごへ進んでつかまる。</summary>
        Top,
    }
}
