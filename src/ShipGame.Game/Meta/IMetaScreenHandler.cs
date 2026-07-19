using Microsoft.Xna.Framework.Input;

namespace ShipGame.Game;

public interface IMetaScreenHandler
{
    MetaScreen Screen { get; }

    void BuildUi(MetaUiContext context);

    void Update(MetaUiContext context);

    void Draw(MetaDrawContext context);

    void HandleHotkeys(MetaUiContext context, Func<Keys, bool> pressed);

    void DriveWindowSmoke(MetaUiContext context, int ticks);
}
