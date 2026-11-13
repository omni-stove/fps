using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// 武器とルールのアセットと DestructionTest シーンを生成し直して Play Mode に入り、小屋の壁の下段を
    /// プレイヤーの launcher の値で <see cref="Hitscan.Fire"/> で撃って次を確かめ、Editor を終える。
    /// <list type="bullet">
    /// <item>プレイヤーが launcher を持って始まり、小屋は射撃場のルールどおり壊れる設定 (<see cref="DestructibleStructure.DestructionEnabled"/>) になっている。</item>
    /// <item><see cref="DestructibleStructure.DestructionEnabled"/> を false にして撃っても、どの部品も壊れず <c>[Destruction]</c> のログも出ない。</item>
    /// <item>true に戻して下段を撃っていくと、<c>[Destruction] ... collapsed=</c> に 1 つ以上の部品が出る。</item>
    /// <item>Play 中にエラー・例外・Assert のログが出ない。</item>
    /// </list>
    /// すべて満たせば終了コード 0、そうでなければ 1 で終わる。
    /// 小屋は角と屋根でつながっているので、1 発で下段の一部を壊しても崩落はしない。そのため、まだ壊れていない下段の部品を
    /// 添字の順に 1 つずつ外側から撃ち、崩落が出るまで続ける (上限 <see cref="MaxShots"/> 発)。
    /// Play Mode に入るので <c>-quit</c> は付けずに、Editor を閉じた状態で次のように呼ぶ:
    /// <code>unity -batchmode -projectPath . -executeMethod Fps.Editor.DestructionSmokeRun.Run -logFile -</code>
    /// </summary>
    [InitializeOnLoad]
    public static class DestructionSmokeRun
    {
        public const string LogTag = "[DestructionSmokeRun]";

        /// <summary>Play Mode に入ってから確かめ始めるまでに待つ秒数。GameBootstrap と建物の Awake を済ませるため。</summary>
        public const float WarmUpSeconds = 1f;

        /// <summary>Play Mode に入れないまま、これだけ待ったら失敗にする (秒)。</summary>
        public const float EnterTimeoutSeconds = 120f;

        /// <summary>撃つ位置を、狙う部品の外側の面からどれだけ離すか (m)。</summary>
        public const float ShotStandOff = 3f;

        /// <summary>崩落が出るまでに撃つ上限。下段の部品を 1 発ずつ 2 巡撃てる数。</summary>
        public const int MaxShots = DestructionTestSceneBuilder.GroundedPieceCount * 2;

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Fps.DestructionSmokeRun.Active";
        private const string RequestedAtKey = "Fps.DestructionSmokeRun.RequestedAt";
        private const string CollapsedKey = " collapsed=";

        private static readonly List<string> _errors = new List<string>();
        private static int _destructionLogs;
        private static int _collapsedPieces;
        private static double _playStartedAt = -1d;
        private static bool _finished;

        static DestructionSmokeRun()
        {
            // Play Mode に入るときにスクリプトが読み直された場合は、ここから続きを見張る。
            if (SessionState.GetBool(ActiveKey, false))
            {
                Attach();
            }
        }

        /// <summary>batchmode から <c>-executeMethod</c> で呼ぶ。終わると Editor を終了コード付きで閉じる。</summary>
        public static void Run()
        {
            try
            {
                WeaponAssetBuilder.Build();
                GameRulesAssetBuilder.Build();
                DestructionTestSceneBuilder.Build();
                EditorSceneManager.OpenScene(DestructionTestSceneBuilder.ScenePath, OpenSceneMode.Single);

                _errors.Clear();
                _destructionLogs = 0;
                _collapsedPieces = 0;
                _playStartedAt = -1d;
                _finished = false;
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
                Attach();

                Debug.Log($"{LogTag} entering play mode in '{DestructionTestSceneBuilder.ScenePath}'");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish(FailureExitCode, $"could not start: {e.Message}");
            }
        }

        /// <summary>
        /// <see cref="DestructibleStructure.FormatDestroyedLog"/> の行から、崩落した部品の数を返す。
        /// 別のログか、崩落が無ければ (<c>collapsed=-</c>) 0。
        /// </summary>
        public static int CountCollapsed(string logLine)
        {
            if (logLine == null || !logLine.StartsWith(DestructibleStructure.LogTag + " ", StringComparison.Ordinal))
            {
                return 0;
            }

            var at = logLine.LastIndexOf(CollapsedKey, StringComparison.Ordinal);
            if (at < 0)
            {
                return 0;
            }

            var value = logLine.Substring(at + CollapsedKey.Length).Trim();
            if (value.Length == 0 || value == "-")
            {
                return 0;
            }

            return value.Split(',').Length;
        }

        private static void Attach()
        {
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        private static void Detach()
        {
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= OnUpdate;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (_finished || !EditorApplication.isPlaying)
            {
                return;
            }

            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                // WeaponSwitchSmokeRun と同じく、Editor の起動時インデックスが Play 中に投げる例外はゲームのコードではないので外す。
                if (stackTrace != null && stackTrace.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup"))
                {
                    return;
                }

                _errors.Add($"{type}: {condition}");
                return;
            }

            if (condition != null && condition.StartsWith(DestructibleStructure.LogTag + " ", StringComparison.Ordinal))
            {
                _destructionLogs++;
                _collapsedPieces += CountCollapsed(condition);
            }
        }

        private static void OnUpdate()
        {
            if (_finished)
            {
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            if (!EditorApplication.isPlaying)
            {
                var requestedAt = SessionState.GetFloat(RequestedAtKey, (float)now);
                if (now - requestedAt > EnterTimeoutSeconds)
                {
                    Finish(FailureExitCode, $"did not enter play mode within {EnterTimeoutSeconds}s");
                }

                return;
            }

            if (_playStartedAt < 0d)
            {
                _playStartedAt = now;
                return;
            }

            if (now - _playStartedAt < WarmUpSeconds)
            {
                return;
            }

            var problems = new List<string>();
            try
            {
                Exercise(problems);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                problems.Add($"{e.GetType().Name}: {e.Message}");
            }

            problems.AddRange(_errors);
            Debug.Log(
                $"{LogTag} destruction logs={_destructionLogs.ToString(CultureInfo.InvariantCulture)} collapsed={_collapsedPieces.ToString(CultureInfo.InvariantCulture)} errors={_errors.Count.ToString(CultureInfo.InvariantCulture)}");
            if (problems.Count == 0)
            {
                Finish(SuccessExitCode, "passed");
            }
            else
            {
                Finish(FailureExitCode, "failed:\n  " + string.Join("\n  ", problems));
            }
        }

        /// <summary>シーンの小屋を launcher の値で撃ち、期待と違うことを problems に足す。</summary>
        private static void Exercise(List<string> problems)
        {
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            if (bootstrap == null || bootstrap.World == null)
            {
                problems.Add("the scene has no GameBootstrap that built the player");
                return;
            }

            var weapon = bootstrap.World.Weapon;
            if (weapon == null || !weapon.IsInitialized)
            {
                problems.Add("the player's WeaponController is not initialized");
                return;
            }

            // 自動の Update で入力から撃たれないよう、確かめる間は止める。
            weapon.enabled = false;
            var spec = weapon.Weapon.Spec;
            Expect(problems, spec.Id == WeaponAssetBuilder.LauncherId, $"the player starts with the launcher (got '{spec.Id}')");
            if (spec.Id != WeaponAssetBuilder.LauncherId)
            {
                return;
            }

            var structure = UnityEngine.Object.FindAnyObjectByType<DestructibleStructure>();
            if (structure == null)
            {
                problems.Add("the scene has no DestructibleStructure");
                return;
            }

            Expect(problems, structure.DestructionEnabled, "the hut is destructible under the range rules");
            Expect(
                problems,
                structure.Pieces.Count == DestructionTestSceneBuilder.PieceCount,
                $"the hut has {DestructionTestSceneBuilder.PieceCount} pieces (got {structure.Pieces.Count})");

            ExpectNothingBreaksWhileDisabled(problems, structure, spec);
            ExpectCollapse(problems, structure, spec);
        }

        private static void ExpectNothingBreaksWhileDisabled(List<string> problems, DestructibleStructure structure, WeaponSpec spec)
        {
            var target = FirstIntactGroundedPiece(structure);
            if (target == null)
            {
                problems.Add("the hut has no grounded piece to shoot");
                return;
            }

            var logsBefore = _destructionLogs;
            structure.DestructionEnabled = false;
            try
            {
                Shoot(problems, target, structure, spec);
            }
            finally
            {
                structure.DestructionEnabled = true;
            }

            Expect(problems, structure.Integrity.DestroyedCount == 0, $"nothing breaks while destruction is disabled (destroyed {structure.Integrity.DestroyedCount})");
            Expect(problems, _destructionLogs == logsBefore, "no [Destruction] log while destruction is disabled");
            foreach (var piece in structure.Pieces)
            {
                if (piece.IsBroken)
                {
                    problems.Add($"piece '{piece.name}' broke while destruction is disabled");
                }
            }
        }

        private static void ExpectCollapse(List<string> problems, DestructibleStructure structure, WeaponSpec spec)
        {
            var shots = 0;
            while (_collapsedPieces == 0 && shots < MaxShots)
            {
                var target = FirstIntactGroundedPiece(structure);
                if (target == null)
                {
                    break;
                }

                Shoot(problems, target, structure, spec);
                shots++;

                // 当たった部品には減衰前の被害がそのまま入り、launcher の 1 発は部品の耐久値を上回るので必ず壊れる。
                if (!structure.Integrity.IsDestroyed(target.Index) || !target.IsBroken)
                {
                    problems.Add($"shot {shots} did not break '{target.name}' (destroyed={structure.Integrity.IsDestroyed(target.Index)}, hidden={target.IsBroken})");
                    break;
                }
            }

            Debug.Log(
                $"{LogTag} shots={shots.ToString(CultureInfo.InvariantCulture)} destroyed={structure.Integrity.DestroyedCount.ToString(CultureInfo.InvariantCulture)}/{structure.Pieces.Count.ToString(CultureInfo.InvariantCulture)}");
            Expect(problems, _collapsedPieces > 0, $"shooting the bottom row collapses at least one piece within {MaxShots} shots (fired {shots})");
        }

        /// <summary>piece を小屋の外側から、その外側の面に垂直に撃つ。当たったのが piece でなければ problems に足す。</summary>
        private static void Shoot(List<string> problems, DestructiblePiece piece, DestructibleStructure structure, WeaponSpec spec)
        {
            var outward = OutwardNormal(piece, structure);
            var center = piece.Collider.bounds.center;
            var origin = center + (outward * ((DestructionTestSceneBuilder.PieceSize / 2f) + ShotStandOff));

            Physics.SyncTransforms();
            var result = Hitscan.Fire(origin, -outward, spec.Range, spec.Damage, spec.Falloff, null, spec.ExplosionRadius);
            if (!result.Hit || result.RaycastHit.collider != piece.Collider)
            {
                var hitName = result.Hit ? result.RaycastHit.collider.name : "nothing";
                problems.Add($"the shot at '{piece.name}' hit {hitName}");
            }
        }

        /// <summary>
        /// 壁の部品の外向きの法線 (水平)。小屋の中心から見て x と z の大きい方の軸を選ぶ。
        /// 角の部品は差が無いので z を選び、正面・背面の壁として撃つ (<see cref="DestructionTestSceneBuilder"/> は角をそちらに含める)。
        /// </summary>
        private static Vector3 OutwardNormal(DestructiblePiece piece, DestructibleStructure structure)
        {
            var offset = structure.transform.InverseTransformPoint(piece.Collider.bounds.center);
            var local = Mathf.Abs(offset.z) >= Mathf.Abs(offset.x)
                ? new Vector3(0f, 0f, Mathf.Sign(offset.z))
                : new Vector3(Mathf.Sign(offset.x), 0f, 0f);
            return structure.transform.TransformDirection(local);
        }

        private static DestructiblePiece FirstIntactGroundedPiece(DestructibleStructure structure)
        {
            foreach (var piece in structure.Pieces)
            {
                if (piece != null && piece.Grounded && !structure.Integrity.IsDestroyed(piece.Index))
                {
                    return piece;
                }
            }

            return null;
        }

        private static void Expect(List<string> problems, bool condition, string what)
        {
            if (!condition)
            {
                problems.Add(what);
            }
        }

        private static void Finish(int exitCode, string summary)
        {
            _finished = true;
            Detach();
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseFloat(RequestedAtKey);

            var line = $"{LogTag} {summary} (exit code {exitCode})";
            if (exitCode == SuccessExitCode)
            {
                Debug.Log(line);
            }
            else
            {
                Debug.LogError(line);
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
            else if (EditorApplication.isPlaying)
            {
                EditorApplication.ExitPlaymode();
            }
        }
    }
}
