using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace ClinicManagementSystem
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // Initializing the database and return the success message
            if (DatabaseHelper.InitializeDatabase())
            {
                MessageBox.Show("Database initialized successfully!", "Success",
                              MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            Application.Run(new HomePageForm());
        }

        // Helper class for database and security matters
        public static class DatabaseHelper
        {
            private static string connectionString = @"Data Source=.;Initial Catalog=ClinicDB;Integrated Security=True;TrustServerCertificate=True";

            public static SqlConnection GetConnection()
            {
                return new SqlConnection(connectionString);
            }

            public static string HashPassword(string password)
            {
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                    StringBuilder builder = new StringBuilder();
                    foreach (byte b in hashBytes)
                    {
                        builder.Append(b.ToString("x2"));
                    }
                    return builder.ToString();
                }
            }

            public static async Task SendEmailAsync(string toEmail, string subject, string body, string fromEmail = "refilwe.maphakisa17@gmail.com", string fromPassword = "iltm neim bixe gann")
            {
                try
                {
                    var smtpClient = new SmtpClient("smtp.gmail.com")
                    {
                        Port = 587,
                        Credentials = new System.Net.NetworkCredential(fromEmail, fromPassword),
                        EnableSsl = true,
                    };

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(fromEmail),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = true,
                    };
                    mailMessage.To.Add(toEmail);

                    await smtpClient.SendMailAsync(mailMessage);
                }
                catch (Exception ex)
                {
                    // Log error
                    MessageBox.Show($"Email failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            public static bool InitializeDatabase()
            {
                try
                {
                    using (var conn = GetConnection())
                    {
                        conn.Open();
                        // if tables don't exist, create them, it includes the insert statements code right after
                        string createTablesQuery = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Roles' AND xtype='U')
                CREATE TABLE Roles (
                    RoleID INT IDENTITY(1,1) PRIMARY KEY,
                    RoleName NVARCHAR(50) NOT NULL
                );
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
                CREATE TABLE Users (
                    UserID INT IDENTITY(1,1) PRIMARY KEY,
                    Username NVARCHAR(50) UNIQUE NOT NULL,
                    HashedPassword NVARCHAR(255) NOT NULL,
                    FullName NVARCHAR(100) NOT NULL,
                    Contact NVARCHAR(50),
                    RoleID INT FOREIGN KEY REFERENCES Roles(RoleID)
                );
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Patients' AND xtype='U')
                CREATE TABLE Patients (
                    PatientID INT IDENTITY(1,1) PRIMARY KEY,
                    UserID INT FOREIGN KEY REFERENCES Users(UserID),
                    StudentID NVARCHAR(50),
                    DateOfBirth DATE,
                    EmergencyContact NVARCHAR(50),
                    Weight DECIMAL(5,2),
                    Height DECIMAL(5,2),
                    HeartRate INT
                );
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Appointments' AND xtype='U')
                CREATE TABLE Appointments (
                    AppointmentID INT IDENTITY(1,1) PRIMARY KEY,
                    PatientID INT FOREIGN KEY REFERENCES Users(UserID),
                    ProviderID INT FOREIGN KEY REFERENCES Users(UserID),
                    AppointmentDate DATE NOT NULL,
                    TimeSlot TIME NOT NULL,
                    Reason NVARCHAR(255),
                    Status NVARCHAR(20) DEFAULT 'Scheduled'
                );
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Consultations' AND xtype='U')
                CREATE TABLE Consultations (
                    ConsultationID INT IDENTITY(1,1) PRIMARY KEY,
                    AppointmentID INT FOREIGN KEY REFERENCES Appointments(AppointmentID),
                    Temperature DECIMAL(5,2),
                    BloodPressure NVARCHAR(20),
                    HeartRate INT,
                    Weight DECIMAL(5,2),
                    Height DECIMAL(5,2),
                    Notes NVARCHAR(MAX),
                    Diagnosis NVARCHAR(255),
                    ConsultationDate DATETIME DEFAULT GETDATE()
                );
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Prescriptions' AND xtype='U')
                CREATE TABLE Prescriptions (
                    PrescriptionID INT IDENTITY(1,1) PRIMARY KEY,
                    ConsultationID INT FOREIGN KEY REFERENCES Consultations(ConsultationID),
                    Medication NVARCHAR(100),
                    Dosage NVARCHAR(50),
                    Frequency NVARCHAR(50),
                    Duration NVARCHAR(50)
                );
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ProviderAvailability' AND xtype='U')
                CREATE TABLE ProviderAvailability (
                    AvailabilityID INT IDENTITY(1,1) PRIMARY KEY,
                    ProviderID INT FOREIGN KEY REFERENCES Users(UserID),
                    DayOfWeek NVARCHAR(20) NOT NULL,
                    StartTime TIME NOT NULL,
                    EndTime TIME NOT NULL,
                    IsActive BIT DEFAULT 1
                );
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Medications' AND xtype='U')
                CREATE TABLE Medications (
                    MedicationID INT IDENTITY(1,1) PRIMARY KEY,
                    MedicationName NVARCHAR(100) NOT NULL,
                    Dosage NVARCHAR(50),
                    Instructions NVARCHAR(255),
                    CreatedBy INT FOREIGN KEY REFERENCES Users(UserID),
                    CreatedDate DATETIME DEFAULT GETDATE()
                );";
                        using (var cmd = new SqlCommand(createTablesQuery, conn))
                        {
                            cmd.ExecuteNonQuery();
                        }

                        string insertDataQuery = @"
                IF NOT EXISTS (SELECT 1 FROM Roles)
                BEGIN
                    INSERT INTO Roles (RoleName) VALUES
                    ('Administrator'),
                    ('Healthcare Provider'),
                    ('Student');
                END
                IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'admin')
                BEGIN
                    INSERT INTO Users (Username, HashedPassword, FullName, Contact, RoleID)
                    VALUES ('admin', '8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918', 'System Administrator', 'admin@bothouniversity.com', 1);
                END
                IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'Violet')
                BEGIN
                    INSERT INTO Users (Username, HashedPassword, FullName, Contact, RoleID)
                    VALUES ('Violet', '8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918', 'Dr. Violet Nkwali', 'v.nkwali@bothouniversity.com', 2);
                END
                IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'Itumeleng')
                BEGIN
                    INSERT INTO Users (Username, HashedPassword, FullName, Contact, RoleID)
                    VALUES ('Itumeleng', '8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918', 'Itumeleng Ntiisa', 'itu.ntiisa@bothouniversity.com', 2);
                END
                
                IF NOT EXISTS (SELECT 1 FROM ProviderAvailability)
                BEGIN
                    INSERT INTO ProviderAvailability (ProviderID, DayOfWeek, StartTime, EndTime)
                    VALUES
                    (2, 'Monday', '09:00', '17:00'),
                    (2, 'Tuesday', '09:00', '17:00'),
                    (2, 'Wednesday', '09:00', '17:00'),
                    (2, 'Thursday', '09:00', '17:00'),
                    (2, 'Friday', '09:00', '17:00'),
                    (3, 'Monday', '08:00', '16:00'),
                    (3, 'Tuesday', '08:00', '16:00'),
                    (3, 'Wednesday', '08:00', '16:00'),
                    (3, 'Thursday', '08:00', '16:00'),
                    (3, 'Friday', '08:00', '16:00');
                END
                
                IF NOT EXISTS (SELECT 1 FROM Medications)
                BEGIN
                    INSERT INTO Medications (MedicationName, Dosage, Instructions, CreatedBy)
                    VALUES
                    ('Amoxicillin', '500mg', 'Three times daily after meals', 1),
                    ('Ibuprofen', '400mg', 'As needed for pain, every 6 hours', 1),
                    ('Paracetamol', '500mg', 'Every 4-6 hours as needed', 1);
                END";
                        using (var cmd = new SqlCommand(insertDataQuery, conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Database initialization failed: {ex.Message}", "Error",
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
        }

        // it gets the info of the currently logged in user
        public static class CurrentUser
        {
            public static int UserID { get; set; }
            public static string Role { get; set; }
            public static string FullName { get; set; }
            public static string Contact { get; set; }
        }

        // the colors/theme of the system
        public static class AppColors
        {
            public static Color NavyBlue = Color.FromArgb(32, 42, 68); // #202A44 color specifically
            public static Color White = Color.White;
            public static Color LightBlue = Color.FromArgb(173, 216, 230);
            public static Color DarkRed = Color.FromArgb(139, 0, 0);
            public static Color LightGray = Color.FromArgb(240, 240, 240);
        }

        // Adding a Home page with a logo for Botho University for a more beautiful logical system
        public class HomePageForm : Form
        {
            private Panel headerPanel;
            private Panel mainPanel;
            private Panel footerPanel;
            private Button btnLogin;
            private PictureBox logoPictureBox;

            public HomePageForm()
            {
                this.Text = "Botho University Online Clinic";
                this.Size = new Size(1000, 700);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.White;
                InitializeHomePage();
            }

            private void InitializeHomePage()
            {
                headerPanel = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 80,
                    BackColor = AppColors.NavyBlue
                };

                logoPictureBox = new PictureBox
                {
                    Size = new Size(60, 60),
                    Location = new Point(20, 10),
                    BackColor = Color.Transparent,
                    Image = CreateRedBothoLogo(),
                    SizeMode = PictureBoxSizeMode.Zoom
                };

                Label lblTitle = new Label
                {
                    Text = "BOTHO UNIVERSITY\nONLINE CLINIC",
                    Location = new Point(90, 20),
                    Size = new Size(400, 40),
                    Font = new Font("Arial", 14, FontStyle.Bold),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleLeft
                };

                headerPanel.Controls.AddRange(new Control[] { logoPictureBox, lblTitle });

                mainPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = AppColors.LightGray,
                    AutoScroll = true
                };

                Panel heroPanel = new Panel
                {
                    Location = new Point(50, 30),
                    Size = new Size(900, 300),
                    BackColor = AppColors.NavyBlue,
                    ForeColor = AppColors.White
                };

                Label lblHeroTitle = new Label
                {
                    Text = "Welcome to Botho University Clinic",
                    Location = new Point(50, 50),
                    Size = new Size(800, 40),
                    Font = new Font("Arial", 24, FontStyle.Bold),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleLeft
                };

                Label lblHeroSubtitle = new Label
                {
                    Text = "Best healthcare services for students and staff",
                    Location = new Point(50, 100),
                    Size = new Size(600, 30),
                    Font = new Font("Arial", 14, FontStyle.Regular),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleLeft
                };

                heroPanel.Controls.AddRange(new Control[] { lblHeroTitle, lblHeroSubtitle });

                // system Features
                Label lblFeatures = new Label
                {
                    Text = "Our Services",
                    Location = new Point(50, 360),
                    Size = new Size(200, 30),
                    Font = new Font("Arial", 18, FontStyle.Bold),
                    ForeColor = AppColors.NavyBlue
                };

                Panel feature1 = CreateFeatureCard("Appointment Booking", "Book appointments with healthcare providers", new Point(50, 410));
                Panel feature2 = CreateFeatureCard("Medical Records", "Access your complete medical history", new Point(320, 410));
                Panel feature3 = CreateFeatureCard("Consultations", "View past consultations and diagnoses", new Point(590, 410));

                btnLogin = new Button
                {
                    Text = "Login",
                    Location = new Point(350, 550),
                    Size = new Size(150, 45),
                    BackColor = AppColors.DarkRed,
                    ForeColor = AppColors.White,
                    Font = new Font("Arial", 12, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat
                };
                btnLogin.Click += (s, e) => { this.Hide(); new LoginForm().Show(); };

                mainPanel.Controls.AddRange(new Control[] {
                    heroPanel, lblFeatures, feature1, feature2, feature3, btnLogin
                });

                footerPanel = new Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = 50,
                    BackColor = AppColors.NavyBlue
                };

                Label lblFooter = new Label
                {
                    Text = "© 2025 Botho University Clinic Management System",
                    Dock = DockStyle.Fill,
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                footerPanel.Controls.Add(lblFooter);

                // Adding panels to form
                this.Controls.AddRange(new Control[] { headerPanel, mainPanel, footerPanel });
            }

            private Panel CreateFeatureCard(string title, string description, Point location)
            {
                Panel card = new Panel
                {
                    Location = location,
                    Size = new Size(250, 120),
                    BackColor = AppColors.White,
                    BorderStyle = BorderStyle.FixedSingle,
                    Padding = new Padding(10)
                };

                Label lblTitle = new Label
                {
                    Text = title,
                    Location = new Point(10, 10),
                    Size = new Size(230, 25),
                    Font = new Font("Arial", 12, FontStyle.Bold),
                    ForeColor = AppColors.NavyBlue
                };

                Label lblDesc = new Label
                {
                    Text = description,
                    Location = new Point(10, 40),
                    Size = new Size(230, 60),
                    Font = new Font("Arial", 9, FontStyle.Regular),
                    ForeColor = Color.DarkGray
                };

                card.Controls.AddRange(new Control[] { lblTitle, lblDesc });
                return card;
            }

            private Image CreateRedBothoLogo()
            {
                Bitmap bmp = new Bitmap(60, 60);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.FromArgb(220, 0, 0));
                    using (Font font = new Font("Arial", 16, FontStyle.Bold))
                    using (Brush whiteBrush = new SolidBrush(Color.White))
                    {
                        StringFormat format = new StringFormat();
                        format.Alignment = StringAlignment.Center;
                        format.LineAlignment = StringAlignment.Center;
                        g.DrawString("BU", font, whiteBrush, new RectangleF(0, 0, 60, 60), format);
                    }
                    using (Pen whitePen = new Pen(Color.White, 2))
                    {
                        g.DrawRectangle(whitePen, 1, 1, 57, 57);
                    }
                }
                return bmp;
            }
        }

        public class LoginForm : Form
        {
            private TextBox txtUsername;
            private TextBox txtPassword;
            private Button btnLogin;
            private Label lblError;
            private LinkLabel lnkBackToHome;

            public LoginForm()
            {
                this.Text = "Clinic Management System Login";
                this.Size = new Size(400, 350);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;

                Label lblTitle = new Label
                {
                    Text = "CLINIC MANAGEMENT SYSTEM",
                    Location = new Point(80, 30),
                    Size = new Size(250, 30),
                    Font = new Font("Arial", 14, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = AppColors.White
                };

                Label lblUsername = new Label
                {
                    Text = "Username:",
                    Location = new Point(50, 90),
                    Size = new Size(80, 20),
                    ForeColor = AppColors.White
                };

                txtUsername = new TextBox
                {
                    Location = new Point(140, 90),
                    Size = new Size(180, 25)
                };

                Label lblPassword = new Label
                {
                    Text = "Password:",
                    Location = new Point(50, 130),
                    Size = new Size(80, 20),
                    ForeColor = AppColors.White
                };

                txtPassword = new TextBox
                {
                    Location = new Point(140, 130),
                    Size = new Size(180, 25)
                };
                txtPassword.UseSystemPasswordChar = true;

                btnLogin = new Button
                {
                    Text = "LOGIN",
                    Location = new Point(140, 180),
                    Size = new Size(100, 35),
                    BackColor = AppColors.DarkRed,
                    ForeColor = AppColors.White,
                    Font = new Font("Arial", 10, FontStyle.Bold)
                };

                lblError = new Label
                {
                    Location = new Point(50, 230),
                    Size = new Size(300, 20),
                    ForeColor = Color.Red,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                lnkBackToHome = new LinkLabel
                {
                    Text = "Home",
                    Location = new Point(140, 260),
                    Size = new Size(100, 20),
                    ForeColor = AppColors.LightBlue,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                // Adding controls to form
                this.Controls.Add(lblTitle);
                this.Controls.Add(lblUsername);
                this.Controls.Add(txtUsername);
                this.Controls.Add(lblPassword);
                this.Controls.Add(txtPassword);
                this.Controls.Add(btnLogin);
                this.Controls.Add(lblError);
                this.Controls.Add(lnkBackToHome);

                // Event handler
                btnLogin.Click += BtnLogin_Click;
                lnkBackToHome.Click += (s, e) => { this.Hide(); new HomePageForm().Show(); };
                this.AcceptButton = btnLogin;
            }

            private void BtnLogin_Click(object sender, EventArgs e)
            {
                string username = txtUsername.Text.Trim();
                string password = txtPassword.Text.Trim();

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    lblError.Text = "Please enter username and password.";
                    return;
                }

                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = @"SELECT UserID, RoleName, FullName, Contact
                                       FROM Users u JOIN Roles r ON u.RoleID = r.RoleID
                                       WHERE Username = @Username AND HashedPassword = @HashedPassword";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@Username", username);
                            cmd.Parameters.AddWithValue("@HashedPassword", DatabaseHelper.HashPassword(password));
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    CurrentUser.UserID = reader.GetInt32(0);
                                    CurrentUser.Role = reader.GetString(1);
                                    CurrentUser.FullName = reader.GetString(2);
                                    CurrentUser.Contact = reader.IsDBNull(3) ? "" : reader.GetString(3);

                                    MessageBox.Show($"Hello! Welcome {CurrentUser.FullName}!", "You have successfully logged in",
                                                  MessageBoxButtons.OK, MessageBoxIcon.Information);

                                    if (CurrentUser.Role == "Administrator")
                                        new AdminDashboardForm().Show();
                                    else if (CurrentUser.Role == "Healthcare Provider")
                                        new ProviderDashboardForm().Show();
                                    else if (CurrentUser.Role == "Student")
                                        new StudentDashboardForm().Show();

                                    this.Hide();
                                }
                                else
                                {
                                    lblError.Text = "Invalid username or password.";
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message, "Database Error",
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // Profile Management Form available to every user to manage their own personal profiles
        public class ManageProfileForm : Form
        {
            private TextBox txtFullName, txtContact, txtCurrentPassword, txtNewPassword, txtConfirmPassword;
            private Button btnSaveProfile, btnChangePassword;

            public ManageProfileForm()
            {
                this.Text = "Manage My Profile";
                this.Size = new Size(500, 400);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;

                Label lblTitle = new Label
                {
                    Text = "MANAGE PROFILE",
                    Location = new Point(150, 20),
                    Size = new Size(200, 30),
                    Font = new Font("Arial", 14, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = AppColors.White
                };

                GroupBox grpProfile = new GroupBox
                {
                    Text = "Personal Information",
                    Location = new Point(30, 60),
                    Size = new Size(440, 100),
                    ForeColor = AppColors.White,
                    Font = new Font("Arial", 9, FontStyle.Bold)
                };

                Label lblFullName = new Label { Text = "Full Name:", Location = new Point(20, 30), Size = new Size(80, 20), ForeColor = AppColors.White };
                txtFullName = new TextBox { Location = new Point(110, 30), Size = new Size(200, 20), Text = CurrentUser.FullName };

                Label lblContact = new Label { Text = "Contact:", Location = new Point(20, 60), Size = new Size(80, 20), ForeColor = AppColors.White };
                txtContact = new TextBox { Location = new Point(110, 60), Size = new Size(200, 20), Text = CurrentUser.Contact };

                grpProfile.Controls.AddRange(new Control[] { lblFullName, txtFullName, lblContact, txtContact });

                GroupBox grpPassword = new GroupBox
                {
                    Text = "Change Password",
                    Location = new Point(30, 180),
                    Size = new Size(440, 150),
                    ForeColor = AppColors.White,
                    Font = new Font("Arial", 9, FontStyle.Bold)
                };

                Label lblCurrentPassword = new Label { Text = "Current Password:", Location = new Point(20, 30), Size = new Size(120, 20), ForeColor = AppColors.White };
                txtCurrentPassword = new TextBox { Location = new Point(150, 30), Size = new Size(200, 20), UseSystemPasswordChar = true };

                Label lblNewPassword = new Label { Text = "New Password:", Location = new Point(20, 60), Size = new Size(120, 20), ForeColor = AppColors.White };
                txtNewPassword = new TextBox { Location = new Point(150, 60), Size = new Size(200, 20), UseSystemPasswordChar = true };

                Label lblConfirmPassword = new Label { Text = "Confirm Password:", Location = new Point(20, 90), Size = new Size(120, 20), ForeColor = AppColors.White };
                txtConfirmPassword = new TextBox { Location = new Point(150, 90), Size = new Size(200, 20), UseSystemPasswordChar = true };

                grpPassword.Controls.AddRange(new Control[] { lblCurrentPassword, txtCurrentPassword, lblNewPassword, txtNewPassword, lblConfirmPassword, txtConfirmPassword });

                btnSaveProfile = new Button
                {
                    Text = "Save Profile",
                    Location = new Point(100, 350),
                    Size = new Size(120, 35),
                    BackColor = AppColors.DarkRed,
                    ForeColor = AppColors.White
                };

                btnChangePassword = new Button
                {
                    Text = "Change Password",
                    Location = new Point(250, 350),
                    Size = new Size(120, 35),
                    BackColor = AppColors.DarkRed,
                    ForeColor = AppColors.White
                };

                this.Controls.Add(lblTitle);
                this.Controls.Add(grpProfile);
                this.Controls.Add(grpPassword);
                this.Controls.Add(btnSaveProfile);
                this.Controls.Add(btnChangePassword);

                btnSaveProfile.Click += BtnSaveProfile_Click;
                btnChangePassword.Click += BtnChangePassword_Click;
            }

            private void BtnSaveProfile_Click(object sender, EventArgs e)
            {
                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = "UPDATE Users SET FullName = @FullName, Contact = @Contact WHERE UserID = @UserID";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@FullName", txtFullName.Text.Trim());
                            cmd.Parameters.AddWithValue("@Contact", txtContact.Text.Trim());
                            cmd.Parameters.AddWithValue("@UserID", CurrentUser.UserID);
                            int rowsAffected = cmd.ExecuteNonQuery();
                            if (rowsAffected > 0)
                            {
                                CurrentUser.FullName = txtFullName.Text.Trim();
                                CurrentUser.Contact = txtContact.Text.Trim();
                                MessageBox.Show("Profile updated successfully!", "Success",
                                              MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error updating profile: " + ex.Message, "Error",
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            private void BtnChangePassword_Click(object sender, EventArgs e)
            {
                if (string.IsNullOrEmpty(txtCurrentPassword.Text) ||
                    string.IsNullOrEmpty(txtNewPassword.Text) ||
                    string.IsNullOrEmpty(txtConfirmPassword.Text))
                {
                    MessageBox.Show("Please fill all password fields.", "Validation Error",
                                  MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (txtNewPassword.Text != txtConfirmPassword.Text)
                {
                    MessageBox.Show("New password and confirmation do not match.", "Validation Error",
                                  MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (txtNewPassword.Text.Length < 6)
                {
                    MessageBox.Show("New password must be at least 6 characters long.", "Validation Error",
                                  MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();

                        string verifyQuery = "SELECT UserID FROM Users WHERE UserID = @UserID AND HashedPassword = @CurrentPassword";
                        using (SqlCommand verifyCmd = new SqlCommand(verifyQuery, conn))
                        {
                            verifyCmd.Parameters.AddWithValue("@UserID", CurrentUser.UserID);
                            verifyCmd.Parameters.AddWithValue("@CurrentPassword", DatabaseHelper.HashPassword(txtCurrentPassword.Text));
                            object result = verifyCmd.ExecuteScalar();
                            if (result == null)
                            {
                                MessageBox.Show("Current password is incorrect.", "Validation Error",
                                              MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                return;
                            }
                        }

                        string updateQuery = "UPDATE Users SET HashedPassword = @NewPassword WHERE UserID = @UserID";
                        using (SqlCommand updateCmd = new SqlCommand(updateQuery, conn))
                        {
                            updateCmd.Parameters.AddWithValue("@NewPassword", DatabaseHelper.HashPassword(txtNewPassword.Text));
                            updateCmd.Parameters.AddWithValue("@UserID", CurrentUser.UserID);
                            updateCmd.ExecuteNonQuery();

                            try
                            {
                                // Send password change confirmation email tio shiw that the password has been changed successfully
                                _ = DatabaseHelper.SendEmailAsync(
                                    CurrentUser.Contact,
                                    "Password Changed Successfully - Botho University Clinic",
                                    $"Dear {CurrentUser.FullName},<br><br>" +
                                    "Your password has been successfully changed.<br><br>" +
                                    "If you did not make this change, please contact the clinic immediately.<br><br>" +
                                    "Best regards,<br>Botho University Clinic Team"
                                );
                            }
                            catch (Exception emailEx)
                            {
                                Console.WriteLine($"Email failed: {emailEx.Message}");
                            }

                            // EXISTING SUCCESS MESSAGE
                            MessageBox.Show("Password changed successfully!", "Success",
                                          MessageBoxButtons.OK, MessageBoxIcon.Information);

                            txtCurrentPassword.Clear();
                            txtNewPassword.Clear();
                            txtConfirmPassword.Clear();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error changing password: " + ex.Message, "Error",
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        public class StudentDashboardForm : Form
        {
            private Panel panelStats;
            private Panel panelQuickActions;
            private Label lblFooter;
            private Button btnLogout;

            public StudentDashboardForm()
            {
                this.Text = $"Student Dashboard - Welcome {CurrentUser.FullName}";
                this.Size = new Size(800, 600);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;

                Label lblHeader = new Label
                {
                    Text = $"Student Dashboard - {CurrentUser.FullName}",
                    Location = new Point(20, 20),
                    Size = new Size(400, 30),
                    Font = new Font("Arial", 16, FontStyle.Bold),
                    ForeColor = AppColors.White
                };

                panelStats = new Panel
                {
                    Location = new Point(20, 70),
                    Size = new Size(760, 120),
                    BackColor = AppColors.LightGray,
                    BorderStyle = BorderStyle.FixedSingle
                };

                panelQuickActions = new Panel
                {
                    Location = new Point(20, 210),
                    Size = new Size(760, 300),
                    BackColor = AppColors.LightGray,
                    BorderStyle = BorderStyle.FixedSingle
                };

                btnLogout = new Button
                {
                    Text = "Logout",
                    Location = new Point(680, 20),
                    Size = new Size(80, 30),
                    BackColor = AppColors.DarkRed,
                    ForeColor = AppColors.White,
                    Font = new Font("Arial", 9, FontStyle.Bold)
                };

                lblFooter = new Label
                {
                    Text = "© 2025 - Botho University Clinic - Privacy",
                    Location = new Point(300, 530),
                    Size = new Size(300, 20),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                this.Controls.Add(lblHeader);
                this.Controls.Add(panelStats);
                this.Controls.Add(panelQuickActions);
                this.Controls.Add(btnLogout);
                this.Controls.Add(lblFooter);

                btnLogout.Click += (s, e) => { this.Close(); new LoginForm().Show(); };
                LoadDashboard();
            }

            private void LoadDashboard()
            {
                LoadStatistics();
                LoadQuickActions();
            }

            private void LoadStatistics()
            {
                panelStats.Controls.Clear();

                Label lblStatsTitle = new Label
                {
                    Text = "My Health Overview",
                    Location = new Point(10, 10),
                    Size = new Size(200, 20),
                    Font = new Font("Arial", 12, FontStyle.Bold),
                    ForeColor = Color.Black
                };
                panelStats.Controls.Add(lblStatsTitle);

                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = @"SELECT
                                        (SELECT COUNT(*) FROM Appointments WHERE PatientID = @PatientID) as TotalAppointments,
                                        (SELECT COUNT(*) FROM Appointments WHERE PatientID = @PatientID AND Status = 'Scheduled') as ScheduledAppointments,
                                        (SELECT COUNT(*) FROM Appointments WHERE PatientID = @PatientID AND AppointmentDate >= GETDATE()) as UpcomingAppointments";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@PatientID", CurrentUser.UserID);
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    int total = reader.GetInt32(0);
                                    int scheduled = reader.GetInt32(1);
                                    int upcoming = reader.GetInt32(2);

                                    CreateStatBox("Total Appointments", total.ToString(), 20, 40, panelStats);
                                    CreateStatBox("Scheduled", scheduled.ToString(), 180, 40, panelStats);
                                    CreateStatBox("Upcoming", upcoming.ToString(), 340, 40, panelStats);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Label lblError = new Label
                    {
                        Text = "Error loading statistics",
                        Location = new Point(20, 40),
                        Size = new Size(200, 20),
                        ForeColor = Color.Red
                    };
                    panelStats.Controls.Add(lblError);
                }
            }

            private void LoadQuickActions()
            {
                panelQuickActions.Controls.Clear();

                Label lblActionsTitle = new Label
                {
                    Text = "Quick Actions",
                    Location = new Point(10, 10),
                    Size = new Size(200, 20),
                    Font = new Font("Arial", 12, FontStyle.Bold),
                    ForeColor = Color.Black
                };
                panelQuickActions.Controls.Add(lblActionsTitle);

                string[] actions = {
                    "Book Appointment",
                    "View Medical History",
                    "Manage Profile",
                    "View Upcoming Appointments",
                    "My Consultations"
                };

                int yPos = 50;
                foreach (string action in actions)
                {
                    Button btnAction = new Button
                    {
                        Text = action,
                        Location = new Point(20, yPos),
                        Size = new Size(200, 40),
                        BackColor = AppColors.DarkRed,
                        ForeColor = AppColors.White,
                        Font = new Font("Arial", 10, FontStyle.Bold),
                        Tag = action
                    };
                    btnAction.Click += QuickActionButton_Click;
                    panelQuickActions.Controls.Add(btnAction);
                    yPos += 60;
                }
            }

            private void CreateStatBox(string title, string value, int x, int y, Panel parent)
            {
                Panel statBox = new Panel
                {
                    Location = new Point(x, y),
                    Size = new Size(150, 60),
                    BackColor = AppColors.LightBlue,
                    BorderStyle = BorderStyle.FixedSingle
                };

                Label lblValue = new Label
                {
                    Text = value,
                    Location = new Point(10, 10),
                    Size = new Size(130, 25),
                    Font = new Font("Arial", 16, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.Black
                };

                Label lblTitle = new Label
                {
                    Text = title,
                    Location = new Point(10, 35),
                    Size = new Size(130, 20),
                    Font = new Font("Arial", 8, FontStyle.Regular),
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.Black
                };

                statBox.Controls.Add(lblValue);
                statBox.Controls.Add(lblTitle);
                parent.Controls.Add(statBox);
            }

            private void QuickActionButton_Click(object sender, EventArgs e)
            {
                Button button = (Button)sender;
                string action = button.Tag.ToString();

                switch (action)
                {
                    case "Book Appointment":
                        new BookAppointmentForm().ShowDialog();
                        LoadDashboard();
                        break;
                    case "View Medical History":
                        new AppointmentHistoryForm().ShowDialog();
                        break;
                    case "Manage Profile":
                        new ManageProfileForm().ShowDialog();
                        LoadDashboard();
                        break;
                    case "View Upcoming Appointments":
                        new StudentUpcomingAppointmentsForm().ShowDialog();
                        break;
                    case "My Consultations":
                        new StudentConsultationsForm().ShowDialog();
                        break;
                }
            }
        }

        //form for students and nurses to see their upcoming appointments
        public class StudentUpcomingAppointmentsForm : Form
        {
            private DataGridView dgvUpcoming;

            public StudentUpcomingAppointmentsForm()
            {
                this.Text = "My Upcoming Appointments";
                this.Size = new Size(700, 500);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;

                Label lblTitle = new Label
                {
                    Text = "MY UPCOMING APPOINTMENTS",
                    Location = new Point(200, 20),
                    Size = new Size(300, 25),
                    Font = new Font("Arial", 14, FontStyle.Bold),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                dgvUpcoming = new DataGridView
                {
                    Location = new Point(50, 60),
                    Size = new Size(600, 350),
                    BackgroundColor = Color.White,
                    ForeColor = Color.Black,
                    ReadOnly = true,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
                };

                this.Controls.Add(lblTitle);
                this.Controls.Add(dgvUpcoming);
                LoadUpcomingAppointments();
            }

            private void LoadUpcomingAppointments()
            {
                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = @"SELECT a.AppointmentDate AS Date, a.TimeSlot AS Time,
                                        u.FullName AS Provider, a.Reason, a.Status
                                        FROM Appointments a
                                        JOIN Users u ON a.ProviderID = u.UserID
                                        WHERE a.PatientID = @PatientID
                                        AND a.AppointmentDate >= CAST(GETDATE() AS DATE)
                                        ORDER BY a.AppointmentDate, a.TimeSlot";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@PatientID", CurrentUser.UserID);
                            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                            {
                                DataTable dt = new DataTable();
                                adapter.Fill(dt);
                                dgvUpcoming.DataSource = dt;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading upcoming appointments: " + ex.Message);
                }
            }
        }

        public class StudentConsultationsForm : Form
        {
            private DataGridView dgvConsultations;

            public StudentConsultationsForm()
            {
                this.Text = "My Consultations";
                this.Size = new Size(800, 500);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;

                Label lblTitle = new Label
                {
                    Text = "MY CONSULTATIONS",
                    Location = new Point(300, 20),
                    Size = new Size(200, 25),
                    Font = new Font("Arial", 14, FontStyle.Bold),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                dgvConsultations = new DataGridView
                {
                    Location = new Point(50, 60),
                    Size = new Size(700, 350),
                    BackgroundColor = Color.White,
                    ForeColor = Color.Black,
                    ReadOnly = true,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
                };

                this.Controls.Add(lblTitle);
                this.Controls.Add(dgvConsultations);
                LoadConsultations();
            }

            private void LoadConsultations()
            {
                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = @"SELECT a.AppointmentDate AS Date, a.TimeSlot AS Time,
                                        u.FullName AS Provider, c.Diagnosis, c.Temperature,
                                        c.BloodPressure, c.HeartRate, c.Weight, c.Height, c.Notes
                                        FROM Consultations c
                                        JOIN Appointments a ON c.AppointmentID = a.AppointmentID
                                        JOIN Users u ON a.ProviderID = u.UserID
                                        WHERE a.PatientID = @PatientID
                                        ORDER BY a.AppointmentDate DESC";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@PatientID", CurrentUser.UserID);
                            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                            {
                                DataTable dt = new DataTable();
                                adapter.Fill(dt);
                                dgvConsultations.DataSource = dt;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading consultations: " + ex.Message);
                }
            }
        }

        public class ProviderDashboardForm : Form
        {
            private Panel panelStats;
            private Panel panelQuickActions;
            private Label lblFooter;
            private Button btnLogout;

            public ProviderDashboardForm()
            {
                this.Text = $"Healthcare Provider Dashboard - {CurrentUser.FullName}";
                this.Size = new Size(800, 600);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;

                Label lblHeader = new Label
                {
                    Text = $"Healthcare Provider Dashboard - {CurrentUser.FullName}",
                    Location = new Point(20, 20),
                    Size = new Size(400, 30),
                    Font = new Font("Arial", 16, FontStyle.Bold),
                    ForeColor = AppColors.White
                };

                panelStats = new Panel
                {
                    Location = new Point(20, 70),
                    Size = new Size(760, 120),
                    BackColor = AppColors.LightGray,
                    BorderStyle = BorderStyle.FixedSingle
                };

                panelQuickActions = new Panel
                {
                    Location = new Point(20, 210),
                    Size = new Size(760, 300),
                    BackColor = AppColors.LightGray,
                    BorderStyle = BorderStyle.FixedSingle
                };

                btnLogout = new Button
                {
                    Text = "Logout",
                    Location = new Point(680, 20),
                    Size = new Size(80, 30),
                    BackColor = AppColors.DarkRed,
                    ForeColor = AppColors.White,
                    Font = new Font("Arial", 9, FontStyle.Bold)
                };

                lblFooter = new Label
                {
                    Text = "© 2025 - Botho University Clinic - Privacy",
                    Location = new Point(300, 530),
                    Size = new Size(300, 20),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                // Adding controls
                this.Controls.Add(lblHeader);
                this.Controls.Add(panelStats);
                this.Controls.Add(panelQuickActions);
                this.Controls.Add(btnLogout);
                this.Controls.Add(lblFooter);

                btnLogout.Click += (s, e) => { this.Close(); new LoginForm().Show(); };
                LoadDashboard();
            }

            private void LoadDashboard()
            {
                LoadStatistics();
                LoadQuickActions();
            }

            private void LoadStatistics()
            {
                panelStats.Controls.Clear();

                Label lblStatsTitle = new Label
                {
                    Text = "Practice Overview",
                    Location = new Point(10, 10),
                    Size = new Size(200, 20),
                    Font = new Font("Arial", 12, FontStyle.Bold),
                    ForeColor = Color.Black
                };
                panelStats.Controls.Add(lblStatsTitle);

                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = @"SELECT
                                        (SELECT COUNT(*) FROM Appointments WHERE ProviderID = @ProviderID AND AppointmentDate = CAST(GETDATE() AS DATE)) as TodayAppointments,
                                        (SELECT COUNT(*) FROM Appointments WHERE ProviderID = @ProviderID AND Status = 'Scheduled') as ScheduledAppointments,
                                        (SELECT COUNT(*) FROM Appointments WHERE ProviderID = @ProviderID AND Status = 'Completed' AND AppointmentDate = CAST(GETDATE() AS DATE)) as CompletedToday";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@ProviderID", CurrentUser.UserID);
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    int today = reader.GetInt32(0);
                                    int scheduled = reader.GetInt32(1);
                                    int completed = reader.GetInt32(2);

                                    CreateStatBox("Today's Appointments", today.ToString(), 20, 40, panelStats);
                                    CreateStatBox("Scheduled", scheduled.ToString(), 180, 40, panelStats);
                                    CreateStatBox("Completed Today", completed.ToString(), 340, 40, panelStats);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Label lblError = new Label
                    {
                        Text = "Error loading statistics",
                        Location = new Point(20, 40),
                        Size = new Size(200, 20),
                        ForeColor = Color.Red
                    };
                    panelStats.Controls.Add(lblError);
                }
            }

            private void LoadQuickActions()
            {
                panelQuickActions.Controls.Clear();

                Label lblActionsTitle = new Label
                {
                    Text = "Quick Actions",
                    Location = new Point(10, 10),
                    Size = new Size(200, 20),
                    Font = new Font("Arial", 12, FontStyle.Bold),
                    ForeColor = Color.Black
                };
                panelQuickActions.Controls.Add(lblActionsTitle);

                string[] actions = {
                    "View Patient Records",
                    "Manage Medications",
                    "Generate Reports",
                    "Set Availability",
                    "Emergency Contacts",
                    "System Settings",
                    "View All Appointments",
                    "Manage Profile",
                    "Consultations"
                };

                int xPos = 20;
                int yPos = 50;
                foreach (string action in actions)
                {
                    Button btnAction = new Button
                    {
                        Text = action,
                        Location = new Point(xPos, yPos),
                        Size = new Size(180, 35),
                        BackColor = AppColors.DarkRed,
                        ForeColor = AppColors.White,
                        Font = new Font("Arial", 9, FontStyle.Bold),
                        Tag = action
                    };
                    btnAction.Click += QuickActionButton_Click;
                    panelQuickActions.Controls.Add(btnAction);
                    xPos += 190;
                    if (xPos > 600)
                    {
                        xPos = 20;
                        yPos += 45;
                    }
                }
            }

            private void CreateStatBox(string title, string value, int x, int y, Panel parent)
            {
                Panel statBox = new Panel
                {
                    Location = new Point(x, y),
                    Size = new Size(150, 60),
                    BackColor = AppColors.LightBlue,
                    BorderStyle = BorderStyle.FixedSingle
                };

                Label lblValue = new Label
                {
                    Text = value,
                    Location = new Point(10, 10),
                    Size = new Size(130, 25),
                    Font = new Font("Arial", 16, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.Black
                };

                Label lblTitle = new Label
                {
                    Text = title,
                    Location = new Point(10, 35),
                    Size = new Size(130, 20),
                    Font = new Font("Arial", 8, FontStyle.Regular),
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.Black
                };

                statBox.Controls.Add(lblValue);
                statBox.Controls.Add(lblTitle);
                parent.Controls.Add(statBox);
            }

            private void QuickActionButton_Click(object sender, EventArgs e)
            {
                Button button = (Button)sender;
                string action = button.Tag.ToString();

                switch (action)
                {
                    case "View Patient Records":
                        new PatientRecordsForm().ShowDialog();
                        break;
                    case "Manage Medications":
                        new ManageMedicationsForm().ShowDialog();
                        break;
                    case "Generate Reports":
                        new QuickReportsForm().ShowDialog();
                        break;
                    case "Set Availability":
                        new SetAvailabilityForm().ShowDialog();
                        break;
                    case "Emergency Contacts":
                        new EmergencyContactsForm().ShowDialog();
                        break;
                    case "System Settings":
                        new SystemSettingsForm().ShowDialog();
                        break;
                    case "View All Appointments":
                        new ProviderAppointmentsForm().ShowDialog();
                        break;
                    case "Manage Profile":
                        new ManageProfileForm().ShowDialog();
                        LoadDashboard();
                        break;
                    case "Consultations":
                        new ProviderConsultationsForm().ShowDialog();
                        break;
                }
            }
        }

        public class ProviderConsultationsForm : Form
        {
            private DataGridView dgvConsultations;
            private Button btnAddConsultation;

            public ProviderConsultationsForm()
            {
                this.Text = "Patient Consultations";
                this.Size = new Size(800, 500);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;

                Label lblTitle = new Label
                {
                    Text = "PATIENT CONSULTATIONS",
                    Location = new Point(300, 20),
                    Size = new Size(200, 25),
                    Font = new Font("Arial", 14, FontStyle.Bold),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                dgvConsultations = new DataGridView
                {
                    Location = new Point(50, 60),
                    Size = new Size(700, 300),
                    BackgroundColor = Color.White,
                    ForeColor = Color.Black,
                    ReadOnly = true,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect
                };

                btnAddConsultation = new Button
                {
                    Text = "Add Consultation",
                    Location = new Point(50, 380),
                    Size = new Size(150, 35),
                    BackColor = AppColors.DarkRed,
                    ForeColor = AppColors.White
                };

                this.Controls.Add(lblTitle);
                this.Controls.Add(dgvConsultations);
                this.Controls.Add(btnAddConsultation);

                btnAddConsultation.Click += BtnAddConsultation_Click;
                dgvConsultations.DoubleClick += (s, e) => ViewConsultationDetails();
                LoadConsultations();
            }

            private void LoadConsultations()
            {
                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = @"SELECT c.ConsultationID, u.FullName AS Patient, a.AppointmentDate, c.Diagnosis, c.ConsultationDate
                                        FROM Consultations c
                                        JOIN Appointments a ON c.AppointmentID = a.AppointmentID
                                        JOIN Users u ON a.PatientID = u.UserID
                                        WHERE a.ProviderID = @ProviderID
                                        ORDER BY c.ConsultationDate DESC";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@ProviderID", CurrentUser.UserID);
                            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                            {
                                DataTable dt = new DataTable();
                                adapter.Fill(dt);
                                dgvConsultations.DataSource = dt;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading consultations: " + ex.Message);
                }
            }

            private void BtnAddConsultation_Click(object sender, EventArgs e)
            {
                new AddConsultationForm().ShowDialog();
                LoadConsultations();
            }

            private void ViewConsultationDetails()
            {
                if (dgvConsultations.SelectedRows.Count > 0)
                {
                    int consultationID = Convert.ToInt32(dgvConsultations.SelectedRows[0].Cells["ConsultationID"].Value);
                    new ConsultationDetailsForm(consultationID).ShowDialog();
                }
            }
        }

        public class AddConsultationForm : Form
        {
            private ComboBox cmbAppointments;
            private TextBox txtDiagnosis, txtNotes, txtTemperature, txtBloodPressure, txtHeartRate, txtWeight, txtHeight;
            private DateTimePicker dtpConsultationDate;
            private Button btnSave;
            private Label lblValidation;

            public AddConsultationForm()
            {
                this.Text = "Add Consultation";
                this.Size = new Size(500, 550);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;

                Label lblTitle = new Label
                {
                    Text = "ADD CONSULTATION",
                    Location = new Point(150, 20),
                    Size = new Size(200, 25),
                    Font = new Font("Arial", 14, FontStyle.Bold),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                lblValidation = new Label
                {
                    Location = new Point(30, 450),
                    Size = new Size(400, 20),
                    ForeColor = Color.Red,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                Label lblAppointment = new Label { Text = "Appointment:*", Location = new Point(30, 60), Size = new Size(100, 20), ForeColor = AppColors.White };
                cmbAppointments = new ComboBox { Location = new Point(140, 60), Size = new Size(200, 20), DropDownStyle = ComboBoxStyle.DropDownList };

                Label lblConsultationDate = new Label { Text = "Consultation Date:*", Location = new Point(30, 90), Size = new Size(120, 20), ForeColor = AppColors.White };
                dtpConsultationDate = new DateTimePicker { Location = new Point(160, 90), Size = new Size(120, 20), Value = DateTime.Now };

                Label lblDiagnosis = new Label { Text = "Diagnosis:*", Location = new Point(30, 120), Size = new Size(100, 20), ForeColor = AppColors.White };
                txtDiagnosis = new TextBox { Location = new Point(140, 120), Size = new Size(200, 20) };

                Label lblTemperature = new Label { Text = "Temperature:", Location = new Point(30, 150), Size = new Size(100, 20), ForeColor = AppColors.White };
                txtTemperature = new TextBox { Location = new Point(140, 150), Size = new Size(100, 20), Text = "36.6" };

                Label lblBloodPressure = new Label { Text = "Blood Pressure:", Location = new Point(30, 180), Size = new Size(100, 20), ForeColor = AppColors.White };
                txtBloodPressure = new TextBox { Location = new Point(140, 180), Size = new Size(100, 20), Text = "120/80" };

                Label lblHeartRate = new Label { Text = "Heart Rate:", Location = new Point(30, 210), Size = new Size(100, 20), ForeColor = AppColors.White };
                txtHeartRate = new TextBox { Location = new Point(140, 210), Size = new Size(100, 20), Text = "72" };

                Label lblWeight = new Label { Text = "Weight (kg):", Location = new Point(30, 240), Size = new Size(100, 20), ForeColor = AppColors.White };
                txtWeight = new TextBox { Location = new Point(140, 240), Size = new Size(100, 20) };

                Label lblHeight = new Label { Text = "Height (cm):", Location = new Point(30, 270), Size = new Size(100, 20), ForeColor = AppColors.White };
                txtHeight = new TextBox { Location = new Point(140, 270), Size = new Size(100, 20) };

                Label lblNotes = new Label { Text = "Notes:", Location = new Point(30, 300), Size = new Size(100, 20), ForeColor = AppColors.White };
                txtNotes = new TextBox { Location = new Point(140, 300), Size = new Size(300, 100), Multiline = true };

                btnSave = new Button
                {
                    Text = "Save Consultation",
                    Location = new Point(150, 420),
                    Size = new Size(150, 35),
                    BackColor = AppColors.DarkRed,
                    ForeColor = AppColors.White
                };

                this.Controls.Add(lblTitle);
                this.Controls.Add(lblAppointment);
                this.Controls.Add(cmbAppointments);
                this.Controls.Add(lblConsultationDate);
                this.Controls.Add(dtpConsultationDate);
                this.Controls.Add(lblDiagnosis);
                this.Controls.Add(txtDiagnosis);
                this.Controls.Add(lblTemperature);
                this.Controls.Add(txtTemperature);
                this.Controls.Add(lblBloodPressure);
                this.Controls.Add(txtBloodPressure);
                this.Controls.Add(lblHeartRate);
                this.Controls.Add(txtHeartRate);
                this.Controls.Add(lblWeight);
                this.Controls.Add(txtWeight);
                this.Controls.Add(lblHeight);
                this.Controls.Add(txtHeight);
                this.Controls.Add(lblNotes);
                this.Controls.Add(txtNotes);
                this.Controls.Add(btnSave);
                this.Controls.Add(lblValidation);

                btnSave.Click += BtnSave_Click;
                LoadAppointments();
            }

            private void LoadAppointments()
            {
                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = @"SELECT a.AppointmentID, u.FullName + ' - ' + CONVERT(VARCHAR, a.AppointmentDate) + ' ' + a.TimeSlot as AppointmentInfo
                                        FROM Appointments a
                                        JOIN Users u ON a.PatientID = u.UserID
                                        WHERE a.ProviderID = @ProviderID AND a.Status IN ('Approved', 'Scheduled')
                                        AND NOT EXISTS (SELECT 1 FROM Consultations c WHERE c.AppointmentID = a.AppointmentID)
                                        ORDER BY a.AppointmentDate DESC";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@ProviderID", CurrentUser.UserID);
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                cmbAppointments.Items.Clear();
                                while (reader.Read())
                                {
                                    cmbAppointments.Items.Add(new
                                    {
                                        AppointmentID = reader.GetInt32(0),
                                        Info = reader.GetString(1)
                                    });
                                }
                                cmbAppointments.DisplayMember = "Info";
                                cmbAppointments.ValueMember = "AppointmentID";
                                if (cmbAppointments.Items.Count > 0)
                                    cmbAppointments.SelectedIndex = 0;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading appointments: " + ex.Message);
                }
            }

            private bool ValidateForm()
            {
                if (cmbAppointments.SelectedItem == null)
                {
                    lblValidation.Text = "Please select an appointment.";
                    return false;
                }
                if (string.IsNullOrEmpty(txtDiagnosis.Text.Trim()))
                {
                    lblValidation.Text = "Please enter a diagnosis.";
                    return false;
                }
                if (dtpConsultationDate.Value > DateTime.Now)
                {
                    lblValidation.Text = "Consultation date cannot be in the future.";
                    return false;
                }
                lblValidation.Text = "";
                return true;
            }

            private void BtnSave_Click(object sender, EventArgs e)
            {
                if (!ValidateForm())
                    return;

                dynamic appointment = cmbAppointments.SelectedItem;
                int appointmentID = appointment.AppointmentID;

                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = @"INSERT INTO Consultations (AppointmentID, Diagnosis, Temperature, BloodPressure, HeartRate, Weight, Height, Notes, ConsultationDate)
                                        VALUES (@AppointmentID, @Diagnosis, @Temperature, @BloodPressure, @HeartRate, @Weight, @Height, @Notes, @ConsultationDate)";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@AppointmentID", appointmentID);
                            cmd.Parameters.AddWithValue("@Diagnosis", txtDiagnosis.Text);
                            cmd.Parameters.AddWithValue("@Temperature", decimal.Parse(txtTemperature.Text));
                            cmd.Parameters.AddWithValue("@BloodPressure", txtBloodPressure.Text);
                            cmd.Parameters.AddWithValue("@HeartRate", int.Parse(txtHeartRate.Text));
                            cmd.Parameters.AddWithValue("@Weight", string.IsNullOrEmpty(txtWeight.Text) ? (object)DBNull.Value : decimal.Parse(txtWeight.Text));
                            cmd.Parameters.AddWithValue("@Height", string.IsNullOrEmpty(txtHeight.Text) ? (object)DBNull.Value : decimal.Parse(txtHeight.Text));
                            cmd.Parameters.AddWithValue("@Notes", txtNotes.Text);
                            cmd.Parameters.AddWithValue("@ConsultationDate", dtpConsultationDate.Value);
                            cmd.ExecuteNonQuery();

                            try
                            {
                                // First, get the patient's email from the database
                                string patientEmail = "";
                                string patientName = "";

                                string patientQuery = @"SELECT u.Contact, u.FullName 
                                                      FROM Appointments a 
                                                      JOIN Users u ON a.PatientID = u.UserID 
                                                      WHERE a.AppointmentID = @AppointmentID";
                                using (SqlCommand patientCmd = new SqlCommand(patientQuery, conn))
                                {
                                    patientCmd.Parameters.AddWithValue("@AppointmentID", appointmentID);
                                    using (SqlDataReader reader = patientCmd.ExecuteReader())
                                    {
                                        if (reader.Read())
                                        {
                                            patientEmail = reader.GetString(0);
                                            patientName = reader.GetString(1);
                                        }
                                    }
                                }

                                // Send consultation summary to the patient
                                if (!string.IsNullOrEmpty(patientEmail))
                                {
                                    _ = DatabaseHelper.SendEmailAsync(
                                        patientEmail,
                                        "Your Consultation Summary - Botho University Clinic",
                                        $"Dear {patientName},<br><br>" +
                                        "Your consultation has been completed. Here is your medical summary:<br><br>" +
                                        $"<strong>Diagnosis:</strong> {txtDiagnosis.Text}<br>" +
                                        $"<strong>Temperature:</strong> {txtTemperature.Text}°C<br>" +
                                        $"<strong>Blood Pressure:</strong> {txtBloodPressure.Text}<br>" +
                                        $"<strong>Heart Rate:</strong> {txtHeartRate.Text} bpm<br>" +
                                        (!string.IsNullOrEmpty(txtWeight.Text) ? $"<strong>Weight:</strong> {txtWeight.Text} kg<br>" : "") +
                                        (!string.IsNullOrEmpty(txtHeight.Text) ? $"<strong>Height:</strong> {txtHeight.Text} cm<br>" : "") +
                                        $"<strong>Notes:</strong> {txtNotes.Text}<br><br>" +
                                        "Please contact us if you have any questions.<br><br>" +
                                        "Best regards,<br>Botho University Clinic Team"
                                    );
                                }
                            }
                            catch (Exception emailEx)
                            {
                                Console.WriteLine($"Email failed: {emailEx.Message}");
                            }

                            MessageBox.Show("Consultation saved successfully!");
                            this.Close();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error saving consultation: " + ex.Message);
                }
            }
        }

        public class ConsultationDetailsForm : Form
        {
            private int consultationID;

            public ConsultationDetailsForm(int consID)
            {
                this.consultationID = consID;
                this.Text = "Consultation Details";
                this.Size = new Size(500, 400);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;
                LoadConsultationDetails();
            }

            private void LoadConsultationDetails()
            {
                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = @"SELECT u.FullName, a.AppointmentDate, c.Diagnosis, c.Temperature, c.BloodPressure,
                                        c.HeartRate, c.Weight, c.Height, c.Notes
                                        FROM Consultations c
                                        JOIN Appointments a ON c.AppointmentID = a.AppointmentID
                                        JOIN Users u ON a.PatientID = u.UserID
                                        WHERE c.ConsultationID = @ConsultationID";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@ConsultationID", consultationID);
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    // Create and display consultation details
                                    Label lblTitle = new Label
                                    {
                                        Text = "CONSULTATION DETAILS",
                                        Location = new Point(150, 20),
                                        Size = new Size(200, 25),
                                        Font = new Font("Arial", 14, FontStyle.Bold),
                                        ForeColor = AppColors.White,
                                        TextAlign = ContentAlignment.MiddleCenter
                                    };

                                    Label lblPatient = new Label { Text = $"Patient: {reader.GetString(0)}", Location = new Point(30, 60), Size = new Size(300, 20), ForeColor = AppColors.White };
                                    Label lblDate = new Label { Text = $"Date: {reader.GetDateTime(1).ToShortDateString()}", Location = new Point(30, 90), Size = new Size(300, 20), ForeColor = AppColors.White };
                                    Label lblDiagnosis = new Label { Text = $"Diagnosis: {reader.GetString(2)}", Location = new Point(30, 120), Size = new Size(300, 20), ForeColor = AppColors.White };
                                    Label lblTemp = new Label { Text = $"Temperature: {reader.GetDecimal(3)}°C", Location = new Point(30, 150), Size = new Size(300, 20), ForeColor = AppColors.White };
                                    Label lblBP = new Label { Text = $"Blood Pressure: {reader.GetString(4)}", Location = new Point(30, 180), Size = new Size(300, 20), ForeColor = AppColors.White };
                                    Label lblHR = new Label { Text = $"Heart Rate: {reader.GetInt32(5)} bpm", Location = new Point(30, 210), Size = new Size(300, 20), ForeColor = AppColors.White };

                                    string weight = reader.IsDBNull(6) ? "N/A" : reader.GetDecimal(6).ToString();
                                    string height = reader.IsDBNull(7) ? "N/A" : reader.GetDecimal(7).ToString();

                                    Label lblWeight = new Label { Text = $"Weight: {weight} kg", Location = new Point(30, 240), Size = new Size(300, 20), ForeColor = AppColors.White };
                                    Label lblHeight = new Label { Text = $"Height: {height} cm", Location = new Point(30, 270), Size = new Size(300, 20), ForeColor = AppColors.White };
                                    Label lblNotes = new Label { Text = $"Notes: {reader.GetString(8)}", Location = new Point(30, 300), Size = new Size(400, 60), ForeColor = AppColors.White };

                                    this.Controls.Add(lblTitle);
                                    this.Controls.Add(lblPatient);
                                    this.Controls.Add(lblDate);
                                    this.Controls.Add(lblDiagnosis);
                                    this.Controls.Add(lblTemp);
                                    this.Controls.Add(lblBP);
                                    this.Controls.Add(lblHR);
                                    this.Controls.Add(lblWeight);
                                    this.Controls.Add(lblHeight);
                                    this.Controls.Add(lblNotes);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading consultation details: " + ex.Message);
                }
            }
        }

        public class AdminDashboardForm : Form
        {
            private Panel panelStats;
            private Panel panelQuickActions;
            private Label lblFooter;
            private Button btnLogout;

            public AdminDashboardForm()
            {
                this.Text = $"Admin Dashboard - Welcome {CurrentUser.FullName}";
                this.Size = new Size(800, 600);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;

                Label lblHeader = new Label
                {
                    Text = $"Admin Dashboard - {CurrentUser.FullName}",
                    Location = new Point(20, 20),
                    Size = new Size(400, 30),
                    Font = new Font("Arial", 16, FontStyle.Bold),
                    ForeColor = AppColors.White
                };

                panelStats = new Panel
                {
                    Location = new Point(20, 70),
                    Size = new Size(760, 120),
                    BackColor = AppColors.LightGray,
                    BorderStyle = BorderStyle.FixedSingle
                };

                panelQuickActions = new Panel
                {
                    Location = new Point(20, 210),
                    Size = new Size(760, 300),
                    BackColor = AppColors.LightGray,
                    BorderStyle = BorderStyle.FixedSingle
                };

                btnLogout = new Button
                {
                    Text = "Logout",
                    Location = new Point(680, 20),
                    Size = new Size(80, 30),
                    BackColor = AppColors.DarkRed,
                    ForeColor = AppColors.White,
                    Font = new Font("Arial", 9, FontStyle.Bold)
                };

                lblFooter = new Label
                {
                    Text = "© 2025 - Botho University Clinic - Privacy",
                    Location = new Point(300, 530),
                    Size = new Size(300, 20),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                this.Controls.Add(lblHeader);
                this.Controls.Add(panelStats);
                this.Controls.Add(panelQuickActions);
                this.Controls.Add(btnLogout);
                this.Controls.Add(lblFooter);

                btnLogout.Click += (s, e) => { this.Close(); new LoginForm().Show(); };
                LoadDashboard();
            }

            private void LoadDashboard()
            {
                LoadStatistics();
                LoadQuickActions();
            }

            private void LoadStatistics()
            {
                panelStats.Controls.Clear();

                Label lblStatsTitle = new Label
                {
                    Text = "System Overview",
                    Location = new Point(10, 10),
                    Size = new Size(200, 20),
                    Font = new Font("Arial", 12, FontStyle.Bold),
                    ForeColor = Color.Black
                };
                panelStats.Controls.Add(lblStatsTitle);

                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = @"SELECT
                                        (SELECT COUNT(*) FROM Users) as TotalUsers,
                                        (SELECT COUNT(*) FROM Appointments WHERE AppointmentDate = CAST(GETDATE() AS DATE)) as TodayAppointments,
                                        (SELECT COUNT(*) FROM Appointments WHERE Status = 'Scheduled') as ScheduledAppointments,
                                        (SELECT COUNT(*) FROM Appointments WHERE Status = 'Completed' AND AppointmentDate = CAST(GETDATE() AS DATE)) as CompletedToday";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    int users = reader.GetInt32(0);
                                    int today = reader.GetInt32(1);
                                    int scheduled = reader.GetInt32(2);
                                    int completed = reader.GetInt32(3);

                                    CreateStatBox("Total Users", users.ToString(), 20, 40, panelStats);
                                    CreateStatBox("Today's Appointments", today.ToString(), 180, 40, panelStats);
                                    CreateStatBox("Scheduled", scheduled.ToString(), 340, 40, panelStats);
                                    CreateStatBox("Completed Today", completed.ToString(), 500, 40, panelStats);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Label lblError = new Label
                    {
                        Text = "Error loading statistics",
                        Location = new Point(20, 40),
                        Size = new Size(200, 20),
                        ForeColor = Color.Red
                    };
                    panelStats.Controls.Add(lblError);
                }
            }

            private void LoadQuickActions()
            {
                panelQuickActions.Controls.Clear();

                Label lblActionsTitle = new Label
                {
                    Text = "Quick Actions",
                    Location = new Point(10, 10),
                    Size = new Size(200, 20),
                    Font = new Font("Arial", 12, FontStyle.Bold),
                    ForeColor = Color.Black
                };
                panelQuickActions.Controls.Add(lblActionsTitle);

                string[] actions = {
                    "Staff Management",
                    "Reporting & Analytics",
                    "Manage Profile",
                    "System Settings",
                    "User Registration",
                    "View All Reports",
                    "All Consultations"
                };

                int xPos = 20;
                int yPos = 50;
                foreach (string action in actions)
                {
                    Button btnAction = new Button
                    {
                        Text = action,
                        Location = new Point(xPos, yPos),
                        Size = new Size(180, 40),
                        BackColor = AppColors.DarkRed,
                        ForeColor = AppColors.White,
                        Font = new Font("Arial", 10, FontStyle.Bold),
                        Tag = action
                    };
                    btnAction.Click += QuickActionButton_Click;
                    panelQuickActions.Controls.Add(btnAction);
                    xPos += 190;
                    if (xPos > 600)
                    {
                        xPos = 20;
                        yPos += 60;
                    }
                }
            }

            private void CreateStatBox(string title, string value, int x, int y, Panel parent)
            {
                Panel statBox = new Panel
                {
                    Location = new Point(x, y),
                    Size = new Size(150, 60),
                    BackColor = AppColors.LightBlue,
                    BorderStyle = BorderStyle.FixedSingle
                };

                Label lblValue = new Label
                {
                    Text = value,
                    Location = new Point(10, 10),
                    Size = new Size(130, 25),
                    Font = new Font("Arial", 16, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.Black
                };

                Label lblTitle = new Label
                {
                    Text = title,
                    Location = new Point(10, 35),
                    Size = new Size(130, 20),
                    Font = new Font("Arial", 8, FontStyle.Regular),
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.Black
                };

                statBox.Controls.Add(lblValue);
                statBox.Controls.Add(lblTitle);
                parent.Controls.Add(statBox);
            }

            private void QuickActionButton_Click(object sender, EventArgs e)
            {
                Button button = (Button)sender;
                string action = button.Tag.ToString();

                switch (action)
                {
                    case "Staff Management":
                        new StaffManagementForm().ShowDialog();
                        break;
                    case "Reporting & Analytics":
                        new ReportingForm().ShowDialog();
                        break;
                    case "Manage Profile":
                        new ManageProfileForm().ShowDialog();
                        LoadDashboard();
                        break;
                    case "System Settings":
                        new SystemSettingsForm().ShowDialog();
                        break;
                    case "User Registration":
                        new UserRegistrationForm().ShowDialog();
                        break;
                    case "View All Reports":
                        new AllReportsForm().ShowDialog();
                        break;
                    case "All Consultations":
                        new AdminConsultationsForm().ShowDialog();
                        break;
                }
            }
        }

        public class ProviderAppointmentsForm : Form
        {
            private DataGridView dgvAllAppointments;
            private DateTimePicker dtpStart, dtpEnd;
            private Button btnFilter, btnExport, btnAccept, btnReject, btnAdmit;

            public ProviderAppointmentsForm()
            {
                this.Text = "All Appointments - Manage Status";
                this.Size = new Size(900, 600);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;

                Label lblTitle = new Label
                {
                    Text = "ALL APPOINTMENTS - MANAGE STATUS",
                    Location = new Point(300, 15),
                    Size = new Size(300, 25),
                    Font = new Font("Arial", 12, FontStyle.Bold),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                Label lblStart = new Label { Text = "From:", Location = new Point(50, 50), Size = new Size(40, 20), ForeColor = AppColors.White };
                dtpStart = new DateTimePicker { Location = new Point(100, 50), Size = new Size(120, 20), Value = DateTime.Today.AddDays(-30) };

                Label lblEnd = new Label { Text = "To:", Location = new Point(230, 50), Size = new Size(40, 20), ForeColor = AppColors.White };
                dtpEnd = new DateTimePicker { Location = new Point(270, 50), Size = new Size(120, 20), Value = DateTime.Today.AddDays(30) };

                btnFilter = new Button
                {
                    Text = "Filter",
                    Location = new Point(400, 50),
                    Size = new Size(80, 25),
                    BackColor = AppColors.DarkRed,
                    ForeColor = AppColors.White
                };

                btnExport = new Button
                {
                    Text = "Export",
                    Location = new Point(490, 50),
                    Size = new Size(80, 25),
                    BackColor = AppColors.DarkRed,
                    ForeColor = AppColors.White
                };

                btnAccept = new Button
                {
                    Text = "Accept",
                    Location = new Point(580, 50),
                    Size = new Size(80, 25),
                    BackColor = Color.Green,
                    ForeColor = AppColors.White
                };

                btnReject = new Button
                {
                    Text = "Reject",
                    Location = new Point(670, 50),
                    Size = new Size(80, 25),
                    BackColor = Color.Red,
                    ForeColor = AppColors.White
                };

                btnAdmit = new Button
                {
                    Text = "Mark as Admitted",
                    Location = new Point(760, 50),
                    Size = new Size(120, 25),
                    BackColor = Color.Orange,
                    ForeColor = AppColors.White
                };

                dgvAllAppointments = new DataGridView
                {
                    Location = new Point(50, 90),
                    Size = new Size(800, 400),
                    ReadOnly = true,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    BackgroundColor = Color.White,
                    ForeColor = Color.Black
                };

                this.Controls.Add(lblTitle);
                this.Controls.Add(lblStart);
                this.Controls.Add(dtpStart);
                this.Controls.Add(lblEnd);
                this.Controls.Add(dtpEnd);
                this.Controls.Add(btnFilter);
                this.Controls.Add(btnExport);
                this.Controls.Add(btnAccept);
                this.Controls.Add(btnReject);
                this.Controls.Add(btnAdmit);
                this.Controls.Add(dgvAllAppointments);

                btnFilter.Click += (s, e) => LoadAppointments();
                btnExport.Click += BtnExport_Click;
                btnAccept.Click += BtnAccept_Click;
                btnReject.Click += BtnReject_Click;
                btnAdmit.Click += BtnAdmit_Click;
                LoadAppointments();
            }

            private void LoadAppointments()
            {
                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = @"SELECT a.AppointmentID, u.FullName as Patient, a.AppointmentDate, a.TimeSlot, a.Reason, a.Status
                                       FROM Appointments a
                                       JOIN Users u ON a.PatientID = u.UserID
                                       WHERE a.ProviderID = @ProviderID
                                       AND a.AppointmentDate BETWEEN @StartDate AND @EndDate
                                       ORDER BY a.AppointmentDate DESC, a.TimeSlot DESC";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@ProviderID", CurrentUser.UserID);
                            cmd.Parameters.AddWithValue("@StartDate", dtpStart.Value.Date);
                            cmd.Parameters.AddWithValue("@EndDate", dtpEnd.Value.Date);
                            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                            {
                                DataTable dt = new DataTable();
                                adapter.Fill(dt);
                                dgvAllAppointments.DataSource = dt;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading appointments: " + ex.Message);
                }
            }

            private void UpdateAppointmentStatus(string status)
            {
                if (dgvAllAppointments.SelectedRows.Count > 0)
                {
                    int appointmentID = Convert.ToInt32(dgvAllAppointments.SelectedRows[0].Cells["AppointmentID"].Value);
                    string patientName = dgvAllAppointments.SelectedRows[0].Cells["Patient"].Value.ToString();

                    try
                    {
                        using (SqlConnection conn = DatabaseHelper.GetConnection())
                        {
                            conn.Open();

                            // thid is done to get patient's email
                            string patientEmail = "";
                            string appointmentDate = "";
                            string timeSlot = "";

                            string patientQuery = @"SELECT u.Contact, a.AppointmentDate, a.TimeSlot 
                                                  FROM Appointments a 
                                                  JOIN Users u ON a.PatientID = u.UserID 
                                                  WHERE a.AppointmentID = @AppointmentID";
                            using (SqlCommand patientCmd = new SqlCommand(patientQuery, conn))
                            {
                                patientCmd.Parameters.AddWithValue("@AppointmentID", appointmentID);
                                using (SqlDataReader reader = patientCmd.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        patientEmail = reader.GetString(0);
                                        appointmentDate = reader.GetDateTime(1).ToShortDateString();
                                        timeSlot = reader.GetTimeSpan(2).ToString();
                                    }
                                }
                            }

                        
                            string query = "UPDATE Appointments SET Status = @Status WHERE AppointmentID = @AppointmentID";
                            using (SqlCommand cmd = new SqlCommand(query, conn))
                            {
                                cmd.Parameters.AddWithValue("@Status", status);
                                cmd.Parameters.AddWithValue("@AppointmentID", appointmentID);
                                cmd.ExecuteNonQuery();

                                try
                                {
                                    if (!string.IsNullOrEmpty(patientEmail))
                                    {
                                        
                                        string statusMessage = "";
                                        if (status.ToLower() == "approved")
                                            statusMessage = "has been approved and confirmed.";
                                        else if (status.ToLower() == "rejected")
                                            statusMessage = "has been cancelled.";
                                        else if (status.ToLower() == "admitted")
                                            statusMessage = "status has been updated to admitted.";
                                        else
                                            statusMessage = "status has been updated.";

                                        _ = DatabaseHelper.SendEmailAsync(
                                            patientEmail,
                                            $"Appointment Update - Botho University Clinic",
                                            $"Dear {patientName},<br><br>" +
                                            $"Your appointment scheduled for {appointmentDate} at {timeSlot} {statusMessage}<br><br>" +
                                            "Best regards,<br>Botho University Clinic Team"
                                        );
                                    }
                                }
                                catch (Exception emailEx)
                                {
                                    Console.WriteLine($"Email failed: {emailEx.Message}");
                                }

                                MessageBox.Show($"Appointment status updated to {status}!");
                                LoadAppointments();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error updating appointment: " + ex.Message);
                    }
                }
                else
                {
                    MessageBox.Show("Please select an appointment first.");
                }
            }

            private void BtnAccept_Click(object sender, EventArgs e) => UpdateAppointmentStatus("Approved");
            private void BtnReject_Click(object sender, EventArgs e) => UpdateAppointmentStatus("Rejected");
            private void BtnAdmit_Click(object sender, EventArgs e) => UpdateAppointmentStatus("Admitted");

            private void BtnExport_Click(object sender, EventArgs e)
            {
                try
                {
                    SaveFileDialog saveFile = new SaveFileDialog();
                    saveFile.Filter = "CSV files (*.csv)|*.csv";
                    saveFile.Title = "Export Appointments";
                    if (saveFile.ShowDialog() == DialogResult.OK)
                    {
                        using (StreamWriter writer = new StreamWriter(saveFile.FileName))
                        {
                            for (int i = 0; i < dgvAllAppointments.Columns.Count; i++)
                            {
                                writer.Write(dgvAllAppointments.Columns[i].HeaderText);
                                if (i < dgvAllAppointments.Columns.Count - 1)
                                    writer.Write(",");
                            }
                            writer.WriteLine();

                            foreach (DataGridViewRow row in dgvAllAppointments.Rows)
                            {
                                for (int i = 0; i < dgvAllAppointments.Columns.Count; i++)
                                {
                                    writer.Write(row.Cells[i].Value?.ToString());
                                    if (i < dgvAllAppointments.Columns.Count - 1)
                                        writer.Write(",");
                                }
                                writer.WriteLine();
                            }
                        }
                        MessageBox.Show("Appointments exported successfully!", "Success",
                                      MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error exporting appointments: " + ex.Message);
                }
            }
        }

        public class AdminConsultationsForm : Form
        {
            private DataGridView dgvConsultations;

            public AdminConsultationsForm()
            {
                this.Text = "All Consultations";
                this.Size = new Size(900, 500);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;

                Label lblTitle = new Label
                {
                    Text = "ALL CONSULTATIONS",
                    Location = new Point(350, 20),
                    Size = new Size(200, 25),
                    Font = new Font("Arial", 14, FontStyle.Bold),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                dgvConsultations = new DataGridView
                {
                    Location = new Point(50, 60),
                    Size = new Size(800, 350),
                    BackgroundColor = Color.White,
                    ForeColor = Color.Black,
                    ReadOnly = true,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
                };

                this.Controls.Add(lblTitle);
                this.Controls.Add(dgvConsultations);
                LoadAllConsultations();
            }

            private void LoadAllConsultations()
            {
                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = @"SELECT c.ConsultationID, p.FullName AS Patient, d.FullName AS Provider,
                                        a.AppointmentDate, c.Diagnosis, c.ConsultationDate
                                        FROM Consultations c
                                        JOIN Appointments a ON c.AppointmentID = a.AppointmentID
                                        JOIN Users p ON a.PatientID = p.UserID
                                        JOIN Users d ON a.ProviderID = d.UserID
                                        ORDER BY c.ConsultationDate DESC";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                            {
                                DataTable dt = new DataTable();
                                adapter.Fill(dt);
                                dgvConsultations.DataSource = dt;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading consultations: " + ex.Message);
                }
            }
        }

        // All Reports Form for Admin
        public class AllReportsForm : Form
        {
            private DataGridView dgvReports;

            public AllReportsForm()
            {
                this.Text = "All System Reports";
                this.Size = new Size(800, 500);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;

                Label lblTitle = new Label
                {
                    Text = "ALL SYSTEM REPORTS",
                    Location = new Point(300, 20),
                    Size = new Size(200, 25),
                    Font = new Font("Arial", 14, FontStyle.Bold),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                dgvReports = new DataGridView
                {
                    Location = new Point(50, 60),
                    Size = new Size(700, 350),
                    BackgroundColor = Color.White,
                    ForeColor = Color.Black,
                    ReadOnly = true,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
                };

                this.Controls.Add(lblTitle);
                this.Controls.Add(dgvReports);
                LoadAllReports();
            }

            private void LoadAllReports()
            {
                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = @"SELECT 'Appointments' as ReportType, COUNT(*) as Count FROM Appointments
                                        UNION ALL
                                        SELECT 'Users' as ReportType, COUNT(*) as Count FROM Users
                                        UNION ALL
                                        SELECT 'Consultations' as ReportType, COUNT(*) as Count FROM Consultations
                                        UNION ALL
                                        SELECT 'Prescriptions' as ReportType, COUNT(*) as Count FROM Prescriptions
                                        UNION ALL
                                        SELECT 'Scheduled Appointments' as ReportType, COUNT(*) as Count FROM Appointments WHERE Status = 'Scheduled'";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                            {
                                DataTable dt = new DataTable();
                                adapter.Fill(dt);
                                dgvReports.DataSource = dt;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading reports: " + ex.Message);
                }
            }
        }

        public class PatientRecordsForm : Form
        {
            private DataGridView dgvPatients;
            private TextBox txtSearch;
            private Button btnSearch;

            public PatientRecordsForm()
            {
                this.Text = "Patient Records";
                this.Size = new Size(800, 600);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;

                Label lblTitle = new Label
                {
                    Text = "PATIENT RECORDS",
                    Location = new Point(300, 20),
                    Size = new Size(200, 25),
                    Font = new Font("Arial", 14, FontStyle.Bold),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                //searchung dfor a patient or student
                Label lblSearch = new Label { Text = "Search Patient:", Location = new Point(50, 60), Size = new Size(100, 20), ForeColor = AppColors.White };
                txtSearch = new TextBox { Location = new Point(160, 60), Size = new Size(200, 20) };
                btnSearch = new Button { Text = "Search", Location = new Point(370, 60), Size = new Size(80, 25), BackColor = AppColors.DarkRed, ForeColor = AppColors.White };

                dgvPatients = new DataGridView
                {
                    Location = new Point(50, 100),
                    Size = new Size(700, 400),
                    BackgroundColor = Color.White,
                    ForeColor = Color.Black,
                    ReadOnly = true,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect
                };

                Button btnViewDetails = new Button
                {
                    Text = "View Patient Details",
                    Location = new Point(50, 520),
                    Size = new Size(150, 35),
                    BackColor = AppColors.DarkRed,
                    ForeColor = AppColors.White
                };

                this.Controls.Add(lblTitle);
                this.Controls.Add(lblSearch);
                this.Controls.Add(txtSearch);
                this.Controls.Add(btnSearch);
                this.Controls.Add(dgvPatients);
                this.Controls.Add(btnViewDetails);

                btnSearch.Click += (s, e) => LoadPatients();
                btnViewDetails.Click += BtnViewDetails_Click;
                dgvPatients.DoubleClick += (s, e) => ViewPatientDetails();
                LoadPatients();
            }

            private void LoadPatients()
            {
                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = @"SELECT DISTINCT u.UserID, u.FullName, u.Contact,
                                p.Weight, p.Height, p.HeartRate, p.EmergencyContact,
                                (SELECT COUNT(*) FROM Appointments WHERE PatientID = u.UserID AND ProviderID = @ProviderID) as AppointmentCount,
                                (SELECT MAX(AppointmentDate) FROM Appointments WHERE PatientID = u.UserID AND ProviderID = @ProviderID) as LastVisit
                                FROM Users u
                                JOIN Patients p ON u.UserID = p.UserID
                                JOIN Appointments a ON u.UserID = a.PatientID
                                WHERE a.ProviderID = @ProviderID
                                AND u.FullName LIKE @Search
                                ORDER BY u.FullName";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@ProviderID", CurrentUser.UserID);
                            cmd.Parameters.AddWithValue("@Search", "%" + txtSearch.Text + "%");
                            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                            {
                                DataTable dt = new DataTable();
                                adapter.Fill(dt);
                                dgvPatients.DataSource = dt;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading patients: " + ex.Message);
                }
            }

            private void BtnViewDetails_Click(object sender, EventArgs e)
            {
                ViewPatientDetails();
            }

            private void ViewPatientDetails()
            {
                if (dgvPatients.SelectedRows.Count > 0)
                {
                    int patientID = Convert.ToInt32(dgvPatients.SelectedRows[0].Cells["UserID"].Value);
                    new PatientDetailsForm(patientID).ShowDialog();
                }
                else
                {
                    MessageBox.Show("Please select a patient first.");
                }
            }
        }

        public class PatientDetailsForm : Form
        {
            private int patientID;
            private DataGridView dgvMedicalHistory;
            private Label lblWeight, lblHeight, lblHeartRate, lblEmergencyContact;

            public PatientDetailsForm(int patientId)
            {
                this.patientID = patientId;
                this.Text = "Patient Medical History and Details";
                this.Size = new Size(900, 600);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;

                Label lblTitle = new Label
                {
                    Text = "PATIENT MEDICAL HISTORY AND DETAILS",
                    Location = new Point(300, 20),
                    Size = new Size(300, 25),
                    Font = new Font("Arial", 14, FontStyle.Bold),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                Panel pnlDetails = new Panel
                {
                    Location = new Point(50, 60),
                    Size = new Size(800, 80),
                    BackColor = AppColors.LightGray,
                    BorderStyle = BorderStyle.FixedSingle
                };

                lblWeight = new Label { Location = new Point(20, 15), Size = new Size(150, 20), ForeColor = Color.Black, Font = new Font("Arial", 9, FontStyle.Bold) };
                lblHeight = new Label { Location = new Point(20, 40), Size = new Size(150, 20), ForeColor = Color.Black, Font = new Font("Arial", 9, FontStyle.Bold) };
                lblHeartRate = new Label { Location = new Point(200, 15), Size = new Size(150, 20), ForeColor = Color.Black, Font = new Font("Arial", 9, FontStyle.Bold) };
                lblEmergencyContact = new Label { Location = new Point(200, 40), Size = new Size(300, 20), ForeColor = Color.Black, Font = new Font("Arial", 9, FontStyle.Bold) };

                pnlDetails.Controls.AddRange(new Control[] { lblWeight, lblHeight, lblHeartRate, lblEmergencyContact });

                dgvMedicalHistory = new DataGridView
                {
                    Location = new Point(50, 160),
                    Size = new Size(800, 350),
                    BackgroundColor = Color.White,
                    ForeColor = Color.Black,
                    ReadOnly = true
                };

                this.Controls.Add(lblTitle);
                this.Controls.Add(pnlDetails);
                this.Controls.Add(dgvMedicalHistory);
                LoadPatientDetails();
                LoadPatientHistory();
            }

            private void LoadPatientDetails()
            {
                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = @"SELECT p.Weight, p.Height, p.HeartRate, p.EmergencyContact, u.FullName
                                FROM Patients p
                                JOIN Users u ON p.UserID = u.UserID
                                WHERE p.UserID = @PatientID";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@PatientID", patientID);
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    string weight = reader.IsDBNull(0) ? "Not recorded" : $"{reader.GetDecimal(0)} kg";
                                    string height = reader.IsDBNull(1) ? "Not recorded" : $"{reader.GetDecimal(1)} cm";
                                    string heartRate = reader.IsDBNull(2) ? "Not recorded" : $"{reader.GetInt32(2)} bpm";
                                    string emergencyContact = reader.IsDBNull(3) ? "Not provided" : reader.GetString(3);
                                    lblWeight.Text = $"Weight: {weight}";
                                    lblHeight.Text = $"Height: {height}";
                                    lblHeartRate.Text = $"Heart Rate: {heartRate}";
                                    lblEmergencyContact.Text = $"Emergency Contact: {emergencyContact}";
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading patient details: " + ex.Message);
                }
            }

            private void LoadPatientHistory()
            {
                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = @"SELECT a.AppointmentDate, a.TimeSlot, c.Diagnosis, c.Temperature,
                                c.BloodPressure, c.HeartRate, c.Weight, c.Height, c.Notes
                                FROM Appointments a
                                LEFT JOIN Consultations c ON a.AppointmentID = c.AppointmentID
                                WHERE a.PatientID = @PatientID
                                ORDER BY a.AppointmentDate DESC";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@PatientID", patientID);
                            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                            {
                                DataTable dt = new DataTable();
                                adapter.Fill(dt);
                                dgvMedicalHistory.DataSource = dt;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading patient history: " + ex.Message);
                }
            }
        }

        // Manage Medications Form used to add and remove medication
        public class ManageMedicationsForm : Form
        {
            private DataGridView dgvMedications;
            private TextBox txtMedication, txtDosage, txtInstructions;
            private Button btnAdd, btnRemove;

            public ManageMedicationsForm()
            {
                this.Text = "Manage Medications";
                this.Size = new Size(700, 500);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;

                Label lblTitle = new Label
                {
                    Text = "MANAGE MEDICATIONS",
                    Location = new Point(250, 20),
                    Size = new Size(200, 25),
                    Font = new Font("Arial", 14, FontStyle.Bold),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                Label lblMedication = new Label { Text = "Medication:", Location = new Point(50, 60), Size = new Size(80, 20), ForeColor = AppColors.White };
                txtMedication = new TextBox { Location = new Point(140, 60), Size = new Size(200, 20) };

                Label lblDosage = new Label { Text = "Dosage:", Location = new Point(50, 90), Size = new Size(80, 20), ForeColor = AppColors.White };
                txtDosage = new TextBox { Location = new Point(140, 90), Size = new Size(200, 20) };

                Label lblInstructions = new Label { Text = "Instructions:", Location = new Point(50, 120), Size = new Size(80, 20), ForeColor = AppColors.White };
                txtInstructions = new TextBox { Location = new Point(140, 120), Size = new Size(200, 60), Multiline = true };

                btnAdd = new Button { Text = "Add Medication", Location = new Point(350, 60), Size = new Size(120, 30), BackColor = AppColors.DarkRed, ForeColor = AppColors.White };
                btnRemove = new Button { Text = "Remove Selected", Location = new Point(350, 100), Size = new Size(120, 30), BackColor = Color.Red, ForeColor = AppColors.White };

                dgvMedications = new DataGridView
                {
                    Location = new Point(50, 200),
                    Size = new Size(600, 250),
                    BackgroundColor = Color.White,
                    ForeColor = Color.Black,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect
                };

                this.Controls.Add(lblTitle);
                this.Controls.Add(lblMedication);
                this.Controls.Add(txtMedication);
                this.Controls.Add(lblDosage);
                this.Controls.Add(txtDosage);
                this.Controls.Add(lblInstructions);
                this.Controls.Add(txtInstructions);
                this.Controls.Add(btnAdd);
                this.Controls.Add(btnRemove);
                this.Controls.Add(dgvMedications);

                btnAdd.Click += BtnAdd_Click;
                btnRemove.Click += BtnRemove_Click;
                LoadMedications();
            }

            private void LoadMedications()
            {
                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = "SELECT MedicationID, MedicationName, Dosage, Instructions FROM Medications ORDER BY MedicationName";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                            {
                                DataTable dt = new DataTable();
                                adapter.Fill(dt);
                                dgvMedications.DataSource = dt;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading medications: " + ex.Message);
                }
            }

            private void BtnAdd_Click(object sender, EventArgs e)
            {
                if (string.IsNullOrEmpty(txtMedication.Text) || string.IsNullOrEmpty(txtDosage.Text))
                {
                    MessageBox.Show("Please enter medication and dosage.");
                    return;
                }

                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = "INSERT INTO Medications (MedicationName, Dosage, Instructions, CreatedBy) VALUES (@Name, @Dosage, @Instructions, @UserID)";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@Name", txtMedication.Text);
                            cmd.Parameters.AddWithValue("@Dosage", txtDosage.Text);
                            cmd.Parameters.AddWithValue("@Instructions", txtInstructions.Text);
                            cmd.Parameters.AddWithValue("@UserID", CurrentUser.UserID);
                            cmd.ExecuteNonQuery();
                            MessageBox.Show("Medication added successfully!");

                            txtMedication.Clear();
                            txtDosage.Clear();
                            txtInstructions.Clear();
                            LoadMedications();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error adding medication: " + ex.Message);
                }
            }

            private void BtnRemove_Click(object sender, EventArgs e)
            {
                if (dgvMedications.SelectedRows.Count > 0)
                {
                    int medicationID = Convert.ToInt32(dgvMedications.SelectedRows[0].Cells["MedicationID"].Value);
                    try
                    {
                        using (SqlConnection conn = DatabaseHelper.GetConnection())
                        {
                            conn.Open();
                            string query = "DELETE FROM Medications WHERE MedicationID = @MedicationID";
                            using (SqlCommand cmd = new SqlCommand(query, conn))
                            {
                                cmd.Parameters.AddWithValue("@MedicationID", medicationID);
                                cmd.ExecuteNonQuery();
                                MessageBox.Show("Medication removed successfully!");
                                LoadMedications();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error removing medication: " + ex.Message);
                    }
                }
                else
                {
                    MessageBox.Show("Please select a medication to remove.");
                }
            }
        }

        public class QuickReportsForm : Form
        {
            private ComboBox cmbReportType;
            private Button btnGenerate;
            private DataGridView dgvReport;

            public QuickReportsForm()
            {
                this.Text = "Quick Reports";
                this.Size = new Size(800, 600);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;

                Label lblTitle = new Label
                {
                    Text = "QUICK REPORTS",
                    Location = new Point(300, 20),
                    Size = new Size(200, 25),
                    Font = new Font("Arial", 14, FontStyle.Bold),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                Label lblReportType = new Label { Text = "Report Type:", Location = new Point(50, 60), Size = new Size(80, 20), ForeColor = AppColors.White };
                cmbReportType = new ComboBox { Location = new Point(140, 60), Size = new Size(200, 20) };
                cmbReportType.Items.AddRange(new string[] {
                    "Today's Appointments",
                    "Weekly Summary",
                    "Patient Statistics",
                    "Prescription Summary"
                });
                cmbReportType.SelectedIndex = 0;

                btnGenerate = new Button { Text = "Generate Report", Location = new Point(350, 60), Size = new Size(120, 25), BackColor = AppColors.DarkRed, ForeColor = AppColors.White };

                dgvReport = new DataGridView
                {
                    Location = new Point(50, 100),
                    Size = new Size(700, 400),
                    BackgroundColor = Color.White,
                    ForeColor = Color.Black,
                    ReadOnly = true
                };

                this.Controls.Add(lblTitle);
                this.Controls.Add(lblReportType);
                this.Controls.Add(cmbReportType);
                this.Controls.Add(btnGenerate);
                this.Controls.Add(dgvReport);

                btnGenerate.Click += BtnGenerate_Click;

                BtnGenerate_Click(null, null);
            }

            private void BtnGenerate_Click(object sender, EventArgs e)
            {
                string reportType = cmbReportType.SelectedItem?.ToString();
                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = "";
                        switch (reportType)
                        {
                            case "Today's Appointments":
                                query = @"SELECT u.FullName as Patient, a.TimeSlot, a.Reason, a.Status
                                FROM Appointments a
                                JOIN Users u ON a.PatientID = u.UserID
                                WHERE a.ProviderID = @ProviderID AND a.AppointmentDate = CAST(GETDATE() AS DATE)
                                ORDER BY a.TimeSlot";
                                break;
                            case "Weekly Summary":
                                query = @"SELECT DATENAME(dw, AppointmentDate) as Day,
                                COUNT(*) as AppointmentCount,
                                SUM(CASE WHEN Status = 'Completed' THEN 1 ELSE 0 END) as Completed
                                FROM Appointments
                                WHERE ProviderID = @ProviderID AND AppointmentDate >= DATEADD(day, -7, GETDATE())
                                GROUP BY DATENAME(dw, AppointmentDate), AppointmentDate
                                ORDER BY MIN(AppointmentDate)";
                                break;
                            case "Patient Statistics":
                                query = @"SELECT u.FullName as Patient,
                                COUNT(*) as TotalVisits,
                                MAX(a.AppointmentDate) as LastVisit
                                FROM Appointments a
                                JOIN Users u ON a.PatientID = u.UserID
                                WHERE a.ProviderID = @ProviderID
                                GROUP BY u.UserID, u.FullName
                                ORDER BY TotalVisits DESC";
                                break;
                            case "Prescription Summary":
                                query = @"SELECT p.Medication, COUNT(*) as TimesPrescribed
                                FROM Prescriptions p
                                JOIN Consultations c ON p.ConsultationID = c.ConsultationID
                                JOIN Appointments a ON c.AppointmentID = a.AppointmentID
                                WHERE a.ProviderID = @ProviderID
                                GROUP BY p.Medication
                                ORDER BY TimesPrescribed DESC";
                                break;
                        }
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@ProviderID", CurrentUser.UserID);
                            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                            {
                                DataTable dt = new DataTable();
                                adapter.Fill(dt);
                                dgvReport.DataSource = dt;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error generating report: " + ex.Message);
                }
            }
        }

        public class SetAvailabilityForm : Form
        {
            private CheckedListBox clbDays;
            private DateTimePicker dtpStartTime, dtpEndTime;
            private Button btnSave;

            public SetAvailabilityForm()
            {
                this.Text = "Set Availability";
                this.Size = new Size(400, 400);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;

                Label lblTitle = new Label
                {
                    Text = "SET AVAILABILITY",
                    Location = new Point(120, 20),
                    Size = new Size(200, 25),
                    Font = new Font("Arial", 14, FontStyle.Bold),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                Label lblDays = new Label { Text = "Available Days:", Location = new Point(50, 60), Size = new Size(120, 20), ForeColor = AppColors.White };
                clbDays = new CheckedListBox
                {
                    Location = new Point(180, 60),
                    Size = new Size(150, 120),
                    BackColor = Color.White,
                    ForeColor = Color.Black
                };
                clbDays.Items.AddRange(new string[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" });
  
                Label lblStartTime = new Label { Text = "Start Time:", Location = new Point(50, 200), Size = new Size(80, 20), ForeColor = AppColors.White };
                dtpStartTime = new DateTimePicker { Location = new Point(140, 200), Size = new Size(100, 20), Format = DateTimePickerFormat.Time, ShowUpDown = true, Value = DateTime.Today.AddHours(9) };

                Label lblEndTime = new Label { Text = "End Time:", Location = new Point(50, 230), Size = new Size(80, 20), ForeColor = AppColors.White };
                dtpEndTime = new DateTimePicker { Location = new Point(140, 230), Size = new Size(100, 20), Format = DateTimePickerFormat.Time, ShowUpDown = true, Value = DateTime.Today.AddHours(17) };

                // Save button
                btnSave = new Button
                {
                    Text = "Save Availability",
                    Location = new Point(120, 280),
                    Size = new Size(150, 35),
                    BackColor = AppColors.DarkRed,
                    ForeColor = AppColors.White
                };

                // Add controls
                this.Controls.Add(lblTitle);
                this.Controls.Add(lblDays);
                this.Controls.Add(clbDays);
                this.Controls.Add(lblStartTime);
                this.Controls.Add(dtpStartTime);
                this.Controls.Add(lblEndTime);
                this.Controls.Add(dtpEndTime);
                this.Controls.Add(btnSave);

                // Event handler
                btnSave.Click += BtnSave_Click;
                LoadCurrentAvailability();
            }

            private void LoadCurrentAvailability()
            {
                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = "SELECT DayOfWeek FROM ProviderAvailability WHERE ProviderID = @ProviderID AND IsActive = 1";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@ProviderID", CurrentUser.UserID);
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string day = reader.GetString(0);
                                    for (int i = 0; i < clbDays.Items.Count; i++)
                                    {
                                        if (clbDays.Items[i].ToString() == day)
                                        {
                                            clbDays.SetItemChecked(i, true);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // If no availability set, default to Monday-Friday
                    for (int i = 0; i < 5; i++)
                    {
                        clbDays.SetItemChecked(i, true);
                    }
                }
            }

            private void BtnSave_Click(object sender, EventArgs e)
            {
                List<string> selectedDays = new List<string>();
                foreach (var item in clbDays.CheckedItems)
                {
                    selectedDays.Add(item.ToString());
                }

                if (selectedDays.Count == 0)
                {
                    MessageBox.Show("Please select at least one day.");
                    return;
                }

                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        // thix Deactivats all current availability
                        string deactivateQuery = "UPDATE ProviderAvailability SET IsActive = 0 WHERE ProviderID = @ProviderID";
                        using (SqlCommand cmd = new SqlCommand(deactivateQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@ProviderID", CurrentUser.UserID);
                            cmd.ExecuteNonQuery();
                        }

                        // Inserts new availability
                        string insertQuery = "INSERT INTO ProviderAvailability (ProviderID, DayOfWeek, StartTime, EndTime) VALUES (@ProviderID, @Day, @Start, @End)";
                        foreach (string day in selectedDays)
                        {
                            using (SqlCommand cmd = new SqlCommand(insertQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@ProviderID", CurrentUser.UserID);
                                cmd.Parameters.AddWithValue("@Day", day);
                                cmd.Parameters.AddWithValue("@Start", dtpStartTime.Value.TimeOfDay);
                                cmd.Parameters.AddWithValue("@End", dtpEndTime.Value.TimeOfDay);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        string availability = $"Availability set for: {string.Join(", ", selectedDays)}\n" +
                                            $"Time: {dtpStartTime.Value.ToString("hh:mm tt")} to {dtpEndTime.Value.ToString("hh:mm tt")}";
                        MessageBox.Show(availability, "Availability Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error saving availability: " + ex.Message);
                }
            }
        }

        public class EmergencyContactsForm : Form
        {
            private DataGridView dgvContacts;

            public EmergencyContactsForm()
            {
                this.Text = "Emergency Contacts";
                this.Size = new Size(600, 400);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;

                // Title
                Label lblTitle = new Label
                {
                    Text = "EMERGENCY CONTACTS - LESOTHO",
                    Location = new Point(200, 20),
                    Size = new Size(200, 25),
                    Font = new Font("Arial", 14, FontStyle.Bold),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                // Contacts Grid
                dgvContacts = new DataGridView
                {
                    Location = new Point(50, 60),
                    Size = new Size(500, 250),
                    BackgroundColor = Color.White,
                    ForeColor = Color.Black,
                    ReadOnly = true
                };

                // Add controls
                this.Controls.Add(lblTitle);
                this.Controls.Add(dgvContacts);
                LoadEmergencyContacts();
            }

            private void LoadEmergencyContacts()
            {
                var contacts = new[]
                {
                    new { Service = "Campus Security", Phone = "+266 2232 1234", Extension = "111" },
                    new { Service = "Medical Emergency", Phone = "+266 2232 5678", Extension = "112" },
                    new { Service = "Poison Control", Phone = "+266 2232 9012", Extension = "113" },
                    new { Service = "Mental Health Crisis", Phone = "+266 2232 3456", Extension = "114" },
                    new { Service = "Student Affairs", Phone = "+266 2232 7890", Extension = "115" },
                    new { Service = "Ambulance Services", Phone = "+266 2231 1111", Extension = "116" },
                    new { Service = "Fire Department", Phone = "+266 2231 2222", Extension = "117" },
                    new { Service = "Police Emergency", Phone = "+266 2231 3333", Extension = "118" },
                    new { Service = "Campus Clinic", Phone = "+266 2232 2468", Extension = "119" }
                };
                dgvContacts.DataSource = contacts.ToList();
            }
        }

        // System Settings Form
        public class SystemSettingsForm : Form
        {
            private CheckBox chkEmailNotifications, chkSMSNotifications, chkAutoBackup;
            private NumericUpDown nudBackupInterval;
            private Button btnSave;

            public SystemSettingsForm()
            {
                this.Text = "System Settings";
                this.Size = new Size(500, 350);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;

                // Title
                Label lblTitle = new Label
                {
                    Text = "SYSTEM SETTINGS",
                    Location = new Point(180, 20),
                    Size = new Size(200, 25),
                    Font = new Font("Arial", 14, FontStyle.Bold),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                // Notification settings
                GroupBox grpNotifications = new GroupBox
                {
                    Text = "Notification Settings",
                    Location = new Point(50, 60),
                    Size = new Size(400, 100),
                    ForeColor = AppColors.White
                };

                chkEmailNotifications = new CheckBox { Text = "Email Notifications", Location = new Point(20, 30), Size = new Size(150, 20), Checked = true, ForeColor = AppColors.White };
                chkSMSNotifications = new CheckBox { Text = "SMS Notifications", Location = new Point(20, 60), Size = new Size(150, 20), Checked = false, ForeColor = AppColors.White };

                grpNotifications.Controls.Add(chkEmailNotifications);
                grpNotifications.Controls.Add(chkSMSNotifications);

                // Backup settings
                GroupBox grpBackup = new GroupBox
                {
                    Text = "Backup Settings",
                    Location = new Point(50, 180),
                    Size = new Size(400, 80),
                    ForeColor = AppColors.White
                };

                chkAutoBackup = new CheckBox { Text = "Enable Auto Backup", Location = new Point(20, 25), Size = new Size(150, 20), Checked = true, ForeColor = AppColors.White };
                Label lblBackupInterval = new Label { Text = "Backup Interval (hours):", Location = new Point(180, 27), Size = new Size(150, 20), ForeColor = AppColors.White };
                nudBackupInterval = new NumericUpDown { Location = new Point(320, 25), Size = new Size(60, 20), Minimum = 1, Maximum = 24, Value = 6 };

                grpBackup.Controls.Add(chkAutoBackup);
                grpBackup.Controls.Add(lblBackupInterval);
                grpBackup.Controls.Add(nudBackupInterval);

                // Save button
                btnSave = new Button
                {
                    Text = "Save Settings",
                    Location = new Point(175, 280),
                    Size = new Size(150, 35),
                    BackColor = AppColors.DarkRed,
                    ForeColor = AppColors.White
                };

                // Add controls
                this.Controls.Add(lblTitle);
                this.Controls.Add(grpNotifications);
                this.Controls.Add(grpBackup);
                this.Controls.Add(btnSave);

                // Event handler
                btnSave.Click += BtnSave_Click;
            }

            private void BtnSave_Click(object sender, EventArgs e)
            {
                string settings = $"Settings Saved:\n" +
                                 $"Email Notifications: {chkEmailNotifications.Checked}\n" +
                                 $"SMS Notifications: {chkSMSNotifications.Checked}\n" +
                                 $"Auto Backup: {chkAutoBackup.Checked}\n" +
                                 $"Backup Interval: {nudBackupInterval.Value} hours";
                MessageBox.Show(settings, "Settings Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // Book Appointment Form
        public class BookAppointmentForm : Form
        {
            private MonthCalendar calendar;
            private ListBox lstTimeSlots;
            private TextBox txtReason;
            private Button btnBook;
            private ComboBox cmbProvider;

            public BookAppointmentForm()
            {
                this.Text = "Book Appointment";
                this.Size = new Size(500, 400);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;

                // Title
                Label lblTitle = new Label
                {
                    Text = "BOOK APPOINTMENT",
                    Location = new Point(150, 15),
                    Size = new Size(200, 25),
                    Font = new Font("Arial", 12, FontStyle.Bold),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                calendar = new MonthCalendar
                {
                    Location = new Point(20, 50),
                    Size = new Size(227, 162),
                    MinDate = DateTime.Today
                };

                Label lblTime = new Label
                {
                    Text = "Available Time Slots:",
                    Location = new Point(270, 50),
                    Size = new Size(150, 20),
                    ForeColor = AppColors.White
                };

                lstTimeSlots = new ListBox
                {
                    Location = new Point(270, 75),
                    Size = new Size(150, 120),
                    BackColor = Color.White,
                    ForeColor = Color.Black
                };

                Label lblReason = new Label
                {
                    Text = "Reason for Visit:",
                    Location = new Point(20, 230),
                    Size = new Size(120, 20),
                    ForeColor = AppColors.White
                };

                txtReason = new TextBox
                {
                    Location = new Point(150, 230),
                    Size = new Size(300, 60),
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical
                };

                Label lblProvider = new Label
                {
                    Text = "Healthcare Provider:",
                    Location = new Point(20, 300),
                    Size = new Size(120, 20),
                    ForeColor = AppColors.White
                };

                cmbProvider = new ComboBox
                {
                    Location = new Point(150, 300),
                    Size = new Size(200, 20),
                    BackColor = Color.White,
                    ForeColor = Color.Black
                };

                btnBook = new Button
                {
                    Text = "Book Appointment",
                    Location = new Point(360, 300),
                    Size = new Size(120, 30),
                    BackColor = AppColors.DarkRed,
                    ForeColor = AppColors.White,
                    Font = new Font("Arial", 9, FontStyle.Bold)
                };

                // Add controls
                this.Controls.Add(lblTitle);
                this.Controls.Add(calendar);
                this.Controls.Add(lblTime);
                this.Controls.Add(lstTimeSlots);
                this.Controls.Add(lblReason);
                this.Controls.Add(txtReason);
                this.Controls.Add(lblProvider);
                this.Controls.Add(cmbProvider);
                this.Controls.Add(btnBook);

                // Event handlers
                calendar.DateChanged += (s, e) => LoadTimeSlots();
                cmbProvider.SelectedIndexChanged += (s, e) => LoadTimeSlots();
                btnBook.Click += BtnBook_Click;
                LoadProviders();
                LoadTimeSlots();
            }

            private void LoadProviders()
            {
                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = "SELECT UserID, FullName FROM Users WHERE RoleID = 2";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                cmbProvider.Items.Clear();
                                cmbProvider.DisplayMember = "Name";
                                cmbProvider.ValueMember = "ID";
                                while (reader.Read())
                                {
                                    cmbProvider.Items.Add(new
                                    {
                                        ID = reader.GetInt32(0),
                                        Name = reader.GetString(1)
                                    });
                                }
                                if (cmbProvider.Items.Count > 0)
                                    cmbProvider.SelectedIndex = 0;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading providers: " + ex.Message);
                }
            }

            private void LoadTimeSlots()
            {
                lstTimeSlots.Items.Clear();
                DateTime selectedDate = calendar.SelectionStart;

                // available time slots
                List<string> allSlots = new List<string> {
                    "09:00", "09:30", "10:00", "10:30", "11:00", "11:30",
                    "14:00", "14:30", "15:00", "15:30", "16:00", "16:30"
                };

                if (cmbProvider.SelectedItem == null)
                {
                    lstTimeSlots.Items.AddRange(allSlots.ToArray());
                    return;
                }

                dynamic provider = cmbProvider.SelectedItem;
                int providerID = provider.ID;

                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = "SELECT CONVERT(VARCHAR(5), TimeSlot, 108) as TimeSlot FROM Appointments WHERE ProviderID = @ProviderID AND AppointmentDate = @Date AND Status != 'Rejected'";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@ProviderID", providerID);
                            cmd.Parameters.AddWithValue("@Date", selectedDate.Date);
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                List<string> booked = new List<string>();
                                while (reader.Read())
                                    booked.Add(reader.GetTimeSpan(0).ToString(@"hh\:mm"));
                                allSlots.RemoveAll(slot => booked.Contains(slot));
                            }
                        }
                    }
                    lstTimeSlots.Items.AddRange(allSlots.ToArray());
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading time slots: " + ex.Message);
                }
            }

            private void BtnBook_Click(object sender, EventArgs e)
            {
                if (lstTimeSlots.SelectedItem == null || cmbProvider.SelectedItem == null || string.IsNullOrEmpty(txtReason.Text))
                {
                    MessageBox.Show("Please select a provider, time slot, and enter reason for visit.");
                    return;
                }

                dynamic provider = cmbProvider.SelectedItem;
                DateTime date = calendar.SelectionStart;
                TimeSpan time = TimeSpan.Parse(lstTimeSlots.SelectedItem.ToString());
                string reason = txtReason.Text;

                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = "INSERT INTO Appointments (PatientID, ProviderID, AppointmentDate, TimeSlot, Reason, Status) VALUES (@PatientID, @ProviderID, @Date, @Time, @Reason, 'Scheduled')";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@PatientID", CurrentUser.UserID);
                            cmd.Parameters.AddWithValue("@ProviderID", provider.ID);
                            cmd.Parameters.AddWithValue("@Date", date);
                            cmd.Parameters.AddWithValue("@Time", time);
                            cmd.Parameters.AddWithValue("@Reason", reason);
                            cmd.ExecuteNonQuery();

                            try
                            {
                                // Send confirmation email to the patient
                                _ = DatabaseHelper.SendEmailAsync(
                                    CurrentUser.Contact,  // Sends to the patient's email
                                    "Appointment Booked Successfully - Botho University Clinic",
                                    $"Dear {CurrentUser.FullName},<br><br>" +
                                    "Your appointment has been booked successfully!<br><br>" +
                                    $"<strong>Date:</strong> {date.ToShortDateString()}<br>" +
                                    $"<strong>Time:</strong> {time}<br>" +
                                    $"<strong>Healthcare Provider:</strong> {provider.Name}<br>" +
                                    $"<strong>Reason:</strong> {reason}<br><br>" +
                                    "You will receive a confirmation once the provider approves your appointment.<br><br>" +
                                    "Best regards,<br>Botho University Clinic Team"
                                );
                            }
                            catch (Exception emailEx)
                            {
                                Console.WriteLine($"Email failed: {emailEx.Message}");
                            }

                            MessageBox.Show("Appointment booked successfully! Waiting for provider confirmation.", "Success",
                                          MessageBoxButtons.OK, MessageBoxIcon.Information);
                            this.Close();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error booking appointment: " + ex.Message, "Error",
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // Appointment History Form
        public class AppointmentHistoryForm : Form
        {
            private DataGridView dgvHistory;

            public AppointmentHistoryForm()
            {
                this.Text = "Appointment History";
                this.Size = new Size(700, 500);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;

                // Title
                Label lblTitle = new Label
                {
                    Text = "APPOINTMENT HISTORY",
                    Location = new Point(250, 20),
                    Size = new Size(200, 30),
                    Font = new Font("Arial", 14, FontStyle.Bold),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                dgvHistory = new DataGridView
                {
                    Location = new Point(50, 70),
                    Size = new Size(600, 350),
                    BackgroundColor = Color.White,
                    ForeColor = Color.Black,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    ReadOnly = true
                };

                // Add controls
                this.Controls.Add(lblTitle);
                this.Controls.Add(dgvHistory);

                // Event handler
                LoadHistory();
            }

            private void LoadHistory()
            {
                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = @"SELECT a.AppointmentDate AS Date, a.TimeSlot AS Time,
                                        u.FullName AS Provider, a.Reason, a.Status,
                                        ISNULL(c.Diagnosis, 'Not completed') AS Diagnosis
                                        FROM Appointments a
                                        JOIN Users u ON a.ProviderID = u.UserID
                                        LEFT JOIN Consultations c ON a.AppointmentID = c.AppointmentID
                                        WHERE a.PatientID = @PatientID
                                        ORDER BY a.AppointmentDate DESC, a.TimeSlot DESC";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@PatientID", CurrentUser.UserID);
                            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                            {
                                DataTable dt = new DataTable();
                                adapter.Fill(dt);
                                dgvHistory.DataSource = dt;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading appointment history: " + ex.Message, "Error",
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        public class ConsultationForm : Form
        {
            private TabControl tabControl;
            private TextBox txtTemp, txtBP, txtHeartRate, txtWeight, txtHeight, txtNotes, txtDiagnosis;
            private DataGridView dgvHistory;
            private ComboBox cmbMedication;
            private TextBox txtDosage, txtFrequency, txtDuration;
            private ListBox lstPrescriptions;
            private Button btnAddPrescription, btnSave;
            private int appointmentID;

            public ConsultationForm(int apptID)
            {
                this.Text = "Patient Consultation";
                this.Size = new Size(600, 550);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;
                this.appointmentID = apptID;

                // Title
                Label lblTitle = new Label
                {
                    Text = "PATIENT CONSULTATION",
                    Location = new Point(200, 15),
                    Size = new Size(200, 25),
                    Font = new Font("Arial", 12, FontStyle.Bold),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                tabControl = new TabControl
                {
                    Location = new Point(20, 50),
                    Size = new Size(550, 400)
                };

                TabPage tabVitals = new TabPage("Vitals & Notes");
                TabPage tabHistory = new TabPage("Medical History");
                TabPage tabPrescriptions = new TabPage("Prescriptions");

                tabControl.TabPages.Add(tabVitals);
                tabControl.TabPages.Add(tabHistory);
                tabControl.TabPages.Add(tabPrescriptions);

                Label lblTemp = new Label { Text = "Temperature (°C):", Location = new Point(20, 30), Size = new Size(120, 20), ForeColor = Color.Black };
                txtTemp = new TextBox { Location = new Point(150, 30), Size = new Size(100, 20), Text = "36.6" };

                Label lblBP = new Label { Text = "Blood Pressure:", Location = new Point(20, 60), Size = new Size(120, 20), ForeColor = Color.Black };
                txtBP = new TextBox { Location = new Point(150, 60), Size = new Size(100, 20), Text = "120/80" };

                Label lblHeartRate = new Label { Text = "Heart Rate (bpm):", Location = new Point(20, 90), Size = new Size(120, 20), ForeColor = Color.Black };
                txtHeartRate = new TextBox { Location = new Point(150, 90), Size = new Size(100, 20), Text = "72" };

                Label lblWeight = new Label { Text = "Weight (kg):", Location = new Point(20, 120), Size = new Size(120, 20), ForeColor = Color.Black };
                txtWeight = new TextBox { Location = new Point(150, 120), Size = new Size(100, 20) };

                Label lblHeight = new Label { Text = "Height (cm):", Location = new Point(20, 150), Size = new Size(120, 20), ForeColor = Color.Black };
                txtHeight = new TextBox { Location = new Point(150, 150), Size = new Size(100, 20) };

                Label lblNotes = new Label { Text = "Clinical Notes:", Location = new Point(20, 180), Size = new Size(120, 20), ForeColor = Color.Black };
                txtNotes = new TextBox { Location = new Point(150, 180), Size = new Size(350, 80), Multiline = true, ScrollBars = ScrollBars.Vertical };

                Label lblDiagnosis = new Label { Text = "Diagnosis:", Location = new Point(20, 270), Size = new Size(120, 20), ForeColor = Color.Black };
                txtDiagnosis = new TextBox { Location = new Point(150, 270), Size = new Size(350, 20) };

                tabVitals.Controls.AddRange(new Control[] { lblTemp, txtTemp, lblBP, txtBP, lblHeartRate, txtHeartRate,
                                                          lblWeight, txtWeight, lblHeight, txtHeight, lblNotes, txtNotes,
                                                          lblDiagnosis, txtDiagnosis });

                dgvHistory = new DataGridView
                {
                    Location = new Point(10, 20),
                    Size = new Size(520, 350),
                    BackgroundColor = Color.White,
                    ForeColor = Color.Black,
                    ReadOnly = true
                };
                tabHistory.Controls.Add(dgvHistory);

                Label lblMed = new Label { Text = "Medication:", Location = new Point(20, 30), Size = new Size(80, 20), ForeColor = Color.Black };
                cmbMedication = new ComboBox { Location = new Point(110, 30), Size = new Size(150, 20) };
                cmbMedication.Items.AddRange(new string[] { "Amoxicillin", "Ibuprofen", "Paracetamol", "Aspirin", "Antibiotic", "Antihistamine", "Vitamins" });

                Label lblDosage = new Label { Text = "Dosage:", Location = new Point(20, 60), Size = new Size(80, 20), ForeColor = Color.Black };
                txtDosage = new TextBox { Location = new Point(110, 60), Size = new Size(150, 20), Text = "500mg" };

                Label lblFrequency = new Label { Text = "Frequency:", Location = new Point(20, 90), Size = new Size(80, 20), ForeColor = Color.Black };
                txtFrequency = new TextBox { Location = new Point(110, 90), Size = new Size(150, 20), Text = "3 times daily" };

                Label lblDuration = new Label { Text = "Duration:", Location = new Point(20, 120), Size = new Size(80, 20), ForeColor = Color.Black };
                txtDuration = new TextBox { Location = new Point(110, 120), Size = new Size(150, 20), Text = "7 days" };

                btnAddPrescription = new Button
                {
                    Text = "Add Prescription",
                    Location = new Point(280, 30),
                    Size = new Size(120, 30),
                    BackColor = AppColors.DarkRed,
                    ForeColor = AppColors.White
                };

                lstPrescriptions = new ListBox
                {
                    Location = new Point(20, 160),
                    Size = new Size(500, 180),
                    BackColor = Color.White,
                    ForeColor = Color.Black
                };

                tabPrescriptions.Controls.AddRange(new Control[] { lblMed, cmbMedication, lblDosage, txtDosage, lblFrequency, txtFrequency,
                                                                 lblDuration, txtDuration, btnAddPrescription, lstPrescriptions });

                // Save Button
                btnSave = new Button
                {
                    Text = "Save Consultation",
                    Location = new Point(230, 470),
                    Size = new Size(150, 40),
                    BackColor = AppColors.DarkRed,
                    ForeColor = AppColors.White,
                    Font = new Font("Arial", 10, FontStyle.Bold)
                };

                // Add controls
                this.Controls.Add(lblTitle);
                this.Controls.Add(tabControl);
                this.Controls.Add(btnSave);

                // Event handlers
                btnAddPrescription.Click += BtnAddPrescription_Click;
                btnSave.Click += BtnSave_Click;
                LoadMedicalHistory();
            }

            private void LoadMedicalHistory()
            {
                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = @"SELECT c.ConsultationDate, c.Diagnosis, c.Notes, c.Temperature, c.BloodPressure
                                         FROM Consultations c
                                         JOIN Appointments a ON c.AppointmentID = a.AppointmentID
                                         WHERE a.PatientID = (SELECT PatientID FROM Appointments WHERE AppointmentID = @ApptID)
                                         ORDER BY c.ConsultationDate DESC";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@ApptID", appointmentID);
                            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                            {
                                DataTable dt = new DataTable();
                                adapter.Fill(dt);
                                dgvHistory.DataSource = dt;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading medical history: " + ex.Message);
                }
            }

            private void BtnAddPrescription_Click(object sender, EventArgs e)
            {
                string med = cmbMedication.SelectedItem?.ToString();
                string dosage = txtDosage.Text.Trim();
                string frequency = txtFrequency.Text.Trim();
                string duration = txtDuration.Text.Trim();

                if (!string.IsNullOrEmpty(med) && !string.IsNullOrEmpty(dosage))
                {
                    string prescription = $"{med} - {dosage} - {frequency} - {duration}";
                    lstPrescriptions.Items.Add(prescription);

                    cmbMedication.SelectedIndex = -1;
                    txtDosage.Clear();
                    txtFrequency.Clear();
                    txtDuration.Clear();
                }
                else
                {
                    MessageBox.Show("Please enter medication and dosage.");
                }
            }

            private void BtnSave_Click(object sender, EventArgs e)
            {
                if (!decimal.TryParse(txtTemp.Text, out decimal temp))
                {
                    MessageBox.Show("Invalid temperature format.");
                    return;
                }

                if (!int.TryParse(txtHeartRate.Text, out int heartRate))
                {
                    MessageBox.Show("Invalid heart rate format.");
                    return;
                }

                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string consQuery = @"INSERT INTO Consultations (AppointmentID, Temperature, BloodPressure, HeartRate, Weight, Height, Notes, Diagnosis)
                                           VALUES (@ApptID, @Temp, @BP, @HeartRate, @Weight, @Height, @Notes, @Diag);
                                           SELECT SCOPE_IDENTITY();";
                        using (SqlCommand cmdCons = new SqlCommand(consQuery, conn))
                        {
                            cmdCons.Parameters.AddWithValue("@ApptID", appointmentID);
                            cmdCons.Parameters.AddWithValue("@Temp", temp);
                            cmdCons.Parameters.AddWithValue("@BP", txtBP.Text.Trim());
                            cmdCons.Parameters.AddWithValue("@HeartRate", heartRate);
                            cmdCons.Parameters.AddWithValue("@Weight", string.IsNullOrEmpty(txtWeight.Text) ? (object)DBNull.Value : decimal.Parse(txtWeight.Text));
                            cmdCons.Parameters.AddWithValue("@Height", string.IsNullOrEmpty(txtHeight.Text) ? (object)DBNull.Value : decimal.Parse(txtHeight.Text));
                            cmdCons.Parameters.AddWithValue("@Notes", txtNotes.Text.Trim());
                            cmdCons.Parameters.AddWithValue("@Diag", txtDiagnosis.Text.Trim());
                            int consID = Convert.ToInt32(cmdCons.ExecuteScalar());

                            // Saving the added and all prescriptions
                            foreach (string item in lstPrescriptions.Items)
                            {
                                string[] parts = item.Split(new[] { " - " }, StringSplitOptions.None);
                                if (parts.Length >= 2)
                                {
                                    using (SqlCommand cmdPres = new SqlCommand("INSERT INTO Prescriptions (ConsultationID, Medication, Dosage, Frequency, Duration) VALUES (@ConsID, @Med, @Dos, @Freq, @Dur)", conn))
                                    {
                                        cmdPres.Parameters.AddWithValue("@ConsID", consID);
                                        cmdPres.Parameters.AddWithValue("@Med", parts[0]);
                                        cmdPres.Parameters.AddWithValue("@Dos", parts[1]);
                                        cmdPres.Parameters.AddWithValue("@Freq", parts.Length > 2 ? parts[2] : "");
                                        cmdPres.Parameters.AddWithValue("@Dur", parts.Length > 3 ? parts[3] : "");
                                        cmdPres.ExecuteNonQuery();
                                    }
                                }
                            }

                            // Updates appointment status
                            using (SqlCommand cmdUpdate = new SqlCommand("UPDATE Appointments SET Status = 'Completed' WHERE AppointmentID = @ApptID", conn))
                            {
                                cmdUpdate.Parameters.AddWithValue("@ApptID", appointmentID);
                                cmdUpdate.ExecuteNonQuery();
                            }
                        }

                        MessageBox.Show("Consultation saved successfully!", "Success",
                                      MessageBoxButtons.OK, MessageBoxIcon.Information);
                        this.Close();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error saving consultation: " + ex.Message, "Error",
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // User Registration Form (Admin Functionality Only)
        public class UserRegistrationForm : Form
        {
            private TextBox txtID, txtFullName, txtContact;
            private ComboBox cmbRole;
            private Button btnRegister;
            private LinkLabel lnkBackToHome;

            public UserRegistrationForm()
            {
                this.Text = "User Registration";
                this.Size = new Size(400, 350);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;

                Label lblTitle = new Label
                {
                    Text = "REGISTER NEW USER",
                    Location = new Point(100, 20),
                    Size = new Size(200, 25),
                    Font = new Font("Arial", 12, FontStyle.Bold),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                Label lblID = new Label { Text = "ID Number:", Location = new Point(30, 60), Size = new Size(80, 20), ForeColor = AppColors.White };
                txtID = new TextBox { Location = new Point(120, 60), Size = new Size(200, 20) };

                Label lblName = new Label { Text = "Full Name:", Location = new Point(30, 90), Size = new Size(80, 20), ForeColor = AppColors.White };
                txtFullName = new TextBox { Location = new Point(120, 90), Size = new Size(200, 20) };

                Label lblContact = new Label { Text = "Contact:", Location = new Point(30, 120), Size = new Size(80, 20), ForeColor = AppColors.White };
                txtContact = new TextBox { Location = new Point(120, 120), Size = new Size(200, 20) };

                Label lblRole = new Label { Text = "Role:", Location = new Point(30, 150), Size = new Size(80, 20), ForeColor = AppColors.White };
                cmbRole = new ComboBox { Location = new Point(120, 150), Size = new Size(200, 20) };
                cmbRole.Items.AddRange(new string[] { "Student", "Healthcare Provider" });

                btnRegister = new Button
                {
                    Text = "Register User",
                    Location = new Point(120, 190),
                    Size = new Size(120, 35),
                    BackColor = AppColors.DarkRed,
                    ForeColor = AppColors.White,
                    Font = new Font("Arial", 9, FontStyle.Bold)
                };

                lnkBackToHome = new LinkLabel
                {
                    Text = "← Back to Home",
                    Location = new Point(140, 240),
                    Size = new Size(100, 20),
                    ForeColor = AppColors.LightBlue,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                this.Controls.Add(lblTitle);
                this.Controls.Add(lblID);
                this.Controls.Add(txtID);
                this.Controls.Add(lblName);
                this.Controls.Add(txtFullName);
                this.Controls.Add(lblContact);
                this.Controls.Add(txtContact);
                this.Controls.Add(lblRole);
                this.Controls.Add(cmbRole);
                this.Controls.Add(btnRegister);
                this.Controls.Add(lnkBackToHome);

                btnRegister.Click += BtnRegister_Click;
                lnkBackToHome.Click += (s, e) => { this.Hide(); new HomePageForm().Show(); };
            }

            private void BtnRegister_Click(object sender, EventArgs e)
            {
                string id = txtID.Text.Trim();
                string name = txtFullName.Text.Trim();
                string contact = txtContact.Text.Trim();
                string role = cmbRole.SelectedItem?.ToString();

                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(role))
                {
                    MessageBox.Show("Please fill all required fields.");
                    return;
                }

                string defaultPassword = "default123";
                string hashed = DatabaseHelper.HashPassword(defaultPassword);
                string username = id.ToLower();

                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        int roleID = 0;
                        using (SqlCommand cmdRole = new SqlCommand("SELECT RoleID FROM Roles WHERE RoleName = @RoleName", conn))
                        {
                            cmdRole.Parameters.AddWithValue("@RoleName", role);
                            roleID = (int)cmdRole.ExecuteScalar();
                        }

                        using (SqlCommand cmdUser = new SqlCommand("INSERT INTO Users (Username, HashedPassword, FullName, Contact, RoleID) VALUES (@Username, @Hashed, @Name, @Contact, @RoleID); SELECT SCOPE_IDENTITY();", conn))
                        {
                            cmdUser.Parameters.AddWithValue("@Username", username);
                            cmdUser.Parameters.AddWithValue("@Hashed", hashed);
                            cmdUser.Parameters.AddWithValue("@Name", name);
                            cmdUser.Parameters.AddWithValue("@Contact", contact);
                            cmdUser.Parameters.AddWithValue("@RoleID", roleID);
                            int newUserID = Convert.ToInt32(cmdUser.ExecuteScalar());

                            if (role == "Student")
                            {
                                using (SqlCommand cmdPatient = new SqlCommand("INSERT INTO Patients (UserID, StudentID) VALUES (@UserID, @StudentID)", conn))
                                {
                                    cmdPatient.Parameters.AddWithValue("@UserID", newUserID);
                                    cmdPatient.Parameters.AddWithValue("@StudentID", id);
                                    cmdPatient.ExecuteNonQuery();
                                }
                            }
                        }

                        try
                        {
                            // Send welcome email to the new user
                            _ = DatabaseHelper.SendEmailAsync(
                                contact,  // This sends to the user's contact email
                                "Welcome to Botho University Clinic",
                                $"Dear {name},<br><br>" +
                                "Your account has been successfully created!<br>" +
                                $"<strong>Username:</strong> {username}<br>" +
                                $"<strong>Temporary Password:</strong> {defaultPassword}<br><br>" +
                                "Please change your password after first login.<br><br>" +
                                "Best regards,<br>Botho University Clinic Team"
                            );
                        }
                        catch (Exception emailEx)
                        {
                            // If email fails, still show success but log the error
                            Console.WriteLine($"Email failed: {emailEx.Message}");
                        }

                        MessageBox.Show($"User registered successfully!\n\nUsername: {username}\nDefault Password: {defaultPassword}\n\nPlease change the password after first login.",
                                      "Registration Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        this.Close();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message, "Registration Error",
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        public class StaffManagementForm : Form
        {
            private DataGridView dgvStaff;
            private Button btnRegisterNew, btnRefresh, btnDeleteUser;

            public StaffManagementForm()
            {
                this.Text = "Staff Management";
                this.Size = new Size(700, 500);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;

                // Title
                Label lblTitle = new Label
                {
                    Text = "STAFF MANAGEMENT",
                    Location = new Point(250, 20),
                    Size = new Size(200, 25),
                    Font = new Font("Arial", 14, FontStyle.Bold),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                dgvStaff = new DataGridView
                {
                    Location = new Point(50, 60),
                    Size = new Size(600, 300),
                    BackgroundColor = Color.White,
                    ForeColor = Color.Black,
                    ReadOnly = true,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect
                };

                btnRegisterNew = new Button
                {
                    Text = "Register New",
                    Location = new Point(50, 380),
                    Size = new Size(120, 35),
                    BackColor = AppColors.DarkRed,
                    ForeColor = AppColors.White
                };

                btnRefresh = new Button
                {
                    Text = "Refresh",
                    Location = new Point(180, 380),
                    Size = new Size(120, 35),
                    BackColor = AppColors.DarkRed,
                    ForeColor = AppColors.White
                };

                // Add Delete User button
                btnDeleteUser = new Button
                {
                    Text = "Delete User",
                    Location = new Point(310, 380),
                    Size = new Size(120, 35),
                    BackColor = Color.Red, 
                    ForeColor = AppColors.White,
                    Font = new Font("Arial", 9, FontStyle.Bold)
                };

                // Add controls
                this.Controls.Add(lblTitle);
                this.Controls.Add(dgvStaff);
                this.Controls.Add(btnRegisterNew);
                this.Controls.Add(btnRefresh);
                this.Controls.Add(btnDeleteUser);

                // Event handlers
                btnRegisterNew.Click += (s, e) => { new UserRegistrationForm().ShowDialog(); LoadStaff(); };
                btnRefresh.Click += (s, e) => LoadStaff();
                btnDeleteUser.Click += BtnDeleteUser_Click;

                LoadStaff();
            }

            private void LoadStaff()
            {
                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = @"SELECT u.UserID, u.Username, u.FullName, u.Contact, r.RoleName
                                FROM Users u
                                JOIN Roles r ON u.RoleID = r.RoleID
                                ORDER BY u.FullName";
                        using (SqlDataAdapter adapter = new SqlDataAdapter(query, conn))
                        {
                            DataTable dt = new DataTable();
                            adapter.Fill(dt);
                            dgvStaff.DataSource = dt;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading staff: " + ex.Message);
                }
            }

            private void BtnDeleteUser_Click(object sender, EventArgs e)
            {
                if (dgvStaff.SelectedRows.Count == 0)
                {
                    MessageBox.Show("Please select a user to delete.", "No Selection",
                                  MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                DataGridViewRow selectedRow = dgvStaff.SelectedRows[0];
                int userID = Convert.ToInt32(selectedRow.Cells["UserID"].Value);
                string username = selectedRow.Cells["Username"].Value.ToString();
                string fullName = selectedRow.Cells["FullName"].Value.ToString();
                string role = selectedRow.Cells["RoleName"].Value.ToString();

                // Prevent admin from deleting themselves
                if (userID == CurrentUser.UserID)
                {
                    MessageBox.Show("You cannot delete your own account!", "Invalid Operation",
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Confirm deletion
                DialogResult result = MessageBox.Show(
                    $"Are you sure you want to delete user:\n\n" +
                    $"Name: {fullName}\n" +
                    $"Username: {username}\n" +
                    $"Role: {role}\n\n" +
                    $"This action cannot be undone!",
                    "Confirm User Deletion",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        using (SqlConnection conn = DatabaseHelper.GetConnection())
                        {
                            conn.Open();

                            // Use transaction to ensure data integrity
                            using (SqlTransaction transaction = conn.BeginTransaction())
                            {
                                try
                                {


                                    // Delete from Patients table if exists
                                    string deletePatientQuery = "DELETE FROM Patients WHERE UserID = @UserID";
                                    using (SqlCommand cmd = new SqlCommand(deletePatientQuery, conn, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@UserID", userID);
                                        cmd.ExecuteNonQuery();
                                    }

                                    // Delete from ProviderAvailability if exists
                                    string deleteAvailabilityQuery = "DELETE FROM ProviderAvailability WHERE ProviderID = @UserID";
                                    using (SqlCommand cmd = new SqlCommand(deleteAvailabilityQuery, conn, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@UserID", userID);
                                        cmd.ExecuteNonQuery();
                                    }

                                    // Delete appointments where user is patient or provider
                                    string deleteAppointmentsQuery = @"
                                DELETE FROM Appointments 
                                WHERE PatientID = @UserID OR ProviderID = @UserID";
                                    using (SqlCommand cmd = new SqlCommand(deleteAppointmentsQuery, conn, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@UserID", userID);
                                        cmd.ExecuteNonQuery();
                                    }

                                    // delete the user
                                    string deleteUserQuery = "DELETE FROM Users WHERE UserID = @UserID";
                                    using (SqlCommand cmd = new SqlCommand(deleteUserQuery, conn, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@UserID", userID);
                                        int rowsAffected = cmd.ExecuteNonQuery();

                                        if (rowsAffected > 0)
                                        {
                                            transaction.Commit();
                                            MessageBox.Show($"User '{fullName}' has been successfully deleted.",
                                                          "User Deleted",
                                                          MessageBoxButtons.OK,
                                                          MessageBoxIcon.Information);
                                            LoadStaff(); 
                                        }
                                        else
                                        {
                                            transaction.Rollback();
                                            MessageBox.Show("Failed to delete user.", "Error",
                                                          MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    transaction.Rollback();
                                    throw ex;
                                }
                            }
                        }
                    }
                    catch (SqlException sqlEx)
                    {
                        MessageBox.Show($"Database error while deleting user: {sqlEx.Message}",
                                      "Database Error",
                                      MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error deleting user: {ex.Message}",
                                      "Error",
                                      MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        public class ReportingForm : Form
        {
            private DateTimePicker dtpStart, dtpEnd;
            private Button btnGenerateReport, btnExportCSV, btnAppointmentStats;
            private DataGridView dgvReport;
            private ComboBox cmbReportType;

            public ReportingForm()
            {
                this.Text = "Reporting & Analytics";
                this.Size = new Size(800, 600);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = AppColors.NavyBlue;
                this.ForeColor = AppColors.White;

                // Title
                Label lblTitle = new Label
                {
                    Text = "REPORTING & ANALYTICS",
                    Location = new Point(250, 20),
                    Size = new Size(300, 25),
                    Font = new Font("Arial", 14, FontStyle.Bold),
                    ForeColor = AppColors.White,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                // Report Type
                Label lblReportType = new Label { Text = "Report Type:", Location = new Point(50, 60), Size = new Size(80, 20), ForeColor = AppColors.White };
                cmbReportType = new ComboBox { Location = new Point(140, 60), Size = new Size(150, 20) };
                cmbReportType.Items.AddRange(new string[] { "Appointments", "Users", "Consultations" });
                cmbReportType.SelectedIndex = 0;

                // Date Range
                Label lblStart = new Label { Text = "Start Date:", Location = new Point(50, 90), Size = new Size(80, 20), ForeColor = AppColors.White };
                dtpStart = new DateTimePicker { Location = new Point(140, 90), Size = new Size(120, 20), Value = DateTime.Today.AddDays(-30) };

                Label lblEnd = new Label { Text = "End Date:", Location = new Point(270, 90), Size = new Size(80, 20), ForeColor = AppColors.White };
                dtpEnd = new DateTimePicker { Location = new Point(360, 90), Size = new Size(120, 20), Value = DateTime.Today };

                btnGenerateReport = new Button
                {
                    Text = "Generate Report",
                    Location = new Point(500, 60),
                    Size = new Size(120, 35),
                    BackColor = AppColors.DarkRed,
                    ForeColor = AppColors.White
                };

                btnExportCSV = new Button
                {
                    Text = "Export to CSV",
                    Location = new Point(500, 105),
                    Size = new Size(120, 35),
                    BackColor = AppColors.DarkRed,
                    ForeColor = AppColors.White
                };

                btnAppointmentStats = new Button
                {
                    Text = "Appointment Stats",
                    Location = new Point(630, 60),
                    Size = new Size(120, 35),
                    BackColor = AppColors.DarkRed,
                    ForeColor = AppColors.White
                };

                // Report Grid
                dgvReport = new DataGridView
                {
                    Location = new Point(50, 150),
                    Size = new Size(700, 350),
                    BackgroundColor = Color.White,
                    ForeColor = Color.Black,
                    ReadOnly = true
                };

                // Add controls
                this.Controls.Add(lblTitle);
                this.Controls.Add(lblReportType);
                this.Controls.Add(cmbReportType);
                this.Controls.Add(lblStart);
                this.Controls.Add(dtpStart);
                this.Controls.Add(lblEnd);
                this.Controls.Add(dtpEnd);
                this.Controls.Add(btnGenerateReport);
                this.Controls.Add(btnExportCSV);
                this.Controls.Add(btnAppointmentStats);
                this.Controls.Add(dgvReport);

                // Event handlers
                btnGenerateReport.Click += BtnGenerateReport_Click;
                btnExportCSV.Click += BtnExportCSV_Click;
                btnAppointmentStats.Click += BtnAppointmentStats_Click;

                // Load report
                BtnGenerateReport_Click(null, null);
            }

            private void BtnGenerateReport_Click(object sender, EventArgs e)
            {
                string reportType = cmbReportType.SelectedItem?.ToString();
                DateTime start = dtpStart.Value.Date;
                DateTime end = dtpEnd.Value.Date.AddDays(1);

                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = "";
                        switch (reportType)
                        {
                            case "Appointments":
                                query = @"SELECT a.AppointmentDate, a.TimeSlot,
                                         u1.FullName AS Patient, u2.FullName AS Provider,
                                         a.Reason, a.Status
                                         FROM Appointments a
                                         JOIN Users u1 ON a.PatientID = u1.UserID
                                         JOIN Users u2 ON a.ProviderID = u2.UserID
                                         WHERE a.AppointmentDate BETWEEN @Start AND @End
                                         ORDER BY a.AppointmentDate DESC";
                                break;
                            case "Users":
                                query = @"SELECT u.Username, u.FullName, u.Contact, r.RoleName
                                         FROM Users u
                                         JOIN Roles r ON u.RoleID = r.RoleID
                                         ORDER BY u.FullName";
                                break;
                            case "Consultations":
                                query = @"SELECT c.ConsultationDate, u.FullName AS Patient,
                                         c.Diagnosis, c.Temperature, c.BloodPressure
                                         FROM Consultations c
                                         JOIN Appointments a ON c.AppointmentID = a.AppointmentID
                                         JOIN Users u ON a.PatientID = u.UserID
                                         WHERE c.ConsultationDate BETWEEN @Start AND @End
                                         ORDER BY c.ConsultationDate DESC";
                                break;
                        }

                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            if (reportType != "Users")
                            {
                                cmd.Parameters.AddWithValue("@Start", start);
                                cmd.Parameters.AddWithValue("@End", end);
                            }
                            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                            {
                                DataTable dt = new DataTable();
                                adapter.Fill(dt);
                                dgvReport.DataSource = dt;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error generating report: " + ex.Message);
                }
            }

            private void BtnExportCSV_Click(object sender, EventArgs e)
            {
                try
                {
                    SaveFileDialog saveFile = new SaveFileDialog();
                    saveFile.Filter = "CSV files (*.csv)|*.csv";
                    saveFile.Title = "Export Report";
                    if (saveFile.ShowDialog() == DialogResult.OK)
                    {
                        using (StreamWriter writer = new StreamWriter(saveFile.FileName))
                        {
                            // Write headers
                            for (int i = 0; i < dgvReport.Columns.Count; i++)
                            {
                                writer.Write(dgvReport.Columns[i].HeaderText);
                                if (i < dgvReport.Columns.Count - 1)
                                    writer.Write(",");
                            }
                            writer.WriteLine();

                            // Write data
                            foreach (DataGridViewRow row in dgvReport.Rows)
                            {
                                for (int i = 0; i < dgvReport.Columns.Count; i++)
                                {
                                    writer.Write(row.Cells[i].Value?.ToString());
                                    if (i < dgvReport.Columns.Count - 1)
                                        writer.Write(",");
                                }
                                writer.WriteLine();
                            }
                        }
                        MessageBox.Show("Report exported successfully!", "Success",
                                      MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error exporting report: " + ex.Message);
                }
            }

            private void BtnAppointmentStats_Click(object sender, EventArgs e)
            {
                try
                {
                    using (SqlConnection conn = DatabaseHelper.GetConnection())
                    {
                        conn.Open();
                        string query = @"SELECT
                                        Status,
                                        COUNT(*) as Count,
                                        CAST(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM Appointments WHERE AppointmentDate BETWEEN @Start AND @End) AS DECIMAL(5,2)) as Percentage
                                        FROM Appointments
                                        WHERE AppointmentDate BETWEEN @Start AND @End
                                        GROUP BY Status
                                        ORDER BY Count DESC";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@Start", dtpStart.Value.Date);
                            cmd.Parameters.AddWithValue("@End", dtpEnd.Value.Date.AddDays(1));
                            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                            {
                                DataTable dt = new DataTable();
                                adapter.Fill(dt);
                                dgvReport.DataSource = dt;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading appointment statistics: " + ex.Message);
                }
            }
        }

        // Test Email Form for testing email functionality
        public class TestEmailForm : Form
        {
            public TestEmailForm()
            {
                this.Text = "Test Email";
                this.Size = new Size(300, 200);
                this.StartPosition = FormStartPosition.CenterScreen;

                Button btnTest = new Button
                {
                    Text = "Send Test Email",
                    Location = new Point(80, 50),
                    Size = new Size(120, 40),
                    BackColor = AppColors.DarkRed,
                    ForeColor = AppColors.White
                };

                btnTest.Click += async (s, e) =>
                {
                    try
                    {
                        await DatabaseHelper.SendEmailAsync(
                            "refiloe.maphakisa@bothouniversity.com",  
                            "Test Email from BU Clinic System",
                            "This is a test email from the Botho University Clinic Management System.<br><br>" +
                            "If you received this, email notifications are working correctly, Ciao dear!"
                        );
                        MessageBox.Show("Test email sent! Check your inbox.", "Success");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to send email: {ex.Message}", "Error");
                    }
                };

                this.Controls.Add(btnTest);
            }
        }
    }
}
