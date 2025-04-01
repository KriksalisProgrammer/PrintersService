namespace MonitoringPrinters.Model
{
    public class ConsumablesInfo
    {
        public int? TonerLevel { get; set; }
        public int? MaxTonerLevel { get; set; }
        public Dictionary<string, int> VendorSpecificConsumables { get; set; } = new Dictionary<string, int>();
    }
}
