namespace TowerDefense.UI
{
    /// <summary>Lazily cached int-to-string for counters, so animating numbers don't allocate.</summary>
    public static class NumberCache
    {
        private static readonly string[] cache = new string[10000];

        public static string Get(int value)
        {
            if (value >= 0 && value < cache.Length)
                return cache[value] ??= value.ToString();
            return value.ToString();
        }
    }
}
