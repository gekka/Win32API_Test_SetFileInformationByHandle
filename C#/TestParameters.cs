using System;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.Foundation;
using static Windows.Win32.PInvoke;

namespace Gekka.Win.Bug.API.__SetFileInformationByHandle
{
    record class TestParameters
        (TestType Type
        , string SourceFileName
        , string DestinationFileName
        , string DestinationDirectoryPath)
    {
        public bool DeleteAfter { get; set; } = false;
        public string CurrentDirecory { get; set; } = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        public string dirSrc { get; set; } = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        public string dirDst => System.IO.Path.Combine(CurrentDirecory, this.DestinationDirectoryPath);

        public string pathAbsSrc => System.IO.Path.GetFullPath(System.IO.Path.Combine(dirSrc, this.SourceFileName));
        public string pathAbsDst => System.IO.Path.GetFullPath(System.IO.Path.Combine(dirDst, this.DestinationFileName));

        public string FileNameForAPI => (this.Type == TestType.AbsolutePath) ? pathAbsDst : this.DestinationFileName;

        public byte[] bsUnicode_withNull => System.Text.Encoding.Unicode.GetBytes(FileNameForAPI + "\0");

        public Microsoft.Win32.SafeHandles.SafeFileHandle? hdirDest { get; private set; }
        public Microsoft.Win32.SafeHandles.SafeFileHandle? hFile { get; private set; }

        public string GetPathFromDirectoryHandle()
        {
            if (hdirDest == null)
            {
                throw new InvalidOperationException();
            }
            return GetPathFromHandle(hdirDest);
        }

        public string GetPathFromFileHandle()
        {
            if (hFile == null)
            {
                throw new InvalidOperationException();
            }
            return GetPathFromHandle(hFile);
        }

        public unsafe string GetPathFromHandle(Microsoft.Win32.SafeHandles.SafeFileHandle handle, GETFINALPATHNAMEBYHANDLE_FLAGS flags = GETFINALPATHNAMEBYHANDLE_FLAGS.FILE_NAME_OPENED)
        {
            var len = GetFinalPathNameByHandle(handle, null, 0, flags);
            var mem = System.Runtime.InteropServices.Marshal.AllocCoTaskMem((int)len * 2);
            var pwstr = new PWSTR((char*)mem.ToPointer());
            len = GetFinalPathNameByHandle(handle, pwstr, len, flags);

            return pwstr.ToString();
        }

        public Microsoft.Win32.SafeHandles.SafeFileHandle? CreateDirectoryHandle()
        {
            if (hdirDest != null)
            {
                throw new InvalidOperationException();
            }

            if (Type == TestType.UseDirectoryHandle)
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
                    hdirDest = null;
                    throw new TestFailException();
                }
            }
            else
            {
                hdirDest = null;
            }
            return hdirDest;
        }

        public Microsoft.Win32.SafeHandles.SafeFileHandle CreateFileHandle()
        {
            if (hFile != null)
            {
                throw new InvalidOperationException();
            }
            hFile = CreateFile
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
                hFile = null;
                throw new TestFailException();
            }
            return hFile;
        }

        public void CloseHandle()
        {
            hFile?.Close();
            hdirDest?.Close();

            hFile = null;
            hdirDest = null;
        }

        public void CreateDestinationDirectiory()
        {
            //if (System.IO.Directory.Exists(dirDst))
            //{
            //    System.IO.Directory.Delete(dirDst, true);
            //}
            System.IO.Directory.CreateDirectory(dirDst);
        }

        public void CreateTestFile()
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(pathAbsSrc));
            using (var fs = System.IO.File.CreateText(pathAbsSrc))
            {
                fs.WriteLine("OK");
            }
        }

        public void PrepareForTest()
        {
            CreateDestinationDirectiory();
            CreateTestFile();

            System.IO.Directory.SetCurrentDirectory(CurrentDirecory);
        }

        public unsafe void ConsoleWrite(FILE_RENAME_INFO* prename)
        {
            Console.WriteLine($"TestType: {Type}");
            Console.WriteLine($"\tCurDir: {System.IO.Directory.GetCurrentDirectory()}");
            Console.WriteLine($"\tDir: {dirDst}");
            Console.WriteLine($"\tSrc: {pathAbsSrc}");
            Console.WriteLine($"\tDst: {pathAbsDst}");
            Console.WriteLine($"\tAPI: {FileNameForAPI}");
            Console.WriteLine($"\thFile: 0x{hFile?.DangerousGetHandle().ToString("X")}");
            Console.WriteLine($"\tFILE_RENAME_INFO->RootDirectory = 0x{prename->RootDirectory.Value.ToString("X")}");
        }
    }
}
