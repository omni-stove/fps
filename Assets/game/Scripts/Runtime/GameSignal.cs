using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class GameSignal<T>
    {
        private readonly string _name;

        internal GameSignal(string name)
        {
            _name = name;
        }

        public event Action<T> Changed;

        public T Value { get; private set; }

        public bool HasValue => !EqualityComparer<T>.Default.Equals(Value, default);

        // 同じ値でも毎回 Changed を呼ぶのは、中身が変わる参照型の値を出し直しで知らせられるようにするため。
        public void Publish(T value)
        {
            Value = value;
            Notify(value);
        }

        public void Clear()
        {
            if (!HasValue)
            {
                return;
            }

            Value = default;
            Notify(default);
        }

        // 出した側が片付けるときに、後から別の側が出した値まで消さないよう、今の値が expected のときだけ消す。
        public bool ClearIfCurrent(T expected)
        {
            if (!HasValue || !EqualityComparer<T>.Default.Equals(Value, expected))
            {
                return false;
            }

            Clear();
            return true;
        }

        internal void Reset()
        {
            Value = default;
            Changed = null;
        }

        private void Notify(T value)
        {
            var handlers = Changed;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<T>)handler)(value);
                }
                catch (Exception e)
                {
                    Debug.LogError($"{GameSignals.LogTag} a subscriber of {_name} threw");
                    Debug.LogException(e);
                }
            }
        }

        public override string ToString()
        {
            return _name + "=" + (HasValue ? Value.ToString() : "(none)");
        }
    }
}
