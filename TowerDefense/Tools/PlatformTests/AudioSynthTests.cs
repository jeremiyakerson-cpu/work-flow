using System;
using System.Diagnostics;
using NUnit.Framework;
using TowerDefense.Audio;

namespace TowerDefense.PlatformTests
{
    [TestFixture]
    public class AudioSynthTests
    {
        private const int Rate = 44100;

        private static SoundId[] AllSounds => (SoundId[])Enum.GetValues(typeof(SoundId));
        private static MusicTrack[] AllTracks => new[] { MusicTrack.Menu, MusicTrack.Battle };

        private static void AssertValidSignal(float[] b, string name)
        {
            double sumSq = 0;
            float peak = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float s = b[i];
                Assert.IsFalse(float.IsNaN(s) || float.IsInfinity(s), name + ": non-finite sample at " + i);
                Assert.That(s, Is.InRange(-1f, 1f), name + ": sample out of range at " + i);
                sumSq += s * s;
                peak = Math.Max(peak, Math.Abs(s));
            }
            double rms = Math.Sqrt(sumSq / b.Length);
            Assert.Greater(rms, 0.01, name + " is (nearly) silent");
            Assert.Greater(peak, 0.5f, name + " peak too low");
        }

        [TestCaseSource(nameof(AllSounds))]
        public void Sfx_IsFiniteInRangeAudibleAndExpectedLength(SoundId id)
        {
            float[] b = SfxRecipes.Render(id, Rate);
            Assert.AreEqual(Synth.Samples(SfxRecipes.Duration(id), Rate), b.Length);
            Assert.That(SfxRecipes.Duration(id), Is.InRange(0.03f, 2.5f));
            AssertValidSignal(b, id.ToString());
            Assert.LessOrEqual(Math.Abs(b[0]), 0.01f, id + " starts with a click");
            Assert.LessOrEqual(Math.Abs(b[b.Length - 1]), 0.01f, id + " ends with a click");
        }

        [TestCaseSource(nameof(AllSounds))]
        public void Sfx_IsDeterministic(SoundId id)
        {
            CollectionAssert.AreEqual(SfxRecipes.Render(id, 22050), SfxRecipes.Render(id, 22050));
        }

        [Test]
        public void Sfx_RendersAtOtherSampleRates()
        {
            foreach (int rate in new[] { 22050, 48000 })
            {
                foreach (SoundId id in AllSounds)
                {
                    float[] b = SfxRecipes.Render(id, rate);
                    Assert.AreEqual(Synth.Samples(SfxRecipes.Duration(id), rate), b.Length);
                    AssertValidSignal(b, id + "@" + rate);
                }
            }
        }

