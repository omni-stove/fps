using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class MissionRunner : MonoBehaviour
    {
        public const string LogTag = MissionReporter.Tag;

        [SerializeField, RequiredReference, Tooltip("進めるミッションの定義")]
        private MissionDefinition _definition;

        private Mission _mission;
        private MissionReporter _reporter;

        public Mission Mission => _mission;

        public bool IsStarted => _mission != null;

        public MissionDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

        private void Awake()
        {
            if (!IsStarted && !TryStart())
            {
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            Stop();
        }

        public bool TryStart()
        {
            if (IsStarted)
            {
                return true;
            }

            if (_definition == null)
            {
                Debug.LogError($"{LogTag} '{name}' に MissionDefinition が設定されていません。", this);
                return false;
            }

            Mission mission;
            try
            {
                mission = _definition.CreateMission();
            }
            catch (MissionDefinitionException e)
            {
                Debug.LogError($"{LogTag} {e.Message}", _definition);
                return false;
            }

            Begin(mission);
            return true;
        }

        public void Progress(string id, int amount = 1)
        {
            if (CheckObjective(id))
            {
                _mission.Progress(id, amount);
            }
        }

        public void Fail(string id)
        {
            if (CheckObjective(id))
            {
                _mission.Fail(id);
            }
        }

        public static MissionRunner Resolve(MissionRunner explicitRunner, string objectiveId, UnityEngine.Object context)
        {
            var runner = explicitRunner != null ? explicitRunner : FindAnyObjectByType<MissionRunner>();
            if (runner == null)
            {
                Debug.LogError($"{LogTag} シーンに MissionRunner がありません。", context);
                return null;
            }

            // 実行順に関わらず解決できるよう、まだなら始める。
            if (!runner.IsStarted && !runner.TryStart())
            {
                Debug.LogError($"{LogTag} MissionRunner '{runner.name}' のミッションを開始できません。", context);
                return null;
            }

            if (string.IsNullOrWhiteSpace(objectiveId) || !runner.Mission.HasObjective(objectiveId))
            {
                Debug.LogError(
                    $"{LogTag} ミッション '{runner.Definition.name}' に目標 '{objectiveId}' がありません。", context);
                return null;
            }

            return runner;
        }

        private void Begin(Mission mission)
        {
            _mission = mission;
            _reporter = new MissionReporter(mission, line => Debug.Log(line, this));
            _reporter.ReportAll();
        }

        private void Stop()
        {
            _reporter?.Dispose();
            _reporter = null;
            _mission = null;
        }

        private bool CheckObjective(string id)
        {
            if (!IsStarted)
            {
                Debug.LogError($"{LogTag} '{name}' のミッションが始まっていません。", this);
                return false;
            }

            if (!_mission.HasObjective(id))
            {
                Debug.LogError($"{LogTag} 目標 '{id}' はありません。", this);
                return false;
            }

            return true;
        }
    }
}
