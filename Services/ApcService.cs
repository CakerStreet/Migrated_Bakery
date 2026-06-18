using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Services;

public class ApcOrderDetails
{
    public long OrderId { get; set; }
    public string DisplayOrderId { get; set; } = "";
    public long ForwardedOrderId { get; set; }
    public bool IsRepeat { get; set; }
    public string Remarks { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Address { get; set; } = "";
    public string Address1 { get; set; } = "";
    public string Address2 { get; set; } = "";
    public string Zip { get; set; } = "";
    public string City { get; set; } = "";
    public string County { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string ProductName { get; set; } = "";
    public double ProductPrice { get; set; }
    public long OrderDetailId { get; set; }
}

public class ApcBookingLog
{
    public DateTime CreatedOn { get; set; }
    public int Status { get; set; }
    public string Carrier => Status == 109 ? "HYPASHIP" : "DHL PARCEL";
}

public class ApcBakeryCredentials
{
    public string CompanyName { get; set; } = "";
    public string AddressLine1 { get; set; } = "";
    public string AddressLine2 { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string City { get; set; } = "";
    public string County { get; set; } = "";
    public string CountryCode { get; set; } = "";
    public string PersonName { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public string ApiEmailId { get; set; } = "";
    public string ApiPassword { get; set; } = "";
}

public class ApcService
{
    private readonly string _liveConnectionString;
    private readonly string _businessConnectionString;

    public ApcService(IConfiguration config)
    {
        _liveConnectionString = config.GetConnectionString("DefaultConnection") ?? "";
        _businessConnectionString = config.GetConnectionString("BusinessConnection") ?? "";
    }

    /// <summary>
    /// Loads delivery information and product details for a given orderID.
    /// </summary>
    public async Task<ApcOrderDetails?> GetOrderDetailsAsync(long orderId)
    {
        await using var conn = new SqlConnection(_liveConnectionString);
        await conn.OpenAsync();

        var sql = @"
            SELECT TOP 1 
                o.order_ID AS OrderId,
                o.order_forwardedorderid AS ForwardedOrderId,
                o.order_isrepeat AS IsRepeat,
                c.ordercollection_Remarks AS Remarks,
                s.shipping_fName AS FirstName,
                s.shipping_lName AS LastName,
                s.shipping_address AS Address,
                s.shipping_zip AS Zip,
                s.shipping_city AS City,
                s.shipping_county AS County,
                s.shipping_phone AS Phone,
                s.shipping_emailID AS Email,
                od.orderDetail_productName AS ProductName,
                od.orderDetail_price AS ProductPrice,
                od.orderDetail_ID AS OrderDetailId
            FROM tbl_order o
            LEFT JOIN tbl_ordercollection c ON o.order_ID = c.ordercollection_OrderID
            LEFT JOIN tbl_shippingdetail s ON o.order_ID = s.shipping_orderID
            LEFT JOIN tbl_orderDetail od ON o.order_ID = od.orderDetail_orderID
            WHERE o.order_ID = @OrderId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@OrderId", orderId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var details = new ApcOrderDetails
            {
                OrderId = Convert.ToInt64(reader["OrderId"]),
                ForwardedOrderId = reader["ForwardedOrderId"] != DBNull.Value ? Convert.ToInt64(reader["ForwardedOrderId"]) : 0,
                IsRepeat = reader["IsRepeat"] != DBNull.Value && Convert.ToBoolean(reader["IsRepeat"]),
                Remarks = reader["Remarks"] != DBNull.Value ? reader["Remarks"].ToString() ?? "" : "",
                FirstName = reader["FirstName"] != DBNull.Value ? reader["FirstName"].ToString() ?? "" : "",
                LastName = reader["LastName"] != DBNull.Value ? reader["LastName"].ToString() ?? "" : "",
                Address = reader["Address"] != DBNull.Value ? reader["Address"].ToString() ?? "" : "",
                Zip = reader["Zip"] != DBNull.Value ? reader["Zip"].ToString() ?? "" : "",
                City = reader["City"] != DBNull.Value ? reader["City"].ToString() ?? "" : "",
                County = reader["County"] != DBNull.Value ? reader["County"].ToString() ?? "" : "",
                Phone = reader["Phone"] != DBNull.Value ? reader["Phone"].ToString() ?? "" : "",
                Email = reader["Email"] != DBNull.Value ? reader["Email"].ToString() ?? "" : "",
                ProductName = reader["ProductName"] != DBNull.Value ? reader["ProductName"].ToString() ?? "" : "",
                ProductPrice = reader["ProductPrice"] != DBNull.Value ? Convert.ToDouble(reader["ProductPrice"]) : 0.0,
                OrderDetailId = reader["OrderDetailId"] != DBNull.Value ? Convert.ToInt64(reader["OrderDetailId"]) : 0
            };

            // Format Display Order ID
            if (details.IsRepeat)
                details.DisplayOrderId = details.ForwardedOrderId + "/ReOrd";
            else if (details.ForwardedOrderId > 0)
                details.DisplayOrderId = details.ForwardedOrderId + "/FWD";
            else
                details.DisplayOrderId = details.OrderId.ToString();

            // Split shipping address for Line 1 and Line 2
            var cleanAddr = details.Address.Replace("\r\n", ",").Replace("\n", ",").Replace(", ", ",");
            var parts = cleanAddr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                details.Address1 = parts[0];
                if (parts.Length > 1)
                {
                    details.Address2 = string.Join(", ", parts.Skip(1));
                }
            }

            return details;
        }

        return null;
    }

    /// <summary>
    /// Gets previous carrier booking records from the order log table.
    /// </summary>
    public async Task<List<ApcBookingLog>> GetBookingHistoryAsync(long orderId)
    {
        var logs = new List<ApcBookingLog>();
        await using var conn = new SqlConnection(_liveConnectionString);
        await conn.OpenAsync();

        var sql = @"
            SELECT orderlog_createdOn, orderlog_status
            FROM tbl_orderlog
            WHERE orderlog_orderID = @OrderId 
              AND (orderlog_status = 109 OR orderlog_status = 1091)
            ORDER BY orderlog_createdOn DESC";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@OrderId", orderId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            logs.Add(new ApcBookingLog
            {
                CreatedOn = Convert.ToDateTime(reader["orderlog_createdOn"]),
                Status = Convert.ToInt32(reader["orderlog_status"])
            });
        }

        return logs;
    }

    /// <summary>
    /// Gets carrier API credentials from the tbl_Bakery_APC table.
    /// </summary>
    public async Task<ApcBakeryCredentials?> GetBakeryApcDetailsAsync(long webshopId)
    {
        await using var conn = new SqlConnection(_businessConnectionString);
        await conn.OpenAsync();

        var sql = @"
            SELECT TOP 1 
                APC_CompanyName, APC_AddressLine1, APC_AddressLine2, APC_PostalCode, APC_City, 
                APC_County, APC_CountryCode, APC_PersonName, APC_PhoneNumber, APC_Contact_Email,
                APC_API_EmailId, APC_API_Password
            FROM tbl_Bakery_APC
            WHERE webstore_ID = @WebshopId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@WebshopId", webshopId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ApcBakeryCredentials
            {
                CompanyName = reader["APC_CompanyName"] != DBNull.Value ? reader["APC_CompanyName"].ToString() ?? "" : "",
                AddressLine1 = reader["APC_AddressLine1"] != DBNull.Value ? reader["APC_AddressLine1"].ToString() ?? "" : "",
                AddressLine2 = reader["APC_AddressLine2"] != DBNull.Value ? reader["APC_AddressLine2"].ToString() ?? "" : "",
                PostalCode = reader["APC_PostalCode"] != DBNull.Value ? reader["APC_PostalCode"].ToString() ?? "" : "",
                City = reader["APC_City"] != DBNull.Value ? reader["APC_City"].ToString() ?? "" : "",
                County = reader["APC_County"] != DBNull.Value ? reader["APC_County"].ToString() ?? "" : "",
                CountryCode = reader["APC_CountryCode"] != DBNull.Value ? reader["APC_CountryCode"].ToString() ?? "" : "",
                PersonName = reader["APC_PersonName"] != DBNull.Value ? reader["APC_PersonName"].ToString() ?? "" : "",
                PhoneNumber = reader["APC_PhoneNumber"] != DBNull.Value ? reader["APC_PhoneNumber"].ToString() ?? "" : "",
                ContactEmail = reader["APC_Contact_Email"] != DBNull.Value ? reader["APC_Contact_Email"].ToString() ?? "" : "",
                ApiEmailId = reader["APC_API_EmailId"] != DBNull.Value ? reader["APC_API_EmailId"].ToString() ?? "" : "",
                ApiPassword = reader["APC_API_Password"] != DBNull.Value ? reader["APC_API_Password"].ToString() ?? "" : ""
            };
        }

        return null;
    }

