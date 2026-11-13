using System;
using System.Linq;
using Fps.Editor;
using NUnit.Framework;

namespace Fps.Tests.EditMode
{
    /// <summary><see cref="ProceduralAudio"/> の合成と WAV の書き出し、<see cref="PresentationAssetBuilder.SoundSpecs"/> の音の性質を確かめる。</summary>
    public sealed class ProceduralAudioTests
    {
        [Test]
        public void EverySoundRendersTheSameBytesTwice()
        {
            foreach (var spec in PresentationAssetBuilder.SoundSpecs())
            {
                Assert.That(ProceduralAudio.Render(spec), Is.EqualTo(ProceduralAudio.Render(spec)), spec.Name);
            }
        }

        [Test]
        public void SoundNamesAreUnique()
        {
            var names = PresentationAssetBuilder.SoundSpecs().Select(s => s.Name).ToList();

            Assert.That(names, Is.Unique);
            Assert.That(names, Has.Count.EqualTo(11));
        }

        [Test]
        public void DifferentSeedsGiveDifferentNoise()
        {
            var layers = new[] { new SoundLayer(0f, 0.1f, 0f, 0.05f, 1f, 5000f) };

            var a = ProceduralAudio.Render(new SoundSpec("A", 1u, 0.1f, 0.9f, layers));
            var b = ProceduralAudio.Render(new SoundSpec("A", 2u, 0.1f, 0.9f, layers));

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void SamplesLastTheSpecLengthAndPeakAtTheSpecPeak()
        {
            foreach (var spec in PresentationAssetBuilder.SoundSpecs())
            {
                var samples = ProceduralAudio.Synthesize(spec);
                var peak = samples.Max(s => Math.Abs(s));

                Assert.That(samples.Length, Is.EqualTo((int)Math.Ceiling(spec.LengthSeconds * (double)ProceduralAudio.DefaultSampleRate)), spec.Name);
                Assert.That(peak, Is.EqualTo(spec.Peak).Within(1e-4f), spec.Name);
                Assert.That(samples[samples.Length - 1], Is.EqualTo(0f), $"{spec.Name} fades out to avoid a click");
            }
        }

        [Test]
        public void GunshotsAreBrighterThanFootsteps()
        {
            var specs = PresentationAssetBuilder.SoundSpecs().ToDictionary(s => s.Name);

            var rifle = Brightness(ProceduralAudio.Synthesize(specs[PresentationAssetBuilder.RifleFireSound]), 0.05f);
            var handgun = Brightness(ProceduralAudio.Synthesize(specs[PresentationAssetBuilder.HandgunFireSound]), 0.05f);
            var footstep = Brightness(ProceduralAudio.Synthesize(specs[PresentationAssetBuilder.FootstepSound]), 0.05f);
            var landing = Brightness(ProceduralAudio.Synthesize(specs[PresentationAssetBuilder.LandingSound]), 0.05f);

            Assert.That(rifle, Is.GreaterThan(footstep * 2f));
            Assert.That(handgun, Is.GreaterThan(footstep * 2f));
            Assert.That(rifle, Is.GreaterThan(landing * 2f));
        }

        [Test]
        public void WavHeaderDescribesMono16BitPcm()
        {
            var bytes = ProceduralAudio.EncodeWav(new[] { 0f, 0.5f, -0.5f }, 22050);

            Assert.That(bytes.Length, Is.EqualTo(44 + 6));
            Assert.That(Ascii(bytes, 0), Is.EqualTo("RIFF"));
            Assert.That(BitConverter.ToInt32(bytes, 4), Is.EqualTo(36 + 6));
            Assert.That(Ascii(bytes, 8), Is.EqualTo("WAVE"));
            Assert.That(Ascii(bytes, 12), Is.EqualTo("fmt "));
            Assert.That(BitConverter.ToInt32(bytes, 16), Is.EqualTo(16));
            Assert.That(BitConverter.ToInt16(bytes, 20), Is.EqualTo(1), "PCM");
            Assert.That(BitConverter.ToInt16(bytes, 22), Is.EqualTo(1), "mono");
            Assert.That(BitConverter.ToInt32(bytes, 24), Is.EqualTo(22050));
            Assert.That(BitConverter.ToInt32(bytes, 28), Is.EqualTo(22050 * 2));
            Assert.That(BitConverter.ToInt16(bytes, 32), Is.EqualTo(2));
            Assert.That(BitConverter.ToInt16(bytes, 34), Is.EqualTo(16));
            Assert.That(Ascii(bytes, 36), Is.EqualTo("data"));
            Assert.That(BitConverter.ToInt32(bytes, 40), Is.EqualTo(6));
            Assert.That(BitConverter.ToInt16(bytes, 44), Is.EqualTo(0));
            Assert.That(BitConverter.ToInt16(bytes, 46), Is.EqualTo(16384));
            Assert.That(BitConverter.ToInt16(bytes, 48), Is.EqualTo(-16384));
        }

        [Test]
        public void EncodeWavClampsOutOfRangeSamplesAndRejectsNaN()
        {
            var bytes = ProceduralAudio.EncodeWav(new[] { 2f, -2f });

            Assert.That(BitConverter.ToInt16(bytes, 44), Is.EqualTo(short.MaxValue));
            Assert.That(BitConverter.ToInt16(bytes, 46), Is.EqualTo(-short.MaxValue));
            Assert.Throws<ArgumentException>(() => ProceduralAudio.EncodeWav(new[] { float.NaN }));
        }

        [Test]
        public void RejectsInvalidSpecs()
        {
            var layer = new SoundLayer(0f, 0.1f, 0f, 0.05f, 1f, 5000f);

            Assert.Throws<ArgumentException>(() => new SoundSpec("Empty", 1u, 0.1f, 0.9f, Array.Empty<SoundLayer>()));
            Assert.Throws<ArgumentException>(() => new SoundSpec("", 1u, 0.1f, 0.9f, new[] { layer }));
            Assert.Throws<ArgumentException>(() => new SoundSpec("Bad/Name", 1u, 0.1f, 0.9f, new[] { layer }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SoundSpec("Zero", 1u, 0f, 0.9f, new[] { layer }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SoundSpec("Loud", 1u, 0.1f, 1.5f, new[] { layer }));
            Assert.Throws<ArgumentException>(() => new SoundSpec("Late", 1u, 0.1f, 0.9f, new[] { new SoundLayer(0.2f, 0.1f, 0f, 0.05f, 1f, 5000f) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SoundLayer(0f, 0.1f, 0f, 0f, 1f, 5000f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SoundLayer(0f, 0.1f, 0f, 0.05f, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SoundLayer(0f, 0.1f, 0f, 0.05f, 0f, 0f, 1f, 0f, 100f));
            Assert.Throws<ArgumentException>(() => new SoundLayer(0f, 0.1f, 0f, 0.05f, 0f, 0f));
        }

        // 隣り合うサンプルの差の二乗和を二乗和で割る。高い音ほど大きく、低い正弦波なら 0 に近い。
        private static float Brightness(float[] samples, float seconds)
        {
            var count = Math.Min(samples.Length, (int)(seconds * ProceduralAudio.DefaultSampleRate));
            var energy = 0.0;
            var difference = 0.0;
            for (var i = 1; i < count; i++)
            {
                energy += samples[i] * (double)samples[i];
                var delta = samples[i] - (double)samples[i - 1];
                difference += delta * delta;
            }

            return (float)(difference / energy);
        }

        private static string Ascii(byte[] bytes, int offset)
        {
            return System.Text.Encoding.ASCII.GetString(bytes, offset, 4);
        }
    }
}
