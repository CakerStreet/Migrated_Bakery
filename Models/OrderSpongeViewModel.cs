namespace CakerStreet.Business.Models;

public class OrderSpongeViewModel
{
    public string FromDate { get; set; } = "";
    public string ToDate { get; set; } = "";
    public bool IncludeRequested { get; set; }
    public bool HasData { get; set; }
    public List<SpongeGridRow> Rows { get; set; } = new();
    public List<ShapeOption> Shapes { get; set; } = new();
    public List<string> Sizes { get; set; } = new();
    public List<string> SpongeTypes { get; set; } = new();
    public List<string> DietaryTypes { get; set; } = new();
}

public class SpongeGridRow
{
    public int RowId { get; set; }
    public int ProductTypeId { get; set; } // 1=Cake, 6=CupCake
    public string ProductTypeName => ProductTypeId == 1 ? "Cake" : "CupCake";
    public string Sponge { get; set; } = "";
    public string Dietary { get; set; } = "";
    public int ShapeId { get; set; }
    public string Shape { get; set; } = "";
    public string Size { get; set; } = "";
    public int Qty { get; set; }
    public int ReqQty { get; set; }
    public List<SpongeOrderThumb> OrderThumbs { get; set; } = new();
}

public class SpongeOrderThumb
{
    public long OrderId { get; set; }
    public string Image { get; set; } = "";
    public long ProductId { get; set; }
}

public class ShapeOption
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
}

public class SpongeOrderHistoryItem
{
    public long Id { get; set; }
    public DateTime ModifiedOn { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public int TotalQty { get; set; }
    public string Remarks { get; set; } = "";
    public string CreatedBy { get; set; } = "";
}

public class SpongeSubmitRequest
{
    public string FromDate { get; set; } = "";
    public string ToDate { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string OrderDate { get; set; } = "";
    public string DeliveryDate { get; set; } = "";
    public string Remarks { get; set; } = "";
    public bool SendMail { get; set; }
    public List<SpongeSubmitRow> Rows { get; set; } = new();
}

public class SpongeSubmitRow
{
    public List<long> OrderIds { get; set; } = new();
    public List<long> ProductIds { get; set; } = new();
    public int ProductTypeId { get; set; }
    public string Sponge { get; set; } = "";
    public string Dietary { get; set; } = "";
    public int ShapeId { get; set; }
    public string ShapeTitle { get; set; } = "";
    public string Size { get; set; } = "";
    public int OriginalQty { get; set; }
    public int RequestedQty { get; set; }
}

public class SpongeSubmitResult
{
    public bool Success { get; set; }
    public long SpongeOrderId { get; set; }
    public string? Error { get; set; }
}
