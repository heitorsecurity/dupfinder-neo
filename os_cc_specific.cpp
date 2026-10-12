/*
 *  Duplicate Files Finder - search for duplicate files
 *  Copyright (C) 2007-2008 Matthias Boehm
 *
 *  This program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation; either version 2 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program; if not, write to the Free Software
 *  Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.
 *
 */


#include "stdinc.h"
using namespace std;

#include "os_cc_specific.h"

#ifdef _WIN32

void Traverse(const wxString &RootDir, const wxString *mask, int nMask, 
	/* wxString (*exclmask)[], int nExclMask, */ int flags, wxExtDirTraverser &sink) 
{
	WIN32_FIND_DATA fd;
	wxString Dir;
	wxString Search;
	HANDLE hFind;
	bool bNoEnd;
	bool bEnd = false;
	int i;

	wxFileName tmp = wxFileName::DirName(RootDir);

	Dir = tmp.GetPathWithSep();

	// first search directories

	if(flags & wxDIR_DIRS && !bEnd) {

		Search = Dir;
		Search.Append(_T("*"));

		hFind = FindFirstFile(
			Search, 
			&fd);

		if(hFind == INVALID_HANDLE_VALUE)
			return;

		do
		{
			if(fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY 
				&& ( !(fd.dwFileAttributes & FILE_ATTRIBUTE_HIDDEN) 
				|| flags & wxDIR_HIDDEN )) {

				if(!( wxString(fd.cFileName) == _T(".") || 
					wxString(fd.cFileName) == _T("..") )) {
				
					wxString Down;
					Down = Dir;
					Down.Append(fd.cFileName);

					wxDirTraverseResult result = sink.OnDir(Down);

					if(result == wxDIR_STOP) {
						bEnd = true;
						break; 
					}
				
					if(result != wxDIR_IGNORE) {
						Traverse(Down, mask, nMask, /*exclmask, nExclMask, */flags, sink); 
					}
				}
			}

			bNoEnd = FindNextFile(hFind, &fd);

		} while(bNoEnd);

		FindClose(hFind);
	}


	// then search files

	if(flags & wxDIR_FILES && !bEnd) {

		for(i = 0; i < nMask; i++) {

			assert(mask[i] != wxEmptyString);
			Search = Dir;
			Search.Append(mask[i]);

			hFind = FindFirstFile(
				Search, 
				&fd);

			if(hFind == INVALID_HANDLE_VALUE) {
				continue; 
			}

			do
			{
				if(!(fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)
					&& (!( fd.dwFileAttributes & FILE_ATTRIBUTE_HIDDEN) 
					|| flags & wxDIR_HIDDEN )) {

					/* can be removed ?! */
					if(!( wxString(fd.cFileName) == _T(".") || 
					   	wxString(fd.cFileName) == _T("..") )) {

						bool bMatches = false;
						/*for(j = 0; j < nExclMask; j++) {
							if(wxString(fd.cFileName).Matches((*exclmask)[j]) ) {
								bMatches = true;
								break;
							}
						}
						bMatches = true; */

						if(!bMatches) {
					
							FileData data;
							data.name = Dir;
							data.name.Append(fd.cFileName);
							data.size = wxULongLong(fd.nFileSizeHigh, fd.nFileSizeLow);
							
							wxDirTraverseResult result = sink.OnExtFile(data);
							
							if(result == wxDIR_STOP) {
								bEnd = true;
								break;
							}
						}
					}
				}

				bNoEnd = FindNextFile(hFind, &fd);
	
			} while(bNoEnd);

			FindClose(hFind);
		}
	}

	// borland warning fix
	(void)bEnd;
}

bool IsSymLink(const wxString &WXUNUSED(filename)) {
	// if name ends on ".lnk", it is a symbolic windows link!
	// actually, there should be a test, whether it's really 
	// a symbolic link or just a file with .lnk ending!
	/* wxFileName name(filename);
	return name.GetExt() == _T("lnk");  */
	return false;
}

bool IsSymLinkSupported() {
	// use .lnk files for symbolic links
	return true;
}

// copied from PSDK documentation
// ***************************************************************************
// CreateLink - uses the Shell's IShellLink and IPersistFile interfaces 
//              to create and store a shortcut to the specified object. 
//
// Returns the result of calling the member functions of the interfaces. 
//
// Parameters:
// lpszPathObj  - address of a buffer containing the path of the object. 
// lpszPathLink - address of a buffer containing the path where the 
//                Shell link is to be stored. 
// lpszDesc     - address of a buffer containing the description of the 
//                Shell link. 

HRESULT CreateLink(LPCTSTR lpszPathObj, LPCTSTR lpszPathLink, LPCTSTR lpszDesc) 
{ 
	HRESULT hres; 
	IShellLink* psl; 
 
	// Get a pointer to the IShellLink interface. 
	hres = CoCreateInstance(CLSID_ShellLink, NULL, CLSCTX_INPROC_SERVER, 
                            IID_IShellLink, (LPVOID*)&psl); 
	if (SUCCEEDED(hres)) 
	{ 
		IPersistFile* ppf; 
 
		// Set the path to the shortcut target and add the description. 
		psl->SetPath(lpszPathObj); 
		psl->SetDescription(lpszDesc); 

 
		// Query IShellLink for the IPersistFile interface for saving the 
		// shortcut in persistent storage. 
		hres = psl->QueryInterface(IID_IPersistFile, (LPVOID*)&ppf); 
 
		if (SUCCEEDED(hres))
		{
#if !( defined (_UNICODE) || defined(UNICODE) )
			size_t length = wxConvFile.ToWChar(NULL, 0, lpszPathLink);
			wchar_t * UnicodePath = new wchar_t [length];
			size_t res = wxConvFile.ToWChar(UnicodePath, length, lpszPathLink);
			(void)res; // warning fix when unicode=0 debug=0
			assert(res != wxCONV_FAILED);
#else
			const wchar_t *UnicodePath = lpszPathLink;
#endif
			// Save the link by calling IPersistFile::Save. 

			hres = ppf->Save(UnicodePath, TRUE); 
			ppf->Release(); 
#if !( defined (_UNICODE) || defined(UNICODE) )
			delete [] UnicodePath;
#endif

		} 
		psl->Release(); 
	} 
	return hres; 
}


