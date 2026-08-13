using CounterStrikeSharp.API.Core;

namespace RoundEndSounds;

public class RoundEndSoundsConfig : BasePluginConfig
{
    public string DatabaseHost { get; set; } = "";
    public int DatabasePort { get; set; } = 3306;
    public string DatabaseUser { get; set; } = "";
    public string DatabasePassword { get; set; } = "";
    public string DatabaseName { get; set; } = "";
    public float DefaultVolume { get; set; } = 0.3f;
    public bool Randomize { get; set; } = true;
    public int MessageType { get; set; } = 1;
    public List<string> SoundEventFiles { get; set; } = ["soundevents_roundendsound.vsndevts"];

    public Dictionary<string, SoundDefinition> Sounds { get; set; } = new()
    {
        {
            "bbno$ - u mad!",
            new SoundDefinition
            {
                Sound = "dp_1.1",
                SoundPic = "https://raw.githubusercontent.com/T3Marius/GIFS-Repository/refs/heads/main/Flawless_4k.gif"
            }
        },
        {
            "The Verkkars - EZ4ENCE",
            new SoundDefinition { Sound = "Music.MVPPreview.theverkkars_01" }
        }
    };
}

public class SoundDefinition
{
    public string Sound { get; init; } = "";
    public string SoundPic { get; init; } = "";
}
