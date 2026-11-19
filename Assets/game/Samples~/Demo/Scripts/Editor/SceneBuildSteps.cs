using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shooter.Editor
{
    // シーンごとのメニューを並べると一覧が長くなり、作り直し忘れも起きるので、全シーンをこの 1 つで作り直す。
    public static class SceneBuildSteps
    {
        public const string MenuPath = "Shooter/Build All Scenes";
        public const string LogTag = "[SceneBuildSteps]";

        // ネットのシーン (Shooter.Samples.Net.Editor) はこの後に足すので、間を空けておく。
        public const int NetOrder = 1000;

        public static IReadOnlyList<ISceneBuildStep> BuiltIn => new ISceneBuildStep[]
        {
            new SceneBuildStep("Bootstrap", 0, BootstrapSceneBuilder.Build),
            new SceneBuildStep("TpsRange", 10, TpsRangeSceneBuilder.Build),
            new SceneBuildStep("Features", 20, FeatureSceneBuilder.Build),
            new SceneBuildStep("EnemyAiTest", 30, EnemyTestSceneBuilder.Build),
            new SceneBuildStep("WaveTest", 40, WaveTestSceneBuilder.Build),
            new SceneBuildStep("DestructionTest", 50, DestructionTestSceneBuilder.Build),
            new SceneBuildStep("MissionTest", 60, MissionTestSceneBuilder.Build),
            new SceneBuildStep("VehicleRange", 70, VehicleRangeSceneBuilder.Build),
            new SceneBuildStep("ModeTest", 100, ModeTestSceneBuilder.Build),
            new SceneBuildStep("FreeForAll", 110, FreeForAllSceneBuilder.Build),
            new SceneBuildStep("KillConfirmed", 120, KillConfirmedSceneBuilder.Build),
            new SceneBuildStep("CaptureTheFlag", 130, CaptureTheFlagSceneBuilder.Build),
            new SceneBuildStep("Domination", 140, DominationSceneBuilder.Build),
            new SceneBuildStep("Hardpoint", 150, HardpointSceneBuilder.Build),
            new SceneBuildStep("SearchAndDestroy", 160, SearchAndDestroySceneBuilder.Build),
            new SceneBuildStep("Execution", 170, ExecutionSceneBuilder.Build),
            new SceneBuildStep("GunGame", 180, GunGameSceneBuilder.Build),
            new SceneBuildStep("Infected", 190, InfectedSceneBuilder.Build),
            new SceneBuildStep("Horde", 200, HordeSceneBuilder.Build),
            new SceneBuildStep("Guardian", 210, GuardianSceneBuilder.Build),
            new SceneBuildStep("Extraction", 220, ExtractionSceneBuilder.Build),
            new SceneBuildStep("BattleRoyale", 230, BattleRoyaleSceneBuilder.Build),
            new SceneBuildStep("Campaign", 300, CampaignSceneBuilder.Build),

            // メニューは作戦の一覧を載せるので、作戦のシーンの後に作る。
            new SceneBuildStep("MainMenu", 310, MenuSceneBuilder.Build),
        };

        [MenuItem(MenuPath)]
        public static void BuildAll()
        {
            // 各シーンの組み立ては開いているシーンを置き換えるので、未保存の変更を最初に一度だけ確かめる。
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(LogTag + " cancelled");
                return;
            }

            Run(Discover());
        }

        public static IReadOnlyList<ISceneBuildStep> Discover()
        {
            return Sort(BuiltIn.Concat(Collect(TypeCache.GetTypesDerivedFrom<ISceneBuildStep>())));
        }

        // 公開されていない型を飛ばすのは、BuiltIn で入れた SceneBuildStep やテストだけの手順を二重に走らせないため。
        public static IReadOnlyList<ISceneBuildStep> Collect(IEnumerable<Type> types)
        {
            if (types == null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            var steps = new List<ISceneBuildStep>();
            foreach (var type in types)
            {
                if (type == null)
                {
                    throw new ArgumentException("types must not contain null", nameof(types));
                }

                if (!typeof(ISceneBuildStep).IsAssignableFrom(type) || !type.IsClass || type.IsAbstract || type.ContainsGenericParameters || !type.IsVisible)
                {
                    continue;
                }

                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    throw new InvalidOperationException($"{LogTag} scene build step '{type.FullName}' needs a public parameterless constructor");
                }

                steps.Add((ISceneBuildStep)Activator.CreateInstance(type));
            }

            return steps;
        }

        public static IReadOnlyList<ISceneBuildStep> Sort(IEnumerable<ISceneBuildStep> steps)
        {
            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            var list = steps.ToList();
            if (list.Contains(null))
            {
                throw new ArgumentException("steps must not contain null", nameof(steps));
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var step in list)
            {
                if (!names.Add(step.Name))
                {
                    throw new ArgumentException($"scene build step '{step.Name}' is listed twice", nameof(steps));
                }
            }

            return list.OrderBy(step => step.Order).ThenBy(step => step.Name, StringComparer.Ordinal).ToList();
        }

        public static IReadOnlyList<string> Run(IEnumerable<ISceneBuildStep> steps)
        {
            var ordered = Sort(steps);
            var done = new List<string>(ordered.Count);
            for (var i = 0; i < ordered.Count; i++)
            {
                var step = ordered[i];
                Debug.Log($"{LogTag} building {(i + 1).ToString(CultureInfo.InvariantCulture)}/{ordered.Count.ToString(CultureInfo.InvariantCulture)} {step.Name}");
                try
                {
                    step.Build();
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException($"{LogTag} step {step.Name} failed: {e.Message}", e);
                }

                done.Add(step.Name);
            }

            Debug.Log($"{LogTag} built {done.Count.ToString(CultureInfo.InvariantCulture)} step(s): {string.Join(", ", done)}");
            return done;
        }
    }

    internal sealed class SceneBuildStep : ISceneBuildStep
    {
        private readonly Action _build;

        public SceneBuildStep(string name, int order, Action build)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("name must not be blank", nameof(name));
            }

            Name = name;
            Order = order;
            _build = build ?? throw new ArgumentNullException(nameof(build));
        }

        public string Name { get; }

        public int Order { get; }

        public void Build() => _build();
    }
}
