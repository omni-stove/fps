using System;

namespace Fps.Core
{
    /// <summary>
    /// 射撃場のチュートリアルの今の段階。今の手順と何番目か・射撃の手順で当てた回数・押すキーのアクション (<see cref="ActionPath"/>) と
    /// その表記に使う入力機器 (<see cref="Device"/>) を持つ。文にするのは UI。
    /// </summary>
    public readonly struct TutorialStatus : IEquatable<TutorialStatus>
    {
        /// <summary>steps の今の段階。</summary>
        /// <param name="steps">チュートリアルの進み。</param>
        /// <param name="actionPath">今の手順で押すアクションのパス (Player/Move など)。すべて終えていれば使わず null にする。</param>
        /// <param name="device">キーの表記に使う入力機器。</param>
        /// <exception cref="ArgumentNullException">steps が null。</exception>
        /// <exception cref="ArgumentException">終えていない手順で actionPath が空。</exception>
        /// <param name="keyLabel">出したときのキーの表記。割り当てを替えたとき段階が変わって出し直されるように持つ。null なら UI が引く。</param>
        public TutorialStatus(TutorialSteps steps, string actionPath, InputDeviceKind device, string keyLabel = null)
        {
            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            IsComplete = steps.IsComplete;
            if (!IsComplete && string.IsNullOrWhiteSpace(actionPath))
            {
                throw new ArgumentException("actionPath must not be empty before the tutorial is complete", nameof(actionPath));
            }

            Step = IsComplete ? default : steps.Current;
            StepNumber = Math.Min(steps.CurrentIndex + 1, TutorialSteps.Order.Count);
            StepCount = TutorialSteps.Order.Count;
            Hits = steps.Hits;
            HitsRequired = steps.HitsRequired;
            ActionPath = IsComplete ? null : actionPath;
            Device = device;
            KeyLabel = IsComplete ? null : keyLabel;
        }

        /// <summary>出したときのキーの表記。無ければ null。<see cref="IsComplete"/> なら null。</summary>
        public string KeyLabel { get; }

        /// <summary>すべての手順を終えたか。</summary>
        public bool IsComplete { get; }

        /// <summary>今の手順。<see cref="IsComplete"/> なら使わない。</summary>
        public TutorialStep Step { get; }

        /// <summary>今の手順の番号 (1 始まり)。すべて終えたら <see cref="StepCount"/>。</summary>
        public int StepNumber { get; }

        /// <summary>手順の数。</summary>
        public int StepCount { get; }

        /// <summary>射撃の手順で、ここまでに当てた回数。他の手順では 0。</summary>
        public int Hits { get; }

        /// <summary>射撃の手順で当てる回数。</summary>
        public int HitsRequired { get; }

        /// <summary>今の手順で押すアクションのパス。<see cref="IsComplete"/> なら null。</summary>
        public string ActionPath { get; }

        /// <summary>キーの表記に使う入力機器 (最後に操作した機器)。</summary>
        public InputDeviceKind Device { get; }

        public bool Equals(TutorialStatus other)
        {
            return IsComplete == other.IsComplete
                && Step == other.Step
                && StepNumber == other.StepNumber
                && StepCount == other.StepCount
                && Hits == other.Hits
                && HitsRequired == other.HitsRequired
                && string.Equals(ActionPath, other.ActionPath, StringComparison.Ordinal)
                && Device == other.Device
                && string.Equals(KeyLabel, other.KeyLabel, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is TutorialStatus other && Equals(other);
        }

        public static bool operator ==(TutorialStatus left, TutorialStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TutorialStatus left, TutorialStatus right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(IsComplete, Step, StepNumber, Hits, HitsRequired, ActionPath, Device, KeyLabel);
        }

        public override string ToString()
        {
            return IsComplete
                ? "complete"
                : $"{Step} {StepNumber}/{StepCount} hits {Hits}/{HitsRequired} {ActionPath} on {Device}";
        }
    }
}
