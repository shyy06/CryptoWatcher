using System.Collections.ObjectModel;
using System.Windows.Interop;
using CryptoWatcher.Models;
using CryptoWatcher.Services;

namespace CryptoWatcher
{
    /// <summary>
    /// 桌面常驻迷你窗：极窄隐蔽条，可拖动、可锁定位置、可鼠标穿透、可取消置顶。
    /// 与主窗口共享同一个监测项集合。
    /// </summary>
    public partial class MiniWindow : Window
    {
        private readonly MainWindow _owner;
        private bool _allowClose;

        public event Action<bool> LockStateChanged;

        public event Action<bool> ClickThroughChanged;

        public MiniWindow(MainWindow owner, ObservableCollection<CryptoItem> items)
        {
            InitializeComponent();

            _owner = owner;
            DataContext = items;

            TopmostItem.IsChecked = Topmost;
            LockItem.IsChecked = owner.MiniLocked;
            ThroughItem.IsChecked = owner.MiniClickThrough;
        }

        public bool IsLocked
        {
            get { return LockItem.IsChecked; }
            set
            {
                LockItem.IsChecked = value;
                var handler = LockStateChanged;
                if (handler != null) handler(value);
            }
        }

        public bool IsClickThrough
        {
            get { return ThroughItem.IsChecked; }
            set
            {
                ThroughItem.IsChecked = value;
                ApplyClickThrough();
                var handler = ClickThroughChanged;
                if (handler != null) handler(value);
            }
        }

        /// <summary>显示在主窗口附近（不做边界钳制，避免多屏时跳到主屏）</summary>
        public void PlaceNear(Window source)
        {
            try
            {
                Left = source.Left + 40;
                Top = source.Top + 40;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[MiniWindow] 定位失败: " + ex.Message);
            }
        }

        public void AllowClose()
        {
            _allowClose = true;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            ApplyClickThrough();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // 程序退出过程中不拦截（否则会挡住 Application.Shutdown 的连锁关闭）；
            // 用户单独关闭迷你窗时，回到主界面。
            bool ownerShuttingDown = _owner != null && _owner.IsShuttingDown;

            if (!_allowClose && !ownerShuttingDown && !Dispatcher.HasShutdownStarted)
            {
                e.Cancel = true;
                if (_owner != null) _owner.RestoreMainWindow();
                return;
            }

            base.OnClosing(e);
        }

        private void ApplyClickThrough()
        {
            try
            {
                var helper = new WindowInteropHelper(this);
                NativeMethods.SetClickThrough(helper.Handle, ThroughItem.IsChecked);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[MiniWindow] 应用鼠标穿透失败: " + ex.Message);
            }
        }

        /// <summary>拖拽条：单击拖动位置，双击恢复主界面（锁定位置时仍可双击）</summary>
        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2)
            {
                if (_owner != null) _owner.RestoreMainWindow();
                return;
            }

            if (LockItem.IsChecked) return;

            try { DragMove(); }
            catch (Exception ex) { Debug.WriteLine("[MiniWindow] 拖动失败: " + ex.Message); }
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            if (_owner != null) _owner.RestoreMainWindow();
        }

        /// <summary>取消置顶后，迷你窗会被其他窗口盖住，进一步降低存在感</summary>
        private void Topmost_Click(object sender, RoutedEventArgs e)
        {
            Topmost = TopmostItem.IsChecked;
        }

        private void Lock_Click(object sender, RoutedEventArgs e)
        {
            var handler = LockStateChanged;
            if (handler != null) handler(LockItem.IsChecked);
        }

        private void Through_Click(object sender, RoutedEventArgs e)
        {
            ApplyClickThrough();

            var handler = ClickThroughChanged;
            if (handler != null) handler(ThroughItem.IsChecked);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            if (_owner != null) _owner.RequestShutdown();
            else Application.Current.Shutdown();
        }
    }
}
