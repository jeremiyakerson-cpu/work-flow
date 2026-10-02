using System;

namespace TowerDefense.Audio
{
    /// <summary>
    /// Procedurally composed looping background music. Each track is a fixed
    /// chord progression with bass, pad, arpeggio/lead and (battle) drums,
    /// rendered into one buffer whose length is an exact number of bars.
    /// Every note is written with wrap-around, so release tails that run past
    /// the end land at the start and the loop point is seamless.
    /// </summary>
    public static class MusicComposer
    {
        public const float Peak = 0.8f;

        private struct Chord
        {
            public int Root;      // MIDI note of the bass root
            public bool Minor;
            public Chord(int root, bool minor) { Root = root; Minor = minor; }
            public int Third => Minor ? 3 : 4;
        }

        private sealed class Spec
        {
            public float Bpm;
            public Chord[] Bars;
            public int[] Scale;     // scale degrees (semitones from tonic) for the melody
            public int Tonic;       // MIDI tonic for the melody octave
            public bool Drums;
            public uint Seed;
        }

        private static Spec GetSpec(MusicTrack track)
        {
            switch (track)
            {
                case MusicTrack.Battle:
                    // A minor, driving: Am F C G | Am F G E
                    return new Spec
                    {
                        Bpm = 120f, Drums = true, Seed = 0xBA77u, Tonic = 69,
                        Scale = new[] { 0, 3, 5, 7, 10, 12 },
                        Bars = new[]
                        {
                            new Chord(45, true), new Chord(41, false), new Chord(48, false), new Chord(43, false),
                            new Chord(45, true), new Chord(41, false), new Chord(43, false), new Chord(40, false),
                        },
                    };
                case MusicTrack.Menu:
                    // C major, calm: C Am F G | C Am Dm G
                    return new Spec
                    {
                        Bpm = 84f, Drums = false, Seed = 0x3E7Au, Tonic = 72,
                        Scale = new[] { 0, 2, 4, 7, 9, 12 },
                        Bars = new[]
                        {
                            new Chord(48, false), new Chord(45, true), new Chord(41, false), new Chord(43, false),
                            new Chord(48, false), new Chord(45, true), new Chord(50, true), new Chord(43, false),
                        },
                    };
                default:
                    throw new ArgumentOutOfRangeException(nameof(track), track, "No music for this track");
            }
        }

        public static float BeatSeconds(MusicTrack track) => 60f / GetSpec(track).Bpm;

        /// <summary>Exact loop length in seconds (bars x 4 beats).</summary>
        public static float LoopSeconds(MusicTrack track)
        {
            Spec s = GetSpec(track);
            return s.Bars.Length * 4 * 60f / s.Bpm;
        }

        /// <summary>Exact loop length in samples at <paramref name="sampleRate"/>.</summary>
        public static int LoopSamples(MusicTrack track, int sampleRate) => Synth.Samples(LoopSeconds(track), sampleRate);

