using Microsoft.Data.SqlClient;
using System.Data;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CakerStreet.Business.Services;

// ─── Result Model ──────────────────────────────────────────────────────────────

public class RouteCalculationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public decimal Miles { get; set; }
    public decimal Seconds { get; set; }
    public decimal Charges { get; set; }
    public string? MapUrl { get; set; }
}

// ─── Internal Models ───────────────────────────────────────────────────────────

internal class RouteAddress
{
    public long RouteOrderId { get; set; }
    public string DestAddress { get; set; } = "";
}

// Google Routes API response models
internal class GoogleRoutesResponse
{
    [JsonPropertyName("routes")]
    public List<GoogleRouteItem>? Routes { get; set; }
}

internal class GoogleRouteItem
{
    [JsonPropertyName("distanceMeters")]
    public long DistanceMeters { get; set; }

    [JsonPropertyName("duration")]
    public string? Duration { get; set; }

    [JsonPropertyName("optimizedIntermediateWaypointIndex")]
    public List<int>? OptimizedIntermediateWaypointIndex { get; set; }
}

// Google Distance Matrix response models
internal class GoogleMatrixEntry
{
    [JsonPropertyName("originIndex")]
    public int OriginIndex { get; set; }

    [JsonPropertyName("destinationIndex")]
    public int DestinationIndex { get; set; }

    [JsonPropertyName("distanceMeters")]
    public long DistanceMeters { get; set; }

    [JsonPropertyName("duration")]
    public string? Duration { get; set; }

    [JsonPropertyName("status")]
    public JsonElement? Status { get; set; }

    [JsonPropertyName("condition")]
    public string? Condition { get; set; }
}

// ─── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Encapsulates the Google Routes API call and DB writes for route calculation.
/// Matches the legacy clsDeliveryRoute.CalculateRoute() logic.
/// Phase 2C — behind Mutations:DeliveryRouteCalculation:Enabled feature flag.
/// </summary>
public class RouteCalculationService
{
    private readonly string _businessConnectionString;
    private readonly string _defaultConnectionString;
    private readonly string _routesApiKey;
    private readonly string _originConfigAddress;
    private readonly HttpClient _httpClient;

    public RouteCalculationService(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _businessConnectionString = config.GetConnectionString("BusinessConnection") ?? "";
        _defaultConnectionString = config.GetConnectionString("DefaultConnection") ?? "";
        _routesApiKey = config["GoogleMaps:RoutesApiKey"] ?? "";
        _originConfigAddress = config["GoogleMaps:OriginAddress"] ?? "UB1 3AF";
        _httpClient = httpClientFactory.CreateClient();
    }

