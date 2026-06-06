namespace FicsitMcp.Domain.GameData.Model;

/// <summary>
/// Physical form of an item, mapped from the Docs.json <c>mForm</c> field
/// (<c>RF_SOLID</c>, <c>RF_LIQUID</c>, <c>RF_GAS</c>, <c>RF_INVALID</c>).
/// </summary>
/// <remarks>
/// The form is load-bearing for rate math: in the raw docs, liquid and gas
/// ingredient/product amounts are expressed in millilitres (×1000), whereas solids
/// are literal counts. The parser normalises both to display units, but the form is
/// retained so callers can label rates correctly (m³/min vs items/min).
/// </remarks>
public enum ItemForm
{
    /// <summary>Form was missing or <c>RF_INVALID</c> in the source data.</summary>
    Invalid = 0,

    /// <summary>Solid part counted as discrete items.</summary>
    Solid,

    /// <summary>Liquid measured in cubic metres (m³).</summary>
    Liquid,

    /// <summary>Gas measured in cubic metres (m³).</summary>
    Gas,
}
