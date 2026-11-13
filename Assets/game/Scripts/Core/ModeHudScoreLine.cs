namespace Fps.Core
{
    /// <summary>モードの HUD に出す、陣営 1 つ分の得点の行。<see cref="ModeHudText.Scores"/> が作る。</summary>
    public readonly struct ModeHudScoreLine
    {
        /// <summary>行の陣営。</summary>
        public Side Side { get; }

        /// <summary>出す文字列 (陣営名と得点)。</summary>
        public string Text { get; }

        /// <summary>自分の陣営なら true。</summary>
        public bool IsOwn { get; }

        /// <summary>単独でいちばん得点が多い陣営なら true。</summary>
        public bool IsLeading { get; }

        public ModeHudScoreLine(Side side, string text, bool isOwn, bool isLeading)
        {
            Side = side;
            Text = text ?? string.Empty;
            IsOwn = isOwn;
            IsLeading = isLeading;
        }
    }
}
