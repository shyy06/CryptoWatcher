using System.Collections.ObjectModel;
using CryptoWatcher.Models;
using CryptoWatcher.Services;

namespace CryptoWatcher.Views
{
    /// <summary>
    /// 添加 / 编辑监测项对话框。保存时会先取一次实时价格校验币种是否有效。
    /// </summary>
    public partial class ItemEditWindow : Window
    {
        public CryptoItem Result { get; private set; }

        public ObservableCollection<AlertRow> AlertRows { get; } = new ObservableCollection<AlertRow>();

        public ItemEditWindow(CryptoItem editing)
        {
            InitializeComponent();

            DataContext = this;

            CoinCombo.ItemsSource = CoinCatalog.LoadCache();
            BaseCombo.ItemsSource = new string[] { "usdt", "usdc", "btc", "eth" };
            IntervalCombo.ItemsSource = new string[] { "1", "2", "3", "5", "10", "15", "30", "60" };

            if (editing != null)
            {
                TitleText.Text = "编辑监测项";
                Title = "编辑监测项";

                CoinCombo.Text = editing.CybermoneyName;
                BaseCombo.Text = editing.CurrencyName;
                IntervalCombo.Text = (editing.RefreshInterval / 1000.0)
                    .ToString("0.#", CultureInfo.InvariantCulture);

                if (editing.Alerts != null)
                {
                    foreach (Alert alert in editing.Alerts)
                    {
                        if (alert != null) AlertRows.Add(new AlertRow(alert));
                    }
                }
            }
            else
            {
                BaseCombo.Text = "usdt";
                IntervalCombo.Text = "2";
            }

            AlertRows.CollectionChanged += (s, e) => UpdateAlertHint();
            UpdateAlertHint();

            Loaded += OnDialogLoaded;
        }

        /// <summary>窗口显示后再联网刷新热门币种，避免弹窗卡顿</summary>
        private async void OnDialogLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                string current = CoinCombo.Text;
                string[] coins = await CoinCatalog.FetchTopAsync(20, CancellationToken.None);

                if (coins != null && coins.Length > 0)
                {
                    CoinCombo.ItemsSource = coins;
                    CoinCombo.Text = current;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ItemEditWindow] 刷新热门币种失败: " + ex.Message);
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            string coin = (CoinCombo.Text ?? "").Trim().ToUpperInvariant();
            string baseCoin = (BaseCombo.Text ?? "").Trim().ToLowerInvariant();
            string intervalText = (IntervalCombo.Text ?? "").Trim();

            if (coin.Length == 0)
            {
                MessageBox.Show(this, "请填写或选择币种代码（例如 BTC）",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!coin.All(char.IsLetterOrDigit))
            {
                MessageBox.Show(this, "币种代码只能包含字母和数字（例如 BTC、SHIB）",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (baseCoin.Length == 0) baseCoin = "usdt";

            if (!baseCoin.All(char.IsLetterOrDigit))
            {
                MessageBox.Show(this, "计价币种只能包含字母和数字（例如 usdt）",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int seconds;
            if (!int.TryParse(intervalText, NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds)
                || seconds < 1)
            {
                MessageBox.Show(this, "刷新间隔必须是不小于 1 的整数（秒）",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (seconds > 6000) seconds = 6000;

            SaveButton.IsEnabled = false;
            object originalContent = SaveButton.Content;
            SaveButton.Content = "获取价格中...";

            try
            {
                QuoteResult quote = await WebApis.GetPriceAsync(coin, baseCoin, CancellationToken.None);

                var item = new CryptoItem
                {
                    CybermoneyName = coin,
                    CurrencyName = baseCoin,
                    RefreshInterval = seconds * 1000
                };
                item.Price = quote.Price;

                foreach (AlertRow row in AlertRows)
                {
                    if (row != null && row.Source != null) item.Alerts.Add(row.Source);
                }

                Result = item;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ItemEditWindow] 保存失败: " + ex);
                MessageBox.Show(this,
                    "保存失败，请确认币种代码是否正确。\n\n" + ex.Message,
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SaveButton.IsEnabled = true;
                SaveButton.Content = originalContent ?? "保存";
            }
        }

        private void AddAlert_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AlertEditWindow { Owner = this };
            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                AlertRows.Add(new AlertRow(dialog.Result));
            }
        }

        private void RemoveAlert_Click(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;

            var row = element.DataContext as AlertRow;
            if (row != null) AlertRows.Remove(row);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try { DragMove(); }
            catch (Exception ex) { Debug.WriteLine("[ItemEditWindow] 拖动失败: " + ex.Message); }
        }

        private void UpdateAlertHint()
        {
            if (NoAlertHint == null) return;
            NoAlertHint.Visibility = AlertRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
