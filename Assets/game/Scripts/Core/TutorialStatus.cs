using System;

namespace Shooter.Core
{
    // Core に表示の文を持たせないよう、押すキーはアクションのパスと表記に使う入力機器で持ち、文にするのは UI に任せる。
    public readonly struct TutorialStatus : IEquatable<TutorialStatus>
    {
        public TutorialStatus(TutorialSteps steps, string actionPath, InputDeviceKind device)
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
            StepNumber = Math.Min(steps.CurrentIndex + 1, steps.Steps.Count);
            StepCount = steps.Steps.Count;
            Hits = steps.Hits;
            HitsRequired = steps.HitsRequired;
            ActionPath = IsComplete ? null : actionPath;
            Device = device;
        }

        public bool IsComplete { get; }

        public TutorialStep Step { get; }

        public int StepNumber { get; }

        public int StepCount { get; }

        public int Hits { get; }

        public int HitsRequired { get; }

        public string ActionPath { get; }

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
                && Device == other.Device;
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
            return HashCode.Combine(IsComplete, Step, StepNumber, Hits, HitsRequired, ActionPath, Device);
        }

        public override string ToString()
        {
            return IsComplete
                ? "complete"
                : $"{Step} {StepNumber}/{StepCount} hits {Hits}/{HitsRequired} {ActionPath} on {Device}";
        }
    }
}
