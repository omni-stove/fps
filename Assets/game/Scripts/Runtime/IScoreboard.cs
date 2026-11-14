using System.Collections.Generic;
using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>参加者のキル・デス・接続状態をチームごとに一覧にする表示。</summary>
    public interface IScoreboard
    {
        /// <summary>
        /// 一覧の中身を差し替える。entries が並べられない (null・同じ Id が 2 つ・知らないチーム) ときはエラーを出して前の一覧を残す。
        /// </summary>
        /// <param name="entries">参加者。順番は問わない。</param>
        /// <param name="firstTeam">先に出すチーム。</param>
        void SetEntries(IEnumerable<ScoreboardEntry> entries, Team firstTeam);

        /// <summary>一覧を空にする。</summary>
        void ClearEntries();
    }
}
