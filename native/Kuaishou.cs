using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Shiying {
    // Public share-page adapter. Login/verification pages are reported to the user, never bypassed.
    public static class Kuaishou {
        public const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36";
        public static async Task<Dictionary<string, object>> Resolve(string source, Settings settings, CancellationToken token) {
            var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false };
            if (!string.IsNullOrWhiteSpace(settings.proxy)) handler.Proxy = new WebProxy(settings.proxy);
            if (!string.IsNullOrWhiteSpace(settings.cookieFile) && System.IO.File.Exists(settings.cookieFile)) {
                foreach (string original in System.IO.File.ReadLines(settings.cookieFile)) {
                    string line = original.StartsWith("#HttpOnly_") ? original.Substring(10) : original;
                    if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line)) continue;
                    string[] fields = line.Split('\t'); if (fields.Length < 7) continue;
                    string domain = fields[0].TrimStart('.');
                    if (domain != "kuaishou.com" && !domain.EndsWith(".kuaishou.com") && domain != "gifshow.com" && !domain.EndsWith(".gifshow.com")) continue;
                    try { handler.CookieContainer.Add(new Cookie(fields[5], fields[6], fields[2], fields[0]) { Secure = fields[3] == "TRUE" }); } catch (CookieException) { }
                }
            }
            using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(45) }) {
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
                var uri = new Uri(source);
                for (int redirect = 0; redirect < 8; redirect++) {
                    using (var response = await client.GetAsync(uri, token)) {
                        if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400 && response.Headers.Location != null) {
                            uri = response.Headers.Location.IsAbsoluteUri ? response.Headers.Location : new Uri(uri, response.Headers.Location);
                            if ((uri.Scheme != "http" && uri.Scheme != "https") || Core.Platform(uri.AbsoluteUri) != "快手") throw new Exception("快手链接跳转到未知页面，请重新复制视频分享链接。");
                            continue;
                        }
                        if (!response.IsSuccessStatusCode) throw new Exception("快手要求登录或验证，请在连接设置中选择登录信息文件后重试。");
                        string html = await response.Content.ReadAsStringAsync();
                        var result = ParsePage(html, uri.AbsoluteUri);
                        if (result != null) return result;
                        throw new Exception("未能读取这个快手视频。请在浏览器确认视频可播放，并尝试导入登录信息文件。平台验证页面暂不支持解析。");
                    }
                }
                throw new Exception("快手链接跳转次数过多，请使用视频详情页链接。");
            }
        }
        public static Dictionary<string, object> ParsePage(string html, string source) {
            // Embedded page data used by public desktop, mobile share and Next.js pages.
            foreach (Match match in Regex.Matches(html, @"<script\b[^>]*>([\s\S]*?)</script>", RegexOptions.IgnoreCase)) {
                string content = match.Groups[1].Value.Trim();
                string json = content;
                var assignment = Regex.Match(content, @"(?:window\.)?__(?:APOLLO_STATE|INITIAL_STATE|NEXT_DATA|NUXT)__\s*=\s*");
                if (assignment.Success) json = content.Substring(assignment.Index + assignment.Length).Trim().TrimEnd(';');
                if (!json.StartsWith("{")) continue;
                try {
                    object data = Core.Serializer().DeserializeObject(json);
                    foreach (var item in Walk(data, 0)) {
                        string media = Core.Text(item, "photoUrl", Core.Text(item, "videoUrl", Core.Text(item, "playUrl")));
                        if (media == "") {
                            object raw;
                            if (item.TryGetValue("mainMvUrls", out raw)) foreach (var nested in Walk(raw, 0)) { media = Core.Text(nested, "url"); if (media != "") break; }
                        }
                        Uri parsed;
                        if (!Uri.TryCreate(media, UriKind.Absolute, out parsed) || (parsed.Scheme != "http" && parsed.Scheme != "https")) continue;
                        string id = Core.Text(item, "photoId", Core.Text(item, "id"));
                        if (id == "") id = Regex.Match(new Uri(source).AbsolutePath, @"[^/]+$").Value;
                        double duration = Core.Number(item, "duration"); if (duration > 1000) duration /= 1000;
                        return new Dictionary<string, object> {
                            { "id", id }, { "title", Core.Text(item, "caption", Core.Text(item, "title", "快手视频")) },
                            { "thumbnail", Core.Text(item, "coverUrl", Core.Text(item, "poster")) }, { "duration", duration },
                            { "url", media }, { "webpage_url", source }, { "extractor", "generic" }, { "extractor_key", "Generic" },
                            { "ext", "mp4" }, { "vcodec", "unknown" }, { "acodec", "unknown" },
                            { "http_headers", new Dictionary<string, object> { { "Referer", source }, { "User-Agent", UserAgent } } }
                        };
                    }
                } catch (ArgumentException) { } catch (InvalidOperationException) { }
            }
            return null;
        }
        private static IEnumerable<Dictionary<string, object>> Walk(object value, int depth) {
            if (depth > 40 || value == null || value is string) yield break;
            var obj = value as Dictionary<string, object>;
            if (obj != null) {
                yield return obj;
                foreach (object child in obj.Values) foreach (var found in Walk(child, depth + 1)) yield return found;
            } else if (value is IEnumerable) {
                foreach (object child in (IEnumerable)value) foreach (var found in Walk(child, depth + 1)) yield return found;
            }
        }
    }
}
