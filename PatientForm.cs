using System;
using Microsoft.Data.SqlClient;
using System.Windows.Forms;

namespace BalanceApp
{
    public partial class PatientForm : Form
    {
        private readonly string _connStr;
        public int? InsertedPatientId { get; private set; }

        public PatientForm(string connectionString)
        {
            InitializeComponent();
            _connStr = connectionString;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            SavePatient();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

    private void SavePatient()
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Nhập tên bệnh nhân.");
                return;
            }
            using var conn = new SqlConnection(_connStr);
            try
            {
                conn.Open();
                // Phát hiện schema thực tế của bảng Patients (Anh hoặc Việt)
                var colCmd = new SqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Patients'", conn);
                var cols = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var rdr = colCmd.ExecuteReader())
                {
                    while (rdr.Read()) cols.Add(rdr.GetString(0));
                }

                bool english = cols.Contains("FullName") && cols.Contains("PatientID");
                bool vietnam = cols.Contains("TenBenhNhan") && cols.Contains("IdBenhNhan");

                string sql;
                if (english)
                {
                    // Chọn tên cột ngày sinh khả dụng (DateOfBirth hoặc DateOfBith bị sai chính tả)
                    string dateCol = cols.Contains("DateOfBirth") ? "DateOfBirth" : (cols.Contains("DateOfBith") ? "DateOfBith" : string.Empty);
                    if (string.IsNullOrEmpty(dateCol))
                    {
                        MessageBox.Show("Không tìm thấy cột DateOfBirth / DateOfBith trong bảng Patients.");
                        return;
                    }
                    sql = $"INSERT INTO Patients(FullName,Gender,{dateCol},Address,Phone) OUTPUT INSERTED.PatientID VALUES(@n,@g,@dob,@a,@p)";
                }
                else if (vietnam)
                {
                    string dateCol = cols.Contains("NgaySinh") ? "NgaySinh" : string.Empty;
                    if (string.IsNullOrEmpty(dateCol))
                    {
                        MessageBox.Show("Không tìm thấy cột NgaySinh trong bảng Patients.");
                        return;
                    }
                    sql = $"INSERT INTO Patients(TenBenhNhan,GioiTinh,{dateCol},DiaChi,SoDienThoai) OUTPUT INSERTED.IdBenhNhan VALUES(@n,@g,@dob,@a,@p)";
                }
                else
                {
                    MessageBox.Show("Không xác định được schema (cần FullName/PatientID hoặc TenBenhNhan/IdBenhNhan).");
                    return;
                }

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@n", txtName.Text.Trim());
                cmd.Parameters.AddWithValue("@g", cbGender.Text.Trim());
                cmd.Parameters.AddWithValue("@dob", dtpBirth.Value.Date);
                cmd.Parameters.AddWithValue("@a", txtAddress.Text.Trim());
                cmd.Parameters.AddWithValue("@p", txtPhone.Text.Trim());
                object? result = cmd.ExecuteScalar();
                if (result != null && int.TryParse(result.ToString(), out int newId)) InsertedPatientId = newId;
                MessageBox.Show("Đã thêm bệnh nhân mới!");
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi lưu: " + ex.Message);
            }
    }
    }
}
