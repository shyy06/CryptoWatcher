using System.Text.Json;
using CryptoWatcher.Models;

namespace CryptoWatcher.Services
{
    /// <summary>
    /// 监测列表的持久化（System.Text.Json，兼容旧版 Newtonsoft 写出的 config.json）
    /// </summary>
    public static class ConfigStore
    {
        private static readonly JsonSerializerOptions ReadOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        private static readonly JsonSerializerOptions WriteOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public static List<CryptoItem> Load(out string error)
        {
            error = null;
            try
            {
                if (!File.Exists(AppPaths.ConfigFile)) return new List<CryptoItem>();

                string json = File.ReadAllText(AppPaths.ConfigFile);
                if (string.IsNullOrWhiteSpace(json)) return new List<CryptoItem>();

                var list = JsonSerializer.Deserialize<List<CryptoItem>>(json, ReadOptions);
                return list ?? new List<CryptoItem>();
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Debug.WriteLine("[ConfigStore] 加载配置失败: " + ex);
                return new List<CryptoItem>();
            }
        }

        public static bool Save(IEnumerable<CryptoItem> items, out string error)
        {
            error = null;
            try
            {
                AppPaths.EnsureDataDir();
                string json = JsonSerializer.Serialize(items.ToList(), WriteOptions);
                File.WriteAllText(AppPaths.ConfigFile, json);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Debug.WriteLine("[ConfigStore] 保存配置失败: " + ex);
                return false;
            }
        }
    }
}
