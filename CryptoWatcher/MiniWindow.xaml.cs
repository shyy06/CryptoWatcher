using System.Collections.ObjectModel;
using System.Windows.Interop;
using System.Windows.Threading;
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

        /// <summary>置顶守卫：周期性重新申明 HWND_TOPMOST，修复长时间运行后掉到最底层的问题。</summary>
        private readonly DispatcherTimer _topmostGuard;

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

            // 2.5s 的兜底：即便中途遇到息屏唤醒/分辨率变化/其他置顶窗口进出导致 z-order 被降级，
            // 也能在很短时间内自动恢复置顶，用户基本无感。
            _topmostGuard = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(2500)
            };
            _topmostGuard.Tick += (s, e) => EnsureTopmost();
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
            EnsureTopmost();
            _topmostGuard.Start();
        }

        protected override void OnClosed(EventArgs e)
        {
            try { _topmostGuard.Stop(); }
            catch (Exception ex) { Debug.WriteLine("[MiniWindow] 停止置顶守卫失败: " + ex.Message); }
            base.OnClosed(e);
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

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            // 从主界面再次切回迷你模式（Show 复用同一实例）时，立即恢复置顶，
            // 不必等守卫定时器下一拍。
            EnsureTopmost();
        }

        private void ApplyClickThrough()
        {
            try
            {
                var helper = new WindowInteropHelper(this);
                NativeMethods.SetClickThrough(helper.Handle, ThroughItem.IsChecked);

                // 修改 GWL_EXSTYLE 会触发系统重估窗口样式，可能顺带降级 z-order；
                // 这里立即补一次置顶申明，避免「勾选鼠标穿透后沉底」。
                EnsureTopmost();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[MiniWindow] 应用鼠标穿透失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 在「期望置顶」且窗口可见时，重新向系统申明置顶（不抢焦点）。
        /// 由置顶守卫定时器与若干生命周期节点调用。
        /// </summary>
        private void EnsureTopmost()
        {
            if (!IsVisible) return;
            if (!TopmostItem.IsChecked) return; // 用户主动取消置顶时不干预

            try
            {
                var helper = new WindowInteropHelper(this);
                NativeMethods.ForceTopmost(helper.Handle);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[MiniWindow] 恢复置顶失败: " + ex.Message);
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

        /// <summary>取消置顶后，迷你窗会被其他窗口盖住，进一步降低存在感；
        /// 重新勾选时立即补一次置顶申明，无需等守卫定时器下一拍。</summary>
        private void Topmost_Click(object sender, RoutedEventArgs e)
        {
            Topmost = TopmostItem.IsChecked;
            if (Topmost) EnsureTopmost();
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
