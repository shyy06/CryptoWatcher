using System.Text.Json;

namespace CryptoWatcher.Services
{
    /// <summary>一条行情结果：价格 + 数据来源 + 耗时</summary>
    public sealed record QuoteResult(decimal Price, string Source, long ElapsedMs);

    /// <summary>
    /// 行情接口层：6 家交易所同时竞速，取最快返回的有效价格，并取消其余请求。
    /// 全部为公开行情接口，无需 Key，均支持 ≥1 次/秒 查询。
    /// 零第三方依赖，仅使用 .NET 内置 HttpClient + System.Text.Json。
    /// </summary>
    public static class WebApis
    {
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(6)
        };

        /// <summary>
        /// 6 路竞速获取现价。fsym=币种代码（BTC），tsyms=计价币种（usdt）
        /// </summary>
        public static async Task<QuoteResult> GetPriceAsync(string fsym, string tsyms, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(fsym))
                throw new ArgumentException("币种代码不能为空", nameof(fsym));

            string fs = fsym.Trim().ToUpperInvariant();
            string ts = string.IsNullOrWhiteSpace(tsyms) ? "USDT" : tsyms.Trim().ToUpperInvariant();

            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                var pending = new List<Task<QuoteResult>>
                {
                    Race("火币 HTX", () => HuobiAsync(fs, ts, cts.Token)),
                    Race("币安",     () => BinanceAsync(fs, ts, cts.Token)),
                    Race("Bybit",   () => BybitAsync(fs, ts, cts.Token)),
                    Race("OKX",     () => OkxAsync(fs, ts, cts.Token)),
                    Race("Gate.io", () => GateAsync(fs, ts, cts.Token)),
                    Race("KuCoin",  () => KuCoinAsync(fs, ts, cts.Token))
                };

                Exception lastError = null;

