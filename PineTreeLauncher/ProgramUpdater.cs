#if RELEASE
using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Xml.Linq;
using MySql.Data.MySqlClient;

namespace PineTreeLauncher
{
    internal static class ProgramUpdater
    {
        internal static void UpdateIfNeeded()
        {
            UpdateInfo update = GetLatestUpdate();
            if (update == null) return;
            ApplyUpdate(update);
        }

        private static void ApplyUpdate(UpdateInfo update)
        {
            Version latest;
            if (!Version.TryParse(update.Version, out latest))
                throw new InvalidOperationException("DB에 등록된 최신 버전 형식이 올바르지 않습니다.");

            string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PineTree.dll");
            Version local = ReadVersion(dllPath);
            if (local != null && local >= latest) return;
            if (local != null && !update.Required && MessageBox.Show(
                "새 버전 " + latest + "을 다운로드하시겠습니까?", "PineTree Update",
                MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes)
                return;

            string temporary = dllPath + ".download";
            try
            {
                using (var client = new WebClient())
                    client.DownloadFile(update.Url, temporary);
                ValidateDownload(temporary, latest, update.Hash);
                if (File.Exists(dllPath))
                    File.Replace(temporary, dllPath, dllPath + ".bak");
                else
                    File.Move(temporary, dllPath);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private static Version ReadVersion(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                AssemblyName name = AssemblyName.GetAssemblyName(path);
                return name.Name == "PineTree" ? name.Version : null;
            }
            catch (BadImageFormatException) { return null; }
            catch (FileLoadException) { return null; }
        }

        private static void ValidateDownload(string path, Version expectedVersion, string expectedHash)
        {
            Version version = ReadVersion(path);
            if (version == null || version != expectedVersion)
                throw new InvalidOperationException("다운로드한 파일이 DB에 등록된 버전의 PineTree DLL이 아닙니다.");
            if (string.IsNullOrWhiteSpace(expectedHash)) return;
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                string expected = expectedHash.Trim().Replace("-", "").Replace(" ", "");
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("다운로드한 파일의 SHA-256 해시가 일치하지 않습니다.");
            }
        }

        private static UpdateInfo GetLatestUpdate()
        {
            using (var connection = new MySqlConnection(GetConnectionString()))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT LatestVersion, DownloadUrl, FileHash, IsRequired
                    FROM ProgramUpdate WHERE ProgramName = @ProgramName
                    ORDER BY UpdatedAt DESC, SeqNo DESC LIMIT 1";
                command.Parameters.AddWithValue("@ProgramName", "PineTree");
                command.CommandTimeout = 15;
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new UpdateInfo
                    {
                        Version = Convert.ToString(reader["LatestVersion"]).Trim(),
                        Url = Convert.ToString(reader["DownloadUrl"]).Trim(),
                        Hash = Convert.ToString(reader["FileHash"]),
                        Required = Convert.ToInt32(reader["IsRequired"]) == 1
                    };
                }
            }
        }

        private static string GetConnectionString()
        {
            // 외부 설정이 있으면 우선 사용하고, EXE만 배포되면 내장 설정을 사용합니다.
            var setting = ConfigurationManager.ConnectionStrings["PineTreeUpdate"];
            if (setting != null && !string.IsNullOrWhiteSpace(setting.ConnectionString))
                return setting.ConnectionString;

            using (var stream = typeof(ProgramUpdater).Assembly
                .GetManifestResourceStream("PineTreeLauncher.Update.config"))
            {
                if (stream != null)
                {
                    var connections = XDocument.Load(stream).Root.Element("connectionStrings");
                    if (connections != null)
                    {
                        foreach (var entry in connections.Elements("add"))
                        {
                            if ((string)entry.Attribute("name") != "PineTreeUpdate") continue;
                            string value = (string)entry.Attribute("connectionString");
                            if (!string.IsNullOrWhiteSpace(value)) return value;
                        }
                    }
                }
            }
            throw new ConfigurationErrorsException("PineTreeUpdate DB 접속 설정이 없습니다.");
        }

        private sealed class UpdateInfo
        {
            internal string Version;
            internal string Url;
            internal string Hash;
            internal bool Required;
        }
    }
}
#endif
