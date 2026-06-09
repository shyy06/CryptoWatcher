using CryptoWatcher.Common;
using CryptoWatcher.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CryptoWatcher.Forms
{
    public partial class ItemEdit : Form
    {
        public CryptoItem Result;

        /// <summary>
        /// 离线兜底列表 — 仅当无缓存且网络全部失败时使用
        /// </summary>
        private static readonly string[] FallbackCoins = new[]
        {
            "BTC", "ETH", "USDT", "BNB", "SOL",
            "XRP", "USDC", "DOGE", "ADA", "TRX",
            "TON", "AVAX", "LINK", "SHIB", "SUI",
            "DOT", "BCH", "LTC", "NEAR", "UNI",
        };

        /// <summary>
        /// 缓存目录
        /// </summary>
        private static readonly string CacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CryptoWatcher");

        private static readonly string CacheFile = Path.Combine(CacheDir, "topcoins.json");

        /// <summary>
        /// 从缓存文件加载币种列表，无缓存则返回 null
        /// </summary>
        private static string[] LoadCache()
        {
            try
            {
                if (File.Exists(CacheFile))
                {
                    string json = File.ReadAllText(CacheFile, Encoding.UTF8);
                    return JsonConvert.DeserializeObject<string[]>(json);
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 保存币种列表到缓存文件
        /// </summary>
        private static void SaveCache(string[] coins)
        {
            try
            {
                Directory.CreateDirectory(CacheDir);
                string json = JsonConvert.SerializeObject(coins);
                File.WriteAllText(CacheFile, json, Encoding.UTF8);
            }
            catch { }
        }

        public ItemEdit()
        {
            InitializeComponent();

            // 1. 优先加载缓存（上次联网获取的数据），瞬间显示
            string[] cached = LoadCache();
            if (cached != null && cached.Length > 0)
            {
                cybermoneyName.Items.AddRange(cached);
            }
            else
            {
                cybermoneyName.Items.AddRange(FallbackCoins);
            }

            // 2. 窗口显示后后台联网更新
            this.Shown += ItemEdit_Shown;
        }

        private async void ItemEdit_Shown(object sender, EventArgs e)
        {
            string[] coins = await WebAPIs.GetTop20CoinsAsync();
            if (coins != null && coins.Length > 0)
            {
                // 保存缓存供下次使用
                SaveCache(coins);

                // 更新下拉框（在 UI 线程）
                cybermoneyName.Items.Clear();
                cybermoneyName.Items.AddRange(coins);
            }
        }

        /// <summary>
        /// 编辑入口 — 同步方法，窗口立即弹出
        /// </summary>
        public static CryptoItem Edit(CryptoItem old_item = null)
        {
            var form = new ItemEdit();
            if (old_item != null)
                form.InitFromItem(old_item);

            if (form.ShowDialog() == DialogResult.OK)
            {
                return form.Result;
            }
            else return null;
        }

        public void InitFromItem(CryptoItem item)
        {
            currencyName.Text = item.CurrencyName;
            cybermoneyName.Text = item.CybermoneyName;
            refreshInterval.Value = item.RefreshInterval / 1000;
            foreach (var alert in item.Alerts)
            {
                appendAlertItem(alert);
            }
        }
        public bool CheckValid()
        {
            return !string.IsNullOrEmpty(cybermoneyName.Text) && !string.IsNullOrEmpty(currencyName.Text);
        }

        private async void saveBtn_Click(object sender, EventArgs e)
        {
            if (CheckValid())
            {
                try
                {
                    Result = new CryptoItem();
                    Result.CybermoneyName = cybermoneyName.Text;
                    Result.CurrencyName = currencyName.Text;
                    Result.RefreshInterval = Convert.ToInt32(refreshInterval.Value * 1000);
                    Result.Price = float.Parse(await WebAPIs.GetCurrentPrice(Result.CybermoneyName, Result.CurrencyName));
                    foreach (ListViewItem alert in alertList.Items)
                        Result.Alerts.Add(alert.Tag as Alert);
                    DialogResult = DialogResult.OK;
                }
                catch
                {
                    MessageBox.Show("添加失败,请确认币种是否填写正确!");
                }
            }
            else
            {
                MessageBox.Show("请填写全部信息!");
            }
        }
        private void appendAlertItem(Alert item)
        {
            if (item != null)
            {
                var listItem = new ListViewItem();
                listItem.Text = item.Type == JugerType.Greater ? "大于" : item.Type == JugerType.Less ? "小于" : "等于";
                listItem.SubItems.Add(item.PricePoint.ToString());
                listItem.Tag = item;
                alertList.Items.Add(listItem);
            }
        }
        private void alert_add_Click(object sender, EventArgs e)
        {
            appendAlertItem(AlertEdit.Create());
        }

        private void alert_del_Click(object sender, EventArgs e)
        {
            if (alertList.SelectedItems.Count > 0)
                alertList.Items.Remove(alertList.SelectedItems[0]);
        }
    }
}
