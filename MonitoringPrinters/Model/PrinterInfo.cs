using MonitoringPrinters.Model.ErrorModel;
using PrinterMonitoringApi;

namespace MonitoringPrinters.Model
{
    public class PrinterInfo
    {
        public string IpAddress { get; set; }
        public DateTime Timestamp { get; set; }
        public BasicInfo BasicInfo { get; set; } = new BasicInfo();
        public StatusInfo Status { get; set; } = new StatusInfo();
        public ConsumablesInfo Consumables { get; set; } = new ConsumablesInfo();
        public CountersInfo Counters { get; set; } = new CountersInfo();
        public ErrorsInfo Errors { get; set; } = new ErrorsInfo();
    }
}
