// Auto-generated designer file for LoginForm
namespace BalanceApp
{
    partial class LoginForm
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
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblSubtitle = new System.Windows.Forms.Label();
            this.picLogo = new System.Windows.Forms.PictureBox();
            this.lblUsername = new System.Windows.Forms.Label();
            this.lblPassword = new System.Windows.Forms.Label();
            this.txtUsername = new System.Windows.Forms.TextBox();
            this.txtPassword = new System.Windows.Forms.TextBox();
            this.btnGuest = new System.Windows.Forms.Button();
            this.btnLogin = new System.Windows.Forms.Button();
            this.btnRegister = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.picLogo)).BeginInit();
            this.SuspendLayout();

            // lblTitle
            this.lblTitle.Text = "BalanceApp";
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 20F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblTitle.AutoSize = true;
            this.lblTitle.Location = new System.Drawing.Point(250, 20);

            // lblSubtitle (tagline under app name)
            this.lblSubtitle.Text = "Thiết bị hỗ trợ đánh giá thăng bằng"; // tagline yêu cầu
            this.lblSubtitle.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblSubtitle.AutoSize = true;
            this.lblSubtitle.Location = new System.Drawing.Point(210, 65);

            // picLogo (logo sẽ gán bằng mã vẽ nội bộ trong LoginForm constructor; không cần file ngoài)
            this.picLogo.Location = new System.Drawing.Point(240, 100); // nằm giữa form
            this.picLogo.Size = new System.Drawing.Size(220, 110);
            this.picLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;

            // lblUsername
            this.lblUsername.Text = "Tên tài khoản";
            this.lblUsername.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.lblUsername.AutoSize = true;
            this.lblUsername.Location = new System.Drawing.Point(200, 235);

            // lblPassword
            this.lblPassword.Text = "Mật khẩu";
            this.lblPassword.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.lblPassword.AutoSize = true;
            this.lblPassword.Location = new System.Drawing.Point(200, 275);

            // txtUsername
            this.txtUsername.Location = new System.Drawing.Point(320, 235);
            this.txtUsername.Size = new System.Drawing.Size(200, 27);
            this.txtUsername.PlaceholderText = "Tên đăng nhập";

            // txtPassword
            this.txtPassword.Location = new System.Drawing.Point(320, 275);
            this.txtPassword.Size = new System.Drawing.Size(200, 27);
            this.txtPassword.PasswordChar = '●';
            this.txtPassword.PlaceholderText = "Mật khẩu";

            // btnGuest
            this.btnGuest.Location = new System.Drawing.Point(200, 325);
            this.btnGuest.Size = new System.Drawing.Size(100, 36);
            this.btnGuest.Text = "Khách";

            // btnLogin
            this.btnLogin.Location = new System.Drawing.Point(320, 325);
            this.btnLogin.Size = new System.Drawing.Size(100, 36);
            this.btnLogin.Text = "Đăng nhập";
            this.btnLogin.Click += new System.EventHandler(this.btnLogin_Click);

            // btnRegister
            this.btnRegister.Location = new System.Drawing.Point(440, 325);
            this.btnRegister.Size = new System.Drawing.Size(100, 36);
            this.btnRegister.Text = "Đăng ký";

            // Form
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(700, 450);
            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.lblSubtitle);
            this.Controls.Add(this.picLogo);
            this.Controls.Add(this.lblUsername);
            this.Controls.Add(this.lblPassword);
            this.Controls.Add(this.txtUsername);
            this.Controls.Add(this.txtPassword);
            this.Controls.Add(this.btnGuest);
            this.Controls.Add(this.btnLogin);
            this.Controls.Add(this.btnRegister);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Đăng nhập";
            ((System.ComponentModel.ISupportInitialize)(this.picLogo)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
        #endregion
    private System.Windows.Forms.Label lblTitle;
    private System.Windows.Forms.Label lblSubtitle;
    private System.Windows.Forms.PictureBox picLogo;
    private System.Windows.Forms.Label lblUsername;
    private System.Windows.Forms.Label lblPassword;
    private System.Windows.Forms.TextBox txtUsername;
    private System.Windows.Forms.TextBox txtPassword;
    private System.Windows.Forms.Button btnGuest;
    private System.Windows.Forms.Button btnLogin;
    private System.Windows.Forms.Button btnRegister;
    }
}
