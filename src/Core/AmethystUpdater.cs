using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Amethyst;

internal static partial class AmethystUpdater
{
    private const string ApiUrl = AmethystRelease.ApiUrl;
    private const int TimeoutSeconds = 20;

    internal enum Phase { Idle, Checking, UpToDate, Found, Downloading, Ready, Done, Failed }

    internal static Phase State = Phase.Idle;
    internal static string RemoteVersion = string.Empty;
    internal static string DownloadUrl = string.Empty;
    internal static string ExpectedSha256 = string.Empty;
    internal static string StagedPath = string.Empty;
    internal static bool DownloadViaApi;   
    internal static string Error = string.Empty;
    internal static float Progress;
    internal static bool NeedConfirm;
    internal static bool NeedRestart;

    internal static bool Busy => State == Phase.Checking || State == Phase.Downloading;


    internal static string StagingDir
    {
        get
        {
            try { return Path.Combine(AmethystPlatform.DataDir, "update"); }
            catch { return string.Empty; }
        }
    }


    internal static IEnumerator CoCheck()
    {
        Reset();
        State = Phase.Checking;

        UnityWebRequest req = null;
        try
        {
            req = UnityWebRequest.Get(ApiUrl);
            if (req != null)
            {
                req.timeout = TimeoutSeconds;
                req.SetRequestHeader("Accept", "application/vnd.github+json");
                req.SetRequestHeader("User-Agent", "Amethyst-Updater");
            }
        }
        catch { }

        if (req == null)
        {
            Fail(AmethystText.T("UpdateCheckFailed"));
            yield break;
        }

        yield return req.SendWebRequest();

        try
        {
            if (req.result != UnityWebRequest.Result.Success || req.downloadHandler == null)
            {
                Fail(AmethystText.T("UpdateCheckFailed"));
                yield break;
            }

            string json = req.downloadHandler.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                Fail(AmethystText.T("UpdateCheckFailed"));
                yield break;
            }

            if (!AmethystRelease.TryParse(json, out AmethystRelease.Info rel))
            {
                Fail(AmethystText.T("UpdateCheckFailed"));
                yield break;
            }

            if (string.IsNullOrEmpty(rel.Version))
            {
                Fail(AmethystText.T("UpdateCheckFailed"));
                yield break;
            }

            RemoteVersion = rel.Version;

            if (SafeApiAssetUrl(rel.AssetApiUrl))
            {
                DownloadUrl = rel.AssetApiUrl;
                DownloadViaApi = true;
            }
            else if (SafeUrl(rel.AssetUrl))
            {
                DownloadUrl = rel.AssetUrl;
                DownloadViaApi = false;
            }
            else
            {
                Fail(AmethystText.T("UpdateNoAsset"));
                yield break;
            }

            ExpectedSha256 = NormalizeDigest(rel.Digest);

            if (AmethystRelease.Compare(AmethystPlugin.PluginVersion, RemoteVersion) <= 0)
            {
                State = Phase.UpToDate;
                AmethystToast.Push(AmethystText.T("CheckUpdate"),
                    AmethystText.T("UpdateUpToDate", AmethystPlugin.PluginVersion),
                    3.5f, AmethystNotifyKind.Success);
                yield break;
            }

            State = Phase.Found;
            NeedConfirm = true;
            AmethystToast.Push(AmethystText.T("CheckUpdate"),
                AmethystText.T("UpdateFound", AmethystPlugin.PluginVersion, RemoteVersion),
                6f, AmethystNotifyKind.Info);
        }
        catch (Exception ex)
        {
            Fail(AmethystText.T("UpdateCheckFailed"));
            try { AmethystPlugin.Logger?.LogWarning("[Updater] check failed: " + ex.Message); } catch { }
        }
        finally
        {
            try { req.Dispose(); } catch { }
        }
    }

    internal static IEnumerator CoDownload()
    {
        if (string.IsNullOrEmpty(DownloadUrl)) { Fail(AmethystText.T("UpdateDownloadFailed")); yield break; }

        NeedConfirm = false;
        State = Phase.Downloading;
        Progress = 0f;

        string dir = StagingDir;
        string staged = string.Empty;

        try
        {
            if (string.IsNullOrEmpty(dir)) { Fail(AmethystText.T("UpdateDownloadFailed")); yield break; }
            Directory.CreateDirectory(dir);
            staged = Path.Combine(dir, "Amethyst.new");

            try { if (File.Exists(staged)) File.Delete(staged); } catch { }
        }
        catch
        {
            Fail(AmethystText.T("UpdateDownloadFailed"));
            yield break;
        }

        const float StallSeconds = 30f;

        string downloadError = null;
        long totalRead = 0;
        long reportedBytes = 0;
        long expectedLength = 0;

        var cancel = new System.Threading.CancellationTokenSource();

        var task = System.Threading.Tasks.Task.Run(async () =>
        {
            using (var http = new System.Net.Http.HttpClient())
            {
                http.Timeout = System.Threading.Timeout.InfiniteTimeSpan;  
                http.DefaultRequestHeaders.Add("User-Agent", "Amethyst-Updater");

                var request = new System.Net.Http.HttpRequestMessage(
                    System.Net.Http.HttpMethod.Get, DownloadUrl);

                if (DownloadViaApi)
                {
                    
                    request.Headers.Accept.ParseAdd("application/octet-stream");
                }

                using (request)
                using (var resp = await http.SendAsync(request,
                    System.Net.Http.HttpCompletionOption.ResponseHeadersRead,
                    cancel.Token).ConfigureAwait(false))
                {
                    resp.EnsureSuccessStatusCode();

                    long len = resp.Content.Headers.ContentLength ?? -1;
                    System.Threading.Interlocked.Exchange(ref expectedLength, len);

                    using (var net = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var fs = new FileStream(staged, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        byte[] buf = new byte[64 * 1024];

                        while (true)
                        {
                            cancel.Token.ThrowIfCancellationRequested();

                            int n = await net.ReadAsync(buf, 0, buf.Length, cancel.Token).ConfigureAwait(false);
                            if (n <= 0) break;

                            fs.Write(buf, 0, n);

                            long done = System.Threading.Interlocked.Add(ref totalRead, n);
                            System.Threading.Interlocked.Exchange(ref reportedBytes, done);
                        }

                        fs.Flush();
                    }
                }
            }
        });

        long lastSeen = -1;
        float lastChangeAt = Time.realtimeSinceStartup;
        bool stalled = false;

        while (!task.IsCompleted)
        {
            long nowBytes = System.Threading.Interlocked.Read(ref reportedBytes);
            long total = System.Threading.Interlocked.Read(ref expectedLength);

            if (nowBytes != lastSeen)
            {
                lastSeen = nowBytes;
                lastChangeAt = Time.realtimeSinceStartup;

                if (total > 0)
                {
                    Progress = Mathf.Clamp((float)nowBytes / total, 0f, 0.99f);
                }
                else if (nowBytes > 0)
                {
                    
                    Progress = Mathf.Min(0.95f, 0.05f + nowBytes / (64f * 1024f * 1024f));
                }
            }
            else if (Time.realtimeSinceStartup - lastChangeAt > StallSeconds)
            {
                stalled = true;
                try { cancel.Cancel(); } catch { }
                break;
            }

            yield return null;
        }

        if (stalled)
        {
            
            float waitUntil = Time.realtimeSinceStartup + 3f;
            while (!task.IsCompleted && Time.realtimeSinceStartup < waitUntil) yield return null;

            try { if (File.Exists(staged)) File.Delete(staged); } catch { }
            Fail(AmethystText.T("UpdateDownloadStalled"));
            yield break;
        }

        if (task.IsFaulted)
        {
            try { downloadError = task.Exception?.GetBaseException()?.Message; } catch { }
            try { if (File.Exists(staged)) File.Delete(staged); } catch { }

            try
            {
                AmethystPlugin.Logger?.LogWarning(
                    "[Updater] download failed: url=" + DownloadUrl
                    + " viaApi=" + DownloadViaApi + " err=" + downloadError);
            }
            catch { }

            Fail(AmethystText.T("UpdateDownloadFailed"));
            yield break;
        }

        bool ok = false;
        long finalRead = System.Threading.Interlocked.Read(ref totalRead);
        try
        {
            FileInfo fi = new FileInfo(staged);
            ok = fi.Exists && fi.Length > 0 && fi.Length == finalRead;
            if (!ok)
            {
                try { AmethystPlugin.Logger?.LogWarning("[Updater] file mismatch: onDisk=" + fi.Length + " read=" + finalRead); } catch { }
            }
        }
        catch { ok = false; }

        if (!ok)
        {
            try { if (File.Exists(staged)) File.Delete(staged); } catch { }
            Fail(AmethystText.T("UpdateDownloadFailed"));
            yield break;
        }

        Progress = 1f;

        string reason = Verify(staged);
        if (reason != null)
        {
            try { if (File.Exists(staged)) File.Delete(staged); } catch { }
            Fail(AmethystText.T("UpdateInvalidFile"));
            try { AmethystPlugin.Logger?.LogWarning("[Updater] rejected download: " + reason); } catch { }
            yield break;
        }

        if (!DigestMatches(staged, ExpectedSha256))
        {
            try { if (File.Exists(staged)) File.Delete(staged); } catch { }
            Fail(AmethystText.T("UpdateInvalidFile"));
            try { AmethystPlugin.Logger?.LogWarning("[Updater] sha256 mismatch"); } catch { }
            yield break;
        }

        StagedPath = staged;
        Progress = 1f;
        State = Phase.Ready;
        NeedRestart = true;
        AmethystToast.Push(AmethystText.T("CheckUpdate"),
            AmethystText.T("UpdateReady", RemoteVersion), 8f, AmethystNotifyKind.Success);
    }



    private static string Verify(string path)
    {
        try
        {
            FileInfo fi = new FileInfo(path);
            if (!fi.Exists) return "missing";
            if (fi.Length < 4096) return "too small: " + fi.Length;   

            AssemblyName an = AssemblyName.GetAssemblyName(path);
            if (an == null || !string.Equals(an.Name, "Amethyst", StringComparison.OrdinalIgnoreCase))
                return "unexpected assembly: " + (an != null ? an.Name : "null");
            return null;
        }
        catch (Exception ex) { return ex.GetType().Name + ": " + ex.Message; }
    }

    internal static bool BeginReplace()
    {
        try
        {
            string target = CurrentDllPath();
            if (string.IsNullOrEmpty(target) || !File.Exists(target))
            {
                Fail(AmethystText.T("UpdateTargetMissing"));
                return false;
            }

            string plugins = string.Empty;
            try { plugins = Path.GetFullPath(BepInEx.Paths.PluginPath); } catch { }
            if (!string.IsNullOrEmpty(plugins))
            {
                if (!plugins.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                    plugins += Path.DirectorySeparatorChar;
            }
            if (string.IsNullOrEmpty(plugins) ||
                !Path.GetFullPath(target).StartsWith(plugins, StringComparison.OrdinalIgnoreCase))
            {
                Fail(AmethystText.T("UpdateTargetMissing"));
                return false;
            }

            if (string.IsNullOrEmpty(StagedPath) || !File.Exists(StagedPath))
            {
                Fail(AmethystText.T("UpdateDownloadFailed"));
                return false;
            }

            try
            {
                string backup = target + ".bak";

                if (File.Exists(backup)) File.Delete(backup);
                File.Move(target, backup);          

                try
                {
                    File.Move(StagedPath, target);  
                }
                catch
                {
                    
                    try { if (File.Exists(target)) File.Delete(target); } catch { }
                    try { File.Move(backup, target); } catch { }
                    throw;
                }

                State = Phase.Done;
                NeedRestart = false;
                AmethystToast.Push(AmethystText.T("CheckUpdate"),
                    AmethystText.T("UpdateDoneRestart"), 10f, AmethystNotifyKind.Success);
                try { AmethystPlugin.Logger?.LogInfo("[Updater] replaced in place, restart required"); } catch { }
                return true;
            }
            catch (Exception exInPlace)
            {
                try { AmethystPlugin.Logger?.LogWarning("[Updater] in-place replace failed, falling back to script: " + exInPlace.Message); } catch { }
            }

            if (HasBatchUnsafe(target) || HasBatchUnsafe(StagedPath) || HasBatchUnsafe(StagingDir))
            {
                Fail(AmethystText.T("UpdateApplyFailed"));
                try { AmethystPlugin.Logger?.LogWarning("[Updater] unsafe path for batch: " + target); } catch { }
                return false;
            }

            string exe = Path.Combine(BepInEx.Paths.GameRootPath, "Among Us.exe");
            string script = Path.Combine(StagingDir, "apply_update.bat");

            string bat =
                "@echo off\r\n" +
                "chcp 65001 >nul\r\n" +
                "setlocal\r\n" +
                "set \"SRC=" + StagedPath + "\"\r\n" +
                "set \"DST=" + target + "\"\r\n" +
                "set \"BAK=" + target + ".bak\"\r\n" +
                "set \"EXE=" + exe + "\"\r\n" +
                "ping -n 4 127.0.0.1 >nul\r\n" +
                ":waitloop\r\n" +
                "tasklist /fi \"imagename eq Among Us.exe\" 2>nul | find /i \"Among Us.exe\" >nul\r\n" +
                "if not errorlevel 1 ( ping -n 2 127.0.0.1 >nul & goto waitloop )\r\n" +
                "if exist \"%BAK%\" del /f /q \"%BAK%\" >nul 2>&1\r\n" +
                "if exist \"%DST%\" move /y \"%DST%\" \"%BAK%\" >nul 2>&1\r\n" +
                "copy /y \"%SRC%\" \"%DST%\" >nul 2>&1\r\n" +
                "if errorlevel 1 (\r\n" +
                "  if exist \"%BAK%\" move /y \"%BAK%\" \"%DST%\" >nul 2>&1\r\n" +
                "  echo update failed> \"%" + "~dp0update_failed.txt\"\r\n" +
                "  exit /b 1\r\n" +
                ")\r\n" +
                "del /f /q \"%SRC%\" >nul 2>&1\r\n" +
                "if exist \"%EXE%\" start \"\" \"%EXE%\"\r\n" +
                "exit /b 0\r\n";

            File.WriteAllText(script, bat, new System.Text.UTF8Encoding(false));

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c \"" + script + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = StagingDir,
            };
            System.Diagnostics.Process.Start(psi);

            AmethystToast.Push(AmethystText.T("CheckUpdate"),
                AmethystText.T("UpdateRestartHint"), 8f, AmethystNotifyKind.Warning);

            try { Application.Quit(); } catch { }
            return true;
        }
        catch (Exception ex)
        {
            Fail(AmethystText.T("UpdateApplyFailed"));
            try { AmethystPlugin.Logger?.LogWarning("[Updater] apply failed: " + ex.Message); } catch { }
            return false;
        }
    }


}
