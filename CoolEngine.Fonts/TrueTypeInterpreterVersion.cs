namespace CoolEngine.Fonts;

/// <summary>The TrueType instruction interpreter mode, as FreeType's <c>interpreter-version</c> property.</summary>
public enum TrueTypeInterpreterVersion
{
    /// <summary>Version 40: ClearType-style hinting with the backward-compatibility rules (FreeType's default).</summary>
    V40,

    /// <summary>Version 35: the classic interpreter that hints in both directions.</summary>
    V35,
}
