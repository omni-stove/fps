using System.Globalization;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class AreaEffectZone : IVisionBlocker
    {
        public const string SmokeLogTag = "[Smoke]";

        private const int _smokeParticles = 160;
        private const float _smokeParticleSeconds = 4f;
        private const float _smokeDriftSpeed = 0.3f;
        private static readonly Color _smokeColor = new Color(0.78f, 0.78f, 0.8f, 0.9f);

        private bool _blockingSight;

        public bool BlocksSight => _blockingSight;

        public bool Blocks(Vector3 eye, Vector3 target)
        {
            return _blockingSight && SmokeScreen.Blocks(Center.ToNumerics(), Radius, eye.ToNumerics(), target.ToNumerics());
        }

        partial void StartSmoke()
        {
            if (_blockingSight || !SmokeScreen.IsSmoke(Spec))
            {
                return;
            }

            _blockingSight = EnemySenses.RegisterVisionBlocker(this);
            Debug.Log(string.Format(CultureInfo.InvariantCulture, "{0} blocks sight kind={1} radius={2:0.0}", SmokeLogTag, KindId, Radius));
        }

        partial void StopSmoke()
        {
            if (!_blockingSight)
            {
                return;
            }

            _blockingSight = false;
            EnemySenses.UnregisterVisionBlocker(this);
            Debug.Log($"{SmokeLogTag} cleared kind={KindId}");
        }

        partial void BuildSmokeVisual(ref bool built)
        {
            if (!SmokeScreen.IsSmoke(Spec ?? KnownSpec(KindId)))
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
            main.duration = _smokeParticleSeconds;
            main.startLifetime = _smokeParticleSeconds;
            main.startSpeed = _smokeDriftSpeed;
            main.startSize = Radius;
            main.startColor = _smokeColor;
            main.maxParticles = _smokeParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = particles.emission;
            emission.rateOverTime = _smokeParticles / _smokeParticleSeconds;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Radius * 0.7f;

            visual.GetComponent<ParticleSystemRenderer>().sharedMaterial = DefaultMaterial();
            particles.Play();
            built = true;
        }

        // ネット対戦のクライアントには効き目の値が届かないので、届いた見た目の Id を持つ種類を手元のグレネードの値から探す
        private static AreaEffectSpec KnownSpec(string visualId)
        {
            if (string.IsNullOrEmpty(visualId))
            {
                return null;
            }

            foreach (var grenade in KitTuning.Grenades.Types)
            {
                var area = grenade.Impact.AreaEffect;
                if (area != null && string.Equals(area.VisualId, visualId, System.StringComparison.Ordinal))
                {
                    return area;
                }
            }

            return null;
        }

        // プリミティブを作るのは、その描画パイプラインの既定の材質を得るため
        private static Material DefaultMaterial()
        {
            var probe = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var material = probe.GetComponent<Renderer>().sharedMaterial;
            DestroyImmediate(probe);
            return material;
        }
    }
}
