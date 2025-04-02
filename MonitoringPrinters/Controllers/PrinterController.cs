using Microsoft.AspNetCore.Mvc;
using MonitoringPrinters.Model.PrinterModel;
using MonitoringPrinters.Model;
using MonitoringPrinters.Service;

namespace MonitoringPrinters.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PrinterController : ControllerBase
    {
        private readonly PrinterService _printerService;

        public PrinterController(PrinterService printerService)
        {
            _printerService = printerService;
        }

        [HttpGet("{ipAddress}")]
        public async Task<ActionResult<PrinterInfo>> GetPrinterMetrics(string ipAddress)
        {
            var printerInfo = await _printerService.GetPrinterInfoAsync(ipAddress);
            return Ok(printerInfo);
        }

        [HttpGet("{ipAddress}/status")]
        public async Task<ActionResult> GetPrinterStatus(string ipAddress)
        {
            var printerInfo = await _printerService.GetPrinterInfoAsync(ipAddress);
            return Ok(new
            {
                IpAddress = ipAddress,
                Timestamp = printerInfo.Timestamp,
                Status = printerInfo.Status
            });
        }

        [HttpGet("{ipAddress}/consumables")]
        public async Task<ActionResult> GetPrinterConsumables(string ipAddress)
        {
            var printerInfo = await _printerService.GetPrinterInfoAsync(ipAddress);
            return Ok(new
            {
                IpAddress = ipAddress,
                Timestamp = printerInfo.Timestamp,
                Consumables = printerInfo.Consumables
            });
        }

        [HttpGet]
        public ActionResult<List<PrinterListItem>> GetAllPrinters()
        {
            var printers = _printerService.GetAllPrinters();
            return Ok(printers);
        }
    }
}
