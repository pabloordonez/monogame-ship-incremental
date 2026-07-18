using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Content;

public interface IAssetCatalog
{
    string LoadText(ContentId id);
}
