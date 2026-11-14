using System;

namespace Fps.Protocol
{
    /// <summary><see cref="MatchSnapshot"/> の部分。</summary>
    [Flags]
    public enum MatchFields
    {
        None = 0,
        /// <summary>試合の進み・勝者・スコア・残り時間。</summary>
        Status = 1 << 0,
        Rules = 1 << 1,
        /// <summary>目標 (Domination など) とそのポイント。</summary>
        Objective = 1 << 2,
        All = Status | Rules | Objective,
    }
}
