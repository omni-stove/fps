namespace Fps.Core
{
    /// <summary>音の合図が聞こえた向き。聞いている人の向きから見た 8 方位で、向きが分からなければ <see cref="None"/>。</summary>
    public enum SoundCueDirection
    {
        /// <summary>向きが分からない (位置の無い音・真上や真下の音)。字幕には向きを書かない。</summary>
        None,

        Front,
        FrontRight,
        Right,
        BackRight,
        Back,
        BackLeft,
        Left,
        FrontLeft,
    }
}
