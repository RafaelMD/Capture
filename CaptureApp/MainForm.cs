using System;
using System.IO;
using System.Windows.Forms;

namespace CaptureApp
{
    public partial class MainForm : Form
    {
        private AudioRecorder? _audioRecorder;
        private string _currentRecordingFile = string.Empty;

        public MainForm()
        {
            InitializeComponent();
            cmbLanguage.SelectedIndex = 0; // Default to English
            
            // Set up notify icon
            notifyIcon.Icon = SystemIcons.Application;
        }

        private void btnRecord_Click(object? sender, EventArgs e)
        {
            if (_audioRecorder == null || !_audioRecorder.IsRecording)
            {
                StartRecording();
            }
            else
            {
                StopRecording();
            }
        }

        private void StartRecording()
        {
            try
            {
                // Create output directory if it doesn't exist
                string outputDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "CaptureRecordings");
                
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // Generate filename with timestamp
                string fileName = $"Recording_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
                _currentRecordingFile = Path.Combine(outputDir, fileName);

                // Initialize recorder if needed
                if (_audioRecorder == null)
                {
                    _audioRecorder = new AudioRecorder();
                    _audioRecorder.RecordingStarted += OnRecordingStarted;
                    _audioRecorder.RecordingStopped += OnRecordingStopped;
                    _audioRecorder.ErrorOccurred += OnRecordingError;
                }

                // Start recording on a background thread (NAudio handles this)
                _audioRecorder.StartRecording(_currentRecordingFile);

                // Update UI
                btnRecord.Text = "Stop Recording";
                btnRecord.BackColor = System.Drawing.Color.IndianRed;
                lblStatus.Text = "Status: Recording...";
                lblStatus.ForeColor = System.Drawing.Color.Red;
                cmbLanguage.Enabled = false;

                // Show notification icon
                notifyIcon.Visible = true;
                notifyIcon.ShowBalloonTip(2000, "Recording Started", 
                    "Audio recording is in progress. You can minimize this window.", 
                    ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start recording: {ex.Message}", 
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopRecording()
        {
            try
            {
                if (_audioRecorder != null)
                {
                    // Stop recording (runs on background thread)
                    _audioRecorder.StopRecording();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error stopping recording: {ex.Message}", 
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnRecordingStarted(object? sender, EventArgs e)
        {
            // This event comes from a background thread, so invoke on UI thread
            if (InvokeRequired)
            {
                Invoke(new EventHandler(OnRecordingStarted), sender, e);
                return;
            }
        }

        private void OnRecordingStopped(object? sender, EventArgs e)
        {
            // This event comes from a background thread, so invoke on UI thread
            if (InvokeRequired)
            {
                Invoke(new EventHandler(OnRecordingStopped), sender, e);
                return;
            }

            // Update UI
            btnRecord.Text = "Start Recording";
            btnRecord.BackColor = System.Drawing.SystemColors.Control;
            lblStatus.Text = $"Status: Recording saved to {Path.GetFileName(_currentRecordingFile)}";
            lblStatus.ForeColor = System.Drawing.Color.Green;
            cmbLanguage.Enabled = true;

            // Hide notification icon
            notifyIcon.Visible = false;

            MessageBox.Show($"Recording saved to:\n{_currentRecordingFile}\n\nYou can now proceed with transcription.", 
                "Recording Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnRecordingError(object? sender, string error)
        {
            // This event comes from a background thread, so invoke on UI thread
            if (InvokeRequired)
            {
                Invoke(new EventHandler<string>(OnRecordingError), sender, error);
                return;
            }

            lblStatus.Text = "Status: Error occurred";
            lblStatus.ForeColor = System.Drawing.Color.Red;
            MessageBox.Show(error, "Recording Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void MainForm_Resize(object? sender, EventArgs e)
        {
            // When minimized, hide form and show notification icon
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                if (_audioRecorder != null && _audioRecorder.IsRecording)
                {
                    notifyIcon.ShowBalloonTip(1000, "Still Recording", 
                        "Recording continues in background. Double-click to restore.", 
                        ToolTipIcon.Info);
                }
            }
        }

        private void notifyIcon_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            // Restore window when clicking on notification icon
            Show();
            WindowState = FormWindowState.Normal;
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // Warn user if recording is in progress
            if (_audioRecorder != null && _audioRecorder.IsRecording)
            {
                var result = MessageBox.Show(
                    "Recording is still in progress. Are you sure you want to exit?", 
                    "Confirm Exit", 
                    MessageBoxButtons.YesNo, 
                    MessageBoxIcon.Warning);

                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }

                // Stop recording before closing
                _audioRecorder.StopRecording();
            }

            // Cleanup
            _audioRecorder?.Dispose();
            notifyIcon.Dispose();
        }
    }
}
