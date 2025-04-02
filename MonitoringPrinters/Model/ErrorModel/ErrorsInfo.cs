namespace MonitoringPrinters.Model.ErrorModel
{
    public class ErrorsInfo
    {
        public int? ErrorState { get; set; }
        public Dictionary<string, int> VendorSpecificErrors { get; set; } = new Dictionary<string, int>();
    }
}
