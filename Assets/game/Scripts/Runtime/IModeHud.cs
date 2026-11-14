using System.Collections.Generic;
using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>
    /// オフラインのモード (<see cref="ModeRunnerBase"/>) の HUD。残り時間・陣営ごとの得点・派生モードが足す行・試合結果を出す。
    /// 文は今の言語で文にして出し、言語が変わったら出し直す。
    /// </summary>
    public interface IModeHud
    {
        /// <summary>
        /// 中身を差し替えて出す。scores は上から並べる順、lines は得点の下に足す行。空の文の行は出さない。
        /// result が空の文なら結果の行を隠す。
        /// </summary>
        /// <exception cref="System.ArgumentNullException">scores が null。</exception>
        void Show(LocalizedText time, IReadOnlyList<ModeHudScoreLine> scores, IReadOnlyList<LocalizedText> lines, LocalizedText result);

        /// <summary>隠す。</summary>
        void Hide();
    }
}
