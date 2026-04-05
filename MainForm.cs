using System;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace UsbTempMonitor;

public class MainForm : Form
{
    private readonly Label _tempLabel;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly ToolStripMenuItem _portMenu;
    private readonly ToolStripMenuItem _autoScanItem;
    private readonly ToolStripMenuItem _fahrenheitItem;
    private readonly ToolStripMenuItem _celsiusItem;
    private volatile bool _running = true;

    private bool _useFahrenheit = true;
    private double _lastTempC;
    private bool _hasReading;
    private volatile string? _selectedPort;   // null = auto scan
    private volatile bool _reconnect;

    public MainForm()
    {
        Text = "USB Temperature Monitor";
        float scale = DeviceDpi / 96f;
        ClientSize = new Size((int)(600 * scale), (int)(340 * scale));
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Color.FromArgb(30, 30, 30);

        using var iconStream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("UsbTempMonitor.icon.png")!;
        using var bmp = new Bitmap(iconStream);
        Icon = Icon.FromHandle(bmp.GetHicon());

        // --- Menu bar ---
        var menuStrip = new MenuStrip();

        // Port menu
        _portMenu = new ToolStripMenuItem("&Port");
        _autoScanItem = new ToolStripMenuItem("Auto Scan", null, OnAutoScan) { Checked = true };
        _portMenu.DropDownItems.Add(_autoScanItem);
        _portMenu.DropDownOpening += RefreshPortList;
        menuStrip.Items.Add(_portMenu);

        // Units menu
        var unitsMenu = new ToolStripMenuItem("&Units");
        _fahrenheitItem = new ToolStripMenuItem("°F  Fahrenheit", null, OnSelectFahrenheit) { Checked = true };
        _celsiusItem = new ToolStripMenuItem("°C  Celsius", null, OnSelectCelsius);
        unitsMenu.DropDownItems.Add(_fahrenheitItem);
        unitsMenu.DropDownItems.Add(_celsiusItem);
        menuStrip.Items.Add(unitsMenu);

        MainMenuStrip = menuStrip;
        Controls.Add(menuStrip);

        // --- Temperature label ---
        _tempLabel = new Label
        {
            Text = "--- °F",
            Font = new Font("Segoe UI", 72, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true
        };
        Controls.Add(_tempLabel);

        // --- Status bar ---
        _statusLabel = new ToolStripStatusLabel("Searching for device...")
        {
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var statusStrip = new StatusStrip
        {
            BackColor = SystemColors.Control,
            SizingGrip = false
        };
        statusStrip.Items.Add(_statusLabel);
        Controls.Add(statusStrip);

        Layout += (_, _) => CenterControls();

        new Thread(PollLoop) { IsBackground = true }.Start();
    }

    // --- Port menu handlers ---

    private void RefreshPortList(object? sender, EventArgs e)
    {
        // Keep Auto Scan + separator, rebuild the port list below
        _portMenu.DropDownItems.Clear();
        _portMenu.DropDownItems.Add(_autoScanItem);
        _portMenu.DropDownItems.Add(new ToolStripSeparator());

        string[] ports = SerialPort.GetPortNames();
        Array.Sort(ports);
        foreach (string port in ports)
        {
            var item = new ToolStripMenuItem(port, null, OnSelectPort)
            {
                Checked = (_selectedPort == port)
            };
            _portMenu.DropDownItems.Add(item);
        }
    }

    private void OnAutoScan(object? sender, EventArgs e)
    {
        _selectedPort = null;
        _autoScanItem.Checked = true;
        _reconnect = true;
    }

    private void OnSelectPort(object? sender, EventArgs e)
    {
        var item = (ToolStripMenuItem)sender!;
        _selectedPort = item.Text;
        _autoScanItem.Checked = false;
        _reconnect = true;
    }

    // --- Units menu handlers ---

    private void OnSelectFahrenheit(object? sender, EventArgs e)
    {
        _useFahrenheit = true;
        _fahrenheitItem.Checked = true;
        _celsiusItem.Checked = false;
        UpdateTempDisplay();
    }

    private void OnSelectCelsius(object? sender, EventArgs e)
    {
        _useFahrenheit = false;
        _fahrenheitItem.Checked = false;
        _celsiusItem.Checked = true;
        UpdateTempDisplay();
    }

    // --- Layout ---

    private void CenterControls()
    {
        _tempLabel.Left = (ClientSize.Width - _tempLabel.Width) / 2;
        _tempLabel.Top = (ClientSize.Height - _tempLabel.Height) / 2;
    }

    // --- Display helpers ---

    private void UpdateTempDisplay()
    {
        if (!_hasReading)
            _tempLabel.Text = _useFahrenheit ? "--- °F" : "--- °C";
        else if (_useFahrenheit)
            _tempLabel.Text = $"{_lastTempC * 9.0 / 5.0 + 32.0:F1} °F";
        else
            _tempLabel.Text = $"{_lastTempC:F1} °C";
    }

    private void SetStatus(string text)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetStatus(text));
            return;
        }
        _statusLabel.Text = text;
    }

    // --- Background polling ---

    private void PollLoop()
    {
        Thermometer? therm = null;
        string? connectedPort = null;

        while (_running)
        {
            // Disconnect if user changed port selection
            if (_reconnect && therm != null)
            {
                therm.Close();
                therm = null;
                _reconnect = false;
                Invoke(() =>
                {
                    _hasReading = false;
                    UpdateTempDisplay();
                });
            }
            _reconnect = false;

            if (therm == null)
            {
                string? forced = _selectedPort;

                if (forced != null)
                {
                    // Connect to a specific port
                    SetStatus($"Connecting to {forced}...");
                    var t = new Thermometer(forced);
                    try
                    {
                        t.Open();
                        t.Detect();
                        therm = t;
                        connectedPort = forced;
                    }
                    catch (Exception ex)
                    {
                        t.Close();
                        SetStatus($"{forced}: {ex.Message}");
                        Thread.Sleep(3000);
                        continue;
                    }
                }
                else
                {
                    // Auto scan all ports
                    SetStatus("Searching for device...");

                    string[] ports = SerialPort.GetPortNames();
                    if (ports.Length == 0)
                    {
                        SetStatus("No serial ports found");
                        Thread.Sleep(3000);
                        continue;
                    }

                    foreach (string p in ports)
                    {
                        var t = new Thermometer(p);
                        try
                        {
                            t.Open();
                            t.Detect();
                            therm = t;
                            connectedPort = p;
                            break;
                        }
                        catch
                        {
                            t.Close();
                        }
                    }

                    if (therm == null)
                    {
                        SetStatus($"No device found ({ports.Length} ports scanned)");
                        Thread.Sleep(3000);
                        continue;
                    }
                }
            }

            try
            {
                double tempC = therm.Temperature();
                Invoke(() =>
                {
                    _lastTempC = tempC;
                    _hasReading = true;
                    UpdateTempDisplay();
                });
                SetStatus($"Connected: {connectedPort}");
            }
            catch (Exception ex)
            {
                SetStatus($"Error: {ex.Message}");
                therm.Close();
                therm = null;
                Invoke(() =>
                {
                    _hasReading = false;
                    UpdateTempDisplay();
                });
                Thread.Sleep(2000);
                continue;
            }

            Thread.Sleep(10000);
        }

        therm?.Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _running = false;
        base.OnFormClosed(e);
    }
}
