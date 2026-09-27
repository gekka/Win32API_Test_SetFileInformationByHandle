#pragma once


using namespace System;

namespace Cpp {
	public ref class Class1
	{
	public:
		static int Exec(HANDLE hFile, void* p, UINT size)
		{
			FILE_RENAME_INFO* prename = (FILE_RENAME_INFO*)p;
			return SetFileInformationByHandle(hFile, FileRenameInfo, prename, size);
		}

	};
}
