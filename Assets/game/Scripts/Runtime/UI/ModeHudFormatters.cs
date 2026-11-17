using System;
using System.Collections.Generic;
using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>
    /// モードの状態 (<see cref="ModeStatus"/>) を <see cref="ModeHudView"/> に出す文にする役。<see cref="ModeStatus.Detail"/> の型ごとに
    /// <see cref="ModeHudFormatters"/> に登録する。
    /// </summary>
    public interface IModeHudFormatter
    {
        /// <summary>残り時間。空の文なら時間の行を隠す。</summary>
        LocalizedText Time(ModeStatus status);

        /// <summary>陣営ごとの得点の行 (上から)。null は返さない。</summary>
        IReadOnlyList<ModeHudScoreLine> Scores(ModeStatus status);

        /// <summary>得点の下に足す行を lines に入れる。lines は空で渡される。空の文の行は出さない。</summary>
        void AppendLines(ModeStatus status, List<LocalizedText> lines);

        /// <summary>試合の結果。空の文なら結果の行を隠す。</summary>
        LocalizedText Result(ModeStatus status);
    }

    /// <summary>
    /// <see cref="ModeStatus.Detail"/> の型ごとの書式役 (<see cref="IModeHudFormatter"/>) の置き場。
    /// 引くときは Detail の型そのもの、無ければその基底の型の順に探し、どれも登録が無ければ (Detail が null のときも)
    /// 残り時間・得点・リスポーンまでの秒数・結果だけを出す既定の書式役 (<see cref="Default"/>) を返す。
    /// </summary>
    /// <remarks>
    /// 登録はシーンを読む前に済ませる。同じ型を登録し直すと置き換わるので、ドメインの再読み込みを切った Play で登録を繰り返しても重ならない。
    /// </remarks>
    public static class ModeHudFormatters
    {
        private static readonly Dictionary<Type, IModeHudFormatter> _formatters = new Dictionary<Type, IModeHudFormatter>();

        /// <summary>登録の無いモードの書式役。残り時間・陣営ごとの得点・リスポーンまでの秒数・結果を <see cref="ModeStatus"/> の値から出す。</summary>
        public static IModeHudFormatter Default { get; } = new DefaultFormatter();

        /// <summary>Detail が TDetail (とその派生) のモードの書式役を formatter にする。</summary>
        /// <exception cref="ArgumentNullException">formatter が null。</exception>
        public static void Register<TDetail>(IModeHudFormatter formatter)
            where TDetail : class
        {
            Register(typeof(TDetail), formatter);
        }

        /// <summary>Detail が detailType (とその派生) のモードの書式役を formatter にする。</summary>
        /// <exception cref="ArgumentNullException">detailType か formatter が null。</exception>
        public static void Register(Type detailType, IModeHudFormatter formatter)
        {
            if (detailType == null)
            {
                throw new ArgumentNullException(nameof(detailType));
            }

            _formatters[detailType] = formatter ?? throw new ArgumentNullException(nameof(formatter));
        }

        /// <summary>detailType の登録を外す。外したら true。</summary>
        /// <exception cref="ArgumentNullException">detailType が null。</exception>
        public static bool Unregister(Type detailType)
        {
            if (detailType == null)
            {
                throw new ArgumentNullException(nameof(detailType));
            }

            return _formatters.Remove(detailType);
        }

        /// <summary>status の Detail の型の書式役。登録が無ければ <see cref="Default"/>。</summary>
        /// <exception cref="ArgumentNullException">status が null。</exception>
        public static IModeHudFormatter For(ModeStatus status)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            for (var type = status.Detail?.GetType(); type != null; type = type.BaseType)
            {
                if (_formatters.TryGetValue(type, out var formatter))
                {
                    return formatter;
                }
            }

            return Default;
        }

        private sealed class DefaultFormatter : ModeHudFormatterBase
        {
        }
    }
}
