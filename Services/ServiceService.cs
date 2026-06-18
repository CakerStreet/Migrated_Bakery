using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CakerStreet.Business.Services;

public class ServiceItem
{
    public long ServiceId { get; set; }
    public int ServiceCatId { get; set; }
    public string Name { get; set; } = "";
    public string Desc { get; set; } = "";
    public decimal WsPrice { get; set; }
    public decimal MarketPrice { get; set; }
    public string Image { get; set; } = "";
    public string ImageResolution { get; set; } = "";
    public int RecurringOrOnline { get; set; }
    public int RecurringModeVal { get; set; }
    public string RecurringMode { get; set; } = "";
    public bool IsRecommend { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime ModifiedOn { get; set; }
    public string ChildCat { get; set; } = "";
    public string ParentCat { get; set; } = "";
}

public class ServiceImageItem
{
    public long ImageId { get; set; }
    public long ServiceId { get; set; }
    public string ImageName { get; set; } = "";
    public string ImageResolution { get; set; } = "";
    public bool IsDefaultImage { get; set; }
    public int ImgNo { get; set; }
    public int ImageType { get; set; }
}

public class ServiceListResult
{
    public List<ServiceItem> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

public class ServiceCategoryItem
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
}

public class ServiceService
{
    private readonly string _defaultConnection;

    public ServiceService(IConfiguration config)
    {
        _defaultConnection = config.GetConnectionString("DefaultConnection") ?? "";
    }

