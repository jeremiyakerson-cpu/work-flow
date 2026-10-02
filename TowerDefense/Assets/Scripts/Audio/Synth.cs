using System;

namespace TowerDefense.Audio
{
    /// <summary>Oscillator shapes. Square/saw are naive (slightly aliased): a chiptune flavour that suits the style.</summary>
    public enum Waveform
    {
        Sine,
        Triangle,
        Square,
        Saw,
        /// <summary>25 % pulse: thinner, nasal square.</summary>
        Pulse,
    }

    /// <summary>
    /// Attack-decay-sustain-release envelope. The note is held for its duration
    /// minus <see cref="Release"/>, then fades out over <see cref="Release"/>.
    /// </summary>
    public struct Adsr
    {
        public float Attack, Decay, Sustain, Release;

        public Adsr(float attack, float decay, float sustain, float release)
        {
            Attack = attack; Decay = decay; Sustain = sustain; Release = release;
        }

        /// <summary>Percussive: instant attack, exponential-ish decay to silence.</summary>
        public static Adsr Pluck(float decay) => new Adsr(0.002f, decay, 0f, 0.01f);
        public static Adsr Pad(float attack, float release) => new Adsr(attack, 0.1f, 0.85f, release);

        /// <summary>Gain 0..1 at time <paramref name="t"/> into a note lasting <paramref name="duration"/>.</summary>
        public float Evaluate(float t, float duration)
        {
            if (t < 0f || t >= duration) return 0f;
            float level;
            if (Attack > 0f && t < Attack) level = t / Attack;
            else if (Decay > 0f && t < Attack + Decay)
            {
                float k = (t - Attack) / Decay;
                level = 1f + (Sustain - 1f) * k;
            }
            else level = Sustain;

            float releaseStart = duration - Release;
            if (Release > 0f && t > releaseStart)
            {
                // Release from whatever level we were at, not from sustain.
                level *= Math.Max(0f, (duration - t) / Release);
            }
            return level < 0f ? 0f : level;
        }
    }

    /// <summary>Deterministic white noise (xorshift32) so every launch sounds identical and tests are stable.</summary>
    public struct NoiseSource
    {
        private uint state;

        public NoiseSource(uint seed)
        {
            state = seed == 0 ? 0x9E3779B9u : seed;
        }

        /// <summary>Uniform in [-1, 1).</summary>
        public float Next()
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return (x >> 8) * (2f / 16777216f) - 1f;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float Next01() => (Next() + 1f) * 0.5f;
    }

    /// <summary>One-pole low-pass filter (6 dB/oct). Cheap and unconditionally stable.</summary>
    public struct OnePoleLowPass
    {
        private float z;

        public float Process(float input, float cutoffHz, int sampleRate) => Process(input, Coefficient(cutoffHz, sampleRate));

        /// <summary>Filter with a precomputed <see cref="Coefficient"/> (avoids an exp per sample).</summary>
        public float Process(float input, float coefficient)
        {
            z += coefficient * (input - z);
            return z;
        }

        public static float Coefficient(float cutoffHz, int sampleRate)
        {
            if (cutoffHz <= 0f) return 0f;
            float a = 1f - (float)Math.Exp(-2.0 * Math.PI * cutoffHz / sampleRate);
            return a < 0f ? 0f : (a > 1f ? 1f : a);
        }
    }

    /// <summary>One-pole high-pass (input minus its low-passed copy).</summary>
    public struct OnePoleHighPass
    {
        private OnePoleLowPass lp;

        public float Process(float input, float cutoffHz, int sampleRate) => input - lp.Process(input, cutoffHz, sampleRate);

        public float Process(float input, float coefficient) => input - lp.Process(input, coefficient);
    }

    /// <summary>
    /// Engine-free synthesis toolkit: renders tones, sweeps and noise bursts
    /// into float buffers. Buffers are mono, -1..1, ready for AudioClip.SetData.
    /// All renders are deterministic.
    /// </summary>
    public static class Synth
    {
        public const double TwoPi = Math.PI * 2.0;

        public static int Samples(float seconds, int sampleRate) => Math.Max(1, (int)Math.Round(seconds * sampleRate));

