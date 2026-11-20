using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class PickupSerializedFieldsTests
    {
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void TheSerializedFieldsKeepTheirNamesSoSceneValuesStayBound()
        {
            var serialized = typeof(Pickup).GetFields(InstanceFields)
                .Where(f => f.IsDefined(typeof(SerializeField), false) || (f.IsPublic && !f.IsDefined(typeof(NonSerializedAttribute), false)))
                .ToDictionary(f => f.Name, f => f.FieldType);

            Assert.That(serialized, Is.EquivalentTo(new[]
            {
                Pair("_contents", typeof(PickupContents)),
                Pair("_collectMode", typeof(PickupCollectMode)),
                Pair("_radius", typeof(float)),
                Pair("_lookAngleDegrees", typeof(float)),
                Pair("_destroyOnCollect", typeof(bool)),
                Pair("_lifetimeSeconds", typeof(float)),
                Pair("_visualPrefab", typeof(GameObject)),
            }));
        }

        [Test]
        public void TheInteractActionStaysNonSerialized()
        {
            var field = typeof(Pickup).GetField("_interactAction", InstanceFields);

            Assert.That(field, Is.Not.Null);
            Assert.That(field.IsDefined(typeof(NonSerializedAttribute), false), Is.True);
        }

        [Test]
        public void TheSerializedFieldsAreDeclaredInPickupCs()
        {
            var source = System.IO.File.ReadAllText("Assets/game/Scripts/Runtime/Pickup/Pickup.cs");
            var partials = System.IO.Directory.GetFiles("Assets/game/Scripts/Runtime/Pickup", "Pickup.*.cs");

            Assert.That(partials, Is.Not.Empty);
            Assert.That(source.Split(new[] { "[SerializeField" }, StringSplitOptions.None).Length - 1, Is.EqualTo(7));
            foreach (var path in partials)
            {
                StringAssert.DoesNotContain("[SerializeField", System.IO.File.ReadAllText(path), path);
            }
        }

        private static System.Collections.Generic.KeyValuePair<string, Type> Pair(string name, Type type)
        {
            return new System.Collections.Generic.KeyValuePair<string, Type>(name, type);
        }
    }
}
