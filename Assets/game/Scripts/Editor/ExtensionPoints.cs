using System;
using System.Collections.Generic;
using UnityEditor;

namespace Shooter.Editor
{
    // 拾うのを public な具象クラスだけにするのは、テストの中に private で置いた偽物を拾わないため。
    public static class ExtensionPoints
    {
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
