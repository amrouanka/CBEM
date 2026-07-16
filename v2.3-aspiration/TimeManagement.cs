using System.Runtime.InteropServices;

public static class TimeManagement
{
    // Time control variables
    public static bool quit = false;
    public static int movestogo = 30;
    public static int movetime = -1;
    public static int time = -1;
    public static int inc = 0;
    public static int starttime = 0;
    public static int stoptime = 0;
    public static bool timeset = false;
    public static bool stopped = false;

    private const int StdInputHandle = -10;
    private const uint FileTypePipe = 0x0003;
    private static readonly IntPtr InvalidHandleValue = new(-1);
    private static bool inputInitialized;
    private static bool inputIsConsole;
    private static bool inputIsPipe;
    private static IntPtr inputHandle;

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll")]
    private static extern uint GetFileType(IntPtr hFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool PeekNamedPipe(
        IntPtr hNamedPipe,
        IntPtr lpBuffer,
        uint nBufferSize,
        out uint lpBytesRead,
        out uint lpTotalBytesAvail,
        out uint lpBytesLeftThisMessage);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNumberOfConsoleInputEvents(IntPtr hConsoleInput, out uint lpcNumberOfEvents);

    // get time in milliseconds
    public static int GetTimeMs()
    {
        return Environment.TickCount;
    }

    // check if there's input waiting from STDIN
    public static bool InputWaiting()
    {
        if (!OperatingSystem.IsWindows())
        {
            if (!Console.IsInputRedirected)
                return Console.KeyAvailable;

            return Console.In.Peek() != -1;
        }

        if (!inputInitialized)
        {
            inputInitialized = true;
            inputHandle = GetStdHandle(StdInputHandle);

            if (inputHandle != IntPtr.Zero && inputHandle != InvalidHandleValue)
            {
                inputIsConsole = GetConsoleMode(inputHandle, out _);

                if (!inputIsConsole)
                {
                    uint fileType = GetFileType(inputHandle);
                    inputIsPipe = fileType == FileTypePipe;
                }
            }
        }

        if (inputHandle == IntPtr.Zero || inputHandle == InvalidHandleValue)
            return false;

        if (inputIsPipe)
        {
            if (!PeekNamedPipe(inputHandle, IntPtr.Zero, 0, out _, out uint bytesAvail, out _))
                return false;

            return bytesAvail > 0;
        }

        if (!inputIsConsole)
            return Console.In.Peek() != -1;

        if (!GetNumberOfConsoleInputEvents(inputHandle, out uint eventsCount))
            return false;

        return eventsCount > 1;
    }

    // read GUI/user input
    public static void ReadInput()
    {
        if (!InputWaiting())
            return;

        if (OperatingSystem.IsWindows() && inputIsPipe)
        {
            if (!PeekNamedPipe(inputHandle, IntPtr.Zero, 0, out _, out uint bytesAvail, out _))
                return;

            if (bytesAvail == 0)
                return;

            int peekLength = (int)Math.Min(bytesAvail, 512u);
            byte[] buffer = new byte[peekLength];
            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            try
            {
                if (!PeekNamedPipe(inputHandle, handle.AddrOfPinnedObject(), (uint)peekLength, out uint bytesRead, out _, out _))
                    return;

                bool hasNewline = false;
                for (int i = 0; i < bytesRead; i++)
                {
                    if (buffer[i] == (byte)'\n')
                    {
                        hasNewline = true;
                        break;
                    }
                }

                if (!hasNewline)
                    return;
            }
            finally
            {
                handle.Free();
            }
        }

        stopped = true;

        string? input = Console.ReadLine();
        if (string.IsNullOrEmpty(input))
            return;

        input = input.Trim();

        if (input.StartsWith("quit"))
        {
            quit = true;
            stopped = true;
        }
        else if (input.StartsWith("stop"))
        {
            stopped = true;
        }
    }

    // a bridge function to interact between search and GUI input
    public static void Communicate()
    {
        // if time is up break here
        if (timeset && GetTimeMs() > stoptime)
        {
            // tell engine to stop calculating
            stopped = true;
        }

        // read GUI input
        ReadInput();
    }
}
