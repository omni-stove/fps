using System;
using Fps.Core;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// 射撃場のチュートリアルの段階 (<see cref="TutorialStatus"/>) を、画面に出す 1 行の文にする。
    /// 押すキーの表記は段階のアクションと入力機器から、プロジェクト全体のアクション (<see cref="InputSystem.actions"/>) の今の割り当てで引く。
    /// </summary>
    public static class TutorialText
    {
        /// <summary>すべて終えたときに出す文。</summary>
        public static LocalizedText CompletedText => new LocalizedText("tutorial.completed");

        /// <summary>status を、今の割り当てのキーで案内する文にする。すべて終えていれば <see cref="CompletedText"/>。</summary>
        public static LocalizedText For(TutorialStatus status)
        {
            return status.IsComplete ? CompletedText : Prompt(status, KeyLabelFor(status.ActionPath, status.Device));
        }

        /// <summary>
        /// 画面に出す文。今の手順を keyLabel のキーで案内し、頭に「[今の番号/手順の数]」を付ける (日本語の表で <c>[1/6] WASD で移動</c>)。
        /// 射撃の手順では当てた回数も付ける。すべて終えたら <see cref="CompletedText"/>。
        /// </summary>
        /// <param name="status">チュートリアルの今の段階。</param>
        /// <param name="keyLabel">今の手順で使うキー (またはボタン) の表記。空にはできない。すべて終えていれば使わない。</param>
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

        /// <summary>step を keyLabel のキーで行うよう案内する文 (番号や進みは付けない)。</summary>
        /// <param name="step">案内する手順。</param>
        /// <param name="keyLabel">操作に使うキーの表記。空にはできない。</param>
        /// <param name="hitsRequired">射撃の手順で当てる回数。1 以上。</param>
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

        /// <summary>
        /// actionPath のアクションを device で行うキーの表記 (<see cref="TutorialKeyLabels"/>)。プロジェクト全体のアクションから引き、
        /// アクションか device の割り当てが無ければアクションの名前 (Move など) を返す。
        /// </summary>
        /// <exception cref="ArgumentException">actionPath が空。</exception>
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
