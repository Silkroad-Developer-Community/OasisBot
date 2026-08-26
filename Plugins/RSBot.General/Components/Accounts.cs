using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using RSBot.Core;
using RSBot.Core.Components;
using RSBot.Core.Network.Protocol;
using RSBot.General.Models;

namespace RSBot.General.Components;

internal class Accounts
{
    /// <summary>
    ///     Gets or sets the saved accounts.
    /// </summary>
    /// <value>
    ///     The saved accounts.
    /// </value>
    public static List<Account> SavedAccounts { get; set; }

    /// <summary>
    ///     Gets or sets the joined account.
    /// </summary>
    public static Account Joined { get; set; }

    /// <summary>
    ///     Loads this instance.
    /// </summary>
    public static void Load()
    {
        try
        {
            SavedAccounts = GlobalConfig.Get<List<Account>>("Accounts") ?? new List<Account>(4);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex);
        }
    }

    /// <summary>
    ///     Saves this instance.
    /// </summary>
    public static void Save()
    {
        if (SavedAccounts == null)
            return;

        try
        {
            GlobalConfig.Set("Accounts", SavedAccounts);
            GlobalConfig.Save();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex);
        }
    }
}
