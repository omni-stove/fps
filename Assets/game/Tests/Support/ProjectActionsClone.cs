using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Tests.EditMode
{
    // 割り当ての上書きはアセットに残り、ほかのテストやエディタの操作に効いてしまうので、上書きを試すテストは本物ではなくこの写しを使う。
    // 割り当ての id は JSON に入っているので、写しにも同じ id が付き、保存した上書きを別の写しに読み込める (起動し直したのと同じ)。
    public static class ProjectActionsClone
    {
        public static InputActionAsset Create()
        {
            var project = InputSystem.actions;
            Assert.That(project, Is.Not.Null, "Project-wide Input Actions must be assigned");
            var clone = InputActionAsset.FromJson(project.ToJson());
            clone.name = project.name + " (test copy)";
            return clone;
        }

        public static void Destroy(InputActionAsset clone)
        {
            if (clone != null)
            {
                clone.Disable();
                Object.DestroyImmediate(clone);
            }
        }
    }
}