    /// <summary>
    /// Stubs APC carrier booking. Saves mock booking log, checklist, and reviews to DB.
    /// </summary>
    public async Task<string> BookApcStubAsync(long orderId, string displayOrderId, string serviceCode, string customerId, string personName, string instructions)
    {
        // 1. Write status logs and checklists inside DB
        await using var conn = new SqlConnection(_liveConnectionString);
        await conn.OpenAsync();

        // 1.1 Insert/Update tbl_orderreviews
        var checkReviewSql = "SELECT COUNT(1) FROM tbl_orderreviews WHERE orderreviews_orderID = @OrderId";
        await using var checkCmd = new SqlCommand(checkReviewSql, conn);
        checkCmd.Parameters.AddWithValue("@OrderId", orderId);
        var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;

        if (exists)
        {
            var updateReviewSql = @"
                UPDATE tbl_orderreviews 
                SET orderreviews_remarks = CONCAT(orderreviews_remarks, ' | APC Booked'),
                    orderreviews_modifiedOn = GETDATE(),
                    orderreviews_modifiedby = @CustomerId
                WHERE orderreviews_orderID = @OrderId";
            await using var updateCmd = new SqlCommand(updateReviewSql, conn);
            updateCmd.Parameters.AddWithValue("@OrderId", orderId);
            updateCmd.Parameters.AddWithValue("@CustomerId", Convert.ToInt64(customerId));
            await updateCmd.ExecuteNonQueryAsync();
        }
        else
        {
            var insertReviewSql = @"
                INSERT INTO tbl_orderreviews (orderreviews_orderID, orderreviews_stars, orderreviews_remarks, orderreviews_createdOn, orderreviews_modifiedOn, orderreviews_modifiedby) 
                VALUES (@OrderId, 0, 'APC Booked', GETDATE(), GETDATE(), @CustomerId)";
            await using var insertCmd = new SqlCommand(insertReviewSql, conn);
            insertCmd.Parameters.AddWithValue("@OrderId", orderId);
            insertCmd.Parameters.AddWithValue("@CustomerId", Convert.ToInt64(customerId));
            await insertCmd.ExecuteNonQueryAsync();
        }

        // 1.2 Insert tbl_lnkOrderChecklist2Order
        var checkChecklistSql = "SELECT COUNT(1) FROM tbl_lnkOrderChecklist2Order WHERE lnkOrderChecklist2Order_orderID = @OrderId AND lnkOrderChecklist2Order_checklistID = 10";
        await using var checkChkCmd = new SqlCommand(checkChecklistSql, conn);
        checkChkCmd.Parameters.AddWithValue("@OrderId", orderId);
        var chkExists = Convert.ToInt32(await checkChkCmd.ExecuteScalarAsync()) > 0;

        if (!chkExists)
        {
            // Load detail ID
            var getDetailIdSql = "SELECT TOP 1 orderDetail_ID FROM tbl_orderDetail WHERE orderDetail_orderID = @OrderId";
            await using var getDetailCmd = new SqlCommand(getDetailIdSql, conn);
            getDetailCmd.Parameters.AddWithValue("@OrderId", orderId);
            var detailIdObj = await getDetailCmd.ExecuteScalarAsync();
            var detailId = detailIdObj != null && detailIdObj != DBNull.Value ? Convert.ToInt64(detailIdObj) : 0;

            var insertChkSql = @"
                INSERT INTO tbl_lnkOrderChecklist2Order 
                (lnkOrderChecklist2Order_checklistID, lnkOrderChecklist2Order_orderID, lnkOrderChecklist2Order_orderDetailID, lnkOrderChecklist2Order_isDone, lnkOrderChecklist2Order_isexcluded, lnkOrderChecklist2Order_remarks, lnkOrderChecklist2Order_userID, lnkOrderChecklist2Order_modifiedOn)
                VALUES (10, @OrderId, @DetailId, 1, 0, 'APC Booked (Stubbed)', @CustomerId, GETDATE())";
            await using var insertChkCmd = new SqlCommand(insertChkSql, conn);
            insertChkCmd.Parameters.AddWithValue("@OrderId", orderId);
            insertChkCmd.Parameters.AddWithValue("@DetailId", detailId);
            insertChkCmd.Parameters.AddWithValue("@CustomerId", Convert.ToInt64(customerId));
            await insertChkCmd.ExecuteNonQueryAsync();
        }

        // 1.3 Insert order log
        var insertLogSql = @"
            INSERT INTO tbl_orderlog (orderlog_requestedby, orderlog_status, orderlog_orderID, orderlog_createdOn)
            VALUES (@CustomerId, 109, @OrderId, GETDATE())";
        await using var insertLogCmd = new SqlCommand(insertLogSql, conn);
        insertLogCmd.Parameters.AddWithValue("@CustomerId", Convert.ToInt64(customerId));
        insertLogCmd.Parameters.AddWithValue("@OrderId", orderId);
        await insertLogCmd.ExecuteNonQueryAsync();

        // 2. Return mock API payload details
        var mockWaybill = "APC" + new Random().Next(100000, 999999);
        var mockOrderNo = "APC-ORD-" + displayOrderId;

        return $@"Stubbed APC Carrier Booking Successful.
[Requires production credentials before go-live]
Message Code: SUCCESS
Message Description: Booking recorded in test environment.
Order Number: {mockOrderNo}
WayBill: {mockWaybill}
Product Code: {serviceCode}";
    }

