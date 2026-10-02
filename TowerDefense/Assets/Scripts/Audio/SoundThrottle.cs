using System;

namespace TowerDefense.Audio
{
    /// <summary>
    /// Decides whether a sound may start now, per <see cref="SoundCatalog"/>:
    /// a minimum real-time gap between starts and a cap on overlapping voices.
    /// Uses real (unscaled) time, so 3x game speed triples the requests but
    /// not the sounds that reach the speaker. Allocation-free after construction.
    /// </summary>
    public sealed class SoundThrottle
    {
        private readonly double[] lastStart;
        private readonly double[][] voiceEnds;

        public SoundThrottle()
        {
            int n = SoundCatalog.Count;
            lastStart = new double[n];
            voiceEnds = new double[n][];
            for (int i = 0; i < n; i++)
            {
                lastStart[i] = double.NegativeInfinity;
                voiceEnds[i] = new double[Math.Max(1, SoundCatalog.Get((SoundId)i).MaxVoices)];
                for (int v = 0; v < voiceEnds[i].Length; v++) voiceEnds[i][v] = double.NegativeInfinity;
            }
        }

        /// <summary>
        /// Returns true and reserves a voice if <paramref name="id"/> may play at
        /// <paramref name="now"/> for <paramref name="duration"/> seconds.
        /// </summary>
        public bool TryStart(SoundId id, double now, double duration)
        {
            int i = (int)id;
            SoundInfo info = SoundCatalog.Get(id);
            if (now - lastStart[i] < info.MinInterval) return false;

            double[] ends = voiceEnds[i];
            int free = -1;
            for (int v = 0; v < ends.Length; v++)
            {
                if (ends[v] <= now) { free = v; break; }
            }
            if (free < 0) return false;

            ends[free] = now + Math.Max(0.0, duration);
            lastStart[i] = now;
            return true;
        }

        /// <summary>Voices of <paramref name="id"/> still sounding at <paramref name="now"/>.</summary>
        public int ActiveVoices(SoundId id, double now)
        {
            double[] ends = voiceEnds[(int)id];
            int n = 0;
            for (int v = 0; v < ends.Length; v++) if (ends[v] > now) n++;
            return n;
        }

        /// <summary>Forget all history (e.g. after a long pause or a scene change).</summary>
        public void Reset()
        {
            for (int i = 0; i < lastStart.Length; i++)
            {
                lastStart[i] = double.NegativeInfinity;
                for (int v = 0; v < voiceEnds[i].Length; v++) voiceEnds[i][v] = double.NegativeInfinity;
            }
        }
    }
}
