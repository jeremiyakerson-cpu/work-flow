namespace TowerDefense.Persistence
{
    /// <summary>
    /// Turns <see cref="SaveData"/> into JSON and back. Unity uses
    /// JsonUtilitySaveSerializer; the .NET tests use System.Text.Json.
    /// Deserialize may throw or return null on malformed input - the
    /// store treats both as a corrupt file.
    /// </summary>
    public interface ISaveSerializer
    {
        string Serialize(SaveData data);
        SaveData Deserialize(string json);
    }
}
