using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    public static class PresentationAssetSteps
    {
        public const string MenuPath = "Shooter/Build All Presentation Assets";
        public const string LogTag = "[PresentationAssetSteps]";

        [MenuItem(MenuPath)]
        public static void BuildAll()
        {
            Run(Discover());
        }

        public static IReadOnlyList<IPresentationAssetStep> Discover()
        {
            return Sort(Collect(TypeCache.GetTypesDerivedFrom<IPresentationAssetStep>()));
        }

        // 公開されていない型を飛ばすのは、テストの中だけで使う手順が一括実行に混ざらないようにするため。
        public static IReadOnlyList<IPresentationAssetStep> Collect(IEnumerable<Type> types)
        {
            if (types == null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            var steps = new List<IPresentationAssetStep>();
            foreach (var type in types)
            {
                if (type == null)
                {
                    throw new ArgumentException("types must not contain null", nameof(types));
                }

                if (!IsStepType(type))
                {
                    continue;
                }

                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    throw new InvalidOperationException(
                        $"{LogTag} presentation asset step '{type.FullName}' needs a public parameterless constructor");
                }

                try
                {
                    steps.Add((IPresentationAssetStep)Activator.CreateInstance(type));
                }
                catch (System.Reflection.TargetInvocationException e)
                {
                    throw new InvalidOperationException(
                        $"{LogTag} could not create presentation asset step '{type.FullName}': {e.InnerException?.Message}", e.InnerException ?? e);
                }
            }

            return steps;
        }

        public static IReadOnlyList<IPresentationAssetStep> Sort(IEnumerable<IPresentationAssetStep> steps)
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

            return list
                .OrderBy(step => step.Order)
                .ThenBy(step => step.GetType().FullName, StringComparer.Ordinal)
                .ToList();
        }

        public static IReadOnlyList<string> Run(IEnumerable<IPresentationAssetStep> steps)
        {
            var ordered = Sort(steps);
            var done = new List<string>(ordered.Count);
            for (var i = 0; i < ordered.Count; i++)
            {
                var step = ordered[i];
                var label = $"{step.Name} ({step.GetType().FullName}, order {step.Order.ToString(CultureInfo.InvariantCulture)})";
                Debug.Log($"{LogTag} running {(i + 1).ToString(CultureInfo.InvariantCulture)}/{ordered.Count.ToString(CultureInfo.InvariantCulture)} {label}");
                try
                {
                    step.Build();
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException($"{LogTag} step {label} failed: {e.Message}", e);
                }

                done.Add(step.Name);
            }

            Debug.Log($"{LogTag} built {done.Count.ToString(CultureInfo.InvariantCulture)} step(s): {string.Join(", ", done)}");
            return done;
        }

        private static bool IsStepType(Type type)
        {
            return typeof(IPresentationAssetStep).IsAssignableFrom(type)
                && type.IsClass
                && !type.IsAbstract
                && !type.ContainsGenericParameters
                && type.IsVisible;
        }
    }
}
