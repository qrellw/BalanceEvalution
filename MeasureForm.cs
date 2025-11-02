using System;
using System.Data;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using Microsoft.Data.SqlClient;
using System.Drawing.Imaging;
using System.Globalization;
using ClosedXML.Excel;
using System.Net.NetworkInformation; // để chọn IP chính
using System.Text.RegularExpressions; // cho parser linh hoạt

namespace BalanceApp
{
    public partial class MeasureForm : Form
    {
    private readonly string _connStr;
    private readonly int _selectedPatientId = 1;
    private readonly string _patientName = string.Empty;
    private readonly string _patientGender = string.Empty;
    private readonly string _patientDob = string.Empty;

        private TcpListener? _tcpServer;
        private Thread? _listenerThread;
        private volatile bool _isListening;

        private readonly DataTable _dataTable = new();
        private readonly System.Collections.Generic.List<(DateTime t, double x, double y, string stance)> _allSamples = new(); // lưu toàn bộ để xuất Excel đầy đủ
        private System.Windows.Forms.Timer? _measurementTimer; // đếm ngược 30s
        private int _secondsRemaining = 0;
        private Label? _lblCountdown;
        private bool _measurementFinished = false; // hoàn tất 30s
        private DateTime _sessionStartTime = DateTime.MinValue;
        private const int TARGET_TOTAL_SAMPLES = 300; // thu tổng 300 mẫu
        private const int EXPORT_SAMPLES = 200;       // xuất 200 mẫu cuối (sau loại bỏ đầu)
        private const int MIN_TRIM_SECONDS = 10;      // bỏ tối thiểu 10s đầu
        private const int MAX_TRIM_SECONDS = 15;      // tối đa 15s nếu đủ mẫu
        private double _sumX = 0;
        private double _sumY = 0;
        private int _count = 0;
        private string? _fixedIp; // IP được chọn cố định để hiển thị cho ESP32
    private int? _currentTestDateId; // TestDateID cho phiên đo hiện tại (dùng chung BMI + thông số COP)
        private int _zeroStreak = 0; // đếm số mẫu liên tiếp (0,0)
        private const int ZERO_STREAK_WARNING = 80; // ~80 mẫu (tùy tốc độ gửi) sẽ cảnh báo
        // Hiển thị lực loadcell
        private Label? _lblF1, _lblF2, _lblF3, _lblF4, _lblFtot;
        private double _lastF1, _lastF2, _lastF3, _lastF4;
    private int _extendedParseFailCount = 0; // đếm lần fail để debug
    // Lưu điểm cuối đã vẽ để lọc trùng (tránh hàng loạt (0,0) tạo cảm giác "trôi sang phải")
    private double? _lastPlottedX;
    private double? _lastPlottedY;
    private Label? _lblStatus;           // UI status label
    private DateTime _lastDataTime = DateTime.MinValue;
    private System.Windows.Forms.Timer? _statusTimer;
    private Button? _btnStart;
    private Button? _btnStop;
    private CheckBox? _chkAutoStart;
    private Button? _btnReset;
    private TcpClient? _currentClient; // client connection to send commands back
    private DataGridView? _grid;       // realtime data grid
    private const int MAX_ROWS_DISPLAY = 1000; // limit to keep UI responsive
    // Đã bỏ nhãn Samples để dành chỗ cho thông tin bệnh nhân
    private TextBox? _txtDebug;        // debug raw lines
    private Button? _btnInject;        // inject test data
    private Button? _btnExport;        // export excel button
    // Removed overlay patient panel; using designer group boxes/fields.
    private Panel? _rightPanel;        // panel chứa status + info + debug
    private Label? _lblPatientInfo;    // hiển thị thông tin bệnh nhân
    private Panel? _patientInfoArea;   // panel tóm tắt bệnh nhân dưới biểu đồ
    // BMI controls
    private TextBox? _txtHeight; // cm
    private TextBox? _txtWeight; // kg
    private Button? _btnBMI;
    private Label? _lblBMIResult;
    private Button? _btnSaveBMI; // nút lưu BMI vào CSDL
    private ComboBox? _cmbStance; // chọn tư thế: mở chân / đóng chân
    // Đã bỏ nút đổi theme, luôn dùng giao diện nền đen

        // Parameterless constructor for Designer
        public MeasureForm() : this(string.Empty, 1, "", "", "") { }

        public MeasureForm(string connectionString, int patientId, string patientName, string patientGender, string patientDob)
        {
            InitializeComponent();
            _connStr = connectionString;
            _selectedPatientId = patientId;
            _patientName = patientName;
            _patientGender = patientGender;
            _patientDob = patientDob;
            InitChart(); // luôn dark theme
            InitTable();
            InitStatusLabel();
            InitStatusTimer();
            InitControlButtons();
            InitAutoStartOption();
            InitGrid();
            InitDebugPanel();
            InitPatientInfoPanel();
            this.Resize += (_, __) => RepositionPatientInfoPanel();
            SetStatus("Nhấn Start để bắt đầu", System.Drawing.Color.DarkOrange);
            // Không tự động bắt đầu đo khi form hiển thị
        }

        private void InitDebugPanel()
        {
            // Tạo panel bên phải (nếu chưa có)
            if (_rightPanel == null)
            {
                _rightPanel = new Panel
                {
                    Dock = DockStyle.Right,
                    Width = 260,
                    BackColor = System.Drawing.Color.Black
                };
                Controls.Add(_rightPanel);
                _rightPanel.BringToFront();
            }

            // Di chuyển status label vào panel (nếu đã tạo trước đó)
            if (_lblStatus != null)
            {
                _rightPanel.Controls.Add(_lblStatus);
                _lblStatus.Left = 6;
                _lblStatus.Top = 6;
                _lblStatus.ForeColor = System.Drawing.Color.DeepSkyBlue;
                _lblStatus.BackColor = System.Drawing.Color.Black;
            }

            // Thêm label lực trước khi thêm hộp debug
            AddForceLabels();

            // Không gắn label bệnh nhân vào panel phải nữa; sẽ hiển thị ở panel dưới Auto Start
            if (_lblPatientInfo == null)
            {
                _lblPatientInfo = new Label
                {
                    AutoSize = false,
                    Width = 180,
                    Height = 70,
                    ForeColor = System.Drawing.Color.Black,
                    BackColor = System.Drawing.Color.WhiteSmoke,
                    Font = new System.Drawing.Font("Segoe UI", 8F),
                    Text = ""
                };
            }

            _txtDebug = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BackColor = System.Drawing.Color.Black,
                ForeColor = System.Drawing.Color.LightGreen,
                Font = new System.Drawing.Font("Consolas", 8F)
            };
            if (_rightPanel != null) _rightPanel.Controls.Add(_txtDebug);
            // Không BringToFront để khỏi che các label lực; thay vào đó đưa label lực lên trước
            if (_lblF1 != null)
            {
                _lblF1.BringToFront();
                _lblF2?.BringToFront();
                _lblF3?.BringToFront();
                _lblF4?.BringToFront();
                _lblFtot?.BringToFront();
            }

