using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="GameSignals"/> の項目 1 つ。今の値 (<see cref="Value"/>) と、値を出したときのイベント (<see cref="Changed"/>) を持つ。
    /// ゲーム進行が <see cref="Publish"/> / <see cref="Clear"/> で書き、UI が読んで購読する。値が無いときは <c>default</c> (参照型と null 許容型は null)。
    /// </summary>
    /// <remarks>
    /// <see cref="Publish"/> は同じ値でも毎回 <see cref="Changed"/> を呼ぶ (中身が変わる参照型の値を、出し直しで知らせられるように)。
    /// 購読者が例外を投げてもゲーム進行は止めず、例外をログに出して残りの購読者を呼ぶ。
    /// </remarks>
    public sealed class GameSignal<T>
    {
        private readonly string _name;

        internal GameSignal(string name)
        {
            _name = name;
        }

        /// <summary>値を出したときと消したときに、新しい値を渡して呼ばれる。</summary>
        public event Action<T> Changed;

        /// <summary>今の値。出していなければ <c>default</c>。</summary>
        public T Value { get; private set; }

        /// <summary>値を出しているか (<see cref="Value"/> が <c>default</c> でない)。</summary>
        public bool HasValue => !EqualityComparer<T>.Default.Equals(Value, default);

        /// <summary>value を今の値にして <see cref="Changed"/> を呼ぶ。</summary>
        public void Publish(T value)
        {
            Value = value;
            Notify(value);
        }

        /// <summary>値を消す。出していたときだけ <see cref="Changed"/> を <c>default</c> で呼ぶ。</summary>
        public void Clear()
        {
            if (!HasValue)
            {
                return;
            }

            Value = default;
            Notify(default);
        }

        /// <summary>
        /// 今の値が expected のときだけ消し、消したら true。出した側が片付けるときに、後から別の側が出した値まで消さないために使う。
        /// </summary>
        public bool ClearIfCurrent(T expected)
        {
            if (!HasValue || !EqualityComparer<T>.Default.Equals(Value, expected))
            {
                return false;
            }

            Clear();
            return true;
        }

        /// <summary>値を消し、購読者もすべて外す。<see cref="GameSignals"/> がシーンを読む前に空へ戻すときに使う。</summary>
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
