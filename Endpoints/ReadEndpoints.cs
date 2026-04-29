using EmailPlatform.Shared.Clients;

namespace EmailPlatform.Read.Endpoints;

/// <summary>
/// Public-facing read endpoints.
/// Uses X-Manager-Id header to scope results to the calling manager.
/// </summary>
public static class ReadEndpoints
{
    public const string ManagerIdHeader = "X-Manager-Id";

    public static IEndpointRouteBuilder MapReadEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/announcements").WithTags("Read");
        group.MapGet("/", ListAsync);
        group.MapGet("/{id}", GetAsync);
        return routes;
    }

    // ----------------------------------------------- GET /announcements

    private static async Task<IResult> ListAsync(
        HttpContext ctx,
        IStorageClient storage,
        int? limit,
        string? nextToken,
        CancellationToken ct)
    {
        if (!TryGetManagerId(ctx, out var managerId))
        {
            return Results.BadRequest(new { error = $"{ManagerIdHeader} header is required." });
        }

        var effectiveLimit = (limit is null || limit <= 0 || limit > 100) ? 20 : limit.Value;
        var page = await storage.ListByManagerAsync(managerId, effectiveLimit, nextToken, ct);
        return Results.Ok(page);
    }

    // ------------------------------------------- GET /announcements/{id}

    private static async Task<IResult> GetAsync(
        HttpContext ctx,
        string id,
        IStorageClient storage,
        ILogger<ReadMarker> log,
        CancellationToken ct)
    {
        if (!TryGetManagerId(ctx, out var managerId))
        {
            return Results.BadRequest(new { error = $"{ManagerIdHeader} header is required." });
        }

        var announcement = await storage.GetAsync(id, ct);
        if (announcement is null) return Results.NotFound();

        if (announcement.ManagerId != managerId)
        {
            log.LogWarning("Manager {ManagerId} attempted to read announcement {AnnouncementId} owned by {OwnerId}",
                managerId, id, announcement.ManagerId);
            return Results.NotFound(); // don't leak existence
        }

        return Results.Ok(announcement);
    }

    private static bool TryGetManagerId(HttpContext ctx, out string managerId)
    {
        if (ctx.Request.Headers.TryGetValue(ManagerIdHeader, out var values) &&
            !string.IsNullOrWhiteSpace(values.ToString()))
        {
            managerId = values.ToString();
            return true;
        }
        managerId = string.Empty;
        return false;
    }

    public sealed class ReadMarker { }
}
