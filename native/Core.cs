using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Shiying {
    public class Settings {
        public string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "拾影");
        public string proxy = "";
        public string cookieFile = "";
    }
    public class HistoryEntry {
        public string id, title, platform, filePath, completedAt;
        public long size;
    }
    public class Video {
        public string id, title, uploader, thumbnail, platform, sourceUrl;
        public double duration;
        public bool needsMerger;
        public List<Quality> qualities;
    }
    public class Quality { public string value, label; }
    public static class Core {
        public static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024, RecursionLimit = 120 }; }
        public static string Json(object value) { return Serializer().Serialize(value); }
        public static Dictionary<string, object> Parse(string json) { return Serializer().Deserialize<Dictionary<string, object>>(json); }
        public static string Text(IDictionary<string, object> obj, string key, string fallback = "") {
            object value;
            return obj != null && obj.TryGetValue(key, out value) && value != null ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) : fallback;
        }
        public static double Number(IDictionary<string, object> obj, string key) {
            double value;
            return double.TryParse(Text(obj, key), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value) ? value : 0;
        }
        public static bool Flag(IDictionary<string, object> obj, string key) { return Text(obj, key).Equals("true", StringComparison.OrdinalIgnoreCase); }
        public static IEnumerable<Dictionary<string, object>> Objects(IDictionary<string, object> obj, string key) {
            object value;
            if (!obj.TryGetValue(key, out value) || !(value is IEnumerable)) yield break;
            foreach (object item in (IEnumerable)value) { var record = item as Dictionary<string, object>; if (record != null) yield return record; }
        }
        public static string ExtractUrl(string input) {
            if (string.IsNullOrWhiteSpace(input) || input.Length > 16000) throw new Exception("请粘贴一条视频链接或分享文案。");
            var matches = Regex.Matches(input, @"https?://[^\s<>""'\u2018\u2019\u201c\u201d\u3000-\u303f\uff00-\uffef\u4e00-\u9fff]+", RegexOptions.IgnoreCase);
            var urls = matches.Cast<Match>().Select(m => m.Value.TrimEnd(')', ',', '.', '!', '?', ';', ']', '}')).Distinct().ToList();
            if (urls.Count == 0) throw new Exception("没有找到有效链接，请粘贴以 http:// 或 https:// 开头的视频链接。");
            if (urls.Count > 1) throw new Exception("一次只能解析一个视频，请只保留一条链接。");
            Uri uri;
            if (!Uri.TryCreate(urls[0], UriKind.Absolute, out uri) || string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsoluteUri.Length > 4096)
                throw new Exception("链接格式不正确，请重新复制完整的视频链接。");
            return new UriBuilder(uri) { Fragment = "" }.Uri.AbsoluteUri;
        }
        public static string Platform(string url) {
            var host = new Uri(url).Host.ToLowerInvariant();
            Func<string, bool> belongs = domain => host == domain || host.EndsWith("." + domain);
            if (belongs("douyin.com") || belongs("iesdouyin.com")) return "抖音";
            if (belongs("tiktok.com")) return "TikTok";
            if (belongs("kuaishou.com") || belongs("gifshow.com") || belongs("kwai.com")) return "快手";
            if (belongs("youtube.com") || belongs("youtu.be")) return "YouTube";
            if (belongs("bilibili.com") || belongs("b23.tv")) return "哔哩哔哩";
            return host;
        }
        public static Video Normalize(Dictionary<string, object> info, string url, bool hasMerger) {
            if (Text(info, "_type") == "playlist" || info.ContainsKey("entries")) throw new Exception("请使用单条视频链接，暂不支持合集和播放列表。");
            if (Flag(info, "is_live") || Text(info, "live_status") == "is_upcoming") throw new Exception("暂不支持正在直播或尚未开始的视频，请在直播结束后重试。");
            var formats = Objects(info, "formats").Where(f => Text(f, "vcodec") != "none" && !Flag(f, "has_drm")).ToList();
            if (Flag(info, "has_drm") || (formats.Count == 0 && (Text(info, "url") == "" || Text(info, "vcodec") == "none"))) throw new Exception("这个链接没有可下载的视频，或视频受加密保护。");
            var available = hasMerger ? formats : formats.Where(f => Text(f, "acodec") != "none").ToList();
            var heights = available.Select(f => (int)Number(f, "height")).Where(h => h > 0).Distinct().OrderByDescending(h => h);
            var qualities = new List<Quality> { new Quality { value = "best", label = hasMerger ? "最佳可用画质" : "最佳单文件画质" } };
            qualities.AddRange(heights.Select(h => new Quality { value = h.ToString(), label = h + "p" }));
            var thumbnail = Text(info, "thumbnail");
            Uri thumbnailUri;
            if (!Uri.TryCreate(thumbnail, UriKind.Absolute, out thumbnailUri) || (thumbnailUri.Scheme != "https" && thumbnailUri.Scheme != "http")) thumbnail = "";
            return new Video { id = Guid.NewGuid().ToString("N"), title = Text(info, "title", "未命名视频"), uploader = Text(info, "uploader", Text(info, "channel")),
                duration = Number(info, "duration"), thumbnail = thumbnail, platform = Platform(url), sourceUrl = url, qualities = qualities,
                needsMerger = !hasMerger && formats.Count > 0 && available.Count == 0 };
        }
        public static string Format(string quality, bool merger) {
            if (quality != "best" && !Regex.IsMatch(quality ?? "", @"^\d{2,5}$")) throw new Exception("请选择有效的清晰度。");
            var bound = quality == "best" ? "" : "[height<=" + quality + "]";
            return merger ? "bv*" + bound + "+ba/b" + bound : "b" + bound + "[ext=mp4]/b" + bound;
        }
        public static string Proxy(string input) {
            if (string.IsNullOrWhiteSpace(input)) return "";
            Uri uri;
            if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out uri) || (uri.Scheme != "http" && uri.Scheme != "https") || uri.UserInfo != "" || uri.AbsolutePath != "/" || uri.Query != "" || uri.Fragment != "")
                throw new Exception("请填写 HTTP 代理地址，例如 http://127.0.0.1:7890，不要包含账号密码。");
            return input.Trim();
        }
        public static string Quote(string value) {
            // Windows CommandLineToArgvW quoting, including embedded quotes and trailing backslashes.
            var result = new StringBuilder("\""); int slash = 0;
            foreach (char c in value) {
                if (c == '\\') { slash++; continue; }
                if (c == '"') { result.Append('\\', slash * 2 + 1).Append('"'); slash = 0; continue; }
                result.Append('\\', slash).Append(c); slash = 0;
            }
            return result.Append('\\', slash * 2).Append('"').ToString();
        }
        public static bool Inside(string file, string folder) {
            return Path.GetFullPath(file).StartsWith(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        public static string Friendly(string raw) {
            if (Regex.IsMatch(raw, "cookies?|sign in|log[ -]?in|not a bot|captcha|verify|verification|HTTP Error (401|403)|blocked", RegexOptions.IgnoreCase)) return "平台要求登录或验证。请在「连接设置」中选择登录信息文件，再重新解析。";
            if (Regex.IsMatch(raw, "timed? ?out|Unable to download|network|connection|resolve|HTTP Error (429|5[0-9][0-9])|Remote end closed", RegexOptions.IgnoreCase)) return "暂时无法连接视频平台。请检查网络；访问海外平台时，可在「连接设置」中填写代理。";
            if (Regex.IsMatch(raw, "Unsupported URL|No suitable extractor", RegexOptions.IgnoreCase)) return "暂时无法识别这个链接，请尝试视频详情页的完整链接。";
            if (Regex.IsMatch(raw, "Requested format", RegexOptions.IgnoreCase)) return "这个视频需要合并音画。请在「连接设置」中安装高清组件，然后重新解析。";
            if (Regex.IsMatch(raw, "Private video|video.*unavailable|removed|not available|does not exist|404", RegexOptions.IgnoreCase)) return "视频不存在、已下架或当前账号无权访问，请检查原始链接。";
            if (Regex.IsMatch(raw, "Permission denied|Access is denied|denied access", RegexOptions.IgnoreCase)) return "无法写入保存位置，请选择一个有写入权限的文件夹。";
            if (Regex.IsMatch(raw, "disk full|No space left", RegexOptions.IgnoreCase)) return "保存位置的空间不足，请清理空间或选择其他文件夹。";
            if (Regex.IsMatch(raw, "DRM|protected", RegexOptions.IgnoreCase)) return "该视频受加密保护，无法下载。";
            return "这次解析或下载没有完成，请重试或更换视频链接。";
        }
        public static void WriteAtomic(string file, string content) {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            string temp = file + ".tmp";
            File.WriteAllText(temp, content, new UTF8Encoding(false));
            if (File.Exists(file)) File.Replace(temp, file, null); else File.Move(temp, file);
        }
    }
}
