using System.Drawing;
using System.Drawing.Drawing2D;
using TmrOverlay.App.Settings;
using TmrOverlay.App.Telemetry;

namespace TmrOverlay.App.Overlays.Status;

internal sealed class StatusOverlayForm : Form
{
    private const int WsExToolWindow = 0x00000080;
    private readonly TelemetryCaptureState _state;
    private readonly OverlaySettings _settings;
    private readonly Action _saveSettings;
    private readonly Action _closeApplication;
    private readonly Panel _indicatorPanel;
    private readonly Label _titleLabel;
    private readonly Label _statusLabel;
    private readonly Label _detailLabel;
    private readonly Button _closeButton;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private Point _dragCursorOrigin;
    private Point _dragFormOrigin;
    private bool _dragging;

    public StatusOverlayForm(
        TelemetryCaptureState state,
        OverlaySettings settings,
        Action saveSettings,
        Action closeApplication)
    {
        _state = state;
        _settings = settings;
        _saveSettings = saveSettings;
        _closeApplication = closeApplication;

        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(26, 26, 26);
        ClientSize = new Size(
            _settings.Width > 0 ? _settings.Width : StatusOverlayDefinition.Definition.DefaultWidth,
            _settings.Height > 0 ? _settings.Height : StatusOverlayDefinition.Definition.DefaultHeight);
        DoubleBuffered = true;
        FormBorderStyle = FormBorderStyle.None;
        Location = new Point(_settings.X, _settings.Y);
        MaximizeBox = false;
        MinimizeBox = false;
        Opacity = _settings.Opacity;
        Padding = new Padding(14, 12, 14, 12);
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = _settings.AlwaysOnTop;

        _indicatorPanel = new Panel
        {
            Location = new Point(16, 16),
            Size = new Size(12, 12),
            BackColor = Color.FromArgb(140, 140, 140)
        };
        _indicatorPanel.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(_indicatorPanel.BackColor);
            e.Graphics.FillEllipse(brush, 0, 0, _indicatorPanel.Width - 1, _indicatorPanel.Height - 1);
        };

        _titleLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold, GraphicsUnit.Point),
            Location = new Point(36, 10),
            Text = "TmrOverlay"
        };

        _statusLabel = new Label
        {
            AutoSize = false,
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point),
            Location = new Point(16, 36),
            Size = new Size(236, 22),
            Text = "Waiting for iRacing"
        };

        _detailLabel = new Label
        {
            AutoSize = false,
            ForeColor = Color.FromArgb(180, 180, 180),
            Font = new Font("Consolas", 9f, FontStyle.Regular, GraphicsUnit.Point),
            Location = new Point(16, 58),
            Size = new Size(272, 18),
            Text = "collector idle"
        };

        _closeButton = new Button
        {
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(225, 225, 225),
            Location = new Point(268, 8),
            Size = new Size(26, 24),
            TabStop = false,
            Text = "X",
            UseVisualStyleBackColor = false
        };
        _closeButton.FlatAppearance.BorderSize = 0;
        _closeButton.Cursor = Cursors.Hand;
        _closeButton.Click += (_, _) => _closeApplication();

        Controls.Add(_indicatorPanel);
        Controls.Add(_titleLabel);
        Controls.Add(_statusLabel);
        Controls.Add(_detailLabel);
        Controls.Add(_closeButton);

        RegisterDragSurface(this);
        RegisterDragSurface(_indicatorPanel);
        RegisterDragSurface(_titleLabel);
        RegisterDragSurface(_statusLabel);
        RegisterDragSurface(_detailLabel);

        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 250
        };
        _refreshTimer.Tick += (_, _) => RefreshOverlay();
        _refreshTimer.Start();

        RefreshOverlay();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var createParams = base.CreateParams;
            createParams.ExStyle |= WsExToolWindow;
            return createParams;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
            _closeButton.Dispose();
            _titleLabel.Dispose();
            _statusLabel.Dispose();
            _detailLabel.Dispose();
            _indicatorPanel.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var borderPen = new Pen(Color.FromArgb(72, 255, 255, 255));
        e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
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
            _dragFormOrigin.X + (cursor.X - _dragCursorOrigin.X),
            _dragFormOrigin.Y + (cursor.Y - _dragCursorOrigin.Y));
    }

    private void EndDrag(object? sender, MouseEventArgs e)
    {
        _dragging = false;
        _settings.X = Location.X;
        _settings.Y = Location.Y;
        _settings.Width = Width;
        _settings.Height = Height;
        _settings.Opacity = Opacity;
        _settings.AlwaysOnTop = TopMost;
        _saveSettings();
    }

    private void RefreshOverlay()
    {
        var snapshot = _state.Snapshot();

        if (snapshot.IsCapturing)
        {
            BackColor = Color.FromArgb(18, 46, 34);
            _indicatorPanel.BackColor = Color.FromArgb(80, 214, 124);
            _closeButton.BackColor = Color.FromArgb(28, 67, 49);
            _statusLabel.Text = "Collecting live session data";
            _detailLabel.Text = $"frames {snapshot.FrameCount,7:N0}   drops {snapshot.DroppedFrameCount,4:N0}";
        }
        else if (snapshot.IsConnected)
        {
            BackColor = Color.FromArgb(56, 44, 14);
            _indicatorPanel.BackColor = Color.FromArgb(242, 176, 64);
            _closeButton.BackColor = Color.FromArgb(87, 67, 22);
            _statusLabel.Text = "Connected to iRacing";
            _detailLabel.Text = "waiting for first telemetry frame";
        }
        else
        {
            BackColor = Color.FromArgb(26, 26, 26);
            _indicatorPanel.BackColor = Color.FromArgb(140, 140, 140);
            _closeButton.BackColor = Color.FromArgb(54, 54, 54);
            _statusLabel.Text = "Waiting for iRacing";
            _detailLabel.Text = "collector idle";
        }

        _indicatorPanel.Invalidate();
        Invalidate();
    }
}
