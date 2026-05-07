namespace B2B.Shared.Core.Common;

/// <summary>
/// Canonical column-length constants shared across all service DbContext configurations.
///
/// Centralising these values guarantees that the domain validator constraints (e.g.
/// <c>MaximumLength(FieldLengths.Email)</c>) and the database column definitions
/// (<c>HasMaxLength(FieldLengths.Email)</c>) never diverge.
/// </summary>
public static class FieldLengths
{
    // ── Currency / financial ───────────────────────────────────────────────────
    /// <summary>ISO 4217 currency code (e.g. "USD").</summary>
    public const int CurrencyCode = 3;

    /// <summary>ISO 3166-1 alpha-2 or alpha-3 country code (e.g. "US" or "USA").</summary>
    public const int CountryCode = 3;

    // ── Identity fields ────────────────────────────────────────────────────────
    /// <summary>RFC 5321 maximum email address length.</summary>
    public const int Email = 256;

    /// <summary>BCrypt output is always 60 chars; 500 gives future-proof headroom.</summary>
    public const int PasswordHash = 500;

    /// <summary>Refresh token (GUID-derived hex string).</summary>
    public const int Token = 500;

    // ── Short identifiers / codes ──────────────────────────────────────────────
    /// <summary>Generic short code (e.g. coupon or discount codes).</summary>
    public const int Code = 50;

    /// <summary>Stock-keeping unit identifier used in product catalogues.</summary>
    public const int Sku = 100;

    /// <summary>Human-readable order reference number (e.g. "ORD-20240501-0042").</summary>
    public const int OrderNumber = 50;

    /// <summary>Carrier-assigned shipment tracking code.</summary>
    public const int TrackingNumber = 100;

    /// <summary>Invoice or billing document reference number.</summary>
    public const int InvoiceNumber = 50;

    /// <summary>Tax identification number (VAT, GST, EIN, etc.).</summary>
    public const int TaxId = 50;

    // ── Names / labels ─────────────────────────────────────────────────────────
    /// <summary>Short display labels — e.g. status labels, enum display names.</summary>
    public const int ShortName = 100;

    /// <summary>Standard display name for products, tenants, users, and discounts.</summary>
    public const int Name = 200;

    /// <summary>Extended display names used where additional context is needed (e.g. full company names).</summary>
    public const int LongName = 300;

    // ── Free-text fields ───────────────────────────────────────────────────────
    /// <summary>Absolute URL stored in the database (avatar, webhook endpoint, logo, etc.).</summary>
    public const int Url = 500;

    /// <summary>Order, product, or customer notes entered by users.</summary>
    public const int Notes = 1000;

    /// <summary>Long-form marketing or product description.</summary>
    public const int Description = 2000;

    // ── Address fields ─────────────────────────────────────────────────────────
    /// <summary>Street line, including house or building number.</summary>
    public const int AddressLine = 300;

    /// <summary>City or locality name.</summary>
    public const int City = 100;

    /// <summary>State, province, or region name or code.</summary>
    public const int State = 100;

    /// <summary>Postal or ZIP code — varies widely by country (max 20 chars).</summary>
    public const int PostalCode = 20;

    // ── Miscellaneous ──────────────────────────────────────────────────────────
    /// <summary>URL-safe slug used in tenant subdomains and resource paths (e.g. "acme-corp").</summary>
    public const int Slug = 200;

    /// <summary>Domain name stored for tenant routing (e.g. "acme.example.com").</summary>
    public const int Domain = 255;

    /// <summary>Estimated delivery window string returned by carriers (e.g. "2–5 business days").</summary>
    public const int EstimatedDelivery = 50;

    /// <summary>Carrier or logistics provider name (e.g. "FedEx", "UPS").</summary>
    public const int Carrier = 100;

    /// <summary>Full name of the shipment recipient.</summary>
    public const int RecipientName = 200;
}
