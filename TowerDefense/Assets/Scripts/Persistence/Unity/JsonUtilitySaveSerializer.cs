using UnityEngine;

namespace TowerDefense.Persistence
{
    /// <summary>
    /// Unity's built-in JSON (no extra packages, IL2CPP/stripping safe because
    /// it serializes [Serializable] fields natively, no reflection emit).
    /// </summary>
    public sealed class JsonUtilitySaveSerializer : ISaveSerializer
    {
        private readonly bool prettyPrint;

        public JsonUtilitySaveSerializer(bool prettyPrint = true)
        {
            this.prettyPrint = prettyPrint;
        }

        public string Serialize(SaveData data) => JsonUtility.ToJson(data, prettyPrint);

        // Throws ArgumentException on malformed JSON; SaveFileStore treats that as corrupt.
        public SaveData Deserialize(string json) => JsonUtility.FromJson<SaveData>(json);
    }
}
