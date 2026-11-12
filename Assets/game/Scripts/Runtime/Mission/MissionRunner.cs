using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// シーンで 1 つのミッションを進める。Awake で <see cref="MissionDefinition"/> から <see cref="Fps.Core.Mission"/> を作り、
    /// 状態の変化を <see cref="MissionReporter"/> の書式で Debug.Log に出す。
    /// トリガー (<see cref="ReachAreaTrigger"/> など) は <see cref="Resolve"/> でこれを見つけ、<see cref="Mission"/> を直接進める。
    /// 定義が無い・不正なときはエラーを出して自分を無効にする。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class MissionRunner : MonoBehaviour
    {
        public const string LogTag = MissionReporter.Tag;

        [SerializeField, Tooltip("進めるミッションの定義")]
        private MissionDefinition _definition;

        private Mission _mission;
        private MissionReporter _reporter;

        /// <summary>進めているミッション。開始前や開始に失敗したときは null。</summary>
        public Mission Mission => _mission;

        public bool IsStarted => _mission != null;

        /// <summary>使う定義。開始後に差し替えても進行中のミッションには影響しない。</summary>
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

        /// <summary>
        /// <see cref="Definition"/> からミッションを作って始める。既に始まっていれば何もせず true。
        /// 失敗したら理由を Debug.LogError に出して false を返す。
        /// </summary>
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

        /// <summary>id の目標を amount 進める。開始前や未知の目標ならエラーを出して何もしない。</summary>
        public void Progress(string id, int amount = 1)
        {
            if (CheckObjective(id))
            {
                _mission.Progress(id, amount);
            }
        }

        /// <summary>id の目標を失敗にする。開始前や未知の目標ならエラーを出して何もしない。</summary>
        public void Fail(string id)
        {
            if (CheckObjective(id))
            {
                _mission.Fail(id);
            }
        }

        /// <summary>
        /// トリガー用。explicitRunner があればそれを、無ければシーンにある MissionRunner を探して返す。
        /// 見つからない・ミッションが始まっていない・objectiveId が無いときは理由を Debug.LogError に出して null を返す。
        /// </summary>
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
