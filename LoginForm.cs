using System;
using Microsoft.Data.SqlClient;
using System.Windows.Forms;
using System.Drawing; // added for drawing

namespace BalanceApp
{
    public partial class LoginForm : Form
    {
        string? connStr; // will be resolved dynamically

        public LoginForm()
        {
            InitializeComponent();
            InitEmbeddedLogo();
            ResolveConnection();
        }

        private void ResolveConnection()
        {
            connStr = DatabaseHelper.GetWorkingConnectionString();
            if (connStr == null)
            {
                MessageBox.Show(
                    "Không kết nối được CSDL.\n" +
                    "Kiểm tra: \n 1. SQL Server Express đã cài & service đang chạy.\n" +
                    " 2. Instance tên SQLEXPRESS hoặc LocalDB tồn tại.\n" +
                    " 3. Cho phép Windows Authentication.\n\nChi tiết cuối: " + DatabaseHelper.LastErrorDetail,
                    "DB Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitEmbeddedLogo()
        {
            if (picLogo == null) return;
            try
            {
                // Create bitmap matching the PictureBox size
                int w = picLogo.Width > 0 ? picLogo.Width : 220;
                int h = picLogo.Height > 0 ? picLogo.Height : 110;
                var bmp = new Bitmap(w, h);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    // Colors
                    Color cBg = Color.White;
                    Color cPrimary = Color.FromArgb(0,122,255); // blue
                    Color cSecondary = Color.FromArgb(10,40,60); // dark

                    // Draw heart-like overlapping pill shapes similar to provided image
                    var rectSecondary = new Rectangle((int)(w*0.18), (int)(h*0.25), (int)(w*0.50), (int)(h*0.50));
                    int corner = (int)(rectSecondary.Height * 0.9);
                    using (var gp = new System.Drawing.Drawing2D.GraphicsPath())
                    {
                        gp.AddArc(rectSecondary.X, rectSecondary.Y, corner, corner, 135, 270);
                        gp.AddArc(rectSecondary.Right - corner, rectSecondary.Bottom - corner, corner, corner, 315, 270);
                        gp.CloseFigure();
                        using var br2 = new SolidBrush(cSecondary);
                        g.FillPath(br2, gp);
                    }

                    var rectPrimary = new Rectangle((int)(w*0.38), (int)(h*0.10), (int)(w*0.50), (int)(h*0.70));
                    corner = (int)(rectPrimary.Height * 0.9);
                    using (var gp2 = new System.Drawing.Drawing2D.GraphicsPath())
                    {
                        gp2.AddArc(rectPrimary.X, rectPrimary.Y, corner, corner, 135, 270);
                        gp2.AddArc(rectPrimary.Right - corner, rectPrimary.Bottom - corner, corner, corner, 315, 270);
                        gp2.CloseFigure();
                        using var br = new SolidBrush(cPrimary);
                        g.FillPath(br, gp2);
                    }

                    // inner circle
                    int circleD = (int)(rectPrimary.Height * 0.35);
                    int circleX = rectPrimary.X + (int)(rectPrimary.Width * 0.55) - circleD/2;
                    int circleY = rectPrimary.Y + (int)(rectPrimary.Height * 0.40) - circleD/2;
                    using (var brc = new SolidBrush(Color.White))
                    {
                        g.FillEllipse(brc, circleX, circleY, circleD, circleD);
                    }
                }
                picLogo.Image = bmp;
                picLogo.BackColor = Color.White;
            }
            catch { }
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(connStr))
            {
                MessageBox.Show("Chưa có kết nối CSDL khả dụng.");
                return;
            }
            using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr))
            {
                try
                {
                    conn.Open();
                    string sql = "SELECT * FROM Registration WHERE Username=@u AND PasswordHash=@p";
                    var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@u", txtUsername.Text);
                    cmd.Parameters.AddWithValue("@p", txtPassword.Text);

                    var dr = cmd.ExecuteReader();
                    if (dr.Read())
                    {
                        DashboardForm dash = new DashboardForm(connStr);
                        dash.Show();
                        this.Hide();
                    }
                    else
                    {
                        MessageBox.Show("Sai tài khoản hoặc mật khẩu!");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi kết nối CSDL: " + ex.Message);
                }
            }
        }
    }
}
