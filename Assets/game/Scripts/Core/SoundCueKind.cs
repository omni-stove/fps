namespace Shooter.Core
{
    /// <summary>字幕にする音の合図の種類。</summary>
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

        /// <summary>投げた物が落ちた音 (注意をそらす能力)。</summary>
        Clatter,
    }
}
