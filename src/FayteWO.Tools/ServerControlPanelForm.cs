using System.Diagnostics;

namespace FayteWO.Tools;

public sealed class ServerControlPanelForm : Form
{
    private const int MaxLogCharacters = 120_000;

    private readonly Button _startServerButton;
    private readonly Button _stopServerButton;
    private readonly Button _helpButton;
    private readonly Button _refreshStatusButton;
    private readonly Button _launchClientButton;
    private readonly Button _stopClientsButton;
    private readonly Button _sendCommandButton;
    private readonly Button _sendAnnouncementButton;
    private readonly CheckBox _showVerboseLogsCheckBox;

    private readonly TextBox _serverLogTextBox;
    private readonly TextBox _commandTextBox;
    private readonly TextBox _announcementTextBox;

    private readonly ListBox _connectedPlayersListBox;
    private readonly ListBox _activeSessionsListBox;

    private readonly Label _serverStatusLabel;
    private readonly Label _playerCountLabel;
    private readonly Label _sessionCountLabel;

    private readonly System.Windows.Forms.Timer _statusRefreshTimer;

    private readonly List<string> _pendingPlayerStatusLines = new();
    private readonly List<string> _pendingSessionStatusLines = new();

    private Process? _serverProcess;
    private bool _isReadingGuiStatus;
    private bool _showVerboseLogs;

    public ServerControlPanelForm()
    {
        Text = "FayteWO Server Control Panel";
        Width = 1250;
        Height = 850;
        StartPosition = FormStartPosition.CenterScreen;

        _startServerButton = new Button
        {
            Text = "Start Server",
            Width = 120,
            Height = 32,
            Left = 10,
            Top = 10
        };

        _stopServerButton = new Button
        {
            Text = "Stop Server",
            Width = 120,
            Height = 32,
            Left = 140,
            Top = 10,
            Enabled = false
        };

        _helpButton = new Button
        {
            Text = "Help",
            Width = 80,
            Height = 32,
            Left = 270,
            Top = 10,
            Enabled = false
        };

        _refreshStatusButton = new Button
        {
            Text = "Refresh Status",
            Width = 120,
            Height = 32,
            Left = 360,
            Top = 10,
            Enabled = false
        };

        _launchClientButton = new Button
        {
            Text = "Launch Client",
            Width = 120,
            Height = 32,
            Left = 490,
            Top = 10,
            Enabled = false
        };

        _stopClientsButton = new Button
        {
            Text = "Stop Clients",
            Width = 120,
            Height = 32,
            Left = 620,
            Top = 10,
            Enabled = false
        };

        _showVerboseLogsCheckBox = new CheckBox
        {
            Text = "Show verbose packet logs",
            AutoSize = true,
            Left = 760,
            Top = 16,
            Checked = false
        };

        _serverStatusLabel = new Label
        {
            Text = "Server Status: Stopped",
            AutoSize = true,
            Left = 980,
            Top = 17
        };

        _serverLogTextBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Left = 10,
            Top = 55,
            Width = 800,
            Height = 650,
            Font = new Font(FontFamily.GenericMonospace, 9)
        };

        Label playersLabel = new Label
        {
            Text = "Connected Players",
            AutoSize = true,
            Left = 835,
            Top = 55
        };

        _playerCountLabel = new Label
        {
            Text = "Players: 0",
            AutoSize = true,
            Left = 1070,
            Top = 55
        };

        _connectedPlayersListBox = new ListBox
        {
            Left = 835,
            Top = 80,
            Width = 380,
            Height = 220,
            Font = new Font(FontFamily.GenericMonospace, 9)
        };

        Label sessionsLabel = new Label
        {
            Text = "Active Sessions",
            AutoSize = true,
            Left = 835,
            Top = 315
        };

        _sessionCountLabel = new Label
        {
            Text = "Sessions: 0",
            AutoSize = true,
            Left = 1070,
            Top = 315
        };

        _activeSessionsListBox = new ListBox
        {
            Left = 835,
            Top = 340,
            Width = 380,
            Height = 180,
            Font = new Font(FontFamily.GenericMonospace, 9)
        };

        Label announcementLabel = new Label
        {
            Text = "Announcement",
            AutoSize = true,
            Left = 835,
            Top = 540
        };

        _announcementTextBox = new TextBox
        {
            Left = 835,
            Top = 565,
            Width = 380,
            Height = 26
        };

