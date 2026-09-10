using CryptoWatcher.Models;

namespace CryptoWatcher.Views
{
    /// <summary>
    /// 单个价格提醒的编辑对话框（大于 / 小于 / 等于 某个价位）
    /// </summary>
    public partial class AlertEditWindow : Window
    {
        public Alert Result { get; private set; }

        public AlertEditWindow()
        {
            InitializeComponent();

            TypeCombo.ItemsSource = new string[] { "大于", "小于", "等于" };
            TypeCombo.SelectedIndex = 0;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            string text = (PriceBox.Text ?? "").Trim();

            decimal price;
            if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out price))
            {
                MessageBox.Show(this, "请输入有效的价位数字（例如 65000 或 0.00001234）",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (price <= 0m)
            {
                MessageBox.Show(this, "价位必须大于 0",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int index = TypeCombo.SelectedIndex;
            if (index < 0) index = 0;

            Result = new Alert
            {
                Type = (JugerType)index,
                PricePoint = price
            };

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try { DragMove(); }
            catch (Exception ex) { Debug.WriteLine("[AlertEditWindow] 拖动失败: " + ex.Message); }
        }
    }
}
