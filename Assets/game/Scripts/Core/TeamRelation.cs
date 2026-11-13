namespace Fps.Core
{
    /// <summary>見ている人から見た相手 (プレイヤー・拠点の持ち主など) の立場。色は <see cref="TeamPalette.ColorOf(TeamRelation)"/> で引く。</summary>
    public enum TeamRelation
    {
        /// <summary>見ている人と同じチーム。</summary>
        Friendly,

        /// <summary>見ている人と違うチーム。</summary>
        Enemy,

        /// <summary>どのチームのものでもない (持ち主のいない拠点など)。</summary>
        Neutral,
    }
}