        public static float[] NewBuffer(float seconds, int sampleRate) => new float[Samples(seconds, sampleRate)];

        /// <summary>Oscillator value for a phase in cycles (0..1 wraps).</summary>
        public static float Oscillator(Waveform wave, double phase)
        {
            double p = phase - Math.Floor(phase);
            switch (wave)
            {
                case Waveform.Sine: return (float)Math.Sin(p * TwoPi);
                case Waveform.Triangle: return (float)(p < 0.5 ? 4.0 * p - 1.0 : 3.0 - 4.0 * p);
                case Waveform.Square: return p < 0.5 ? 1f : -1f;
                case Waveform.Saw: return (float)(2.0 * p - 1.0);
                case Waveform.Pulse: return p < 0.25 ? 1f : -1f;
                default: return 0f;
            }
        }

        /// <summary>Equal-tempered frequency of a MIDI note (69 = A4 = 440 Hz).</summary>
        public static float MidiToHz(float midiNote) => 440f * (float)Math.Pow(2.0, (midiNote - 69f) / 12.0);

        /// <summary>Parameters for <see cref="AddTone"/>.</summary>
        public struct Tone
        {
            public Waveform Wave;
            public float Start;          // seconds into the buffer
            public float Duration;       // seconds, including release
            public float FreqStart;      // Hz
            public float FreqEnd;        // Hz (exponential sweep); 0 = same as start
            public float Amplitude;
            public Adsr Envelope;
            public float VibratoHz;
            public float VibratoDepth;   // fraction of frequency, e.g. 0.01
            public float LowPassHz;      // 0 = off
            public bool Wrap;            // write past the end into the start (seamless loops)
        }

        /// <summary>Mix a tone into <paramref name="buffer"/>.</summary>
        public static void AddTone(float[] buffer, int sampleRate, Tone tone)
        {
            int start = (int)Math.Round(tone.Start * sampleRate);
            int length = Samples(tone.Duration, sampleRate);
            float f0 = tone.FreqStart;
            float f1 = tone.FreqEnd > 0f ? tone.FreqEnd : tone.FreqStart;
            double logRatio = f0 > 0f && f1 > 0f ? Math.Log(f1 / f0) : 0.0;
            double phase = 0.0;
            var lp = new OnePoleLowPass();
            float lpCoef = OnePoleLowPass.Coefficient(tone.LowPassHz, sampleRate);
            float invRate = 1f / sampleRate;

            for (int i = 0; i < length; i++)
            {
                int idx = start + i;
                if (idx >= buffer.Length)
                {
                    if (!tone.Wrap) break;
                    idx %= buffer.Length;
                }
                if (idx < 0) continue;

                float t = i * invRate;
                float k = length > 1 ? (float)i / (length - 1) : 0f;
                double freq = logRatio != 0.0 ? f0 * Math.Exp(logRatio * k) : f0;
                if (tone.VibratoDepth > 0f) freq *= 1.0 + tone.VibratoDepth * Math.Sin(TwoPi * tone.VibratoHz * t);
                phase += freq * invRate;

                float s = Oscillator(tone.Wave, phase);
                if (tone.LowPassHz > 0f) s = lp.Process(s, lpCoef);
                buffer[idx] += s * tone.Amplitude * tone.Envelope.Evaluate(t, tone.Duration);
            }
        }

        /// <summary>Convenience overload for a simple enveloped note.</summary>
        public static void AddTone(float[] buffer, int sampleRate, Waveform wave, float start, float duration,
                                   float freqStart, float freqEnd, float amplitude, Adsr envelope, float lowPassHz = 0f, bool wrap = false)
        {
            AddTone(buffer, sampleRate, new Tone
            {
                Wave = wave, Start = start, Duration = duration, FreqStart = freqStart, FreqEnd = freqEnd,
                Amplitude = amplitude, Envelope = envelope, LowPassHz = lowPassHz, Wrap = wrap,
            });
        }

