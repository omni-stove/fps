using System;

namespace Fps.Protocol
{
    /// <summary><see cref="ControlPointSnapshot"/> の部分。</summary>
    [Flags]
    public enum ControlPointFields
    {
        None = 0,
        /// <summary>占領しているチーム・進み・押し合い。</summary>
        State = 1 << 0,
        All = State,
    }
}
