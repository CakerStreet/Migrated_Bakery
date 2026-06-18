using Microsoft.AspNetCore.Mvc;

namespace CakerStreet.Business.Controllers;

[Route("mailcheck")]
public class MailCheckController : Controller
{
    [HttpGet("")]
    public IActionResult Index([FromQuery] string? previewMode = null, [FromQuery] string orderId = "11885")
    {
        var html = "";

        if (!string.IsNullOrEmpty(previewMode))
        {
            html = previewMode.ToLower() switch
            {
                "subscribe" => GetSubscribeHtml(),
                "forgot" => GetForgotHtml(),
                "register" => GetRegisterHtml(),
                "forgot_bakery" => GetForgotBakeryHtml(),
                "register_business" => GetRegisterBusinessHtml(),
                "contactus" => GetContactUsHtml(),
                "ordersuccess" => GetOrderSuccessHtml(orderId),
                "orderpayout" => GetOrderPayoutHtml(orderId),
                "cancelled" => GetCancelledHtml(orderId),
                "trustpilot" => GetTrustPilotHtml(orderId),
                "coupon" => GetCouponHtml(),
                "emailforwarder" => GetEmailForwarderHtml(),
                _ => ""
            };
        }

        ViewBag.Html = html;
        ViewBag.PreviewMode = previewMode;
        ViewBag.OrderId = orderId;

        return View("~/Views/MailCheck/Index.cshtml");
    }

    private string GetSubscribeHtml()
    {
        return @"<div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ccc; max-width: 600px;'>
                    <h2 style='color: #db3736;'>Caker Street - Please update your account today</h2>
                    <p>Dear Kamal Narang,</p>
                    <p>We noticed your account details are incomplete. Please log in and update your account to continue receiving updates.</p>
                    <p><a href='http://localhost:5000' style='background-color: #db3736; color: white; padding: 10px 20px; text-decoration: none; border-radius: 4px;'>Update Account</a></p>
                    <p>Thanks,<br/>Caker Street Team</p>
                 </div>";
    }

    private string GetForgotHtml()
    {
        return @"<div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ccc; max-width: 600px;'>
                    <h2 style='color: #db3736;'>Caker Street - Password Recovery</h2>
                    <p>Dear Kamal Narang,</p>
                    <p>You requested to recover your password. Here are your credentials:</p>
                    <p><strong>Email:</strong> Kamalpreet.singh@itmltd.co.uk<br/><strong>Password:</strong> KaMaL</p>
                    <p>Thanks,<br/>Caker Street Support</p>
                 </div>";
    }

    private string GetForgotBakeryHtml()
    {
        return @"<div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ccc; max-width: 600px;'>
                    <h2 style='color: #db3736;'>Caker Street Bakery - Password Recovery</h2>
                    <p>Dear Kamal Narang,</p>
                    <p>Your password recovery request was received. Password: <strong>KaMaL</strong></p>
                 </div>";
    }

    private string GetRegisterHtml()
    {
        return @"<div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ccc; max-width: 600px;'>
                    <h2>Successfully Registered at Caker Street</h2>
                    <p>Dear Kamal Narang,</p>
                    <p>Welcome to Caker Street! Your Individual Account has been created successfully.</p>
                 </div>";
    }

    private string GetRegisterBusinessHtml()
    {
        return @"<div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ccc; max-width: 600px;'>
                    <h2>Successfully Registered at Caker Street</h2>
                    <p>Dear Kamal Narang,</p>
                    <p>Welcome to Caker Street! Your Business Customer Account has been created successfully.</p>
                 </div>";
    }

    private string GetContactUsHtml()
    {
        return @"<div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ccc; max-width: 600px;'>
                    <h3>--- Mail To Admin ---</h3>
                    <p><strong>Enquiry ID:</strong> 1<br/><strong>Name:</strong> Kamal Narang<br/><strong>Subject:</strong> General Enquiry<br/><strong>Message:</strong> This is just a test enquiry. Please Ignore.</p>
                    <hr/>
                    <h3>--- Mail To Customer ---</h3>
                    <p>Dear Kamal Narang, thank you for contacting us. We have received your enquiry (ID: 1) regarding General Enquiry. We will get back to you shortly.</p>
                 </div>";
    }

    private string GetOrderSuccessHtml(string orderId)
    {
        return $@"<div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ccc; max-width: 600px;'>
                    <h3 style='color: green;'>--- Mail To Customer & BCC Admin ---</h3>
                    <p><strong>Invoice for Order #{orderId}</strong></p>
                    <p>Thank you for shopping at Caker Street! Your payment has been received.</p>
                    <table style='width: 100%; border-collapse: collapse;'>
                        <tr style='background-color: #f2f2f2;'><th style='padding: 8px;'>Item</th><th style='padding: 8px;'>Qty</th><th style='padding: 8px;'>Price</th></tr>
                        <tr><td style='padding: 8px; border-bottom: 1px solid #ddd;'>Standard Birthday Cake</td><td style='padding: 8px; border-bottom: 1px solid #ddd;'>1</td><td style='padding: 8px; border-bottom: 1px solid #ddd;'>£45.00</td></tr>
                    </table>
                 </div>";
    }

    private string GetOrderPayoutHtml(string orderId)
    {
        return $@"<div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ccc; max-width: 600px;'>
                    <h3 style='color: blue;'>--- Mail To Bakery & BCC Admin ---</h3>
                    <p><strong>Order Payout Notification for Order #{orderId}</strong></p>
                    <p>The payouts for your fulfilled order #{orderId} have been calculated and processed.</p>
                    <p>Total Payout: <strong>£38.50</strong> (after commission & fees).</p>
                 </div>";
    }

    private string GetCancelledHtml(string orderId)
    {
        return $@"<div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ccc; max-width: 600px;'>
                    <h3>--- Mail To Customer & CC Bakery ---</h3>
                    <p><strong>Order Cancellation: #{orderId}</strong></p>
                    <p>We regret to inform you that your order #{orderId} has been cancelled. A refund will be processed to your account.</p>
                 </div>";
    }

    private string GetTrustPilotHtml(string orderId)
    {
        return $@"<div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ccc; max-width: 600px;'>
                    <h3>--- Mail To Customer & Trustpilot BCC ---</h3>
                    <p>Dear Customer, your order #{orderId} has been delivered! We value your feedback. Please take a moment to review us on Trustpilot.</p>
                 </div>";
    }

    private string GetCouponHtml()
    {
        return @"<div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ccc; max-width: 600px;'>
                    <h2>Your Caker Street Gift Coupon</h2>
                    <p>Dear Kamal Narang,</p>
                    <p>Here is your exclusive coupon code: <strong>123456</strong></p>
                    <p>Value: <strong>10% Off</strong><br/>Valid from today until next 30 days.</p>
                 </div>";
    }

    private string GetEmailForwarderHtml()
    {
        return @"<div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ccc; max-width: 600px;'>
                    <h2>Email Forwarder</h2>
                    <p>Your friend Kamal Narang has forwarded a product suggestion to you:</p>
                    <p><strong>MG-New Product</strong> (Code: Mg-123456)</p>
                    <p>Message: This is just a test enquiry. Please Ignore.</p>
                 </div>";
    }
}
