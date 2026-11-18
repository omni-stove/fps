using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

namespace Shooter.Tests.EditMode
{
    public sealed class InputActionFieldsTests
    {
        private static IEnumerable<FieldInfo> InputActionFieldsOfUnityObjects()
        {
            var types = TypeCache.GetTypesDerivedFrom<UnityEngine.Object>()
                .Where(t => IsKitAssembly(t.Assembly.GetName().Name));
            foreach (var type in types)
            {
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType == typeof(InputAction) || field.FieldType == typeof(InputAction[]))
                    {
                        yield return field;
                    }
                }
            }
        }

        private static bool IsKitAssembly(string assemblyName)
        {
            return assemblyName.StartsWith("Shooter.", StringComparison.Ordinal) && !assemblyName.StartsWith("Shooter.Tests", StringComparison.Ordinal);
        }

        [Test]
        public void TheKitHasInputActionFieldsToCheck()
        {
            Assert.That(
                InputActionFieldsOfUnityObjects().Select(f => f.DeclaringType.Name + "." + f.Name),
                Has.Member(nameof(AmmoCrate) + "._interactAction"));
        }

        [Test]
        public void InputActionFieldsAreNeverRestoredByTheEditor()
        {
            var restorable = InputActionFieldsOfUnityObjects()
                .Where(f => !f.IsInitOnly && !f.IsDefined(typeof(NonSerializedAttribute), false))
                .Select(f => f.DeclaringType.FullName + "." + f.Name)
                .ToList();

            Assert.That(restorable, Is.Empty, "mark these [NonSerialized] so a recompile cannot turn null into an empty InputAction");
        }

        [Test]
        public void TheAmmoCrateInteractActionIsNonSerialized()
        {
            var field = typeof(AmmoCrate).GetField("_interactAction", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null);
            Assert.That(field.IsDefined(typeof(NonSerializedAttribute), false), Is.True);
        }
    }
}
