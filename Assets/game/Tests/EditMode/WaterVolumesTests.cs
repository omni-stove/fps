using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime.Traversal;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using WaterComponent = Shooter.Runtime.Traversal.Water;

namespace Shooter.Tests.EditMode
{
    public sealed class WaterVolumesTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    var water = created.GetComponent<WaterComponent>();
                    if (water != null)
                    {
                        water.Unregister();
                    }

                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void InsideABoxTheSurfaceIsItsSurface()
        {
            var volumes = new WaterVolumes(new[] { Box(0f, 0f, 0f, 10f, 3f, 10f, 2.5f) });

            Assert.That(volumes.SurfaceAt(N(5f, 1f, 5f)), Is.EqualTo(2.5f));
            Assert.That(volumes.SurfaceAt(N(5f, 2.9f, 5f)), Is.EqualTo(2.5f), "above the surface but inside the box");
        }

        [Test]
        public void OutsideEveryBoxThereIsNoSurface()
        {
            var volumes = new WaterVolumes(new[] { Box(0f, 0f, 0f, 10f, 3f, 10f, 2.5f) });

            Assert.That(volumes.SurfaceAt(N(11f, 1f, 5f)), Is.Null, "beside the box");
            Assert.That(volumes.SurfaceAt(N(5f, 3.5f, 5f)), Is.Null, "above the box");
            Assert.That(volumes.SurfaceAt(N(5f, -0.5f, 5f)), Is.Null, "below the box");
            Assert.That(new WaterVolumes(Array.Empty<WaterBox>()).SurfaceAt(N(0f, 0f, 0f)), Is.Null);
        }

        [Test]
        public void TheFacesOfABoxCountAsInside()
        {
            var volumes = new WaterVolumes(new[] { Box(0f, 0f, 0f, 10f, 3f, 10f, 2.5f) });

            Assert.That(volumes.SurfaceAt(N(0f, 0f, 0f)), Is.EqualTo(2.5f));
            Assert.That(volumes.SurfaceAt(N(10f, 3f, 10f)), Is.EqualTo(2.5f));
            Assert.That(volumes.SurfaceAt(N(10.001f, 1f, 5f)), Is.Null);
        }

        [Test]
        public void WhereBoxesOverlapTheHigherSurfaceWins()
        {
            var deep = Box(0f, -5f, 0f, 10f, 3f, 10f, 2f);
            var shallow = Box(4f, 0f, 4f, 6f, 1f, 6f, 1f);

            Assert.That(new WaterVolumes(new[] { deep, shallow }).SurfaceAt(N(5f, 0.5f, 5f)), Is.EqualTo(2f));
            Assert.That(new WaterVolumes(new[] { shallow, deep }).SurfaceAt(N(5f, 0.5f, 5f)), Is.EqualTo(2f), "the order does not matter");
            Assert.That(new WaterVolumes(new[] { deep, shallow }).SurfaceAt(N(1f, 0.5f, 1f)), Is.EqualTo(2f), "only the deep water is here");
        }

        [Test]
        public void TheVolumesCopyTheBoxesTheyAreGiven()
        {
            var boxes = new List<WaterBox> { Box(0f, 0f, 0f, 1f, 1f, 1f, 1f) };
            var volumes = new WaterVolumes(boxes);
            boxes.Clear();

            Assert.That(volumes.Boxes.Count, Is.EqualTo(1));
            Assert.Throws<ArgumentNullException>(() => new WaterVolumes(null));
        }

        [Test]
        public void AWaterComponentIsABoxStandingOnItsPosition()
        {
            var water = CreateWater("Pool", new Vector3(10f, 1f, 0f), new Vector3(4f, 3f, 6f), 2f);

            var box = water.Box;

            Assert.That(box.Min, Is.EqualTo(N(8f, 1f, -3f)));
            Assert.That(box.Max, Is.EqualTo(N(12f, 4f, 3f)));
            Assert.That(box.SurfaceY, Is.EqualTo(3f));
        }

        [Test]
        public void TheSceneListsOnlyRegisteredWater()
        {
            var pool = CreateWater("Pool", new Vector3(100f, 0f, 100f), new Vector3(4f, 3f, 4f), 2f);
            var closed = CreateWater("Closed", new Vector3(200f, 0f, 200f), new Vector3(4f, 3f, 4f), 2f);
            closed.Unregister();

            var snapshot = WaterVolumes.FromScene();

            Assert.That(WaterComponent.Enabled, Has.Member(pool));
            Assert.That(WaterComponent.Enabled, Has.No.Member(closed));
            Assert.That(snapshot.SurfaceAt(N(100f, 1f, 100f)), Is.EqualTo(2f));
            Assert.That(snapshot.SurfaceAt(N(200f, 1f, 200f)), Is.Null);
            Assert.That(WaterVolumes.Scene.SurfaceAt(N(100f, 1f, 100f)), Is.EqualTo(2f));

            pool.Unregister();
            Assert.That(WaterVolumes.Scene.SurfaceAt(N(100f, 1f, 100f)), Is.Null, "the scene water follows what is open");
            Assert.That(snapshot.SurfaceAt(N(100f, 1f, 100f)), Is.EqualTo(2f), "a snapshot keeps the water it was made with");
        }

        [Test]
        public void InvalidWaterIsSkippedAndReportedOnce()
        {
            var water = CreateWater("Broken", new Vector3(300f, 0f, 300f), new Vector3(4f, 3f, 4f), 5f);
            LogAssert.Expect(LogType.Error, new Regex(@"^\[Water\] 'Broken' is ignored: "));

            Assert.That(WaterVolumes.Scene.SurfaceAt(N(300f, 1f, 300f)), Is.Null);
            Assert.That(WaterVolumes.Scene.SurfaceAt(N(300f, 1f, 300f)), Is.Null, "the second look does not log again");
            Assert.That(water.TryGetBox(out _), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        private WaterComponent CreateWater(string name, Vector3 position, Vector3 size, float surfaceHeight)
        {
            var go = new GameObject(name);
            _created.Add(go);
            go.transform.position = position;
            var water = go.AddComponent<WaterComponent>();
            water.Size = size;
            water.SurfaceHeight = surfaceHeight;
            water.Register();
            return water;
        }

        private static WaterBox Box(float minX, float minY, float minZ, float maxX, float maxY, float maxZ, float surfaceY)
        {
            return new WaterBox(N(minX, minY, minZ), N(maxX, maxY, maxZ), surfaceY);
        }

        private static System.Numerics.Vector3 N(float x, float y, float z)
        {
            return new System.Numerics.Vector3(x, y, z);
        }
    }
}
