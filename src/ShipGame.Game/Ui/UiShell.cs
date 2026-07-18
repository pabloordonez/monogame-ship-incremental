namespace ShipGame.Game;

/// <summary>
/// Thin focus/hover/press shell for 640×360 virtual UI. Host rebuilds controls per screen;
/// presentation reads states for chrome.
/// </summary>
public sealed class UiShell
{
    private readonly List<UiControl> _controls = new(32);
    private MetaScreen? _boundScreen;
    private int _focusIndex = -1;
    private string? _hoveredId;
    private string? _pressedId;
    private bool _pointerDown;

    public MetaScreen? BoundScreen => _boundScreen;
    public IReadOnlyList<UiControl> Controls => _controls;
    public string? FocusedId =>
        _focusIndex >= 0 && _focusIndex < _controls.Count ? _controls[_focusIndex].Id : null;
    public string? HoveredId => _hoveredId;
    public string? PressedId => _pressedId;

    public void Begin(MetaScreen screen)
    {
        if (_boundScreen != screen)
        {
            _focusIndex = -1;
            _hoveredId = null;
            _pressedId = null;
            _pointerDown = false;
            _boundScreen = screen;
        }

        _controls.Clear();
    }

    public void Add(string id, UiRect bounds, string label, bool enabled, Action activate) =>
        _controls.Add(new UiControl(id, bounds, label, enabled, activate));

    public void EndBuild()
    {
        if (_controls.Count == 0)
        {
            _focusIndex = -1;
            return;
        }

        if (_focusIndex < 0 || _focusIndex >= _controls.Count || !_controls[_focusIndex].Enabled)
            _focusIndex = FirstEnabledIndex();
    }

    public void Focus(string id)
    {
        var index = IndexOf(id);
        if (index >= 0 && _controls[index].Enabled)
            _focusIndex = index;
    }

    public void MoveFocus(int delta)
    {
        if (_controls.Count == 0 || delta == 0)
            return;
        var start = _focusIndex < 0 ? 0 : _focusIndex;
        var index = start;
        for (var step = 0; step < _controls.Count; step++)
        {
            index = (index + delta) % _controls.Count;
            if (index < 0)
                index += _controls.Count;
            if (_controls[index].Enabled)
            {
                _focusIndex = index;
                return;
            }
        }
    }

    public bool TryActivateFocused()
    {
        if (_focusIndex < 0 || _focusIndex >= _controls.Count)
            return false;
        var control = _controls[_focusIndex];
        if (!control.Enabled)
            return false;
        control.Activate();
        return true;
    }

    public void UpdatePointer(int virtualX, int virtualY, bool leftDown, bool leftPressed)
    {
        _hoveredId = null;
        for (var i = 0; i < _controls.Count; i++)
        {
            var control = _controls[i];
            if (!control.Bounds.Contains(virtualX, virtualY))
                continue;
            _hoveredId = control.Id;
            if (control.Enabled)
                _focusIndex = i;
            break;
        }

        if (leftPressed && _hoveredId is not null)
        {
            var hovered = _controls[IndexOf(_hoveredId)];
            if (hovered.Enabled)
            {
                _pressedId = hovered.Id;
                _pointerDown = true;
            }
        }

        if (!leftDown)
        {
            if (_pointerDown &&
                _pressedId is not null &&
                string.Equals(_pressedId, _hoveredId, StringComparison.Ordinal))
            {
                var index = IndexOf(_pressedId);
                if (index >= 0 && _controls[index].Enabled)
                    _controls[index].Activate();
            }

            _pressedId = null;
            _pointerDown = false;
        }
        else if (_pressedId is not null &&
                 (_hoveredId is null || !string.Equals(_pressedId, _hoveredId, StringComparison.Ordinal)))
        {
            // Keep pressed visual only while pointer remains over the control.
            _pressedId = null;
        }
    }

    public UiControlState GetState(string id)
    {
        var index = IndexOf(id);
        if (index < 0)
            return UiControlState.Normal;
        var control = _controls[index];
        if (!control.Enabled)
            return UiControlState.Disabled;
        if (string.Equals(_pressedId, id, StringComparison.Ordinal))
            return UiControlState.Pressed;
        if (index == _focusIndex)
            return UiControlState.Focused;
        if (string.Equals(_hoveredId, id, StringComparison.Ordinal))
            return UiControlState.Hovered;
        return UiControlState.Normal;
    }

    public static bool TryMapScreenToVirtual(
        int mouseX,
        int mouseY,
        int backBufferWidth,
        int backBufferHeight,
        out int virtualX,
        out int virtualY)
    {
        var scale = Math.Max(
            1,
            Math.Min(
                backBufferWidth / MvpPresentation.VirtualWidth,
                backBufferHeight / MvpPresentation.VirtualHeight));
        var offsetX = (backBufferWidth - MvpPresentation.VirtualWidth * scale) / 2;
        var offsetY = (backBufferHeight - MvpPresentation.VirtualHeight * scale) / 2;
        virtualX = (int)MathF.Floor((mouseX - offsetX) / (float)scale);
        virtualY = (int)MathF.Floor((mouseY - offsetY) / (float)scale);
        return virtualX >= 0 &&
               virtualY >= 0 &&
               virtualX < MvpPresentation.VirtualWidth &&
               virtualY < MvpPresentation.VirtualHeight;
    }

    private int FirstEnabledIndex()
    {
        for (var i = 0; i < _controls.Count; i++)
        {
            if (_controls[i].Enabled)
                return i;
        }

        return -1;
    }

    private int IndexOf(string id)
    {
        for (var i = 0; i < _controls.Count; i++)
        {
            if (string.Equals(_controls[i].Id, id, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }
}
