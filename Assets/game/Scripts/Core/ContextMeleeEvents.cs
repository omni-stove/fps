using System;

namespace Fps.Core
{
    // 大きな秒数の Tick では位置合わせの終わりと結果が同じ回に起きるので、どちらも落とさないよう flags で返す。
    [Flags]
    public enum ContextMeleeEvents
    {
        None = 0,
        Aligned = 1 << 0,
        Resolved = 1 << 1,
    }
}