        _sendAnnouncementButton = new Button
        {
            Text = "Send Announcement",
            Left = 835,
            Top = 600,
            Width = 380,
            Height = 32,
            Enabled = false
        };

        Label commandLabel = new Label
        {
            Text = "Raw Server Command",
            AutoSize = true,
            Left = 10,
            Top = 725
        };

        _commandTextBox = new TextBox
        {
            Left = 10,
            Top = 750,
            Width = 685,
            Height = 26
        };

        _sendCommandButton = new Button
        {
            Text = "Send Command",
            Left = 705,
            Top = 748,
            Width = 105,
            Height = 32,
            Enabled = false
        };

        _statusRefreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 2000
        };

        Controls.Add(_startServerButton);
        Controls.Add(_stopServerButton);
        Controls.Add(_helpButton);
        Controls.Add(_refreshStatusButton);
        Controls.Add(_launchClientButton);
        Controls.Add(_stopClientsButton);
        Controls.Add(_showVerboseLogsCheckBox);
        Controls.Add(_serverStatusLabel);
        Controls.Add(_serverLogTextBox);
        Controls.Add(playersLabel);
        Controls.Add(_playerCountLabel);
        Controls.Add(_connectedPlayersListBox);
        Controls.Add(sessionsLabel);
        Controls.Add(_sessionCountLabel);
        Controls.Add(_activeSessionsListBox);
        Controls.Add(announcementLabel);
        Controls.Add(_announcementTextBox);
        Controls.Add(_sendAnnouncementButton);
        Controls.Add(commandLabel);
        Controls.Add(_commandTextBox);
        Controls.Add(_sendCommandButton);

        _startServerButton.Click += (_, _) => StartServer();
        _stopServerButton.Click += (_, _) => StopServer();
        _helpButton.Click += (_, _) => SendServerCommand("help");
        _refreshStatusButton.Click += (_, _) => RequestGuiStatus();
        _launchClientButton.Click += (_, _) => LaunchClient();
        _stopClientsButton.Click += (_, _) => StopClients();
        _sendCommandButton.Click += (_, _) => SendRawCommandFromTextBox();
        _sendAnnouncementButton.Click += (_, _) => SendAnnouncement();
        _commandTextBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SendRawCommandFromTextBox();
            }
        };

        _announcementTextBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SendAnnouncement();
            }
        };

        _showVerboseLogsCheckBox.CheckedChanged += (_, _) =>
        {
            _showVerboseLogs = _showVerboseLogsCheckBox.Checked;
        };

        _statusRefreshTimer.Tick += (_, _) => RequestGuiStatus();

        FormClosing += (_, _) => StopServer();
    }

    

    private void StartServer()
    {
        if (_serverProcess is not null && !_serverProcess.HasExited)
        {
            AppendLog("Server is already running.");
            return;
        }

        string? repoRoot = FindRepoRoot();

        if (repoRoot is null)
        {
            AppendLog("Could not find repo root. Expected to find src/FayteWO.Server/FayteWO.Server.csproj.");
            return;
        }

        string serverProjectPath = Path.Combine(repoRoot, "src", "FayteWO.Server", "FayteWO.Server.csproj");

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{serverProjectPath}\"",
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true
        };

        _serverProcess = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        _serverProcess.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            HandleServerOutputLine(e.Data);
        };

        _serverProcess.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            AppendLog("[ERR] " + e.Data);
        };

        _serverProcess.Exited += (_, _) =>
        {
            BeginInvoke(new Action(() =>
            {
                AppendLog("Server process exited.");
                SetServerRunningState(false);
            }));
        };

        try
        {
            _serverProcess.Start();
            _serverProcess.BeginOutputReadLine();
            _serverProcess.BeginErrorReadLine();

            SetServerRunningState(true);
            AppendLog("Started server process.");

            RequestGuiStatus();
        }
        catch (Exception ex)
        {
            AppendLog($"Failed to start server: {ex.Message}");
            SetServerRunningState(false);
        }
    }

    private void LaunchClient()
    {
        string? repoRoot = FindRepoRoot();

        if (repoRoot is null)
        {
            AppendLog("Could not find repo root. Expected to find src/FayteWO.Client/FayteWO.Client.csproj.");
            return;
        }

        string clientExecutablePath = Path.Combine(
            repoRoot,
            "src",
            "FayteWO.Client",
            "bin",
            "Debug",
            "net9.0-windows",
            "FayteWO.Client.exe");

        if (!File.Exists(clientExecutablePath))
        {
            AppendLog("Client executable was not found.");
            AppendLog($"Expected path: {clientExecutablePath}");
            AppendLog("Run 'dotnet build' first, then try Launch Client again.");
            return;
        }

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = clientExecutablePath,
            WorkingDirectory = Path.GetDirectoryName(clientExecutablePath) ?? repoRoot,
            UseShellExecute = true
        };

        try
        {
            Process.Start(startInfo);
            AppendLog("Launched FayteWO client.");
        }
        catch (Exception ex)
        {
            AppendLog($"Failed to launch client: {ex.Message}");
        }
    }
    private void StopClients()
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = "/IM FayteWO.Client.exe /F",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using Process process = Process.Start(startInfo)!;

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(output))
            {
                AppendLog(output.Trim());
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                AppendLog(error.Trim());
            }

            AppendLog("Requested all FayteWO client processes to stop.");
        }
        catch (Exception ex)
        {
            AppendLog($"Failed to stop clients: {ex.Message}");
        }
    }

    private void StopServer()
    {
        _statusRefreshTimer.Stop();

        if (_serverProcess is null)
        {
            SetServerRunningState(false);
            return;
        }

        try
        {
            if (!_serverProcess.HasExited)
            {
                AppendLog("Stopping server...");

                try
                {
                    SendServerCommand("quit");
                    _serverProcess.WaitForExit(2000);
                }
                catch
                {
                    // If graceful stop fails, kill the process below.
                }

                if (!_serverProcess.HasExited)
                {
                    _serverProcess.Kill(entireProcessTree: true);
                    _serverProcess.WaitForExit(2000);
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Failed while stopping server: {ex.Message}");
        }
        finally
        {
            _serverProcess.Dispose();
            _serverProcess = null;

            _connectedPlayersListBox.Items.Clear();
            _activeSessionsListBox.Items.Clear();
            _playerCountLabel.Text = "Players: 0";
            _sessionCountLabel.Text = "Sessions: 0";

            SetServerRunningState(false);
        }
    }

    private void RequestGuiStatus()
    {
        if (_serverProcess is null || _serverProcess.HasExited)
        {
            return;
        }

        SendServerCommand("guistatus", echoToLog: false);
    }

    private void SendRawCommandFromTextBox()
    {
        string command = _commandTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        SendServerCommand(command);
        _commandTextBox.Clear();
    }

    private void SendAnnouncement()
    {
        string announcement = _announcementTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(announcement))
        {
            return;
        }

        SendServerCommand($"announce {announcement}");
        _announcementTextBox.Clear();
    }

    private void SendServerCommand(string command, bool echoToLog = true)
    {
        if (_serverProcess is null || _serverProcess.HasExited)
        {
            AppendLog("Cannot send command. Server is not running.");
            return;
        }

        try
        {
            if (echoToLog)
            {
                AppendLog($"> {command}");
            }

            _serverProcess.StandardInput.WriteLine(command);
            _serverProcess.StandardInput.Flush();
        }
        catch (Exception ex)
        {
            AppendLog($"Failed to send command: {ex.Message}");
        }
    }

    private bool TryConsumeGuiStatusLine(string line)
    {
        if (line == "GUI_STATUS_BEGIN")
        {
            _pendingPlayerStatusLines.Clear();
            _pendingSessionStatusLines.Clear();
            _isReadingGuiStatus = true;
            return true;
        }

        if (line == "GUI_STATUS_END")
        {
            _isReadingGuiStatus = false;

            BeginInvoke(new Action(() =>
            {
                ApplyGuiStatus();
            }));

            return true;
        }

        if (!_isReadingGuiStatus)
        {
            return false;
        }

        if (line.StartsWith("PLAYER|", StringComparison.Ordinal))
        {
            _pendingPlayerStatusLines.Add(line);
            return true;
        }

        if (line.StartsWith("SESSION|", StringComparison.Ordinal))
        {
            _pendingSessionStatusLines.Add(line);
            return true;
        }

        if (line.StartsWith("PLAYERS|", StringComparison.Ordinal) ||
            line.StartsWith("SESSIONS|", StringComparison.Ordinal))
        {
            return true;
        }

        return true;
    }

    private void ApplyGuiStatus()
    {
        _connectedPlayersListBox.BeginUpdate();
        _activeSessionsListBox.BeginUpdate();

        try
        {
            _connectedPlayersListBox.Items.Clear();
            _activeSessionsListBox.Items.Clear();

            foreach (string playerLine in _pendingPlayerStatusLines)
            {
                string[] parts = playerLine.Split('|');

                if (parts.Length < 6)
                {
                    continue;
                }

                string name = UnescapeGuiStatusValue(parts[1]);
                string playerId = parts[2];
                string x = parts[3];
                string y = parts[4];
                string z = parts[5];

                _connectedPlayersListBox.Items.Add($"{name} | ({x}, {y}, {z}) | {ShortenId(playerId)}");
            }

            foreach (string sessionLine in _pendingSessionStatusLines)
            {
                string[] parts = sessionLine.Split('|');

                if (parts.Length < 4)
                {
                    continue;
                }

                string sessionId = parts[1];
                string playerId = string.IsNullOrWhiteSpace(parts[2]) ? "not logged in" : ShortenId(parts[2]);
                string loggedIn = parts[3];

                _activeSessionsListBox.Items.Add($"{ShortenId(sessionId)} | LoggedIn={loggedIn} | Player={playerId}");
            }

            _playerCountLabel.Text = $"Players: {_connectedPlayersListBox.Items.Count}";
            _sessionCountLabel.Text = $"Sessions: {_activeSessionsListBox.Items.Count}";
        }
        finally
        {
            _connectedPlayersListBox.EndUpdate();
            _activeSessionsListBox.EndUpdate();
        }
    }

    private void SetServerRunningState(bool isRunning)
    {
        _startServerButton.Enabled = !isRunning;
        _stopServerButton.Enabled = isRunning;
        _helpButton.Enabled = isRunning;
        _refreshStatusButton.Enabled = isRunning;
        _launchClientButton.Enabled = isRunning;
        _stopClientsButton.Enabled = true;
        _sendCommandButton.Enabled = isRunning;
        _sendAnnouncementButton.Enabled = isRunning;

        if (isRunning)
        {
            _statusRefreshTimer.Start();
        }
        else
        {
            _statusRefreshTimer.Stop();
        }

        _serverStatusLabel.Text = isRunning
            ? "Server Status: Running"
            : "Server Status: Stopped";
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => AppendLog(message)));
            return;
        }

        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        _serverLogTextBox.AppendText($"[{timestamp}] {message}{Environment.NewLine}");

        TrimLogIfNeeded();
    }

    private void TrimLogIfNeeded()
    {
        if (_serverLogTextBox.TextLength <= MaxLogCharacters)
        {
            return;
        }

        int removeLength = MaxLogCharacters / 3;
        _serverLogTextBox.Text = _serverLogTextBox.Text[removeLength..];
        _serverLogTextBox.SelectionStart = _serverLogTextBox.TextLength;
        _serverLogTextBox.ScrollToCaret();
    }

    private static string UnescapeGuiStatusValue(string value)
    {
        return value
            .Replace("\\p", "|")
            .Replace("\\\\", "\\");
    }

    private static string ShortenId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return value.Length <= 8
            ? value
            : value[..8];
    }

    private static string? FindRepoRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string expectedServerProject = Path.Combine(
                directory.FullName,
                "src",
                "FayteWO.Server",
                "FayteWO.Server.csproj");

            if (File.Exists(expectedServerProject))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private void HandleServerOutputLine(string line)
    {
        if (TryConsumeGuiStatusLine(line))
        {
            return;
        }

        if (!ShouldDisplayLogLine(line))
        {
            return;
        }

        AppendLog(line);
    }

    private bool ShouldDisplayLogLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        if (_showVerboseLogs)
        {
            return true;
        }

        // GUI status lines are meant for the right-side panels, not the log box.
        if (line == "GUI_STATUS_BEGIN" ||
            line == "GUI_STATUS_END" ||
            line.StartsWith("PLAYERS|", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("PLAYER|", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("SESSIONS|", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("SESSION|", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (line.StartsWith("server>", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (line.Contains("Active sessions:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (line.Contains("Active players:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (line.Contains("Connected players:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (line.Contains("Total sessions:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (line.Contains("Total players:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (line.Contains("Waiting for clients", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (line.Contains("Listening for clients", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (line.Contains("Received raw packet:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (line.Contains("Sending packet:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (line.Contains("Decoded ChunkRequest", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (line.Contains("\"TileIds\"", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}