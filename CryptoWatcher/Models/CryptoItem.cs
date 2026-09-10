using System.Text.Json.Serialization;

namespace CryptoWatcher.Models
{
    /// <summary>
    /// 一个监测项。属性名与旧版 config.json 保持一致（CybermoneyName / CurrencyName /
    /// RefreshInterval / Price / Alerts），因此旧版配置文件可直接被新版读取。
    /// 界面状态类属性全部标记 JsonIgnore，不写入配置文件。
    /// </summary>
    public class CryptoItem : INotifyPropertyChanged
    {
        private decimal _price;
        private decimal _previousPrice;
        private bool _hasPrice;
        private string _status = "等待...";

        public string CybermoneyName { get; set; } = "";

        public string CurrencyName { get; set; } = "usdt";

        public int RefreshInterval { get; set; } = 2000;

        public decimal Price
        {
            get { return _price; }
            set
            {
                if (_price == value) return;
                if (_hasPrice) _previousPrice = _price;
                _price = value;
                _hasPrice = true;
                Raise(nameof(Price));
                Raise(nameof(PriceText));
                Raise(nameof(ChangeText));
                Raise(nameof(Direction));
            }
        }

        public List<Alert> Alerts { get; set; } = new List<Alert>();

        /// <summary>唯一键：BTC/USDT</summary>
        [JsonIgnore]
        public string Key => string.Format(CultureInfo.InvariantCulture, "{0}/{1}",
            (CybermoneyName ?? "").ToUpperInvariant(),
            (CurrencyName ?? "").ToUpperInvariant());

        /// <summary>行情来源与耗时，例如 "OKX · 38ms"</summary>
        [JsonIgnore]
        public string Status
        {
            get { return _status; }
            set
            {
                if (_status == value) return;
                _status = value;
                Raise(nameof(Status));
            }
        }

        /// <summary>涨跌方向：up / down / flat（中国市场惯例 涨红跌绿）</summary>
        [JsonIgnore]
        public string Direction
        {
            get
            {
                if (_previousPrice <= 0 || _price <= 0 || _price == _previousPrice) return "flat";
                return _price > _previousPrice ? "up" : "down";
            }
        }

        [JsonIgnore]
        public string PriceText
        {
            get
            {
                if (_price <= 0) return "—";
                if (_price >= 1000m) return _price.ToString("#,##0.00", CultureInfo.InvariantCulture);
                if (_price >= 1m) return _price.ToString("0.0000", CultureInfo.InvariantCulture);
                return _price.ToString("0.00000000", CultureInfo.InvariantCulture);
            }
        }

        [JsonIgnore]
        public string ChangeText
        {
            get
            {
                if (_previousPrice <= 0 || _price <= 0) return "—";
                decimal pct = (_price - _previousPrice) / _previousPrice * 100m;
                if (pct == 0m) return "—";
                return (pct > 0 ? "+" : "") + pct.ToString("0.00", CultureInfo.InvariantCulture) + "%";
            }
        }

        [JsonIgnore]
        public string IntervalText =>
            (RefreshInterval / 1000.0).ToString("0.#", CultureInfo.InvariantCulture) + "秒";

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise(string name)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }
    }
}
