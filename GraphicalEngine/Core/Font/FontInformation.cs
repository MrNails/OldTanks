using System.Collections.ObjectModel;

namespace GraphicalEngine.Core.Font;

public class FontInformation
{
    private readonly Dictionary<char, CharacterInfo> m_characters;
    private readonly ReadOnlyDictionary<char, CharacterInfo> m_readOnlyCharacters;
    
    public FontInformation(int originalFontSize, string fontName, Dictionary<char, CharacterInfo> characterInformations)
    {
        OriginalFontSize = originalFontSize;
        FontName = fontName;

        m_characters = characterInformations;
        m_readOnlyCharacters = new ReadOnlyDictionary<char, CharacterInfo>(m_characters);
    }

    public int OriginalFontSize { get; }
    public string FontName { get; }
    public ReadOnlyDictionary<char, CharacterInfo> CharacterInformations => m_readOnlyCharacters;
}