bool CreateSymLink(const wxString &source, const wxString &_target) {
	wxString target = _target;
	target.Append(_T(".lnk"));
	if(wxFile::Exists(target)) {
		return false;
	} else {
		return SUCCEEDED( CreateLink(source, target, _T("Link created by Duplicate Files Finder")) );
	}
}

bool IsHardLinkSupported() {
	wxPlatformInfo pi;
	// is the NT family the only one which supports 
	// hardlinks? What about Microwindows, win CE?
	if(!(pi.GetOperatingSystemId() & wxOS_WINDOWS_NT)) {
		return false;
	} else {
		return true;
	}
}

bool CreateHardLink(const wxString &oldpath, const wxString &newpath) {
	wxDynamicLibrary kernel32(_T("kernel32.dll"));

	void *CreateHardLinkFun = kernel32.GetSymbolAorW(_T("CreateHardLink"));

	if(CreateHardLinkFun == NULL) {
		// we are working with Windows 95 family
		return false;
	}
	else {
		// windows NT upwards :-)
		return ((BOOL (WINAPI *)(
  			  LPCTSTR lpFileName,
			  LPCTSTR lpExistingFileName,
			  LPSECURITY_ATTRIBUTES lpSecurityAttributes
			) ) CreateHardLinkFun)(newpath.fn_str(), oldpath.fn_str(), NULL);
	}
}

#endif /* _WIN32 */


#if defined( __UNIX__ ) && !defined(_WIN32)
/*
void Traverse(const wxString &RootDir, const wxString &mask, int flags, wxExtDirTraverser &sink) 
{
	const _TCHAR * array[2] = { NULL, NULL };
	array[0] = new _TCHAR[RootDir.Length() + 1];
	_tscpy(array[0], RootDir.c_str());
	FTS *hFind;
	FTSENT *fe;
	wxString FileName;
	wxULongLong size;

	errno = 0;
	hFind = fts_open((_TCHAR *const*)array, FTS_PHYSICAL, NULL);

	if(errno != 0) {
		return;
	}

	do {
		fe = fts_read(hFind);

		if(fe != NULL) {
			if(fe->fts_info == FTS_F) {
				_tcscpy_s(ff.cFileName, MAX_PATH, fe->fts_path);
				ff.size.QuadPart = fe->fts_statp->st_size;
				// ff.bDirectory = fe->fts_info == FTS_D;
				function(&ff, pData);
			}
		}

	} while(fe != NULL);

	fts_close(hFind);
}
*/

bool IsSymLink(const wxString &filename) {
	struct stat st;

	if(lstat(filename.fn_str(), &st) == 0) {
		return S_ISLNK(st.st_mode) ? true : false;
	}
	else {
		return false;
	}
}

bool IsSymLinkSupported() {
	// this is not always true, e.g. if 
	// there are filesystems mounted (e.g fat)
	return true;
}

bool CreateSymLink(const wxString &oldpath, const wxString &newpath) {
	return symlink(oldpath.fn_str(), newpath.fn_str()) == 0;
}

bool IsHardLinkSupported() {
	// unix: yes!
	return true;
}

bool CreateHardLink(const wxString &oldpath, const wxString &newpath) {
	return link(oldpath.fn_str(), newpath.fn_str()) == 0;
}

#endif /* __UNIX __ */

wxChar GetPathSepChar() {
	wxPlatformInfo pi;
	if(pi.GetOperatingSystemId() & wxOS_UNIX) {
		return _T(':');
	}
	else if (pi.GetOperatingSystemId() & wxOS_WINDOWS) {
		return _T(';');
	}
	else {
		
		wxLogFatalError(_T("Unknown OS! "));
		abort();
		return _T(' '); // removes warnings
	}
}

static wxULongLong_t StrToULongLong(const wxString & str, bool &bResult) {
	
	// no test whether it's a valid string
	// not necessary because of Validator
	wxULongLong_t value, lastvalue;
	int size = str.Length();
	int i;
	
	value = lastvalue = 0;
	for(i = 0; i < size; i++) {
		value *= 10;
		// test for overflow
		if(lastvalue > value) {
			bResult = false;
			return 0;
		}
		
		if(str[i] < '0' || str[i] > '9') {
			break;
		}
		value += str[i]-'0';
		// test for overflow
		if(lastvalue > value) {
			bResult = false;
			return 0;
		}
		lastvalue = value;
	}

	bResult = true;

	return value;
}

bool StringToULongLong(const wxString &str, wxULongLong &_result) 
{
	wxULongLong_t result;
	wxLongLong_t test;
	bool bResult, bResultTest;

#if wxCHECK_VERSION(2,7,2)
	bResultTest = str.ToLongLong(&test);
	if(test < 0 && bResultTest) {
		return false;
	}
	// supported first with 2.7.2
	bResult = str.ToULongLong(&result);
#else
	bResult = false;
#endif

	if(!bResult) {
		// this is ONLY the case, if the number is a) too big
		// or b) we are using mingw
		result = StrToULongLong(str, bResult);
	}

	if(bResult) {
		_result = result;
	}

	return bResult;
}