                while (pending.Count > 0)
                {
                    Task<QuoteResult> done = await Task.WhenAny(pending).ConfigureAwait(false);
                    pending.Remove(done);

                    try
                    {
                        QuoteResult result = await done.ConfigureAwait(false);
                        if (result != null && result.Price > 0m)
                        {
                            // 已拿到有效价格，取消其余仍在进行的请求
                            cts.Cancel();
                            return result;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // 被取消，忽略
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                    }
                }

                string detail = lastError != null ? "（" + lastError.Message + "）" : "";
                throw new InvalidOperationException(
                    "无法获取 " + fs + "/" + ts + " 的实时价格，请检查网络或币种代码是否正确" + detail);
            }
        }

        /// <summary>CoinCap 市值 Top N 币种符号</summary>
        public static async Task<List<string>> GetTopCoinsAsync(int limit, CancellationToken ct)
        {
            string url = "https://api.coincap.io/v2/assets?limit=" + limit.ToString(CultureInfo.InvariantCulture);

            using (var doc = JsonDocument.Parse(await Http.GetStringAsync(url, ct).ConfigureAwait(false)))
            {
                var result = new List<string>();
                JsonElement data;
                if (doc.RootElement.TryGetProperty("data", out data) && data.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in data.EnumerateArray())
                    {
                        JsonElement symbol;
                        if (item.TryGetProperty("symbol", out symbol) && symbol.ValueKind == JsonValueKind.String)
                        {
                            string s = symbol.GetString();
                            if (!string.IsNullOrWhiteSpace(s)) result.Add(s.ToUpperInvariant());
                        }
                    }
                }
                return result;
            }
        }

        // ================= 内部实现 =================

        private static async Task<QuoteResult> Race(string source, Func<Task<decimal>> fetch)
        {
            var sw = Stopwatch.StartNew();
            decimal price = await fetch().ConfigureAwait(false);
            sw.Stop();
            if (price <= 0m) throw new InvalidOperationException(source + " 返回无效价格");
            return new QuoteResult(price, source, sw.ElapsedMilliseconds);
        }

        /// <summary>火币 HTX：tick.close（数字）</summary>
        private static async Task<decimal> HuobiAsync(string fs, string ts, CancellationToken ct)
        {
            string url = "https://api.huobi.pro/market/detail/merged?symbol="
                         + fs.ToLowerInvariant() + ts.ToLowerInvariant();

            using (var doc = JsonDocument.Parse(await Http.GetStringAsync(url, ct).ConfigureAwait(false)))
            {
                JsonElement root = doc.RootElement;
                JsonElement status;
                if (root.TryGetProperty("status", out status)
                    && status.ValueKind == JsonValueKind.String
                    && string.Equals(status.GetString(), "ok", StringComparison.OrdinalIgnoreCase))
                {
                    JsonElement tick;
                    if (root.TryGetProperty("tick", out tick) && tick.ValueKind == JsonValueKind.Object)
                    {
                        JsonElement close;
                        if (tick.TryGetProperty("close", out close)) return ReadDecimal(close);
                    }
                }
            }
            throw new InvalidOperationException("火币返回数据异常");
        }

        /// <summary>币安：price（字符串数字），symbol 形如 BTCUSDT</summary>
        private static async Task<decimal> BinanceAsync(string fs, string ts, CancellationToken ct)
        {
            string url = "https://api.binance.com/api/v3/ticker/price?symbol=" + fs + ts;

            using (var doc = JsonDocument.Parse(await Http.GetStringAsync(url, ct).ConfigureAwait(false)))
            {
                JsonElement price;
                if (doc.RootElement.TryGetProperty("price", out price)) return ReadDecimal(price);
            }
            throw new InvalidOperationException("币安返回数据异常");
        }

        /// <summary>Bybit：result.list[0].lastPrice</summary>
        private static async Task<decimal> BybitAsync(string fs, string ts, CancellationToken ct)
        {
            string url = "https://api.bybit.com/v5/market/tickers?category=spot&symbol=" + fs + ts;

            using (var doc = JsonDocument.Parse(await Http.GetStringAsync(url, ct).ConfigureAwait(false)))
            {
                JsonElement result;
                if (doc.RootElement.TryGetProperty("result", out result) && result.ValueKind == JsonValueKind.Object)
                {
                    JsonElement list;
                    if (result.TryGetProperty("list", out list)
                        && list.ValueKind == JsonValueKind.Array
                        && list.GetArrayLength() > 0)
                    {
                        JsonElement last;
                        if (list[0].TryGetProperty("lastPrice", out last)) return ReadDecimal(last);
                    }
                }
            }
            throw new InvalidOperationException("Bybit 返回数据异常");
        }

        /// <summary>OKX：data[0].last，instId 形如 BTC-USDT</summary>
        private static async Task<decimal> OkxAsync(string fs, string ts, CancellationToken ct)
        {
            string url = "https://www.okx.com/api/v5/market/ticker?instId=" + fs + "-" + ts;

            using (var doc = JsonDocument.Parse(await Http.GetStringAsync(url, ct).ConfigureAwait(false)))
            {
                JsonElement data;
                if (doc.RootElement.TryGetProperty("data", out data)
                    && data.ValueKind == JsonValueKind.Array
                    && data.GetArrayLength() > 0)
                {
                    JsonElement last;
                    if (data[0].TryGetProperty("last", out last)) return ReadDecimal(last);
                }
            }
            throw new InvalidOperationException("OKX 返回数据异常");
        }

        /// <summary>Gate.io：返回数组，[0].last，currency_pair 形如 BTC_USDT</summary>
        private static async Task<decimal> GateAsync(string fs, string ts, CancellationToken ct)
        {
            string url = "https://api.gateio.ws/api/v4/spot/tickers?currency_pair=" + fs + "_" + ts;

            using (var doc = JsonDocument.Parse(await Http.GetStringAsync(url, ct).ConfigureAwait(false)))
            {
                JsonElement root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    JsonElement last;
                    if (root[0].TryGetProperty("last", out last)) return ReadDecimal(last);
                }
            }
            throw new InvalidOperationException("Gate.io 返回数据异常");
        }

        /// <summary>KuCoin：code == 200000 时取 data.price，symbol 形如 BTC-USDT</summary>
        private static async Task<decimal> KuCoinAsync(string fs, string ts, CancellationToken ct)
        {
            string url = "https://api.kucoin.com/api/v1/market/orderbook/level1?symbol=" + fs + "-" + ts;

            using (var doc = JsonDocument.Parse(await Http.GetStringAsync(url, ct).ConfigureAwait(false)))
            {
                JsonElement root = doc.RootElement;
                JsonElement code;
                if (root.TryGetProperty("code", out code) && code.ValueKind == JsonValueKind.String
                    && code.GetString() == "200000")
                {
                    JsonElement data;
                    if (root.TryGetProperty("data", out data) && data.ValueKind == JsonValueKind.Object)
                    {
                        JsonElement price;
                        if (data.TryGetProperty("price", out price)) return ReadDecimal(price);
                    }
                }
            }
            throw new InvalidOperationException("KuCoin 返回数据异常");
        }

        /// <summary>兼容 JSON 数字与字符串两种价格格式</summary>
        private static decimal ReadDecimal(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number) return element.GetDecimal();

            if (element.ValueKind == JsonValueKind.String)
            {
                string text = element.GetString();
                decimal value;
                if (!string.IsNullOrWhiteSpace(text)
                    && decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    return value;
                }
            }

            throw new InvalidOperationException("价格字段格式无法解析");
        }
    }
}
