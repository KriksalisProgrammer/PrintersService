using Lextm.SharpSnmpLib;

namespace MonitoringPrinters.Model.SNMPConnectionModel
{
    public class SnmpConnectionInfo
    {
        public VersionCode Version { get; set; }
        public string Community { get; set; }
    }
}
