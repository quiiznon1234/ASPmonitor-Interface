using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace ASPMonitorInterface
{
    public partial class Form1 : Form
    {
        Rax.Utility.IniFile inFile;
        string folderresultfile;
        string connectionString;
        private bool isProcessing = false;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private bool isRunning = false;
        private System.Windows.Forms.Timer timerStatusLabel;
        string bakPath;
        bool isAutoStart = false;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                string filename = Path.Combine(Application.StartupPath, "uti.txt");
                inFile = new Rax.Utility.IniFile(filename);
                connectionString = inFile.IniReadValue("Mycom", "ConnectionString");
                folderresultfile = inFile.IniReadValue("Mycom", "Folderresultfile");
                bakPath = inFile.IniReadValue("Mycom", "BackupPath")?.Trim();
                isAutoStart = Convert.ToBoolean(Convert.ToInt32(inFile.IniReadValue("Mycom", "IsAutoStart")));
                checkBox1.Checked = isAutoStart;

                if (!string.IsNullOrWhiteSpace(bakPath)) textBox1.Text = bakPath;
                else WriteList("ยังไม่ได้ตั้งค่าโฟลเดอร์ BackUp กรุณาไปที่เมนูตั้งค่า");

                if (!string.IsNullOrWhiteSpace(folderresultfile)) txtFoderFileResult.Text = folderresultfile;
                else WriteList("ยังไม่ได้ตั้งค่าโฟลเดอร์ Result กรุณาไปที่เมนูตั้งค่า");

                // ตรวจสอบเงื่อนไขก่อน Auto Start
                if (isAutoStart &&
                    !string.IsNullOrWhiteSpace(connectionString) &&
                    !string.IsNullOrWhiteSpace(folderresultfile) &&
                    !string.IsNullOrWhiteSpace(bakPath) &&
                    Directory.Exists(folderresultfile))
                {
                    timerReadFile.Enabled = true;
                    timerStatusLabel.Start();
                    btnStart.Text = "Stop";
                    isRunning = true;
                    Task.Run(async () => await LoadFileAsync());
                }
                else
                {
                    btnStart.Text = "Start";
                }
            }
            catch (Exception ex)
            {
                WriteList($"เกิดข้อผิดพลาดในการโหลดค่าเริ่มต้น: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }


        private HashSet<string> processedFiles = new HashSet<string>();
        private bool isFirstRun = true;
        private bool hasShownWaitingToast = false;
        private async Task LoadFileAsync()
        {
            if (string.IsNullOrWhiteSpace(folderresultfile))
            {
                if (isFirstRun)
                {
                    WriteList("❌ ยังไม่ได้ตั้งค่าโฟลเดอร์!");
                    ShowToastNotification("❌ ยังไม่ได้ตั้งค่าโฟลเดอร์!", 2500);
                    isFirstRun = false;
                }
                return;
            }

            if (!Directory.Exists(folderresultfile))
            {
                ShowToastNotification("❌ โฟลเดอร์ที่กำหนดไม่มีอยู่จริง", 2000);
                return;
            }

            var files = Directory.GetFiles(folderresultfile, "*.HL7")
                                 .OrderByDescending(f => new FileInfo(f).CreationTime)
                                 .Where(f => !processedFiles.Contains(f))
                                 .ToList();

            if (!files.Any())
            {
                if (!hasShownWaitingToast)
                {
                    ShowToastNotification("⏳ กำลังรอไฟล์ใหม่...", 2000);
                    hasShownWaitingToast = true;
                }
                return;
            }
            hasShownWaitingToast = false;

            foreach (var file in files)
            {
                processedFiles.Add(file);
                await ProcessFileAsync(file);
            }

            ShowToastNotification("✅ อ่านและบันทึกไฟล์ทั้งหมดเสร็จสิ้น!", 3000);
            WriteList("✅ บันทึกไฟล์สำเร็จจำนวน " + files.Count + " ไฟล์");
        }

        private string CreateBackupFolder(string folder)
        {
            //จะ return Text ประมาณนี้  D:\Project\Keychain\2567\02\27
            string basePath = string.IsNullOrWhiteSpace(bakPath)
                                ? Path.Combine(Application.StartupPath, "Backup")
                                : bakPath;
            string folderName = bakPath + "\\" +DateTime.Now.ToString("yyyy") + "\\" +
                                DateTime.Now.ToString("MM") + "\\" +
                                DateTime.Now.ToString("dd") + "\\" + folder + "\\";

            // ตรวจสอบว่ามีโฟลเดอร์แล้วหรือยัง
            if (!Directory.Exists(folderName))
            {
                Directory.CreateDirectory(folderName);
            }
            return folderName;
        }

        private async Task ProcessFileAsync(string filePath)
        {
            try
            {
                string text = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(text))
                {
                    File.Delete(filePath);
                    return;
                }

                await SendToMonitorAsync(text.Trim(), filePath);

                string backupFolder = CreateBackupFolder("Processed");
                string backupFilePath = Path.Combine(backupFolder, Path.GetFileName(filePath));

                if (!File.Exists(backupFilePath))
                {
                    File.Move(filePath, backupFilePath);
                }
            }
            catch (Exception ex)
            {
                WriteList($"เกิดข้อผิดพลาดในการประมวลผลไฟล์ {Path.GetFileName(filePath)}: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        private async Task SendToMonitorAsync(string text, string fileName)
        {
            string patientName = string.Empty;
            string barcode = string.Empty;
            string requestDate = string.Empty;
            string sectionName = string.Empty;
            string testName = string.Empty;

            string[] arrmessage = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (!arrmessage.Any())
            {
                File.Delete(fileName);
                return;
            }

            foreach (string str in arrmessage)
            {
                string[] header = str.Split('|');

                if (header.Length > 5 && header[0] == "PID")
                {
                    string[] name = header[5].Split('^');
                    if (name.Length >= 2)
                    {
                        patientName = $"{name[2]} {name[0]}".Trim();
                    }
                }

                if (header.Length > 12 && header[0] == "ORC")
                {
                    barcode = header[2]?.Trim();
                    requestDate = header[8]?.Trim();

                    string[] sectionFields = header[11].Split('^');
                    if (sectionFields.Length >= 3)
                    {
                        sectionName = sectionFields[2]?.Trim();
                    }
                }

                if (header.Length > 4 && header[0] == "OBR")
                {
                    string[] testFields = header[4].Split('^');
                    if (testFields.Length >= 3)
                    {
                        testName = testFields[2]?.Trim();
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(barcode) &&
                !string.IsNullOrWhiteSpace(requestDate) &&
                !string.IsNullOrWhiteSpace(sectionName) &&
                !string.IsNullOrWhiteSpace(testName) &&
                !string.IsNullOrWhiteSpace(patientName))
            {
                if (DateTime.TryParseExact(requestDate, "yyyyMMddHHmmss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime requestDateTime))
                {
                    await SaveToDatabaseAsync(barcode, requestDateTime, sectionName, testName, patientName);

                    WriteList($"ผู้ป่วย: {patientName}");
                }
            }
            else
            {
                string errorFolder = CreateBackupFolder("Error"); // 🔹 เก็บไฟล์ที่ผิดพลาด
                string errorFilePath = Path.Combine(errorFolder, Path.GetFileName(fileName));
                File.Move(fileName, errorFilePath);

                WriteList($"ไฟล์: {Path.GetFileName(fileName)} มีข้อมูลวันที่ไม่ถูกต้อง");
            }
        }

        /// Connec Database
        private async Task SaveToDatabaseAsync(string barcode, DateTime requestDate, string sectionName, string testName, string patientName)
        {
            string connectionString = @"Data Source=localhost\SQL2019;Initial Catalog=aspdashboard;Integrated Security=True;User Id=sa;Password=N@wee2546";
            connectionString = this.inFile.IniReadValue("MyCom", "ConnectionString");
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        INSERT INTO PatienRoom (BarCode, DateTime, SectionName, TestName, PatientName) 
                        VALUES (@Barcode, @DateTime, @SectionName, @TestName, @PatientName)";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Barcode", barcode);
                        command.Parameters.AddWithValue("@DateTime", requestDate);
                        command.Parameters.AddWithValue("@SectionName", sectionName);
                        command.Parameters.AddWithValue("@TestName", testName);
                        command.Parameters.AddWithValue("@PatientName", patientName);

                        await command.ExecuteNonQueryAsync();
                    }
                }

                ShowToastNotification("✅ บันทึกข้อมูลสำเร็จ!", 2000);
            }
            catch (Exception ex)
            {
                WriteList($"เกิดข้อผิดพลาดในการบันทึกข้อมูล: {ex.Message}");
            }
        }

        private async void timerReadFile_Tick(object sender, EventArgs e)
        {
            if (isProcessing || !isRunning) return;

            isProcessing = true;
            try
            {
                await LoadFileAsync();
            }
            finally
            {
                isProcessing = false;
            }
        }

        /// Folderfile
        private void btnSetting_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    folderresultfile = folderDialog.SelectedPath;
                    if (inFile == null)
                    {
                        string filename = Path.Combine(Application.StartupPath, "uti.txt");
                        inFile = new Rax.Utility.IniFile(filename);
                    }

                    inFile.IniWriteValue("Mycom", "Folderresultfile", folderresultfile);
                    txtFoderFileResult.Text = folderresultfile;
                    MessageBox.Show("บันทึก Path สำเร็จ!", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private async void ShowToastNotification(string message, int duration)
        {
            try
            {
                // 🟢 สร้างและแสดง Toast Notification
                using (Form toastForm = new Form())
                {
                    toastForm.StartPosition = FormStartPosition.Manual;
                    toastForm.FormBorderStyle = FormBorderStyle.None;
                    toastForm.BackColor = Color.Black;
                    toastForm.ForeColor = Color.White;
                    toastForm.Size = new Size(300, 50);
                    toastForm.TopMost = true;

                    Label lblMessage = new Label()
                    {
                        Text = message,
                        ForeColor = Color.White,
                        AutoSize = false,
                        Dock = DockStyle.Fill,
                        Font = new Font("Arial", 10, FontStyle.Bold),
                        TextAlign = ContentAlignment.MiddleCenter
                    };

                    toastForm.Controls.Add(lblMessage);

                    // 🔹 ตำแหน่งของ Toast Notification (มุมล่างขวา)
                    int screenX = Screen.PrimaryScreen.WorkingArea.Width - toastForm.Width - 10;
                    int screenY = Screen.PrimaryScreen.WorkingArea.Height - toastForm.Height - 10;
                    toastForm.Location = new Point(screenX, screenY);

                    toastForm.Show();

                    await Task.Delay(duration); // 🟢 แสดงผลตามเวลาที่กำหนด
                    toastForm.Close();
                }
            }
            catch (Exception ex)
            {
                WriteList($"Error in ShowToastNotification: {ex.Message}");
            }
        }

        /// Btn Start
        private async void btnStart_Click(object sender, EventArgs e)
        {
            if (!isRunning)
            {
                isRunning = true;
                btnStart.Text = "Stop";
                timerStatusLabel?.Start();

                cancellationTokenSource = new CancellationTokenSource();
                timerReadFile.Start();
                await LoadFileAsync();
            }
            else
            {
                cancellationTokenSource.Cancel();
                timerReadFile.Stop();
                isRunning = false;
                btnStart.Text = "Start";

                timerStatusLabel?.Stop();
                label2.Text = "⛔ หยุดการทำงานชั่วคราว";
                ShowToastNotification("⛔ หยุดการทำงานแล้ว!", 2000);
            }
        }
        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
        private void WriteLog(string message)
        {
            try
            {
                string logFolder = Application.StartupPath + "\\Logs\\";
                if (!Directory.Exists(logFolder))
                {
                    Directory.CreateDirectory(logFolder);
                }

                string pathFile = Path.Combine(logFolder, DateTime.Now.ToString("yyyy-MM-dd") + ".log");

                using (StreamWriter st = new StreamWriter(pathFile, true))
                {
                    st.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " " + message);
                }

                // 🔹 ลบไฟล์ Log ที่เก่ากว่า 30 วัน
                string oldLogFile = Path.Combine(logFolder, DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd") + ".log");
                if (File.Exists(oldLogFile))
                {
                    try
                    {
                        File.Delete(oldLogFile);
                    }
                    catch (Exception delEx)
                    {
                        ShowToastNotification("⚠️ ไม่สามารถลบไฟล์ Log เก่าได้: " + delEx.Message, 3000);
                    }
                }
            }
            catch (Exception ex)
            {
                string errorMessage = "Write log error: [" + ex.Message + "]";

                ShowToastNotification(errorMessage, 3000);
            }
        }
        private void WriteList(string message)
        {
            if (listBox1.Items.Count >= 1000)
            {
                listBox1.Items.RemoveAt(listBox1.Items.Count - 1);
            }
            listBox1.Items.Insert(0, "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] -> " + message);

            WriteLog(message);
        }

        /// BackUp Btn
        private void button2_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    bakPath = folderDialog.SelectedPath;
                    if (inFile == null)
                    {
                        string filename = Path.Combine(Application.StartupPath, "uti.txt");
                        inFile = new Rax.Utility.IniFile(filename);
                    }
                    inFile.IniWriteValue("Mycom", "BackupPath", bakPath);
                    textBox1.Text = bakPath;
                    MessageBox.Show("บันทึก Path สำเร็จ!", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private List<string> statusMessages = new List<string> { "📁 กำลังอ่าน...", "🔄 รอไฟล์ใหม่...", "⏳ Monitoring..." };
        private int currentStatusIndex = 0;
        private void timerStatusLabel_Tick(object sender, EventArgs e)
        {
            label2.Text = statusMessages[currentStatusIndex];
            currentStatusIndex = (currentStatusIndex + 1) % statusMessages.Count;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                inFile.IniWriteValue("Mycom", "IsAutoStart", checkBox1.Checked ? "1" : "0");
                WriteList("บันทึกค่า Auto Start เรียบร้อยแล้ว");
            }
            catch (Exception ex)
            {
                WriteList($"ไม่สามารถบันทึกค่า AutoStart ได้: {ex.Message}");
            }
        }
    }
}