            _btnInject = new Button
            {
                Text = "Inject 10",
                Width = 80,
                Height = 24,
                Top = 5,
                Left = (_btnReset?.Right ?? 300) + 10,
                BackColor = System.Drawing.Color.DarkSlateBlue,
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnInject.FlatAppearance.BorderSize = 0;
            _btnInject.Click += (s, e) => InjectRandomSamples(10);
            Controls.Add(_btnInject);
            _btnInject.BringToFront();
        }

        private void InitPatientInfoPanel()
        {
            try
            {
                if (Controls.Find("txtId", true) is { Length: > 0 } idArr && idArr[0] is TextBox tbId) tbId.Text = _selectedPatientId.ToString();
                if (Controls.Find("txtName", true) is { Length: > 0 } nameArr && nameArr[0] is TextBox tbName) tbName.Text = _patientName;
                if (Controls.Find("cbGender", true) is { Length: > 0 } genderArr && genderArr[0] is ComboBox cbG)
                {
                    if (!string.IsNullOrWhiteSpace(_patientGender) && cbG.Items.Contains(_patientGender)) cbG.SelectedItem = _patientGender;
                }
                if (Controls.Find("dtpBirth", true) is { Length: > 0 } dobArr && dobArr[0] is DateTimePicker dtp && DateTime.TryParse(_patientDob, out var parsed)) dtp.Value = parsed;
                UpdatePatientInfoDisplay();
                if (_patientInfoArea == null)
                {
                    int left = _chkAutoStart != null ? _chkAutoStart.Left : (chart1?.Left ?? 10);
                    int top = (_chkAutoStart != null ? _chkAutoStart.Bottom : (chart1?.Top ?? 10)) + 6;
                    _patientInfoArea = new Panel
                    {
                        Left = left,
                        Top = top,
                        Width = 260, // tăng rộng để không che nút Lưu
                        Height = 170,
                        BackColor = System.Drawing.Color.WhiteSmoke,
                        BorderStyle = BorderStyle.FixedSingle
                    };
                    Controls.Add(_patientInfoArea);
                }
                if (_patientInfoArea != null && _lblPatientInfo != null)
                {
                    if (_lblPatientInfo.Parent != _patientInfoArea)
                    {
                        _rightPanel?.Controls.Remove(_lblPatientInfo);
                        _patientInfoArea.Controls.Add(_lblPatientInfo);
                    }
                    _lblPatientInfo.Left = 4;
                    _lblPatientInfo.Top = 4;
                    _lblPatientInfo.Width = _patientInfoArea.Width - 8;
                    _lblPatientInfo.Height = 72;
                    InitBMIControls();
                }
            }
            catch { }
        }

        private void AppendDebug(string msg)
        {
            if (_txtDebug == null) return;
            if (!_txtDebug.IsHandleCreated)
            {
                _txtDebug.Text = msg + "\r\n" + _txtDebug.Text;
                return;
            }
            if (_txtDebug.InvokeRequired)
            {
                try { _txtDebug.BeginInvoke((MethodInvoker)(() => AppendDebug(msg))); } catch { }
            }
            else
            {
                var lines = _txtDebug.Lines;
                if (lines.Length > 100)
                    _txtDebug.Lines = lines[..100];
                _txtDebug.Text = msg + "\r\n" + _txtDebug.Text;
            }
        }

        private void InjectRandomSamples(int count)
        {
            var rand = new Random();
            for (int i = 0; i < count; i++)
            {
                double x = Math.Round(rand.NextDouble() * 60 - 30, 2);
                double y = Math.Round(rand.NextDouble() * 60 - 30, 2);
                AddSample(x, y);
            }
            AppendDebug($"[INJECT] {count} samples");
        }

        private void RepositionPatientInfoPanel()
        {
            if (_patientInfoArea == null) return;
            int left = _chkAutoStart != null ? _chkAutoStart.Left : (chart1?.Left ?? 10);
            int top = (_chkAutoStart != null ? _chkAutoStart.Bottom : (chart1?.Top ?? 10)) + 6;
            _patientInfoArea.Left = left;
            _patientInfoArea.Top = top;
        }

        private void InitBMIControls()
        {
            if (_patientInfoArea == null) return;
            if (_txtHeight != null) return; // already created

            // Label chiều cao
            var lblH = new Label
            {
                Text = "Cao (cm)" ,
                Left = 6,
                Top = _lblPatientInfo!.Bottom + 4,
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 8F)
            };
            _patientInfoArea.Controls.Add(lblH);

            _txtHeight = new TextBox
            {
                Left = 70,
                Top = lblH.Top - 2,
                Width = 50,
                Font = new System.Drawing.Font("Segoe UI", 8F)
            };
            _patientInfoArea.Controls.Add(_txtHeight);

            // Label cân nặng
            var lblW = new Label
            {
                Text = "Nặng (kg)",
                Left = 6,
                Top = lblH.Top + 24,
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 8F)
            };
            _patientInfoArea.Controls.Add(lblW);

            _txtWeight = new TextBox
            {
                Left = 70,
                Top = lblW.Top - 2,
                Width = 50,
                Font = new System.Drawing.Font("Segoe UI", 8F)
            };
            _patientInfoArea.Controls.Add(_txtWeight);

