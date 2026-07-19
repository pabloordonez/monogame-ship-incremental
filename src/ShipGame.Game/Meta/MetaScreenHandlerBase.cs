using Microsoft.Xna.Framework.Input;

namespace ShipGame.Game;

internal abstract class MetaScreenHandlerBase : IMetaScreenHandler
{
    public abstract MetaScreen Screen { get; }

    public abstract void BuildUi(MetaUiContext context);

    public virtual void Update(MetaUiContext context)
    {
    }

    public abstract void Draw(MetaDrawContext context);

    public virtual void HandleHotkeys(MetaUiContext context, Func<Keys, bool> pressed)
    {
    }

    public virtual void DriveWindowSmoke(MetaUiContext context, int ticks)
    {
    }
}
