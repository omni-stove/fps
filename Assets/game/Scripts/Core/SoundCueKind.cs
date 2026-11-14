namespace Fps.Core
{
    /// <summary>字幕 (<see cref="SubtitleLines"/>) にする音の合図の種類。字幕は種類ごとに 1 行にまとめる。</summary>
    public enum SoundCueKind
    {
        /// <summary>銃声。</summary>
        Gunshot,

        /// <summary>足音と着地の音。</summary>
        Footstep,

        /// <summary>爆発 (グレネードなど)。</summary>
        Explosion,

        /// <summary>リロードの音。</summary>
        Reload,
    }
}
