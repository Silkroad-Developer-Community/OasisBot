using System;
using System.Text;

namespace RSBot
{
    public static class Terminal
    {
        private static readonly object _lock = new();
        private static readonly StringBuilder _inputBuffer = new();
        private static int _cursorPos = 0;
        private static string _prompt = "> ";

        /// <summary>
        /// Gets or sets the command prompt string.
        /// </summary>
        public static string Prompt
        {
            get => _prompt;
            set
            {
                lock (_lock)
                {
                    _prompt = value;
                    RedrawPrompt();
                }
            }
        }

        /// <summary>
        /// Writes a log message thread-safely by temporarily clearing the prompt.
        /// </summary>
        /// <param name="message">The message to write.</param>
        public static void WriteLog(string message)
        {
            lock (_lock)
            {
                ClearCurrentLine();
                Console.WriteLine(message);
                RedrawPrompt();
            }
        }

        /// <summary>
        /// Clears the current input line from the console.
        /// </summary>
        public static void ClearCurrentLine()
        {
            try
            {
                if (!Console.IsOutputRedirected)
                {
                    Console.CursorLeft = 0;
                    int width = Console.WindowWidth;
                    if (width <= 0)
                        width = 80;
                    Console.Write(new string(' ', width - 1));
                    Console.CursorLeft = 0;
                }
            }
            catch
            {
                // Fallback
            }
        }

        /// <summary>
        /// Redraws the prompt and the current input buffer contents.
        /// </summary>
        public static void RedrawPrompt()
        {
            Console.Write(_prompt + _inputBuffer.ToString());
            try
            {
                if (!Console.IsOutputRedirected)
                {
                    Console.CursorLeft = _prompt.Length + _cursorPos;
                }
            }
            catch
            {
                // Fallback
            }
        }

        /// <summary>
        /// Read line character by character, keeping the prompt at the bottom of the console.
        /// </summary>
        /// <returns>The entered command string.</returns>
        public static string ReadLine()
        {
            _inputBuffer.Clear();
            _cursorPos = 0;

            lock (_lock)
            {
                RedrawPrompt();
            }

            while (true)
            {
                var keyInfo = Console.ReadKey(true);

                lock (_lock)
                {
                    if (keyInfo.Key == ConsoleKey.Enter)
                    {
                        var result = _inputBuffer.ToString();
                        Console.WriteLine();
                        _inputBuffer.Clear();
                        _cursorPos = 0;
                        return result;
                    }
                    else if (keyInfo.Key == ConsoleKey.Backspace)
                    {
                        if (_cursorPos > 0)
                        {
                            _inputBuffer.Remove(_cursorPos - 1, 1);
                            _cursorPos--;
                            ClearCurrentLine();
                            RedrawPrompt();
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.Delete)
                    {
                        if (_cursorPos < _inputBuffer.Length)
                        {
                            _inputBuffer.Remove(_cursorPos, 1);
                            ClearCurrentLine();
                            RedrawPrompt();
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.LeftArrow)
                    {
                        if (_cursorPos > 0)
                        {
                            _cursorPos--;
                            try
                            {
                                if (!Console.IsOutputRedirected)
                                {
                                    Console.CursorLeft = _prompt.Length + _cursorPos;
                                }
                            }
                            catch { }
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.RightArrow)
                    {
                        if (_cursorPos < _inputBuffer.Length)
                        {
                            _cursorPos++;
                            try
                            {
                                if (!Console.IsOutputRedirected)
                                {
                                    Console.CursorLeft = _prompt.Length + _cursorPos;
                                }
                            }
                            catch { }
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.Home)
                    {
                        _cursorPos = 0;
                        try
                        {
                            if (!Console.IsOutputRedirected)
                            {
                                Console.CursorLeft = _prompt.Length;
                            }
                        }
                        catch { }
                    }
                    else if (keyInfo.Key == ConsoleKey.End)
                    {
                        _cursorPos = _inputBuffer.Length;
                        try
                        {
                            if (!Console.IsOutputRedirected)
                            {
                                Console.CursorLeft = _prompt.Length + _cursorPos;
                            }
                        }
                        catch { }
                    }
                    else if (keyInfo.KeyChar != '\0' && !char.IsControl(keyInfo.KeyChar))
                    {
                        _inputBuffer.Insert(_cursorPos, keyInfo.KeyChar);
                        _cursorPos++;
                        ClearCurrentLine();
                        RedrawPrompt();
                    }
                }
            }
        }
    }
}
