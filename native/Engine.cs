using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Shiying {
    public class Engine {
        public readonly string DataDirectory;
        public Settings settings;
        public List<HistoryEntry> history = new List<HistoryEntry>();
        public Video video;
        public bool Busy { get { return cancellation != null; } }
        public event Action<object> Progress;
        private CancellationTokenSource cancellation;
        private Process process;
        private Dictionary<string, object> lastInfo;
        private readonly string appDirectory;
        public Engine(string appDir, string dataDir) {
            appDirectory = appDir; DataDirectory = dataDir; Directory.CreateDirectory(DataDirectory);
            try { settings = Core.Serializer().Deserialize<Settings>(File.ReadAllText(Path.Combine(DataDirectory, "settings.json"))); } catch { settings = new Settings(); }
            if (settings == null) settings = new Settings();
            try { history = Core.Serializer().Deserialize<List<HistoryEntry>>(File.ReadAllText(Path.Combine(DataDirectory, "history.json"))) ?? new List<HistoryEntry>(); } catch { }
        }
        public string Binary(string name) {
            string cached = Path.Combine(DataDirectory, "bin", name + ".exe");
            return File.Exists(cached) ? cached : Path.Combine(appDirectory, "bin", name + ".exe");
        }
        public bool Has(string name) { return File.Exists(Binary(name)); }
        public object State() { return new { settings = settings, history = history, components = new { parser = Has("yt-dlp"), merger = Has("ffmpeg"), youtube = Has("qjs") }, portable = File.Exists(Path.Combine(appDirectory, "portable.flag")), version = "0.1.0" }; }
        public void SaveSettings() { Core.WriteAtomic(Path.Combine(DataDirectory, "settings.json"), Core.Json(settings)); }
        public void CheckIdle() { if (Busy) throw new Exception("请等待当前任务结束，或先取消当前任务。"); }
        private CancellationToken Start() { CheckIdle(); cancellation = new CancellationTokenSource(); return cancellation.Token; }
        private void Finish() { process = null; cancellation.Dispose(); cancellation = null; }
        public void Cancel() { if (cancellation != null) { cancellation.Cancel(); KillProcess(); } }
        private void KillProcess() {
            var child = process;
            if (child == null) return;
            try {
                if (!child.HasExited) {
                    using (var kill = Process.Start(new ProcessStartInfo("taskkill.exe", "/PID " + child.Id + " /T /F") { CreateNoWindow = true, UseShellExecute = false })) { }
                }
            } catch { try { child.Kill(); } catch { } }
        }
        private void Report(object value) { var handler = Progress; if (handler != null) handler(value); }
        public async Task<Video> Analyze(string input) {
            string url = Core.ExtractUrl(input);
            var token = Start(); video = null; lastInfo = null;
            try {
                if (!Has("yt-dlp")) throw new Exception("解析组件缺失，请重新解压完整的便携包，或重新安装应用。");
                if (Core.Platform(url) == "YouTube" && !Has("qjs")) await EnsureComponent("qjs", token);
                Report(new { stage = "analyzing", message = "正在读取视频信息…" });
                if (Core.Platform(url) == "快手") lastInfo = await Kuaishou.Resolve(url, settings, token);
                else {
                    string json = await Run(new[] { "--dump-single-json", "--skip-download", "--", url }, null, 120000, token);
                    lastInfo = Core.Parse(json);
                }
                token.ThrowIfCancellationRequested();
                video = Core.Normalize(lastInfo, url, Has("ffmpeg"));
                return video;
            } finally { Finish(); }
        }
        public async Task<HistoryEntry> Download(string id, string quality) {
            if (video == null || video.id != id) throw new Exception("请先解析视频链接。");
            if (!video.qualities.Any(q => q.value == quality)) throw new Exception("请选择有效的清晰度。");
            if (video.needsMerger) throw new Exception("该视频的声音与画面分开存储。请在「连接设置」安装高清合并组件，再开始下载。");
            var token = Start(); var current = video;
            string infoFile = null;
            try {
                string folder = settings.folder;
                if (!Path.IsPathRooted(folder)) throw new Exception("请先选择有效的保存文件夹。");
                Directory.CreateDirectory(folder);
                string file = "";
                var args = new List<string> { "--no-simulate", "--no-overwrites", "--continue", "--windows-filenames", "--trim-filenames", "190",
                    "--format", Core.Format(quality, Has("ffmpeg")), "--format-sort", "res,ext:mp4:m4a", "--merge-output-format", "mp4/mkv",
                    "--paths", folder, "--output", "%(title).140B [%(id).40B].%(ext)s", "--progress", "--progress-delta", "0.3",
                    "--progress-template", "download:__SHI_PROGRESS__{\"status\":%(progress.status)j,\"downloaded\":%(progress.downloaded_bytes)j,\"total\":%(progress.total_bytes)j,\"estimate\":%(progress.total_bytes_estimate)j,\"speed\":%(progress.speed)j,\"eta\":%(progress.eta)j}",
                    "--print", "after_move:__SHI_FILE__%(filepath)j" };
                if (current.platform == "快手") {
                    infoFile = Path.Combine(DataDirectory, "video-" + Guid.NewGuid().ToString("N") + ".json");
                    Core.WriteAtomic(infoFile, Core.Json(lastInfo));
                    args.AddRange(new[] { "--load-info-json", infoFile });
                } else args.AddRange(new[] { "--", current.sourceUrl });
                await Run(args, line => {
                    if (line.StartsWith("__SHI_PROGRESS__")) {
                        try {
                            var p = Core.Parse(line.Substring(16));
                            double total = Core.Number(p, "total"); if (total == 0) total = Core.Number(p, "estimate");
                            double downloaded = Core.Number(p, "downloaded");
                            Report(new { stage = Core.Text(p, "status") == "finished" ? "processing" : "downloading", percent = total > 0 ? (double?)Math.Min(100, downloaded / total * 100) : null,
                                downloaded = downloaded, total = total, speed = Core.Number(p, "speed"), eta = Core.Number(p, "eta") });
                        } catch { }
                    }
                    if (line.StartsWith("__SHI_FILE__")) { try { file = Core.Serializer().Deserialize<string>(line.Substring(12)); } catch { } }
                }, 0, token);
                token.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(file) || !Core.Inside(file, folder) || !File.Exists(file) || new FileInfo(file).Length == 0) throw new Exception("下载结束，但未找到完整视频文件，请重试。");
                var entry = new HistoryEntry { id = Guid.NewGuid().ToString("N"), title = current.title, platform = current.platform, filePath = file, size = new FileInfo(file).Length, completedAt = DateTime.UtcNow.ToString("o") };
                history.Insert(0, entry); history = history.Take(20).ToList();
                try { Core.WriteAtomic(Path.Combine(DataDirectory, "history.json"), Core.Json(history)); } catch { Report(new { stage = "warning", message = "视频已保存，但下载记录未能写入。" }); }
                return entry;
            } finally { if (infoFile != null && File.Exists(infoFile)) File.Delete(infoFile); Finish(); }
        }
        private async Task<string> Run(IEnumerable<string> extra, Action<string> onLine, int timeout, CancellationToken token) {
            var args = new List<string> { "--ignore-config", "--no-playlist", "--no-color", "--newline", "--encoding", "utf-8", "--socket-timeout", "20", "--retries", "2", "--fragment-retries", "2", "--extractor-retries", "1" };
            if (Has("ffmpeg")) args.AddRange(new[] { "--ffmpeg-location", Binary("ffmpeg") });
            if (Has("qjs")) args.AddRange(new[] { "--no-js-runtimes", "--js-runtimes", "quickjs:" + Binary("qjs") });
            if (!string.IsNullOrWhiteSpace(settings.proxy)) args.AddRange(new[] { "--proxy", settings.proxy });
            string cookieCopy = null;
            if (!string.IsNullOrWhiteSpace(settings.cookieFile)) {
                if (!File.Exists(settings.cookieFile)) throw new Exception("登录信息文件已被移动或删除，请在连接设置中重新选择。");
                cookieCopy = Path.Combine(DataDirectory, "cookie-" + Guid.NewGuid().ToString("N") + ".tmp");
                File.Copy(settings.cookieFile, cookieCopy);
                args.AddRange(new[] { "--cookies", cookieCopy });
            }
            args.AddRange(extra);
            try {
                var start = new ProcessStartInfo(Binary("yt-dlp"), string.Join(" ", args.Select(Core.Quote))) {
                    CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
                };
                using (var child = new Process { StartInfo = start, EnableRaisingEvents = true }) {
                    var complete = new TaskCompletionSource<int>();
                    child.Exited += (s, e) => { try { complete.TrySetResult(child.ExitCode); } catch (Exception ex) { complete.TrySetException(ex); } };
                    token.ThrowIfCancellationRequested();
                    child.Start(); process = child;
                    var stderr = ReadLimited(child.StandardError, 12000, false);
                    var stdout = onLine == null ? ReadLimited(child.StandardOutput, 32 * 1024 * 1024, true) : ReadLines(child.StandardOutput, onLine);
                    using (token.Register(KillProcess)) {
                        if (timeout > 0 && await Task.WhenAny(complete.Task, Task.Delay(timeout, token)) != complete.Task) {
                            KillProcess(); await complete.Task;
                            token.ThrowIfCancellationRequested();
                            throw new Exception("连接超时，请检查网络，或在连接设置中填写代理。");
                        }
                        int code = await complete.Task;
                        var text = await stdout; var errors = await stderr;
                        token.ThrowIfCancellationRequested();
                        if (code != 0) throw new EngineException(Core.Friendly(errors), errors);
                        return text;
                    }
                }
            } finally { process = null; if (cookieCopy != null) { try { File.Delete(cookieCopy); } catch { } } }
        }
        private static async Task<string> ReadLines(StreamReader reader, Action<string> onLine) {
            string line; while ((line = await reader.ReadLineAsync()) != null) onLine(line); return "";
        }
        private static async Task<string> ReadLimited(StreamReader reader, int limit, bool failOnOverflow) {
            var result = new StringBuilder(); var buffer = new char[4096]; int count;
            while ((count = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0) {
                result.Append(buffer, 0, count);
                if (result.Length > limit) { if (failOnOverflow) throw new Exception("视频信息过大，无法解析。"); result.Remove(0, result.Length - limit); }
            }
            return result.ToString();
        }
        public async Task<object> InstallComponent(string name) {
            if (name != "ffmpeg" && name != "qjs") throw new Exception("未知组件。");
            var token = Start();
            try {
                await EnsureComponent(name, token);
                if (video != null && lastInfo != null) video = Core.Normalize(lastInfo, video.sourceUrl, Has("ffmpeg"));
                return new { state = State(), video = video };
            } finally { Finish(); }
        }
        private HttpClient CreateClient() {
            var handler = new HttpClientHandler();
            if (!string.IsNullOrEmpty(settings.proxy)) handler.Proxy = new WebProxy(settings.proxy);
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Shiying/0.1"); return client;
        }
        private async Task EnsureComponent(string name, CancellationToken token) {
            if (Has(name)) return;
            Directory.CreateDirectory(Path.Combine(DataDirectory, "bin"));
            var manifest = Core.Parse(File.ReadAllText(Path.Combine(appDirectory, "components.json")));
            var component = (Dictionary<string, object>)manifest[name];
            string url = Core.Text(component, "url"), hash = Core.Text(component, "sha256"), label = Core.Text(component, "label");
            if (!Regex.IsMatch(hash, "^[a-f0-9]{64}$")) throw new Exception("组件校验信息缺失，请更新应用。");
            string archive = Path.Combine(DataDirectory, "bin", name + ".download");
            string temporary = Path.Combine(DataDirectory, "bin", name + ".new");
            string target = Path.Combine(DataDirectory, "bin", name + ".exe");
            try {
                using (var client = CreateClient()) using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token)) {
                    response.EnsureSuccessStatusCode();
                    long total = response.Content.Headers.ContentLength ?? 0, downloaded = 0;
                    Report(new { stage = "component", message = "首次使用，正在准备" + label + "…", percent = (double?)null });
                    using (var input = await response.Content.ReadAsStreamAsync()) using (var output = File.Create(archive)) {
                        var buffer = new byte[81920]; int read; var clock = Stopwatch.StartNew();
                        while ((read = await input.ReadAsync(buffer, 0, buffer.Length, token)) > 0) {
                            await output.WriteAsync(buffer, 0, read, token); downloaded += read;
                            if (clock.ElapsedMilliseconds > 300) { Report(new { stage = "component", message = "正在准备" + label + "…", percent = total > 0 ? (double?)(downloaded * 100d / total) : null, downloaded = downloaded, total = total }); clock.Restart(); }
                        }
                    }
                }
                token.ThrowIfCancellationRequested();
                using (var sha = SHA256.Create()) using (var stream = File.OpenRead(archive)) {
                    string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                    if (actual != hash) throw new Exception("组件完整性校验失败，请重新下载。");
                }
                await Task.Run(() => {
                    if (Core.Text(component, "type") == "binary") {
                        File.Copy(archive, temporary);
                    } else {
                        using (var zip = ZipFile.OpenRead(archive)) {
                            var entry = zip.Entries.Single(e => e.FullName == Core.Text(component, "entry"));
                            using (var input = entry.Open()) using (var output = File.Create(temporary)) { CopyCancelable(input, output, token); }
                        }
                    }
                }, token);
                token.ThrowIfCancellationRequested();
                File.Move(temporary, target);
            } catch (HttpRequestException ex) { throw new EngineException("组件下载失败，请检查网络或在连接设置中填写代理后重试。", ex.Message); }
            finally { if (File.Exists(archive)) File.Delete(archive); if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private static void CopyCancelable(Stream input, Stream output, CancellationToken token) {
            var buffer = new byte[81920]; int size; while ((size = input.Read(buffer, 0, buffer.Length)) > 0) { token.ThrowIfCancellationRequested(); output.Write(buffer, 0, size); }
        }
    }
    public class EngineException : Exception { public string Details; public EngineException(string message, string details) : base(message) { Details = details; } }
}
