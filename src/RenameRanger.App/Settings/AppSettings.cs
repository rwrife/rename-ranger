namespace RenameRanger.App.Settings;

public sealed class AppSettings
{
    public LocalAiSettings LocalAi { get; set; } = new();
}

public sealed class LocalAiSettings
{
    public const string DefaultEndpointUrl = "http://127.0.0.1:11434";
    public const string DefaultModel = "qwen2.5:1.5b";

    public bool Enabled { get; set; } = false;

    public string EndpointUrl { get; set; } = DefaultEndpointUrl;

    public string Model { get; set; } = DefaultModel;
}
