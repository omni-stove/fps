namespace Fps.Core
{
    /// <summary><see cref="FootstepCadence.Advance"/> が鳴らすよう求める音の種類。</summary>
    public enum FootstepCueKind
    {
        /// <summary>何も鳴らさない。</summary>
        None = 0,

        /// <summary>足音。</summary>
        Footstep = 1,

        /// <summary>着地の音。</summary>
        Landing = 2,
    }
}
