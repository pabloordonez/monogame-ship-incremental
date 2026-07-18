using ShipGame.Domain;

namespace ShipGame.Game.Smoke.Tests;

public sealed class UiShellTests
{
    [Fact]
    public void FocusWrapsAmongEnabledControlsAndSkipsDisabled()
    {
        var shell = new UiShell();
        var activated = new List<string>();
        shell.Begin(MetaScreen.Station);
        shell.Add("a", new UiRect(0, 0, 10, 10), "A", true, () => activated.Add("a"));
        shell.Add("b", new UiRect(0, 20, 10, 10), "B", false, () => activated.Add("b"));
        shell.Add("c", new UiRect(0, 40, 10, 10), "C", true, () => activated.Add("c"));
        shell.EndBuild();

        Assert.Equal("a", shell.FocusedId);
        Assert.Equal(UiControlState.Focused, shell.GetState("a"));
        Assert.Equal(UiControlState.Disabled, shell.GetState("b"));

        shell.MoveFocus(1);
        Assert.Equal("c", shell.FocusedId);
        shell.MoveFocus(1);
        Assert.Equal("a", shell.FocusedId);
        shell.MoveFocus(-1);
        Assert.Equal("c", shell.FocusedId);

        Assert.True(shell.TryActivateFocused());
        Assert.Equal(["c"], activated);
    }

    [Fact]
    public void MouseHoverFocusesAndClickActivatesOnRelease()
    {
        var shell = new UiShell();
        var activated = 0;
        shell.Begin(MetaScreen.Title);
        shell.Add("one", new UiRect(10, 10, 40, 20), "One", true, () => activated++);
        shell.Add("two", new UiRect(10, 40, 40, 20), "Two", true, () => { });
        shell.EndBuild();

        shell.UpdatePointer(15, 15, leftDown: true, leftPressed: true);
        Assert.Equal("one", shell.FocusedId);
        Assert.Equal("one", shell.HoveredId);
        Assert.Equal(UiControlState.Pressed, shell.GetState("one"));
        Assert.Equal(0, activated);

        shell.UpdatePointer(15, 15, leftDown: false, leftPressed: false);
        Assert.Equal(1, activated);
        Assert.Null(shell.PressedId);
    }

    [Fact]
    public void MouseDragOffControlCancelsActivation()
    {
        var shell = new UiShell();
        var activated = 0;
        shell.Begin(MetaScreen.Title);
        shell.Add("one", new UiRect(10, 10, 40, 20), "One", true, () => activated++);
        shell.EndBuild();

        shell.UpdatePointer(15, 15, leftDown: true, leftPressed: true);
        shell.UpdatePointer(100, 100, leftDown: true, leftPressed: false);
        shell.UpdatePointer(100, 100, leftDown: false, leftPressed: false);
        Assert.Equal(0, activated);
    }

    [Fact]
    public void ScreenChangeResetsFocus()
    {
        var shell = new UiShell();
        shell.Begin(MetaScreen.Station);
        shell.Add("lobby", new UiRect(0, 0, 10, 10), "Lobby", true, () => { });
        shell.EndBuild();
        shell.Focus("lobby");

        shell.Begin(MetaScreen.Map);
        shell.Add("map", new UiRect(0, 0, 10, 10), "Map", true, () => { });
        shell.EndBuild();
        Assert.Equal("map", shell.FocusedId);
    }

    [Fact]
    public void TryMapScreenToVirtualUsesLetterboxScale()
    {
        // 1280x720 -> scale 2, no letterbox offset
        Assert.True(UiShell.TryMapScreenToVirtual(100, 50, 1280, 720, out var vx, out var vy));
        Assert.Equal(50, vx);
        Assert.Equal(25, vy);

        // Point in side letterbox on a wider buffer should miss
        Assert.False(UiShell.TryMapScreenToVirtual(10, 100, 1600, 720, out _, out _));
    }

    [Fact]
    public void MapSelectionAndSettingsAreReachableViaMetaSession()
    {
        var root = Path.Combine(Path.GetTempPath(), "ShipGame-UiShell-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var session = new MetaSession(root, newProfileSeed: 42);
            Assert.True(session.Navigate(MetaScreen.Station).Accepted);
            Assert.True(session.Navigate(MetaScreen.Map).Accepted);
            Assert.True(session.SelectEnvironment(MetaContentIds.CinderBelt).Accepted);
            Assert.Equal(MetaContentIds.CinderBelt, session.Ui.SelectedEnvironmentId);
            Assert.True(session.Back().Accepted);
            Assert.True(session.Navigate(MetaScreen.Settings).Accepted);
            Assert.True(session.ApplySettings(
                "TX_UI_TEST",
                GameSettings.Default with { Flashes = false, ScreenShake = false }).Accepted);
            Assert.False(session.Profile.Snapshot.Settings.Flashes);
            Assert.False(session.Profile.Snapshot.Settings.ScreenShake);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }
}