            _btnBMI = new Button
            {
                Text = "BMI",
                Left = 130,
                Top = lblH.Top,
                Width = 55,
                Height = 44,
                BackColor = System.Drawing.Color.LightSteelBlue,
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold)
            };
            _btnBMI.FlatAppearance.BorderSize = 0;
            _btnBMI.Click += (s, e) => CalculateBMI();
            _patientInfoArea.Controls.Add(_btnBMI);

            _lblBMIResult = new Label
            {
                Text = "",
                Left = 6,
                Top = lblW.Top + 26,
                Width = _patientInfoArea.Width - 12,
                Height = 34,
                Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Italic),
                ForeColor = System.Drawing.Color.DimGray
            };
            _patientInfoArea.Controls.Add(_lblBMIResult);

            _btnSaveBMI = new Button
            {
                Text = "Lưu",
                Left = _btnBMI.Left + _btnBMI.Width + 6,
                Top = _btnBMI.Top,
                Width = 50,
                Height = 44,
                BackColor = System.Drawing.Color.SteelBlue,
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold)
            };
            _btnSaveBMI.FlatAppearance.BorderSize = 0;
            _btnSaveBMI.Click += (s, e) => SaveBMIToDatabase();
            _patientInfoArea.Controls.Add(_btnSaveBMI);
        }

        private void CalculateBMI()
        {
            try
            {
                if (_txtHeight == null || _txtWeight == null) return;
                if (!double.TryParse(_txtHeight.Text.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double hCm) || hCm <= 0)
                {
                    MessageBox.Show("Chiều cao không hợp lệ", "BMI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (!double.TryParse(_txtWeight.Text.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double wKg) || wKg <= 0)
                {
                    MessageBox.Show("Cân nặng không hợp lệ", "BMI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                double hM = hCm / 100.0;
                double bmi = wKg / (hM * hM);
                string category = GetBmiCategory(bmi);
                string msg = $"BMI: {bmi:F1}\r\n{category}";
                _lblBMIResult!.Text = msg;
                MessageBox.Show(category, "Tình trạng dinh dưỡng", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi tính BMI: " + ex.Message);
            }
        }


        private static string GetBmiCategory(double bmi)
        {
            // Phân loại theo ngưỡng châu Á phổ biến
            if (bmi < 18.5) return "Cân nặng thấp (gầy)";
            if (bmi < 23) return "Bình thường";
            if (bmi < 25) return "Thừa cân (tiền béo phì)";
            if (bmi < 30) return "Béo phì độ I";
            return "Béo phì độ II";
        }

        private void UpdatePatientInfoDisplay()
        {
            if (_lblPatientInfo == null) return;
            _lblPatientInfo.Text =
                $"ID: {_selectedPatientId}\r\n" +
                $"Tên: {_patientName}\r\n" +
                $"Giới tính: {_patientGender}\r\n" +
                $"Ngày sinh: {_patientDob}";
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // Không auto start khi mở form
        }

        private void InitAutoStartOption()
        {
            int left = _btnStart?.Left ?? 10;
            int top = (_btnStart?.Bottom ?? 30) + 6; // đặt xuống dưới hàng nút
            _chkAutoStart = new CheckBox
            {
                Text = "Auto Start",
                AutoSize = true,
                Checked = false,
                Top = top,
                Left = left
            };
            Controls.Add(_chkAutoStart);
            _chkAutoStart.BringToFront();
        }

        private void InitControlButtons()
        {
            _btnStart = new Button
            {
                Text = "Chạy",
                Width = 70,
                Height = 28,
                Top = chart1?.Top + 5 ?? 5,
                Left = (chart1?.Left ?? 10) + 5,
                BackColor = System.Drawing.Color.FromArgb(0, 122, 204),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnStart.FlatAppearance.BorderSize = 0;
            _btnStart.Click += (s, e) => StartSession();

            _btnStop = new Button
            {
                Text = "Dừng",
                Width = 70,
                Height = 28,
                Top = _btnStart.Top,
                Left = _btnStart.Left + _btnStart.Width + 8,
                BackColor = System.Drawing.Color.IndianRed,
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            _btnStop.FlatAppearance.BorderSize = 0;
            _btnStop.Click += (s, e) => StopSession();

            Controls.Add(_btnStart);
            Controls.Add(_btnStop);
            _btnStart.BringToFront();
            _btnStop.BringToFront();

            _btnReset = new Button
            {
                Text = "Xóa",
                Width = 70,
                Height = 28,
                Top = _btnStart.Top,
                Left = _btnStop.Left + _btnStop.Width + 8,
                BackColor = System.Drawing.Color.MediumPurple,
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            _btnReset.FlatAppearance.BorderSize = 0;
            _btnReset.Click += (s, e) => SendResetCommand();
            Controls.Add(_btnReset);
            _btnReset.BringToFront();

            _btnExport = new Button
            {
                Text = "Lưu",
                Width = 90,
                Height = 28,
                Top = _btnStart.Top,
                Left = _btnReset.Left + _btnReset.Width + 8,
                BackColor = System.Drawing.Color.SeaGreen,
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnExport.FlatAppearance.BorderSize = 0;
            _btnExport.Click += (s, e) => ExportToExcel();
            Controls.Add(_btnExport);
            _btnExport.BringToFront();
            _btnExport.Enabled = false; // chỉ cho lưu sau khi đo xong 30s

            // Countdown label
            _lblCountdown = new Label
            {
                Text = "--",
                Left = _btnStart.Left,
                Top = _btnStart.Bottom + 4,
                Width = 80,
                Height = 20,
                ForeColor = System.Drawing.Color.Gold,
                Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold),
                Visible = false // ẩn khi chưa nhấn Start
            };
            Controls.Add(_lblCountdown);
            _lblCountdown.BringToFront();

            // ComboBox chọn tư thế
            _cmbStance = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 110,
                Font = new System.Drawing.Font("Segoe UI", 8F)
            };
            _cmbStance.Items.AddRange(new object[] { "Mở chân", "Đóng chân" });
            _cmbStance.SelectedIndex = 0;
            // Đặt xuống hàng dưới cạnh nút Xóa
            _cmbStance.Left = _btnReset.Left;
            _cmbStance.Top = _btnStart.Bottom + 6;
            Controls.Add(_cmbStance);
            _cmbStance.BringToFront();
            // Label "Tư thế" nếu cần rõ hơn
            var lblStance = new Label
            {
                Text = "Tư thế:",
                AutoSize = true,
                Left = _cmbStance.Left - 52,
                Top = _cmbStance.Top + 4,
                ForeColor = System.Drawing.Color.White,
                BackColor = System.Drawing.Color.Transparent,
                Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold)
            };
            Controls.Add(lblStance);
            lblStance.BringToFront();

        }

        private void StartSession()
        {
            if (_isListening) return;
            ResetSession();
            StartServer();
            SetStatus("Chuẩn bị đo (đang chờ ESP32 nếu chưa kết nối)...", System.Drawing.Color.DarkOrange);
            if (_btnStart != null) _btnStart.Enabled = false;
            if (_btnStop != null) _btnStop.Enabled = true;
            if (_btnReset != null) _btnReset.Enabled = false; // chưa cho Xóa khi đang chạy
            if (_cmbStance != null) _cmbStance.Enabled = false; // khóa tư thế
            _measurementFinished = false;
            _sessionStartTime = DateTime.UtcNow;
            // Đặt lại vị trí và style cho label đếm ngược trước khi bắt đầu
            if (_lblCountdown != null)
            {
                _lblCountdown.Visible = true;
                _lblCountdown.Font = new System.Drawing.Font("Segoe UI", 48F, System.Drawing.FontStyle.Bold);
                _lblCountdown.ForeColor = System.Drawing.Color.Gold;
                _lblCountdown.BackColor = System.Drawing.Color.Black;
                _lblCountdown.Width = 300;
                _lblCountdown.Height = 100;
                _lblCountdown.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
                // Đặt giữa form
                _lblCountdown.Left = (this.ClientSize.Width - _lblCountdown.Width) / 2;
                _lblCountdown.Top = (this.ClientSize.Height - _lblCountdown.Height) / 2;
                _lblCountdown.BringToFront();
            }
            BeginCountdown(30);
        }

        private void StopSession()
        {
            if (!_isListening) return;
            StopServer();
            if (!_measurementFinished)
            {
                SetStatus("Đã dừng sớm", System.Drawing.Color.Gray);
            }
            else
            {
                SaveSessionSummary();
                SetStatus("Hoàn tất 30s - nhấn Lưu để xuất hoặc Xóa để đo lại", System.Drawing.Color.LimeGreen);
            }
            if (_btnStart != null) _btnStart.Enabled = true;
            if (_btnStop != null) _btnStop.Enabled = false;
            if (_btnReset != null) _btnReset.Enabled = true;
            if (_cmbStance != null) _cmbStance.Enabled = true;
            if (_btnExport != null) _btnExport.Enabled = _measurementFinished; // chỉ bật lưu nếu hoàn tất
            StopCountdown();
        }

        private void ResetSession()
        {
            _sumX = 0; _sumY = 0; _count = 0; _currentTestDateId = null;
            _lastPlottedX = null; _lastPlottedY = null; // reset lưu điểm cuối
            _allSamples.Clear();
            _sessionStartTime = DateTime.MinValue;
            if (chart1.Series.IndexOf("COG") >= 0)
            {
                chart1.Series["COG"].Points.Clear();
            }
            if (chart1.Series.IndexOf("Center") >= 0)
            {
                chart1.Series["Center"].Points.Clear();
            }
            if (_lblCountdown != null) _lblCountdown.Text = "--";
        }

        private void InitStatusLabel()
        {
            try
            {
                _lblStatus = new Label()
                {
                    AutoSize = true,
                    Text = "Đang chờ ESP32...",
                    ForeColor = System.Drawing.Color.DarkOrange,
                    BackColor = System.Drawing.Color.Transparent,
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold)
                };
                // đặt ở góc trên bên phải chart nếu có
                if (chart1 != null)
                {
                    _lblStatus.Left = chart1.Right - 170;
                    _lblStatus.Top = chart1.Top + 5;
                    this.Controls.Add(_lblStatus);
                    _lblStatus.BringToFront();
                }
            }
            catch { }
        }

        private void AddForceLabels()
        {
            if (_rightPanel == null || _lblF1 != null) return; // tránh tạo lại
            int topBase = (_lblStatus?.Bottom ?? 18) + 2;
            var font = new System.Drawing.Font("Consolas", 8F);
            _lblF1 = new Label { Left = 6, Top = topBase, Width = 120, Height = 14, Text = "F1: --", ForeColor = System.Drawing.Color.LightSkyBlue, BackColor = System.Drawing.Color.Black, Font = font };
            _lblF2 = new Label { Left = 130, Top = topBase, Width = 120, Height = 14, Text = "F2: --", ForeColor = System.Drawing.Color.LightSkyBlue, BackColor = System.Drawing.Color.Black, Font = font };
            _lblF3 = new Label { Left = 6, Top = topBase + 14, Width = 120, Height = 14, Text = "F3: --", ForeColor = System.Drawing.Color.LightSkyBlue, BackColor = System.Drawing.Color.Black, Font = font };
            _lblF4 = new Label { Left = 130, Top = topBase + 14, Width = 120, Height = 14, Text = "F4: --", ForeColor = System.Drawing.Color.LightSkyBlue, BackColor = System.Drawing.Color.Black, Font = font };
            _lblFtot = new Label { Left = 6, Top = topBase + 28, Width = 244, Height = 14, Text = "Ftot: --", ForeColor = System.Drawing.Color.Khaki, BackColor = System.Drawing.Color.Black, Font = font };
            _rightPanel.Controls.AddRange(new Control[] { _lblF1, _lblF2, _lblF3, _lblF4, _lblFtot });
        }

        private void UpdateForceLabels()
        {
            if (_lblF1 == null) return;
            try
            {
                _lblF1.Text = $"F1: {_lastF1:F1}";
                _lblF2!.Text = $"F2: {_lastF2:F1}";
                _lblF3!.Text = $"F3: {_lastF3:F1}";
                _lblF4!.Text = $"F4: {_lastF4:F1}";
                _lblFtot!.Text = $"Ftot: {_lastF1 + _lastF2 + _lastF3 + _lastF4:F1}";
            }
            catch { }
        }

        private void InitStatusTimer()
        {
            _statusTimer = new System.Windows.Forms.Timer();
            _statusTimer.Interval = 1000; // 1s
            _statusTimer.Tick += (s, e) => RefreshStatusByTime();
            _statusTimer.Start();
        }

        private void RefreshStatusByTime()
        {
            if (_lblStatus == null) return;
            if (_isListening && _count == 0)
            {
                // vẫn đang đợi kết nối / dữ liệu
                return; // giữ nguyên text hiện tại (Đang chờ ... hoặc Đã kết nối ...)
            }
            if (_count > 0)
            {
                var delta = DateTime.UtcNow - _lastDataTime;
                if (delta.TotalSeconds > 3 && _lblStatus.Text.StartsWith("Đang nhận"))
                {
                    _lblStatus.Text = "Mất tín hiệu (3s+)";
                    _lblStatus.ForeColor = System.Drawing.Color.Red;
                }
            }
        }

        private void SetStatus(string text, System.Drawing.Color color)
        {
            if (_lblStatus == null) return;
            // Nếu handle chưa tạo, gán trực tiếp để tránh exception
            if (!_lblStatus.IsHandleCreated || !IsHandleCreated)
            {
                _lblStatus.Text = text;
                _lblStatus.ForeColor = color;
                return;
            }
            if (_lblStatus.InvokeRequired)
            {
                try
                {
                    _lblStatus.BeginInvoke((MethodInvoker)(() =>
                    {
                        _lblStatus.Text = text;
                        _lblStatus.ForeColor = color;
                    }));
                }
                catch { /* ignore */ }
            }
            else
            {
                _lblStatus.Text = text;
                _lblStatus.ForeColor = color;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            StopSession();
        }

        private void InitChart()
        {
            chart1.Series.Clear();
            chart1.ChartAreas.Clear();
            var ca = new ChartArea("Default");
            ca.AxisX.Title = "X (cm)";
            ca.AxisY.Title = "Y (cm)";
            ca.AxisX.Minimum = -30; // cố định ±30 để luôn thấy điểm
            ca.AxisX.Maximum = 30;
            ca.AxisY.Minimum = -30;
            ca.AxisY.Maximum = 30;
            // Dark theme mặc định
            ca.BackColor = System.Drawing.Color.Black;
            ca.BorderColor = System.Drawing.Color.White;
            ca.AxisX.MajorGrid.Enabled = true;
            ca.AxisY.MajorGrid.Enabled = true;
            ca.AxisX.MajorGrid.LineColor = System.Drawing.Color.FromArgb(60, 200, 200, 200);
            ca.AxisY.MajorGrid.LineColor = System.Drawing.Color.FromArgb(60, 200, 200, 200);
            ca.AxisX.LineColor = System.Drawing.Color.White;
            ca.AxisY.LineColor = System.Drawing.Color.White;
            ca.AxisX.MajorTickMark.LineColor = System.Drawing.Color.White;
            ca.AxisY.MajorTickMark.LineColor = System.Drawing.Color.White;
            ca.AxisX.LabelStyle.ForeColor = System.Drawing.Color.White;
            ca.AxisY.LabelStyle.ForeColor = System.Drawing.Color.White;
            ca.AxisX.Interval = 5; // nhãn mỗi 5 cm
            ca.AxisY.Interval = 5;
            // Cho trục cắt tại 0 để có dấu thập ở giữa
            ca.AxisX.Crossing = 0;
            ca.AxisY.Crossing = 0;
            ca.AxisX.ArrowStyle = AxisArrowStyle.Triangle;
            ca.AxisY.ArrowStyle = AxisArrowStyle.Triangle;
            chart1.ChartAreas.Add(ca);

            var path = new Series("COG")
            {
                ChartType = SeriesChartType.Point,
                Color = System.Drawing.Color.White,
                MarkerStyle = MarkerStyle.Circle,
                MarkerSize = 5,
                YAxisType = AxisType.Primary,
                XAxisType = AxisType.Primary,
                // Tắt indexing để không tự tăng chỉ số X (giữ đúng giá trị thực và chồng lên nếu trùng)
                IsXValueIndexed = false
            };
            chart1.Series.Add(path);

            // (Bỏ nhãn Samples để trống không gian)
        }

    // Đã bỏ ApplyChartTheme và ToggleTheme (luôn dark)

        private void InitTable()
        {
            _dataTable.Columns.Add("Time", typeof(DateTime));
            _dataTable.Columns.Add("X", typeof(double));
            _dataTable.Columns.Add("Y", typeof(double));
            _dataTable.Columns.Add("Stance", typeof(string));
        }

        private void InitGrid()
        {
            _grid = new DataGridView
            {
                DataSource = _dataTable,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Dock = DockStyle.Bottom,
                Height = 180,
                RowHeadersVisible = false
            };
            // Format columns when available
            _grid.DataBindingComplete += (s, e) =>
            {
                if (_grid.Columns.Count >= 3)
                {
                    _grid.Columns[0].HeaderText = "Time";
                    _grid.Columns[1].HeaderText = "X (cm)";
                    _grid.Columns[2].HeaderText = "Y (cm)";
                    _grid.Columns[1].DefaultCellStyle.Format = "F2";
                    _grid.Columns[2].DefaultCellStyle.Format = "F2";
                    if (_grid.Columns.Count >= 4)
                    {
                        _grid.Columns[3].HeaderText = "Tư thế";
                    }
                }
            };
            Controls.Add(_grid);
            _grid.BringToFront();
        }

        private void EnsureCenterSeries()
        {
            // Giữ lại hàm để tương thích, nhưng ta dùng hai series: Last & Mean
            if (chart1.Series.IndexOf("Last") == -1)
            {
                chart1.Series.Add(new Series("Last")
                {
                    ChartType = SeriesChartType.Point,
                    Color = System.Drawing.Color.Orange,
                    MarkerStyle = MarkerStyle.Circle,
                    MarkerSize = 9
                });
            }
            if (chart1.Series.IndexOf("Mean") == -1)
            {
                chart1.Series.Add(new Series("Mean")
                {
                    ChartType = SeriesChartType.Point,
                    Color = System.Drawing.Color.LawnGreen,
                    MarkerStyle = MarkerStyle.Cross,
                    MarkerSize = 10,
                    BorderWidth = 2
                });
            }
        }

        private void StartServer()
        {
            try
            {
                _tcpServer = new TcpListener(IPAddress.Any, 8080);
                _tcpServer.Start();
                _isListening = true;
                _listenerThread = new Thread(ListenLoop) { IsBackground = true };
                _listenerThread.Start();
                AppendDebug("[SERVER] Started listening 0.0.0.0:8080");
                // Chỉ lấy 1 IP ổn định để ESP32 dùng, tránh thay đổi danh sách gây khó sử dụng
                try
                {
                    _fixedIp = GetPrimaryIPv4();
                    if (InvokeRequired)
                        BeginInvoke((MethodInvoker)(() => this.Text = $"Đo lường - Listening 8080 @ {_fixedIp}"));
                    else
                        this.Text = $"Đo lường - Listening 8080 @ {_fixedIp}";
                    AppendDebug("[SERVER] Primary IP: " + _fixedIp);
                }
                catch { }
            }
            catch (Exception ex)
            {
                AppendDebug("[ERROR] StartServer: " + ex.Message);
                MessageBox.Show("Không thể khởi động TCP server: " + ex.Message);
            }
        }

        // Chọn IP IPv4 đang hoạt động ổn định: ưu tiên Ethernet, sau đó Wi-Fi, bỏ qua 169.254.* và loopback
        private string GetPrimaryIPv4()
        {
            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces();
                // ưu tiên thứ tự: Ethernet, WiFi, còn lại
                string[] preferOrder = { "Ethernet", "Wireless", "Wi-Fi", "WiFi" };
                var candidates = nics
                    .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                                n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                !n.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                                !n.Name.Contains("Virtual", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(n => {
                        for (int i = 0; i < preferOrder.Length; i++)
                            if (n.Description.Contains(preferOrder[i], StringComparison.OrdinalIgnoreCase) || n.Name.Contains(preferOrder[i], StringComparison.OrdinalIgnoreCase))
                                return i; // ưu tiên cao
                        return preferOrder.Length + 1;
                    })
                    .ToList();
                foreach (var nic in candidates)
                {
                    var ipProps = nic.GetIPProperties();
                    foreach (var ua in ipProps.UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            string ip = ua.Address.ToString();
                            if (!ip.StartsWith("169.254.")) // bỏ APIPA
                                return ip;
                        }
                    }
                }
                // Fallback DNS
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var first = host.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                if (first != null) return first.ToString();
            }
            catch { }
            return "127.0.0.1";
        }

        private void StopServer()
        {
            try
            {
                _isListening = false;
                _tcpServer?.Stop();
                AppendDebug("[SERVER] Stop requested");
                if (_listenerThread != null && _listenerThread.IsAlive)
                {
                    if (!_listenerThread.Join(500))
                        _listenerThread.Interrupt();
                }
            }
            catch { /* ignore */ }
            finally
            {
                _tcpServer = null;
                _listenerThread = null;
                SetStatus("Đã dừng server", System.Drawing.Color.Gray);
                try { _currentClient?.Close(); } catch { }
                _currentClient = null;
            }
        }

        private void ListenLoop()
        {
            while (_isListening && _tcpServer != null)
            {
                TcpClient? client = null;
                try
                {
                    AppendDebug("[SERVER] Waiting for client...");
                    client = _tcpServer.AcceptTcpClient();
                    SetStatus("ESP32 đã kết nối", System.Drawing.Color.Green);
                    _currentClient = client; // store current
                    AppendDebug("[SERVER] Client connected from " + ((client.Client.RemoteEndPoint != null) ? client.Client.RemoteEndPoint.ToString() : "unknown"));
                }
                catch
                {
                    if (!_isListening) break; // stopping
                    AppendDebug("[WARN] Accept failed (loop continues)");
                    continue;
                }

                using (client)
                using (var ns = client.GetStream())
                {
                    var buffer = new byte[1024];
                    var sb = new System.Text.StringBuilder();
                    while (_isListening && client.Connected)
                    {
                        int read;
                        try { read = ns.Read(buffer, 0, buffer.Length); }
                        catch (Exception ex) { AppendDebug("[ERROR] Read: " + ex.Message); break; }
                        if (read == 0) break;
                        // Log raw bytes
                        try
                        {
                            var hex = BitConverter.ToString(buffer, 0, read);
                            AppendDebug($"[READ] {read} bytes HEX={hex}");
                        }
                        catch { }
                        sb.Append(System.Text.Encoding.ASCII.GetString(buffer, 0, read));
                        string content = sb.ToString();
                        int newlineIndex;
                        while ((newlineIndex = content.IndexOf('\n')) >= 0)
                        {
                            string line = content.Substring(0, newlineIndex).Trim();
                            content = content.Substring(newlineIndex + 1);
                            if (TryParseExtendedLine(line, out double f1, out double f2, out double f3, out double f4, out double x6, out double y6))
                            {
                                _lastF1 = f1; _lastF2 = f2; _lastF3 = f3; _lastF4 = f4; UpdateForceLabels();
                                AddSample(x6, y6);
                                AppendDebug($"[DATA6] F1={f1:F1} F2={f2:F1} F3={f3:F1} F4={f4:F1} -> {x6:F2},{y6:F2}");
                            }
                            else if (TryParseSample(line, out double x, out double y))
                            {
                                AddSample(x, y);
                                AppendDebug($"[DATA] {x:F2},{y:F2}");
                            }
                            else if (!string.IsNullOrEmpty(line))
                            {
                                AppendDebug($"[RAW] {line}");
                            }
                        }
                        // Nếu content rất dài không có newline, cắt bớt để tránh tràn
                        if (content.Length > 2048)
                        {
                            AppendDebug("[WARN] Buffer overflow trimming");
                            content = content[^1024..];
                        }
                        sb.Clear();
                        sb.Append(content);
                    }
                }
                SetStatus("ESP32 ngắt kết nối", System.Drawing.Color.DarkOrange);
                AppendDebug("[INFO] Client disconnected");
            }
            AppendDebug("[SERVER] ListenLoop ended");
        }

        private static bool TryParseSample(string line, out double x, out double y)
        {
            x = y = 0;
            if (string.IsNullOrWhiteSpace(line) || !line.Contains(",")) return false;
            var parts = line.Split(',');
            if (parts.Length < 2) return false;
         return double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
             && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
        }

        // Dòng có 6 số: F1,F2,F3,F4,X,Y (có thể có tiền tố F: hoặc DATA:)
        private static bool TryParseExtendedLine(string line, out double f1, out double f2, out double f3, out double f4, out double x, out double y)
        {
            f1 = f2 = f3 = f4 = x = y = 0;
            if (string.IsNullOrWhiteSpace(line)) return false;
            // 1. Cắt các tiền tố chữ cái (DATA:, RAW:, F:, etc.) ở đầu cho tới khi gặp chữ số, dấu - hoặc +
            int cut = 0;
            while (cut < line.Length && !char.IsDigit(line[cut]) && line[cut] != '-' && line[cut] != '+') cut++;
            if (cut > 0 && cut < line.Length) line = line.Substring(cut);

            // 2. Chuẩn hoá: thay ; hoặc tab hoặc nhiều khoảng trắng thành dấu phẩy
            line = line.Replace(';', ',');
            line = Regex.Replace(line, "[\t ]+", ",");
            // 3. Xoá các ký tự không thuộc [0-9.+-eE,] (loại bỏ rác hex kiểu 2E-30)
            line = Regex.Replace(line, @"[^0-9eE+\-.,]", "");
            // 4. Nếu dùng dấu phẩy thập phân (có nhiều dấu phẩy) khó phân biệt -> tạm thay mọi ',' thành ' ' rồi chuẩn hoá lại.
            // Tuy nhiên ở đây giữ nguyên vì ta kỳ vọng định dạng đã là phân tách bằng dấu phẩy.

            var partsRaw = line.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (partsRaw.Length < 6) return false;

            // Lấy đúng 6 phần đầu tiên (có thể thừa do noise)
            string[] parts = new string[6];
            for (int i = 0; i < 6; i++) parts[i] = partsRaw[i].Trim();

            bool ok = double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out f1)
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out f2)
                && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out f3)
                && double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out f4)
                && double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
                && double.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out y);
            return ok;
        }

        private void AddSample(double x, double y)
        {
            if (InvokeRequired)
            {
                BeginInvoke((MethodInvoker)(() => AddSample(x, y)));
                return;
            }

            // Bỏ qua điểm lặp lại giống hệt trước đó để tránh vệt ngang (ví dụ nhiều gói 0,0 liên tục).
            if (_lastPlottedX.HasValue && _lastPlottedY.HasValue && Math.Abs(_lastPlottedX.Value - x) < 0.0001 && Math.Abs(_lastPlottedY.Value - y) < 0.0001)
            {
                // Vẫn thêm vào bảng dữ liệu để lưu thời gian nếu muốn? Ở đây vẫn thêm để thống kê.
                // Nếu không muốn lưu cũng có thể 'return;' trước khi thêm bảng.
            }

            // Chart path
            if (chart1.Series.IndexOf("COG") == -1)
            {
                chart1.Series.Add(new Series("COG")
                {
                    ChartType = SeriesChartType.Point,
                    Color = System.Drawing.Color.White,
                    MarkerStyle = MarkerStyle.Circle,
                    MarkerSize = 5,
                    IsXValueIndexed = false
                });
            }
            // Chỉ thêm vào scatter khi (1) khác điểm trước, hoặc (2) không phải 0,0 lặp lại.
            bool isRepeatZero = Math.Abs(x) < 0.0001 && Math.Abs(y) < 0.0001 && _lastPlottedX.HasValue && Math.Abs(_lastPlottedX.Value) < 0.0001 && _lastPlottedY.HasValue && Math.Abs(_lastPlottedY.Value) < 0.0001;
            if (!isRepeatZero)
            {
                chart1.Series["COG"].Points.AddXY(x, y);
                _lastPlottedX = x; _lastPlottedY = y;
            }

            // Theo dõi chuỗi (0,0) để cảnh báo người dùng điều chỉnh threshold / tải lên bàn
            if (Math.Abs(x) < 0.05 && Math.Abs(y) < 0.05)
            {
                _zeroStreak++;
                if (_zeroStreak == ZERO_STREAK_WARNING)
                {
                    AppendDebug("[WARN] Chỉ nhận (0,0) kéo dài - kiểm tra cảm biến / tải / NO_LOAD_THRESHOLD");
                    SetStatus("Chỉ nhận (0,0) - kiểm tra tải hoặc phần cứng", System.Drawing.Color.OrangeRed);
                }
            }
            else
            {
                if (_zeroStreak >= ZERO_STREAK_WARNING)
                {
                    AppendDebug("[INFO] Đã có chuyển động khác (0,0) trở lại");
                    SetStatus("Đang nhận dữ liệu", System.Drawing.Color.Blue);
                }
                _zeroStreak = 0;
            }

            string stance = _cmbStance?.SelectedItem?.ToString() ?? "";
            var now = DateTime.Now;
            _dataTable.Rows.Add(now, x, y, stance);
            _allSamples.Add((now, x, y, stance));
            // Nếu đã đủ 300 mẫu thì tự kết thúc (trong trường hợp tốc độ cao < 30s) hoặc nếu countdown vẫn chạy thì cho tiếp nhưng không thêm quá 300
            if (_allSamples.Count >= TARGET_TOTAL_SAMPLES)
            {
                // Ngăn thêm vượt quá 300
                _allSamples.RemoveRange(TARGET_TOTAL_SAMPLES, _allSamples.Count - TARGET_TOTAL_SAMPLES);
                if (!_measurementFinished)
                {
                    _measurementFinished = true;
                    StopCountdown();
                    StopSession();
                }
            }
            // Giới hạn số dòng hiển thị để tránh nặng UI
            if (_dataTable.Rows.Count > MAX_ROWS_DISPLAY)
            {
                // remove oldest row
                _dataTable.Rows.RemoveAt(0);
            }
            // Auto scroll to last
            if (_grid != null)
            {
                try
                {
                    _grid.FirstDisplayedScrollingRowIndex = _dataTable.Rows.Count - 1;
                }
                catch { }
            }
            _sumX += x; _sumY += y; _count++;
            _lastDataTime = DateTime.UtcNow;
            SetStatus("Đang nhận dữ liệu...", System.Drawing.Color.Blue);

            EnsureCenterSeries();
            var last = chart1.Series["Last"];
            var meanSeries = chart1.Series["Mean"];
            last.Points.Clear();
            meanSeries.Points.Clear();
            // Last sample
            last.Points.AddXY(x, y);
            // Mean of session
            double meanX = _sumX / _count;
            double meanY = _sumY / _count;
            meanSeries.Points.AddXY(meanX, meanY);

            // Hiển thị số mẫu trong status bar thay thế
            if (_count % 5 == 0) // tránh cập nhật quá dày
                SetStatus($"Đang nhận dữ liệu... N={_count}", System.Drawing.Color.Blue);
            // Màu điểm trung bình (center) thành cam giống ảnh
            // Colors already set in initialization
        }

        private void BeginCountdown(int seconds)
        {
            _secondsRemaining = seconds;
            if (_measurementTimer == null)
            {
                _measurementTimer = new System.Windows.Forms.Timer();
                _measurementTimer.Interval = 1000;
                _measurementTimer.Tick += (s, e) => TickCountdown();
            }
            _measurementTimer.Start();
            UpdateCountdownLabel();
        }

        private void TickCountdown()
        {
            _secondsRemaining--;
            if (_secondsRemaining <= 0)
            {
                _measurementFinished = true;
                _secondsRemaining = 0;
                UpdateCountdownLabel();
                _measurementTimer?.Stop();
                // auto stop
                StopSession();
            }
            else
            {
                UpdateCountdownLabel();
            }
        }

        private void UpdateCountdownLabel()
        {
            if (_lblCountdown == null) return;
            if (_secondsRemaining > 0)
            {
                _lblCountdown.Text = $"{_secondsRemaining}";
                _lblCountdown.Visible = true;
            }
            else if (_measurementFinished)
            {
                _lblCountdown.Text = "XONG";
                // Ẩn sau 1 giây
                var t = new System.Windows.Forms.Timer();
                t.Interval = 1000;
                t.Tick += (s, e) => { _lblCountdown.Visible = false; t.Stop(); t.Dispose(); };
                t.Start();
            }
            else
            {
                _lblCountdown.Visible = false;
            }
        }

        private void StopCountdown()
        {
            _measurementTimer?.Stop();
        }

        private void SaveSessionSummary()
        {
            if (string.IsNullOrEmpty(_connStr)) return;
            if (_count == 0) return; // không có dữ liệu COP
            try
            {
                double meanX = _sumX / _count;
                double meanY = _sumY / _count;
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                // Nếu chưa có DateOfTest (BMI chưa lưu trước đó) thì tạo mới
                if (_currentTestDateId == null)
                {
                    string sqlDate = "INSERT INTO DateOfTest(PatientID,TestDate) OUTPUT INSERTED.TestDateID VALUES(@pid,GETDATE())";
                    using var cmdDate = new SqlCommand(sqlDate, conn);
                    cmdDate.Parameters.AddWithValue("@pid", _selectedPatientId);
                    _currentTestDateId = (int)cmdDate.ExecuteScalar();
                }
                int testDateId = _currentTestDateId.Value;

                InsertValueSummary(conn, testDateId, 1, meanX); // Mean X
                InsertValueSummary(conn, testDateId, 2, meanY); // Mean Y
                InsertValueSummary(conn, testDateId, 99, _count); // Samples
            }
            catch { }
        }

        private static void InsertValueSummary(SqlConnection conn, int testDateId, int parameterId, double value)
        {
            string sql = "INSERT INTO Test(TestDateID,ParameterID,Value) VALUES(@d,@p,@v)";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@d", testDateId);
            cmd.Parameters.AddWithValue("@p", parameterId);
            cmd.Parameters.AddWithValue("@v", value);
            cmd.ExecuteNonQuery();
        }

        public string? SaveChartToImage(string filePath)
        {
            try
            {
                if (InvokeRequired)
                {
                    Invoke((MethodInvoker)(() => chart1.SaveImage(filePath, ChartImageFormat.Png)));
                }
                else
                {
                    chart1.SaveImage(filePath, ChartImageFormat.Png);
                }
                return filePath;
            }
            catch
            {
                return null;
            }
        }
        private void SendResetCommand()
        {
            try
            {
                if (_currentClient == null || !_currentClient.Connected)
                {
                    SetStatus("Không có ESP32 để RESET", System.Drawing.Color.Red);
                    return;
                }
                var ns = _currentClient.GetStream();
                if (ns.CanWrite)
                {
                    var cmd = "RESET\nRESETALL\n";
                    var bytes = System.Text.Encoding.ASCII.GetBytes(cmd);
                    ns.Write(bytes, 0, bytes.Length);
                    SetStatus("Đã gửi RESET + RESETALL", System.Drawing.Color.MediumPurple);
                    ResetSession();
                }
            }
            catch
            {
                SetStatus("Lỗi gửi RESET", System.Drawing.Color.Red);
            }
        }

        private void ExportToExcel()
        {
            try
            {
                using var sfd = new SaveFileDialog { Filter = "Excel Workbook|*.xlsx", FileName = $"Measurement_{DateTime.Now:yyyyMMdd_HHmm}.xlsx" };
                if (sfd.ShowDialog() != DialogResult.OK) return;
                using var wb = new XLWorkbook();
                var wsInfo = wb.Worksheets.Add("Summary");
                wsInfo.Cell(1, 1).Value = "Patient Name"; wsInfo.Cell(1, 2).Value = _patientName;
                wsInfo.Cell(2, 1).Value = "Gender"; wsInfo.Cell(2, 2).Value = _patientGender;
                wsInfo.Cell(3, 1).Value = "DOB"; wsInfo.Cell(3, 2).Value = _patientDob;
                wsInfo.Cell(4, 1).Value = "Patient ID"; wsInfo.Cell(4, 2).Value = _selectedPatientId;
                var stance = _cmbStance?.SelectedItem?.ToString() ?? string.Empty;
                wsInfo.Cell(5, 1).Value = "Stance"; wsInfo.Cell(5, 2).Value = stance;
                wsInfo.Cell(6, 1).Value = "Samples"; wsInfo.Cell(6, 2).Value = _count;
                wsInfo.Cell(7, 1).Value = "Samples (total)"; wsInfo.Cell(7, 2).Value = _allSamples.Count;
                double meanX = _count > 0 ? _sumX / _count : 0;
                double meanY = _count > 0 ? _sumY / _count : 0;
                wsInfo.Cell(8, 1).Value = "Mean X"; wsInfo.Cell(8, 2).Value = meanX;
                wsInfo.Cell(9, 1).Value = "Mean Y"; wsInfo.Cell(9, 2).Value = meanY;
                if (_allSamples.Count > 0)
                {
                    var last = _allSamples[^1];
                    wsInfo.Cell(10, 1).Value = "Last X"; wsInfo.Cell(10, 2).Value = last.x;
                    wsInfo.Cell(11, 1).Value = "Last Y"; wsInfo.Cell(11, 2).Value = last.y;
                }
                if (_txtHeight != null && _txtWeight != null &&
                    double.TryParse(_txtHeight.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double hCm) && hCm > 0 &&
                    double.TryParse(_txtWeight.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double wKg) && wKg > 0)
                {
                    double bmi = wKg / Math.Pow(hCm / 100.0, 2);
                    int baseRow = 12;
                    wsInfo.Cell(baseRow, 1).Value = "BMI"; wsInfo.Cell(baseRow, 2).Value = bmi;
                    wsInfo.Cell(baseRow + 1, 1).Value = "Height (cm)"; wsInfo.Cell(baseRow + 1, 2).Value = hCm;
                    wsInfo.Cell(baseRow + 2, 1).Value = "Weight (kg)"; wsInfo.Cell(baseRow + 2, 2).Value = wKg;
                }
                wsInfo.Columns().AdjustToContents();
                var ws = wb.Worksheets.Add("Samples");
                ws.Cell(1, 1).Value = "Time";
                ws.Cell(1, 2).Value = "X";
                ws.Cell(1, 3).Value = "Y";
                ws.Cell(1, 4).Value = "Stance";
                // Chọn 200 mẫu cuối hợp lệ sau khi loại bỏ 10-15s đầu
                var filtered = FilterForExport();
                int r = 2;
                foreach (var rec in filtered)
                {
                    ws.Cell(r, 1).Value = rec.t;
                    ws.Cell(r, 2).Value = rec.x;
                    ws.Cell(r, 3).Value = rec.y;
                    ws.Cell(r, 4).Value = rec.stance;
                    r++;
                }
                ws.Columns().AdjustToContents();
                try
                {
                    string tmp = Path.Combine(Path.GetTempPath(), "chart_export_" + Guid.NewGuid().ToString("N") + ".png");
                    SaveChartToImage(tmp);
                    if (File.Exists(tmp))
                    {
                        wsInfo.AddPicture(tmp).MoveTo(wsInfo.Cell(13, 1)).WithSize(480, 360);
                    }
                }
                catch { }
                wb.SaveAs(sfd.FileName);
                MessageBox.Show("Xuất Excel thành công", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi xuất Excel: " + ex.Message);
            }
        }

        private System.Collections.Generic.List<(DateTime t,double x,double y,string stance)> FilterForExport()
        {
            var list = new System.Collections.Generic.List<(DateTime t,double x,double y,string stance)>();
            if (_allSamples.Count == 0) return list;
            // xác định thời gian cắt bỏ đầu
            DateTime first = _allSamples[0].t;
            // mặc định bỏ 10s đầu
            double trimSec = MIN_TRIM_SECONDS;
            // nếu tổng thời lượng > 28s và tổng mẫu > 250 thì có thể tăng đến 15s để vẫn còn >=200 mẫu
            double totalDuration = (_allSamples[^1].t - first).TotalSeconds;
            if (totalDuration > 28 && _allSamples.Count >= 250)
            {
                trimSec = Math.Min(MAX_TRIM_SECONDS, MIN_TRIM_SECONDS + 5);
            }
            var kept = _allSamples.FindAll(s => (s.t - first).TotalSeconds >= trimSec);
            // lấy 200 mẫu cuối cùng
            if (kept.Count > EXPORT_SAMPLES)
            {
                kept = kept.GetRange(kept.Count - EXPORT_SAMPLES, EXPORT_SAMPLES);
            }
            return kept;
        }
    private void SaveBMIToDatabase()
        {
            if (string.IsNullOrEmpty(_connStr))
            {
                MessageBox.Show("Chưa có kết nối CSDL", "BMI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_txtHeight == null || _txtWeight == null)
            {
                MessageBox.Show("Thiếu dữ liệu chiều cao / cân nặng", "BMI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!double.TryParse(_txtHeight.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double hCm) || hCm <= 0)
            {
                MessageBox.Show("Chiều cao không hợp lệ", "BMI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!double.TryParse(_txtWeight.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double wKg) || wKg <= 0)
            {
                MessageBox.Show("Cân nặng không hợp lệ", "BMI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            double hM = hCm / 100.0;
            double bmi = wKg / (hM * hM);
            try
            {
                using var conn = new SqlConnection(_connStr);
                conn.Open();
                // Nếu phiên chưa có DateOfTest thì tạo mới và giữ lại ID để COP dùng chung
                if (_currentTestDateId == null)
                {
                    string sqlDate = "INSERT INTO DateOfTest(PatientID,TestDate) OUTPUT INSERTED.TestDateID VALUES(@pid,GETDATE())";
                    using var cmdDate = new SqlCommand(sqlDate, conn);
                    cmdDate.Parameters.AddWithValue("@pid", _selectedPatientId);
                    _currentTestDateId = (int)cmdDate.ExecuteScalar();
                }
                int testDateId = _currentTestDateId.Value;
                InsertValueSummary(conn, testDateId, 10, bmi);   // BMI
                InsertValueSummary(conn, testDateId, 11, hCm);   // Height cm
                InsertValueSummary(conn, testDateId, 12, wKg);   // Weight kg
                MessageBox.Show("Đã lưu BMI vào phiên", "BMI", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi lưu BMI: " + ex.Message, "BMI", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
