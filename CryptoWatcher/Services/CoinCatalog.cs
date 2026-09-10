using System.Text.Json;

namespace CryptoWatcher.Services
{
    /// <summary>
    /// 热门币种目录：优先用本地缓存，窗口显示后后台刷新并覆写缓存
    /// </summary>
    public static class CoinCatalog
    {
        /// <summary>离线兜底列表（网络不可用时使用）</summary>
        public static readonly string[] Fallback = new string[]
        {
            "BTC", "ETH", "USDT", "BNB", "SOL",
            "XRP", "USDC", "DOGE", "ADA", "TRX",
            "TON", "AVAX", "LINK", "SHIB", "SUI",
            "DOT", "BCH", "LTC", "NEAR", "UNI"
        };

        public static string[] LoadCache()
        {
            try
            {
                if (File.Exists(AppPaths.CoinCacheFile))
                {
                    string json = File.ReadAllText(AppPaths.CoinCacheFile);
                    var arr = JsonSerializer.Deserialize<string[]>(json);
                    if (arr != null && arr.Length > 0) return arr;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CoinCatalog] 读取缓存失败: " + ex.Message);
            }
            return Fallback;
        }

        public static void SaveCache(string[] coins)
        {
            try
            {
                if (coins == null || coins.Length == 0) return;
                AppPaths.EnsureDataDir();
                File.WriteAllText(AppPaths.CoinCacheFile, JsonSerializer.Serialize(coins));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CoinCatalog] 写入缓存失败: " + ex.Message);
            }
        }

        /// <summary>联网获取市值 Top N，成功则写入缓存；失败返回 null</summary>
        public static async Task<string[]> FetchTopAsync(int limit, CancellationToken ct)
        {
            try
            {
                var list = await WebApis.GetTopCoinsAsync(limit, ct).ConfigureAwait(false);
                if (list != null && list.Count > 0)
                {
                    var arr = list.ToArray();
                    SaveCache(arr);
                    return arr;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CoinCatalog] 联网获取热门币种失败: " + ex.Message);
            }
            return null;
        }
    }
}
