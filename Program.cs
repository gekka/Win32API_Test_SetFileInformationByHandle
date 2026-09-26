using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Windows.Win32;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.Foundation;
using Windows.Win32.System.Diagnostics.Debug;
//using Windows.Wdk.Storage.FileSystem;
using static Windows.Win32.PInvoke;
using System.Runtime.InteropServices;

namespace Gekka.Win.Bug.API.__SetFileInformationByHandle
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                //Test(TestType.AbsolutePath);
                Test(TestType.UseDirectoryHandle);
            }
            catch (Exception ex)
            {
                //var err = Marshal.GetLastWin32Error();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.Message);
            }
            finally
            {
                Console.ResetColor();
            }
        }

        enum TestType
        {
            AbsolutePath,
            UseDirectoryHandle
        }

        static void Test(TestType testType)
        {
            string fileNameSrc = $"Test.txt";
            string fileNameDst = $"Test{DateTime.Now:HHmmss}.txt";

            string dirName = "TestDir";
            string dirSrc = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string dirDst = System.IO.Path.Combine(dirSrc, dirName);

            string pathAbsSrc = System.IO.Path.Combine(dirSrc, fileNameSrc);
            string pathAbsDst = System.IO.Path.Combine(dirDst, fileNameDst);

            string fileNameForAPI
                = (testType == TestType.AbsolutePath)
                ? pathAbsDst
                : (".\\" + fileNameDst);

            byte[] bsUnicode_withNull = System.Text.Encoding.Unicode.GetBytes(fileNameForAPI + "\0");

            System.IO.Directory.SetCurrentDirectory(dirSrc);

            //create source file in current directory
            using (var fs = System.IO.File.CreateText(pathAbsSrc))
            {
                fs.WriteLine("OK");
            }

            if (System.IO.Directory.Exists(dirDst))
            {
                System.IO.Directory.Delete(dirDst, true);
            }
            System.IO.Directory.CreateDirectory(dirDst);

            Microsoft.Win32.SafeHandles.SafeFileHandle? hdirDest = default;
            if (testType == TestType.UseDirectoryHandle)
            {
                hdirDest = CreateFile
                    (dirDst
                    , 0 //| (uint)GENERIC_ACCESS_RIGHTS.GENERIC_READ | (uint)GENERIC_ACCESS_RIGHTS.GENERIC_WRITE
                        //| (uint)FILE_ACCESS_RIGHTS.FILE_LIST_DIRECTORY
                        //| (uint)FILE_ACCESS_RIGHTS.FILE_ADD_FILE
                        //| (uint)FILE_ACCESS_RIGHTS.FILE_ADD_SUBDIRECTORY
                        //| (uint)FILE_ACCESS_RIGHTS.FILE_TRAVERSE
                    , FILE_SHARE_MODE.FILE_SHARE_READ //| FILE_SHARE_MODE.FILE_SHARE_WRITE | FILE_SHARE_MODE.FILE_SHARE_DELETE
                    , null
                    , FILE_CREATION_DISPOSITION.OPEN_EXISTING
                    , FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_BACKUP_SEMANTICS
                    , null);
                if (hdirDest.IsInvalid || hdirDest.IsClosed)
                {
                    ThrowGetWin32ErrorMessage();
                    return;
                }
            }

            try
            {
                var hFile = CreateFile
                    (pathAbsSrc
                    , 0 //| (uint)GENERIC_ACCESS_RIGHTS.GENERIC_READ
                        //| (uint)GENERIC_ACCESS_RIGHTS.GENERIC_WRITE
                        | (uint)FILE_ACCESS_RIGHTS.DELETE
                    //| (uint)FILE_ACCESS_RIGHTS.FILE_ALL_ACCESS
                    , 0//FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE | FILE_SHARE_MODE.FILE_SHARE_DELETE
                    , null
                    , FILE_CREATION_DISPOSITION.OPEN_EXISTING
                    , 0//FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_NORMAL
                    , null);

                if (hFile.IsInvalid)
                {
                    ThrowGetWin32ErrorMessage();
                }


                try
                {
                    IntPtr basesize = Marshal.OffsetOf(typeof(FILE_RENAME_INFO), nameof(FILE_RENAME_INFO.FileName));
                    int size = basesize.ToInt32() + bsUnicode_withNull.Length;

                    unsafe
                    {
                        void* pb = stackalloc byte[size];
                        FILE_RENAME_INFO* prename = (FILE_RENAME_INFO*)pb;
                        prename->RootDirectory = new Windows.Win32.Foundation.HANDLE(IntPtr.Zero);
                        prename->Anonymous.ReplaceIfExists = true;
                        prename->FileNameLength = (uint)(bsUnicode_withNull.Length - sizeof(char));
                        Marshal.Copy(bsUnicode_withNull, 0, IntPtr.Add(new IntPtr(pb), basesize.ToInt32()), bsUnicode_withNull.Length);

                        if (testType == TestType.UseDirectoryHandle && hdirDest != null)
                        {
                            prename->RootDirectory = new Windows.Win32.Foundation.HANDLE(hdirDest.DangerousGetHandle());
                        }

                        Console.WriteLine($"TestType: {testType}");
                        Console.WriteLine($"Src: {pathAbsSrc}");
                        Console.WriteLine($"Dsc: {pathAbsDst}");
                        Console.WriteLine($"fileNameForAPI={fileNameForAPI}");
                        Console.WriteLine($"hFile: 0x{hFile.DangerousGetHandle().ToString("X")}");
                        Console.WriteLine($"prename->RootDirectory = 0x{prename->RootDirectory.Value.ToString("X")}");

                        //System.Diagnostics.Debugger.Break();
                        var result = SetFileInformationByHandle(hFile, FILE_INFO_BY_HANDLE_CLASS.FileRenameInfo, prename, (uint)size);
                        var er2 = Marshal.GetLastWin32Error();
                        if (!System.IO.File.Exists(pathAbsDst))
                        {
                            ThrowGetWin32ErrorMessage(er2, "Fail: ");
                        }
                        else
                        {
                            Console.WriteLine("Success");
                        }
                    }

                }
                finally
                {
                    hFile.Close();
                }
            }
            finally
            {
                hdirDest?.Close();
            }
        }

        unsafe static void ThrowGetWin32ErrorMessage(string msg = "")
        {
            ThrowGetWin32ErrorMessage(Marshal.GetLastWin32Error(), msg);
        }
        unsafe static void ThrowGetWin32ErrorMessage(int err, string msg = "")
        {
            throw new ApplicationException(GetWin32ErrorMessage(err, msg));
        }
        unsafe static string GetWin32ErrorMessage(int err, string msg = "")
        {
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

