using System.Collections.ObjectModel;
using ShipGame.Domain;

namespace ShipGame.Gameplay;

public static class WorldRunIds
{
    public static readonly ContentId CinderBelt = new("ENV_CINDER_BELT");
    public static readonly ContentId IonVeil = new("ENV_ION_VEIL");
    public static readonly ContentId FieldProof = new("OBJ_FIELD_PROOF");
    public static readonly ContentId StandardGate = new("EXT_STANDARD_GATE");
    public static readonly ContentId Ferrite = new("MAT_FERRITE");
    public static readonly ContentId Lumen = new("MAT_LUMEN");
    public static readonly ContentId DataCore = new("MAT_DATA_CORE");
}
