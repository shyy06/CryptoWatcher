using CryptoWatcher.Models;

namespace CryptoWatcher.Services
{
    /// <summary>
    /// 价格监控：每个监测项一个独立轮询循环，价格更新、提醒判定、托盘通知。
    /// 所有界面状态更新都通过 Dispatcher 回到 UI 线程执行。
    /// </summary>
    public sealed class PriceMonitor : IDisposable
    {
        private readonly Dictionary<string, CancellationTokenSource> _loops =
            new Dictionary<string, CancellationTokenSource>(StringComparer.OrdinalIgnoreCase);

        private readonly Dispatcher _dispatcher;
        private readonly System.Windows.Forms.NotifyIcon _tray;
        private bool _disposed;

        /// <summary>状态栏消息（来源 + 耗时），在主线程触发</summary>
        public event Action<string> StatusChanged;

        public PriceMonitor(Dispatcher dispatcher, System.Windows.Forms.NotifyIcon tray)
        {
            _dispatcher = dispatcher;
            _tray = tray;
        }

        public void Start(CryptoItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.CybermoneyName)) return;

            Stop(item.Key);

            var cts = new CancellationTokenSource();
            _loops[item.Key] = cts;
            _ = RunAsync(item, cts.Token);
        }

        public void Stop(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            CancellationTokenSource cts;
            if (_loops.TryGetValue(key, out cts))
            {
                try { cts.Cancel(); } catch (Exception ex) { Debug.WriteLine("[PriceMonitor] 取消失败: " + ex.Message); }
                try { cts.Dispose(); } catch (Exception ex) { Debug.WriteLine("[PriceMonitor] 释放失败: " + ex.Message); }
                _loops.Remove(key);
            }
        }

        public void StopAll()
        {
            foreach (string key in _loops.Keys.ToList()) Stop(key);
            _loops.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            StopAll();
        }

        private async Task RunAsync(CryptoItem item, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                SetStatus(item, "等待...");

                try
                {
                    await Task.Delay(Math.Max(item.RefreshInterval, 500), ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (ct.IsCancellationRequested) break;

                SetStatus(item, "获取中...");

                try
                {
                    QuoteResult quote = await WebApis
                        .GetPriceAsync(item.CybermoneyName, item.CurrencyName, ct)
                        .ConfigureAwait(false);

                    OnUi(() => item.Price = quote.Price);
                    CheckAlerts(item);
                    SetStatus(item, quote.Source + " · " + quote.ElapsedMs.ToString(CultureInfo.InvariantCulture) + "ms");
                    RaiseStatus(item.Key + "   ←   " + quote.Source + " "
                                + quote.ElapsedMs.ToString(CultureInfo.InvariantCulture) + "ms");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    SetStatus(item, "获取失败");
                    RaiseStatus(item.Key + "   获取失败：" + ex.Message);
                    Debug.WriteLine("[PriceMonitor] " + item.Key + " 请求失败: " + ex);
                }
            }
        }

        /// <summary>提醒判定（与旧版逻辑一致，等于判定使用容差避免浮点精确比较失效）</summary>
        private void CheckAlerts(CryptoItem item)
        {
            var alerts = item.Alerts;
            if (alerts == null || alerts.Count == 0) return;

            decimal price = item.Price;

            // 大于提醒：从最高阈值往下找第一个满足的
            foreach (Alert juger in alerts.Where(a => a.Type == JugerType.Greater).OrderByDescending(a => a.PricePoint))
            {
                if (price > juger.PricePoint)
                {
                    if (!juger.Trigged)
                    {
                        Notify("上涨提醒", item.Key + " 目前价格为: " + price.ToString(CultureInfo.InvariantCulture));
                        juger.Trigged = true;
                    }
                    break;
                }
                juger.Trigged = false;
            }

            // 小于提醒：从最低阈值往上找第一个满足的
            foreach (Alert juger in alerts.Where(a => a.Type == JugerType.Less).OrderBy(a => a.PricePoint))
            {
                if (price < juger.PricePoint)
                {
                    if (!juger.Trigged)
                    {
                        Notify("下跌提醒", item.Key + " 目前价格为: " + price.ToString(CultureInfo.InvariantCulture));
                        juger.Trigged = true;
                    }
                    break;
                }
                juger.Trigged = false;
            }

            // 等于提醒：容差比较（阈值万分之一，最小 1e-8）
            foreach (Alert juger in alerts.Where(a => a.Type == JugerType.Equal))
            {
                decimal tolerance = Math.Max(juger.PricePoint * 0.0001m, 0.00000001m);
                if (Math.Abs(price - juger.PricePoint) <= tolerance)
                {
                    if (!juger.Trigged)
                    {
                        Notify("到达设定值", item.Key + " 目前价格为: " + price.ToString(CultureInfo.InvariantCulture));
                        juger.Trigged = true;
                    }
                }
                else
                {
                    juger.Trigged = false;
                }
            }
        }

        private void Notify(string title, string text)
        {
            try
            {
                if (_tray == null) return;
                _tray.BalloonTipTitle = title;
                _tray.BalloonTipText = text;
                _tray.ShowBalloonTip(3000);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[PriceMonitor] 托盘通知失败: " + ex.Message);
            }
        }

        private void SetStatus(CryptoItem item, string text)
        {
            OnUi(() => item.Status = text);
        }

        private void RaiseStatus(string text)
        {
            var handler = StatusChanged;
            if (handler == null) return;
            OnUi(() => handler(text));
        }

        /// <summary>确保回调在 UI 线程执行</summary>
        private void OnUi(Action action)
        {
            try
            {
                if (_dispatcher == null || _dispatcher.CheckAccess()) action();
                else _dispatcher.Invoke(action);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[PriceMonitor] UI 更新失败: " + ex.Message);
            }
        }
    }
}
