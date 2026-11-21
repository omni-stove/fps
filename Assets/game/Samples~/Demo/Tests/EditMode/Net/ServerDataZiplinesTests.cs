using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Editor;
using UnityEngine;
using Object = UnityEngine.Object;
using SceneZipline = Shooter.Runtime.Traversal.Zipline;

namespace Shooter.Net.Tests
{
    public sealed class ServerDataZiplinesTests
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
        public void TheExporterWritesZiplinesBesideTheOtherMapData()
        {
            Assert.That(MapCollisionExporter.SceneDataExtensions, Does.Contain(NetMapZiplines.Extension));
        }

        [Test]
        public void LinesAreWrittenInWorldSpaceWithTheirSpeedsInHierarchyOrder()
        {
            var root = new GameObject("Ziplines");
            _created.Add(root);
            root.transform.position = new Vector3(10f, 0f, 0f);
            var first = Line(root.transform, "First", new Vector3(0f, 6f, 0f), new Vector3(0f, 2f, 20f));
            first.SetSpeeds(5f, 12f, 3f, 0.25f);
            Line(root.transform, "Second", new Vector3(-4f, 3f, 0f), new Vector3(8f, 3f, 0f));

            var written = ServerDataZiplines.Collect(new[] { root });

            Assert.That(written.Ziplines, Has.Count.EqualTo(2));
            Assert.That(written.Ziplines[0].Start, Is.EqualTo(new System.Numerics.Vector3(10f, 6f, 0f)), "the ends are in world space");
            Assert.That(written.Ziplines[0].End, Is.EqualTo(new System.Numerics.Vector3(10f, 2f, 20f)));
            Assert.That(written.Ziplines[0].Spec.MinSpeed, Is.EqualTo(5f));
            Assert.That(written.Ziplines[0].Spec.MaxSpeed, Is.EqualTo(12f));
            Assert.That(written.Ziplines[0].Spec.Gravity, Is.EqualTo(3f));
            Assert.That(written.Ziplines[0].Spec.EndMargin, Is.EqualTo(0.25f));
            Assert.That(written.Ziplines[1].Start, Is.EqualTo(new System.Numerics.Vector3(6f, 3f, 0f)));
            Assert.That(written.Ziplines[1].Spec.MinSpeed, Is.EqualTo(ZiplineSpec.DefaultMinSpeed));

            var text = NetMapZiplines.Write(written);
            Assert.That(NetMapZiplines.Write(NetMapZiplines.Parse(text, "test")), Is.EqualTo(text), "the server reads back what was written");
        }

        [Test]
        public void DisabledAndInactiveLinesAreLeftOut()
        {
            var root = new GameObject("Ziplines");
            _created.Add(root);
            Line(root.transform, "Kept", new Vector3(0f, 6f, 0f), new Vector3(0f, 2f, 20f));
            Line(root.transform, "Disabled", new Vector3(0f, 6f, 0f), new Vector3(0f, 2f, 20f)).enabled = false;
            Line(root.transform, "Inactive", new Vector3(0f, 6f, 0f), new Vector3(0f, 2f, 20f)).gameObject.SetActive(false);

            var written = ServerDataZiplines.Collect(new[] { root });

            Assert.That(written.Ziplines, Has.Count.EqualTo(1));
        }

        [Test]
        public void ABrokenLineStopsTheExportAndNamesIt()
        {
            var root = new GameObject("Ziplines");
            _created.Add(root);
            var broken = Line(root.transform, "Short", new Vector3(0f, 6f, 0f), new Vector3(0f, 2f, 20f));

            // Inspector で両端を寄せたときと同じく、検査を通さずに縮める
            broken.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            var e = Assert.Throws<InvalidOperationException>(() => ServerDataZiplines.Collect(new[] { root }));
            Assert.That(e.Message, Does.Contain("Ziplines/Short"));
        }

        [Test]
        public void NoRootsIsAnError()
        {
            Assert.Throws<ArgumentNullException>(() => ServerDataZiplines.Collect(null));
        }

        private SceneZipline Line(Transform parent, string name, Vector3 start, Vector3 end)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<SceneZipline>();
            line.SetEnds(go.transform.TransformPoint(start), go.transform.TransformPoint(end));
            return line;
        }
    }
}
