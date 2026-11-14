using System;

namespace Fps.Core
{
    /// <summary>
    /// 対戦マップに置いたはしご 1 本の形と調整値。Unity 抜きのサーバーとクライアントの予測が同じはしごで動くよう、
    /// <see cref="NetMapLadders"/> で書き出したものを読む。
    /// </summary>
    public sealed class MapLadder
    {
        public MapLadder(LadderSpan span, LadderSpec spec)
        {
            if (span.Height <= 0f)
            {
                throw new ArgumentException("span must be a constructed LadderSpan", nameof(span));
            }

            Span = span;
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public LadderSpan Span { get; }

        public LadderSpec Spec { get; }
    }
}
