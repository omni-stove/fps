using System;
using System.Collections.Generic;
using System.IO;

namespace Fps.Editor
{
    /// <summary>音の層の波形。ノイズに重ねる音程のある成分の形。</summary>
    public enum ToneWaveform
    {
        Sine,
        Saw,
    }

    /// <summary>
    /// 音の 1 層。startSeconds から durationSeconds の間だけ鳴り、attackSeconds で立ち上がってから decaySeconds を時定数として
    /// 指数的に減衰する。ノイズ (lowpassHz の 1 次ローパスを通す) と、toneStartHz から toneEndHz へ指数的に動く音程の成分を足す。
    /// </summary>
    public sealed class SoundLayer
    {
        public SoundLayer(
            float startSeconds,
            float durationSeconds,
            float attackSeconds,
            float decaySeconds,
            float noiseGain,
            float lowpassHz,
            float toneGain = 0f,
            float toneStartHz = 0f,
            float toneEndHz = 0f,
            ToneWaveform waveform = ToneWaveform.Sine)
        {
            RequireFinite(startSeconds, nameof(startSeconds), 0f);
            RequireFinite(attackSeconds, nameof(attackSeconds), 0f);
            RequireFinite(noiseGain, nameof(noiseGain), 0f);
            RequireFinite(toneGain, nameof(toneGain), 0f);
            RequirePositive(durationSeconds, nameof(durationSeconds));
            RequirePositive(decaySeconds, nameof(decaySeconds));

            if (noiseGain > 0f)
            {
                RequirePositive(lowpassHz, nameof(lowpassHz));
            }

            if (toneGain > 0f)
            {
                RequirePositive(toneStartHz, nameof(toneStartHz));
                RequirePositive(toneEndHz, nameof(toneEndHz));
            }

            if (noiseGain <= 0f && toneGain <= 0f)
            {
                throw new ArgumentException("a sound layer needs a noise or tone gain above 0");
            }

            StartSeconds = startSeconds;
            DurationSeconds = durationSeconds;
            AttackSeconds = attackSeconds;
            DecaySeconds = decaySeconds;
            NoiseGain = noiseGain;
            LowpassHz = lowpassHz;
            ToneGain = toneGain;
            ToneStartHz = toneStartHz;
            ToneEndHz = toneEndHz;
            Waveform = waveform;
        }

        public float StartSeconds { get; }

        public float DurationSeconds { get; }

        public float AttackSeconds { get; }

        public float DecaySeconds { get; }

        public float NoiseGain { get; }

        public float LowpassHz { get; }

        public float ToneGain { get; }

        public float ToneStartHz { get; }

        public float ToneEndHz { get; }

        public ToneWaveform Waveform { get; }

