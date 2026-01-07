# Installation Guide

Complete installation guide for BIM-DXPlatform.

## System Requirements

### Minimum Requirements

| Component | Requirement |
|-----------|-------------|
| **Operating System** | Windows 10 (64-bit) or later |
| **Revit** | Autodesk Revit 2025 |
| **.NET Runtime** | .NET 8.0 (included with Revit 2025) |
| **RAM** | 8 GB minimum, 16 GB recommended |
| **Disk Space** | 200 MB for installation |
| **Permissions** | Administrator rights for initial installation |

### Supported Revit Versions

- ✅ Revit 2025 (Primary support)
- 🚧 Revit 2024 (Planned for future release)
- ❌ Revit 2023 and earlier (Not supported)

---

## Installation Methods

### Method 1: Download Installer (Recommended)

**For most users**

1. **Download the Latest Release**
   - Visit [Releases Page](https://github.com/tygwan/BIM-DXPlatform/releases)
   - Download `DXrevit-Setup-v0.1.0.zip`

2. **Extract the ZIP File**
   - Right-click the downloaded file
   - Select "Extract All..."
   - Choose a temporary location

3. **Run the Installer**
   - Right-click `install.bat`
   - Select "Run as Administrator"
   - Follow the on-screen prompts

4. **Restart Revit**
   - Close all Revit instances
   - Launch Revit 2025
   - Look for the "DX Platform" tab in the ribbon

### Method 2: Manual Installation

**For advanced users or troubleshooting**

1. **Download the Release**
   - Download `DXrevit-Manual-v0.1.0.zip`

2. **Create Installation Directory**
   ```
   C:\ProgramData\Autodesk\Revit\Addins\2025\DXrevit\
   ```

3. **Copy Files**
   - Extract ZIP contents
   - Copy all DLL files to the DXrevit folder:
     - DXrevit.dll
     - DXBase.dll
     - System.Text.Json.dll
     - System.Text.Encodings.Web.dll
     - System.IO.Pipelines.dll

4. **Install Manifest File**
   - Copy `DXrevit.addin` to:
     ```
     C:\ProgramData\Autodesk\Revit\Addins\2025\
     ```

5. **Verify Installation**
   - Open `DXrevit.addin` in Notepad
   - Confirm the Assembly path:
     ```xml
     <Assembly>C:\ProgramData\Autodesk\Revit\Addins\2025\DXrevit\DXrevit.dll</Assembly>
     ```

6. **Restart Revit**

### Method 3: Build from Source

**For developers**

See [Development Setup Guide](../dev/setup-guide.md) for detailed instructions.

---

## Verification

### Check Installation

1. **Open Revit 2025**

2. **Look for DX Platform Tab**
   - You should see a new tab labeled "DX Platform" in the ribbon
   - If not visible, see [Troubleshooting](#troubleshooting) below

3. **Verify Buttons**
   - The "데이터 관리" (Data Management) panel should have 3 buttons:
     - 스냅샷 저장 (Snapshot Capture)
     - 매개변수 설정 (Parameter Setup)
     - 설정 (Settings)

4. **Check Logs**
   - Navigate to:
     ```
     C:\Users\{YourUsername}\AppData\Roaming\DX_Platform\Logs\
     ```
   - You should see a log file like `DX_20251013.log`
   - Open it and confirm successful startup:
     ```
     [2025-10-13 20:00:00] [INFO] [DXrevit] DXrevit 애드인 시작
     [2025-10-13 20:00:01] [INFO] [DXrevit] DXrevit 리본 UI 생성 완료
     ```

---

## Configuration

### First-Time Setup

1. **Click "Settings" Button**
   - In the DX Platform ribbon tab

2. **Configure API Server** (Optional)
   - API Server URL: `https://your-server.com` (leave default for now)
   - Timeout: `30` seconds
   - Batch Size: `100` elements

3. **Configure Logging** (Optional)
   - Log Level: `INFO` (default)
   - Log Retention: `30` days
   - Log Path: `C:\Users\{Username}\AppData\Roaming\DX_Platform\Logs`

4. **Save Settings**
   - Click "Save" to persist configuration
   - Settings are stored in:
     ```
     C:\Users\{Username}\AppData\Roaming\DX_Platform\settings.json
     ```

---

## Troubleshooting

### Issue: "DX Platform" tab is not visible

**Possible Causes:**

1. **Installation path is incorrect**
   - Verify files exist in:
     ```
     C:\ProgramData\Autodesk\Revit\Addins\2025\DXrevit\
     ```

2. **.addin file is missing or incorrect**
   - Check if `DXrevit.addin` exists in:
     ```
     C:\ProgramData\Autodesk\Revit\Addins\2025\
     ```
   - Open the file and verify `<Assembly>` path

3. **Revit didn't load the add-in**
   - Check Revit's journal file:
     ```
     C:\Users\{Username}\AppData\Local\Autodesk\Revit\Autodesk Revit 2025\Journals\
     ```
   - Look for DXrevit errors

4. **Missing DLL dependencies**
   - Ensure `System.Text.Json.dll` is present in the DXrevit folder
   - Re-run the installer or copy manually

**Solution Steps:**

1. Close all Revit instances
2. Verify all files are in correct locations
3. Run Command Prompt as Administrator:
   ```cmd
   taskkill /F /IM Revit.exe
   ```
4. Restart Revit
5. If still not working, see [Advanced Troubleshooting](#advanced-troubleshooting)

---

### Issue: Add-in loads but buttons don't work

**Possible Causes:**

1. **Configuration file is corrupted**
   - Delete:
     ```
     C:\Users\{Username}\AppData\Roaming\DX_Platform\settings.json
     ```
   - Restart Revit (settings will be recreated)

2. **Insufficient permissions**
   - Run Revit as Administrator (once)
   - This allows DXrevit to create necessary folders

3. **API server is not configured**
   - This is normal if you haven't set up DXserver yet
   - The add-in will still work, but data won't be sent anywhere

**Solution:**
- Check logs for specific errors
- Report issues on GitHub with log files

---

### Issue: Error message on startup

**Example Error:**
```
Could not load file or assembly 'System.Text.Json, Version=9.0.0.0'
```

**Solution:**

1. **Verify System.Text.Json.dll is present**
   ```
   C:\ProgramData\Autodesk\Revit\Addins\2025\DXrevit\System.Text.Json.dll
   ```

2. **Check file version**
   - Right-click DLL → Properties → Details
   - Version should be 8.0.5 or compatible

3. **Re-download and install**
   - Use the official installer
   - Or manually copy all DLL files

---

## Advanced Troubleshooting

### Enable Debug Logging

1. **Locate debug log file:**
   ```
   C:\Users\{Username}\Desktop\AWP_2025\개발폴더\Errorlog\DXrevit_startup.log
   ```

2. **Check for errors:**
   ```
   ❌ 예외 발생: FileNotFoundException
      메시지: Could not load file or assembly...
   ```

3. **Report the issue:**
   - Include the log file contents
   - Describe steps to reproduce
   - [Submit issue on GitHub](https://github.com/tygwan/BIM-DXPlatform/issues)

### Clean Reinstall

If all else fails:

1. **Uninstall completely:**
   ```cmd
   rmdir /S /Q "C:\ProgramData\Autodesk\Revit\Addins\2025\DXrevit"
   del "C:\ProgramData\Autodesk\Revit\Addins\2025\DXrevit.addin"
   rmdir /S /Q "C:\Users\{Username}\AppData\Roaming\DX_Platform"
   ```

2. **Restart computer**

3. **Reinstall using Method 1**

---

## Uninstallation

### Complete Removal

1. **Run uninstaller (if available):**
   ```
   C:\ProgramData\Autodesk\Revit\Addins\2025\DXrevit\uninstall.bat
   ```

2. **Or manually remove:**
   ```cmd
   # Remove add-in files
   rmdir /S /Q "C:\ProgramData\Autodesk\Revit\Addins\2025\DXrevit"
   del "C:\ProgramData\Autodesk\Revit\Addins\2025\DXrevit.addin"

   # Remove user data (optional)
   rmdir /S /Q "C:\Users\{Username}\AppData\Roaming\DX_Platform"
   ```

3. **Restart Revit**

---

## Next Steps

After successful installation:

1. **Read the [User Guide](user-guide.md)** to learn how to use DXrevit
2. **Try the [Quick Start Tutorial](tutorials.md)** for hands-on experience
3. **Check the [FAQ](faq.md)** for common questions

---

## Getting Help

- 🐛 [Report Installation Issues](https://github.com/tygwan/BIM-DXPlatform/issues/new?labels=installation)
- 💬 [Ask Questions](https://github.com/tygwan/BIM-DXPlatform/discussions)
- 📧 Email Support: tygwan@users.noreply.github.com