        /// <summary>Parameters for <see cref="AddNoise"/>.</summary>
        public struct NoiseBurst
        {
            public float Start;
            public float Duration;
            public float Amplitude;
            public Adsr Envelope;
            public float LowPassStartHz;  // 0 = unfiltered
            public float LowPassEndHz;    // filter sweep target; 0 = same as start
            public float HighPassHz;      // 0 = off
            public uint Seed;
            public bool Wrap;
        }

        /// <summary>Mix filtered, enveloped white noise into <paramref name="buffer"/>.</summary>
        public static void AddNoise(float[] buffer, int sampleRate, NoiseBurst burst)
        {
            int start = (int)Math.Round(burst.Start * sampleRate);
            int length = Samples(burst.Duration, sampleRate);
            var noise = new NoiseSource(burst.Seed);
            var lp = new OnePoleLowPass();
            var lp2 = new OnePoleLowPass();
            var hp = new OnePoleHighPass();
            float c0 = burst.LowPassStartHz;
            float c1 = burst.LowPassEndHz > 0f ? burst.LowPassEndHz : c0;
            bool sweep = c1 != c0;
            float lpCoef = OnePoleLowPass.Coefficient(c0, sampleRate);
            float hpCoef = OnePoleLowPass.Coefficient(burst.HighPassHz, sampleRate);
            float invRate = 1f / sampleRate;

            for (int i = 0; i < length; i++)
            {
                int idx = start + i;
                if (idx >= buffer.Length)
                {
                    if (!burst.Wrap) break;
                    idx %= buffer.Length;
                }
                if (idx < 0) continue;

                float t = i * invRate;
                float s = noise.Next();
                if (c0 > 0f)
                {
                    if (sweep)
                    {
                        float k = length > 1 ? (float)i / (length - 1) : 0f;
                        lpCoef = OnePoleLowPass.Coefficient(c0 + (c1 - c0) * k, sampleRate);
                    }
                    s = lp2.Process(lp.Process(s, lpCoef), lpCoef); // 12 dB/oct
                }
                if (burst.HighPassHz > 0f) s = hp.Process(s, hpCoef);
                buffer[idx] += s * burst.Amplitude * burst.Envelope.Evaluate(t, burst.Duration);
            }
        }

        /// <summary>Scale so the loudest sample is <paramref name="peak"/>. Silent buffers are left alone.</summary>
        public static void Normalize(float[] buffer, float peak)
        {
            float max = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                float a = Math.Abs(buffer[i]);
                if (a > max) max = a;
            }
            if (max < 1e-6f) return;
            float g = peak / max;
            for (int i = 0; i < buffer.Length; i++) buffer[i] *= g;
        }

        /// <summary>
        /// Gentle tanh saturation that tames layered peaks. Scaled so +-1 in maps
        /// to +-1 out; anything louder is limited to -1..1.
        /// </summary>
        public static void SoftClip(float[] buffer, float drive)
        {
            float norm = (float)Math.Tanh(drive);
            for (int i = 0; i < buffer.Length; i++)
            {
                float s = (float)Math.Tanh(buffer[i] * drive) / norm;
                buffer[i] = s > 1f ? 1f : (s < -1f ? -1f : s);
            }
        }

        /// <summary>Short linear fades so a one-shot never starts or stops with a click.</summary>
        public static void FadeEdges(float[] buffer, int sampleRate, float fadeInSeconds, float fadeOutSeconds)
        {
            int fin = Math.Min(buffer.Length, (int)(fadeInSeconds * sampleRate));
            for (int i = 0; i < fin; i++) buffer[i] *= (float)i / fin;
            int fout = Math.Min(buffer.Length, (int)(fadeOutSeconds * sampleRate));
            for (int i = 0; i < fout; i++) buffer[buffer.Length - 1 - i] *= (float)i / fout;
        }

        /// <summary>Replace NaN/Infinity with silence and hard-limit to -1..1 (final safety net).</summary>
        public static void Sanitize(float[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                float s = buffer[i];
                if (float.IsNaN(s) || float.IsInfinity(s)) s = 0f;
                else if (s > 1f) s = 1f;
                else if (s < -1f) s = -1f;
                buffer[i] = s;
            }
        }
    }
}
