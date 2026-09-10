namespace CryptoWatcher.Models
{
    /// <summary>
    /// 价格提醒项。字段名与旧版 config.json 完全一致，保证配置向后兼容。
    /// </summary>
    public class Alert
    {
        public JugerType Type { get; set; }

        public decimal PricePoint { get; set; }

        public bool Trigged { get; set; } = false;
    }

    public enum JugerType
    {
        Greater = 0,
        Less = 1,
        Equal = 2
    }

    /// <summary>
    /// 提醒项的界面包装（用于添加/编辑窗口的列表展示）
    /// </summary>
    public class AlertRow
    {
        public Alert Source { get; }

        public AlertRow(Alert source)
        {
            Source = source;
        }

        public string TypeText
        {
            get
            {
                switch (Source.Type)
                {
                    case JugerType.Greater: return "大于";
                    case JugerType.Less: return "小于";
                    default: return "等于";
                }
            }
        }

        public string PriceText =>
            Source.PricePoint.ToString("0.########", CultureInfo.InvariantCulture);
    }
}
