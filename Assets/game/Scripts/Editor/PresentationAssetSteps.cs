using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// 演出の仮素材を作る手順 (<see cref="IPresentationAssetStep"/>) をすべて集めて順に実行する入口。
    /// Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.PresentationAssetSteps.BuildAll</code>
    /// どれかの手順が例外を投げたら、どの手順かを付けた例外にして止める (batchmode では終了コードが 0 でなくなる)。
    /// </summary>
    public static class PresentationAssetSteps
    {
        public const string MenuPath = "Fps/Build All Presentation Assets";
        public const string LogTag = "[PresentationAssetSteps]";

        /// <summary>読み込まれているアセンブリから手順を集め (<see cref="Discover"/>)、順に実行する (<see cref="Run"/>)。</summary>
        [MenuItem(MenuPath)]
        public static void BuildAll()
        {
            Run(Discover());
        }

        /// <summary>読み込まれているアセンブリの <see cref="IPresentationAssetStep"/> の実装を <see cref="Collect"/> の規則で集め、実行の順に並べて返す。</summary>
        public static IReadOnlyList<IPresentationAssetStep> Discover()
        {
            return Sort(Collect(TypeCache.GetTypesDerivedFrom<IPresentationAssetStep>()));
        }

        /// <summary>
        /// types のうち、<see cref="IPresentationAssetStep"/> を実装した公開の具象クラスを作って返す。それ以外の型
        /// (インターフェース・抽象クラス・ジェネリックの定義・公開されていない型・実装していない型) は飛ばす。
        /// 公開されていない型を飛ばすのは、テストの中だけで使う手順が一括実行に混ざらないようにするため。
        /// </summary>
        /// <exception cref="ArgumentNullException">types が null。</exception>
        /// <exception cref="ArgumentException">types に null が入っている。</exception>
        /// <exception cref="InvalidOperationException">手順にあたる型が引数の無い公開コンストラクタを持たない、またはコンストラクタが例外を投げた。</exception>
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

        /// <summary>steps を実行の順 (<see cref="IPresentationAssetStep.Order"/> の小さい順、同じなら型の完全名の順) に並べ替えた新しい列を返す。</summary>
        /// <exception cref="ArgumentNullException">steps が null。</exception>
        /// <exception cref="ArgumentException">steps に null が入っている。</exception>
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

        /// <summary>
        /// steps を実行の順 (<see cref="Sort"/>) に 1 つずつ実行し、実行した手順の名前を順に返す。
        /// 手順が例外を投げたら残りは実行せず、手順の名前を付けた <see cref="InvalidOperationException"/> を投げる (元の例外は InnerException)。
        /// </summary>
        /// <exception cref="ArgumentNullException">steps が null。</exception>
        /// <exception cref="ArgumentException">steps に null が入っている。</exception>
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
