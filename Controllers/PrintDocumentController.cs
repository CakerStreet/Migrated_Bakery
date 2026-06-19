using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CakerStreet.Business.Services;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Controllers
{
    [Route("print-document-ops")]
    public class PrintDocumentController : Controller
    {
        private readonly PrintDocumentService _printService;
        private readonly IConfiguration _config;

        public PrintDocumentController(PrintDocumentService printService, IConfiguration config)
        {
            _printService = printService;
            _config = config;
        }

        // ─── Purchase Order Print ──────────────────────────────────────────────────

        [HttpGet("printpurchaseorder")]
        [HttpGet("printpurchaseorder/{id:long}")]
        public async Task<IActionResult> PrintPurchaseOrder([FromQuery] long? id, [FromRoute] long? idRoute)
        {
            var poId = idRoute ?? id ?? 0L;
            if (poId <= 0)
                return BadRequest("Invalid Purchase Order ID.");

            // Auth check
            var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(webshopId))
                return Redirect("/businesslogin");

            // HQ-only check
            if (webshopId != "82")
                return Redirect("/businessorders");

            var result = await _printService.GetPurchaseOrderPrintAsync(poId);
            if (result == null)
                return NotFound("Purchase Order not found.");

            ViewBag.CdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
            return View("~/Views/PrintDocument/PurchaseOrder.cshtml", result);
        }

        // ─── Purchase Order Item Received Print ────────────────────────────────────

        [HttpGet("printpurchaseorderitemrecd")]
        [HttpGet("printpurchaseorderitemrecd/{id:long}")]
        public async Task<IActionResult> PrintPurchaseOrderItemReceived([FromQuery] long? id, [FromRoute] long? idRoute)
        {
            var itemsRecId = idRoute ?? id ?? 0L;
            if (itemsRecId <= 0)
                return BadRequest("Invalid Purchase Order Item Received ID.");

            // Auth check
            var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(webshopId))
                return Redirect("/businesslogin");

            // HQ-only check
            if (webshopId != "82")
                return Redirect("/businessorders");

            var result = await _printService.GetPurchaseOrderItemReceivedPrintAsync(itemsRecId);
            if (result == null)
                return NotFound("Purchase Order Item Received details not found.");

            ViewBag.CdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
            return View("~/Views/PrintDocument/PurchaseOrderItemReceived.cshtml", result);
        }

        // ─── Credit Note Print ─────────────────────────────────────────────────────

        [HttpGet("printcreditnote/{keyword}")]
        [HttpGet("printcreditnote")]
        public async Task<IActionResult> PrintCreditNote([FromRoute] string? keyword, [FromQuery] string? q)
        {
            var searchKey = keyword ?? q ?? HttpContext.Request.Query["keyword"].ToString();
            if (string.IsNullOrWhiteSpace(searchKey))
                return BadRequest("Invalid Credit Note parameter.");

            // Auth check
            var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(webshopId))
                return Redirect("/businesslogin");

            var result = await _printService.GetCreditNotePrintAsync(searchKey);
            if (result == null)
                return NotFound("Credit Note not found.");

            ViewBag.CdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
            return View("~/Views/PrintDocument/CreditNote.cshtml", result);
        }

        // ─── Franchise Checklist Print ─────────────────────────────────────────────

        [HttpGet("printFranchiseChecklist")]
        [HttpGet("printFranchiseChecklist/{checklistID:long}")]
        public async Task<IActionResult> PrintFranchiseChecklist([FromQuery] long? checklistID, [FromRoute] long? checklistIDRoute)
        {
            var cid = checklistIDRoute ?? checklistID ?? 0L;
            if (cid <= 0)
                return BadRequest("Invalid Checklist ID.");

            // Auth check
            var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(webshopId))
                return Redirect("/businesslogin");

            var result = await _printService.GetFranchiseChecklistPrintAsync(cid);
            if (result == null)
                return NotFound("Franchise Checklist not found.");

            ViewBag.CdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
            return View("~/Views/PrintDocument/FranchiseChecklist.cshtml", result);
        }
    }
}
