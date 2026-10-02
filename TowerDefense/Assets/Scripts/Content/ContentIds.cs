namespace TowerDefense.Content
{
    /// <summary>
    /// Stable content keys (saves, lookups, level enemy pools). Engine-free so
    /// level layouts and their .NET tests can reference them. Never rename a
    /// shipped id: save files store them.
    /// </summary>
    public static class ContentIds
    {
        // ---------------- Towers ----------------
        public const string Archer = "archer";
        public const string Mage = "mage";
        public const string Artillery = "artillery";
        public const string Frost = "frost";
        public const string Alchemist = "alchemist";

        public static readonly string[] Towers = { Archer, Mage, Artillery, Frost, Alchemist };

        // ---------------- Enemies (appear in wave pools) ----------------
        public const string Grunt = "grunt";
        public const string Runner = "runner";
        public const string Bat = "bat";
        public const string Brute = "brute";
        public const string Shaman = "shaman";
        public const string Knight = "knight";
        public const string Splitter = "splitter";
        public const string Warder = "warder";
        public const string Troll = "troll";
        public const string Wyvern = "wyvern";

        public static readonly string[] Enemies =
            { Grunt, Runner, Bat, Brute, Shaman, Knight, Splitter, Warder, Troll, Wyvern };

        // ---------------- Minions (only spawned by abilities) ----------------
        public const string Slimeling = "slimeling";
        public const string Skeleton = "skeleton";
        public const string Spiderling = "spiderling";

        public static readonly string[] Minions = { Slimeling, Skeleton, Spiderling };

        // ---------------- Bosses ----------------
        public const string Warlord = "warlord";
        public const string Necromancer = "necromancer";
        public const string Broodmother = "broodmother";
        public const string Drake = "drake";

        public static readonly string[] Bosses = { Warlord, Necromancer, Broodmother, Drake };

        // ---------------- Levels (campaign order lives in CampaignLayouts) ----------------
        public const string Meadow = "meadow";
        public const string Crossroads = "crossroads";
        public const string Frostfang = "frostfang";
        public const string Marsh = "marsh";
        public const string Citadel = "citadel";

        /// <summary>Suffix that turns a campaign level id into its endless variant id.</summary>
        public const string EndlessSuffix = "_endless";

        public static string EndlessIdFor(string levelId) => levelId + EndlessSuffix;

        public static bool IsEndlessId(string levelId) =>
            levelId != null && levelId.EndsWith(EndlessSuffix, System.StringComparison.Ordinal);

        /// <summary>"meadow_endless" -> "meadow"; campaign ids are returned unchanged.</summary>
        public static string BaseLevelId(string levelId) =>
            IsEndlessId(levelId) ? levelId.Substring(0, levelId.Length - EndlessSuffix.Length) : levelId;

        public static bool Contains(string[] ids, string id)
        {
            if (ids == null) return false;
            for (int i = 0; i < ids.Length; i++)
                if (ids[i] == id) return true;
            return false;
        }
    }
}
