using System.Text.Json;
using MozaTelemetry.App;

namespace MozaTelemetry.Tests;

public sealed class HotkeyTests
{
    private sealed class FakeApi : IHotkeyApi
    {
        public List<(int Id, uint Modifiers, uint Key)> Registered { get; } = [];
        public List<int> Released { get; } = [];
        public bool Conflict { get; set; }
        public bool Register(nint window, int id, uint modifiers, uint key, out int error)
        {
            error = Conflict ? 1409 : 0;
            if (Conflict) return false;
            Registered.Add((id, modifiers, key));
            return true;
        }
        public bool Unregister(nint window, int id) { Released.Add(id); return true; }
    }

    [Fact]
    public void OldSettingsDefaultToDisabledCtrlAltF10AndRoundTripCustomChoice()
    {
        var old = JsonSerializer.Deserialize<AppSettings>("{}");
        Assert.False(old!.Hotkey.Enabled);
        Assert.Equal("Ctrl+Alt+F10", old.Hotkey.DisplayName);
        var custom = old with { Hotkey = new() { Enabled = true, Control = false, Alt = true, Shift = true, Key = "G" } };
        var json = JsonSerializer.Serialize(custom);
        Assert.DoesNotContain("VirtualKey", json);
        Assert.Equal(custom.Hotkey, JsonSerializer.Deserialize<AppSettings>(json)!.Hotkey);
    }

    [Theory]
    [InlineData("F1", 0x70)] [InlineData("F10", 0x79)] [InlineData("F11", 0x7A)]
    [InlineData("A", 0x41)] [InlineData("F", 0x46)] [InlineData("Z", 0x5A)]
    [InlineData("0", 0x30)] [InlineData("9", 0x39)]
    public void KeysMapToWindowsVirtualKeys(string key, int expected)
        => Assert.Equal((uint)expected, (new HotkeyOptions { Key = key }).VirtualKey);

    [Theory]
    [InlineData("F12")] [InlineData("F13")] [InlineData("Escape")] [InlineData("a")] [InlineData("")]
    public void RejectsUnsupportedOrReservedKeys(string key)
        => Assert.Throws<ArgumentException>(() => (new HotkeyOptions { Key = key }).Validate());

    [Fact]
    public void RequiresCtrlOrAltAndCombinesModifierFlags()
    {
        Assert.Throws<ArgumentException>(() => (new HotkeyOptions { Control = false, Alt = false, Shift = true }).Validate());
        Assert.Equal(7u, (new HotkeyOptions { Shift = true }).Modifiers);
    }

    [Fact]
    public void DisabledHotkeyDoesNotReserveAnythingAndEnableSuppressesRepeats()
    {
        var api = new FakeApi();
        using var registration = new HotkeyRegistration(1, api);
        Assert.True(registration.TryApply(new(), out _));
        Assert.Empty(api.Registered);
        Assert.True(registration.TryApply(new() { Enabled = true }, out _));
        Assert.Equal(HotkeyRegistration.NoRepeat | 3u, api.Registered.Single().Modifiers);
        Assert.True(registration.TryApply(new() { Enabled = true }, out _));
        Assert.Single(api.Registered);
    }

    [Fact]
    public void ConflictingReplacementKeepsWorkingShortcutAndShowsReason()
    {
        var api = new FakeApi();
        using var registration = new HotkeyRegistration(1, api);
        var original = new HotkeyOptions { Enabled = true };
        Assert.True(registration.TryApply(original, out _));
        var id = registration.ActiveId;
        api.Conflict = true;
        Assert.False(registration.TryApply(original with { Key = "G" }, out var error));
        Assert.Contains("already in use", error);
        Assert.Equal(id, registration.ActiveId);
        Assert.Equal(original, registration.ActiveOptions);
        Assert.Empty(api.Released);
    }

    [Fact]
    public void ReplacementRejectsStaleMessagesAndDisableReleasesRegistration()
    {
        var api = new FakeApi();
        using var registration = new HotkeyRegistration(1, api);
        var original = new HotkeyOptions { Enabled = true };
        registration.TryApply(original, out _);
        var oldId = registration.ActiveId!.Value;
        var replacement = original with { Key = "G", Shift = true };
        registration.TryApply(replacement, out _);
        var packed = (nint)((replacement.VirtualKey << 16) | replacement.Modifiers);
        Assert.Contains(oldId, api.Released);
        Assert.False(registration.Matches(oldId, packed));
        Assert.True(registration.Matches(registration.ActiveId!.Value, packed));
        Assert.True(registration.Matches(registration.ActiveId.Value, packed | (nint)HotkeyRegistration.NoRepeat));
        Assert.False(registration.Matches(registration.ActiveId.Value, packed + 1));
        var newId = registration.ActiveId.Value;
        registration.TryApply(replacement with { Enabled = false }, out _);
        Assert.Contains(newId, api.Released);
        Assert.False(registration.Matches(newId, packed));
        Assert.Null(registration.ActiveOptions);
    }

    [Fact]
    public void DisposalReleasesTheOwnedRegistrationExactlyOnce()
    {
        var api = new FakeApi();
        var registration = new HotkeyRegistration(1, api);
        registration.TryApply(new() { Enabled = true }, out _);
        registration.Dispose(); registration.Dispose();
        Assert.Single(api.Released);
        Assert.Throws<ObjectDisposedException>(() => registration.TryApply(new(), out _));
    }

    [Fact]
    public async Task ToggleStartsAndStopsOnlyItsSessionAndHonorsUnavailableStart()
    {
        var running = false;
        var available = true;
        var starts = 0; var stops = 0;
        var toggle = new SessionToggle(() => false, () => running, () => available,
            () => { running = true; starts++; return Task.CompletedTask; },
            () => { running = false; stops++; return Task.CompletedTask; });
        Assert.Equal(ToggleResult.Started, await toggle.ToggleAsync());
        available = false; // An unavailable process name must not prevent stopping our own session.
        Assert.Equal(ToggleResult.Stopped, await toggle.ToggleAsync());
        Assert.Equal(ToggleResult.Unavailable, await toggle.ToggleAsync());
        Assert.Equal(1, starts); Assert.Equal(1, stops);
    }

    [Fact]
    public async Task BusyOrBlockedTogglesNeverQueueAnotherTransition()
    {
        var blocked = true; var running = false; var starts = 0;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var toggle = new SessionToggle(() => blocked, () => running, () => true,
            async () => { starts++; await ready.Task; running = true; }, () => Task.CompletedTask);
        Assert.Equal(ToggleResult.Ignored, await toggle.ToggleAsync());
        blocked = false;
        var first = toggle.ToggleAsync();
        Assert.Equal(ToggleResult.Ignored, await toggle.ToggleAsync());
        ready.SetResult();
        Assert.Equal(ToggleResult.Started, await first);
        Assert.Equal(1, starts);
    }

    [Fact]
    public async Task FailedStartCanBeRetried()
    {
        var fail = true; var running = false;
        var toggle = new SessionToggle(() => false, () => running, () => true,
            () => { if (fail) throw new IOException("test"); running = true; return Task.CompletedTask; }, () => Task.CompletedTask);
        await Assert.ThrowsAsync<IOException>(() => toggle.ToggleAsync());
        fail = false;
        Assert.Equal(ToggleResult.Started, await toggle.ToggleAsync());
    }
}
