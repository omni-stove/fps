using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    // 中身が変わったときか、誰も案内を出していないときだけ出す。毎フレーム出し直すと、範囲の重なる物同士が毎フレーム案内を奪い合うため。
    public sealed class InteractHintSlot
    {
        private readonly object _source;
        private InteractHint? _published;

        public InteractHintSlot(object source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        // 案内を出していても、押して何も起きないとき (弾が満タンの弾薬箱など) は譲らせない。canInteract で呼ぶ側が決める。
        public void Show(object detail, bool canInteract = true)
        {
            KeySharing.SetClaim(InputActionPaths.Interact, _source, canInteract);
            var hint = new InteractHint(_source, detail);
            var signal = GameSignals.InteractHint;
            if (_published == hint && signal.HasValue)
            {
                return;
            }

            _published = hint;
            signal.Publish(hint);
        }

        public void Clear()
        {
            KeySharing.Release(InputActionPaths.Interact, _source);
            if (_published == null)
            {
                return;
            }

            GameSignals.InteractHint.ClearIfCurrent(_published);
            _published = null;
        }
    }
}
