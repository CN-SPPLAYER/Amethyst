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

// ===== 模组自更新（第 266 轮）=====
//
// 为什么不能「下载后直接覆盖自己」：
//   Among Us 运行时 Amethyst.dll 已被进程映射（文件锁定），
//   进程内 File.Copy/WriteAllBytes 写目标必然 IOException。
//
// 因此流程是「下载 → 校验 → 暂存 → 交给外部脚本换文件 → 重启」：
//   1. 读 GitHub Releases API 拿最新 tag / 资产下载地址（含可选的 sha256 digest）；
//   2. 用 DownloadHandlerBuffer 接收后写入 %TEMP%\AmethystUpdate\Amethyst.new；
//   3. 校验：长度、sha256（API 提供时）、以及是否为名为 Amethyst 的 .NET 程序集；
//   4. 询问用户；同意后把 .new 连同「替换 + 重启」脚本写到同一目录；
//   5. 用户点「立即更新」→ 启动脚本 → 退出游戏，由脚本在进程结束后替换并重启。
//
// 安全约束（全部为硬编码，不读配置）：
//   · 只接受 https，且必须指向本仓库的 releases/download 路径；
//   · 目标路径必须位于 BepInEx\plugins 内（防路径穿越）；
//   · 替换前先把现有 DLL 备份为 .bak，替换失败则回滚；
//   · 任何一步失败都不动现有文件。
internal static class AmethystUpdater
{
    private const string ApiUrl = AmethystRelease.ApiUrl;
    private const int TimeoutSeconds = 20;

    // 阶段
    internal enum Phase { Idle, Checking, UpToDate, Found, Downloading, Ready, Done, Failed }

    internal static Phase State = Phase.Idle;
    internal static string RemoteVersion = string.Empty;
    internal static string DownloadUrl = string.Empty;
    internal static string ExpectedSha256 = string.Empty;
    internal static string StagedPath = string.Empty;
    internal static bool DownloadViaApi;   // true = 走 api.github.com 资产端点
    internal static string Error = string.Empty;
    internal static float Progress;
    internal static bool NeedConfirm;
    internal static bool NeedRestart;

    internal static bool Busy => State == Phase.Checking || State == Phase.Downloading;

