# ASPMonitor Interface (WinApp)

**ASPMonitor Interface** is a Windows Forms application built with C#. It is designed to monitor, parse, and process laboratory result files (`.RES`). The application automatically reads data from `.RES` files, parses patient information and test results, and inserts the data into a SQL Server database. It also organizes backups of processed files and provides a live log for monitoring data processing activities.

---

## ✨ Features

- 📄 Parse `.RES` laboratory result files
- 🔍 Extract patient data, barcode, section, and test results
- 🔄 Insert parsed data into SQL Server automatically
- ♻️ Auto-backup processed files into folders organized by **Year/Month/Day**
- 🕒 Display live log of processed files and operations
- ⚙️ Support both **Manual Load** and **Auto Monitor Mode**
- 🔗 Connect seamlessly with the **ASPMonitor Dashboard** for data visualization

---

## 🛠️ Tech Stack

- **Language:** C# (.NET Framework, WinForms)
- **Database:** SQL Server
- **IDE:** Visual Studio 2022
- **File Type Processed:** `.RES` (Lab Test Result Files)

---

## 📦 Database Requirements

- This application shares the same database with the **ASPMonitor Dashboard**.
- Use the SQL script located in `/Database/aspdashboard.sql` to set up the database.

### Required Tables:

- `DashBoardRooms`
- `SectionRoom`
- `PatienRoom`
- `UserAuthorize`

---

### 🔗 Database Connection

- The database connection string is stored in the file `uti.txt`.

### Example content for `uti.txt`:

ConnectionString =Data Source=.\sqlexpress; Initial Catalog=YOUR_DATABASE_NAME; User ID=YOUR_USERNAME; Password=YOUR_PASSWORD;

---

### 🔥 Important:

- The file `uti.txt` is ignored from Git (`.gitignore`) for security.
- A sample file `uti.example.txt` is provided — copy and rename it to `uti.txt` and adjust the connection string to match your environment.

---

### 🔧 Run from Visual Studio:

1. Open `ASPMonitorInterface.sln`.
2. Click **Start (F5)** to run.

### 💾 Run as Executable:

1. Publish the application (`Right-click Project → Publish`).
2. Run `ASPMonitorInterface.exe` from the publish output folder.

---

## 📂 Folder Structure

- **Input Folder:** Folder where `.RES` files are located.
- **Backup Folder:** Processed files are backed up automatically by **Year/Month/Day**.
- **Logs Folder:** Activity logs are saved here (if applicable).

---

## 📜 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 👤 Author

Developed by **Nawee Pukpak (quiiznon1234)**  
📧 Contact: navypukpak@gmail.com