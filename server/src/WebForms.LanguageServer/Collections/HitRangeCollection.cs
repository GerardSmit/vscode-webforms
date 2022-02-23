using System.Collections;
using WebForms.Nodes;

namespace WebForms.Collections;

public class HitRangeCollection : ICollection<HitRange>
{
    private readonly ICollection<HitRange> _inner;

    public HitRangeCollection(ICollection<HitRange> inner)
    {
        _inner = inner;
    }

    public Node CurrentNode { get; set; } = null!;

    public IEnumerator<HitRange> GetEnumerator()
    {
        return _inner.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable) _inner).GetEnumerator();
    }

    public void Add(HitRange item)
    {
        item.Node = CurrentNode;
        _inner.Add(item);
    }

    public void Clear()
    {
        _inner.Clear();
    }

    public bool Contains(HitRange item)
    {
        return _inner.Contains(item);
    }

    public void CopyTo(HitRange[] array, int arrayIndex)
    {
        _inner.CopyTo(array, arrayIndex);
    }

    public bool Remove(HitRange item)
    {
        return _inner.Remove(item);
    }

    public int Count => _inner.Count;

    public bool IsReadOnly => _inner.IsReadOnly;
}