        /// <summary>Render one seamless loop of <paramref name="track"/>.</summary>
        public static float[] Render(MusicTrack track, int sampleRate)
        {
            if (sampleRate < 8000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            Spec s = GetSpec(track);
            var buf = new float[LoopSamples(track, sampleRate)];
            float beat = 60f / s.Bpm;
            float bar = beat * 4f;
            var rng = new NoiseSource(s.Seed);

            for (int i = 0; i < s.Bars.Length; i++)
            {
                Chord c = s.Bars[i];
                float t0 = i * bar;
                if (s.Drums) BattleBar(buf, sampleRate, c, t0, beat, i);
                else MenuBar(buf, sampleRate, c, t0, beat);
            }
            Melody(buf, sampleRate, s, beat, ref rng);

            Synth.SoftClip(buf, 1.2f);
            Synth.Normalize(buf, Peak);
            Synth.Sanitize(buf);
            return buf;
        }

        private static float Hz(float midi) => Synth.MidiToHz(midi);

        private static void Note(float[] b, int sr, Waveform w, float start, float dur, float midi, float amp, Adsr env, float lp = 0f,
                                 float vibHz = 0f, float vibDepth = 0f)
        {
            Synth.AddTone(b, sr, new Synth.Tone
            {
                Wave = w, Start = start, Duration = dur, FreqStart = Hz(midi), Amplitude = amp, Envelope = env,
                LowPassHz = lp, VibratoHz = vibHz, VibratoDepth = vibDepth, Wrap = true,
            });
        }

        private static void BattleBar(float[] b, int sr, Chord c, float t0, float beat, int barIndex)
        {
            float eighth = beat * 0.5f;

            // Pad: the triad, held the whole bar.
            var pad = new Adsr(0.08f, 0.2f, 0.7f, 0.3f);
            Note(b, sr, Waveform.Triangle, t0, beat * 4f, c.Root + 12, 0.10f, pad);
            Note(b, sr, Waveform.Triangle, t0, beat * 4f, c.Root + 12 + c.Third, 0.08f, pad);
            Note(b, sr, Waveform.Triangle, t0, beat * 4f, c.Root + 19, 0.08f, pad);

            // Bass: driving eighths, octave jump on the off-beats.
            for (int e = 0; e < 8; e++)
            {
                int midi = c.Root + ((e & 1) == 1 ? 12 : 0);
                Note(b, sr, Waveform.Square, t0 + e * eighth, eighth * 0.95f, midi, 0.22f, new Adsr(0.003f, 0.12f, 0.35f, 0.05f), 650f);
            }

            // Drums: kick 1 & 3 (+ pickup on the last bar), snare 2 & 4, closed hats on eighths.
            for (int q = 0; q < 4; q++)
            {
                float t = t0 + q * beat;
                if (q == 0 || q == 2) Kick(b, sr, t);
                else Snare(b, sr, t, (uint)(barIndex * 4 + q + 1));
            }
            if (barIndex % 4 == 3) Kick(b, sr, t0 + beat * 3.5f);
            for (int e = 0; e < 8; e++)
            {
                Synth.AddNoise(b, sr, new Synth.NoiseBurst
                {
                    Start = t0 + e * eighth, Duration = 0.045f, Amplitude = (e & 1) == 0 ? 0.10f : 0.06f,
                    Envelope = Adsr.Pluck(0.04f), HighPassHz = 7000f, Seed = (uint)(1000 + barIndex * 8 + e), Wrap = true,
                });
            }
        }

        private static void Kick(float[] b, int sr, float t)
        {
            Synth.AddTone(b, sr, new Synth.Tone
            {
                Wave = Waveform.Sine, Start = t, Duration = 0.22f, FreqStart = 150f, FreqEnd = 45f, Amplitude = 0.55f,
                Envelope = new Adsr(0.002f, 0.2f, 0f, 0.02f), Wrap = true,
            });
        }

        private static void Snare(float[] b, int sr, float t, uint seed)
        {
            Synth.AddNoise(b, sr, new Synth.NoiseBurst
            {
                Start = t, Duration = 0.16f, Amplitude = 0.28f, Envelope = Adsr.Pluck(0.15f),
                LowPassStartHz = 5000f, LowPassEndHz = 2500f, HighPassHz = 300f, Seed = seed, Wrap = true,
            });
            Synth.AddTone(b, sr, new Synth.Tone
            {
                Wave = Waveform.Triangle, Start = t, Duration = 0.08f, FreqStart = 200f, FreqEnd = 160f, Amplitude = 0.2f,
                Envelope = Adsr.Pluck(0.075f), Wrap = true,
            });
        }

        private static void MenuBar(float[] b, int sr, Chord c, float t0, float beat)
        {
            // Long soft pad and a sustained bass.
            var pad = new Adsr(0.6f, 0.4f, 0.8f, 0.9f);
            float len = beat * 4f + 0.6f; // overlap into the next bar for legato
            Note(b, sr, Waveform.Triangle, t0, len, c.Root + 12, 0.10f, pad, 0f, 4.5f, 0.003f);
            Note(b, sr, Waveform.Triangle, t0, len, c.Root + 12 + c.Third, 0.08f, pad, 0f, 4.1f, 0.003f);
            Note(b, sr, Waveform.Triangle, t0, len, c.Root + 19, 0.08f, pad, 0f, 4.8f, 0.003f);
            Note(b, sr, Waveform.Sine, t0, beat * 4f, c.Root, 0.30f, new Adsr(0.05f, 0.5f, 0.6f, 0.4f));

            // Gentle up-down arpeggio in eighths.
            int[] pattern = { 0, 1, 2, 3, 2, 1, 0, 1 };
            int[] tones = { c.Root + 24, c.Root + 24 + c.Third, c.Root + 31, c.Root + 36 };
            float eighth = beat * 0.5f;
            for (int e = 0; e < 8; e++)
                Note(b, sr, Waveform.Sine, t0 + e * eighth, eighth * 1.6f, tones[pattern[e]], 0.09f, new Adsr(0.01f, 0.25f, 0.2f, 0.2f));
        }

        private static void Melody(float[] b, int sr, Spec s, float beat, ref NoiseSource rng)
        {
            // A 4-bar phrase answered by a varied repeat, so the loop has a shape
            // (call, response) rather than an endless random walk.
            int barsPerPhrase = s.Bars.Length / 2;
            int stepsPerBar = 8; // eighth-note grid
            int phraseSteps = barsPerPhrase * stepsPerBar;
            var degrees = new int[phraseSteps];
            var rests = new bool[phraseSteps];
            int degree = 2;
            for (int i = 0; i < phraseSteps; i++)
            {
                bool strong = i % 4 == 0;
                rests[i] = !strong && rng.Next01() < (s.Drums ? 0.35f : 0.55f);
                int stepMove = (int)Math.Round(rng.Next() * 1.6f);
                degree = Math.Max(0, Math.Min(s.Scale.Length - 1, degree + stepMove));
                degrees[i] = degree;
            }

            float eighth = beat * 0.5f;
            Waveform wave = s.Drums ? Waveform.Pulse : Waveform.Triangle;
            float amp = s.Drums ? 0.13f : 0.12f;
            for (int phrase = 0; phrase < 2; phrase++)
            {
                for (int i = 0; i < phraseSteps; i++)
                {
                    if (rests[i]) continue;
                    // Hold the note through following rests (up to a beat).
                    int hold = 1;
                    while (hold < 2 && i + hold < phraseSteps && rests[i + hold]) hold++;

                    int deg = degrees[i];
                    // Response phrase: resolve the last bar to the tonic.
                    if (phrase == 1 && i >= phraseSteps - stepsPerBar) deg = i % 4 == 0 ? 0 : Math.Min(deg, 2);
                    int barIndex = phrase * barsPerPhrase + i / stepsPerBar;
                    Chord c = s.Bars[barIndex];
                    int midi = s.Tonic + s.Scale[deg];
                    // On the downbeat, snap to the nearest chord tone so the lead agrees with the harmony.
                    if (i % stepsPerBar == 0) midi = NearestChordTone(midi, c);

                    float start = (phrase * phraseSteps + i) * eighth;
                    Note(b, sr, wave, start, eighth * hold * 0.95f, midi, amp, new Adsr(0.01f, 0.08f, 0.6f, 0.06f),
                         s.Drums ? 3200f : 0f, 5.5f, 0.005f);
                }
            }
        }

        private static int NearestChordTone(int midi, Chord c)
        {
            int best = midi;
            int bestDist = int.MaxValue;
            int[] intervals = { 0, c.Third, 7 };
            for (int octave = -2; octave <= 3; octave++)
            {
                for (int k = 0; k < intervals.Length; k++)
                {
                    int candidate = c.Root + octave * 12 + intervals[k];
                    int d = Math.Abs(candidate - midi);
                    if (d < bestDist) { bestDist = d; best = candidate; }
                }
            }
            return best;
        }
    }
}
