public static class ItemEndpoints
{
    public static void MapItemEndpoints(this WebApplication app)
    {
        // ── Lấy toàn bộ danh mục vật phẩm ──────────────────────────────
        app.MapGet("/api/catalog/items", (IItemCatalog catalog) =>
            Results.Ok(catalog.All));

        // ── Tra cứu vật phẩm theo ID (O(1) Hash Table Lookup) ──────────
        app.MapGet("/api/catalog/items/{id}", (string id, IItemCatalog catalog) =>
            catalog.TryGet(id, out var item)
                ? Results.Ok(item)
                : Results.NotFound(new ApiError($"Không tìm thấy vật phẩm với ID '{id}'.")));

        // ── Lấy toàn bộ công cụ ────────────────────────────────────────
        app.MapGet("/api/catalog/tools", (IItemCatalog catalog) =>
            Results.Ok(catalog.AllTools));

        // ── Tra cứu công cụ theo ID (O(1) Hash Table Lookup) ───────────
        app.MapGet("/api/catalog/tools/{id}", (string id, IItemCatalog catalog) =>
            catalog.TryGetTool(id, out var tool)
                ? Results.Ok(tool)
                : Results.NotFound(new ApiError($"Không tìm thấy công cụ với ID '{id}'.")));

        // ── Tra cứu vật phẩm theo chuỗi băm (Lookup Hash) ──────────────
        app.MapGet("/api/catalog/hash/{hash}", (string hash, IItemCatalog catalog) =>
            catalog.TryGetByHash(hash, out var item)
                ? Results.Ok(item)
                : Results.NotFound(new ApiError($"Không tìm thấy vật phẩm với mã băm '{hash}'.")));
    }
}