    /// <summary>
    /// Stubs DHL Rates retrieval.
    /// </summary>
    public List<DhlRateQuote> GetDhlRatesStub(string postcode, string city, double price)
    {
        // Simply return 2 standard DHL Parcel rates for testing
        return new List<DhlRateQuote>
        {
            new DhlRateQuote { ServiceTypeCode = "UKMail_Express_UK", ServiceTypeName = "DHL Express Domestic Next Day", TotalCharge = 8.50 },
            new DhlRateQuote { ServiceTypeCode = "UKMail_Express_UK_AM", ServiceTypeName = "DHL Next Day Morning Delivery", TotalCharge = 12.50 }
        };
    }

    /// <summary>
    /// Stubs DHL Carrier booking. Saves mock booking log, checklist, and reviews to DB.
    /// </summary>
    public async Task<string> BookDhlStubAsync(long orderId, string displayOrderId, string serviceTypeCode, string customerId, string personName)
    {
        // 1. Write status logs and checklists inside DB
        await using var conn = new SqlConnection(_liveConnectionString);
        await conn.OpenAsync();

        // 1.1 Insert/Update tbl_orderreviews
        var checkReviewSql = "SELECT COUNT(1) FROM tbl_orderreviews WHERE orderreviews_orderID = @OrderId";
        await using var checkCmd = new SqlCommand(checkReviewSql, conn);
        checkCmd.Parameters.AddWithValue("@OrderId", orderId);
        var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;

        if (exists)
        {
            var updateReviewSql = @"
                UPDATE tbl_orderreviews 
                SET orderreviews_remarks = CONCAT(orderreviews_remarks, ' | DHL Booked'),
                    orderreviews_modifiedOn = GETDATE(),
                    orderreviews_modifiedby = @CustomerId
                WHERE orderreviews_orderID = @OrderId";
            await using var updateCmd = new SqlCommand(updateReviewSql, conn);
            updateCmd.Parameters.AddWithValue("@OrderId", orderId);
            updateCmd.Parameters.AddWithValue("@CustomerId", Convert.ToInt64(customerId));
            await updateCmd.ExecuteNonQueryAsync();
        }
        else
        {
            var insertReviewSql = @"
                INSERT INTO tbl_orderreviews (orderreviews_orderID, orderreviews_stars, orderreviews_remarks, orderreviews_createdOn, orderreviews_modifiedOn, orderreviews_modifiedby) 
                VALUES (@OrderId, 0, 'DHL Booked', GETDATE(), GETDATE(), @CustomerId)";
            await using var insertCmd = new SqlCommand(insertReviewSql, conn);
            insertCmd.Parameters.AddWithValue("@OrderId", orderId);
            insertCmd.Parameters.AddWithValue("@CustomerId", Convert.ToInt64(customerId));
            await insertCmd.ExecuteNonQueryAsync();
        }

        // 1.2 Insert tbl_lnkOrderChecklist2Order
        var checkChecklistSql = "SELECT COUNT(1) FROM tbl_lnkOrderChecklist2Order WHERE lnkOrderChecklist2Order_orderID = @OrderId AND lnkOrderChecklist2Order_checklistID = 11";
        await using var checkChkCmd = new SqlCommand(checkChecklistSql, conn);
        checkChkCmd.Parameters.AddWithValue("@OrderId", orderId);
        var chkExists = Convert.ToInt32(await checkChkCmd.ExecuteScalarAsync()) > 0;

        if (!chkExists)
        {
            var getDetailIdSql = "SELECT TOP 1 orderDetail_ID FROM tbl_orderDetail WHERE orderDetail_orderID = @OrderId";
            await using var getDetailCmd = new SqlCommand(getDetailIdSql, conn);
            getDetailCmd.Parameters.AddWithValue("@OrderId", orderId);
            var detailIdObj = await getDetailCmd.ExecuteScalarAsync();
            var detailId = detailIdObj != null && detailIdObj != DBNull.Value ? Convert.ToInt64(detailIdObj) : 0;

            var insertChkSql = @"
                INSERT INTO tbl_lnkOrderChecklist2Order 
                (lnkOrderChecklist2Order_checklistID, lnkOrderChecklist2Order_orderID, lnkOrderChecklist2Order_orderDetailID, lnkOrderChecklist2Order_isDone, lnkOrderChecklist2Order_isexcluded, lnkOrderChecklist2Order_remarks, lnkOrderChecklist2Order_userID, lnkOrderChecklist2Order_modifiedOn)
                VALUES (11, @OrderId, @DetailId, 1, 0, 'DHL Booked (Stubbed)', @CustomerId, GETDATE())";
            await using var insertChkCmd = new SqlCommand(insertChkSql, conn);
            insertChkCmd.Parameters.AddWithValue("@OrderId", orderId);
            insertChkCmd.Parameters.AddWithValue("@DetailId", detailId);
            insertChkCmd.Parameters.AddWithValue("@CustomerId", Convert.ToInt64(customerId));
            await insertChkCmd.ExecuteNonQueryAsync();
        }

        // 1.3 Insert order log
        var insertLogSql = @"
            INSERT INTO tbl_orderlog (orderlog_requestedby, orderlog_status, orderlog_orderID, orderlog_createdOn)
            VALUES (@CustomerId, 1091, @OrderId, GETDATE())";
        await using var insertLogCmd = new SqlCommand(insertLogSql, conn);
        insertLogCmd.Parameters.AddWithValue("@CustomerId", Convert.ToInt64(customerId));
        insertLogCmd.Parameters.AddWithValue("@OrderId", orderId);
        await insertLogCmd.ExecuteNonQueryAsync();

        // 2. Return mock API payload details
        var mockCollectionNo = "DHL-COLL-" + new Random().Next(200000, 899999);
        var mockTrackingNo = "DHL-TRACK-" + new Random().Next(5000000, 9999999);

        return $@"Stubbed DHL Carrier Booking Successful.
[Requires production credentials before go-live]
Message Description: DHL booking completed in test mode.
Collection Date Number: {mockCollectionNo}
Master Tracking No: {mockTrackingNo}
Service: {serviceTypeCode}";
    }

