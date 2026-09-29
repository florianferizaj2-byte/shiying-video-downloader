using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Shiying;
class Components {
    static int Main(string[] args) { try { Run(args).GetAwaiter().GetResult(); return 0; } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; } }
    static async Task Run(string[] args) {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        string root = Path.GetFullPath(args[0]);
        var engine = new Engine(Path.Combine(root, "build", "app"), Path.Combine(root, "test-results", "components-profile"));
        foreach (string name in new[] { "qjs", "ffmpeg" }) {
            Console.WriteLine("Preparing " + name + " from verified package...");
            await engine.InstallComponent(name);
            var start = new ProcessStartInfo(engine.Binary(name), name == "qjs" ? "--version" : "-version") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true };
            using (var child = Process.Start(start)) { string line = await child.StandardOutput.ReadLineAsync(); await child.StandardOutput.ReadToEndAsync(); child.WaitForExit(); if (child.ExitCode != 0) throw new Exception(name + " could not start"); Console.WriteLine("PASS " + line); }
        }
        if (args.Length > 1) {
            foreach (string url in args[1].Split('|')) {
                try { var info = await engine.Analyze(url); Console.WriteLine("RESOLVED " + info.platform + ": " + info.title + " / " + info.duration + " sec / " + info.qualities.Count + " quality options"); }
                catch (Exception ex) { Console.WriteLine("PLATFORM LIMITATION: " + ex.Message); var failure = ex as EngineException; if (failure != null) Console.WriteLine(failure.Details); }
            }
        }
    }
}