    /// <summary>
    /// Calculates optimal route via Google Routes API, updates sort order, charges, and API record.
    /// Returns: RouteCalculationResult with Success, Error, Miles, Seconds, Charges, MapUrl.
    /// </summary>
    public async Task<RouteCalculationResult> CalculateRouteAsync(long routeId, long templateId, bool returnToUnit, int userId, long webshopId)
    {
        try
        {
            // 1. Verify route exists
            bool routeExists = await RouteExistsAsync(routeId);
            if (!routeExists)
                return new RouteCalculationResult { Success = false, Error = "Route not found." };

            // 2. Get delivery addresses from tbl_deliveryRouteOrder joined with tbl_shippingDetail
            var addresses = await GetRouteAddressesAsync(routeId);
            if (addresses.Count == 0)
                return new RouteCalculationResult { Success = false, Error = "No delivery addresses found for this route." };

            // 3. Get origin address from tbl_webstore
            string originAddress = await GetOriginAddressAsync(webshopId);
            if (string.IsNullOrEmpty(originAddress))
                return new RouteCalculationResult { Success = false, Error = "Origin address not found for webshop." };

            // 4. Determine destination
            string destinationAddress;
            long excludeRouteOrderId = 0;
            int destIndex = -1;

            if (!returnToUnit)
            {
                // Call Distance Matrix to find farthest destination
                destIndex = await GetFarthestDestinationIndexAsync(originAddress, addresses);
                if (destIndex < 0 || destIndex >= addresses.Count)
                    return new RouteCalculationResult { Success = false, Error = "Failed to determine farthest destination from Distance Matrix API." };

                destinationAddress = addresses[destIndex].DestAddress;
                excludeRouteOrderId = addresses[destIndex].RouteOrderId;
            }
            else
            {
                destinationAddress = originAddress;
            }

            // 5. Build intermediates list (all addresses except the destination when returnToUnit=false)
            var intermediates = addresses
                .Where(a => a.RouteOrderId != excludeRouteOrderId)
                .ToList();

            // 6. Call Google Routes API (computeRoutes)
            var routeResponse = await ComputeRouteAsync(originAddress, intermediates, destinationAddress);
            if (routeResponse?.Routes == null || routeResponse.Routes.Count == 0)
                return new RouteCalculationResult { Success = false, Error = "Google Routes API returned no routes." };

            var route = routeResponse.Routes[0];

            // 7. Parse response
            double distanceMeters = route.DistanceMeters;
            double miles = ConvertMetersToMiles(distanceMeters);
            decimal seconds = ParseDurationSeconds(route.Duration);

            // 8. Update sort order in tbl_deliveryRouteOrder based on optimizedIntermediateWaypointIndex
            await UpdateSortOrderAsync(route.OptimizedIntermediateWaypointIndex, intermediates, excludeRouteOrderId, destIndex);

            // 9. Calculate charges using tbl_deliveryRouteChargesCalc
            decimal charges = await CalculateDeliveryChargesAsync(webshopId, miles, templateId);

            // 10. Generate Google Maps URL
            var intermediateAddresses = intermediates.Select(a => a.DestAddress).ToList();
            string mapsUrl = $"https://www.google.com/maps/dir/?api=1&origin={Uri.EscapeDataString(originAddress)}&destination={Uri.EscapeDataString(destinationAddress)}&waypoints={Uri.EscapeDataString(string.Join("|", intermediateAddresses))}&travelmode=driving";

            // 11. Upsert tbl_DeliveryRouteApi and update tbl_deliveryRoute
            await UpsertRouteApiAndUpdateRouteAsync(routeId, templateId, returnToUnit, userId, (decimal)miles, seconds, charges, mapsUrl);

            return new RouteCalculationResult
            {
                Success = true,
                Miles = Math.Round((decimal)miles, 2),
                Seconds = seconds,
                Charges = charges,
                MapUrl = mapsUrl
            };
        }
        catch (Exception ex)
        {
            return new RouteCalculationResult { Success = false, Error = $"Route calculation failed: {ex.Message}" };
        }
    }

    // ─── Private: DB Reads ─────────────────────────────────────────────────────

