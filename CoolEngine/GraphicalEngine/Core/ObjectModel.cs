using System.Collections.ObjectModel;

namespace CoolEngine.GraphicalEngine.Core;

public sealed class ObjectModel
{
    private readonly List<KeyValuePair<float[], uint[]>> m_elements;
    private readonly ReadOnlyCollection<KeyValuePair<float[], uint[]>> m_readOnlyElements;

    public ObjectModel(IEnumerable<KeyValuePair<float[], uint[]>> elements)
    {
        m_elements = new List<KeyValuePair<float[], uint[]>>(elements);
        m_readOnlyElements = new ReadOnlyCollection<KeyValuePair<float[], uint[]>>(m_readOnlyElements);
    }

    public ReadOnlyCollection<KeyValuePair<float[], uint[]>> Elements => m_readOnlyElements;
}