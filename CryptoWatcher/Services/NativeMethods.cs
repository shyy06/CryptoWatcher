using System.Runtime.InteropServices;

namespace CryptoWatcher.Services
{
    /// <summary>
    /// Win32 互操作：鼠标穿透（64 位安全，按 IntPtr.Size 自动选择 Long/LongPtr 版本）
    /// </summary>
    internal static class NativeMethods
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOOWNERZORDER = 0x0200;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private static IntPtr GetWindowLongCompat(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);
        }

        private static IntPtr SetWindowLongCompat(IntPtr hWnd, int nIndex, IntPtr value)
        {
            return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, value) : SetWindowLong32(hWnd, nIndex, value);
        }

        /// <summary>
        /// 开启/关闭窗口鼠标穿透。
        /// 注意：关闭时只清除 WS_EX_TRANSPARENT，保留 WS_EX_LAYERED
        /// （WPF 的 AllowsTransparency 依赖它，清掉会导致窗口渲染异常）。
        /// </summary>
        public static void SetClickThrough(IntPtr hWnd, bool enabled)
        {
            if (hWnd == IntPtr.Zero) return;

            try
            {
                long style = GetWindowLongCompat(hWnd, GWL_EXSTYLE).ToInt64();

                if (enabled) style |= WS_EX_TRANSPARENT | WS_EX_LAYERED;
                else style &= ~(long)WS_EX_TRANSPARENT;

                SetWindowLongCompat(hWnd, GWL_EXSTYLE, new IntPtr(style));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[NativeMethods] 设置鼠标穿透失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 强制把窗口重新钉回置顶（不改大小/位置、不抢焦点）。
        /// 这是修复「迷你窗运行一段时间后掉到最底层」的关键：
        /// WPF 的 Topmost 仅在设置/显示时向系统申明一次 WS_EX_TOPMOST，
        /// 之后遇到息屏唤醒、分辨率变化、其他置顶窗口进出等系统事件时 z-order 可能被降级，
        /// 且不会自动恢复。此处用 SetWindowPos(HWND_TOPMOST) 重新申明置顶。
        /// 参数均为 SWP_NO* 标志，故不会移动窗口、不会改变大小、不会抢焦点。
        /// </summary>
        public static void ForceTopmost(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            try
            {
                SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[NativeMethods] 强制置顶失败: " + ex.Message);
            }
        }
    }
}
