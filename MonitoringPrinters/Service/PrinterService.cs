using Lextm.SharpSnmpLib.Messaging;
using Lextm.SharpSnmpLib;
using MonitoringPrinters.Model.PrinterModel;
using MonitoringPrinters.Model;
using System.Net;

namespace MonitoringPrinters.Service
{
    public class PrinterService
    {

        private readonly Dictionary<string, string> OidMap = new Dictionary<string, string>
        {
            { "device_name", "1.3.6.1.2.1.1.5.0" },
            { "device_description", "1.3.6.1.2.1.1.1.0" },
            { "uptime", "1.3.6.1.2.1.1.3.0" },
            { "contact", "1.3.6.1.2.1.1.4.0" },
            { "location", "1.3.6.1.2.1.1.6.0" },


            { "printer_status", "1.3.6.1.2.1.25.3.5.1.1.1" },


            { "toner_level", "1.3.6.1.2.1.43.11.1.1.9.1.1" },
            { "max_toner_level", "1.3.6.1.2.1.43.11.1.1.8.1.1" },


            { "completed_jobs", "1.3.6.1.2.1.43.10.2.1.4.1.1" },


            { "total_pages_printed", "1.3.6.1.2.1.43.10.2.1.4.1.1" },

            { "tray_status", "1.3.6.1.2.1.43.8.2.1.10.1" },
            { "tray_max_capacity", "1.3.6.1.2.1.43.8.2.1.9.1" },
            { "tray_current_level", "1.3.6.1.2.1.43.8.2.1.10.1" },



            { "error_state", "1.3.6.1.2.1.25.3.5.1.2.1" }
        };

        private readonly Dictionary<string, Dictionary<string, string>> VendorSpecificOids = new Dictionary<string, Dictionary<string, string>>
        {
            {
                "hp", new Dictionary<string, string> {
                    { "toner_level", "1.3.6.1.2.1.43.11.1.1.9.1.1" },
                    { "error_code", "1.3.6.1.2.1.43.18.1.1.8.1" }
                }
            },
            {
                "brother", new Dictionary<string, string> {
                    { "toner_level", "1.3.6.1.4.1.2435.2.3.9.4.2.1.5.5.1" },
                    { "error_code", "1.3.6.1.4.1.2435.2.3.9.4.2.1.5.1.17" }
                }
            },
            {
                "xerox", new Dictionary<string, string> {
                    { "toner_level", "1.3.6.1.4.1.253.8.53.13.2.1.6.1.20.34" },
                    { "error_code", "1.3.6.1.4.1.253.8.53.13.2.1.5.1.1" }
                }
            },
            {
                "canon", new Dictionary<string, string> {
                    { "toner_level", "1.3.6.1.4.1.1602.1.11.1.3.1.4.130" },
                    { "error_code", "1.3.6.1.4.1.1602.1.3.2.1.4.0" }
                }
            },
            {
                "oki", new Dictionary<string, string> {
                    { "toner_level", "1.3.6.1.4.1.2001.1.1.1.1.11.1.1.9.1.1" },
                    { "error_code", "1.3.6.1.4.1.2001.1.1.1.1.11.2.1.1.1" }
                 }
            }
        };
        public object GetSnmpData(string ipAddress, string oid, string community = "public")
        {
            try
            {
                var endpoint = new IPEndPoint(IPAddress.Parse(ipAddress), 161);
                var communityString = new OctetString(community);
                var oidToQuery = new ObjectIdentifier(oid);
                var variables = new List<Variable> { new Variable(oidToQuery) };
                const int timeout = 10000;

            
                try
                {
                    var resultV2 = Messenger.Get(VersionCode.V2,
                                              endpoint,
                                              communityString,
                                              variables,
                                              timeout);
                    if (resultV2 != null && resultV2.Count > 0)
                    {
                        Console.WriteLine($"{DateTime.Now}: Successfully connected to {ipAddress} using SNMPv2");
                        return resultV2[0].Data;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{DateTime.Now}: SNMPv2 failed for {ipAddress}: {ex.Message}. Trying SNMPv1...");

                    
                    var resultV1 = Messenger.Get(VersionCode.V1,
                                              endpoint,
                                              communityString,
                                              variables,
                                              timeout);
                    if (resultV1 != null && resultV1.Count > 0)
                    {
                        Console.WriteLine($"{DateTime.Now}: Successfully connected to {ipAddress} using SNMPv1");
                        return resultV1[0].Data;
                    }
                }

                
                Console.WriteLine($"{DateTime.Now}: No SNMP response from {ipAddress} for OID {oid}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now}: SNMP error: {ex.Message} for OID {oid} on {ipAddress}");
                return null;
            }
        }

        private readonly Dictionary<string, VersionCode> _printerVersionMap = new Dictionary<string, VersionCode>();

        public object GetSnmpDataWithCachedVersion(string ipAddress, string oid, string community = "public")
        {

