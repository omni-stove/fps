using System;
using System.Collections.Generic;
using UnityEditor;

namespace Fps.Editor
{
    /// <summary>
    /// 差し込み口 (<see cref="ITpsRangeFixture"/>・<see cref="ITpsActionSmokeStep"/> など) を実装した型を読み込み済みのアセンブリから探して作る。
    /// 拾うのは public な具象クラスだけで、テストの中に private で置いた偽物は拾わない。
    /// </summary>
    public static class ExtensionPoints
    {
        /// <summary>
        /// T を実装した public な具象クラスを 1 つずつ引数なしのコンストラクターで作り、order の小さい順 (同じなら型の完全名の順) に返す。
        /// </summary>
        /// <exception cref="ArgumentNullException">order が null。</exception>
        /// <exception cref="InvalidOperationException">拾う型に public な引数なしのコンストラクターが無い。</exception>
        public static IReadOnlyList<T> CreateAll<T>(Func<T, int> order)
            where T : class
        {
            if (order == null)
            {
                throw new ArgumentNullException(nameof(order));
            }

            var created = new List<T>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<T>())
            {
                if (!IsCandidate(type))
                {
                    continue;
                }

                var constructor = type.GetConstructor(Type.EmptyTypes);
                if (constructor == null)
                {
                    throw new InvalidOperationException(
                        $"{type.FullName} implements {typeof(T).Name} but has no public parameterless constructor to create it with");
                }

                created.Add((T)constructor.Invoke(null));
            }

            created.Sort((a, b) =>
            {
                var byOrder = order(a).CompareTo(order(b));
                return byOrder != 0 ? byOrder : string.CompareOrdinal(a.GetType().FullName, b.GetType().FullName);
            });
            return created;
        }

        /// <summary>type が <see cref="CreateAll{T}"/> で拾われる型か。public (入れ子なら外側も public) で、抽象でも開いたジェネリックでもない class。</summary>
        public static bool IsCandidate(Type type)
        {
            return type != null
                && type.IsClass
                && !type.IsAbstract
                && !type.ContainsGenericParameters
                && type.IsVisible;
        }
    }
}
