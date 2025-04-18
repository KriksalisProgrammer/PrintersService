﻿using Lextm.SharpSnmpLib.Messaging;
using Lextm.SharpSnmpLib;
using MonitoringPrinters.Model.PrinterModel;
using MonitoringPrinters.Model;
using System.Net;
using MonitoringPrinters.Model.SNMPConnectionModel;

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
   
        private static Dictionary<string, SnmpConnectionInfo> snmpConnectionCache = new Dictionary<string, SnmpConnectionInfo>();

        private static readonly List<string> knownCommunities = new List<string> { "public", "zabbix" };

        public object GetSnmpData(string ipAddress, string oid, string community = "public")
        {
            var logger = new Action<string>(message => Console.WriteLine($"{DateTime.Now}: {message}"));
            logger($"Starting SNMP request to {ipAddress} for OID {oid}");

            try
            {
                var endpoint = new IPEndPoint(IPAddress.Parse(ipAddress), 161);
                const int timeout = 3000; // Уменьшил таймаут до 3 секунд
                var oidToQuery = new ObjectIdentifier(oid);
                var variables = new List<Variable> { new Variable(oidToQuery) };

                // 1. Проверка кэша соединений
                if (snmpConnectionCache.TryGetValue(ipAddress, out SnmpConnectionInfo connectionInfo))
                {
                    logger($"Using cached SNMP version {connectionInfo.Version} with community '{connectionInfo.Community}' for {ipAddress}");

                    try
                    {
                        var communityString = new OctetString(connectionInfo.Community);
                        var result = Messenger.Get(connectionInfo.Version,
                                              endpoint,
                                              communityString,
                                              variables,
                                              timeout);
                        if (result != null && result.Count > 0)
                        {
                            var data = result[0].Data;
                            logger($"SNMP data retrieved: {data?.ToString() ?? "null"}");
                            return data;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger($"Cached settings failed for {ipAddress}: {ex.Message}. Will try other options.");
                        snmpConnectionCache.Remove(ipAddress);
                    }
                }

                // 2. Формирование списка community для проверки без дубликатов и с приоритетом
                HashSet<string> communitiesToTry = new HashSet<string>();

                // Сначала добавляем переданный community
                if (!string.IsNullOrEmpty(community))
                {
                    communitiesToTry.Add(community);
                }

                // Затем остальные из известных
                foreach (var comm in knownCommunities)
                {
                    communitiesToTry.Add(comm);
                }

                // 3. Параллельное тестирование комбинаций версий и community
                var tasks = new List<Task<(bool Success, VersionCode Version, string Community, object Data)>>();

                foreach (var comm in communitiesToTry)
                {
                    // Запускаем параллельную проверку для SNMPv2
                    tasks.Add(Task.Run(() => TrySnmpGet(VersionCode.V2, endpoint, comm, variables, timeout / 2, logger)));

                    // Запускаем параллельную проверку для SNMPv1
                    tasks.Add(Task.Run(() => TrySnmpGet(VersionCode.V1, endpoint, comm, variables, timeout / 2, logger)));
                }

                // 4. Ожидаем первый успешный результат или завершение всех задач
                while (tasks.Any(t => !t.IsCompleted))
                {
                    // Проверяем, есть ли успешно завершенные задачи
                    var completedTask = tasks.FirstOrDefault(t => t.IsCompleted && !t.IsFaulted && t.Result.Success);
                    if (completedTask != null)
                    {
                        var result = completedTask.Result;

                        // Кэшируем успешное соединение
                        logger($"Successfully connected to {ipAddress} using SNMP{result.Version} with community '{result.Community}'");
                        snmpConnectionCache[ipAddress] = new SnmpConnectionInfo { Version = result.Version, Community = result.Community };

                        // Возвращаем данные
                        logger($"SNMP{result.Version} data retrieved: {result.Data?.ToString() ?? "null"}");
                        return result.Data;
                    }

                    // Небольшая задержка перед следующей проверкой
                    Thread.Sleep(50);
                }

                // Проверяем, есть ли успешные результаты среди завершенных задач
                var successfulTask = tasks.FirstOrDefault(t => !t.IsFaulted && t.Result.Success);
                if (successfulTask != null)
                {
                    var result = successfulTask.Result;
                    logger($"Successfully connected to {ipAddress} using SNMP{result.Version} with community '{result.Community}'");
                    snmpConnectionCache[ipAddress] = new SnmpConnectionInfo { Version = result.Version, Community = result.Community };
                    logger($"SNMP{result.Version} data retrieved: {result.Data?.ToString() ?? "null"}");
                    return result.Data;
                }

                logger($"No SNMP response from {ipAddress} for OID {oid} with any available community settings");
                return null;
            }
            catch (Exception ex)
            {
                logger($"SNMP error: {ex.Message} for OID {oid} on {ipAddress}");
                return null;
            }
        }

        // Вспомогательный метод для попытки получения SNMP-данных с указанной версией и community
        private (bool Success, VersionCode Version, string Community, object Data) TrySnmpGet(
            VersionCode version,
            IPEndPoint endpoint,
            string community,
            List<Variable> variables,
            int timeout,
            Action<string> logger)
        {
            try
            {
                var communityString = new OctetString(community);
                var result = Messenger.Get(version,
                                      endpoint,
                                      communityString,
                                      variables,
                                      timeout);

                if (result != null && result.Count > 0)
                {
                    return (true, version, community, result[0].Data);
                }
            }
            catch (Exception ex)
            {
                logger($"SNMP{version} with community '{community}' failed: {ex.Message}");
            }

            return (false, version, community, null);
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
                 new PrinterListItem { Ip = "192.168.1.100", Name = "Printer 1" },
                 new PrinterListItem { Ip = "192.168.1.101", Name = "Printer 2" },
            };
        }
    }
}
