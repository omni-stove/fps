using System;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    // 敵 (EnemyAgent) ではなく受け皿だけの的にするのは、射撃場に NavMesh が無く、敵を置くと NavMesh の外にいるエラーで smoke が落ちるため。
    public sealed class SuppressionRangeFixture : ITpsRangeFixture
    {
        public const string TargetName = "SuppressionTarget";

        // 乗り越えの壁 (x = -10) の奥、標的ダミーの列より左奥。撃つ線がほかの置き物を横切らない場所。
        public static readonly Vector3 TargetGround = new Vector3(-14f, 0f, 16f);

        public const float TargetHeight = GameBootstrap.PlayerHeight;

        // Unity のカプセルの既定の太さ。拡大せずに済む。
        public const float TargetDiameter = 1f;

        public static Vector3 TargetCenter => TargetGround + (Vector3.up * (TargetHeight / 2f));

        public int Order => 0;

        public string Name => TargetName;

        public Bounds Footprint => new Bounds(TargetCenter, new Vector3(TargetDiameter, TargetHeight, TargetDiameter));

        public GameObject Place(Scene scene, GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            try
            {
                target.name = TargetName;
                SceneManager.MoveGameObjectToScene(target, scene);
                target.transform.SetPositionAndRotation(TargetCenter, Quaternion.identity);
                target.transform.localScale = new Vector3(TargetDiameter, TargetHeight / 2f, TargetDiameter);
                target.isStatic = true;

                // Configure は登録とログを伴うので、シーンに書くだけの値はシリアライズされた欄へ直接入れる。
                var receiver = target.AddComponent<SuppressionReceiver>();
                var serialized = new SerializedObject(receiver);
                serialized.FindProperty("_rule").enumValueIndex = (int)rules.Actions.Suppression;
                serialized.FindProperty("_id").stringValue = TargetName;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return target;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(target);
                throw;
            }
        }
    }
}
