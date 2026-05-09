# InventoryHub – Reflective Summary

## Overview

This document describes all integration work performed on the InventoryHub full-stack application, which consists of a Blazor WebAssembly front-end (`ClientApp`) and a .NET Minimal API back-end (`ServerApp`). Each section covers an issue that was deliberately introduced or discovered, the symptoms it produced, and how it was resolved or gracefully handled.

---

## 1. Incorrect API Route

**Error introduced:** The front-end was calling `/api/products` while the back-end endpoint was mapped at `/api/productlist`.

**Symptoms:** The HTTP call returned a 404 Not Found, causing the product list to never load and triggering the error path in the component.

**Resolution:** The `MapGet` route in `ServerApp/Program.cs` was renamed from `/api/products` to `/api/productlist`, and the `Http.GetAsync` call in `FetchProducts.razor` was updated to match.

**Copilot's role:** Copilot located both sides of the mismatch simultaneously by searching the workspace, then applied both edits in a single pass, reducing the risk of leaving one side out of sync.

---

## 2. Friendly Error Message for API Unavailability

**Scenario:** With the route still pointing to a wrong address, requests would fail with an `HttpRequestException` or a non-success status code. The original messages were technical and unsuitable for end users.

**Resolution:** All error branches (`HttpRequestException`, `TaskCanceledException`, non-success HTTP status) were consolidated to display a single user-friendly message:

> _"API temporariamente indisponível. Tente novamente em alguns instantes."_

The message is stored in a constant (`TemporaryApiUnavailableMessage`) so it can be updated in one place.

**Copilot's role:** Copilot identified the multiple catch blocks and the HTTP status check, unified them under a shared constant, and preserved the `Console.WriteLine` log for developer diagnostics without leaking it to the UI.

---

## 3. CORS Misconfiguration

**Error introduced:** The back-end registered a named CORS policy (`"ClientApp"`) with `WithOrigins(...)` but the `app.UseCors(...)` middleware was later replaced with the inline lambda form using `AllowAnyOrigin()`, `AllowAnyMethod()`, and `AllowAnyHeader()`.

**Symptoms:** Requests from the Blazor app would be blocked by the browser with a CORS policy error whenever the origin did not match the hard-coded list.

**Resolution:** The named policy registration (`builder.Services.AddCors(options => ...)`) was removed and replaced with `builder.Services.AddCors()` (no named policy), while the inline middleware correctly applies the permissive policy to all routes.

**Copilot's role:** Copilot identified the dead named-policy registration and generated the corrected middleware configuration in the exact format requested, keeping the service registration minimal and consistent.

---

## 4. JSON Deserialization Error Handling

**Scenario:** If the API returned an unexpected payload (wrong shape, malformed JSON, or a plain error string), `JsonSerializer.Deserialize<Product[]>` would throw a `JsonException` and crash the component silently.

**Resolution:** The deserialization block in `OnInitializedAsync` was wrapped in a `try-catch (Exception ex)` that logs the error to the console and shows the friendly unavailability message in the UI — consistent with the API error treatment.

**Copilot's role:** Copilot generated the full `try-catch` block using `GetAsync` + `EnsureSuccessStatusCode` + `ReadAsStringAsync` + `JsonSerializer.Deserialize`, matching the exact pattern requested while also integrating the existing friendly message constant.

---

## 5. Case-Sensitivity in JSON Deserialization

**Issue discovered:** The .NET Minimal API serializes C# record properties using PascalCase by default (`Id`, `Name`, `Price`, `Stock`). `System.Text.Json` is case-sensitive by default, meaning a JSON payload with lowercase keys (`id`, `name`, etc.) would deserialize to empty/default values without throwing any exception.

**Resolution:** A shared `JsonSerializerOptions` instance with `PropertyNameCaseInsensitive = true` was added as a `static readonly` field in `ProductCatalogService`, and passed to every `Deserialize` call.

**Copilot's role:** Copilot explained the distinction between a syntactically malformed JSON (which would throw) and a structurally incompatible JSON (which silently produces empty objects), and generated the options object in the correct location to avoid repeated instantiation.

---

## 6. Standardized and Nested JSON Response

