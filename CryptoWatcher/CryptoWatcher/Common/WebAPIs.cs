using Flurl.Http;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CryptoWatcher.Common
{
    static class WebAPIs
    {
        /// <summary>
        /// 获取加密货币实时价格 — 6 大 API 竞速，取最快响应。
        /// ① 火币(HTX)  ② 币安(Binance)  ③ Bybit  ④ OKX
        /// ⑤ Gate.io     ⑥ KuCoin
        /// 全部为交易所公开行情 API，无需 Key，各 5 秒超时，Task.WhenAny 竞速。
        /// ①⑤⑥ 国内可直接访问，②③④ 海外可访问。
        /// </summary>
        public static async Task<string> GetCurrentPrice(string fsym, string tsyms = "usdt")
        {
            string symbol = $"{fsym}{tsyms}".ToLower();

            var tasks = new List<Task<string>>
            {
                TryHuobi(symbol),        // ① 国内直连
                TryBinance(fsym, tsyms), // ② 海外
                TryBybit(symbol),        // ③ 海外
                TryOKX(symbol),          // ④ 海外
                TryGateIO(fsym, tsyms),  // ⑤ 国内直连
                TryKuCoin(fsym, tsyms),  // ⑥ 国内直连
            };

            // 竞速取第一个成功返回的结果
            while (tasks.Count > 0)
            {
                var finished = await Task.WhenAny(tasks);
                tasks.Remove(finished);

                string price = null;
                try { price = await finished; }
                catch { }

                if (!string.IsNullOrEmpty(price))
                    return price;
            }

            throw new Exception($"无法获取 {symbol} 的实时价格，请检查网络连接或币种名称是否正确");
        }

        // ═══════════════════════════════════════════
        // ① 火币 HTX — 国内直连，~10 次/秒
        // ═══════════════════════════════════════════
        private static async Task<string> TryHuobi(string symbol)
        {
            var resp = await $"https://api.huobi.pro/market/detail/merged?symbol={symbol}"
                .WithTimeout(TimeSpan.FromSeconds(5))
                .GetStringAsync();

            var j = JObject.Parse(resp);
            if (j["status"]?.ToString() == "ok" && j["tick"] != null)
                return j["tick"]["close"]?.ToString();

            throw new Exception("火币无数据");
        }

        // ═══════════════════════════════════════════
        // ② 币安 Binance — 海外，~20 次/秒
        // ═══════════════════════════════════════════
        private static async Task<string> TryBinance(string fsym, string tsyms)
        {
            string symbol1 = $"{fsym}USDT".ToUpper();
            try
            {
                var resp = await $"https://api.binance.com/api/v3/ticker/price?symbol={symbol1}"
                    .WithTimeout(TimeSpan.FromSeconds(5))
                    .GetStringAsync();

                var price = JObject.Parse(resp)["price"]?.ToString();
                if (!string.IsNullOrEmpty(price)) return price;
            }
            catch { }

            string symbol2 = $"{fsym}{tsyms}".ToUpper();
            var resp2 = await $"https://api.binance.com/api/v3/ticker/price?symbol={symbol2}"
                .WithTimeout(TimeSpan.FromSeconds(5))
                .GetStringAsync();

            return JObject.Parse(resp2)["price"]?.ToString();
        }

        // ═══════════════════════════════════════════
        // ③ Bybit — 海外，~10 次/秒
        // ═══════════════════════════════════════════
        private static async Task<string> TryBybit(string symbol)
        {
            var resp = await $"https://api.bybit.com/v5/market/tickers?category=spot&symbol={symbol.ToUpper()}"
                .WithTimeout(TimeSpan.FromSeconds(5))
                .GetStringAsync();

            var result = JObject.Parse(resp)["result"]?["list"] as JArray;
            if (result != null && result.Count > 0)
            {
                var price = result[0]["lastPrice"]?.ToString();
                if (!string.IsNullOrEmpty(price) && price != "0")
                    return price;
            }

            throw new Exception("Bybit 无数据");
        }

        // ═══════════════════════════════════════════
        // ④ OKX — 海外，~10 次/秒
        // ═══════════════════════════════════════════
        private static async Task<string> TryOKX(string symbol)
        {
            var resp = await $"https://www.okx.com/api/v5/market/ticker?instId={symbol.ToUpper()}-SPOT"
                .WithTimeout(TimeSpan.FromSeconds(5))
                .GetStringAsync();

            var price = JObject.Parse(resp)["data"]?[0]?["last"]?.ToString();
            if (!string.IsNullOrEmpty(price))
                return price;

            throw new Exception("OKX 无数据");
        }

        // ═══════════════════════════════════════════
        // ⑤ Gate.io — 国内直连，~200 次/秒
        // ═══════════════════════════════════════════
        private static async Task<string> TryGateIO(string fsym, string tsyms)
        {
            string pair = $"{fsym}_{tsyms}".ToUpper();
            // 先尝试精确交易对
            try
            {
                var resp = await $"https://api.gateio.ws/api/v4/spot/tickers?currency_pair={pair}"
                    .WithTimeout(TimeSpan.FromSeconds(5))
                    .GetStringAsync();

                var data = JArray.Parse(resp);
                if (data.Count > 0)
                {
                    var price = data[0]["last"]?.ToString();
                    if (!string.IsNullOrEmpty(price) && price != "0")
                        return price;
                }
            }
            catch { }

            // 回退到 USDT
            string pair2 = $"{fsym}_USDT".ToUpper();
            var resp2 = await $"https://api.gateio.ws/api/v4/spot/tickers?currency_pair={pair2}"
                .WithTimeout(TimeSpan.FromSeconds(5))
                .GetStringAsync();

            var data2 = JArray.Parse(resp2);
            return data2[0]["last"]?.ToString();
        }

        // ═══════════════════════════════════════════
        // ⑥ KuCoin — 国内直连，~30 次/秒
        // ═══════════════════════════════════════════
        private static async Task<string> TryKuCoin(string fsym, string tsyms)
        {
            string symbol1 = $"{fsym}-USDT".ToUpper();
            try
            {
                var resp = await $"https://api.kucoin.com/api/v1/market/orderbook/level1?symbol={symbol1}"
                    .WithTimeout(TimeSpan.FromSeconds(5))
                    .GetStringAsync();

                var j = JObject.Parse(resp);
                if (j["code"]?.ToString() == "200000")
                {
                    var price = j["data"]?["price"]?.ToString();
                    if (!string.IsNullOrEmpty(price))
                        return price;
                }
            }
            catch { }

            // 回退到用户指定的计价币
            string symbol2 = $"{fsym}-{tsyms}".ToUpper();
            var resp2 = await $"https://api.kucoin.com/api/v1/market/orderbook/level1?symbol={symbol2}"
                .WithTimeout(TimeSpan.FromSeconds(5))
                .GetStringAsync();

            var j2 = JObject.Parse(resp2);
            return j2["data"]?["price"]?.ToString();
        }

        /// <summary>
        /// 从 CoinCap API 获取市值 Top 20 币种列表（供下拉框缓存使用）
        /// </summary>
        public static async Task<string[]> GetTop20CoinsAsync()
        {
            try
            {
                var resp = await "https://api.coincap.io/v2/assets"
                    .WithTimeout(TimeSpan.FromSeconds(5))
                    .SetQueryParam("limit", 20)
                    .GetStringAsync();

                var data = JObject.Parse(resp)["data"] as JArray;
                if (data != null && data.Count > 0)
                {
                    var symbols = new List<string>();
                    foreach (var item in data)
                    {
                        string symbol = item["symbol"]?.ToString();
                        if (!string.IsNullOrEmpty(symbol))
                            symbols.Add(symbol.ToUpper());
                    }
                    return symbols.ToArray();
                }
            }
            catch { }

            return null;
        }
    }
}
