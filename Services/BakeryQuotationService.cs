using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Services
{
    public class BakeryQuotationService
    {
        private readonly string _defaultConnection;
        private readonly string _staffAssessmentConnection;

        public BakeryQuotationService(IConfiguration config)
        {
            _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
            _staffAssessmentConnection = config.GetConnectionString("StaffAssessmentConnection") ?? "";
        }

        // ─── Get Quotation Requests ────────────────────────────────────────────────

        public async Task<List<BakeryQuotationRequest>> GetQuotationRequestsAsync(long bakeryId, string sortId, long? specificCrfId)
        {
            var list = new List<BakeryQuotationRequest>();
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            var sb = new StringBuilder();
            sb.Append(@"SELECT 
                c.CRF_ID, c.CRF_Name, c.CRF_EmailID, c.CRF_Contactno, c.CRF_Postcode, c.CRF_datetime, 
                c.CRF_Size, c.CRF_Cakedesc, c.CRF_remarks, c.CRF_image1, c.CRF_image2, c.CRF_image3, c.CRF_image4, 
                c.CRF_Alergyadvice, c.CRF_messageoncake, c.CRF_createdOn,
                sh.CakeShapeTitle, ty.CakeTypeTitle, cat.category_Name,
                ISNULL((SELECT TOP 1 order_ID FROM tbl_order inner join tbl_orderDetail on order_ID=orderDetail_orderID inner join tbl_crfQuote on crfQuote_prdID=orderDetail_productID WHERE crfQuote_CRFID=c.CRF_ID and order_isPurchased=1), 0) as OrderId,
                ISNULL((SELECT customer_name+' '+customer_surname FROM tbl_customers WHERE customer_ID=c.CRF_linkedto), '-') as CrfPerson
                FROM tbl_CRF c
                INNER JOIN tbl_lnkCRFtoBakery lnk ON c.CRF_ID = lnk.lnkCRFtoBakery_CRFID
                LEFT JOIN tbl_CakeShape sh ON c.CRF_ShapeID = sh.CakeShapeID
                LEFT JOIN tbl_CakeType ty ON c.CRF_typeID = ty.CakeTypeID
                LEFT JOIN tbl_category cat ON c.CRF_OccasionID = cat.category_ID
                WHERE lnk.lnkCRFtoBakery_isdeleted = 0 AND lnk.lnkCRFtoBakery_bakeryID = @bakeryId AND c.CRF_datetime > GETDATE()");

            if (specificCrfId.HasValue && specificCrfId.Value > 0)
            {
                sb.Append(" AND c.CRF_ID = @specificCrfId");
            }

            if (sortId == "2")
            {
                sb.Append(" ORDER BY c.CRF_datetime DESC");
            }
            else
            {
                sb.Append(" ORDER BY c.CRF_modifiedOn DESC");
            }

            await using var cmd = new SqlCommand(sb.ToString(), conn);
            cmd.Parameters.AddWithValue("@bakeryId", bakeryId);
            if (specificCrfId.HasValue && specificCrfId.Value > 0)
            {
                cmd.Parameters.AddWithValue("@specificCrfId", specificCrfId.Value);
            }

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new BakeryQuotationRequest
                {
                    CRF_ID = reader.GetInt64(0),
                    CRF_Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    CRF_EmailID = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    CRF_Contactno = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    CRF_Postcode = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    CRF_datetime = reader.IsDBNull(5) ? DateTime.MinValue : reader.GetDateTime(5),
                    CRF_Size = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    CRF_Cakedesc = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    CRF_remarks = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    CRF_image1 = reader.IsDBNull(9) ? "" : reader.GetString(9),
                    CRF_image2 = reader.IsDBNull(10) ? "" : reader.GetString(10),
                    CRF_image3 = reader.IsDBNull(11) ? "" : reader.GetString(11),
                    CRF_image4 = reader.IsDBNull(12) ? "" : reader.GetString(12),
                    CRF_Alergyadvice = reader.IsDBNull(13) ? "" : reader.GetString(13),
                    CRF_messageoncake = reader.IsDBNull(14) ? "" : reader.GetString(14),
                    CRF_createdOn = reader.IsDBNull(15) ? DateTime.MinValue : reader.GetDateTime(15),
                    ShapeTitle = reader.IsDBNull(16) ? "" : reader.GetString(16),
                    TypeTitle = reader.IsDBNull(17) ? "" : reader.GetString(17),
                    OccasionTitle = reader.IsDBNull(18) ? "" : reader.GetString(18),
                    OrderId = reader.GetInt64(19),
                    CrfPerson = reader.IsDBNull(20) ? "" : reader.GetString(20)
                });
            }

            return list;
        }

        // ─── Get Active Cake Sizes ──────────────────────────────────────────────────

        public async Task<List<BakerySizeModel>> GetSizesForBakeryAsync(long bakeryId)
        {
            var list = new List<BakerySizeModel>();
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            var sql = "SELECT SizeID, SizeTitle FROM tbl_CakeSize WHERE custid = @bakeryId AND IsActive = 1 ORDER BY DisplayOrder";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@bakeryId", bakeryId);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new BakerySizeModel
                {
                    SizeID = Convert.ToInt64(reader.GetValue(0)),
                    SizeTitle = reader.IsDBNull(1) ? "" : reader.GetString(1)
                });
            }

            return list;
        }

        public async Task<string> GetBakeryAddressAsync(long bakeryId)
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();
            var sql = @"SELECT webstore_address + ', ' + webstore_city + 
                        CASE WHEN ISNULL(webstore_State,'') <> '' THEN ', ' + webstore_State ELSE '' END + 
                        ', ' + webstore_postcode 
                        FROM tbl_webstore WHERE webstore_ID = @bakeryId";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@bakeryId", bakeryId);
            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString() ?? "";
        }


        // ─── Get Existing Quotes For Request ─────────────────────────────────────────

        public async Task<List<BakeryQuoteModel>> GetExistingQuotesForRequestAsync(long crfId, long bakeryId)
        {
            var list = new List<BakeryQuoteModel>();
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            var sql = @"SELECT 
                q.crfQuote_ID, q.crfQuote_modifiedOn, q.crfQuote_Remarks, q.crfQuote_Deliverycharges, q.crfQuote_QuotePrice,
                q.crfQuote_isdelivery, q.crfQuote_deliverymode, sz.SizeTitle, q.crfQuote_bakeryID, q.crfQuote_CRFID,
                q.crfQuote_isdeclined, q.crfQuote_isbakeryConfirmed, q.crfQuote_isbakerydeclined, q.crfQuote_iscounteroffer,
                q.crfQuote_counterofferrefquoteid,
                ISNULL(d.crfQuoteDecline_Remarks, '') as DeclineRemarks,
                ISNULL(d.crfQuoteDecline_reason, '') as DeclineReason,
                ISNULL(q.crfQuote_image1, '') as crfQuote_image1
                FROM tbl_crfQuote q
                LEFT JOIN tbl_CakeSize sz ON q.crfQuote_SizeID = sz.SizeID
                LEFT JOIN tbl_crfQuoteDecline d ON q.crfQuote_ID = d.crfQuoteDecline_quoteID AND q.crfQuote_isdeclined = 1 AND d.crfQuoteDecline_mode = 1
                WHERE q.crfQuote_CRFID = @crfId AND q.crfQuote_bakeryID = @bakeryId AND q.crfQuote_isdelete = 0
                ORDER BY q.crfQuote_modifiedOn DESC";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@crfId", crfId);
            cmd.Parameters.AddWithValue("@bakeryId", bakeryId);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new BakeryQuoteModel
                {
                    crfQuote_ID = reader.GetInt64(0),
                    crfQuote_modifiedOn = reader.IsDBNull(1) ? DateTime.MinValue : reader.GetDateTime(1),
                    crfQuote_Remarks = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    crfQuote_Deliverycharges = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                    crfQuote_QuotePrice = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                    crfQuote_isdelivery = !reader.IsDBNull(5) && reader.GetBoolean(5),
                    crfQuote_deliverymode = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                    SizeTitle = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    crfQuote_bakeryID = reader.GetInt64(8),
                    crfQuote_CRFID = reader.GetInt64(9),
                    crfQuote_isdeclined = !reader.IsDBNull(10) && reader.GetBoolean(10),
                    crfQuote_isbakeryConfirmed = !reader.IsDBNull(11) && reader.GetBoolean(11),
                    crfQuote_isbakerydeclined = !reader.IsDBNull(12) && reader.GetBoolean(12),
                    crfQuote_iscounteroffer = !reader.IsDBNull(13) && reader.GetBoolean(13),
                    crfQuote_counterofferrefquoteid = reader.IsDBNull(14) ? 0 : reader.GetInt64(14),
                    DeclineRemarks = reader.IsDBNull(15) ? "" : reader.GetString(15),
                    DeclineReason = reader.IsDBNull(16) ? "" : reader.GetString(16),
                    crfQuote_image1 = reader.IsDBNull(17) ? "" : reader.GetString(17)
                });
            }

            return list;
        }

        // ─── Custom Attributes Retrieval ─────────────────────────────────────────────

        public async Task<string> GetCustomAttributesHtmlAsync(long crfId)
        {
            var sb = new StringBuilder();
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            var sql = @"SELECT a.CRFAtt_ViewType, a.CRFAtt_datatext, f.FlavourTitle, p.FlavourTitle as ParentFlavourTitle
                FROM tbl_CRFAtt a
                LEFT JOIN tbl_CRFflavour f ON a.CRFAtt_AttID = f.FlavourID
                LEFT JOIN tbl_CRFflavour p ON a.CRFAtt_ParentattID = p.FlavourID
                WHERE a.CRFAtt_CRFID = @crfId";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@crfId", crfId);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var viewType = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                var dataText = reader.IsDBNull(1) ? "" : reader.GetString(1);
                var flavorTitle = reader.IsDBNull(2) ? "" : reader.GetString(2);
                var parentTitle = reader.IsDBNull(3) ? "" : reader.GetString(3);

                var value = viewType == 3 ? dataText : flavorTitle;

                if (!string.IsNullOrEmpty(parentTitle))
                {
                    sb.Append($"<ul><li class='parentli'>{parentTitle}</li><li class='data_li'>{value}</li></ul>");
                }
            }

            if (sb.Length > 0)
            {
                return $"<div class='Flavour_outer'>{sb}</div>";
            }

            return "";
        }

        // ─── Submit Bid Quote ───────────────────────────────────────────────────────

        public async Task<bool> SubmitQuoteAsync(long crfId, long bakeryId, BakeryQuoteInput input, int staffUserId, string businessName)
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            // Handle counter-offer / edit logic
            if (input.CounterOfferRefQuoteId > 0)
            {
                // Delete the old quote first
                var delSql = "UPDATE tbl_crfQuote SET crfQuote_isdelete = 1 WHERE crfQuote_ID = @oldId AND crfQuote_bakeryID = @bakeryId";
                await using var delCmd = new SqlCommand(delSql, conn);
                delCmd.Parameters.AddWithValue("@oldId", input.CounterOfferRefQuoteId);
                delCmd.Parameters.AddWithValue("@bakeryId", bakeryId);
                await delCmd.ExecuteNonQueryAsync();
            }

            // Set sizeID. If custom input, check or insert size title dynamically
            long sizeId = input.SizeId;
            if (sizeId == -1 && !string.IsNullOrWhiteSpace(input.CustomSize))
            {
                sizeId = await GetOrCreateCustomSizeIdAsync(input.CustomSize, bakeryId, conn);
            }

            // Get base details from CRF
            var imgSql = "SELECT CRF_image1, CRF_datetime, CRF_ShapeID FROM tbl_CRF WHERE CRF_ID = @crfId";
            string crfImage = "";
            DateTime crfDeliveryDate = DateTime.Now;
            int crfShapeId = 0;

            await using (var cmdImg = new SqlCommand(imgSql, conn))
            {
                cmdImg.Parameters.AddWithValue("@crfId", crfId);
                await using var reader = await cmdImg.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    crfImage = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    crfDeliveryDate = reader.IsDBNull(1) ? DateTime.Now : reader.GetDateTime(1);
                    crfShapeId = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                }
            }

            var now = DateTime.Now;
            // Parse valid till date
            DateTime validTillDate = now.AddDays(1);
            if (!string.IsNullOrEmpty(input.ValidTillDate))
            {
                var dtStr = $"{input.ValidTillDate} {input.ValidTillHour:D2}:{input.ValidTillMinute:D2}:00";
                if (DateTime.TryParseExact(dtStr, "dd/MM/yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedVal))
                {
                    validTillDate = parsedVal;
                }
                else if (DateTime.TryParse(dtStr, out parsedVal))
                {
                    validTillDate = parsedVal;
                }
            }

            SqlCommand cmd;
            if (input.QuoteId > 0 && input.CounterOfferRefQuoteId == 0) // Regular Edit
            {
                var updateSql = @"UPDATE tbl_crfQuote SET
                    crfQuote_QuotePrice = @price,
                    crfQuote_Remarks = @remarks,
                    crfQuote_SizeID = @sizeId,
                    crfQuote_Deliverycharges = @delCharges,
                    crfQuote_isdelivery = @isDel,
                    crfQuote_deliverymode = @delMode,
                    crfQuote_validtill = @validTill,
                    crfQuote_modifiedOn = GETDATE(),
                    crfQuote_isbakeryConfirmed = 1,
                    crfQuote_isbakerydeclined = 0
                    WHERE crfQuote_ID = @quoteId AND crfQuote_bakeryID = @bakeryId";

                cmd = new SqlCommand(updateSql, conn);
                cmd.Parameters.AddWithValue("@quoteId", input.QuoteId);
            }
            else // New quote or Counter offer
            {
                var insertSql = @"INSERT INTO tbl_crfQuote (
                    crfQuote_CRFID, crfQuote_bakeryID, crfQuote_QuotePrice, crfQuote_Remarks, crfQuote_SizeID,
                    crfQuote_Deliverycharges, crfQuote_isdelivery, crfQuote_deliverymode, crfQuote_validtill,
                    crfQuote_image1, crfQuote_isnewImage, crfQuote_deliverydate, crfQuote_ShapeID,
                    crfQuote_isdelete, crfQuote_isdeclined, crfQuote_CSMargin, crfQuote_CRMID,
                    crfQuote_isread, crfQuote_isbakeryConfirmed, crfQuote_isbakerydeclined,
                    crfQuote_counterofferrefquoteid, crfQuote_iscounteroffer, crfQuote_prdID, crfQuote_isopenforcustomer, crfQuote_modifiedOn
                ) VALUES (
                    @crfId, @bakeryId, @price, @remarks, @sizeId,
                    @delCharges, @isDel, @delMode, @validTill,
                    @image1, 0, @delDate, @shapeId,
                    0, 0, 0, 0,
                    0, 1, 0,
                    @counterRef, @isCounter, 0, 0, GETDATE()
                )";

                cmd = new SqlCommand(insertSql, conn);
                cmd.Parameters.AddWithValue("@crfId", crfId);
                cmd.Parameters.AddWithValue("@image1", crfImage);
                cmd.Parameters.AddWithValue("@delDate", crfDeliveryDate);
                cmd.Parameters.AddWithValue("@shapeId", crfShapeId);
                cmd.Parameters.AddWithValue("@counterRef", input.CounterOfferRefQuoteId);
                cmd.Parameters.AddWithValue("@isCounter", input.CounterOfferRefQuoteId > 0);
            }

            cmd.Parameters.AddWithValue("@bakeryId", bakeryId);
            cmd.Parameters.AddWithValue("@price", input.QuotePrice);
            cmd.Parameters.AddWithValue("@remarks", input.Remarks ?? "");
            cmd.Parameters.AddWithValue("@sizeId", (int)sizeId);
            cmd.Parameters.AddWithValue("@delCharges", input.IsDelivery ? input.DeliveryCharges : 0m);
            cmd.Parameters.AddWithValue("@isDel", input.IsDelivery);
            cmd.Parameters.AddWithValue("@delMode", input.IsDelivery ? 2 : 1);
            cmd.Parameters.AddWithValue("@validTill", validTillDate);

            var rows = await cmd.ExecuteNonQueryAsync();
            if (rows <= 0) return false;

            // Log email stub simulation to tbl_sms
            var recipient = "admin@cakerstreet.com";
            var emailSubject = input.CounterOfferRefQuoteId > 0
                ? $"New Counter offer placed on Quote Ref #{crfId} by {businessName}"
                : $"New quote placed by {businessName} (Ref #{crfId})";

            var emailBody = $"[SIMULATED EMAIL TO ADMIN] Subject: {emailSubject} | Details: Price: £{input.QuotePrice}, Remarks: {input.Remarks}, Size ID: {sizeId}";

            var smsSql = @"INSERT INTO tbl_sms (
                sms_custID, sms_mobileno, sms_text, sms_response, sms_typeID, sms_reqID, sms_createOn
            ) VALUES (
                @staffUserId, @recipient, @body, 'EMAIL_STUB_SENT', 11, @crfId, GETDATE()
            )";

            await using var cmdSms = new SqlCommand(smsSql, conn);
            cmdSms.Parameters.AddWithValue("@staffUserId", staffUserId);
            cmdSms.Parameters.AddWithValue("@recipient", recipient);
            cmdSms.Parameters.AddWithValue("@body", emailBody);
            cmdSms.Parameters.AddWithValue("@crfId", crfId);
            await cmdSms.ExecuteNonQueryAsync();

            return true;
        }

        private async Task<long> GetOrCreateCustomSizeIdAsync(string customSizeTitle, long bakeryId, SqlConnection conn)
        {
            var checkSql = "SELECT SizeID FROM tbl_CakeSize WHERE custid = @bakeryId AND SizeTitle = @title";
            await using (var cmd = new SqlCommand(checkSql, conn))
            {
                cmd.Parameters.AddWithValue("@bakeryId", bakeryId);
                cmd.Parameters.AddWithValue("@title", customSizeTitle);
                var result = await cmd.ExecuteScalarAsync();
                if (result != null) return Convert.ToInt64(result);
            }

            var insertSql = @"INSERT INTO tbl_CakeSize (
                SizeTitle, custid, IsActive, DisplayOrder, CakeSize_WebstoreCatID, CakeSize_Weight
            ) VALUES (
                @title, @bakeryId, 1, 99, 0, 0
            ); SELECT SCOPE_IDENTITY();";

            await using (var cmd = new SqlCommand(insertSql, conn))
            {
                cmd.Parameters.AddWithValue("@bakeryId", bakeryId);
                cmd.Parameters.AddWithValue("@title", customSizeTitle);
                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt64(result);
            }
        }

        // ─── Delete Quote ──────────────────────────────────────────────────────────

        public async Task<bool> DeleteQuoteAsync(long crfId, long bakeryId)
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            var sql = "UPDATE tbl_crfQuote SET crfQuote_isdelete = 1 WHERE crfQuote_CRFID = @crfId AND crfQuote_bakeryID = @bakeryId AND crfQuote_isdelete = 0";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@crfId", crfId);
            cmd.Parameters.AddWithValue("@bakeryId", bakeryId);

            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }

        // ─── Decline Custom Request ────────────────────────────────────────────────

        public async Task<bool> DeclineRequestAsync(long crfId, long bakeryId, string reason, string remarks, int staffUserId, string businessName)
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            // 1. Mark lnkCRFtoBakery as deleted
            var lnkSql = "UPDATE tbl_lnkCRFtoBakery SET lnkCRFtoBakery_isdeleted = 1 WHERE lnkCRFtoBakery_CRFID = @crfId AND lnkCRFtoBakery_bakeryID = @bakeryId";
            await using (var cmdLnk = new SqlCommand(lnkSql, conn))
            {
                cmdLnk.Parameters.AddWithValue("@crfId", crfId);
                cmdLnk.Parameters.AddWithValue("@bakeryId", bakeryId);
                await cmdLnk.ExecuteNonQueryAsync();
            }

            // 2. Add tbl_crfQuoteDecline
            var decSql = @"INSERT INTO tbl_crfQuoteDecline (
                crfQuoteDecline_CrfID, crfQuoteDecline_quoteID, crfQuoteDecline_custID, 
                crfQuoteDecline_reason, crfQuoteDecline_Remarks, crfQuoteDecline_mode, crfQuoteDecline_modifiedOn
            ) VALUES (
                @crfId, 0, @bakeryId, 
                @reason, @remarks, 2, GETDATE()
            )";

            await using (var cmdDec = new SqlCommand(decSql, conn))
            {
                cmdDec.Parameters.AddWithValue("@crfId", crfId);
                cmdDec.Parameters.AddWithValue("@bakeryId", bakeryId);
                cmdDec.Parameters.AddWithValue("@reason", reason);
                cmdDec.Parameters.AddWithValue("@remarks", remarks ?? "");
                await cmdDec.ExecuteNonQueryAsync();
            }

            // 3. Log SMS email simulation stub
            var recipient = "admin@cakerstreet.com";
            var subject = $"Customer's Quotation has been declined by {businessName} (Ref #{crfId})";
            var body = $"[SIMULATED EMAIL TO ADMIN] Subject: {subject} | Details: Reason: {reason}, Remarks: {remarks}";

            var smsSql = @"INSERT INTO tbl_sms (
                sms_custID, sms_mobileno, sms_text, sms_response, sms_typeID, sms_reqID, sms_createOn
            ) VALUES (
                @staffUserId, @recipient, @body, 'EMAIL_STUB_SENT', 11, @crfId, GETDATE()
            )";

            await using var cmdSms = new SqlCommand(smsSql, conn);
            cmdSms.Parameters.AddWithValue("@staffUserId", staffUserId);
            cmdSms.Parameters.AddWithValue("@recipient", recipient);
            cmdSms.Parameters.AddWithValue("@body", body);
            cmdSms.Parameters.AddWithValue("@crfId", crfId);
            await cmdSms.ExecuteNonQueryAsync();

            return true;
        }

        // ─── Accept Confirmation ───────────────────────────────────────────────────

        public async Task<bool> AcceptConfirmationAsync(long crfId, long bakeryId, long quoteId, int staffUserId, string businessName)
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            var sql = @"UPDATE tbl_crfQuote SET 
                crfQuote_isbakeryConfirmed = 1, 
                crfQuote_isopenforcustomer = 1 
                WHERE crfQuote_ID = @quoteId AND crfQuote_CRFID = @crfId AND crfQuote_bakeryID = @bakeryId";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@quoteId", quoteId);
            cmd.Parameters.AddWithValue("@crfId", crfId);
            cmd.Parameters.AddWithValue("@bakeryId", bakeryId);

            var rows = await cmd.ExecuteNonQueryAsync();
            if (rows <= 0) return false;

            // Log email stub
            var recipient = "admin@cakerstreet.com";
            var subject = $"Quote Confirmation has been Accepted by {businessName} (Ref #{crfId})";
            var body = $"[SIMULATED EMAIL TO ADMIN] Subject: {subject} | Quote ID: {quoteId}";

            var smsSql = @"INSERT INTO tbl_sms (
                sms_custID, sms_mobileno, sms_text, sms_response, sms_typeID, sms_reqID, sms_createOn
            ) VALUES (
                @staffUserId, @recipient, @body, 'EMAIL_STUB_SENT', 11, @crfId, GETDATE()
            )";

            await using var cmdSms = new SqlCommand(smsSql, conn);
            cmdSms.Parameters.AddWithValue("@staffUserId", staffUserId);
            cmdSms.Parameters.AddWithValue("@recipient", recipient);
            cmdSms.Parameters.AddWithValue("@body", body);
            cmdSms.Parameters.AddWithValue("@crfId", crfId);
            await cmdSms.ExecuteNonQueryAsync();

            return true;
        }

        // ─── Decline Confirmation ───────────────────────────────────────────────────

        public async Task<bool> DeclineConfirmationAsync(long crfId, long bakeryId, long quoteId, int staffUserId, string businessName)
        {
            await using var conn = new SqlConnection(_defaultConnection);
            await conn.OpenAsync();

            var sql = @"UPDATE tbl_crfQuote SET 
                crfQuote_isbakerydeclined = 1 
                WHERE crfQuote_ID = @quoteId AND crfQuote_CRFID = @crfId AND crfQuote_bakeryID = @bakeryId";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@quoteId", quoteId);
            cmd.Parameters.AddWithValue("@crfId", crfId);
            cmd.Parameters.AddWithValue("@bakeryId", bakeryId);

            var rows = await cmd.ExecuteNonQueryAsync();
            if (rows <= 0) return false;

            // Log email stub
            var recipient = "admin@cakerstreet.com";
            var subject = $"Quote Confirmation has been declined by {businessName} (Ref #{crfId})";
            var body = $"[SIMULATED EMAIL TO ADMIN] Subject: {subject} | Quote ID: {quoteId}";

            var smsSql = @"INSERT INTO tbl_sms (
                sms_custID, sms_mobileno, sms_text, sms_response, sms_typeID, sms_reqID, sms_createOn
            ) VALUES (
                @staffUserId, @recipient, @body, 'EMAIL_STUB_SENT', 11, @crfId, GETDATE()
            )";

            await using var cmdSms = new SqlCommand(smsSql, conn);
            cmdSms.Parameters.AddWithValue("@staffUserId", staffUserId);
            cmdSms.Parameters.AddWithValue("@recipient", recipient);
            cmdSms.Parameters.AddWithValue("@body", body);
            cmdSms.Parameters.AddWithValue("@crfId", crfId);
            await cmdSms.ExecuteNonQueryAsync();

            return true;
        }
    }

    // ─── Data Models ──────────────────────────────────────────────────────────────

    public class BakeryQuotationRequest
    {
        public long CRF_ID { get; set; }
        public string CRF_Name { get; set; } = "";
        public string CRF_EmailID { get; set; } = "";
        public string CRF_Contactno { get; set; } = "";
        public string CRF_Postcode { get; set; } = "";
        public DateTime CRF_datetime { get; set; }
        public string CRF_Size { get; set; } = "";
        public string CRF_Cakedesc { get; set; } = "";
        public string CRF_remarks { get; set; } = "";
        public string CRF_image1 { get; set; } = "";
        public string CRF_image2 { get; set; } = "";
        public string CRF_image3 { get; set; } = "";
        public string CRF_image4 { get; set; } = "";
        public string CRF_Alergyadvice { get; set; } = "";
        public string CRF_messageoncake { get; set; } = "";
        public DateTime CRF_createdOn { get; set; }
        public string ShapeTitle { get; set; } = "";
        public string TypeTitle { get; set; } = "";
        public string OccasionTitle { get; set; } = "";
        public long OrderId { get; set; }
        public string CrfPerson { get; set; } = "";
        public string CustomAttributesHtml { get; set; } = "";
        public List<BakeryQuoteModel> Quotes { get; set; } = new List<BakeryQuoteModel>();
    }

    public class BakerySizeModel
    {
        public long SizeID { get; set; }
        public string SizeTitle { get; set; } = "";
    }

    public class BakeryQuoteModel
    {
        public long crfQuote_ID { get; set; }
        public DateTime crfQuote_modifiedOn { get; set; }
        public string crfQuote_Remarks { get; set; } = "";
        public decimal crfQuote_Deliverycharges { get; set; }
        public decimal crfQuote_QuotePrice { get; set; }
        public bool crfQuote_isdelivery { get; set; }
        public int crfQuote_deliverymode { get; set; }
        public string SizeTitle { get; set; } = "";
        public long crfQuote_bakeryID { get; set; }
        public long crfQuote_CRFID { get; set; }
        public bool crfQuote_isdeclined { get; set; }
        public bool crfQuote_isbakeryConfirmed { get; set; }
        public bool crfQuote_isbakerydeclined { get; set; }
        public bool crfQuote_iscounteroffer { get; set; }
        public long crfQuote_counterofferrefquoteid { get; set; }
        public string DeclineRemarks { get; set; } = "";
        public string DeclineReason { get; set; } = "";
        public string crfQuote_image1 { get; set; } = "";
    }

    public class BakeryQuoteInput
    {
        public long QuoteId { get; set; }
        public decimal QuotePrice { get; set; }
        public string Remarks { get; set; } = "";
        public long SizeId { get; set; }
        public string CustomSize { get; set; } = "";
        public bool IsDelivery { get; set; }
        public decimal DeliveryCharges { get; set; }
        public string ValidTillDate { get; set; } = "";
        public int ValidTillHour { get; set; }
        public int ValidTillMinute { get; set; }
        public long CounterOfferRefQuoteId { get; set; }
    }
}
