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
    public const int Code = 50;
    public const int Sku = 100;
    public const int OrderNumber = 50;
    public const int TrackingNumber = 100;
    public const int InvoiceNumber = 50;
    public const int TaxId = 50;

    // ── Names / labels ─────────────────────────────────────────────────────────
    public const int ShortName = 100;
    public const int Name = 200;
    public const int LongName = 300;

    // ── Free-text fields ───────────────────────────────────────────────────────
    public const int Url = 500;
    public const int Notes = 1000;
    public const int Description = 2000;

    // ── Address fields ─────────────────────────────────────────────────────────
    public const int AddressLine = 300;
    public const int City = 100;
    public const int State = 100;
    public const int PostalCode = 20;

    // ── Miscellaneous ──────────────────────────────────────────────────────────
    public const int Slug = 200;
    public const int Domain = 255;
    public const int EstimatedDelivery = 50;
    public const int Carrier = 100;
    public const int RecipientName = 200;
}