        [Test]
        public void Sfx_InvalidSampleRate_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SfxRecipes.Render(SoundId.Coin, 100));
        }

        [TestCaseSource(nameof(AllTracks))]
        public void Music_IsValidExactLoop(MusicTrack track)
        {
            const int rate = 32000;
            float[] b = MusicComposer.Render(track, rate);
            float loop = MusicComposer.LoopSeconds(track);
            Assert.AreEqual(Synth.Samples(loop, rate), b.Length);
            Assert.That(loop, Is.InRange(8f, 40f));
            // A whole number of beats -> the loop lands on the downbeat.
            double beats = loop / MusicComposer.BeatSeconds(track);
            Assert.AreEqual(Math.Round(beats), beats, 1e-3);
            AssertValidSignal(b, track.ToString());
        }

        [TestCaseSource(nameof(AllTracks))]
        public void Music_LoopSeamIsContinuous(MusicTrack track)
        {
            const int rate = 32000;
            float[] b = MusicComposer.Render(track, rate);
            // The jump across the seam (last -> first sample) should look like any
            // other step in the signal, not a discontinuity.
            float seam = Math.Abs(b[0] - b[b.Length - 1]);
            double meanStep = 0;
            float maxStep = 0f;
            for (int i = 1; i < b.Length; i++)
            {
                float d = Math.Abs(b[i] - b[i - 1]);
                meanStep += d;
                maxStep = Math.Max(maxStep, d);
            }
            meanStep /= b.Length - 1;
            Assert.LessOrEqual(seam, maxStep, "seam step larger than any step inside the loop");
            Assert.Less(seam, Math.Max(0.25f, (float)(meanStep * 20)), "audible click at the loop point");
        }

        [Test]
        public void Music_None_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MusicComposer.Render(MusicTrack.None, 32000));
        }

        [Test]
        public void FullLibrary_RendersQuickly()
        {
            // Startup budget sanity check (desktop CPU; device is ~3-5x slower).
            var sw = Stopwatch.StartNew();
            foreach (SoundId id in AllSounds) SfxRecipes.Render(id, Rate);
            foreach (MusicTrack t in AllTracks) MusicComposer.Render(t, 32000);
            sw.Stop();
            Assert.Less(sw.ElapsedMilliseconds, 3000, "synthesis too slow for startup");
        }

        [Test]
        public void Adsr_ShapesAndBounds()
        {
            var env = new Adsr(0.1f, 0.1f, 0.5f, 0.2f);
            Assert.AreEqual(0f, env.Evaluate(-0.1f, 1f));
            Assert.AreEqual(0.5f, env.Evaluate(0.05f, 1f), 1e-5f);
            Assert.AreEqual(1f, env.Evaluate(0.1f, 1f), 1e-5f);
            Assert.AreEqual(0.5f, env.Evaluate(0.5f, 1f), 1e-5f);
            Assert.AreEqual(0.25f, env.Evaluate(0.9f, 1f), 1e-5f);
            Assert.AreEqual(0f, env.Evaluate(1f, 1f));
            for (float t = 0; t < 1f; t += 0.001f) Assert.That(env.Evaluate(t, 1f), Is.InRange(0f, 1f));
        }

        [Test]
        public void Oscillators_StayInRange()
        {
            foreach (Waveform w in Enum.GetValues(typeof(Waveform)))
                for (double p = -2; p < 2; p += 0.0137)
                    Assert.That(Synth.Oscillator(w, p), Is.InRange(-1f, 1f), w.ToString());
        }

        [Test]
        public void Noise_IsDeterministicAndBounded()
        {
            var a = new NoiseSource(42);
            var b = new NoiseSource(42);
            double sum = 0;
            for (int i = 0; i < 10000; i++)
            {
                float x = a.Next();
                Assert.AreEqual(x, b.Next());
                Assert.That(x, Is.InRange(-1f, 1f));
                sum += x;
            }
            Assert.Less(Math.Abs(sum / 10000), 0.05, "noise should be roughly zero-mean");
            Assert.AreNotEqual(new NoiseSource(0).Next(), 0f, "seed 0 must not lock xorshift at zero");
        }

        [Test]
        public void MidiToHz_MatchesReference()
        {
            Assert.AreEqual(440f, Synth.MidiToHz(69), 1e-3f);
            Assert.AreEqual(261.6256f, Synth.MidiToHz(60), 1e-2f);
        }

        [Test]
        public void LowPass_AttenuatesHighFrequencies()
        {
            float[] high = new float[Rate];
            float[] low = new float[Rate];
            Synth.AddTone(high, Rate, Waveform.Sine, 0f, 1f, 8000f, 0f, 1f, new Adsr(0, 0, 1, 0), 300f);
            Synth.AddTone(low, Rate, Waveform.Sine, 0f, 1f, 100f, 0f, 1f, new Adsr(0, 0, 1, 0), 300f);
            Assert.Less(Rms(high) * 5, Rms(low));
        }

        [Test]
        public void SoftClipAndSanitize_KeepRange()
        {
            float[] b = { 5f, -5f, 0.5f, float.NaN, float.PositiveInfinity, float.NegativeInfinity };
            Synth.Sanitize(b);
            CollectionAssert.AreEqual(new[] { 1f, -1f, 0.5f, 0f, 0f, 0f }, b);
            float[] c = { 3f, -3f, 0.1f };
            Synth.SoftClip(c, 2f);
            foreach (float s in c) Assert.That(s, Is.InRange(-1.0001f, 1.0001f));
        }

        [Test]
        public void Normalize_SilentBufferStaysSilent()
        {
            float[] b = new float[100];
            Synth.Normalize(b, 0.9f);
            foreach (float s in b) Assert.AreEqual(0f, s);
        }

        private static double Rms(float[] b)
        {
            double s = 0;
            foreach (float x in b) s += x * x;
            return Math.Sqrt(s / b.Length);
        }
    }

    [TestFixture]
    public class SoundThrottleTests
    {
        [Test]
        public void ThreeTimesSpeed_DoesNotMultiplySounds()
        {
            // 20 archer towers firing at 3x speed: ~60 requests per real second for 2 s.
            var throttle = new SoundThrottle();
            SoundInfo info = SoundCatalog.Get(SoundId.ArrowShot);
            int played = 0;
            for (int i = 0; i < 120; i++)
            {
                double now = i / 60.0;
                if (throttle.TryStart(SoundId.ArrowShot, now, SfxRecipes.Duration(SoundId.ArrowShot))) played++;
            }
            int maxByInterval = (int)Math.Ceiling(2.0 / info.MinInterval) + 1;
            Assert.LessOrEqual(played, maxByInterval);
            Assert.Greater(played, 5, "throttling must not silence combat entirely");
        }

        [Test]
        public void VoiceCap_IsRespected()
        {
            var throttle = new SoundThrottle();
            SoundInfo info = SoundCatalog.Get(SoundId.EnemyDeath);
            double now = 0;
            int played = 0;
            for (int i = 0; i < 50; i++)
            {
                now += info.MinInterval; // as fast as the interval allows, long sounds
                if (throttle.TryStart(SoundId.EnemyDeath, now, 10.0)) played++;
                Assert.LessOrEqual(throttle.ActiveVoices(SoundId.EnemyDeath, now), info.MaxVoices);
            }
            Assert.AreEqual(info.MaxVoices, played);
        }

        [Test]
        public void SoundsAreThrottledIndependently()
        {
            var throttle = new SoundThrottle();
            Assert.IsTrue(throttle.TryStart(SoundId.ArrowShot, 0, 0.2));
            Assert.IsFalse(throttle.TryStart(SoundId.ArrowShot, 0.01, 0.2));
            Assert.IsTrue(throttle.TryStart(SoundId.Coin, 0.01, 0.3));
            Assert.IsTrue(throttle.TryStart(SoundId.ArrowShot, 1.0, 0.2));
        }

        [Test]
        public void Reset_ClearsHistory()
        {
            var throttle = new SoundThrottle();
            Assert.IsTrue(throttle.TryStart(SoundId.BossWarning, 0, 1));
            Assert.IsFalse(throttle.TryStart(SoundId.BossWarning, 0.1, 1));
            throttle.Reset();
            Assert.IsTrue(throttle.TryStart(SoundId.BossWarning, 0.1, 1));
        }

        [Test]
        public void Catalog_HasSaneEntriesForEverySound()
        {
            foreach (SoundId id in Enum.GetValues(typeof(SoundId)))
            {
                SoundInfo info = SoundCatalog.Get(id);
                Assert.AreEqual(id, info.Id);
                Assert.That(info.Volume, Is.InRange(0.05f, 1f), id.ToString());
                Assert.GreaterOrEqual(info.MaxVoices, 1, id.ToString());
                Assert.That(info.PitchJitter, Is.InRange(0f, 0.2f), id.ToString());
                Assert.GreaterOrEqual(info.MinInterval, 0f, id.ToString());
            }
        }

        [Test]
        public void TowerShotMapping()
        {
            Assert.AreEqual(SoundId.ArrowShot, SoundMapping.ForTowerShot(false, false, 0f, false, false));
            Assert.AreEqual(SoundId.MagicZap, SoundMapping.ForTowerShot(true, false, 0f, false, false));
            Assert.AreEqual(SoundId.CannonShot, SoundMapping.ForTowerShot(false, false, 1.5f, false, false));
            Assert.AreEqual(SoundId.Frost, SoundMapping.ForTowerShot(true, false, 0f, true, false));
            Assert.AreEqual(SoundId.Poison, SoundMapping.ForTowerShot(false, true, 0f, false, false));
            Assert.AreEqual(SoundId.Poison, SoundMapping.ForTowerShot(false, false, 0f, false, true));
            Assert.AreEqual(SoundId.CannonShot, SoundMapping.ForTowerShot(true, false, 2f, true, true), "splash wins");
            Assert.AreEqual(SoundId.BossDeath, SoundMapping.ForEnemyDeath(true));
            Assert.AreEqual(SoundId.EnemyDeath, SoundMapping.ForEnemyDeath(false));
        }
    }
}