    /// <summary>
    /// Stubs Label generation by generating a simple dummy text document representing the PDF label.
    /// Saves it under wwwroot/upload/Labels/{orderNumber}.pdf
    /// </summary>
    public async Task<string> GenerateLabelStubAsync(string orderNumber)
    {
        var wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadDir = Path.Combine(wwwrootPath, "upload", "Labels");
        if (!Directory.Exists(uploadDir))
        {
            Directory.CreateDirectory(uploadDir);
        }

        var labelFilePath = Path.Combine(uploadDir, orderNumber + ".pdf");
        
        // Write a mock text content that behaves like a simple PDF document or text file.
        // To make it compile and act as a valid PDF stub:
        var pdfStub = $@"%PDF-1.4
%Stubbed label for Order: {orderNumber}
%[Requires production credentials to render carrier labels]
1 0 obj < < /Type /Catalog /Pages 2 0 R > > endobj
2 0 obj < < /Type /Pages /Kids [ 3 0 R ] /Count 1 > > endobj
3 0 obj < < /Type /Page /Parent 2 0 R /Resources < < /Font < < /F1 4 0 R > > > > /MediaBox [ 0 0 500 500 ] /Contents 5 0 R > > endobj
4 0 obj < < /Type /Font /Subtype /Type1 /BaseFont /Helvetica > > endobj
5 0 obj < < /Length 100 > > stream
BT
/F1 24 Tf
50 400 Td
(APC / DHL STUBBED LABEL FOR ORDER: {orderNumber}) Tj
ET
endstream
endobj
xref
0 6
0000000000 65535 f
0000000009 00000 n
0000000058 00000 n
0000000122 00000 n
0000000281 00000 n
0000000350 00000 n
trailer < < /Size 6 /Root 1 0 R > >
startxref
500
%%EOF";

        await File.WriteAllTextAsync(labelFilePath, pdfStub);
        return $"/upload/Labels/{orderNumber}.pdf";
    }

    /// <summary>
    /// Stubs tracking checking.
    /// </summary>
    public string TrackOrderStub(string recOrderId)
    {
        return $@"Stubbed Tracking Info for ID: {recOrderId}
[Requires production credentials before go-live]
Current Status: In Transit
Last Location: London Central Depot
Signed By: N/A
Updated At: {DateTime.Now:dd/MM/yyyy HH:mm}";
    }

    /// <summary>
    /// Stubs order cancellation.
    /// </summary>
    public string CancelOrderStub(string recOrderId)
    {
        return $@"Stubbed Cancellation Info for ID: {recOrderId}
[Requires production credentials before go-live]
Status: CANCELLED
Description: Shipment cancelled in carrier system.";
    }
}

public class DhlRateQuote
{
    public string ServiceTypeCode { get; set; } = "";
    public string ServiceTypeName { get; set; } = "";
    public double TotalCharge { get; set; }
}
