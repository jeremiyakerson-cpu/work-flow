using System;

namespace TowerDefense.Audio
{
    /// <summary>
    /// Recipes for every <see cref="SoundId"/>: layered oscillators, pitch
    /// sweeps and filtered noise. <see cref="Render"/> returns a mono buffer
    /// normalized to <see cref="Peak"/> with click-free edges; per-sound
    /// loudness is applied at playback from <see cref="SoundCatalog"/>.
    /// Swapping in recorded audio later only needs a different clip source.
    /// </summary>
    public static class SfxRecipes
    {
        public const float Peak = 0.9f;

        /// <summary>Nominal length of each sound in seconds (render length is exactly this).</summary>
        public static float Duration(SoundId id)
        {
            switch (id)
            {
                case SoundId.ArrowShot: return 0.20f;
                case SoundId.MagicZap: return 0.32f;
                case SoundId.CannonShot: return 0.70f;
                case SoundId.Frost: return 0.55f;
                case SoundId.Poison: return 0.45f;
                case SoundId.EnemyHit: return 0.09f;
                case SoundId.EnemyDeath: return 0.32f;
                case SoundId.BossDeath: return 1.60f;
                case SoundId.Coin: return 0.30f;
                case SoundId.LifeLost: return 0.55f;
                case SoundId.WaveStart: return 1.30f;
                case SoundId.BossWarning: return 1.80f;
                case SoundId.Victory: return 2.20f;
                case SoundId.Defeat: return 2.20f;
                case SoundId.UiClick: return 0.05f;
                case SoundId.Build: return 0.42f;
                case SoundId.Upgrade: return 0.60f;
                case SoundId.Sell: return 0.40f;
                default: throw new ArgumentOutOfRangeException(nameof(id), id, null);
            }
        }

