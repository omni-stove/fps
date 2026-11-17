using System;

namespace Shooter.Core
{
    // Unity 抜きのサーバーとクライアントの予測が同じはしごで動くよう、シーンからではなく NetMapLadders で書き出したものを読む。
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
