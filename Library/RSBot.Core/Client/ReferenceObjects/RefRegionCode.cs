namespace RSBot.Core.Client.ReferenceObjects;

public class RefRegionCode : IReference<ushort>
{
    public ushort PrimaryKey => RegionID;

    public string NameStrID => CodeName + "_01";

    public bool Load(ReferenceParser parser)
    {
        if (!parser.TryParse(0, out Service) || Service == 0)
            return false;

        if (!parser.TryParse(1, out RegionID))
            return false;

        if (!parser.TryParse(2, out CodeName) || string.IsNullOrWhiteSpace(CodeName) || CodeName == "xxx")
            return false;

        // The last column is not localized; display names come from TextZoneName.txt.
        return true;
    }

    public string GetRealName()
    {
        string name = Game.ReferenceManager.GetTranslation(NameStrID);
        return string.IsNullOrWhiteSpace(name) || name == "0" || name == NameStrID ? RegionID.ToString() : name;
    }

    public byte Service;
    public ushort RegionID;
    public string CodeName;
}
