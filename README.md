# 概要

Win32 APIのSetFileInformationByHandleでRootDirectoryを設定するとエラーとなる問題の調査のためのコード

## 問題提起へのリンク

https://learn.microsoft.com/ja-jp/answers/questions/6015074/setfileinformationbyhandle-filerenameinfo-rootdire?page=1&orderby=Helpful&translated=false#answers
https://stackoverflow.com/questions/36450222/moving-a-file-using-setfileinformationbyhandle

---

# 解析結果

SetFileInformationByHandleに以下の[FILE_RENAME_INFO](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_rename_info)を渡したとする

```
#SetFileInformationByHandle : FILE_RENAME_INFO
01 00 00 00 00 00 00 00 44 00 00 00 00 00 00 00
20 00 00 00 2E 00 5C 00 54 00 65 00 73 00 74 00
31 00 39 00 32 00 35 00 32 00 34 00 2E 00 74 00
78 00 74 00
```
これはRootDirectoryが0x44で、 FileNameは文字数が0x20の"./Test192524.txt"である。(ReplaceIfExistsはtrue)

SetFileInformationByHandleはいくつか処理したのちに[NtSetInformationFile](https://learn.microsoft.com/ja-jp/windows-hardware/drivers/ddi/ntifs/nf-ntifs-ntsetinformationfile)を呼び出している。

しかしNtSetInformationFileへ渡されるFILE_RENAME_INFORMATIONは以下のようになってしまっている。
これはRootDirectoryが0x44で、 FileNameは文字数が0x56の"\??\D:Test\bin\Debug\net481\Test192524.txt"である。

```
#NtSetInformationFile : FILE_RENAME_INFORMATION
01 00 00 00 0D F0 AD BA 44 00 00 00 00 00 00 00
56 00 00 00 5C 00 3F 00 3F 00 5C 00 44 00 3A 00
5C 00 54 00 65 00 73 00 74 00 5C 00 62 00 69 00
6E 00 5C 00 44 00 65 00 62 00 75 00 67 00 5C 00
6E 00 65 00 74 00 34 00 38 00 31 00 5C 00 54 00
65 00 73 00 74 00 31 00 39 00 32 00 35 00 32 00
34 00 2E 00 74 00 78 00 74 00
```

SetFileInformationByHandleのアセンブラを追っていくと、受けとった引数を[FILE_RENAME_INFORMATION](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ntifs/ns-ntifs-_file_rename_information)に変換してNtSetInformationFileを実行している。

引数チェックが通ったら元のファイル名がドライブレターを含んでいなければ[RtlDosPathNameToNtPathName_U_WithStatus関数](https://learn.microsoft.com/en-us/windows/win32/devnotes/rtldospathnametontpathname_u_withstatus)でNTパスに変換されている。
この変換により相対パスが絶対パスに変換されている。
なお、RtlDosPathNameToNtPathName_U_WithStatusではカレント基準で絶対パスに変換されているようだ。

注目するのはFILE_RENAME_INFOのRootDirectoryがそのままコピーされてしまっているということと、RtlDosPathNameToNtPathName_U_WithStatusが1文字の判定のみで変換が行われていること。

## 結論

SetFileInformationByHandleの現在の実装はFILE_RENAME_INFORMATIONに対して

- RootDirectoryにNULLを設定する方法がない
- FileNameに絶対パスを渡す場合RootDirectoryはNULLにしなければならない決まりとなっているのに RootDirectoryが設定されている。
- フルパスへの変換の判定が手抜きであること。

によりこの問題が発生しているのである。

## 回避方法

- 絶対パスを指定する。
- SetFileInformationByHandleを使わずにNtSetInformationFileを直接使う