        private static void RequireFinite(float value, string name, float min)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < min)
            {
                throw new ArgumentOutOfRangeException(name, value, $"must be a finite number >= {min}");
            }
        }

        private static void RequirePositive(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(name, value, "must be a finite number > 0");
            }
        }
    }

    /// <summary>
    /// 手続きで作る音 1 つの設計図。name は書き出すファイルの名前 (拡張子なし)、seed はノイズの種。
    /// 同じ設計図からは常に同じサンプルができる。
    /// </summary>
    public sealed class SoundSpec
    {
        public SoundSpec(string name, uint seed, float lengthSeconds, float peak, IReadOnlyList<SoundLayer> layers)
        {
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException($"sound name '{name}' must be a non-empty file name", nameof(name));
            }

            if (float.IsNaN(lengthSeconds) || float.IsInfinity(lengthSeconds) || lengthSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(lengthSeconds), lengthSeconds, "must be a finite number > 0");
            }

            if (float.IsNaN(peak) || peak <= 0f || peak > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(peak), peak, "must be in (0, 1]");
            }

            if (layers == null || layers.Count == 0)
            {
                throw new ArgumentException($"sound '{name}' needs at least one layer", nameof(layers));
            }

            for (var i = 0; i < layers.Count; i++)
            {
                if (layers[i] == null)
                {
                    throw new ArgumentException($"sound '{name}' layer #{i} is missing", nameof(layers));
                }

                if (layers[i].StartSeconds >= lengthSeconds)
                {
                    throw new ArgumentException(
                        $"sound '{name}' layer #{i} starts at {layers[i].StartSeconds}s, after the sound ends ({lengthSeconds}s)", nameof(layers));
                }
            }

            Name = name;
            Seed = seed;
            LengthSeconds = lengthSeconds;
            Peak = peak;
            Layers = new List<SoundLayer>(layers).AsReadOnly();
        }

        public string Name { get; }

        public uint Seed { get; }

        public float LengthSeconds { get; }

        /// <summary>出来上がりの振幅の最大値。すべての層を足した後、最大値がこの値になるよう全体を拡大・縮小する。</summary>
        public float Peak { get; }

        public IReadOnlyList<SoundLayer> Layers { get; }
    }

    /// <summary>
    /// <see cref="SoundSpec"/> からモノラルのサンプルを作り、16 bit PCM の WAV に書き出す。
    /// 乱数は自前の xorshift にして、Unity や .NET の乱数の実装が変わっても同じ WAV になるようにしている。
    /// </summary>
    public static class ProceduralAudio
    {
        public const int DefaultSampleRate = 44100;

        /// <summary>spec の音のサンプル (-1〜1) を作る。長さは lengthSeconds × sampleRate (切り上げ)。</summary>
        public static float[] Synthesize(SoundSpec spec, int sampleRate = DefaultSampleRate)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "must be > 0");
            }

            var length = (int)Math.Ceiling(spec.LengthSeconds * (double)sampleRate);
            var mix = new double[length];
            for (var i = 0; i < spec.Layers.Count; i++)
            {
                // 層ごとに種をずらし、同じ設定の層を重ねても同じノイズが重ならないようにする。
                AddLayer(mix, spec.Layers[i], sampleRate, unchecked(spec.Seed * 2654435761u + (uint)i * 40503u + 1u));
            }

            var max = 0.0;
            for (var i = 0; i < length; i++)
            {
                max = Math.Max(max, Math.Abs(mix[i]));
            }

            var gain = max > 0.0 ? spec.Peak / max : 0.0;
            var samples = new float[length];
            for (var i = 0; i < length; i++)
            {
                samples[i] = (float)(mix[i] * gain);
            }

            // 端で途切れるとクリックが鳴るので、最後の 5ms を 0 へ絞る。
            var fade = Math.Min(length, sampleRate / 200);
            for (var i = 0; i < fade; i++)
            {
                samples[length - 1 - i] *= (float)i / fade;
            }

            return samples;
        }

        /// <summary>モノラル・16 bit PCM の WAV ファイルの中身を作る。範囲外のサンプルは -1〜1 に切り詰める。</summary>
        public static byte[] EncodeWav(IReadOnlyList<float> samples, int sampleRate = DefaultSampleRate)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "must be > 0");
            }

            const short channels = 1;
            const short bitsPerSample = 16;
            const short blockAlign = channels * bitsPerSample / 8;
            var dataSize = samples.Count * blockAlign;

            using (var stream = new MemoryStream(44 + dataSize))
            using (var writer = new BinaryWriter(stream))
            {
                // BinaryWriter は常にリトルエンディアンで書くので、WAV の並びと一致する。
                writer.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
                writer.Write(36 + dataSize);
                writer.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
                writer.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write(channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * blockAlign);
                writer.Write(blockAlign);
                writer.Write(bitsPerSample);
                writer.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
                writer.Write(dataSize);

                for (var i = 0; i < samples.Count; i++)
                {
                    var sample = samples[i];
                    if (float.IsNaN(sample))
                    {
                        throw new ArgumentException($"sample #{i} is NaN", nameof(samples));
                    }

                    var clamped = Math.Max(-1f, Math.Min(1f, sample));
                    writer.Write((short)Math.Round(clamped * short.MaxValue, MidpointRounding.AwayFromZero));
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>spec を合成して WAV の中身にする。</summary>
        public static byte[] Render(SoundSpec spec, int sampleRate = DefaultSampleRate)
        {
            return EncodeWav(Synthesize(spec, sampleRate), sampleRate);
        }

        private static void AddLayer(double[] mix, SoundLayer layer, int sampleRate, uint seed)
        {
            var start = (int)Math.Round(layer.StartSeconds * (double)sampleRate);
            var end = Math.Min(mix.Length, start + (int)Math.Ceiling(layer.DurationSeconds * (double)sampleRate));
            var attackSamples = layer.AttackSeconds * sampleRate;
            var lowpass = layer.NoiseGain > 0f ? 1.0 - Math.Exp(-2.0 * Math.PI * layer.LowpassHz / sampleRate) : 0.0;

            // ローパスは周波数を下げるほど振幅も落とすので、noiseGain が遮断周波数によらず同じくらいの音量になるよう戻す。
            var noiseCompensation = lowpass > 0.0 ? Math.Sqrt((2.0 - lowpass) / lowpass) : 0.0;
            var decayPerSample = Math.Exp(-1.0 / (layer.DecaySeconds * sampleRate));
            var duration = Math.Max(1, end - start);
            var frequencyRatio = layer.ToneGain > 0f ? (double)layer.ToneEndHz / layer.ToneStartHz : 1.0;

            var state = seed == 0 ? 0x9E3779B9u : seed;
            var filtered = 0.0;
            var decay = 1.0;
            var phase = 0.0;

            for (var i = start; i < end; i++)
            {
                var t = i - start;
                var attack = attackSamples > 0.0 && t < attackSamples ? t / attackSamples : 1.0;
                var envelope = attack * decay;
                decay *= decayPerSample;

                var value = 0.0;
                if (layer.NoiseGain > 0f)
                {
                    state ^= state << 13;
                    state ^= state >> 17;
                    state ^= state << 5;
                    var white = (state / (double)uint.MaxValue * 2.0) - 1.0;
                    filtered += lowpass * (white - filtered);
                    value += filtered * noiseCompensation * layer.NoiseGain;
                }

                if (layer.ToneGain > 0f)
                {
                    var frequency = layer.ToneStartHz * Math.Pow(frequencyRatio, (double)t / duration);
                    phase += frequency / sampleRate;
                    phase -= Math.Floor(phase);
                    var tone = layer.Waveform == ToneWaveform.Saw ? (2.0 * phase) - 1.0 : Math.Sin(2.0 * Math.PI * phase);
                    value += tone * layer.ToneGain;
                }

                mix[i] += value * envelope;
            }
        }
    }
}
