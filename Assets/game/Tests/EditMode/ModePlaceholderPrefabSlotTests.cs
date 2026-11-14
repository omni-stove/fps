using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// モードの物と壊れる物 (<see cref="Pickup"/>・<see cref="BombSite"/>・<see cref="ControlPointZone"/>・<see cref="ShrinkingZone"/>・
    /// <see cref="DestructiblePiece"/>) が、プレハブを渡されればその見た目で出し、空なら今までの仮の形で出すことを確かめる。
    /// プレハブの代わりに、当たり判定の付いた Cube を子に持つシーンの GameObject を使う (Instantiate は同じく複製する)。
    /// </summary>
    public sealed class ModePlaceholderPrefabSlotTests
    {
        private const string ModelName = "Model";
        private const float Tolerance = 1e-4f;

        // 材質に書いた色は、読み戻すと丸めで最下位の桁がずれることがあるので、ぴったりでは比べない。
        private const float ColorTolerance = 1e-3f;

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void ControlPointMarkerUsesThePrefabPaintedInTheOwnersColour()
        {
            var prefab = Prefab("Ring");
            var prefabMaterial = prefab.GetComponentInChildren<Renderer>().sharedMaterial;
            var prefabColor = prefabMaterial.color;
            var zone = Track(new GameObject("Point")).AddComponent<ControlPointZone>();
            zone.Radius = 4f;
            zone.MarkerPrefab = prefab;

            zone.RefreshMarker();

            var marker = zone.Marker;
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.name, Is.EqualTo(ControlPointZone.MarkerName));
            Assert.That(marker.transform.parent, Is.SameAs(zone.transform));
            Assert.That(marker.transform.Find(ModelName), Is.Not.Null, "the prefab's look is used");
            Assert.That(marker.GetComponentsInChildren<Collider>(true), Is.Empty, "the marker does not block shots or movement");
            Assert.That(marker.transform.localScale, Is.EqualTo(new Vector3(8f, 1f, 8f)), "spread to the diameter, height kept");

            var markerMaterial = TrackMaterials(marker);
            Assert.That(markerMaterial, Is.Not.SameAs(prefabMaterial));
            AssertColor(markerMaterial.color, ControlPointZone.MarkerColorOf(null, null));

            zone.ShowOwner(Team.Bravo);

            AssertColor(markerMaterial.color, ControlPointZone.MarkerColorOf(Team.Bravo, null));
            AssertColor(prefabMaterial.color, prefabColor, "the prefab's own material is not repainted");
        }

        [Test]
        public void ControlPointMarkerWithoutAPrefabIsThePlaceholderDisc()
        {
            var zone = Track(new GameObject("Point")).AddComponent<ControlPointZone>();
            zone.Radius = 4f;

            zone.RefreshMarker();

            var marker = zone.Marker;
            Assert.That(marker.name, Is.EqualTo(ControlPointZone.MarkerName));
            Assert.That(marker.GetComponent<MeshFilter>(), Is.Not.Null);
            Assert.That(marker.GetComponent<Collider>() == null, Is.True);
            Assert.That(marker.transform.localScale, Is.EqualTo(new Vector3(8f, ControlPointZone.MarkerThickness * 0.5f, 8f)));
            AssertColor(TrackMaterials(marker).color, ControlPointZone.MarkerColorOf(null, null));
        }

        [Test]
        public void BombSiteMarkerUsesThePrefabPaintedWhenPlanted()
        {
            var prefab = Prefab("Pad");
            var site = Track(new GameObject("Site")).AddComponent<BombSite>();
            site.Radius = 2f;
            site.MarkerPrefab = prefab;

            site.RefreshMarker();

            var marker = site.Marker;
            Assert.That(marker.name, Is.EqualTo(BombSite.MarkerName));
            Assert.That(marker.transform.Find(ModelName), Is.Not.Null);
            Assert.That(marker.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(marker.transform.localScale, Is.EqualTo(new Vector3(4f, 1f, 4f)));
            var markerMaterial = TrackMaterials(marker);
            AssertColor(markerMaterial.color, BombSite.IdleColor);

            site.ShowPlanted(true);

            AssertColor(markerMaterial.color, BombSite.PlantedColor);
        }

        [Test]
        public void BombSiteMarkerWithoutAPrefabIsThePlaceholderDisc()
        {
            var site = Track(new GameObject("Site")).AddComponent<BombSite>();
            site.Radius = 2f;
            site.ShowPlanted(true);

            site.RefreshMarker();

            var marker = site.Marker;
            Assert.That(marker.GetComponent<MeshFilter>(), Is.Not.Null);
            Assert.That(marker.GetComponent<Collider>() == null, Is.True);
            Assert.That(marker.transform.localScale, Is.EqualTo(new Vector3(4f, ControlPointZone.MarkerThickness * 0.5f, 4f)));
            AssertColor(TrackMaterials(marker).color, BombSite.PlantedColor);
        }

        [Test]
        public void ZoneBoundaryUsesThePrefabSizedToTheCircle()
        {
            var prefab = Prefab("Wall");
            var prefabMaterial = prefab.GetComponentInChildren<Renderer>().sharedMaterial;
            var zone = NewZone();
            zone.BoundaryPrefab = prefab;

            zone.Begin();

            var boundary = zone.Boundary;
            Assert.That(boundary, Is.Not.Null);
            Assert.That(boundary.name, Is.EqualTo(ShrinkingZone.BoundaryName));
            Assert.That(boundary.transform.Find(ModelName), Is.Not.Null);
            Assert.That(boundary.GetComponent<MeshFilter>(), Is.Null, "no placeholder tube");
            Assert.That(boundary.GetComponentsInChildren<Collider>(true), Is.Empty, "the boundary does not block shots or movement");
            Assert.That(boundary.GetComponentInChildren<Renderer>().sharedMaterial, Is.SameAs(prefabMaterial), "the prefab keeps its material");
            Assert.That(boundary.transform.localScale.x, Is.EqualTo(20f).Within(Tolerance));
            Assert.That(boundary.transform.localScale.y, Is.EqualTo(zone.BoundaryHeight).Within(Tolerance));

            zone.Tick(15f);

            Assert.That(boundary.transform.localScale.x, Is.EqualTo(15f).Within(Tolerance));
        }

        [Test]
        public void ZoneBoundaryWithoutAPrefabIsThePlaceholderTube()
        {
            var zone = NewZone();

            zone.Begin();

            var boundary = zone.Boundary;
            Assert.That(boundary.GetComponent<MeshFilter>().sharedMesh.name, Is.EqualTo(ShrinkingZone.BoundaryName));
            Assert.That(boundary.GetComponent<Collider>() == null, Is.True);
            Track(boundary.GetComponent<MeshFilter>().sharedMesh);
            Track(boundary.GetComponent<Renderer>().sharedMaterial);
        }

        [Test]
        public void DestructiblePieceShowsThePrefabAndKeepsItsBox()
        {
            var prefab = Prefab("Brick");
            var root = Track(new GameObject("Structure"));
            var size = new Vector3(2f, 1f, 0.5f);

            var piece = DestructiblePiece.Create(root.transform, "Piece", new Vector3(0f, 0.5f, 0f), size, 0, grounded: true, visualPrefab: prefab);

            Assert.That(piece.name, Is.EqualTo("Piece"));
            Assert.That(piece.GetComponent<MeshFilter>(), Is.Null, "no placeholder cube on the root");
            Assert.That(piece.transform.localScale, Is.EqualTo(size));
            Assert.That(piece.Collider, Is.SameAs(piece.GetComponent<BoxCollider>()));
            Assert.That(((BoxCollider)piece.Collider).size, Is.EqualTo(Vector3.one));
            var visual = piece.transform.Find(DestructiblePiece.VisualName);
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.Find(ModelName), Is.Not.Null);
            Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty, "hits and support use the piece's box only");
            Assert.That(piece.DebrisMaterial, Is.SameAs(prefab.GetComponentInChildren<Renderer>().sharedMaterial));

            piece.SetBroken(true);

            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                Assert.That(renderer.enabled, Is.False, "a broken piece hides the prefab's look");
            }

            piece.SetBroken(false);

            Assert.That(visual.GetComponentInChildren<Renderer>().enabled, Is.True);
        }

        [Test]
        public void DestructiblePieceWithoutAPrefabIsThePlaceholderCube()
        {
            var root = Track(new GameObject("Structure"));

            var piece = DestructiblePiece.Create(root.transform, "Piece", Vector3.zero, Vector3.one, 0);

            Assert.That(piece.GetComponent<MeshFilter>(), Is.Not.Null);
            Assert.That(piece.Collider, Is.SameAs(piece.GetComponent<BoxCollider>()));
            Assert.That(piece.transform.Find(DestructiblePiece.VisualName), Is.Null);
        }

        [Test]
        public void PickupShowsThePrefabFloatingAboveTheGround()
        {
            var prefab = Prefab("Medkit");

            var pickup = Track(Pickup.Create(PickupContents.ForHealth(25), Vector3.zero, visualPrefab: prefab).gameObject).GetComponent<Pickup>();

            Assert.That(pickup.VisualPrefab, Is.SameAs(prefab));
            var visual = pickup.transform.Find(Pickup.VisualName);
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.Find(ModelName), Is.Not.Null);
            Assert.That(visual.GetComponent<MeshFilter>(), Is.Null, "no placeholder cube");
            Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty, "the look does not block movement or shots");
            Assert.That(visual.localPosition, Is.EqualTo(new Vector3(0f, Pickup.VisualHeight, 0f)));
            Assert.That(visual.localScale, Is.EqualTo(Vector3.one), "the prefab keeps its size");
            Assert.That(pickup.GetComponent<SphereCollider>().isTrigger, Is.True, "the pickup range is still the trigger sphere");

            pickup.Tick(1f);

            Assert.That(visual.localEulerAngles.y, Is.EqualTo(Pickup.SpinDegreesPerSecond).Within(0.01f), "the look spins");
        }

        [Test]
        public void PickupWithoutAPrefabIsThePlaceholderCube()
        {
            var pickup = Track(Pickup.Create(PickupContents.ForHealth(25), Vector3.zero).gameObject).GetComponent<Pickup>();

            var visual = pickup.transform.Find(Pickup.VisualName);
            Assert.That(visual.GetComponent<MeshFilter>(), Is.Not.Null);
            Assert.That(visual.localScale, Is.EqualTo(Vector3.one * Pickup.VisualSize));
        }

        [Test]
        public void SpawnPointAndDropOnDeathPassTheirPrefab()
        {
            var prefab = Prefab("Crate");
            var point = Track(new GameObject("Spawn")).AddComponent<PickupSpawnPoint>();
            point.Contents = PickupContents.ForHealth(25);
            point.VisualPrefab = prefab;

            Assert.That(point.TrySpawn(), Is.True);

            Assert.That(point.Current.transform.Find(Pickup.VisualName).Find(ModelName), Is.Not.Null);

            var dropper = Track(new GameObject("Dropper")).AddComponent<DropOnDeath>();
            dropper.VisualPrefab = prefab;

            var dropped = dropper.Drop(new[] { PickupContents.ForHealth(10) }, Vector3.zero);

            Assert.That(dropped, Has.Count.EqualTo(1));
            Track(dropped[0].gameObject);
            Assert.That(dropped[0].transform.Find(Pickup.VisualName).Find(ModelName), Is.Not.Null);
        }

        private static void AssertColor(Color actual, Color expected, string message = null)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(ColorTolerance), message);
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(ColorTolerance), message);
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(ColorTolerance), message);
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(ColorTolerance), message);
        }

        private ShrinkingZone NewZone()
        {
            var zone = Track(new GameObject("Zone")).AddComponent<ShrinkingZone>();
            zone.ShowsBoundary = true;
            zone.BeginsOnStart = false;
            zone.UseRandomSeed = false;
            zone.Seed = 1;
            zone.InitialRadius = 20f;

            // 10 秒待ち、10 秒で半径 10 へ。
            zone.SetPhases(new[] { new ShrinkingZone.PhaseSettings(10f, 10f, 10f, 5f) });
            zone.SetTargets(new List<Damageable>());
            return zone;
        }

        /// <summary>印の Renderer の複製された材質を消すよう覚え、最初のものを返す (Edit Mode では OnDestroy が呼ばれないため)。</summary>
        private Material TrackMaterials(GameObject marker)
        {
            Material first = null;
            foreach (var renderer in marker.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    Track(material);
                    first = first != null ? first : material;
                }
            }

            Assert.That(first, Is.Not.Null);
            return first;
        }

        /// <summary>当たり判定と Renderer の付いた Cube (<see cref="ModelName"/>) を子に持つ、プレハブ代わりの GameObject。</summary>
        private GameObject Prefab(string name)
        {
            var root = Track(new GameObject(name));
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.name = ModelName;
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            model.transform.localScale = Vector3.one * 0.5f;
            return root;
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
