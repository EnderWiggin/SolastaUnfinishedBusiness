using Newtonsoft.Json;

namespace SolastaUnfinishedBusiness.Models;

[JsonObject]
public class InfoJson
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public string Version { get; set; }
    public string GameVersion { get; set; }
    public string ManagerVersion { get; set; }
    public string AssemblyName { get; set; }
    public string EntryMethod { get; set; }
    public string HomePage { get; set; }
    public string Repository { get; set; }
    public string VersionURL { get; set; }
    public string Changelog { get; set; }
}
