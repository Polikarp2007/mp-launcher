using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PoliCoLauncherApp.Models;
using Renci.SshNet;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PoliCoLauncherApp.Services
{
    public static class LoginService
    {
        private const string Host = "116.203.229.254";
        private const int Port = 22;
        private const string SftpUser = "Launcher_Login";
        private const string SftpPass = "2007";
        private const string KeysRemotePath = "/PC-MP Login Keys/";

        private static string CachePath =>
            Path.Combine(AppContext.BaseDirectory, "MyUIK");

        // ── Public API ──────────────────────────────────────────────────────

        public static bool HasCache() => File.Exists(CachePath);

        public static void ClearCache()
        {
            if (File.Exists(CachePath))
                File.Delete(CachePath);
        }

        /// <summary>Called when user types a new key in the launcher.</summary>
        public static async Task<LoginResult> ValidateAndBindKeyAsync(string key)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var sftp = ConnectSftp();
                    string path = $"{KeysRemotePath}{key}.json";

                    if (!sftp.Exists(path))
                        return LoginResult.Fail("Key not found. Check the key and try again.");

                    string json = DownloadText(sftp, path);
                    var jobj = JObject.Parse(json);

                    string status = jobj["Status"]?.ToString() ?? "";
                    if (status == "Acounted")
                        return LoginResult.Fail("This key is already bound to another PC.");
                    if (status != "Active")
                        return LoginResult.Fail("This key is inactive or has been revoked.");

                    var session = ExtractSession(jobj, key);

                    // Build hardware fingerprint
                    string pcName = Environment.MachineName;
                    string cpuId = GetCpuId();
                    string diskSerial = GetDiskSerial();
                    string hwid = ComputeHwid(pcName, cpuId, diskSerial);

                    jobj["Status"] = "Acounted";
                    jobj["Hardware"] = JObject.FromObject(new
                    {
                        PC_Name = pcName,
                        CPU_ID = cpuId,
                        Disk_Serial = diskSerial,
                        HWID_Full = hwid
                    });

                    UploadText(sftp, path, jobj.ToString(Formatting.Indented));
                    sftp.Disconnect();

                    return LoginResult.Ok(session);
                }
                catch (Exception ex)
                {
                    return LoginResult.Fail($"Connection error: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Called on startup when a stored key exists.
        /// Connects to the server, verifies HWID, and returns fresh user data.
        /// If HWID doesn't match → access denied (key used on a different PC).
        /// </summary>
        public static async Task<LoginResult> LoginWithCacheAsync()
        {
            if (!File.Exists(CachePath))
                return LoginResult.Fail("No stored key.");

            string key;
            try
            {
                string raw = (await File.ReadAllTextAsync(CachePath)).Trim();
                // Backward compat: old format was JSON with a "Key" field
                key = raw.StartsWith("{")
                    ? JObject.Parse(raw)["Key"]?.ToString()?.Trim() ?? ""
                    : raw;

                if (string.IsNullOrWhiteSpace(key))
                    return LoginResult.Fail("Stored key is empty.");
            }
            catch
            {
                return LoginResult.Fail("Failed to read stored key.");
            }

            return await Task.Run(() =>
            {
                try
                {
                    using var sftp = ConnectSftp();
                    string path = $"{KeysRemotePath}{key}.json";

                    if (!sftp.Exists(path))
                        return LoginResult.Fail("Key not found on server.");

                    var jobj = JObject.Parse(DownloadText(sftp, path));

                    string status = jobj["Status"]?.ToString() ?? "";
                    if (status != "Acounted")
                        return LoginResult.Fail("Key is not registered.");

                    // HWID check — compare current machine against what was stored at first launch
                    string storedHwid = jobj["Hardware"]?["HWID_Full"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(storedHwid))
                    {
                        string currentHwid = ComputeHwid(
                            Environment.MachineName,
                            GetCpuId(),
                            GetDiskSerial()
                        );
                        if (storedHwid != currentHwid)
                            return LoginResult.Fail("Access denied: this key is registered on a different PC.");
                    }

                    sftp.Disconnect();
                    return LoginResult.Ok(ExtractSession(jobj, key));
                }
                catch (Exception ex)
                {
                    return LoginResult.Fail($"Connection error: {ex.Message}");
                }
            });
        }

        /// <summary>Saves only the key locally — user data is always fetched fresh from the server.</summary>
        public static void SaveCache(UserSession session)
        {
            File.WriteAllText(CachePath, session.Key);
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private static SftpClient ConnectSftp()
        {
            var conn = new ConnectionInfo(Host, Port, SftpUser,
                new PasswordAuthenticationMethod(SftpUser, SftpPass));
            var client = new SftpClient(conn);
            client.Connect();
            return client;
        }

        private static string DownloadText(SftpClient sftp, string remotePath)
        {
            using var ms = new MemoryStream();
            sftp.DownloadFile(remotePath, ms);
            // Strip UTF-8 BOM if present
            return Encoding.UTF8.GetString(ms.ToArray()).TrimStart('\uFEFF');
        }

        private static void UploadText(SftpClient sftp, string remotePath, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            using var ms = new MemoryStream(bytes);
            sftp.UploadFile(ms, remotePath, true);
        }

        private static UserSession ExtractSession(JObject jobj, string key) => new()
        {
            Key = key,
            Name      = Field(jobj, "Name", "name", "FirstName", "First Name", "firstName", "first_name", "Firstname"),
            LastName  = Field(jobj, "Last Name", "LastName", "Surname", "last_name", "lastName", "lastname", "Lastname", "Last_Name"),
            SteamURL  = Field(jobj, "SteamURL", "SteamUrl", "steamURL", "steam_url", "Steam_URL", "SteamLink")
        };

        private static string Field(JObject jobj, params string[] names)
        {
            foreach (var n in names)
            {
                var v = jobj[n]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(v)) return v;
            }
            return "";
        }

        private static string GetCpuId()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    string output = RunWmic("cpu get ProcessorId /value");
                    var m = Regex.Match(output, @"ProcessorId=(.+)");
                    return m.Success ? m.Groups[1].Value.Trim() : "UNKNOWN";
                }
            }
            catch { }
            return "UNKNOWN";
        }

        private static string GetDiskSerial()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    string output = RunWmic("diskdrive get SerialNumber /value");
                    var m = Regex.Match(output, @"SerialNumber=(.+)");
                    return m.Success ? m.Groups[1].Value.Trim() : "UNKNOWN";
                }
            }
            catch { }
            return "UNKNOWN";
        }

        private static string ComputeHwid(string pcName, string cpuId, string diskSerial)
        {
            string raw = $"{pcName}|{cpuId}|{diskSerial}";
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        }

        private static string RunWmic(string args)
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo("wmic", args)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            string result = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            return result;
        }
    }
}
