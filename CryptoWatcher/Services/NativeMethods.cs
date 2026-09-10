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
    }
}
