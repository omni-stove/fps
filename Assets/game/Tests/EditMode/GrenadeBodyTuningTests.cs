using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace Shooter.Tests.EditMode
{
    public sealed class GrenadeBodyTuningTests
    {
        private const float Tolerance = 1e-5f;

        private static readonly GrenadeSpec _bouncy = new GrenadeSpec(
            bodyRadius: 0.2f, mass: 1.5f, bounciness: 0.9f, friction: 0.1f, angularDamping: 2f, releaseDistance: 2f);

        private readonly List<GameObject> _created = new List<GameObject>();

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
        public void TheThrownBallTakesItsSizeWeightAndBounceFromTheSpec()
        {
            var grenade = Spawn(_bouncy);

            Assert.That(grenade.BodyRadius, Is.EqualTo(0.2f));
            var sphere = grenade.GetComponent<SphereCollider>();
            Assert.That(sphere.radius * grenade.transform.lossyScale.x, Is.EqualTo(0.2f).Within(Tolerance));
            var body = grenade.GetComponent<Rigidbody>();
            Assert.That(body.mass, Is.EqualTo(1.5f));
            Assert.That(body.angularDamping, Is.EqualTo(2f));
            Assert.That(sphere.sharedMaterial.bounciness, Is.EqualTo(0.9f));
            Assert.That(sphere.sharedMaterial.dynamicFriction, Is.EqualTo(0.1f));
            Assert.That(sphere.sharedMaterial.staticFriction, Is.EqualTo(0.1f));
            Assert.That(sphere.sharedMaterial.bounceCombine, Is.EqualTo(PhysicsMaterialCombine.Maximum));
        }

        [Test]
        public void TheDefaultSpecKeepsTheBallItHadBefore()
        {
            var grenade = Spawn(GrenadeSpec.Default);

            Assert.That(grenade.BodyRadius, Is.EqualTo(0.08f));
            var body = grenade.GetComponent<Rigidbody>();
            Assert.That(body.mass, Is.EqualTo(0.4f));
            Assert.That(body.angularDamping, Is.EqualTo(0.05f));
            var material = grenade.GetComponent<SphereCollider>().sharedMaterial;
            Assert.That(material.bounciness, Is.EqualTo(0.35f));
            Assert.That(material.dynamicFriction, Is.EqualTo(0.6f));
        }

        [Test]
        public void TypesWithTheSameBounceShareOneMaterial()
        {
            var sameBounce = new GrenadeSpec(bounciness: 0.9f, friction: 0.1f, bodyRadius: 0.05f);

            Assert.That(ThrownGrenade.PhysicsMaterialFor(sameBounce), Is.SameAs(ThrownGrenade.PhysicsMaterialFor(_bouncy)));
            Assert.That(ThrownGrenade.PhysicsMaterialFor(GrenadeSpec.Default), Is.Not.SameAs(ThrownGrenade.PhysicsMaterialFor(_bouncy)));
        }

        [Test]
        public void AGrenadeThrownWithoutATypeUsesTheFirstConfiguredType()
        {
            var untyped = ThrownGrenade.UntypedSpec;
            Assert.That(untyped, Is.SameAs(KitTuning.Grenades.Types[0]));

            var grenade = ThrownGrenade.Spawn(null, new Vector3(0f, 50f, 0f), Vector3.zero, 1f, new ExplosionDamage(10, 2f), 0);
            _created.Add(grenade.gameObject);

            Assert.That(grenade.BodyRadius, Is.EqualTo(untyped.BodyRadius));
            Assert.That(grenade.RollDamping, Is.EqualTo(untyped.RollDamping));
            Assert.That(grenade.GetComponent<Rigidbody>().mass, Is.EqualTo(untyped.Mass));
            Assert.That(grenade.GetComponent<SphereCollider>().sharedMaterial, Is.SameAs(ThrownGrenade.SharedPhysicsMaterial()));
            Assert.That(ThrownGrenade.SharedPhysicsMaterial().bounciness, Is.EqualTo(untyped.Bounciness));
        }

        [Test]
        public void TheGrenadeLeavesTheHandAtTheSpecReleaseDistance()
        {
            var body = Track(new GameObject("Thrower"));
            body.transform.SetPositionAndRotation(new Vector3(0f, 1f, 0f), Quaternion.identity);

            GrenadeThrower.ResolveRelease(body.transform, _bouncy, out var position, out var direction);

            Assert.That(direction, Is.EqualTo(Vector3.forward).Using(Vector3EqualityComparer.Instance));
            Assert.That(position, Is.EqualTo(new Vector3(0f, 1f, 2f)).Using(Vector3EqualityComparer.Instance));
        }

        [Test]
        public void AWallInsideTheReleaseDistanceKeepsTheBallOfTheSpecOutOfIt()
        {
            var body = Track(new GameObject("Thrower"));
            body.transform.SetPositionAndRotation(new Vector3(0f, 1f, 0f), Quaternion.identity);
            var wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.position = new Vector3(0f, 1f, 1.5f);
            wall.transform.localScale = new Vector3(4f, 3f, 0.2f);
            Physics.SyncTransforms();

            GrenadeThrower.ResolveRelease(body.transform, _bouncy, out var position, out _);

            // 壁の手前の面は z=1.4。半径 0.2 の球が埋まらないよう 1.2 に出す
            Assert.That(position, Is.EqualTo(new Vector3(0f, 1f, 1.2f)).Using(Vector3EqualityComparer.Instance));
        }

        [Test]
        public void TheThrowerReleasesItsSelectedTypeAtThatTypesDistance()
        {
            var body = Track(new GameObject("Thrower"));
            body.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var thrower = body.AddComponent<GrenadeThrower>();
            thrower.ApplySpec(_bouncy);

            thrower.ResolveThrow(out var position, out _);

            Assert.That(position, Is.EqualTo(new Vector3(0f, 0f, 2f)).Using(Vector3EqualityComparer.Instance));
        }

        private ThrownGrenade Spawn(GrenadeSpec spec)
        {
            var grenade = ThrownGrenade.Spawn(null, new Vector3(0f, 50f, 0f), Vector3.zero, spec, null);
            _created.Add(grenade.gameObject);
            return grenade;
        }

        private GameObject Track(GameObject go)
        {
            _created.Add(go);
            return go;
        }
    }
}
