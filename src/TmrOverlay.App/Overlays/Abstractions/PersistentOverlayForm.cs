using System.Drawing;
using TmrOverlay.Core.Settings;

namespace TmrOverlay.App.Overlays.Abstractions;

internal abstract class PersistentOverlayForm : Form
{
    private const int WsExToolWindow = 0x00000080;

    private readonly OverlaySettings _settings;
    private readonly Action _saveSettings;
    private Point _dragCursorOrigin;
    private Point _dragFormOrigin;
    private bool _dragging;

    protected PersistentOverlayForm(
        OverlaySettings settings,
        Action saveSettings,
        int defaultWidth,
        int defaultHeight)
    {
        _settings = settings;
        _saveSettings = saveSettings;

        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(
            _settings.Width > 0 ? _settings.Width : defaultWidth,
            _settings.Height > 0 ? _settings.Height : defaultHeight);
        DoubleBuffered = true;
        FormBorderStyle = FormBorderStyle.None;
        Location = new Point(_settings.X, _settings.Y);
        MaximizeBox = false;
        MinimizeBox = false;
        Opacity = _settings.Opacity;
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = _settings.AlwaysOnTop;

        RegisterDragSurface(this);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var createParams = base.CreateParams;
            if (UseToolWindowStyle)
            {
                createParams.ExStyle |= WsExToolWindow;
            }

            return createParams;
        }
    }

    protected virtual bool UseToolWindowStyle => true;

    protected void RegisterDragSurfaces(params Control[] controls)
    {
        foreach (var control in controls)
        {
            RegisterDragSurface(control);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        PersistOverlayFrame();
        base.OnFormClosing(e);
    }

    protected virtual Size GetPersistedOverlaySize()
    {
        return Size;
    }

    private void RegisterDragSurface(Control control)
    {
        control.Cursor = Cursors.SizeAll;
        control.MouseDown += BeginDrag;
        control.MouseMove += DragOverlay;
        control.MouseUp += EndDrag;
    }

    private void BeginDrag(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _dragging = true;
        _dragCursorOrigin = Cursor.Position;
        _dragFormOrigin = Location;
    }

    private void DragOverlay(object? sender, MouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var cursor = Cursor.Position;
        Location = new Point(
            _dragFormOrigin.X + cursor.X - _dragCursorOrigin.X,
            _dragFormOrigin.Y + cursor.Y - _dragCursorOrigin.Y);
    }

    private void EndDrag(object? sender, MouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        PersistOverlayFrame();
    }

    private void PersistOverlayFrame()
    {
        var persistedSize = GetPersistedOverlaySize();
        _settings.X = Location.X;
        _settings.Y = Location.Y;
        _settings.Width = persistedSize.Width;
        _settings.Height = persistedSize.Height;
        _settings.Opacity = Opacity;
        _settings.AlwaysOnTop = TopMost;
        _saveSettings();
    }
}
