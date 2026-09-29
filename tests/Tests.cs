using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Shiying;
using ShiyingSetup;

class Tests {
    static int count;
    static void Check(bool value, string name) { if (!value) throw new Exception("FAILED: " + name); Console.WriteLine("PASS " + name); count++; }
    static void Throws(Action action, string name) { bool thrown = false; try { action(); } catch { thrown = true; } Check(thrown, name); }
    static async Task ThrowsAsync(Func<Task> action, string name) { bool thrown = false; try { await action(); } catch { thrown = true; } Check(thrown, name); }
    static int Main(string[] args) { try { Run(args).GetAwaiter().GetResult(); Console.WriteLine("Passed " + count + " checks."); return 0; } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; } }
    static async Task Run(string[] args) {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        string root = Path.GetFullPath(args[0]);
        string output = Path.Combine(root, "test-results", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(output);
        Check(Core.ExtractUrl("复制这段分享文案 https://v.douyin.com/AbC_12/，打开抖音") == "https://v.douyin.com/AbC_12/", "Extract share URL with Chinese punctuation");
        Check(Core.ExtractUrl("(https://www.youtube.com/watch?v=Ab_c-123#hello).") == "https://www.youtube.com/watch?v=Ab_c-123", "Trim wrapper punctuation and fragment");
        Check(Core.ExtractUrl("https://youtu.be/a https://youtu.be/a") == "https://youtu.be/a", "Deduplicate identical URLs");
        Throws(() => Core.ExtractUrl("https://a.com/ https://b.com/"), "Reject multiple different URLs");
        Throws(() => Core.ExtractUrl("file:///C:/private.mp4"), "Reject non-web schemes");
        Throws(() => Core.ExtractUrl("https://user:secret@example.com/video"), "Reject embedded credentials");
        Throws(() => Core.ExtractUrl("hello"), "Reject missing URL");
        Check(Core.Platform("https://vm.tiktok.com/abc") == "TikTok", "Identify TikTok short link");
        Check(Core.Platform("https://v.kuaishou.com/abc") == "快手", "Identify Kuaishou short link");
        Check(Core.Platform("https://youtube.com.evil.test/v") != "YouTube", "Platform names require an actual domain boundary");
        Throws(() => Core.Proxy("http://name:secret@localhost:7890"), "Reject proxy credentials");
        Check(Core.Proxy("http://127.0.0.1:7890") == "http://127.0.0.1:7890", "Accept local HTTP proxy");
        Check(Core.Format("1080", true) == "bv*[height<=1080]+ba/b[height<=1080]", "Enforce chosen maximum resolution");
        Check(Core.Format("best", false) == "b[ext=mp4]/b", "Lightweight mode requires combined video/audio");
        Throws(() => Core.Format("1080];--exec calc", true), "Reject injected format expressions");
        Check(Core.Quote("C:\\含 空格\\").EndsWith("\\\\\""), "Quote trailing slash in Windows arguments");
        Check(!Core.Inside(Path.Combine(output, "..", "escape.mp4"), output), "Reject output outside selected folder");
        var parsed = Core.Parse("{\"title\":\"影片\",\"formats\":[{\"height\":1080,\"vcodec\":\"h264\",\"acodec\":\"none\"},{\"height\":720,\"vcodec\":\"h264\",\"acodec\":\"aac\"},{\"height\":720,\"vcodec\":\"h264\",\"acodec\":\"aac\"}]}");
        var video = Core.Normalize(parsed, "https://www.youtube.com/watch?v=a", false);
        Check(video.qualities.Count == 2 && video.qualities[1].value == "720", "Do not offer unmergeable or duplicate formats");
        Check(Core.Normalize(parsed, "https://www.youtube.com/watch?v=a", true).qualities.Count == 3, "Offer separated HD when merger installed");
        Throws(() => Core.Normalize(Core.Parse("{\"_type\":\"playlist\",\"entries\":[]}"), "https://example.com/", false), "Reject playlists");
        Throws(() => Core.Normalize(Core.Parse("{\"is_live\":true}"), "https://example.com/", false), "Reject active livestreams");
        var ks = Kuaishou.ParsePage("<script>window.__APOLLO_STATE__={\"Photo:1\":{\"photoId\":\"3abc\",\"caption\":\"快手标题\",\"photoUrl\":\"https://cdn.example.com/video.mp4\",\"coverUrl\":\"https://cdn.example.com/cover.jpg\"}};</script>", "https://www.kuaishou.com/short-video/3abc");
        Check(Core.Text(ks, "title") == "快手标题" && Core.Text(ks, "url").EndsWith("video.mp4"), "Parse embedded public Kuaishou data");
        Check(Kuaishou.ParsePage("<html>请完成验证</html>", "https://v.kuaishou.com/abc") == null, "Do not treat verification pages as videos");
        Check(Kuaishou.ParsePage("<script>{\"photoUrl\":\"file:///C:/secret\"}</script>", "https://v.kuaishou.com/abc") == null, "Reject non-network Kuaishou media URLs");
        string fakeRoot = Path.Combine(output, "fake-app"); Directory.CreateDirectory(Path.Combine(fakeRoot, "bin"));
        File.Copy(Path.Combine(root, "build", "tests", "FakeEngine.exe"), Path.Combine(fakeRoot, "bin", "yt-dlp.exe"));
        var engine = new Engine(fakeRoot, Path.Combine(output, "profile"));
        engine.settings.folder = Path.Combine(output, "下载 文件夹"); engine.SaveSettings();
        var info = await engine.Analyze("分享 https://example.com/video?id=1&extra=2");
        Check(info.title == "测试视频 & 中文文件名" && !engine.Busy, "Analyze actual child process with Unicode metadata");
        bool progressReceived = false;
        engine.Progress += p => { if (Core.Json(p).Contains("50")) progressReceived = true; };
        var entry = await engine.Download(info.id, "720");
        Check(File.Exists(entry.filePath) && entry.size > 0 && Core.Inside(entry.filePath, engine.settings.folder), "Write downloaded file to chosen Unicode folder");
        Check(progressReceived, "Parse real process progress protocol");
        var restored = new Engine(fakeRoot, Path.Combine(output, "profile"));
        Check(restored.history.Count == 1 && restored.settings.folder == engine.settings.folder, "Persist folder and download history");
        await ThrowsAsync(async () => await engine.Download("forged-id", "best"), "Reject stale or forged video IDs");
        var slow = engine.Analyze("https://slow.example/video");
        await ThrowsAsync(async () => await engine.Analyze("https://example.com/other"), "Prevent concurrent operations");
        engine.Cancel();
        bool cancelled = false; try { await slow; } catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled && !engine.Busy, "Cancel the process and release busy state");
        await ThrowsAsync(async () => await engine.Analyze("https://fail.example/video"), "Surface extractor failure");
        Check(!engine.Busy && engine.video == null, "Clear stale result after failed parse");
        string installRoot = Path.Combine(output, "Installed");
        using (var stream = new MemoryStream()) {
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) {
                using (var writer = new StreamWriter(zip.CreateEntry("Shiying.exe").Open())) writer.Write("test");
                using (var writer = new StreamWriter(zip.CreateEntry("ui/index.html").Open())) writer.Write("test page");
            }
            stream.Position = 0; Installation.Extract(stream, installRoot, null);
        }
        Check(File.Exists(Path.Combine(installRoot, "Shiying.exe")), "Installer extracts payload");
        File.WriteAllText(Path.Combine(installRoot, "user-video.mp4"), "keep");
        Installation.RemoveFiles(installRoot);
        Check(File.Exists(Path.Combine(installRoot, "user-video.mp4")) && !File.Exists(Path.Combine(installRoot, "Shiying.exe")), "Uninstaller preserves files outside installation manifest");
        Throws(() => Installation.ValidateTarget(output), "Reject install into an unrelated nonempty folder");
        using (var stream = new MemoryStream()) {
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) { zip.CreateEntry("Shiying.exe"); zip.CreateEntry("../outside.txt"); }
            stream.Position = 0; Throws(() => Installation.Extract(stream, Path.Combine(output, "Unsafe"), null), "Block installer zip path traversal before writes");
        }
        if (args.Contains("--integration")) {
            var actual = new Engine(Path.Combine(root, "build", "app"), Path.Combine(output, "real-profile"));
            actual.settings.folder = Path.Combine(output, "真实下载");
            Console.WriteLine("Testing a public CC0 sample video...");
            var actualVideo = await actual.Analyze("https://interactive-examples.mdn.mozilla.net/media/cc0-videos/flower.mp4");
            var download = await actual.Download(actualVideo.id, "best");
            Check(download.size > 10000, "Real yt-dlp metadata and MP4 download");
            byte[] bytes = File.ReadAllBytes(download.filePath);
            Check(System.Text.Encoding.ASCII.GetString(bytes, 4, 4) == "ftyp", "Downloaded file contains a valid MP4 container header");
            Console.WriteLine("Saved sample: " + download.filePath);
            var repeated = await actual.Download(actualVideo.id, "best");
            Check(repeated.size == download.size, "Repeat download recognizes already saved file");
        }
    }
}
