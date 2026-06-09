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
        /// 获取加密货币实时价格 — 6 大交易所竞速，取最快响应。
        /// 所有接口均为交易所公开行情 API，无需 API Key，支持 ≥1 次/秒查询。
        /// ① 火币(HTX) ② 币安(Binance) ③ Bybit ④ OKX ⑤ Gate.io ⑥ MEXC(抹茶)
        /// </summary>
        public static async Task<string> GetCurrentPrice(string fsym, string tsyms = "usdt")
        {
            string symbol = $"{fsym}{tsyms}".ToLower();

            // 同时向 6 个交易所发起请求，各 5 秒超时
            var tasks = new List<Task<string>>
            {
                TryHuobi(symbol),
                TryBinance(fsym, tsyms),
                TryBybit(symbol),
                TryOKX(symbol),
                TryGate(fsym, tsyms),
                TryMEXC(symbol),
            };

            // 竞速：取第一个成功返回的结果
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

        // === ① 火币 HTX ===
        // 公开行情 API，无需 Key，频率 ~10 次/秒
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

        // === ② 币安 Binance ===
        // 公开行情 API，无需 Key，频率 ~20 次/秒
        private static async Task<string> TryBinance(string fsym, string tsyms)
        {
            // 优先用 USDT 交易对，失败时换用户指定的计价币
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

            // 回退：用户指定的计价货币
            string symbol2 = $"{fsym}{tsyms}".ToUpper();
            var resp2 = await $"https://api.binance.com/api/v3/ticker/price?symbol={symbol2}"
                .WithTimeout(TimeSpan.FromSeconds(5))
                .GetStringAsync();

            return JObject.Parse(resp2)["price"]?.ToString();
        }

        // === ③ Bybit ===
        // 公开行情 API，无需 Key，频率 ~10 次/秒
        private static async Task<string> TryBybit(string symbol)
        {
            string instId = symbol.ToUpper();
            var resp = await $"https://api.bybit.com/v5/market/tickers?category=spot&symbol={instId}"
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

        // === ④ OKX ===
        // 公开行情 API，无需 Key，频率 ~10 次/秒
        private static async Task<string> TryOKX(string symbol)
        {
            string instId = $"{symbol.ToUpper()}-SPOT";
            var resp = await $"https://www.okx.com/api/v5/market/ticker?instId={instId}"
                .WithTimeout(TimeSpan.FromSeconds(5))
                .GetStringAsync();

            var price = JObject.Parse(resp)["data"]?[0]?["last"]?.ToString();
            if (!string.IsNullOrEmpty(price))
                return price;

            throw new Exception("OKX 无数据");
        }

        // === ⑤ Gate.io ===
        // 公开行情 API，无需 Key。国内网络可直接访问。
        private static async Task<string> TryGate(string fsym, string tsyms)
        {
            string pair = $"{fsym}_{tsyms}".ToUpper();
            var resp = await $"https://api.gateio.ws/api/v4/spot/tickers?currency_pair={pair}"
                .WithTimeout(TimeSpan.FromSeconds(5))
                .GetStringAsync();

            var arr = JArray.Parse(resp);
            if (arr.Count > 0)
            {
                var price = arr[0]["last"]?.ToString();
                if (!string.IsNullOrEmpty(price) && price != "0")
                    return price;
            }

            throw new Exception("Gate.io 无数据");
        }

        // === ⑥ MEXC（抹茶）===
        // 公开行情 API，无需 Key。国内网络可直接访问。
        private static async Task<string> TryMEXC(string symbol)
        {
            string instId = symbol.ToUpper();
            var resp = await $"https://api.mexc.com/api/v3/ticker/price?symbol={instId}"
                .WithTimeout(TimeSpan.FromSeconds(5))
                .GetStringAsync();

            var price = JObject.Parse(resp)["price"]?.ToString();
            if (!string.IsNullOrEmpty(price) && price != "0")
                return price;

            throw new Exception("MEXC 无数据");
        }

        /// <summary>
        /// 从 CoinCap API 获取市值 Top 20 币种列表（用于下拉框）
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