    // 当前插件 DLL 的真实路径。BepInEx 的 Info.Location 指向被加载的 DLL，
    // 比 Assembly.GetExecutingAssembly().Location 更可靠（后者在 IL2CPP 下常为空）。
    internal static string CurrentDllPath()
    {
        try
        {
            string loc = typeof(AmethystUpdater).Assembly.Location;
            if (!string.IsNullOrEmpty(loc) && File.Exists(loc)) return loc;
        }
        catch { }
        try
        {
            string dir = BepInEx.Paths.PluginPath;
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                // 稳定名（自动部署使用的名字）
                string stable = Path.Combine(dir, "Amethyst.dll");
                if (File.Exists(stable)) return stable;

                // 允许用户直接安装发布名：Amethyst_<版本>_Windows.dll。
                // 选最新写入的那个，并校验程序集名仍为 Amethyst（防止误选别的模组）。
                string best = null;
                DateTime bestTime = DateTime.MinValue;
                foreach (string file in Directory.GetFiles(dir, "Amethyst*.dll"))
                {
                    try
                    {
                        System.Reflection.AssemblyName an = System.Reflection.AssemblyName.GetAssemblyName(file);
                        if (!string.Equals(an.Name, "Amethyst", StringComparison.Ordinal)) continue;

                        DateTime t = File.GetLastWriteTimeUtc(file);
                        if (best == null || t > bestTime)
                        {
                            best = file;
                            bestTime = t;
                        }
                    }
                    catch { }
                }
                if (best != null) return best;
            }
        }
        catch { }
        return string.Empty;
    }

    internal static string StagingDir
    {
        get
        {
            try { return Path.Combine(AmethystPlatform.DataDir, "update"); }
            catch { return string.Empty; }
        }
    }

    internal static void Reset()
    {
        State = Phase.Idle;
        RemoteVersion = string.Empty;
        DownloadUrl = string.Empty;
        ExpectedSha256 = string.Empty;
        StagedPath = string.Empty;
        DownloadViaApi = false;
        Error = string.Empty;
        Progress = 0f;
        NeedConfirm = false;
        NeedRestart = false;
    }

    // ===== 第一步：查询最新版本 =====
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

            // 下载地址选择（第 271 轮）：
            //   本机/部分网络里 `github.com` 解析到的 IP 不可达，但 `api.github.com`
            //   与 `objects.githubusercontent.com` 可达。因此优先走 API 资产端点
            //   （GET 带 Accept: application/octet-stream，GitHub 会 302 到 objects 域名），
            //   只有它不可用时才退回 browser_download_url。
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

            // 比较版本：远程 > 本地才算有新版
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

    // ===== 第二步：下载并暂存 =====
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

            // 清掉上一轮的残留，避免把旧包当新包用
            try { if (File.Exists(staged)) File.Delete(staged); } catch { }
        }
        catch
        {
            Fail(AmethystText.T("UpdateDownloadFailed"));
            yield break;
        }

        // ⚠ 第 270 轮（第二次根因修复，换实现路径）：
        //   上次失败日志是 `data: ObjectCollectedException ... progress=1.000`，
        //   说明 `DownloadHandler.data` **属性本身**在 IL2CPP 下就会返回已回收的包装
        //   （`result=Success code=200` 表示数据早已收全）。在它上面做任何读取都不可靠。
        //
        //   因此不再经过 UnityWebRequest / DownloadHandler，改用 .NET 的 HttpClient
        //   直接把响应流写进文件：
        //     · 完全绕开 IL2CPP 包装对象，不存在被 GC 回收的问题；
        //     · 边下边写，内存里只有一个 64 KB 缓冲；
        //     · 进度来自实际写入字节数。
        //   协程只负责在主线程推进度条；真正下载在线程池里。
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
                http.Timeout = System.Threading.Timeout.InfiniteTimeSpan;  // 由外层看门狗负责
                http.DefaultRequestHeaders.Add("User-Agent", "Amethyst-Updater");

                var request = new System.Net.Http.HttpRequestMessage(
                    System.Net.Http.HttpMethod.Get, DownloadUrl);

                if (DownloadViaApi)
                {
                    // API 资产端点必须显式要求二进制，否则返回的是 JSON 元数据；
                    // GitHub 会 302 到 objects.githubusercontent.com 再给出文件。
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

        // 主线程：推进度 + 无进度看门狗
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

                // 总长度到达之前保持 0，绝不提前跳到 99%：
                // 之前写成 `total > 0 ? ratio : 0.99f`，导致刚点下载、total 还是 0 时
                // 进度条立刻显示 99%，用户看到的「一点就 99%」就是这个原因。
                if (total > 0)
                {
                    Progress = Mathf.Clamp((float)nowBytes / total, 0f, 0.99f);
                }
                else if (nowBytes > 0)
                {
                    // 没有 Content-Length：按已下载量给一个缓慢推进的近似值，最高 0.95
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
            // 等后台线程真正退出后再删文件，避免「边写边删」的 IOException
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

        // ===== 校验：必须是有效 .NET 程序集，否则删掉，绝不留坏包 =====
        string reason = Verify(staged);
        if (reason != null)
        {
            try { if (File.Exists(staged)) File.Delete(staged); } catch { }
            Fail(AmethystText.T("UpdateInvalidFile"));
            try { AmethystPlugin.Logger?.LogWarning("[Updater] rejected download: " + reason); } catch { }
            yield break;
        }

        // GitHub API 提供 digest 时做 SHA256 比对（防中间人 / 缓存投毒）
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

    // "sha256:abc..." → "abc..."；没有 digest 时返回空串（跳过比对）
    private static string NormalizeDigest(string digest)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(digest)) return string.Empty;
            string s = digest.Trim();
            int colon = s.IndexOf(':');
            if (colon >= 0) s = s.Substring(colon + 1);
            return s.Trim().ToLowerInvariant();
        }
        catch { return string.Empty; }
    }

    // 没有期望值时视为通过（老版本 API 不返回 digest）
    private static bool DigestMatches(string path, string expected)
    {
        if (string.IsNullOrEmpty(expected)) return true;
        try
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(fs);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                return string.Equals(sb.ToString(), expected, StringComparison.OrdinalIgnoreCase);
            }
        }
        catch { return false; }
    }

    // 返回 null = 通过；否则是拒绝原因
    private static string Verify(string path)
    {
        try
        {
            FileInfo fi = new FileInfo(path);
            if (!fi.Exists) return "missing";
            if (fi.Length < 4096) return "too small: " + fi.Length;   // 正常模组 DLL 远大于此

            // 读 PE 头：MZ + 有效 .NET 程序集（能读出 AssemblyName 说明是托管 DLL）
            AssemblyName an = AssemblyName.GetAssemblyName(path);
            if (an == null || !string.Equals(an.Name, "Amethyst", StringComparison.OrdinalIgnoreCase))
                return "unexpected assembly: " + (an != null ? an.Name : "null");
            return null;
        }
        catch (Exception ex) { return ex.GetType().Name + ": " + ex.Message; }
    }

    // ===== 第三步：生成替换脚本并启动 =====
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

            // 目标必须位于 BepInEx\plugins 内，防路径穿越
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

            // ===== 首选：进程内直接改名替换（参考 FinalSuspect / Elysium 的做法）=====
            //   运行中的 DLL 虽然被映射，但 Windows 允许**改名**（不允许删除/覆盖）。
            //   因此：旧 DLL → .bak，新 DLL → 目标名。两步都是 Move，不碰被锁定的内容。
            //   这比「退出游戏跑批处理」可靠得多，不依赖 cmd.exe、不依赖进程名匹配。
            try
            {
                string backup = target + ".bak";

                if (File.Exists(backup)) File.Delete(backup);
                File.Move(target, backup);          // 旧版改名（被锁也能改名）

                try
                {
                    File.Move(StagedPath, target);  // 新版就位
                }
                catch
                {
                    // 新版放不进去：把旧版改回来，绝不留下一个没有 DLL 的 plugins 目录
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

            // ===== 回退：退出游戏，由脚本在进程结束后替换并重启 =====
            // 批处理里 `%` 会被二次展开，`"` 会破坏引号配对。
            if (HasBatchUnsafe(target) || HasBatchUnsafe(StagedPath) || HasBatchUnsafe(StagingDir))
            {
                Fail(AmethystText.T("UpdateApplyFailed"));
                try { AmethystPlugin.Logger?.LogWarning("[Updater] unsafe path for batch: " + target); } catch { }
                return false;
            }

            string exe = Path.Combine(BepInEx.Paths.GameRootPath, "Among Us.exe");
            string script = Path.Combine(StagingDir, "apply_update.bat");

            // 脚本逻辑：等游戏退出 → 备份 → 替换 → 失败回滚 → 重启游戏
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

            // 让游戏自己退出，脚本会等待进程结束后替换并重启
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

    private static bool HasBatchUnsafe(string s)
    {
        try
        {
            if (string.IsNullOrEmpty(s)) return true;
            return s.IndexOf('%') >= 0 || s.IndexOf('"') >= 0
                || s.IndexOf('\r') >= 0 || s.IndexOf('\n') >= 0;
        }
        catch { return true; }
    }

    internal static void Fail(string reason)
    {
        State = Phase.Failed;
        Error = reason ?? string.Empty;
        NeedConfirm = false;
        if (!string.IsNullOrEmpty(Error))
        {
            AmethystToast.Push(AmethystText.T("CheckUpdate"), Error, 5f, AmethystNotifyKind.Warning);
        }
    }

    // API 资产端点：只允许本仓库的 assets 路径
    private static bool SafeApiAssetUrl(string url)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri)) return false;
            if (uri.Scheme != Uri.UriSchemeHttps) return false;
            if (!string.Equals(uri.Host, "api.github.com", StringComparison.OrdinalIgnoreCase)) return false;
            return uri.AbsolutePath.StartsWith("/repos/CN-SPPLAYER/Amethyst/releases/assets/",
                StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    // 只允许 https，且必须指向本仓库的 release 资产（防伪造 API 响应把用户引到别处）
    private static bool SafeUrl(string url)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri)) return false;
            if (uri.Scheme != Uri.UriSchemeHttps) return false;

            if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)) return false;
            return uri.AbsolutePath.StartsWith("/CN-SPPLAYER/Amethyst/releases/download/",
                StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}
