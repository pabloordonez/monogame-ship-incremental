using System.Buffers.Binary;
using System.Text;

namespace ShipGame.Domain;

public readonly record struct ContentId
{
    public string Value { get; }

    public ContentId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
            throw new ArgumentException("Content IDs must contain 1-128 characters.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value;
}
