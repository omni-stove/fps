using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class AreaEffectZone
    {
        private const int _fireParticles = 120;
        private const float _fireParticleSeconds = 0.8f;
        private const float _fireRiseSpeed = 1.5f;
        private const float _fireParticleSize = 0.6f;
        private static readonly Color _fireColor = new Color(1f, 0.45f, 0.05f, 0.9f);
        private static Material _fireMaterial;

        partial void StepFire(float deltaSeconds)
        {
            var burn = FireArea.Affliction(Spec);
            if (burn == null || !FireArea.Pulses(Spec, Spec.DurationSeconds - _remaining, deltaSeconds))
            {
                return;
            }

            foreach (var target in Explosion.FindTargets(Center, Radius))
            {
                if (!target.IsDead)
                {
                    target.EnsureStatusEffects().Afflict(burn, Owner, WeaponId);
                }
            }
        }

        partial void BuildFireVisual(ref bool built)
        {
            if (!FireArea.IsFire(Spec ?? FireSpecOf(KindId)))
            {
                return;
            }

            var visual = new GameObject(VisualName);
            visual.transform.SetParent(transform, false);
            var particles = visual.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = _fireParticleSeconds;
            main.startLifetime = _fireParticleSeconds;
            main.startSpeed = _fireRiseSpeed;
            main.startSize = _fireParticleSize;
            main.startColor = _fireColor;
            main.maxParticles = _fireParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = particles.emission;
            emission.rateOverTime = _fireParticles / _fireParticleSeconds;

            // 炎は地面を這って広がるので、球ではなく範囲の円から真上へ出す
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Radius;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var renderer = visual.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = FireMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particles.Play();
            built = true;
        }

        // ネット対戦のクライアントには効き目の値が届かないので、届いた見た目の Id を持つ種類を手元のグレネードの値から探す
        private static AreaEffectSpec FireSpecOf(string visualId)
        {
            if (string.IsNullOrEmpty(visualId))
            {
                return null;
            }

            foreach (var grenade in KitTuning.Grenades.Types)
            {
                var area = grenade.Impact.AreaEffect;
                if (area != null && string.Equals(area.VisualId, visualId, StringComparison.Ordinal))
                {
                    return area;
                }
            }

            return null;
        }

        // プリミティブを作るのは、その描画パイプラインの既定の材質を元に色だけ変えるため
        private static Material FireMaterial()
        {
            if (_fireMaterial == null)
            {
                var probe = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _fireMaterial = new Material(probe.GetComponent<Renderer>().sharedMaterial) { name = ObjectName + "_" + StatusEffectKinds.Burn, color = _fireColor };
                _fireMaterial.EnableKeyword("_EMISSION");
                _fireMaterial.SetColor("_EmissionColor", _fireColor * 2f);
                DestroyImmediate(probe);
            }

            return _fireMaterial;
        }
    }
}
