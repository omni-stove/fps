using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    public interface IModeHudFormatter
    {
        LocalizedText Time(ModeStatus status);

        IReadOnlyList<ModeHudScoreLine> Scores(ModeStatus status);

        void AppendLines(ModeStatus status, List<LocalizedText> lines);

        LocalizedText Result(ModeStatus status);
    }

    // 同じ型の登録を例外にせず置き換えるのは、ドメインの再読み込みを切った Play で登録を繰り返しても重ならないようにするため。
    public static class ModeHudFormatters
    {
        private static readonly Dictionary<Type, IModeHudFormatter> _formatters = new Dictionary<Type, IModeHudFormatter>();

        public static IModeHudFormatter Default { get; } = new DefaultFormatter();

        public static void Register<TDetail>(IModeHudFormatter formatter)
            where TDetail : class
        {
            Register(typeof(TDetail), formatter);
        }

        public static void Register(Type detailType, IModeHudFormatter formatter)
        {
            if (detailType == null)
            {
                throw new ArgumentNullException(nameof(detailType));
            }

            _formatters[detailType] = formatter ?? throw new ArgumentNullException(nameof(formatter));
        }

        public static bool Unregister(Type detailType)
        {
            if (detailType == null)
            {
                throw new ArgumentNullException(nameof(detailType));
            }

            return _formatters.Remove(detailType);
        }

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
