namespace Wintor.Core;

/// <summary>
/// Turns a file type's "open" command from the registry (<c>"C:\…\vlc.exe" --started-from-file "%1"</c>) into a
/// program and arguments that open a link instead of a file. A link opened the normal way goes to the browser, not
/// the player, so a file still downloading is handed to the player's program directly.
/// </summary>
public static class PlayerCommand
{
    // %1 and %L are the file; %V is the same for most handlers. %* (the rest of the arguments) is empty for us.
    private static readonly string[] FilePlaceholders = ["\"%1\"", "\"%L\"", "\"%l\"", "\"%V\"", "%1", "%L", "%l", "%V"];

    /// <returns>The program (environment variables expanded) and its arguments, or null if the command is not a program.</returns>
    public static (string Program, string Arguments)? Build(string? command, string url)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        command = command.Trim();
        string program, arguments;
        if (command.StartsWith('"'))
        {
            int end = command.IndexOf('"', 1);
            if (end < 0)
            {
                return null;
            }

            program = command[1..end];
            arguments = command[(end + 1)..];
        }
        else
        {
            // Unquoted: the program ends at ".exe" (its path may contain spaces), otherwise at the first space.
            int exe = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            int end = exe >= 0 ? exe + 4 : command.IndexOf(' ');
            program = end < 0 ? command : command[..end];
            arguments = end < 0 ? "" : command[end..];
        }

        program = Environment.ExpandEnvironmentVariables(program.Trim());
        if (!program.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return null; // rundll32 lines, packaged apps' handlers and the like cannot take a link
        }

        string quoted = "\"" + url + "\"";
        bool placed = false;
        foreach (string placeholder in FilePlaceholders)
        {
            if (arguments.Contains(placeholder, StringComparison.Ordinal))
            {
                arguments = arguments.Replace(placeholder, quoted, StringComparison.Ordinal);
                placed = true;
                break;
            }
        }

        arguments = arguments.Replace("%*", "", StringComparison.Ordinal).Trim();
        if (!placed)
        {
            arguments = arguments.Length == 0 ? quoted : arguments + " " + quoted;
        }

        return (program, arguments);
    }
}
