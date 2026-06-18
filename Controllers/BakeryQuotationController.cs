using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using CakerStreet.Business.Services;

namespace CakerStreet.Business.Controllers
{
    [Route("business-quotation")]
    [Route("crflist_forsbakery")]
    [Route("crflist_forsBakery.aspx")]
    public class BakeryQuotationController : Controller
    {
        private readonly BakeryQuotationService _quotationService;
        private readonly BakeryMenuService _menuService;
        private readonly IConfiguration _config;

        public BakeryQuotationController(
            BakeryQuotationService quotationService,
            BakeryMenuService menuService,
            IConfiguration config)
        {
            _quotationService = quotationService;
            _menuService = menuService;
            _config = config;
        }

        [HttpGet("")]
        [HttpGet("{id:long}")]
        public async Task<IActionResult> Index(long? id = null, [FromQuery] string sortid = "1")
        {
            var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
            var userType = HttpContext.Items["BakeryUserType"]?.ToString() ?? "";
            var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
            var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
            var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

            if (string.IsNullOrEmpty(webshopId) || userId == 0)
            {
                var returl = Request.Path + Request.QueryString;
                return Redirect($"/businesslogin?returl={Uri.EscapeDataString(returl)}");
            }

            var bakeryId = long.TryParse(webshopId, out var bid) ? bid : 0L;

            // Fetch quotation requests
            var requests = await _quotationService.GetQuotationRequestsAsync(bakeryId, sortid, id);

            // Fetch sizes and details for each request
            foreach (var req in requests)
            {
                req.Quotes = await _quotationService.GetExistingQuotesForRequestAsync(req.CRF_ID, bakeryId);
                req.CustomAttributesHtml = await _quotationService.GetCustomAttributesHtmlAsync(req.CRF_ID);
            }

            // Fetch active sizes for dropdown
            var sizes = await _quotationService.GetSizesForBakeryAsync(bakeryId);

            // Menu visibility
            var menuVisibility = await _menuService.GetMenuVisibilityAsync(userType, webshopId, userId);

            ViewBag.MenuVisibility = menuVisibility;
            ViewBag.BusinessName = businessName;
            ViewBag.UserName = userName;
            ViewBag.CdnBase = _config["CdnBase"] ?? "https://cakerstreet1.s3.amazonaws.com/";
            ViewBag.HdGlobalUrl = "http://localhost:5202";
            ViewBag.HdCustGlobalUrl = "http://localhost:5000";
            ViewBag.HdCRMGlobalUrl = "http://localhost:27201";

            ViewBag.Requests = requests;
            ViewBag.Sizes = sizes;
            ViewBag.SortId = sortid;
            ViewBag.SpecificId = id;
            ViewBag.BakeryId = bakeryId;
            ViewBag.BusinessAddress = await _quotationService.GetBakeryAddressAsync(bakeryId);

            return View("~/Views/BakeryQuotation/Index.cshtml");
        }

        [HttpPost("submit")]
        public async Task<IActionResult> Submit([FromBody] BakeryQuoteInput input, [FromQuery] long crfId)
        {
            var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
            var userName = HttpContext.Items["BakeryUserName"]?.ToString() ?? "";
            var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
            var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

            if (string.IsNullOrEmpty(webshopId) || userId == 0)
                return Json(new { success = false, message = "Unauthorized" });

            var bakeryId = long.TryParse(webshopId, out var bid) ? bid : 0L;

            var success = await _quotationService.SubmitQuoteAsync(crfId, bakeryId, input, userId, businessName);
            if (success)
            {
                return Json(new { success = true, message = "Thanks for submitting your Quote. Once this quote is approved by the customer, you will be notified." });
            }
            return Json(new { success = false, message = "Failed to submit quote. Please check your inputs." });
        }

        [HttpPost("delete")]
        public async Task<IActionResult> Delete([FromForm] long crfId)
        {
            var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
            var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

            if (string.IsNullOrEmpty(webshopId) || userId == 0)
                return Json(new { success = false, message = "Unauthorized" });

            var bakeryId = long.TryParse(webshopId, out var bid) ? bid : 0L;

            var success = await _quotationService.DeleteQuoteAsync(crfId, bakeryId);
            if (success)
            {
                return Json(new { success = true, message = "Your Quote has been removed." });
            }
            return Json(new { success = false, message = "Failed to remove quote." });
        }

        [HttpPost("decline")]
        public async Task<IActionResult> Decline([FromForm] long crfId, [FromForm] string reason, [FromForm] string remarks)
        {
            var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
            var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
            var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

            if (string.IsNullOrEmpty(webshopId) || userId == 0)
                return Json(new { success = false, message = "Unauthorized" });

            var bakeryId = long.TryParse(webshopId, out var bid) ? bid : 0L;

            var success = await _quotationService.DeclineRequestAsync(crfId, bakeryId, reason, remarks, userId, businessName);
            if (success)
            {
                return Json(new { success = true, message = "Quote has been declined successfully." });
            }
            return Json(new { success = false, message = "Failed to decline quote." });
        }

        [HttpPost("accept-confirmation")]
        public async Task<IActionResult> AcceptConfirmation([FromForm] long crfId, [FromForm] long quoteId)
        {
            var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
            var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
            var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

            if (string.IsNullOrEmpty(webshopId) || userId == 0)
                return Json(new { success = false, message = "Unauthorized" });

            var bakeryId = long.TryParse(webshopId, out var bid) ? bid : 0L;

            var success = await _quotationService.AcceptConfirmationAsync(crfId, bakeryId, quoteId, userId, businessName);
            if (success)
            {
                return Json(new { success = true, message = "Quote confirmation accepted successfully." });
            }
            return Json(new { success = false, message = "Failed to accept quote confirmation." });
        }

        [HttpPost("decline-confirmation")]
        public async Task<IActionResult> DeclineConfirmation([FromForm] long crfId, [FromForm] long quoteId)
        {
            var webshopId = HttpContext.Items["BakeryWebshopId"]?.ToString() ?? "";
            var businessName = HttpContext.Items["BakeryBusinessName"]?.ToString() ?? "";
            var userId = HttpContext.Items["BakeryUserId"] is int uid ? uid : 0;

            if (string.IsNullOrEmpty(webshopId) || userId == 0)
                return Json(new { success = false, message = "Unauthorized" });

            var bakeryId = long.TryParse(webshopId, out var bid) ? bid : 0L;

            var success = await _quotationService.DeclineConfirmationAsync(crfId, bakeryId, quoteId, userId, businessName);
            if (success)
            {
                return Json(new { success = true, message = "Quote confirmation declined." });
            }
            return Json(new { success = false, message = "Failed to decline quote confirmation." });
        }
    }
}
