using System.Text.Json.Serialization;

namespace MozaTelemetry.App;

public sealed record HotkeyOptions
{
    public bool Enabled { get; init; }
    public bool Control { get; init; } = true;
    public bool Alt { get; init; } = true;
    public bool Shift { get; init; }
    public string Key { get; init; } = "F10";

    public static IReadOnlyList<string> AllowedKeys { get; } = Array.AsReadOnly(
        Enumerable.Range(1, 11).Select(value => "F" + value)
            .Concat(Enumerable.Range('A', 26).Select(value => ((char)value).ToString()))
            .Concat(Enumerable.Range(0, 10).Select(value => value.ToString())).ToArray());

    [JsonIgnore]
    public uint Modifiers => (Control ? 2u : 0) | (Alt ? 1u : 0) | (Shift ? 4u : 0);
    [JsonIgnore]
    public uint VirtualKey
    {
        get
        {
            Validate();
            return Key.StartsWith('F') && Key.Length > 1 ? 0x70u + uint.Parse(Key[1..]) - 1 : Key[0];
        }
    }

    [JsonIgnore]
    public string DisplayName => string.Join("+", new[] { Control ? "Ctrl" : null, Alt ? "Alt" : null, Shift ? "Shift" : null, Key }.OfType<string>());

    public void Validate()
    {
        if (!Control && !Alt) throw new ArgumentException("Choose Ctrl or Alt, optionally with Shift.");
        if (!AllowedKeys.Contains(Key)) throw new ArgumentException("Choose a letter, number, or F1–F11. Windows reserves F12 for debugging.");
    }
}
