using System.Collections.ObjectModel;
using CryptoWatcher.Models;
using CryptoWatcher.Services;
using CryptoWatcher.Views;

namespace CryptoWatcher
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly PriceMonitor _monitor;
        private readonly System.Windows.Forms.NotifyIcon _tray;
        private readonly System.Windows.Forms.ToolStripMenuItem _trayLock;
        private readonly System.Windows.Forms.ToolStripMenuItem _trayThrough;

        private MiniWindow _mini;
        private CryptoItem _selectedItem;
        private string _statusText = "就绪";

        /// <summary>迷你窗口的位置锁定状态（托盘菜单与迷你窗双向同步）</summary>
        public bool MiniLocked { get; private set; }

        /// <summary>迷你窗口的鼠标穿透状态</summary>
        public bool MiniClickThrough { get; private set; }

        public ObservableCollection<CryptoItem> Items { get; } = new ObservableCollection<CryptoItem>();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _tray = CreateTrayIcon(out _trayLock, out _trayThrough);

            _monitor = new PriceMonitor(Dispatcher, _tray);
            _monitor.StatusChanged += OnMonitorStatus;

            Items.CollectionChanged += (s, e) => UpdateEmptyHint();
            Loaded += OnWindowLoaded;
        }

        // ==================== 绑定属性 ====================

        public CryptoItem SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                _selectedItem = value;
                Raise(nameof(SelectedItem));
                UpdateButtons();
            }
        }

        public string SubTitle =>
            Items.Count == 0
                ? "未添加监测项"
                : "正在监测 " + Items.Count.ToString(CultureInfo.InvariantCulture) + " 个币种";

        public string StatusText
        {
            get { return _statusText; }
            set
            {
                if (_statusText == value) return;
                _statusText = value;
                Raise(nameof(StatusText));
            }
        }

        // ==================== 生命周期 ====================

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            LoadConfig();
            UpdateButtons();
            UpdateEmptyHint();
        }

        private void LoadConfig()
        {
            string error;
            List<CryptoItem> items = ConfigStore.Load(out error);

            foreach (CryptoItem item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.CybermoneyName)) continue;

                if (string.IsNullOrWhiteSpace(item.CurrencyName)) item.CurrencyName = "usdt";
                if (item.RefreshInterval < 500) item.RefreshInterval = 2000;
                if (item.Alerts == null) item.Alerts = new List<Alert>();

                Items.Add(item);
                _monitor.Start(item);
            }

            Raise(nameof(SubTitle));

            if (!string.IsNullOrEmpty(error))
            {
                MessageBox.Show(this,
                    "配置文件读取失败，已忽略旧配置。\n\n" + error + "\n\n路径：" + AppPaths.ConfigFile,
                    "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SaveConfig()
        {
            string error;
            if (!ConfigStore.Save(Items, out error))
            {
                StatusText = "配置保存失败：" + error;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try { _monitor.Dispose(); }
            catch (Exception ex) { Debug.WriteLine("[MainWindow] 停止监控失败: " + ex.Message); }

            try
            {
                if (_mini != null)
                {
                    _mini.AllowClose();
                    _mini.Close();
                }
            }
            catch (Exception ex) { Debug.WriteLine("[MainWindow] 关闭迷你窗失败: " + ex.Message); }

            try
            {
                if (_tray != null)
                {
                    _tray.Visible = false;
                    _tray.Dispose();
                }
            }
            catch (Exception ex) { Debug.WriteLine("[MainWindow] 释放托盘失败: " + ex.Message); }

            base.OnClosed(e);
        }

        // ==================== 标题栏 ====================

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) return; // 不做最大化，避免无边框窗口遮住任务栏
            try { DragMove(); }
            catch (Exception ex) { Debug.WriteLine("[MainWindow] 拖动失败: " + ex.Message); }
        }

        private void Pin_Click(object sender, RoutedEventArgs e)
        {
            Topmost = !Topmost;
            try
            {
                PinButton.Foreground = Topmost
                    ? (Brush)FindResource("AccentBrush")
                    : (Brush)FindResource("TextSecondaryBrush");
            }
            catch (Exception ex) { Debug.WriteLine("[MainWindow] 更新置顶图标失败: " + ex.Message); }

            StatusText = Topmost ? "窗口已置顶" : "已取消置顶";
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            try
            {
                _tray.ShowBalloonTip(1500, "CryptoWatcher",
                    "已最小化到托盘，双击托盘图标可恢复窗口",
                    System.Windows.Forms.ToolTipIcon.Info);
            }
            catch (Exception ex) { Debug.WriteLine("[MainWindow] 托盘提示失败: " + ex.Message); }
        }

        /// <summary>是否正在退出程序（退出过程中迷你窗不再拦截关闭）</summary>
        public bool IsShuttingDown { get; private set; }

        /// <summary>统一退出入口：先标记正在退出，避免迷你窗在退出过程中拦截关闭</summary>
        public void RequestShutdown()
        {
            IsShuttingDown = true;
            Application.Current.Shutdown();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            RequestShutdown();
        }

        // ==================== 增删改 ====================

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ItemEditWindow(null) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                Items.Add(dialog.Result);
                _monitor.Start(dialog.Result);
                SaveConfig();
                SelectedItem = dialog.Result;
                StatusText = "已添加 " + dialog.Result.Key;
            }
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            EditSelectedItem();
        }

        private void ItemsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            EditSelectedItem();
        }

        private void EditSelectedItem()
        {
            CryptoItem target = SelectedItem;
            if (target == null) return;

            var dialog = new ItemEditWindow(target) { Owner = this };
            if (dialog.ShowDialog() != true || dialog.Result == null) return;

            int index = Items.IndexOf(target);
            if (index < 0) return;

            _monitor.Stop(target.Key);
            Items[index] = dialog.Result;
            _monitor.Start(dialog.Result);
            SaveConfig();
            SelectedItem = dialog.Result;
            StatusText = "已更新 " + dialog.Result.Key;
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            CryptoItem target = SelectedItem;
            if (target == null) return;

            MessageBoxResult answer = MessageBox.Show(this,
                "确定要删除 " + target.Key + " 吗？",
                "删除确认", MessageBoxButton.OKCancel, MessageBoxImage.Question);

            if (answer != MessageBoxResult.OK) return;

            _monitor.Stop(target.Key);
            Items.Remove(target);
            SaveConfig();
            StatusText = "已删除 " + target.Key;
        }

        // ==================== 迷你模式 ====================

        private void Mini_Click(object sender, RoutedEventArgs e)
        {
            EnterMiniMode();
        }

        public void EnterMiniMode()
        {
            if (_mini == null)
            {
                _mini = new MiniWindow(this, Items);
                _mini.LockStateChanged += OnMiniLockChanged;
                _mini.ClickThroughChanged += OnMiniThroughChanged;
            }

            _mini.PlaceNear(this);
            _mini.Show();
            Hide();
            StatusText = "已切换到迷你模式（双击迷你窗可恢复主界面）";
        }

        public void RestoreMainWindow()
        {
            if (_mini != null) _mini.Hide();

            Show();
            WindowState = WindowState.Normal;
            Activate();
            StatusText = "就绪";
        }

        private void OnMiniLockChanged(bool locked)
        {
            MiniLocked = locked;
            if (_trayLock != null) _trayLock.Checked = locked;
        }

        private void OnMiniThroughChanged(bool through)
        {
            MiniClickThrough = through;
            if (_trayThrough != null) _trayThrough.Checked = through;
        }

        private void TrayLock_Click(object sender, EventArgs e)
        {
            MiniLocked = _trayLock.Checked;
            if (_mini != null) _mini.IsLocked = MiniLocked;
            else StatusText = MiniLocked ? "锁定位置将在进入迷你模式后生效" : "已取消锁定位置";
        }

        private void TrayThrough_Click(object sender, EventArgs e)
        {
            MiniClickThrough = _trayThrough.Checked;
            if (_mini != null) _mini.IsClickThrough = MiniClickThrough;
            else StatusText = MiniClickThrough ? "鼠标穿透将在进入迷你模式后生效" : "已取消鼠标穿透";
        }

        // ==================== 托盘 ====================

        private System.Windows.Forms.NotifyIcon CreateTrayIcon(
            out System.Windows.Forms.ToolStripMenuItem lockItem,
            out System.Windows.Forms.ToolStripMenuItem throughItem)
        {
            var tray = new System.Windows.Forms.NotifyIcon();
            tray.Text = "CryptoWatcher";
            tray.Icon = LoadAppIcon();
            tray.Visible = true;

            var menu = new System.Windows.Forms.ContextMenuStrip();

            var showItem = new System.Windows.Forms.ToolStripMenuItem("显示主界面");
            showItem.Click += (s, e) => RestoreMainWindow();

            var miniItem = new System.Windows.Forms.ToolStripMenuItem("迷你模式");
            miniItem.Click += (s, e) => EnterMiniMode();

            lockItem = new System.Windows.Forms.ToolStripMenuItem("锁定位置");
            lockItem.CheckOnClick = true;
            lockItem.Click += TrayLock_Click;

            throughItem = new System.Windows.Forms.ToolStripMenuItem("鼠标穿透");
            throughItem.CheckOnClick = true;
            throughItem.Click += TrayThrough_Click;

            var exitItem = new System.Windows.Forms.ToolStripMenuItem("退出");
            exitItem.Click += (s, e) => RequestShutdown();

            menu.Items.Add(showItem);
            menu.Items.Add(miniItem);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add(lockItem);
            menu.Items.Add(throughItem);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add(exitItem);

            tray.ContextMenuStrip = menu;
            tray.MouseDoubleClick += (s, e) => RestoreMainWindow();

            return tray;
        }

        private static System.Drawing.Icon LoadAppIcon()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/Assets/mainIcon.ico");
                var info = Application.GetResourceStream(uri);
                if (info != null && info.Stream != null)
                {
                    byte[] bytes;
                    using (var buffer = new MemoryStream())
                    {
                        info.Stream.CopyTo(buffer);
                        bytes = buffer.ToArray();
                    }
                    return new System.Drawing.Icon(new MemoryStream(bytes));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[MainWindow] 从资源加载图标失败: " + ex.Message);
            }

            try
            {
                string exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    System.Drawing.Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                    if (icon != null) return icon;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[MainWindow] 提取程序图标失败: " + ex.Message);
            }

            return System.Drawing.SystemIcons.Application;
        }

        // ==================== 辅助 ====================

        private void OnMonitorStatus(string text)
        {
            StatusText = text;
        }

        private void UpdateButtons()
        {
            if (EditButton == null || DeleteButton == null) return;
            bool hasSelection = SelectedItem != null;
            EditButton.IsEnabled = hasSelection;
            DeleteButton.IsEnabled = hasSelection;
        }

        private void UpdateEmptyHint()
        {
            if (EmptyHint != null)
            {
                EmptyHint.Visibility = Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            Raise(nameof(SubTitle));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise(string name)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }
    }
}
