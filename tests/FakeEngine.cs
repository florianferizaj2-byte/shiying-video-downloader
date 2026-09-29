using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
class FakeEngine {
    static int Main(string[] args) {
        Console.OutputEncoding = Encoding.UTF8;
        string source = args.LastOrDefault() ?? "";
        if (source.Contains("slow.example")) { Thread.Sleep(60000); return 0; }
        if (source.Contains("fail.example")) { Console.Error.WriteLine("ERROR: Please log in using cookies"); return 1; }
        var json = new JavaScriptSerializer();
        if (args.Contains("--dump-single-json")) {
            Console.WriteLine(json.Serialize(new { id = "test-01", title = "测试视频 & 中文文件名", uploader = "Test", duration = 11, url = source, formats = new[] { new { format_id = "mp4", height = 720, vcodec = "h264", acodec = "aac" } } }));
            return 0;
        }
        string folder = args[Array.IndexOf(args, "--paths") + 1];
        string file = Path.Combine(folder, "测试视频 & 中文文件名 [test-01].mp4");
        File.WriteAllBytes(file, new byte[] { 0, 0, 0, 24, 102, 116, 121, 112, 109, 112, 52, 50 });
        Console.WriteLine("__SHI_PROGRESS__{\"status\":\"downloading\",\"downloaded\":6,\"total\":12,\"speed\":6,\"eta\":1}");
        Console.WriteLine("__SHI_FILE__" + json.Serialize(file));
        return 0;
    }
}
