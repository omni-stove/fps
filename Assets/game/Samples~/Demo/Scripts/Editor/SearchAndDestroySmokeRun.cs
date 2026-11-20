using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shooter.Editor
{
    // batchmode には入力が無いので、買い物はプレイヤーの代わりにここから RoundEconomy.Buy を呼ぶ。撃ち合うのは bot だけになる。
    // 決着まで待つとラウンドを何度も回して長くなるので、買えて・ラウンドが終わって・報酬が配られるまでを確かめる。
    // Play Mode に入るときにスクリプトが読み直されても続きを走らせられるよう、状態は SessionState に置く。
    // Play Mode に入るので、batchmode で呼ぶときは -quit を付けない。
    [InitializeOnLoad]
    public static class SearchAndDestroySmokeRun
    {
        public const string LogTag = "[SearchAndDestroySmokeRun]";

        // GameBootstrap の Awake と Runner の Start を済ませてから見始める。準備の時間 (既定 5 秒) より短くして、最初の準備のうちに買う。
        public const float WarmUpSeconds = 1f;

        // 始まる前は 1 ラウンドの長さがまだ分からないので、始まるまでをこれだけ待つ。
        public const float StartTimeoutSeconds = 30f;

        public const float GraceSeconds = 30f;

        public const float EnterTimeoutSeconds = 120f;

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Shooter.SearchAndDestroySmokeRun.Active";
        private const string RequestedAtKey = "Shooter.SearchAndDestroySmokeRun.RequestedAt";

        private static readonly string _economyStartedPrefix = EconomyLog.Tag + " started ";
        private static readonly string _economyPlayerPrefix = EconomyLog.Tag + " player=";
        private static readonly string _boughtPart = " bought item=";
        private static readonly string _roundRewardPart = " round ";
        private static readonly string _roundEndPrefix = SearchAndDestroyLog.Tag + " round_end ";

        private static readonly List<string> _errors = new List<string>();
        private static Step _step;
        private static int _economyStarts;
        private static int _boughtLines;
        private static int _roundRewards;
        private static string _boughtItem;
        private static int _moneyBefore;
        private static int _moneyAfter;
        private static int _rewardsExpected;
        private static string _roundEndLine;
        private static float _timeoutSeconds = StartTimeoutSeconds;
        private static double _playStartedAt = -1d;
        private static bool _finished;

        public enum LineKind
        {
            Other,
            EconomyStarted,
            Bought,
            RoundReward,
            RoundEnded,
        }

        private enum Step
        {
            CheckStart,
            Buy,
            WaitRoundEnd,
            Done,
        }

        static SearchAndDestroySmokeRun()
        {
            // Play Mode に入るときにスクリプトが読み直された場合は、ここから続きを見張る。
            if (SessionState.GetBool(ActiveKey, false))
            {
                Attach();
            }
        }

        public static void Run()
        {
            try
            {
                WeaponAssetBuilder.Build();
                GameRulesAssetBuilder.Build();
                SearchAndDestroySceneBuilder.Build();
                EditorSceneManager.OpenScene(SearchAndDestroySceneBuilder.ScenePathValue, OpenSceneMode.Single);

                var runner = UnityEngine.Object.FindAnyObjectByType<SearchAndDestroyRunner>();
                if (runner == null)
                {
                    throw new InvalidOperationException(
                        $"'{SearchAndDestroySceneBuilder.ScenePathValue}' has no SearchAndDestroyRunner; rerun {SearchAndDestroySceneBuilder.MenuPath}");
                }

                if (runner.EffectiveEconomy == null)
                {
                    throw new InvalidOperationException(
                        $"the SearchAndDestroyRunner in '{SearchAndDestroySceneBuilder.ScenePathValue}' has no economy; rerun {SearchAndDestroySceneBuilder.MenuPath}");
                }

                ResetObservations();
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
                Attach();

                Debug.Log($"{LogTag} entering play mode in '{SearchAndDestroySceneBuilder.ScenePathValue}'");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish(FailureExitCode, $"could not start: {e.Message}");
            }
        }

        public static LineKind Classify(string line)
        {
            if (line == null)
            {
                return LineKind.Other;
            }

            if (line.StartsWith(_economyStartedPrefix, StringComparison.Ordinal))
            {
                return LineKind.EconomyStarted;
            }

            if (line.StartsWith(_economyPlayerPrefix, StringComparison.Ordinal))
            {
                if (line.Contains(_boughtPart))
                {
                    return LineKind.Bought;
                }

                return line.Contains(_roundRewardPart) ? LineKind.RoundReward : LineKind.Other;
            }

            return line.StartsWith(_roundEndPrefix, StringComparison.Ordinal) ? LineKind.RoundEnded : LineKind.Other;
        }

        // 安い順に試すのは、始めの所持金で買える品を確実に 1 つ買うため。同じ値段なら一覧の順を保つ。
        public static IReadOnlyList<BuyItem> AffordableByPrice(BuyCatalog catalog, int money)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var items = new List<(BuyItem Item, int Index)>();
            for (var i = 0; i < catalog.Items.Count; i++)
            {
                var item = catalog.Items[i];
                if (item.Price <= money)
                {
                    items.Add((item, i));
                }
            }

            items.Sort((a, b) => a.Item.Price != b.Item.Price ? a.Item.Price.CompareTo(b.Item.Price) : a.Index.CompareTo(b.Index));
            var sorted = new BuyItem[items.Count];
            for (var i = 0; i < sorted.Length; i++)
            {
                sorted[i] = items[i].Item;
            }

            return sorted;
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

        private static void ResetObservations()
        {
            _errors.Clear();
            _step = Step.CheckStart;
            _economyStarts = 0;
            _boughtLines = 0;
            _roundRewards = 0;
            _boughtItem = null;
            _moneyBefore = 0;
            _moneyAfter = 0;
            _rewardsExpected = 0;
            _roundEndLine = null;
            _timeoutSeconds = StartTimeoutSeconds;
            _playStartedAt = -1d;
            _finished = false;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            // Play Mode に入る前 (アセットとシーンの生成) のログは判定に使わない。
            if (_finished || !EditorApplication.isPlaying)
            {
                return;
            }

            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                // CampaignSmokeRun と同じく、Editor の起動時インデックスが Play 中に投げる例外はゲームのコードではないので外す。
                if (stackTrace != null && stackTrace.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup"))
                {
                    return;
                }

                _errors.Add($"{type}: {condition}");
                return;
            }

            switch (Classify(condition))
            {
                case LineKind.EconomyStarted:
                    _economyStarts++;
                    break;
                case LineKind.Bought:
                    _boughtLines++;
                    break;
                case LineKind.RoundReward:
                    // 買う前に終わったラウンドの報酬まで数えると、買った後のラウンドを待たずに通ってしまう。
                    if (_step == Step.WaitRoundEnd)
                    {
                        _roundRewards++;
                    }

                    break;
                case LineKind.RoundEnded:
                    if (_step == Step.WaitRoundEnd)
                    {
                        _roundEndLine = condition;
                    }

                    break;
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

            string problem;
            try
            {
                problem = Advance();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                problem = $"{e.GetType().Name} at step {_step}: {e.Message}";
            }

            var timeout = WarmUpSeconds + _timeoutSeconds;
            if (problem == null && _step != Step.Done && now - _playStartedAt > timeout)
            {
                problem = $"step {_step} did not finish within {timeout.ToString("0.#", CultureInfo.InvariantCulture)}s of play";
            }

            if (problem == null && _step != Step.Done && _errors.Count == 0)
            {
                return;
            }

            Report(problem);
        }

        private static string Advance()
        {
            var runner = UnityEngine.Object.FindAnyObjectByType<SearchAndDestroyRunner>();
            if (runner == null)
            {
                return "the scene has no SearchAndDestroyRunner";
            }

            switch (_step)
            {
                case Step.CheckStart:
                    return CheckStart(runner);

                case Step.Buy:
                    return Buy(runner);

                case Step.WaitRoundEnd:
                    return WaitRoundEnd(runner);

                default:
                    return null;
            }
        }

        private static string CheckStart(SearchAndDestroyRunner runner)
        {
            var game = runner.Game;
            if (!runner.IsStarted || game == null)
            {
                return "the SearchAndDestroyRunner did not start a match; see the errors above";
            }

            if (runner.LocalPlayer == null)
            {
                return $"the match has no local player to buy for; rerun {SearchAndDestroySceneBuilder.MenuPath}";
            }

            var economy = runner.RoundEconomy;
            if (economy == null || _economyStarts == 0)
            {
                return $"the match started without the buy economy; rerun {SearchAndDestroySceneBuilder.MenuPath}";
            }

            if (economy.Economy.Catalog.Count == 0)
            {
                return "the buy list is empty; check Resources/Tuning/economy.json";
            }

            // 最初の準備に間に合わなくても次のラウンドの準備で買えるよう、2 ラウンド分と爆弾の時限を待つ。
            var spec = game.Spec;
            _timeoutSeconds = (2f * (spec.PreparationSeconds + spec.RoundSeconds)) + spec.FuseSeconds + GraceSeconds;
            _step = Step.Buy;
            return null;
        }

        private static string Buy(SearchAndDestroyRunner runner)
        {
            var game = runner.Game;
            var roundEconomy = runner.RoundEconomy;
            if (game == null || roundEconomy == null)
            {
                return "the SearchAndDestroyRunner stopped the match before anything was bought";
            }

            if (runner.IsMatchOver)
            {
                return "the match ended before a buy time opened";
            }

            var economy = roundEconomy.Economy;
            if (game.Phase != RoundPhase.Preparing || !economy.IsBuyTime)
            {
                return null;
            }

            var playerId = ModeRunnerBase.LocalPlayerId;
            var money = economy.MoneyOf(playerId);
            var refusals = new List<string>();
            foreach (var item in AffordableByPrice(economy.Catalog, money))
            {
                var verdict = roundEconomy.Buy(playerId, item.Id);
                if (verdict == PurchaseVerdict.Bought)
                {
                    _boughtItem = item.Id;
                    _moneyBefore = money;
                    _moneyAfter = economy.MoneyOf(playerId);
                    if (_moneyAfter != money - item.Price)
                    {
                        return $"buying {item.Id} for {item.Price} left {_moneyAfter} of {money}";
                    }

                    if (_boughtLines == 0)
                    {
                        return $"bought {item.Id} but no purchase was logged";
                    }

                    _rewardsExpected = System.Linq.Enumerable.Count(economy.PlayerIds);
                    _step = Step.WaitRoundEnd;
                    return null;
                }

                refusals.Add($"{item.Id}={verdict}");
            }

            return refusals.Count == 0
                ? $"nothing in the buy list costs {money.ToString(CultureInfo.InvariantCulture)} or less"
                : $"the player with {money.ToString(CultureInfo.InvariantCulture)} could not buy anything: {string.Join(", ", refusals)}";
        }

        private static string WaitRoundEnd(SearchAndDestroyRunner runner)
        {
            if (_roundEndLine == null || _roundRewards < _rewardsExpected)
            {
                return runner.Game != null ? null : "the SearchAndDestroyRunner stopped the match before the round ended";
            }

            _step = Step.Done;
            return null;
        }

        private static void Report(string problem)
        {
            var problems = new List<string>();
            if (problem != null)
            {
                problems.Add(problem);
            }

            problems.AddRange(_errors);
            var played = _playStartedAt >= 0d ? EditorApplication.timeSinceStartup - _playStartedAt : 0d;
            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "{0} step={1} played={2:0.#}s bought={3} money={4}->{5} round rewards={6}/{7} round end={8} errors={9}",
                LogTag,
                _step,
                played,
                _boughtItem ?? "-",
                _moneyBefore,
                _moneyAfter,
                _roundRewards,
                _rewardsExpected,
                _roundEndLine ?? "-",
                _errors.Count));

            if (problems.Count == 0 && _step == Step.Done)
            {
                Finish(SuccessExitCode, $"passed (bought {_boughtItem}; {_roundEndLine})");
            }
            else
            {
                if (problems.Count == 0)
                {
                    problems.Add("not every step was confirmed");
                }

                Finish(FailureExitCode, "failed:\n  " + string.Join("\n  ", problems));
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

            if (SmokeSuite.TakeOver(LogTag, exitCode))
            {
                return;
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
