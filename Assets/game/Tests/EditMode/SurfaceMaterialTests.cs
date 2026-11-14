using System.Collections.Generic;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary><see cref="SurfaceMaterial.IdOf"/> がコライダーから材質の Id を引き、無ければ既定の材質を返すことを確かめる。</summary>
    public sealed class SurfaceMaterialTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void NullColliderIsTheDefaultMaterial()
        {
            Assert.That(SurfaceMaterial.IdOf(null), Is.EqualTo(SurfaceMaterial.DefaultId));
        }

        [Test]
        public void ColliderWithoutTheComponentIsTheDefaultMaterial()
        {
            var wall = CreateCube("wall");

            Assert.That(SurfaceMaterial.IdOf(wall.GetComponent<Collider>()), Is.EqualTo(SurfaceMaterial.DefaultId));
        }

        [Test]
        public void ColliderWithTheComponentIsItsMaterial()
        {
            var wall = CreateCube("wall");
            wall.AddComponent<SurfaceMaterial>().Id = "concrete";

            Assert.That(SurfaceMaterial.IdOf(wall.GetComponent<Collider>()), Is.EqualTo("concrete"));
        }

        [Test]
        public void ParentMaterialAppliesToChildCollidersAndTheNearestOneWins()
        {
            var building = new GameObject("building");
            _created.Add(building);
            building.AddComponent<SurfaceMaterial>().Id = "concrete";
            var wall = CreateCube("wall");
            wall.transform.SetParent(building.transform, false);
            var door = CreateCube("door");
            door.transform.SetParent(building.transform, false);
            door.AddComponent<SurfaceMaterial>().Id = "wood";

            Assert.That(SurfaceMaterial.IdOf(wall.GetComponent<Collider>()), Is.EqualTo("concrete"));
            Assert.That(SurfaceMaterial.IdOf(door.GetComponent<Collider>()), Is.EqualTo("wood"));
        }

        [Test]
        public void DestroyedColliderIsTheDefaultMaterial()
        {
            var wall = CreateCube("wall");
            wall.AddComponent<SurfaceMaterial>().Id = "metal";
            var collider = wall.GetComponent<Collider>();
            Object.DestroyImmediate(wall);

            Assert.That(SurfaceMaterial.IdOf(collider), Is.EqualTo(SurfaceMaterial.DefaultId));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void BlankIdBecomesTheDefault(string id)
        {
            var wall = CreateCube("wall");
            var material = wall.AddComponent<SurfaceMaterial>();

            material.Id = id;

            Assert.That(material.Id, Is.EqualTo(SurfaceMaterial.DefaultId));
            Assert.That(SurfaceMaterial.Normalize(id), Is.EqualTo(SurfaceMaterial.DefaultId));
        }

        [Test]
        public void IdIsTrimmed()
        {
            Assert.That(SurfaceMaterial.Normalize("  glass "), Is.EqualTo("glass"));
        }

        [Test]
        public void NewComponentStartsAsTheDefaultMaterial()
        {
            var wall = CreateCube("wall");

            Assert.That(wall.AddComponent<SurfaceMaterial>().Id, Is.EqualTo(SurfaceMaterial.DefaultId));
        }

        private GameObject CreateCube(string name)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            _created.Add(cube);
            return cube;
        }
    }
}
