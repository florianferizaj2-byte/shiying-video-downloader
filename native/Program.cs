using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

[assembly: System.Reflection.AssemblyTitle("拾影")]
[assembly: System.Reflection.AssemblyDescription("轻量视频下载工具")]
[assembly: System.Reflection.AssemblyVersion("0.1.0.0")]

namespace Shiying {
    static class Program {
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [STAThread] static void Main(string[] args) {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            SetProcessDPIAware(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            string root = AppDomain.CurrentDomain.BaseDirectory;
            string data = File.Exists(Path.Combine(root, "portable.flag")) ? Path.Combine(root, "Data") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shiying");
            bool isNew;
            string key = "Local\\Shiying-" + data.ToLowerInvariant().GetHashCode().ToString("X");
            using (var mutex = new Mutex(true, key, out isNew)) {
                if (!isNew) { MessageBox.Show("拾影已在运行，请切换到已打开的窗口。", "拾影"); return; }
                try { Application.Run(new MainForm(root, data)); }
                catch (Exception ex) { MessageBox.Show("应用未能启动：" + ex.Message, "拾影", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }
    }
    public class MainForm : Form {
        private readonly WebView2 view = new WebView2();
        private readonly Engine engine;
        private readonly string root, data;
        public MainForm(string appRoot, string dataRoot) {
            root = appRoot; data = dataRoot; engine = new Engine(root, data);
            Text = "拾影 · 视频下载工具"; ClientSize = new Size(1100, 790); MinimumSize = new Size(750, 640);
            StartPosition = FormStartPosition.CenterScreen; BackColor = Color.FromArgb(245, 247, 250);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            view.Dock = DockStyle.Fill; view.DefaultBackgroundColor = BackColor; Controls.Add(view);
            engine.Progress += progress => { if (!IsDisposed && IsHandleCreated) BeginInvoke(new Action(() => Post(new { type = "progress", data = progress }))); };
            Shown += async (s, e) => await Initialize();
            FormClosing += (s, e) => {
                if (engine.Busy && MessageBox.Show("当前任务仍在进行，关闭应用会取消任务。确定关闭？", "拾影", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) e.Cancel = true;
                else engine.Cancel();
            };
        }
        private async Task Initialize() {
            try {
                var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(data, "WebView2"));
                await view.EnsureCoreWebView2Async(environment);
                view.CoreWebView2.SetVirtualHostNameToFolderMapping("app.shiying.local", Path.Combine(root, "ui"), CoreWebView2HostResourceAccessKind.DenyCors);
                view.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                view.CoreWebView2.Settings.AreDevToolsEnabled = false;
                view.CoreWebView2.Settings.IsStatusBarEnabled = false;
                view.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                view.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
                view.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
                view.CoreWebView2.NavigationStarting += (s, e) => { if (!Trusted(e.Uri)) e.Cancel = true; };
                view.CoreWebView2.NewWindowRequested += (s, e) => { e.Handled = true; };
                view.CoreWebView2.PermissionRequested += (s, e) => { e.State = CoreWebView2PermissionState.Deny; };
                view.CoreWebView2.DownloadStarting += (s, e) => { e.Cancel = true; };
                view.CoreWebView2.WebMessageReceived += OnMessage;
                view.CoreWebView2.Navigate("https://app.shiying.local/index.html");
            } catch (WebView2RuntimeNotFoundException) {
                if (MessageBox.Show("这台电脑尚未安装 Microsoft Edge WebView2。拾影使用该系统组件显示界面。\n\n是否打开微软官方下载页？", "需要 WebView2", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    Process.Start("https://developer.microsoft.com/microsoft-edge/webview2/#download");
                Close();
            } catch (Exception ex) { MessageBox.Show("界面加载失败：" + ex.Message, "拾影"); Close(); }
        }
        private static bool Trusted(string source) { Uri uri; return Uri.TryCreate(source, UriKind.Absolute, out uri) && uri.Scheme == "https" && uri.Host == "app.shiying.local"; }
        private void Post(object value) { if (!IsDisposed && view.CoreWebView2 != null) view.CoreWebView2.PostWebMessageAsJson(Core.Json(value)); }
        private async void OnMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e) {
            if (!Trusted(e.Source)) return;
            string id = "";
            try {
                string text = e.WebMessageAsJson; if (text.Length > 20000) throw new Exception("输入内容过长。");
                var message = Core.Parse(text); id = Core.Text(message, "id");
                var method = Core.Text(message, "method");
                var args = message.ContainsKey("args") ? message["args"] as Dictionary<string, object> : new Dictionary<string, object>();
                object result;
                switch (method) {
                    case "state": result = engine.State(); break;
                    case "clipboard": result = Clipboard.ContainsText() ? Clipboard.GetText() : ""; break;
                    case "analyze": result = await engine.Analyze(Core.Text(args, "input")); break;
                    case "download": result = await engine.Download(Core.Text(args, "id"), Core.Text(args, "quality")); break;
                    case "cancel": engine.Cancel(); result = true; break;
                    case "folder":
                        engine.CheckIdle();
                        using (var dialog = new FolderBrowserDialog { Description = "选择视频保存文件夹", SelectedPath = engine.settings.folder, ShowNewFolderButton = true }) {
                            if (dialog.ShowDialog(this) == DialogResult.OK) { engine.settings.folder = dialog.SelectedPath; engine.SaveSettings(); }
                        }
                        result = engine.State(); break;
                    case "cookies":
                        engine.CheckIdle();
                        using (var dialog = new OpenFileDialog { Title = "选择 Netscape 格式的登录信息文件", Filter = "登录信息文件 (*.txt)|*.txt", CheckFileExists = true }) {
                            if (dialog.ShowDialog(this) == DialogResult.OK) {
                                if (new FileInfo(dialog.FileName).Length > 10 * 1024 * 1024) throw new Exception("文件过大，请选择浏览器导出的 cookies.txt 文件。");
                                string header; using (var reader = File.OpenText(dialog.FileName)) header = reader.ReadLine();
                                if (header == null || !header.Contains("HTTP Cookie File")) throw new Exception("请选择 Netscape 格式的 cookies.txt 文件。");
                                engine.settings.cookieFile = dialog.FileName; engine.SaveSettings();
                            }
                        }
                        result = engine.State(); break;
                    case "clearCookies": engine.CheckIdle(); engine.settings.cookieFile = ""; engine.SaveSettings(); result = engine.State(); break;
                    case "settings": engine.CheckIdle(); engine.settings.proxy = Core.Proxy(Core.Text(args, "proxy")); engine.SaveSettings(); result = engine.State(); break;
                    case "component": result = await engine.InstallComponent(Core.Text(args, "name")); break;
                    case "openFolder":
                        Directory.CreateDirectory(engine.settings.folder);
                        Process.Start(new ProcessStartInfo("explorer.exe", Core.Quote(engine.settings.folder)) { UseShellExecute = true }); result = true; break;
                    case "reveal":
                        var entry = engine.history.FirstOrDefault(item => item.id == Core.Text(args, "id"));
                        if (entry == null || !File.Exists(entry.filePath)) throw new Exception("文件已被移动或删除，请查看保存文件夹。");
                        Process.Start(new ProcessStartInfo("explorer.exe", "/select," + Core.Quote(entry.filePath)) { UseShellExecute = true }); result = true; break;
                    default: throw new Exception("无法识别这个操作。");
                }
                Post(new { type = "response", id = id, ok = true, data = result });
            } catch (OperationCanceledException) { Post(new { type = "response", id = id, ok = false, cancelled = true }); }
            catch (Exception ex) {
                var failure = ex as EngineException;
                string details = failure == null ? "" : Regex.Replace(failure.Details, @"https?://[^\s]+", "[链接]");
                if (details.Length > 4000) details = details.Substring(details.Length - 4000);
                Post(new { type = "response", id = id, ok = false, error = ex.Message, details = details });
            }
        }
    }
}
