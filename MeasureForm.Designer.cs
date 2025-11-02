// Auto-generated designer file for MeasureForm
namespace BalanceApp
{
    partial class MeasureForm
    {
        private System.ComponentModel.IContainer components = null;
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }
        #region Windows Form Designer generated code
        private void InitializeComponent()
        {

            // GroupBox Thông tin bệnh nhân
            this.groupBoxInfo = new System.Windows.Forms.GroupBox();
            this.lblId = new System.Windows.Forms.Label();
            this.txtId = new System.Windows.Forms.TextBox();
            this.lblName = new System.Windows.Forms.Label();
            this.txtName = new System.Windows.Forms.TextBox();
            this.lblBirth = new System.Windows.Forms.Label();
            this.dtpBirth = new System.Windows.Forms.DateTimePicker();
            this.lblGender = new System.Windows.Forms.Label();
            this.cbGender = new System.Windows.Forms.ComboBox();
            this.lblPhone = new System.Windows.Forms.Label();
            this.txtPhone = new System.Windows.Forms.TextBox();
            this.lblAddress = new System.Windows.Forms.Label();
            this.txtAddress = new System.Windows.Forms.TextBox();
            this.groupBoxInfo.SuspendLayout();

            // GroupBox Bảng ngày khám
            this.groupBoxVisits = new System.Windows.Forms.GroupBox();
            this.dgvVisits = new System.Windows.Forms.DataGridView();
            this.groupBoxVisits.SuspendLayout();

            // (Bỏ groupBoxStats và các textbox chỉ số tạm thời)

            // GroupBox Biểu đồ
            this.groupBoxChart = new System.Windows.Forms.GroupBox();
            this.chart1 = new System.Windows.Forms.DataVisualization.Charting.Chart();
            this.groupBoxChart.SuspendLayout();

            // Điều khiển
            this.btnStart = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnDelete = new System.Windows.Forms.Button();
            this.cbMode = new System.Windows.Forms.ComboBox();

            // (Đã bỏ các textbox nhập liệu tự do)

            ((System.ComponentModel.ISupportInitialize)(this.chart1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvVisits)).BeginInit();
            this.groupBoxInfo.ResumeLayout(false);
            this.groupBoxVisits.ResumeLayout(false);
            this.groupBoxChart.ResumeLayout(false);
            this.ResumeLayout(false);



            // (Đã loại bỏ khu vực chỉ số để dành không gian ghi dữ liệu bệnh nhân)

            // GroupBox Biểu đồ
            this.groupBoxChart.Text = "Đồ thị tọa độ CoG";
            this.groupBoxChart.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.groupBoxChart.Location = new System.Drawing.Point(410, 10);
            this.groupBoxChart.Size = new System.Drawing.Size(480, 400);
            this.groupBoxChart.Controls.Add(this.chart1);
            this.chart1.Location = new System.Drawing.Point(20, 40);
            this.chart1.Size = new System.Drawing.Size(440, 340);
            this.chart1.BorderlineDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.Solid;
            this.chart1.BorderlineColor = System.Drawing.Color.Gray;
            this.chart1.BorderlineWidth = 1;


            // Điều khiển - căn chỉnh lại cho hiển thị đầy đủ
            this.btnStart.Text = "Bắt đầu";
            this.btnStart.Size = new System.Drawing.Size(90, 40);
            this.btnStart.Location = new System.Drawing.Point(420, 430);
            this.btnStop.Text = "Dừng";
            this.btnStop.Size = new System.Drawing.Size(90, 40);
            this.btnStop.Location = new System.Drawing.Point(520, 430);
            this.btnSave.Text = "Lưu";
            this.btnSave.Size = new System.Drawing.Size(90, 40);
            this.btnSave.Location = new System.Drawing.Point(620, 430);
            this.btnDelete.Text = "Xóa";
            this.btnDelete.Size = new System.Drawing.Size(90, 40);
            this.btnDelete.Location = new System.Drawing.Point(720, 430);
            this.cbMode.Location = new System.Drawing.Point(820, 440);
            this.cbMode.Size = new System.Drawing.Size(80, 30);
            this.cbMode.Items.AddRange(new object[] {"Mở chân", "Đóng chân"});

            // (Loại bỏ các ô nhập tự do không dùng: chiều cao, cân nặng, huyết áp, mạch, ghi chú)


            // Thêm groupbox vào form
            this.Controls.Add(this.groupBoxInfo);
            this.Controls.Add(this.groupBoxVisits);
            // (Không thêm groupBoxStats)
            this.Controls.Add(this.groupBoxChart);
            this.Controls.Add(this.btnStart);
            this.Controls.Add(this.btnStop);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.btnDelete);
            this.Controls.Add(this.cbMode);
            // (Không thêm các textbox trắng dư thừa)

            // Form
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(920, 500);
            this.Name = "MeasureForm";
            this.Text = "Theo dõi bệnh nhân";
            // Không có copyright, hotline, version ở đây
            ((System.ComponentModel.ISupportInitialize)(this.chart1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvVisits)).EndInit();
            this.groupBoxInfo.ResumeLayout(false);
            this.groupBoxVisits.ResumeLayout(false);
            this.groupBoxChart.ResumeLayout(false);
            this.ResumeLayout(false);
        }
        #endregion
    // Khai báo biến control cho form
    private System.Windows.Forms.GroupBox groupBoxInfo;
    private System.Windows.Forms.GroupBox groupBoxVisits;
    // private System.Windows.Forms.GroupBox groupBoxStats; // removed
    private System.Windows.Forms.GroupBox groupBoxChart;
    private System.Windows.Forms.Label lblId;
    private System.Windows.Forms.TextBox txtId;
    private System.Windows.Forms.Label lblName;
    private System.Windows.Forms.TextBox txtName;
    private System.Windows.Forms.Label lblBirth;
    private System.Windows.Forms.DateTimePicker dtpBirth;
    private System.Windows.Forms.Label lblGender;
    private System.Windows.Forms.ComboBox cbGender;
    private System.Windows.Forms.Label lblPhone;
    private System.Windows.Forms.TextBox txtPhone;
    private System.Windows.Forms.Label lblAddress;
    private System.Windows.Forms.TextBox txtAddress;
    private System.Windows.Forms.DataGridView dgvVisits;
    private System.Windows.Forms.Button btnStart;
    private System.Windows.Forms.Button btnStop;
    private System.Windows.Forms.Button btnSave;
    private System.Windows.Forms.Button btnDelete;
    private System.Windows.Forms.ComboBox cbMode;
    private System.Windows.Forms.DataVisualization.Charting.Chart chart1;
    // Removed unused input textboxes (height, weight, blood, pulse, note, stats)
    }
}
