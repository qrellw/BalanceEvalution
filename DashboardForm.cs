using System;
using System.Windows.Forms;
using ClosedXML.Excel;

namespace BalanceApp
{
    public partial class DashboardForm : Form
    {
        private string connStr;

        public DashboardForm(string connectionString)
        {
            InitializeComponent();
            connStr = connectionString;
            LoadPatients();
        }

        private void LoadPatients()
        {
            // Thử nhiều kiểu tên cột khác nhau (Việt / Anh) để tương thích DB hiện có
            string[] queries = new[]
            {
                // Kiểu tiếng Việt
                "SELECT IdBenhNhan, TenBenhNhan, NgaySinh, GioiTinh, SoDienThoai, DiaChi FROM Patients",
                // Kiểu tiếng Anh phổ biến
                "SELECT PatientID, FullName, DateOfBirth, Gender, Phone, Address FROM Patients",
                // Dự phòng: * (không khuyến khích nhưng giúp debug)
                "SELECT * FROM Patients"
            };
        foreach (var q in queries)
            {
                try
                {
            using var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr);
                    conn.Open();
            var da = new Microsoft.Data.SqlClient.SqlDataAdapter(q, conn);
                    var dt = new System.Data.DataTable();
                    da.Fill(dt);
                    if (dt.Rows.Count >= 0) // chấp nhận cả rỗng, đã thành công truy vấn
                    {
                        dgvPatients.DataSource = dt;
                        return;
                    }
                }
                catch { /* thử truy vấn tiếp theo */ }
            }
            MessageBox.Show("Không tải được danh sách bệnh nhân. Hãy kiểm tra tên bảng / cột.");
        }

        private void btnNewPatient_Click(object sender, EventArgs e)
        {
            using var pf = new PatientForm(connStr);
            if (pf.ShowDialog() == DialogResult.OK)
            {
                LoadPatients();
                // Tự động chọn dòng bệnh nhân mới nếu tìm thấy
                if (pf.InsertedPatientId.HasValue && dgvPatients.DataSource is System.Data.DataTable dt)
                {
                    foreach (DataGridViewRow row in dgvPatients.Rows)
                    {
                        if (pf.InsertedPatientId.HasValue && row.Cells[0]?.Value != null &&
                            int.TryParse(row.Cells[0].Value?.ToString(), out int id) && id == pf.InsertedPatientId.Value)
                        {
                            row.Selected = true;
                            dgvPatients.CurrentCell = row.Cells[0];
                            break;
                        }
                    }
                }
            }
        }

        private void btnMeasure_Click(object sender, EventArgs e)
        {
            if (dgvPatients.CurrentRow == null || dgvPatients.CurrentRow.Cells[0].Value == null)
            {
                MessageBox.Show("Hãy thêm và chọn một bệnh nhân trước khi đo.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var cellVal = dgvPatients.CurrentRow?.Cells[0]?.Value?.ToString();
            if (!int.TryParse(cellVal, out int patientId))
            {
                MessageBox.Show("ID bệnh nhân không hợp lệ.");
                return;
            }
            // Lấy thêm thông tin để hiển thị ở MeasureForm (tùy thuộc vào tên cột thực tế)
            var row = dgvPatients.CurrentRow!;
            // Tìm tên cột động
            string name = GetValueByPreferredNames(row, new[] { "TenBenhNhan", "FullName", "Name" });
            string dob = GetValueByPreferredNames(row, new[] { "NgaySinh", "DateOfBirth", "DateOfBith", "DOB" });
            string gender = GetValueByPreferredNames(row, new[] { "GioiTinh", "Gender" });
            using var mf = new MeasureForm(connStr, patientId, name, gender, dob);
            mf.ShowDialog();
        }

        private static string SafeCellString(DataGridViewRow? row, int index)
        {
            if (row == null) return string.Empty;
            if (row.Cells.Count <= index) return string.Empty;
            return row.Cells[index].Value?.ToString() ?? string.Empty;
        }

        private static string GetValueByPreferredNames(DataGridViewRow row, string[] names)
        {
        if (row?.DataGridView?.DataSource is System.Data.DataTable dt)
            {
                foreach (var n in names)
                {
            if (dt.Columns.Contains(n))
                    {
                        var col = dt.Columns[n];
                        if (col != null)
                        {
                            int idx = col.Ordinal;
                            return idx >= 0 && idx < row.Cells.Count ? (row.Cells[idx].Value?.ToString() ?? string.Empty) : string.Empty;
                        }
                        return string.Empty;
                    }
                }
            }
            // fallback: lấy ô thứ 1 nếu có
            if (row != null && row.Cells.Count > 1)
                return row.Cells[1].Value?.ToString() ?? string.Empty;
            return string.Empty;
        }
        private void btnExportExcel_Click(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog() { Filter = "Excel Workbook|*.xlsx" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        using (var wb = new XLWorkbook())
                        {
                            var ws = wb.Worksheets.Add("Patients");
                            // Header
                            for (int i = 0; i < dgvPatients.Columns.Count; i++)
                            {
                                ws.Cell(1, i + 1).Value = dgvPatients.Columns[i].HeaderText;
                            }
                            // Data
                            for (int r = 0; r < dgvPatients.Rows.Count; r++)
                            {
                                for (int c = 0; c < dgvPatients.Columns.Count; c++)
                                {
                                    ws.Cell(r + 2, c + 1).Value = dgvPatients.Rows[r].Cells[c].Value?.ToString();
                                }
                            }
                            ws.Columns().AdjustToContents();

                            // Lưu ảnh biểu đồ từ MeasureForm nếu có
                            string chartImagePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "chart_temp.png");
                            bool chartSaved = false;
                            foreach (Form f in Application.OpenForms)
                            {
                                if (f is MeasureForm measureForm)
                                {
                                    var imgPath = measureForm.SaveChartToImage(chartImagePath);
                                    if (!string.IsNullOrEmpty(imgPath) && System.IO.File.Exists(imgPath))
                                    {
                                        var img = ws.AddPicture(imgPath)
                                            .MoveTo(ws.Cell(2, dgvPatients.Columns.Count + 2))
                                            .WithSize(400, 300);
                                        chartSaved = true;
                                    }
                                    break;
                                }
                            }
                            wb.SaveAs(sfd.FileName);
                            // Xóa file ảnh tạm
                            if (chartSaved && System.IO.File.Exists(chartImagePath))
                            {
                                try { System.IO.File.Delete(chartImagePath); } catch { }
                            }
                        }
                        MessageBox.Show("Xuất Excel thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Lỗi khi xuất Excel: " + ex.Message);
                    }
                }
            }
        }
    }
}
