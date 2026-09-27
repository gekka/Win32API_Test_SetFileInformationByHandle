using System;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Diagnostics.Debug;
using System.Runtime.InteropServices;

namespace Gekka.Win.Bug.API.__SetFileInformationByHandle
{
    class TestFailException : ApplicationException
    {
        public TestFailException(string message = "") : this(Marshal.GetLastWin32Error(), message)
        {
        }

        public TestFailException(int err, string message = "") : base(GetWin32ErrorMessage(err, message))
        {
        }


        public unsafe static string GetWin32ErrorMessage(int err, string msg = "")
        {
            if (err == 0)
            {
                return msg;
            }
            const uint bufferSize = 512;
            char* buffer = stackalloc char[(int)bufferSize];
            FORMAT_MESSAGE_OPTIONS flags = FORMAT_MESSAGE_OPTIONS.FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_OPTIONS.FORMAT_MESSAGE_IGNORE_INSERTS;
            uint result = PInvoke.FormatMessage(flags, null, (uint)err, 0, new PWSTR(buffer), bufferSize, null);
            if (result > 0)
            {
                return msg + new string(buffer, 0, (int)result).TrimEnd('\r', '\n');
            }

            return msg + $"0x{err:X8}";
        }
    }
}
