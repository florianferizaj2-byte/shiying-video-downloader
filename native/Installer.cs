using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ShiyingSetup {
    public static class Installation {
        public const string ProductId = "shiying-7c9b978c-495e-41a0-9c56-44f8ace39e20";
        public const string RegistryPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Shiying";
        public static string Quote(string value) { return "\"" + value.Replace("\"", "") + "\""; }
        public static string ValidateTarget(string directory) {
            string target = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
            if (target.Length < 8 || target == Path.GetPathRoot(target).TrimEnd(Path.DirectorySeparatorChar)) throw new Exception("请选择专用的应用文件夹。");
            string marker = Path.Combine(target, "installation.id");
            if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any() && (!File.Exists(marker) || File.ReadAllText(marker) != ProductId))
                throw new Exception("该文件夹已有其他文件，请选择空文件夹，或已有的拾影安装目录。");
            foreach (var process in Process.GetProcessesByName("Shiying")) {
                try { if (string.Equals(process.MainModule.FileName, Path.Combine(target, "Shiying.exe"), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("请先关闭已运行的拾影，再进行安装。"); }
                catch (System.ComponentModel.Win32Exception) { }
                finally { process.Dispose(); }
            }
            return target;
        }
        public static string[] Extract(Stream payload, string directory, Action<int> progress) {
            string target = ValidateTarget(directory);
            using (var zip = new ZipArchive(payload, ZipArchiveMode.Read)) {
                var files = zip.Entries.Where(e => e.Name != "").ToArray();
                if (!files.Any(e => e.FullName == "Shiying.exe")) throw new Exception("安装包不完整。");
                foreach (var entry in files) {
                    string destination = Path.GetFullPath(Path.Combine(target, entry.FullName));
                    if (!destination.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new Exception("安装包中包含无效路径。");
                }
                Directory.CreateDirectory(target);
                var installed = new List<string>();
                foreach (var entry in files) {
                    string destination = Path.GetFullPath(Path.Combine(target, entry.FullName));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    using (var input = entry.Open()) using (var output = File.Create(destination)) input.CopyTo(output);
                    installed.Add(entry.FullName); if (progress != null) progress(installed.Count * 100 / files.Length);
                }
                File.WriteAllText(Path.Combine(target, "installation.id"), ProductId);
                File.WriteAllLines(Path.Combine(target, "installed-files.txt"), installed, Encoding.UTF8);
                return installed.ToArray();
            }
        }
        public static void Shortcut(string file, string target, bool remove) {
            Type type = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(type);
            object link = type.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { file });
            try {
                var linkType = link.GetType();
                if (remove) {
                    string existing = (string)linkType.InvokeMember("TargetPath", BindingFlags.GetProperty, null, link, null);
                    if (string.Equals(existing, target, StringComparison.OrdinalIgnoreCase) && File.Exists(file)) File.Delete(file);
                } else {
                    linkType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { target });
                    linkType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { Path.GetDirectoryName(target) });
                    linkType.InvokeMember("Description", BindingFlags.SetProperty, null, link, new object[] { "拾影 · 视频下载工具" });
                    linkType.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
                }
            } finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link); System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
        }
        public static void Register(string target, bool desktop) {
            string executable = Path.Combine(target, "Shiying.exe");
            Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "拾影.lnk"), executable, false);
            if (desktop) Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "拾影.lnk"), executable, false);
            using (var key = Registry.CurrentUser.CreateSubKey(RegistryPath)) {
                key.SetValue("DisplayName", "拾影"); key.SetValue("DisplayVersion", "0.1.0"); key.SetValue("Publisher", "Shiying");
                key.SetValue("InstallLocation", target); key.SetValue("DisplayIcon", executable);
                key.SetValue("UninstallString", Quote(Path.Combine(target, "Uninstall.exe")));
                key.SetValue("NoModify", 1, RegistryValueKind.DWord); key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("EstimatedSize", (int)(Directory.GetFiles(target, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length) / 1024), RegistryValueKind.DWord);
            }
        }
        public static void RemoveFiles(string directory) {
            string target = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
            string marker = Path.Combine(target, "installation.id"), manifest = Path.Combine(target, "installed-files.txt");
            if (!File.Exists(marker) || File.ReadAllText(marker) != ProductId || !File.Exists(manifest)) throw new Exception("未找到有效的拾影安装信息，未删除任何文件。");
            var files = File.ReadAllLines(manifest).Concat(new[] { "installed-files.txt", "installation.id" }).ToArray();
            // Validate every path before deleting any file; user downloads and AppData are never part of this manifest.
            foreach (string relative in files) {
                if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || !Path.GetFullPath(Path.Combine(target, relative)).StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("卸载清单无效，未删除任何文件。");
            }
            foreach (string relative in files) { string file = Path.Combine(target, relative); if (File.Exists(file)) File.Delete(file); }
            foreach (string dir in Directory.GetDirectories(target, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length)) {
                if (!Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
            }
            if (!Directory.EnumerateFileSystemEntries(target).Any()) Directory.Delete(target);
        }
    }
    static class Program {
        [STAThread] static void Main(string[] args) {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            try {
#if UNINSTALL
                string current = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
                if (args.Length == 2 && args[0] == "--remove") {
                    string target = Path.GetFullPath(args[1]).TrimEnd(Path.DirectorySeparatorChar);
                    Installation.RemoveFiles(target);
                    string executable = Path.Combine(target, "Shiying.exe");
                    Installation.Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "拾影.lnk"), executable, true);
                    Installation.Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "拾影.lnk"), executable, true);
                    using (var key = Registry.CurrentUser.OpenSubKey(Installation.RegistryPath)) {
                        if (key != null && string.Equals((string)key.GetValue("InstallLocation"), target, StringComparison.OrdinalIgnoreCase)) { key.Close(); Registry.CurrentUser.DeleteSubKey(Installation.RegistryPath, false); }
                    }
                    MessageBox.Show("拾影已卸载。下载的视频和用户设置已保留。", "拾影");
                } else if (MessageBox.Show("确定卸载拾影？下载的视频和用户设置会保留。\n请先关闭拾影窗口。", "卸载拾影", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) {
                    string helper = Path.Combine(Path.GetTempPath(), "Shiying-Uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
                    File.Copy(Application.ExecutablePath, helper);
                    Process.Start(new ProcessStartInfo(helper, "--remove " + Installation.Quote(current)) { UseShellExecute = true });
                }
#else
                Application.Run(new SetupForm());
#endif
            } catch (Exception ex) { MessageBox.Show(ex.Message, "拾影", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }
    public class SetupForm : Form {
        TextBox folder = new TextBox(); Button browse = new Button(), install = new Button();
        CheckBox desktop = new CheckBox(), launch = new CheckBox(); ProgressBar progress = new ProgressBar(); Label status = new Label();
        bool working, installed;
        public SetupForm() {
            Text = "安装拾影"; ClientSize = new Size(545, 385); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen; Font = new Font("Microsoft YaHei UI", 9F); BackColor = Color.White;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Controls.Add(new Label { Text = "拾影", Font = new Font(Font.FontFamily, 25F, FontStyle.Bold), ForeColor = Color.FromArgb(33, 85, 205), AutoSize = true, Location = new Point(28, 24) });
            Controls.Add(new Label { Text = "粘贴链接，把喜欢的视频保存到电脑。", AutoSize = true, Location = new Point(31, 86), ForeColor = Color.FromArgb(86, 100, 122) });
            Controls.Add(new Label { Text = "安装位置", AutoSize = true, Location = new Point(31, 130) });
            folder.SetBounds(32, 156, 382, 26); folder.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Shiying"); Controls.Add(folder);
            browse.SetBounds(425, 154, 87, 28); browse.Text = "浏览…"; Controls.Add(browse);
            browse.Click += (s, e) => { using (var dialog = new FolderBrowserDialog { Description = "选择空文件夹或已有拾影安装目录" }) if (dialog.ShowDialog(this) == DialogResult.OK) folder.Text = dialog.SelectedPath; };
            desktop.SetBounds(32, 201, 210, 26); desktop.Text = "创建桌面快捷方式"; desktop.Checked = true; Controls.Add(desktop);
            launch.SetBounds(270, 201, 240, 26); launch.Text = "安装完成后打开拾影"; launch.Checked = true; Controls.Add(launch);
            status.SetBounds(32, 248, 480, 38); status.Text = "仅为当前用户安装，无需管理员权限。\nWindows 10 / 11 64 位，需要系统 WebView2 组件。"; status.ForeColor = Color.FromArgb(86, 100, 122); Controls.Add(status);
            progress.SetBounds(32, 295, 480, 5); Controls.Add(progress);
            install.SetBounds(382, 322, 130, 38); install.Text = "开始安装"; install.BackColor = Color.FromArgb(33, 85, 205); install.ForeColor = Color.White; install.FlatStyle = FlatStyle.Flat; Controls.Add(install);
            install.Click += async (s, e) => await Install();
            FormClosing += (s, e) => { if (working) e.Cancel = true; };
        }
        async Task Install() {
            if (installed) { if (launch.Checked) Process.Start(Path.Combine(folder.Text, "Shiying.exe")); Close(); return; }
            working = true; install.Enabled = false; browse.Enabled = false; folder.ReadOnly = true; desktop.Enabled = false;
            try {
                string target = Installation.ValidateTarget(folder.Text); folder.Text = target; status.Text = "正在安装应用…";
                await Task.Run(() => {
                    using (var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload")) Installation.Extract(payload, target, value => BeginInvoke(new Action(() => progress.Value = value)));
                });
                Installation.Register(target, desktop.Checked);
                installed = true; status.Text = "安装完成。你可以从桌面或开始菜单打开拾影。"; install.Text = "完成";
            } catch (Exception ex) { status.Text = "安装没有完成，请检查安装位置后重试。"; MessageBox.Show(ex.Message, "安装拾影"); browse.Enabled = true; folder.ReadOnly = false; desktop.Enabled = true; }
            finally { working = false; install.Enabled = true; }
        }
    }
}
