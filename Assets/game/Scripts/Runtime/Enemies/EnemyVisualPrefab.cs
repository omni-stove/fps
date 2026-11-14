using UnityEngine;

namespace Fps.Runtime
{
    // 敵の目印や銃は弾・視線・突進や爆発の線を遮ってはいけない。差し替えのプレハブに当たり判定が付いていても外して置く。
    internal static class EnemyVisualPrefab
    {
        public static GameObject Instantiate(GameObject prefab, Transform parent)
        {
            var instance = Object.Instantiate(prefab, parent, false);
            instance.name = prefab.name;
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
            {
                // Destroy は次のフレームまで残り、その間に射撃や NavMeshAgent の判定に当たるので、すぐ消す。
                Object.DestroyImmediate(collider);
            }

            return instance;
        }
    }
}
