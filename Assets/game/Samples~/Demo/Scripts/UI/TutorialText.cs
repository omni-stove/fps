using System;
using Shooter.Core;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    public static class TutorialText
    {
        public static LocalizedText CompletedText => new LocalizedText("tutorial.completed");

        public static LocalizedText For(TutorialStatus status)
        {
            return status.IsComplete ? CompletedText : Prompt(status, KeyLabelFor(status.ActionPath, status.Device));
        }

        public static LocalizedText Prompt(TutorialStatus status, string keyLabel)
        {
            if (status.IsComplete)
            {
                return CompletedText;
            }

            var body = Instruction(status.Step, keyLabel, status.HitsRequired);
            if (status.Step == TutorialStep.Shoot)
            {
                body = new LocalizedText("tutorial.shootProgress", body, status.Hits, status.HitsRequired);
            }

            return new LocalizedText("tutorial.prompt", status.StepNumber, status.StepCount, body);
        }

        public static LocalizedText Instruction(TutorialStep step, string keyLabel, int hitsRequired = TutorialSteps.DefaultHitsRequired)
        {
            if (string.IsNullOrWhiteSpace(keyLabel))
            {
                throw new ArgumentException("keyLabel must not be empty", nameof(keyLabel));
            }

            if (hitsRequired < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(hitsRequired), hitsRequired, "hitsRequired must be at least 1");
            }

            var key = keyLabel.Trim();
            switch (step)
            {
                case TutorialStep.Move:
                case TutorialStep.Aim:
                case TutorialStep.Shoot:
                case TutorialStep.Reload:
                case TutorialStep.Crouch:
                case TutorialStep.Cover:
                    return new LocalizedText("tutorial.step." + step, key, hitsRequired);
                default:
                    throw new ArgumentOutOfRangeException(nameof(step), step, "unknown tutorial step");
            }
        }

        public static string KeyLabelFor(string actionPath, InputDeviceKind device)
        {
            if (string.IsNullOrWhiteSpace(actionPath))
            {
                throw new ArgumentException("actionPath must not be empty", nameof(actionPath));
            }

            var actionName = actionPath.Substring(actionPath.LastIndexOf('/') + 1);
            var actions = InputSystem.actions;
            var action = actions != null ? actions.FindAction(actionPath) : null;
            if (action == null)
            {
                return actionName;
            }

            var label = TutorialKeyLabels.For(action, device);
            return string.IsNullOrWhiteSpace(label) ? actionName : label;
        }
    }
}
