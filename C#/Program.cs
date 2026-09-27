using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Windows.Win32.Storage.FileSystem;
using static Windows.Win32.PInvoke;

namespace Gekka.Win.Bug.API.__SetFileInformationByHandle
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                TestParameters[] parameters =
                [
                    new TestParameters(TestType.AbsolutePath, $@".\Src\Test1.txt", $@"Test1_{DateTime.Now:HHmmss}.txt", "Dst"),
                    new TestParameters(TestType.AbsolutePath, $@".\Src\Test1.txt", $@".\X\Test1_{DateTime.Now:HHmmss}.txt", "Dst"),
                    new TestParameters(TestType.UseDirectoryHandle, $@".\Src\Test2.txt", $@"Test2_{DateTime.Now:HHmmss}.txt", "Dst"),
                    new TestParameters(TestType.UseDirectoryHandle, $@".\Src\Test2.txt", $@".\X\Test2_{DateTime.Now:HHmmss}.txt", "Dst"),
                ];

                try
                {
                    foreach (var param in parameters)
                    {
                        try
                        {
                            Test(param);
                        }
                        catch (TestFailException ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine(ex.Message);
                        }
                        finally
                        {
                            Console.ResetColor();
                        }
                    }
                }
                finally
                {
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.Message);
            }
            finally
            {
                Console.ResetColor();
            }
        }

        static unsafe void Test(TestParameters param)
        {
            Console.WriteLine("\r\n" + new string('=', 40));
            try
            {
                param.PrepareForTest();

                Microsoft.Win32.SafeHandles.SafeFileHandle? hdirDest = param.CreateDirectoryHandle();
                Microsoft.Win32.SafeHandles.SafeFileHandle hFile = param.CreateFileHandle();

                byte[] bsUnicode_withNull = param.bsUnicode_withNull;
                IntPtr basesize =System.Runtime.InteropServices. Marshal.OffsetOf(typeof(FILE_RENAME_INFO), nameof(FILE_RENAME_INFO.FileName));
                int size = basesize.ToInt32() + bsUnicode_withNull.Length;
                void* pb = stackalloc byte[size];

                //Create FILE_RENAME_INFO
                FILE_RENAME_INFO* prename = (FILE_RENAME_INFO*)pb;
                prename->RootDirectory = new Windows.Win32.Foundation.HANDLE(IntPtr.Zero);
                prename->Anonymous.ReplaceIfExists = true;
                prename->FileNameLength = (uint)(bsUnicode_withNull.Length - sizeof(char));
                System.Runtime.InteropServices.Marshal.Copy(bsUnicode_withNull, 0, IntPtr.Add(new IntPtr(pb), basesize.ToInt32()), bsUnicode_withNull.Length);
                if (param.Type == TestType.UseDirectoryHandle && hdirDest != null)
                {
                    prename->RootDirectory = new Windows.Win32.Foundation.HANDLE(hdirDest.DangerousGetHandle());
                }

                param.ConsoleWrite(prename);

                var pathDir = param.Type == TestType.UseDirectoryHandle && hdirDest != null ? param.GetPathFromHandle(hdirDest) : "--";
                var pathFileBefore = param.GetPathFromHandle(hFile);

                Console.WriteLine($"\t---");
                Console.WriteLine($"\tTarget: {pathDir}");
                Console.WriteLine($"\tBefore: {pathFileBefore}");

                //System.Diagnostics.Debugger.Break();
                int result = SetFileInformationByHandle(hFile, FILE_INFO_BY_HANDLE_CLASS.FileRenameInfo, prename, (uint)size);
               
                var er2 = System.Runtime.InteropServices.Marshal.GetLastWin32Error();

                string pathFileAfter = param.GetPathFromHandle(hFile);
                Console.WriteLine($"\tAfter:  {pathFileAfter}");

                if (pathFileBefore == pathFileAfter)
                {
                    throw new TestFailException(er2, "File is not moved: ");
                }
                else if (!string.Equals(System.IO.Path.GetFileName(pathFileAfter), System.IO.Path.GetFileName(param.DestinationFileName), StringComparison.OrdinalIgnoreCase))
                {
                    throw new TestFailException(er2, "Name is not changed: ");
                }
                else if (param.Type == TestType.UseDirectoryHandle && !pathFileAfter.Contains(pathDir))
                {
                    throw new TestFailException(er2, "Move wrong: ");
                }
                else if (param.Type == TestType.AbsolutePath && !pathFileAfter.EndsWith(param.FileNameForAPI, StringComparison.OrdinalIgnoreCase))
                {
                    throw new TestFailException(er2, "Move wrong: ");
                }
                else if (er2 != 0)
                {
                    throw new TestFailException(er2, "Success, but has error: ");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Success");
                    Console.ResetColor();
                }
            }
            finally
            {
                param.CloseHandle();
            }
        }
    }
}