            if (_printerVersionMap.TryGetValue(ipAddress, out var cachedVersion))
            {
                try
                {
                    var endpoint = new IPEndPoint(IPAddress.Parse(ipAddress), 161);
                    var communityString = new OctetString(community);
                    var oidToQuery = new ObjectIdentifier(oid);
                    var variables = new List<Variable> { new Variable(oidToQuery) };

                    var result = Messenger.Get(cachedVersion,
                                             endpoint,
                                             communityString,
                                             variables,
                                             10000);

                    if (result != null && result.Count > 0)
                    {
                        return result[0].Data;
                    }
                }
                catch
                {
                    
                    _printerVersionMap.Remove(ipAddress);
                }
            }

            
            var versionsToTry = new[] { VersionCode.V2, VersionCode.V1 };

            foreach (var version in versionsToTry)
            {
                try
                {
                    var endpoint = new IPEndPoint(IPAddress.Parse(ipAddress), 161);
                    var communityString = new OctetString(community);
                    var oidToQuery = new ObjectIdentifier(oid);
                    var variables = new List<Variable> { new Variable(oidToQuery) };

                    var result = Messenger.Get(version,
                                             endpoint,
                                             communityString,
                                             variables,
                                             10000);

                    if (result != null && result.Count > 0)
                    {

                        _printerVersionMap[ipAddress] = version;
                        return result[0].Data;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{DateTime.Now}: SNMP {version} failed for {ipAddress}: {ex.Message}");
                }
            }

            return null;
        }

        public async Task<PrinterInfo> GetPrinterInfoAsync(string ipAddress, string community = "zabbix")
        {
            return await Task.Run(() =>
            {
                var result = new PrinterInfo
                {
                    IpAddress = ipAddress,
                    Timestamp = DateTime.UtcNow
                };

                foreach (var item in OidMap)
                {
                    var value = GetSnmpData(ipAddress, item.Value, community);

                    if (value != null)
                    {
                        switch (item.Key)
                        {
                            case "device_name":
                                result.BasicInfo.DeviceName = value.ToString();
                                break;
                            case "device_description":
                                result.BasicInfo.DeviceDescription = value.ToString();
                                break;
                            case "uptime":
                                result.BasicInfo.Uptime = value.ToString();
                                break;
                            case "contact":
                                result.BasicInfo.Contact = value.ToString();
                                break;
                            case "location":
                                result.BasicInfo.Location = value.ToString();
                                break;
                            case "printer_status":
                                if (int.TryParse(value.ToString(), out var statusValue))
                                    result.Status.PrinterStatus = statusValue;
                                break;
                            case "toner_level":
                                if (int.TryParse(value.ToString(), out var tonerValue))
                                    result.Consumables.TonerLevel = tonerValue;
                                break;
                            case "max_toner_level":
                                if (int.TryParse(value.ToString(), out var maxTonerValue))
                                    result.Consumables.MaxTonerLevel = maxTonerValue;
                                break;
                            case "completed_jobs":
                                if (int.TryParse(value.ToString(), out var jobsValue))
                                    result.Counters.CompletedJobs = jobsValue;
                                break;
                            case "total_pages_printed":
                                if (int.TryParse(value.ToString(), out var pagesValue))
                                    result.Counters.TotalPagesPrinted = pagesValue;
                                break;
                            case "error_state":
                                if (int.TryParse(value.ToString(), out var errorValue))
                                    result.Errors.ErrorState = errorValue;
                                break;
                        }
                    }
                }

                string vendor = null;
                if (!string.IsNullOrEmpty(result.BasicInfo.DeviceDescription))
                {
                    var desc = result.BasicInfo.DeviceDescription.ToLower();
                    if (desc.Contains("hp") || desc.Contains("hewlett-packard"))
                    {
                        vendor = "hp";
                    }
                    else if (desc.Contains("brother"))
                    {
                        vendor = "brother";
                    }
                    else if (desc.Contains("xerox"))
                    {
                        vendor = "xerox";
                    }
                    else if (desc.Contains("canon"))
                    {
                        vendor = "canon";
                    }
                    else if (desc.Contains("oki"))
                    {
                        vendor = "oki";
                    }
                }

                if (!string.IsNullOrEmpty(vendor) && VendorSpecificOids.ContainsKey(vendor))
                {
                    foreach (var item in VendorSpecificOids[vendor])
                    {
                        var value = GetSnmpData(ipAddress, item.Value, community);
                        if (value != null && int.TryParse(value.ToString(), out var intValue))
                        {
                            if (item.Key.Contains("toner"))
                            {
                                result.Consumables.VendorSpecificConsumables[$"{vendor}_{item.Key}"] = intValue;
                            }
                            else if (item.Key.Contains("error"))
                            {
                                result.Errors.VendorSpecificErrors[$"{vendor}_{item.Key}"] = intValue;
                            }
                        }
                    }
                }

                return result;
            });
        }

        public List<PrinterListItem> GetAllPrinters()
        {

            return new List<PrinterListItem>
            {
                 new PrinterListItem { Ip = "192.168.1.100", Name = "Printer 1", Community = "public" },
                 new PrinterListItem { Ip = "192.168.1.101", Name = "Printer 2", Community = "zabbix" },
            };
        }
    }
}