        /// <summary>Render one sound effect. Deterministic for a given sample rate.</summary>
        public static float[] Render(SoundId id, int sampleRate)
        {
            if (sampleRate < 8000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            float[] b = Synth.NewBuffer(Duration(id), sampleRate);
            int sr = sampleRate;
            switch (id)
            {
                case SoundId.ArrowShot: Arrow(b, sr); break;
                case SoundId.MagicZap: Zap(b, sr); break;
                case SoundId.CannonShot: Cannon(b, sr); break;
                case SoundId.Frost: Frost(b, sr); break;
                case SoundId.Poison: Poison(b, sr); break;
                case SoundId.EnemyHit: Hit(b, sr); break;
                case SoundId.EnemyDeath: EnemyDeath(b, sr); break;
                case SoundId.BossDeath: BossDeath(b, sr); break;
                case SoundId.Coin: Coin(b, sr); break;
                case SoundId.LifeLost: LifeLost(b, sr); break;
                case SoundId.WaveStart: Horn(b, sr); break;
                case SoundId.BossWarning: BossWarning(b, sr); break;
                case SoundId.Victory: Victory(b, sr); break;
                case SoundId.Defeat: Defeat(b, sr); break;
                case SoundId.UiClick: Click(b, sr); break;
                case SoundId.Build: Build(b, sr); break;
                case SoundId.Upgrade: Upgrade(b, sr); break;
                case SoundId.Sell: Sell(b, sr); break;
            }
            Synth.Normalize(b, Peak);
            Synth.FadeEdges(b, sr, 0.002f, 0.012f);
            Synth.Sanitize(b);
            return b;
        }

        private static float N(float midi) => Synth.MidiToHz(midi);

        private static Synth.NoiseBurst Noise(float start, float dur, float amp, Adsr env, float lpStart, float lpEnd = 0f, float hp = 0f, uint seed = 1)
        {
            return new Synth.NoiseBurst
            {
                Start = start, Duration = dur, Amplitude = amp, Envelope = env,
                LowPassStartHz = lpStart, LowPassEndHz = lpEnd, HighPassHz = hp, Seed = seed,
            };
        }

        // ------------------------------------------------------------------ towers

        private static void Arrow(float[] b, int sr)
        {
            // Bow-string twang + air whoosh.
            Synth.AddTone(b, sr, Waveform.Triangle, 0f, 0.12f, 820f, 380f, 0.7f, Adsr.Pluck(0.11f));
            Synth.AddTone(b, sr, Waveform.Square, 0f, 0.04f, 1640f, 900f, 0.12f, Adsr.Pluck(0.04f), 4000f);
            Synth.AddNoise(b, sr, Noise(0.01f, 0.18f, 0.5f, new Adsr(0.03f, 0.15f, 0f, 0.01f), 6000f, 1500f, 800f, 11));
        }

        private static void Zap(float[] b, int sr)
        {
            Synth.AddTone(b, sr, new Synth.Tone
            {
                Wave = Waveform.Square, Start = 0f, Duration = 0.3f, FreqStart = 700f, FreqEnd = 2200f,
                Amplitude = 0.35f, Envelope = new Adsr(0.005f, 0.25f, 0f, 0.04f), VibratoHz = 38f, VibratoDepth = 0.08f, LowPassHz = 5000f,
            });
            Synth.AddTone(b, sr, Waveform.Sine, 0.02f, 0.28f, 2600f, 3400f, 0.3f, new Adsr(0.01f, 0.25f, 0f, 0.02f));
            Synth.AddNoise(b, sr, Noise(0f, 0.12f, 0.15f, Adsr.Pluck(0.11f), 0f, 0f, 3000f, 23));
        }

        private static void Cannon(float[] b, int sr)
        {
            // Thump + explosion body + crackle.
            Synth.AddTone(b, sr, Waveform.Sine, 0f, 0.45f, 130f, 38f, 1f, new Adsr(0.002f, 0.4f, 0f, 0.05f));
            Synth.AddNoise(b, sr, Noise(0f, 0.68f, 1f, new Adsr(0.003f, 0.6f, 0f, 0.07f), 2400f, 180f, 0f, 37));
            Synth.AddNoise(b, sr, Noise(0.03f, 0.25f, 0.25f, Adsr.Pluck(0.24f), 0f, 0f, 2500f, 41));
            Synth.SoftClip(b, 1.6f);
        }

        private static void Frost(float[] b, int sr)
        {
            // Icy chimes + crystalline hiss.
            float[] notes = { 96f, 100f, 103f, 108f };
            for (int i = 0; i < notes.Length; i++)
                Synth.AddTone(b, sr, Waveform.Sine, i * 0.045f, 0.4f, N(notes[i]), 0f, 0.35f, new Adsr(0.002f, 0.38f, 0f, 0.02f));
            Synth.AddTone(b, sr, Waveform.Triangle, 0f, 0.5f, 1800f, 1200f, 0.2f, new Adsr(0.01f, 0.45f, 0f, 0.04f));
            Synth.AddNoise(b, sr, Noise(0f, 0.5f, 0.3f, new Adsr(0.02f, 0.45f, 0f, 0.03f), 0f, 0f, 5000f, 53));
        }

        private static void Poison(float[] b, int sr)
        {
            // Bubbles: short rising sine blips at pseudo-random pitches.
            var rng = new NoiseSource(71);
            for (int i = 0; i < 6; i++)
            {
                float start = i * 0.06f + rng.Next01() * 0.02f;
                float f = 260f + rng.Next01() * 380f;
                Synth.AddTone(b, sr, Waveform.Sine, start, 0.08f, f, f * 1.9f, 0.6f, new Adsr(0.005f, 0.07f, 0f, 0.005f));
            }
            Synth.AddNoise(b, sr, Noise(0f, 0.42f, 0.2f, new Adsr(0.02f, 0.38f, 0f, 0.02f), 900f, 400f, 0f, 73));
        }

        // ------------------------------------------------------------------ enemies

        private static void Hit(float[] b, int sr)
        {
            Synth.AddNoise(b, sr, Noise(0f, 0.08f, 0.8f, Adsr.Pluck(0.075f), 3500f, 900f, 0f, 83));
            Synth.AddTone(b, sr, Waveform.Sine, 0f, 0.07f, 240f, 150f, 0.6f, Adsr.Pluck(0.065f));
        }

        private static void EnemyDeath(float[] b, int sr)
        {
            Synth.AddTone(b, sr, Waveform.Square, 0f, 0.28f, 520f, 90f, 0.4f, new Adsr(0.003f, 0.26f, 0f, 0.02f), 2500f);
            Synth.AddNoise(b, sr, Noise(0f, 0.2f, 0.45f, Adsr.Pluck(0.19f), 2000f, 400f, 0f, 97));
        }

        private static void BossDeath(float[] b, int sr)
        {
            Synth.AddTone(b, sr, Waveform.Sine, 0f, 1.2f, 90f, 28f, 1f, new Adsr(0.005f, 1.1f, 0f, 0.1f));
            Synth.AddTone(b, sr, Waveform.Saw, 0.05f, 1.0f, 320f, 55f, 0.35f, new Adsr(0.01f, 0.9f, 0f, 0.1f), 1200f);
            Synth.AddNoise(b, sr, Noise(0f, 1.55f, 1f, new Adsr(0.005f, 1.4f, 0f, 0.15f), 3000f, 120f, 0f, 101));
            // Secondary blasts.
            Synth.AddNoise(b, sr, Noise(0.35f, 0.5f, 0.6f, Adsr.Pluck(0.48f), 1800f, 200f, 0f, 103));
            Synth.AddNoise(b, sr, Noise(0.7f, 0.5f, 0.4f, Adsr.Pluck(0.48f), 1400f, 150f, 0f, 107));
            Synth.SoftClip(b, 1.4f);
        }

        // ------------------------------------------------------------------ economy / base

        private static void Coin(float[] b, int sr)
        {
            Synth.AddTone(b, sr, Waveform.Square, 0f, 0.07f, N(83), 0f, 0.35f, new Adsr(0.002f, 0.02f, 0.8f, 0.01f), 6000f);  // B5
            Synth.AddTone(b, sr, Waveform.Square, 0.07f, 0.22f, N(88), 0f, 0.35f, new Adsr(0.002f, 0.05f, 0.6f, 0.15f), 6000f); // E6
        }

        private static void LifeLost(float[] b, int sr)
        {
            Synth.AddTone(b, sr, Waveform.Saw, 0f, 0.22f, N(62), N(60), 0.5f, new Adsr(0.005f, 0.05f, 0.7f, 0.05f), 1800f);
            Synth.AddTone(b, sr, Waveform.Saw, 0.2f, 0.33f, N(57), N(52), 0.5f, new Adsr(0.005f, 0.05f, 0.7f, 0.15f), 1500f);
            Synth.AddNoise(b, sr, Noise(0f, 0.15f, 0.3f, Adsr.Pluck(0.14f), 1200f, 0f, 0f, 113));
        }

        // ------------------------------------------------------------------ stingers

        private static void Brass(float[] b, int sr, float start, float dur, float midi, float amp)
        {
            var env = new Adsr(0.06f, 0.12f, 0.75f, 0.18f);
            Synth.AddTone(b, sr, new Synth.Tone
            {
                Wave = Waveform.Saw, Start = start, Duration = dur, FreqStart = N(midi), Amplitude = amp,
                Envelope = env, VibratoHz = 5.5f, VibratoDepth = 0.006f, LowPassHz = 1400f,
            });
            Synth.AddTone(b, sr, new Synth.Tone
            {
                Wave = Waveform.Square, Start = start, Duration = dur, FreqStart = N(midi) * 1.004f, Amplitude = amp * 0.4f,
                Envelope = env, VibratoHz = 5.1f, VibratoDepth = 0.006f, LowPassHz = 1000f,
            });
        }

        private static void Horn(float[] b, int sr)
        {
            // Two-note war horn: G3 -> C4, a fifth below doubling.
            Brass(b, sr, 0f, 0.38f, 55f, 0.5f);
            Brass(b, sr, 0f, 0.38f, 48f, 0.3f);
            Brass(b, sr, 0.36f, 0.92f, 60f, 0.55f);
            Brass(b, sr, 0.36f, 0.92f, 53f, 0.3f);
        }

        private static void BossWarning(float[] b, int sr)
        {
            // Three ominous low pulses with drum hits, then a rising siren.
            for (int i = 0; i < 3; i++)
            {
                float t = i * 0.42f;
                Synth.AddTone(b, sr, new Synth.Tone
                {
                    Wave = Waveform.Saw, Start = t, Duration = 0.38f, FreqStart = N(40), FreqEnd = N(39),
                    Amplitude = 0.6f, Envelope = new Adsr(0.01f, 0.1f, 0.7f, 0.12f), VibratoHz = 9f, VibratoDepth = 0.02f, LowPassHz = 700f,
                });
                Synth.AddTone(b, sr, Waveform.Sine, t, 0.3f, 110f, 45f, 0.9f, Adsr.Pluck(0.28f));
                Synth.AddNoise(b, sr, Noise(t, 0.2f, 0.4f, Adsr.Pluck(0.18f), 900f, 200f, 0f, 127u + (uint)i));
            }
            Synth.AddTone(b, sr, new Synth.Tone
            {
                Wave = Waveform.Square, Start = 1.2f, Duration = 0.6f, FreqStart = N(52), FreqEnd = N(64),
                Amplitude = 0.35f, Envelope = new Adsr(0.05f, 0.1f, 0.8f, 0.2f), VibratoHz = 7f, VibratoDepth = 0.015f, LowPassHz = 1600f,
            });
        }

        private static void Victory(float[] b, int sr)
        {
            // C major fanfare arpeggio, then a held chord.
            float[] arp = { 72f, 76f, 79f, 84f };
            for (int i = 0; i < arp.Length; i++)
            {
                Synth.AddTone(b, sr, Waveform.Square, i * 0.13f, 0.16f, N(arp[i]), 0f, 0.3f, new Adsr(0.005f, 0.05f, 0.7f, 0.04f), 4000f);
                Synth.AddTone(b, sr, Waveform.Triangle, i * 0.13f, 0.16f, N(arp[i] - 12f), 0f, 0.3f, new Adsr(0.005f, 0.05f, 0.7f, 0.04f));
            }
            float[] chord = { 72f, 76f, 79f, 84f };
            for (int i = 0; i < chord.Length; i++)
            {
                Synth.AddTone(b, sr, new Synth.Tone
                {
                    Wave = Waveform.Square, Start = 0.55f, Duration = 1.6f, FreqStart = N(chord[i]), Amplitude = 0.18f,
                    Envelope = new Adsr(0.01f, 0.2f, 0.6f, 0.9f), VibratoHz = 5f, VibratoDepth = 0.004f, LowPassHz = 3500f,
                });
            }
            Synth.AddTone(b, sr, Waveform.Triangle, 0.55f, 1.6f, N(48), 0f, 0.5f, new Adsr(0.01f, 0.2f, 0.7f, 0.9f));
            Synth.AddNoise(b, sr, Noise(0.55f, 0.4f, 0.25f, Adsr.Pluck(0.38f), 0f, 0f, 6000f, 131)); // cymbal
        }

        private static void Defeat(float[] b, int sr)
        {
            // Slow chromatic descent into a low minor drone.
            float[] notes = { 67f, 66f, 65f, 64f };
            for (int i = 0; i < notes.Length; i++)
                Synth.AddTone(b, sr, Waveform.Saw, i * 0.32f, 0.34f, N(notes[i]), 0f, 0.4f, new Adsr(0.02f, 0.1f, 0.6f, 0.1f), 1300f);
            Synth.AddTone(b, sr, Waveform.Saw, 1.28f, 0.9f, N(52), N(50), 0.4f, new Adsr(0.03f, 0.2f, 0.6f, 0.5f), 900f);
            Synth.AddTone(b, sr, Waveform.Triangle, 1.28f, 0.9f, N(40), N(38), 0.6f, new Adsr(0.03f, 0.2f, 0.7f, 0.5f));
            Synth.AddTone(b, sr, Waveform.Sine, 1.28f, 0.5f, 90f, 40f, 0.7f, Adsr.Pluck(0.45f));
        }

        // ------------------------------------------------------------------ UI / building

        private static void Click(float[] b, int sr)
        {
            Synth.AddTone(b, sr, Waveform.Sine, 0f, 0.04f, 1900f, 1400f, 0.8f, Adsr.Pluck(0.035f));
            Synth.AddNoise(b, sr, Noise(0f, 0.01f, 0.3f, Adsr.Pluck(0.01f), 0f, 0f, 3000f, 137));
        }

        private static void Knock(float[] b, int sr, float t, uint seed)
        {
            Synth.AddTone(b, sr, Waveform.Sine, t, 0.12f, 210f, 120f, 0.9f, Adsr.Pluck(0.11f));
            Synth.AddNoise(b, sr, Noise(t, 0.09f, 0.6f, Adsr.Pluck(0.08f), 2200f, 600f, 0f, seed));
        }

        private static void Build(float[] b, int sr)
        {
            // Two hammer knocks and a settling thud.
            Knock(b, sr, 0f, 139);
            Knock(b, sr, 0.14f, 149);
            Synth.AddTone(b, sr, Waveform.Sine, 0.27f, 0.14f, 140f, 70f, 0.8f, Adsr.Pluck(0.13f));
            Synth.AddNoise(b, sr, Noise(0.27f, 0.14f, 0.4f, Adsr.Pluck(0.13f), 900f, 200f, 0f, 151));
        }

        private static void Upgrade(float[] b, int sr)
        {
            float[] notes = { 67f, 72f, 76f, 79f };
            for (int i = 0; i < notes.Length; i++)
                Synth.AddTone(b, sr, Waveform.Pulse, i * 0.07f, 0.16f, N(notes[i]), 0f, 0.3f, new Adsr(0.004f, 0.06f, 0.5f, 0.06f), 5000f);
            Synth.AddTone(b, sr, Waveform.Triangle, 0.28f, 0.3f, N(91), 0f, 0.3f, new Adsr(0.005f, 0.28f, 0f, 0.02f)); // sparkle
            Knock(b, sr, 0f, 157);
        }

        private static void Sell(float[] b, int sr)
        {
            // Descending coin pair over a soft knock.
            Synth.AddTone(b, sr, Waveform.Square, 0f, 0.08f, N(88), 0f, 0.3f, new Adsr(0.002f, 0.02f, 0.8f, 0.01f), 6000f);
            Synth.AddTone(b, sr, Waveform.Square, 0.08f, 0.24f, N(83), 0f, 0.3f, new Adsr(0.002f, 0.05f, 0.6f, 0.15f), 6000f);
            Synth.AddTone(b, sr, Waveform.Sine, 0f, 0.15f, 180f, 90f, 0.5f, Adsr.Pluck(0.14f));
        }
    }
}
