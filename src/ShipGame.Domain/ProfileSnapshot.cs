using System.Buffers.Binary;
using System.Text;

namespace ShipGame.Domain;

public readonly record struct ProfileSnapshot(ulong ProfileSeed, long RunIndex);