**Enhancement:** The product endpoint originally returned a flat anonymous object. The response was enriched to include a nested `category` object per product, following a more realistic and industry-standard API shape.

**Before:**
```json
{ "id": 1, "name": "Laptop", "price": 1200.5, "stock": 25 }
```

**After:**
```json
{
  "id": 1,
  "name": "Laptop",
  "price": 1200.50,
  "stock": 25,
  "category": { "id": 101, "name": "Electronics" }
}
```

**Resolution:** Anonymous objects were replaced with typed `record` definitions (`ProductResponse`, `CategoryResponse`) in `ProductCatalog.cs`, and the `Product` model in the front-end was extended with a `Category` property. The rendered list was updated to display the category name alongside each product.

**Copilot's role:** Copilot generated both the back-end records and the front-end model class in a single operation, then updated the Razor template to project the new field without changing any other part of the component.

---

## 7. Redundant API Calls and Performance Optimization

**Issue:** Every navigation to `/fetchproducts` triggered a new HTTP request to the back-end, even though the catalog data is static. On the server side, the payload array was re-instantiated on every request.

**Resolution — Client-side cache (`ProductCatalogService`):**
- A scoped service (`ClientApp/Services/ProductCatalogService.cs`) was created to own all product-fetching logic.
- It caches the deserialized `Product[]` for 5 minutes using a `DateTimeOffset` expiry guard.
- A `SemaphoreSlim(1,1)` prevents concurrent requests from all fetching simultaneously when the cache is cold (double-checked locking pattern).
- The component (`FetchProducts.razor`) was simplified to a single `await ProductCatalogService.GetProductsAsync(...)` call; `JsonSerializerOptions` and HTTP logic were moved entirely into the service.

**Resolution — Server-side cache (`IMemoryCache`):**
- `builder.Services.AddMemoryCache()` was added to the back-end DI container.
- The `MapGet` handler receives `IMemoryCache` via parameter injection and uses `cache.GetOrCreateAsync("productlist", ...)` with a 5-minute absolute expiration.
- The catalog data itself was extracted to a dedicated `ProductCatalog.cs` class, making it easy to swap for a database call in the future.

**Copilot's role:** Copilot designed the double-checked locking cache pattern for the client service, matched the cache duration on both sides (5 minutes), and applied the `IMemoryCache` injection pattern idiomatic to Minimal APIs — all while keeping the component layer thin and testable.

---

## 8. HTTP Test File

**Addition:** A `.http` file (`ServerApp/ServerApp.http`) was updated to include a named request for the `/api/productlist` endpoint alongside the default `weatherforecast` template entry, making it easy to exercise the API directly from the VS Code REST client without leaving the editor.

---

## Challenges and How Copilot Helped

| Challenge | How Copilot helped |
|---|---|
| Route mismatch was split across two projects | Located both sides in a single workspace search and patched both files atomically |
| Multiple catch blocks with duplicated messages | Unified branches under a shared constant without losing the developer log |
| Silent deserialization failures (wrong casing) | Distinguished between syntax errors and schema mismatches; placed the options object as a `static readonly` to avoid GC pressure |
| Nested JSON and model evolution | Generated back-end records and front-end model in the same pass, keeping naming consistent |
| Eliminating redundant HTTP calls | Designed a thread-safe client-side cache and matched it with a server-side `IMemoryCache` layer |
| Keeping the component thin | Extracted all HTTP and JSON logic into a dedicated service, leaving the Razor file responsible only for rendering |

---

## Files Modified or Created

| File | Change |
|---|---|
| `ServerApp/Program.cs` | Route fix, CORS inline policy, `IMemoryCache` integration |
| `ServerApp/ProductCatalog.cs` | **New** — static catalog data and typed records |
| `ServerApp/ServerApp.http` | Added `/api/productlist` test request |
| `ClientApp/Pages/FetchProducts.razor` | Friendly error message, category display, delegated fetch to service |
| `ClientApp/Services/ProductCatalogService.cs` | **New** — HTTP fetch with client-side cache and JSON deserialization |
| `ClientApp/Program.cs` | Registered `ProductCatalogService` in DI container |