    public async Task<ServiceListResult> GetServicesAsync(
        int statusFilter,
        string searchKeyword,
        int page,
        int pageSize)
    {
        var result = new ServiceListResult();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand("USP_GetServices", conn);
        cmd.CommandType = CommandType.StoredProcedure;

        // Add parameters
        if (statusFilter == 1)
            cmd.Parameters.AddWithValue("@isActive", true);
        else if (statusFilter == 2)
            cmd.Parameters.AddWithValue("@isActive", false);
        else
            cmd.Parameters.AddWithValue("@isActive", DBNull.Value);

        if (!string.IsNullOrEmpty(searchKeyword))
            cmd.Parameters.AddWithValue("@search", searchKeyword.Trim());
        else
            cmd.Parameters.AddWithValue("@search", DBNull.Value);

        cmd.Parameters.AddWithValue("@PageNumber", page);
        cmd.Parameters.AddWithValue("@ProductsPerPage", pageSize);

        var outParam = new SqlParameter("@HowManyProducts", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output
        };
        cmd.Parameters.Add(outParam);

        var items = new List<ServiceItem>();
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                items.Add(new ServiceItem
                {
                    ServiceId = Convert.ToInt64(reader["Service_ID"]),
                    Name = Convert.ToString(reader["Service_Name"]),
                    WsPrice = reader["Ws_Price"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Ws_Price"]),
                    MarketPrice = reader["Market_Price"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Market_Price"]),
                    Image = Convert.ToString(reader["Service_Image"]),
                    ImageResolution = Convert.ToString(reader["Service_ImageResolution"]),
                    IsRecommend = reader["Is_Recommed"] != DBNull.Value && Convert.ToBoolean(reader["Is_Recommed"]),
                    RecurringOrOnline = reader["Recurring_or_Online"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Recurring_or_Online"]),
                    RecurringMode = Convert.ToString(reader["Recurring_Mode"]),
                    IsActive = reader["Is_Active"] != DBNull.Value && Convert.ToBoolean(reader["Is_Active"]),
                    DisplayOrder = reader["Display_Order"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Display_Order"]),
                    ModifiedOn = reader["Service_ModifiedOn"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["Service_ModifiedOn"]),
                    ChildCat = Convert.ToString(reader["child_cat"]),
                    ParentCat = Convert.ToString(reader["parent_cat"])
                });
            }
        }

        result.Items = items;
        result.TotalCount = outParam.Value != DBNull.Value ? Convert.ToInt32(outParam.Value) : 0;
        result.TotalPages = (int)Math.Ceiling((double)result.TotalCount / pageSize);

        return result;
    }

    public async Task<ServiceItem?> GetServiceByIdAsync(long id)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"
            SELECT s.Service_ID, s.Service_CatID, s.Service_Name, s.Service_Desc, s.Ws_Price, s.Market_Price, 
                   s.Service_Image, s.Service_ImageResolution, s.Recurring_or_Online, s.Recurring_Mode, 
                   s.Is_Recommed, s.Is_Active, s.Display_Order, s.Service_ModifiedOn 
            FROM tbl_Service s 
            WHERE s.Service_ID = @id AND s.Service_isdeleted = 0";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ServiceItem
            {
                ServiceId = Convert.ToInt64(reader["Service_ID"]),
                ServiceCatId = reader["Service_CatID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Service_CatID"]),
                Name = Convert.ToString(reader["Service_Name"]),
                Desc = Convert.ToString(reader["Service_Desc"]),
                WsPrice = reader["Ws_Price"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Ws_Price"]),
                MarketPrice = reader["Market_Price"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Market_Price"]),
                Image = Convert.ToString(reader["Service_Image"]),
                ImageResolution = Convert.ToString(reader["Service_ImageResolution"]),
                RecurringOrOnline = reader["Recurring_or_Online"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Recurring_or_Online"]),
                RecurringModeVal = reader["Recurring_Mode"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Recurring_Mode"]),
                IsRecommend = reader["Is_Recommed"] != DBNull.Value && Convert.ToBoolean(reader["Is_Recommed"]),
                IsActive = reader["Is_Active"] != DBNull.Value && Convert.ToBoolean(reader["Is_Active"]),
                DisplayOrder = reader["Display_Order"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Display_Order"]),
                ModifiedOn = reader["Service_ModifiedOn"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["Service_ModifiedOn"])
            };
        }
        return null;
    }

    public async Task<List<ServiceImageItem>> GetServiceImagesAsync(long serviceId)
    {
        var list = new List<ServiceImageItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"
            SELECT ServiceImage_ID, ServiceImage_serviceID, ServiceImage_imagename, 
                   ServiceImage_imageResolution, ServiceImage_isdefaultimage, ServiceImage_imgNo, 
                   ServiceImage_imagetype 
            FROM tbl_ServiceImage 
            WHERE ServiceImage_serviceID = @id AND ServiceImage_imagetype = 1 
            ORDER BY ServiceImage_imgNo";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", serviceId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new ServiceImageItem
            {
                ImageId = Convert.ToInt64(reader["ServiceImage_ID"]),
                ServiceId = Convert.ToInt64(reader["ServiceImage_serviceID"]),
                ImageName = Convert.ToString(reader["ServiceImage_imagename"]),
                ImageResolution = Convert.ToString(reader["ServiceImage_imageResolution"]),
                IsDefaultImage = reader["ServiceImage_isdefaultimage"] != DBNull.Value && Convert.ToBoolean(reader["ServiceImage_isdefaultimage"]),
                ImgNo = reader["ServiceImage_imgNo"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ServiceImage_imgNo"]),
                ImageType = reader["ServiceImage_imagetype"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ServiceImage_imagetype"])
            });
        }
        return list;
    }

    public async Task<List<ServiceCategoryItem>> GetCategoriesAsync(int parentId)
    {
        var list = new List<ServiceCategoryItem>();
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = @"
            SELECT category_id, category_name 
            FROM tbl_category 
            WHERE category_for = 3 AND category_isactive = 1 AND catgory_refCategoryID = @parentId 
            ORDER BY category_displayorder";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@parentId", parentId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new ServiceCategoryItem
            {
                CategoryId = Convert.ToInt32(reader.GetValue(0)),
                CategoryName = reader.GetString(1)
            });
        }
        return list;
    }

    public async Task<long> AddUpdateServiceAsync(ServiceItem model, List<ServiceImageItem> images, int userId)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        long serviceId = model.ServiceId;
        bool isNew = serviceId <= 0;

        if (isNew)
        {
            // Get display order
            var displayOrderSql = "SELECT COALESCE(MAX(Display_Order), 0) + 1 FROM tbl_Service";
            await using var doCmd = new SqlCommand(displayOrderSql, conn);
            int displayOrder = Convert.ToInt32(await doCmd.ExecuteScalarAsync());

            var insSql = @"
                INSERT INTO tbl_Service (
                    Service_CatID, Service_Name, Service_Desc, Ws_Price, Market_Price, 
                    Service_Image, Service_ImageResolution, Recurring_or_Online, Recurring_Mode, 
                    Is_Recommed, Is_Active, Service_isdeleted, Display_Order, 
                    Service_CreatedOn, Service_CreatedBy, Service_ModifiedOn, Service_ModifiedBy
                ) VALUES (
                    @catId, @name, @desc, @wsPrice, @marketPrice, 
                    @image, @imageResolution, @recurringOrOnline, @recurringMode, 
                    @isRecommend, 1, 0, @displayOrder, 
                    GETDATE(), @userId, GETDATE(), @userId
                );
                SELECT SCOPE_IDENTITY();";

            await using var cmd = new SqlCommand(insSql, conn);
            cmd.Parameters.AddWithValue("@catId", model.ServiceCatId);
            cmd.Parameters.AddWithValue("@name", model.Name ?? "");
            cmd.Parameters.AddWithValue("@desc", model.Desc ?? "");
            cmd.Parameters.AddWithValue("@wsPrice", model.WsPrice);
            cmd.Parameters.AddWithValue("@marketPrice", model.MarketPrice);
            cmd.Parameters.AddWithValue("@image", model.Image ?? "");
            cmd.Parameters.AddWithValue("@imageResolution", model.ImageResolution ?? "");
            cmd.Parameters.AddWithValue("@recurringOrOnline", model.RecurringOrOnline);
            cmd.Parameters.AddWithValue("@recurringMode", model.RecurringModeVal);
            cmd.Parameters.AddWithValue("@isRecommend", model.IsRecommend);
            cmd.Parameters.AddWithValue("@displayOrder", displayOrder);
            cmd.Parameters.AddWithValue("@userId", userId);

            serviceId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }
        else
        {
            var updSql = @"
                UPDATE tbl_Service 
                SET Service_CatID = @catId, 
                    Service_Name = @name, 
                    Service_Desc = @desc, 
                    Ws_Price = @wsPrice, 
                    Market_Price = @marketPrice, 
                    Service_Image = @image, 
                    Service_ImageResolution = @imageResolution, 
                    Recurring_or_Online = @recurringOrOnline, 
                    Recurring_Mode = @recurringMode, 
                    Is_Recommed = @isRecommend, 
                    Is_Active = @isActive, 
                    Service_ModifiedOn = GETDATE(), 
                    Service_ModifiedBy = @userId 
                WHERE Service_ID = @id";

            await using var cmd = new SqlCommand(updSql, conn);
            cmd.Parameters.AddWithValue("@catId", model.ServiceCatId);
            cmd.Parameters.AddWithValue("@name", model.Name ?? "");
            cmd.Parameters.AddWithValue("@desc", model.Desc ?? "");
            cmd.Parameters.AddWithValue("@wsPrice", model.WsPrice);
            cmd.Parameters.AddWithValue("@marketPrice", model.MarketPrice);
            cmd.Parameters.AddWithValue("@image", model.Image ?? "");
            cmd.Parameters.AddWithValue("@imageResolution", model.ImageResolution ?? "");
            cmd.Parameters.AddWithValue("@recurringOrOnline", model.RecurringOrOnline);
            cmd.Parameters.AddWithValue("@recurringMode", model.RecurringModeVal);
            cmd.Parameters.AddWithValue("@isRecommend", model.IsRecommend);
            cmd.Parameters.AddWithValue("@isActive", model.IsActive);
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@id", serviceId);

            await cmd.ExecuteNonQueryAsync();
        }

        // Handle Images
        var delSql = "DELETE FROM tbl_ServiceImage WHERE ServiceImage_serviceID = @id AND ServiceImage_imagetype = 1";
        await using (var delCmd = new SqlCommand(delSql, conn))
        {
            delCmd.Parameters.AddWithValue("@id", serviceId);
            await delCmd.ExecuteNonQueryAsync();
        }

        for (int i = 0; i < images.Count; i++)
        {
            var img = images[i];
            var insImgSql = @"
                INSERT INTO tbl_ServiceImage (
                    ServiceImage_serviceID, ServiceImage_imagename, ServiceImage_imageResolution, 
                    ServiceImage_isdefaultimage, ServiceImage_imgNo, ServiceImage_imagetype, ServiceImage_createdOn
                ) VALUES (
                    @id, @imageName, @imageResolution, 
                    @isDefault, @imgNo, 1, GETDATE()
                )";

            await using var imgCmd = new SqlCommand(insImgSql, conn);
            imgCmd.Parameters.AddWithValue("@id", serviceId);
            imgCmd.Parameters.AddWithValue("@imageName", img.ImageName ?? "");
            imgCmd.Parameters.AddWithValue("@imageResolution", img.ImageResolution ?? "");
            imgCmd.Parameters.AddWithValue("@isDefault", img.IsDefaultImage);
            imgCmd.Parameters.AddWithValue("@imgNo", i + 1);

            await imgCmd.ExecuteNonQueryAsync();
        }

        var defaultImg = images.FirstOrDefault(im => im.IsDefaultImage) ?? images.FirstOrDefault();
        if (defaultImg != null)
        {
            var updMainImgSql = "UPDATE tbl_Service SET Service_Image = @img, Service_ImageResolution = @res WHERE Service_ID = @id";
            await using var mainImgCmd = new SqlCommand(updMainImgSql, conn);
            mainImgCmd.Parameters.AddWithValue("@img", defaultImg.ImageName ?? "");
            mainImgCmd.Parameters.AddWithValue("@res", defaultImg.ImageResolution ?? "");
            mainImgCmd.Parameters.AddWithValue("@id", serviceId);
            await mainImgCmd.ExecuteNonQueryAsync();
        }

        return serviceId;
    }

    public async Task<bool> UpdateActiveStatusAsync(List<long> ids, bool isActive)
    {
        if (ids == null || ids.Count == 0) return false;
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = $"UPDATE tbl_Service SET Is_Active = @active, Service_ModifiedOn = GETDATE() WHERE Service_ID IN ({string.Join(",", ids)})";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@active", isActive);

        await cmd.ExecuteNonQueryAsync();
        return true;
    }

    public async Task<bool> BulkDeleteAsync(List<long> ids)
    {
        if (ids == null || ids.Count == 0) return false;
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = $"UPDATE tbl_Service SET Service_isdeleted = 1, Service_ModifiedOn = GETDATE() WHERE Service_ID IN ({string.Join(",", ids)})";
        await using var cmd = new SqlCommand(sql, conn);

        await cmd.ExecuteNonQueryAsync();
        return true;
    }

    public async Task<bool> DeleteServiceAsync(long id)
    {
        await using var conn = new SqlConnection(_defaultConnection);
        await conn.OpenAsync();

        var sql = "UPDATE tbl_Service SET Service_isdeleted = 1, Service_ModifiedOn = GETDATE() WHERE Service_ID = @id";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);

        await cmd.ExecuteNonQueryAsync();
        return true;
    }
}
