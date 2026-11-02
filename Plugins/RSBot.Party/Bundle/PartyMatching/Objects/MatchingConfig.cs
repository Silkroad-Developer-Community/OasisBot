using RSBot.Core;
using RSBot.Core.Objects.Party;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace RSBot.Party.Bundle.PartyMatching.Objects;

internal struct MatchingConfig
{
    /// <summary>
    ///     Gets or sets a value indicating whether [experience automatic share].
    /// </summary>
    /// <value>
    ///     <c>true</c> if [experience automatic share]; otherwise, <c>false</c>.
    /// </value>
    public bool ExperienceAutoShare
    {
        get => PlayerConfig.Profile.Party.ExpAutoShare;
        set => PlayerConfig.Profile.Party.ExpAutoShare = value;
    }

    /// <summary>
    ///     Gets or sets a value indicating whether [item automatic share].
    /// </summary>
    /// <value>
    ///     <c>true</c> if [item automatic share]; otherwise, <c>false</c>.
    /// </value>
    public bool ItemAutoShare
    {
        get => PlayerConfig.Profile.Party.ItemAutoShare;
        set => PlayerConfig.Profile.Party.ItemAutoShare = value;
    }

    /// <summary>
    ///     Gets or sets a value indicating whether [allow invitations].
    /// </summary>
    /// <value>
    ///     <c>true</c> if [allow invitations]; otherwise, <c>false</c>.
    /// </value>
    public bool AllowInvitations
    {
        get => PlayerConfig.Profile.Party.AllowInvitations;
        set => PlayerConfig.Profile.Party.AllowInvitations = value;
    }

    /// <summary>
    ///     Gets or sets the purpose.
    /// </summary>
    /// <value>The purpose.</value>
    public PartyPurpose Purpose
    {
        get => PlayerConfig.Profile.Party.Matching.Purpose;
        set => PlayerConfig.Profile.Party.Matching.Purpose = value;
    }

    /// <summary>
    ///     Gets or sets the title.
    /// </summary>
    /// <value>The title.</value>
    public string Title
    {
        get => PlayerConfig.Profile.Party.Matching.Title;
        set => PlayerConfig.Profile.Party.Matching.Title = value;
    }

    /// <summary>
    ///     Gets or sets a value indicating whether [automatic reform].
    /// </summary>
    /// <value><c>true</c> if [automatic reform]; otherwise, <c>false</c>.</value>
    public bool AutoReform
    {
        get => PlayerConfig.Profile.Party.Matching.AutoReform;
        set => PlayerConfig.Profile.Party.Matching.AutoReform = value;
    }

    /// <summary>
    ///     Gets or sets a value indicating whether [automatic accept from matching invite].
    /// </summary>
    /// <value><c>true</c> if [automatic accept  from matching invite]; otherwise, <c>false</c>.</value>
    public bool AutoAccept
    {
        get => PlayerConfig.Profile.Party.Matching.AutoAccept;
        set => PlayerConfig.Profile.Party.Matching.AutoAccept = value;
    }

    /// <summary>
    ///     Gets or sets the level from.
    /// </summary>
    /// <value>The level from.</value>
    public byte LevelFrom
    {
        get => PlayerConfig.Profile.Party.Matching.LevelFrom;
        set => PlayerConfig.Profile.Party.Matching.LevelFrom = value;
    }

    /// <summary>
    ///     Gets or sets the level to.
    /// </summary>
    /// <value>The level to.</value>
    public byte LevelTo
    {
        get => PlayerConfig.Profile.Party.Matching.LevelTo;
        set => PlayerConfig.Profile.Party.Matching.LevelTo = value;
    }
}
