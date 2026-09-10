namespace CryptoWatcher.Services
{
    /// <summary>
    /// 数据文件路径。与旧版保持一致（%AppData%\CryptoWatcher\），
    /// 因此旧版 WinForms 版本的监测列表可直接被新版读取。
    /// </summary>
    public static class AppPaths
    {
        public static string DataDir { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CryptoWatcher");

        public static string ConfigFile => Path.Combine(DataDir, "config.json");

        public static string CoinCacheFile => Path.Combine(DataDir, "topcoins.json");

        public static string ErrorLogFile => Path.Combine(DataDir, "error.log");

        public static void EnsureDataDir()
        {
            try
            {
                if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[AppPaths] 创建数据目录失败: " + ex.Message);
            }
        }
    }
}
