using System.Collections.Generic;

namespace RSBot.Core.Components.Scripting;

public interface IScriptCommand
{
    #region Properties

    string Name { get; }

    bool IsBusy { get; }

    /// <summary>
    ///     Gets a value indicating whether a failed execution must stop the whole script. A command that
    ///     cannot continue the walk sets this to <c>true</c>, so the runner does not cascade the same
    ///     failure through every remaining line.
    /// </summary>
    bool AbortsScriptOnFailure => false;

    /// <summary>
    ///     Gets a value indicating whether a failed execution counts as a script error. A command that
    ///     recovered from the failure (for example by returning the character to town) reports the run as
    ///     finished without error, so the botbase keeps working instead of treating the script as broken.
    /// </summary>
    bool FailureIsError => true;

    Dictionary<string, string> Arguments { get; }

    #endregion Properties

    #region Methods

    /// <summary>
    ///     Executes this instance.
    /// </summary>
    /// <returns>A value indicating if the command has been executed successfully.</returns>
    bool Execute(string[] arguments = null);

    /// <summary>
    ///     Stops the execution of the command
    /// </summary>
    void Stop();

    #endregion Methods
}
