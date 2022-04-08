using System.Collections.ObjectModel;

namespace CoolEngine.GraphicalEngine.Core.Font;

public class FontInformation
{
    private readonly Dictionary<char, CharacterInfo> m_characters;
    private readonly ReadOnlyDictionary<char, CharacterInfo> m_readOnlyCharacters;
    
    public FontInformation(int originalFontSize, string fontName, Dictionary<char, CharacterInfo> characterInformations, Texture.Texture texture)
    {
        OriginalFontSize = originalFontSize;
        FontName = fontName;

        m_characters = characterInformations;
        Texture = texture;
        m_readOnlyCharacters = new ReadOnlyDictionary<char, CharacterInfo>(m_characters);
    }

    public int OriginalFontSize { get; }
    public string FontName { get; }
    public Texture.Texture Texture { get; }

    public ReadOnlyDictionary<char, CharacterInfo> CharacterInformations => m_readOnlyCharacters;
}