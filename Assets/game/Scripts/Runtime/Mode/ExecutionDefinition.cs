using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 味方を起こせる距離と秒数はルール (ActionRules) にあるので持たない。
    [CreateAssetMenu(fileName = "Execution", menuName = "Shooter/Modes/Execution")]
    public sealed class ExecutionDefinition : ScriptableObject
    {
        [SerializeField, Min(1), Tooltip("試合に勝つのに要るラウンドの勝ち数")]
        private int _winsToWin = ExecutionRunner.DefaultWinsToWin;

        [SerializeField, Min(0f), Tooltip("各ラウンドの開始前の待ち (秒)")]
        private float _preparationSeconds = ExecutionRunner.DefaultPreparationSeconds;

        [SerializeField, Min(1f), Tooltip("1 ラウンドの制限時間 (秒)。切れたらダウンしていない人の多いチームの勝ち")]
        private float _roundTimeLimitSeconds = ExecutionRunner.DefaultRoundTimeLimitSeconds;

        [SerializeField, Min(0.1f), Tooltip("とどめを刺すのに Interact を押し続ける秒数")]
        private float _executeSeconds = ExecutionRunner.DefaultExecuteSeconds;

        [SerializeField, Min(0.1f), Tooltip("ダウン中の敵にとどめを刺せる距離 (m)。足元どうしで測る。味方を起こせる距離はルールの reviveRange")]
        private float _executeRange = ExecutionRunner.DefaultExecuteRange;

        public int WinsToWin
        {
            get => _winsToWin;
            set => _winsToWin = value;
        }

        public float PreparationSeconds
        {
            get => _preparationSeconds;
            set => _preparationSeconds = value;
        }

        public float RoundTimeLimitSeconds
        {
            get => _roundTimeLimitSeconds;
            set => _roundTimeLimitSeconds = value;
        }

        public float ExecuteSeconds
        {
            get => _executeSeconds;
            set => _executeSeconds = value;
        }

        public float ExecuteRange
        {
            get => _executeRange;
            set => _executeRange = value;
        }

        public ExecutionSession CreateExecution(ActionRules actions)
        {
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            if (!(_executeRange > 0f) || float.IsInfinity(_executeRange))
            {
                throw new InvalidOperationException(
                    $"execution definition '{SourceName}' is invalid: executeRange must be positive and finite (was {_executeRange})");
            }

            try
            {
                return ExecutionRunner.CreateExecution(_winsToWin, _preparationSeconds, _roundTimeLimitSeconds, actions, _executeSeconds);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new InvalidOperationException($"execution definition '{SourceName}' is invalid: {e.Message}", e);
            }
        }

        // 勝ち数・待ち・制限時間はネット対戦ではラウンドの欄 (NetMatchDefinition) を使うので、とどめの値だけを渡す
        public ExecutionSpec ToSpec()
        {
            try
            {
                return new ExecutionSpec(_executeSeconds, _executeRange);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new InvalidOperationException($"execution definition '{SourceName}' is invalid: {e.Message}", e);
            }
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed execution definition)" : name;
    }
}
