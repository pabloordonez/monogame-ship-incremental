namespace ShipGame.Domain;

public static class MetaContentIds
{
    public const string Ferrite = "MAT_FERRITE";
    public const string Lumen = "MAT_LUMEN";
    public const string DataCore = "MAT_DATA_CORE";
    public const string CinderBelt = "ENV_CINDER_BELT";
    public const string IonVeil = "ENV_ION_VEIL";
    public const string TravelIonVeil = "CAP_TRAVEL_ION_VEIL";

    public static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 128 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_');
}
