using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    /// <summary>
    /// 1 つの物 (source) が <see cref="GameSignals.InteractHint"/> に出す案内。毎フレーム <see cref="Show"/> を呼んでよい。
    /// </summary>
    /// <remarks>
    /// 中身が変わったときか、誰も案内を出していないときだけ出す。毎フレーム出し直すと、範囲の重なる物同士が毎フレーム案内を奪い合うため。
    /// 中身が変わった物は前の物の案内を置き換え、置き換えられた物はその案内が消えたら出し直す。
    /// </remarks>
    public sealed class InteractHintSlot
    {
        private readonly object _source;
        private InteractHint? _published;

        /// <exception cref="ArgumentNullException">source が null。</exception>
        public InteractHintSlot(object source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>detail の案内を出す。</summary>
        /// <exception cref="ArgumentNullException">detail が null。</exception>
        public void Show(object detail)
        {
            var hint = new InteractHint(_source, detail);
            var signal = GameSignals.InteractHint;
            if (_published == hint && signal.HasValue)
            {
                return;
            }

            _published = hint;
            signal.Publish(hint);
        }

        /// <summary>出した案内が今も出ていれば消す。別の物が出した案内は消さない。</summary>
        public void Clear()
        {
            if (_published == null)
            {
                return;
            }

            GameSignals.InteractHint.ClearIfCurrent(_published);
            _published = null;
        }
    }
}