    private async Task<bool> RouteExistsAsync(long routeId)
    {
        await using var conn = new SqlConnection(_businessConnectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(
            "SELECT COUNT(1) FROM tbl_deliveryRoute WHERE route_ID = @routeId", conn);
        cmd.Parameters.AddWithValue("@routeId", routeId);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    /// <summary>
    /// Gets delivery addresses from tbl_deliveryRouteOrder joined with tbl_shippingDetail (cross-DB).
    /// Matches legacy GetRouteAddressesByRouteID exactly.
    /// </summary>
    private async Task<List<RouteAddress>> GetRouteAddressesAsync(long routeId)
    {
        var addresses = new List<RouteAddress>();

        await using var conn = new SqlConnection(_businessConnectionString);
        await conn.OpenAsync();

        // Cross-DB query: BusinessConnection → DefaultConnection (db_cakerstreet_live)
        await using var cmd = new SqlCommand(@"
            SELECT d.routeOrder_ID,
                   DestAddress = CONCAT_WS(', ',
                       NULLIF(s.shipping_address,''),
                       NULLIF(s.shipping_city,''),
                       NULLIF(s.shipping_county,''),
                       NULLIF(s.shipping_country,'')
                   ) + ' - ' + NULLIF(s.shipping_zip,'')
            FROM [dbo].[tbl_deliveryRouteOrder] d 
            INNER JOIN db_cakerstreet_live.dbo.tbl_shippingDetail s
                ON d.routeOrder_orderID = s.shipping_orderID
            WHERE d.routeOrder_routeID = @routeId", conn);
        cmd.CommandTimeout = 120;
        cmd.Parameters.AddWithValue("@routeId", routeId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var addr = reader.IsDBNull(1) ? "" : reader.GetString(1);
            // Remove newlines (matching legacy RemoveNewLines)
            addr = addr.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ").Trim();

            addresses.Add(new RouteAddress
            {
                RouteOrderId = reader.GetInt64(0),
                DestAddress = addr
            });
        }

        return addresses;
    }

    /// <summary>
    /// Gets origin address from tbl_webstore in DefaultConnection.
    /// Matches legacy GetOriginAddress exactly.
    /// </summary>
    private async Task<string> GetOriginAddressAsync(long webshopId)
    {
        await using var conn = new SqlConnection(_defaultConnectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(@"
            SELECT OriginAddress = webstore_businessName + ', ' + webstore_address + ', ' + webstore_city + ', United kingdom' + ' - ' + webstore_postcode 
            FROM tbl_webstore WHERE webstore_ID = @wid", conn);
        cmd.CommandTimeout = 120;
        cmd.Parameters.AddWithValue("@wid", webshopId);

        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString() ?? "";
    }

    // ─── Private: Google API Calls ─────────────────────────────────────────────

    /// <summary>
    /// Calls Google Distance Matrix API to find the farthest destination.
    /// Returns the index of the farthest destination (MAX destinationIndex).
    /// Matches legacy GetDestinationAddressFromComputeMatrix.
    /// </summary>
    private async Task<int> GetFarthestDestinationIndexAsync(string originAddress, List<RouteAddress> addresses)
    {
        var requestBody = new
        {
            origins = new[]
            {
                new { waypoint = new { address = originAddress } }
            },
            destinations = addresses.Select(a => new
            {
                waypoint = new { address = a.DestAddress, vehicleStopover = true }
            }).ToArray(),
            travelMode = "DRIVE",
            routingPreference = "TRAFFIC_UNAWARE"
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://routes.googleapis.com/distanceMatrix/v2:computeRouteMatrix");
        request.Content = content;
        request.Headers.Add("X-Goog-Api-Key", _routesApiKey);
        request.Headers.Add("X-Goog-FieldMask", "originIndex,destinationIndex,duration,distanceMeters,status,condition");

        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return -1;

        var responseJson = await response.Content.ReadAsStringAsync();
        var matrixEntries = JsonSerializer.Deserialize<List<GoogleMatrixEntry>>(responseJson);

        if (matrixEntries == null || matrixEntries.Count == 0)
            return -1;

        // Find the destination with MAX destinationIndex (legacy logic)
        int maxIndex = matrixEntries.Max(e => e.DestinationIndex);
        return maxIndex;
    }

    /// <summary>
    /// Calls Google Routes API (computeRoutes) with origin, intermediates, destination.
    /// Matches legacy ComputeRoute.
    /// </summary>
    private async Task<GoogleRoutesResponse?> ComputeRouteAsync(string originAddress, List<RouteAddress> intermediates, string destinationAddress)
    {
        var requestBody = new Dictionary<string, object>
        {
            ["origin"] = new { address = originAddress },
            ["destination"] = new { address = destinationAddress },
            ["intermediates"] = intermediates.Select(a => new { address = a.DestAddress }).ToArray(),
            ["travelMode"] = "DRIVE",
            ["routingPreference"] = "TRAFFIC_UNAWARE",
            ["optimizeWaypointOrder"] = true
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://routes.googleapis.com/directions/v2:computeRoutes");
        request.Content = content;
        request.Headers.Add("X-Goog-Api-Key", _routesApiKey);
        request.Headers.Add("X-Goog-FieldMask", "routes.distanceMeters,routes.duration,routes.optimized_intermediate_waypoint_index");

        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return null;

        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<GoogleRoutesResponse>(responseJson);
    }

    // ─── Private: DB Writes ────────────────────────────────────────────────────

    /// <summary>
    /// Updates sort order in tbl_deliveryRouteOrder based on optimizedIntermediateWaypointIndex.
    /// Matches legacy sort order update logic.
    /// </summary>
    private async Task UpdateSortOrderAsync(List<int>? waypointOrder, List<RouteAddress> intermediates, long excludeRouteOrderId, int destIndex)
    {
        await using var conn = new SqlConnection(_businessConnectionString);
        await conn.OpenAsync();
        await using var transaction = conn.BeginTransaction();

        try
        {
            if (waypointOrder != null)
            {
                foreach (var idx in waypointOrder)
                {
                    int safeIdx = idx < 0 ? 0 : (idx >= intermediates.Count ? intermediates.Count - 1 : idx);
                    var routeOrderId = intermediates[safeIdx].RouteOrderId;

                    await using var cmd = new SqlCommand(
                        "UPDATE tbl_deliveryRouteOrder SET routeOrder_sortNo = @sortNo WHERE routeOrder_ID = @routeOrderId",
                        conn, transaction);
                    cmd.Parameters.AddWithValue("@sortNo", idx);
                    cmd.Parameters.AddWithValue("@routeOrderId", routeOrderId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            // Set the destination (farthest point) sort order when returnToUnit=false
            if (excludeRouteOrderId > 0 && destIndex >= 0)
            {
                await using var destCmd = new SqlCommand(
                    "UPDATE tbl_deliveryRouteOrder SET routeOrder_sortNo = @sortNo WHERE routeOrder_ID = @routeOrderId",
                    conn, transaction);
                destCmd.Parameters.AddWithValue("@sortNo", destIndex);
                destCmd.Parameters.AddWithValue("@routeOrderId", excludeRouteOrderId);
                await destCmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Upserts tbl_DeliveryRouteApi and updates tbl_deliveryRoute.
    /// Matches legacy DB write logic exactly.
    /// </summary>
    private async Task UpsertRouteApiAndUpdateRouteAsync(
        long routeId, long templateId, bool returnToUnit, int userId,
        decimal miles, decimal seconds, decimal charges, string mapsUrl)
    {
        await using var conn = new SqlConnection(_businessConnectionString);
        await conn.OpenAsync();
        await using var transaction = conn.BeginTransaction();

        try
        {
            // Check if route already has an API record
            long existingApiId = 0;
            await using (var checkCmd = new SqlCommand(
                "SELECT ISNULL(route_ApiID, 0) FROM tbl_deliveryRoute WHERE route_ID = @routeId",
                conn, transaction))
            {
                checkCmd.Parameters.AddWithValue("@routeId", routeId);
                var result = await checkCmd.ExecuteScalarAsync();
                existingApiId = Convert.ToInt64(result ?? 0);
            }

            long routeApiId;

            if (existingApiId > 0)
            {
                // UPDATE existing tbl_DeliveryRouteApi
                await using var updateCmd = new SqlCommand(@"
                    UPDATE tbl_DeliveryRouteApi SET 
                        routeApi_modifiedOn = GETDATE(),
                        routeApi_modifiedBy = @userId,
                        routeApi_miles = @miles,
                        routeApi_seconds = @seconds,
                        routeApi_charges = @charges,
                        routeApi_url = @url
                    WHERE routeApi_ID = @apiId", conn, transaction);
                updateCmd.Parameters.AddWithValue("@userId", userId);
                updateCmd.Parameters.AddWithValue("@miles", miles);
                updateCmd.Parameters.AddWithValue("@seconds", seconds);
                updateCmd.Parameters.AddWithValue("@charges", charges);
                updateCmd.Parameters.AddWithValue("@url", mapsUrl);
                updateCmd.Parameters.AddWithValue("@apiId", existingApiId);
                await updateCmd.ExecuteNonQueryAsync();

                routeApiId = existingApiId;
            }
            else
            {
                // INSERT new tbl_DeliveryRouteApi
                await using var insertCmd = new SqlCommand(@"
                    INSERT INTO tbl_DeliveryRouteApi 
                        (routeApi_routeID, routeApi_modifiedOn, routeApi_modifiedBy, routeApi_miles, routeApi_seconds, routeApi_charges, routeApi_url)
                    VALUES (@routeId, GETDATE(), @userId, @miles, @seconds, @charges, @url);
                    SELECT SCOPE_IDENTITY();", conn, transaction);
                insertCmd.Parameters.AddWithValue("@routeId", routeId);
                insertCmd.Parameters.AddWithValue("@userId", userId);
                insertCmd.Parameters.AddWithValue("@miles", miles);
                insertCmd.Parameters.AddWithValue("@seconds", seconds);
                insertCmd.Parameters.AddWithValue("@charges", charges);
                insertCmd.Parameters.AddWithValue("@url", mapsUrl);

                var newId = await insertCmd.ExecuteScalarAsync();
                routeApiId = Convert.ToInt64(newId);
            }

            // UPDATE tbl_deliveryRoute: set route_ApiID, route_DriverCharges, route_returnToUnit, route_TemplateID
            await using (var routeCmd = new SqlCommand(@"
                UPDATE tbl_deliveryRoute SET 
                    route_ApiID = @apiId,
                    route_DriverCharges = @charges,
                    route_returnToUnit = @returnToUnit,
                    route_TemplateID = @templateId
                WHERE route_ID = @routeId", conn, transaction))
            {
                routeCmd.Parameters.AddWithValue("@apiId", routeApiId);
                routeCmd.Parameters.AddWithValue("@charges", charges);
                routeCmd.Parameters.AddWithValue("@returnToUnit", returnToUnit ? 1 : 0);
                routeCmd.Parameters.AddWithValue("@templateId", templateId);
                routeCmd.Parameters.AddWithValue("@routeId", routeId);
                await routeCmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ─── Private: Charges Calculation ──────────────────────────────────────────

    /// <summary>
    /// Calculates delivery charges using tiered pricing from tbl_deliveryRouteChargesCalc.
    /// Matches legacy DispatchDeliveryCharges exactly.
    /// </summary>
    private async Task<decimal> CalculateDeliveryChargesAsync(long webshopId, double miles, long templateId)
    {
        decimal deliveryCharges = 0;

        await using var conn = new SqlConnection(_businessConnectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(@"
            SELECT routeChargesCalc_maxDistance, routeChargesCalc_amount, routeChargesCalc_priceType
            FROM tbl_deliveryRouteChargesCalc
            WHERE routeChargesCalc_webstoreID = @webshopId AND routeChargesCalc_templateID = @templateId
            ORDER BY routeChargesCalc_maxDistance", conn);
        cmd.CommandTimeout = 120;
        cmd.Parameters.AddWithValue("@webshopId", webshopId);
        cmd.Parameters.AddWithValue("@templateId", templateId);

        var bands = new List<(double MaxDistance, decimal Amount, int PriceType)>();

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var maxDist = Convert.ToDouble(reader["routeChargesCalc_maxDistance"]);
            var amount = Convert.ToDecimal(reader["routeChargesCalc_amount"]);
            var priceType = Convert.ToInt32(reader["routeChargesCalc_priceType"]);
            bands.Add((maxDist, amount, priceType));
        }

        double calMiles = miles;
        double prevMile = 0;

        foreach (var band in bands)
        {
            if (calMiles >= band.MaxDistance)
            {
                // Distance exceeds this band — apply full band
                if (band.PriceType == 1)
                {
                    // Flat amount
                    deliveryCharges += band.Amount;
                }
                else
                {
                    // Per-mile rate × distance in band
                    if (band.Amount > 0)
                    {
                        deliveryCharges += Math.Round(((decimal)band.MaxDistance - (decimal)prevMile) * band.Amount, 2);
                    }
                }
            }
            else
            {
                // Distance falls within this band — apply partial
                if (band.PriceType == 1)
                {
                    // Flat amount
                    deliveryCharges += band.Amount;
                }
                else
                {
                    // Per-mile rate × remaining distance
                    if (band.Amount > 0)
                    {
                        deliveryCharges += Math.Round(((decimal)calMiles - (decimal)prevMile) * band.Amount, 2);
                    }
                }
                break;
            }

            prevMile = band.MaxDistance;
        }

        return deliveryCharges;
    }

    // ─── Private: Helpers ──────────────────────────────────────────────────────

    private static double ConvertMetersToMiles(double meters) => meters * 0.000621371;

    /// <summary>
    /// Parses duration string like "1234s" to decimal seconds.
    /// </summary>
    private static decimal ParseDurationSeconds(string? duration)
    {
        if (string.IsNullOrEmpty(duration))
            return 0;

        var cleaned = duration.Replace("s", "").Trim();
        return decimal.TryParse(cleaned, out var result) ? result : 0;
    }
}
