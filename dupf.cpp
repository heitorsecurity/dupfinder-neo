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

#include "dbl.h"
#include "os_cc_specific.h"
#include "profile.h"
#include "dupf.h"

// int _tmain(int argc, _TCHAR *_argv[]) 
// {
// a little bit ugly, this DECLARE_MAIN
// that's because of mingw32 build which
// does not support wmain, but still unicode
DECLARE_MAIN
	list<File>::iterator it, it3;

	clock_t tstart, tend;
	bool bReverse;
	int i;
	int nOptions;
	wxFile fOutput;
	bool bQuiet;
	bool bHelp;

	::wxInitialize();
	// timestamps are ugly
	wxLog::GetActiveTarget()->SetTimestamp(NULL);

	INITPROFILE;

	STARTTIME(all);

	tstart = clock();


	if(argc == 0) {
		DisplayHelp();
		return 1;
	}

	// defaults
	nOptions = 0;
	fOutput.Attach(wxFile::fd_stdout);
	bReverse = false;
	bQuiet = false;
	bHelp = false;

	// get general options
	for(i = 0; i < argc && argv[i][0] == _T('-'); i++) {
		if(argv[i] == _T("-r")
			|| argv[i] == _T("--reverse")) {
			bReverse = true;
			nOptions += 1;
		}
		else if(argv[i] == _T("-o") ||
			argv[i] == _T("--out")) {
			bool bResult;
			if(i+1 >= argc) {
				_ftprintf(stderr, _T("Error: Filename expected! \n"));
				return 1;
			}
			bResult = fOutput.Create(argv[i+1], true);
			if(!bResult) {
				_ftprintf(stderr, _T("Error: Cannot open %s! \n"), argv[i+1].c_str());
				return 1;
			}
			nOptions += 2;
			i++;
		} 
		else if (argv[i] == _T("-q") || 
			argv[i] == _T("--quiet")) {
			bQuiet = true;
			nOptions += 1;
		} else if(argv[i] == _T("-h") || 
			argv[i] == _T("--help")) {
			bHelp = true;
			nOptions += 1;
		} else {
			_ftprintf(stderr, _T("Error: unrecognized option %s. \n"), argv[i].c_str());
			return 1;
		}
	}

	if(bHelp) {
		DisplayHelp();
		return 1; // 1 or 0, who cares...
	}

	if(argc-nOptions <= 0) {
		_ftprintf(stderr, _T("Error in commandline: arguments expected! \n"));
		return 1;
	}

	DuplicateFilesFinder dupf(NULL, bQuiet);

	for(/*i*/; i < argc; ) {
		SearchPathInfo pi;
		pi.path = argv[i];
		pi.nMinSize = 0;
		pi.nMaxSize = 0;
		pi.bRecursive = true;
		pi.bHidden = false;
		pi.Include = wxEmptyString;
		pi.Exclude = wxEmptyString;
		pi.bEmptyFiles = false;

		for(i++; i < argc && argv[i][0] == '-'; i++) {
			if(argv[i] == _T("--min")) {
				if(i+1 < argc) {
					wxULongLong_t _nMinSize;
					if(_stscanf_s(argv[i+1], _T("%") wxLongLongFmtSpec _T("u"), &_nMinSize) == 0) {
						_ftprintf(stderr, _T("Error in command line: Number expected! \n"));
						return 1;
					}
					pi.nMinSize = _nMinSize;
				} else {
					_ftprintf(stderr, _T("Error in command line: Number expected! \n"));
					return 1;
				}
				i++;
			}
			else if(argv[i] == _T("--max")) {
				if(i + 1 < argc) {
					wxULongLong_t _nMaxSize;
					if(_stscanf_s(argv[i+1], _T("%") wxLongLongFmtSpec _T("u"), &_nMaxSize) == 0) {
						_ftprintf(stderr, _T("Error in command line: Number expected! \n"));
						return 1;
					}
					pi.nMaxSize = _nMaxSize;
					if(pi.nMaxSize == 0) {
						_ftprintf(stderr, _T("Warning: Ignore maximal size of 0. \n"));
					}
				}
				else {
					_ftprintf(stderr, _T("Error in command line: Number expected! \n"));
					return 1;
				}
				i++;
			}
			else if(argv[i] == _T("-n") || 
				argv[i] == _T("--norecurse")) {
				pi.bRecursive = false;
			}
			else if(argv[i] == _T("-h") ||
				argv[i] == _T("--hidden")) {
				pi.bHidden = true;
			}
			else if(argv[i] == _T("-i") ||
				argv[i] == _T("--include")) {
				if(i+1 >= argc) {
					_ftprintf(stderr, _T("Error: expression expected! \n"));
					return 1;
				}
				pi.Include = argv[i+1];
				i ++;
			}
			else if(argv[i] == _T("-e") || 
				argv[i] == _T("--exclude")) {
				if(i+1 >= argc) {
					_ftprintf(stderr, _T("Error: argument expected! \n"));
					return 1;
				}
				pi.Exclude = argv[i+1];
				i++;
			}
			else if(argv[i] == _T("-z") || 
				argv[i] == _T("--zero")) {
				pi.bEmptyFiles = true;
			} else {
				_ftprintf(stderr, _T("Error: unrecognized option %s. \n"), argv[i].c_str());
				return 1;
			}
		}

		if(pi.nMinSize > pi.nMaxSize && pi.nMaxSize != 0) {
			_ftprintf(stderr, _T("Error: Maximal size must be greater than minimal size. \n"));
			return 1;
		}

		if(pi.Include == _T("")) {
			pi.Include = _T("*");
		}

		dupf.AddPath(pi);
	}

	const list<SearchPathInfo> &paths = dupf.GetPaths();
	list<SearchPathInfo>::const_iterator it5;

	list<wxString> dirs;
	for(it5 = paths.begin(); it5 != paths.end(); it5++) {
		if(!wxFileName::DirExists(it5->path)) {
			_ftprintf(stderr, _T("Error: \"%s\" does not exist! \n"), it5->path.c_str());
			return 1;
		}


		wxFileName dir = wxFileName::DirName(it5->path.c_str());
		dir.Normalize(wxPATH_NORM_ALL | wxPATH_NORM_CASE); 
		dir.Normalize(wxPATH_NORM_CASE);

		dirs.push_back(dir.GetFullPath());
	}

	list<wxString>::iterator it;
	bool bDisplayedHeader = false;
	for(it = dirs.begin(); it != dirs.end(); it++) {
		list<wxString>::iterator it2;
		it2 = it;
		for(it2++; it2 != dirs.end(); it2++) {
			// C:\dir1\dir2 C:\dir\dir3 is correct
			// C:\dir1  C:\dir1\dir2 not

			if(it->StartsWith(*it2) || it2->StartsWith(*it)) {
				bool bCase = it->StartsWith(*it2);
				if(!bDisplayedHeader) {
					_ftprintf(stderr, _T("\nWARNING: \n---------\n"));
					bDisplayedHeader = true;
				}
				_ftprintf(stderr, _T("The Path \"%s\" is contained in \"%s\"! \n"), 
					bCase ? it->c_str() : it2->c_str(), bCase ? it2->c_str() : it->c_str() );
			}			
		}
	}

	if(bDisplayedHeader) {
		_ftprintf(stderr, _T("Correct the command line for avoiding trivial duplicates. \n\n\n"));
	}


	dupf.FindDuplicateFiles();

	DuplicateFilesStats stats;

	dupf.CalculateStats(stats);

	list<DuplicatesGroup> &duplicates = dupf.GetDuplicates();

	PrintResults(duplicates, stats, fOutput, bReverse, bQuiet);

	fOutput.Close();

	tend = clock();

	/* Calculation and output of consumed time */

	const int tmpsize = 20;
	_TCHAR szMinutes[tmpsize] = _T("");
	_TCHAR szHours[tmpsize] = _T("");
	_TCHAR szDays[tmpsize] = _T("");
	
	double dseconds = (double)(tend-tstart)/CLOCKS_PER_SEC;
	int sumseconds = (int)dseconds;
	int seconds = sumseconds % 60;
	int minutes = (sumseconds/60) % 60;
	int hours = (sumseconds/60/60) % 24;
	int days = sumseconds/60/60/24;

	if(days) {
		::wxSnprintf(szDays, tmpsize, _T("%i d, "), days);
		::wxSnprintf(szHours, tmpsize, _T("%i h, "), hours);
		::wxSnprintf(szMinutes, tmpsize, _T("%i min, %i sec"), minutes, seconds);
	}
	else if(hours) {
		::wxSnprintf(szHours, tmpsize, _T("%i h, "), hours);
		::wxSnprintf(szMinutes, tmpsize, _T("%i min, %i sec"), minutes, seconds);
	}
	else if(minutes) { 
		::wxSnprintf(szMinutes, tmpsize, _T("%i min, %i sec"), minutes, seconds);
	}
	else {
		::wxSnprintf(szMinutes, tmpsize, _T("%.2f sec"), dseconds);
	}

	if(!bQuiet) {
		_ftprintf(stderr, _T("Time: %s%s%s. \n"), szDays, szHours, szMinutes);
	}

	STOPTIME(all);

#ifdef PROFILE
	_ftprintf(stderr, _T("Profile-Times: \n"));
	_ftprintf(stderr, _T("   all: %.3f\n"), SECONDS(all));
	_ftprintf(stderr, _T("   compare-time: %.3f (%.3f %% of all)\n"), 
		SECONDS(comparetime), SECONDS(comparetime)/SECONDS(all)*100);
	_ftprintf(stderr, _T("   disk: %.3f (%.3f %% of all, %.3f %% of comparetime)\n"), 
		SECONDS(disk), SECONDS(disk)/SECONDS(all)*100, SECONDS(disk)/SECONDS(comparetime)*100);
	_ftprintf(stderr, _T("     fileopen: %.3f (%.3f %%)\n"), SECONDS(fileopen), SECONDS(fileopen)/SECONDS(disk)*100);
	_ftprintf(stderr, _T("     fileseek: %.3f (%.3f %%)\n"), SECONDS(fileseek), SECONDS(fileseek)/SECONDS(disk)*100);
	_ftprintf(stderr, _T("     fileread: %.3f (%.3f %%)\n"), SECONDS(fileread), SECONDS(fileread)/SECONDS(disk)*100);
#endif

	::wxUninitialize();
	
	return 0;
}


static bool IsAscii(const wxString &string) 
{
#if !(defined( _UNICODE) || defined(UNICODE))
	(void)string;
	return true;
#else
	size_t size = string.Length();
	size_t i;

	for(i = 0; i < size; i++) {
		if((wchar_t) string[i] > 255) {
			return false;
		}
	}
	return true;
#endif
}

static const char * ToAscii(const wxString &string)
{
#if !(defined( _UNICODE) || defined(UNICODE))
	return string.c_str();
#else
	size_t i, size = string.Length();
	char *result = new char[size+1];

	for(i = 0; i < size; i++) {
		if(string[i] > 255) {
			result[i] = '?';
		}
		else {
			result[i] = string[i];
		}
	}
	result[i] = 0;

	return result;
#endif
}

void PrintResults(const list<DuplicatesGroup> &sortedbysize, 
	const DuplicateFilesStats & stats, wxFile &fOutput, bool bReverse, bool bQuiet)
{
	list<File>::const_iterator it, it3;
	list<DuplicatesGroup>::const_iterator it2;
	list<DuplicatesGroup>::const_reverse_iterator rit2;
	wxString Buffer;
	// bConOut because output to console does NOT support unicode/utf-8/... in windows (but it does in unix! )
	wxPlatformInfo platform;
	bool bConOut = (fOutput.GetKind() == wxFILE_KIND_TERMINAL) && 
		(platform.GetOperatingSystemId() & wxOS_WINDOWS);
	bool bDisplayWarning = false;

	if(!bQuiet) {
		_ftprintf(stderr, _T("Printing the results...\n\n"));
	}

	for(
		rit2 = sortedbysize.rbegin(), it2 = sortedbysize.begin();
		bReverse ? rit2 != sortedbysize.rend() : it2 != sortedbysize.end();
		rit2++, it2++) {


		Buffer.Printf(_T("- Equal (%i files of size %") wxLongLongFmtSpec _T("u): \r\n"), 
			bReverse ? rit2->files.size() : it2->files.size(), 
			bReverse ? rit2->size.GetValue() : it2->size.GetValue());

		if(bConOut) {
			fOutput.Write(ToAscii(Buffer), Buffer.Length());
			if(!IsAscii(Buffer)) {
				bDisplayWarning = true;
			}
		}
		else {
			fOutput.Write(Buffer, bConOut ? (wxMBConv &)wxConvUTF8 : (wxMBConv &)wxConvUTF8);
		}
		for(it = it2->files.begin(); it != it2->files.end(); it++) {
			Buffer.Printf(_T("  \"%s\"\r\n"), it->GetName().c_str());
			if(bConOut) {
				fOutput.Write(ToAscii(Buffer), Buffer.Length());
				if(!IsAscii(Buffer)) {
					bDisplayWarning = true;
				}
			}
			else {
				fOutput.Write(Buffer, bConOut ? (wxMBConv &)wxConvUTF8: (wxMBConv &)wxConvUTF8);
			}
		}
	}

	if(bDisplayWarning) {
		_ftprintf(stderr, _T("\n--- WARNING --- \nThe output contains unicode characters which cannot be displayed correctly \n")
			_T("on the console screen! \nIf you want to get the correct filenames, use the -o option! \n\n"));
	}

	if(!bQuiet) {
		_ftprintf(stderr, _T("Found %") wxLongLongFmtSpec _T("u files, of which exist at least one more copy. "), 
			stats.nFilesWithDuplicates.GetValue());
		_ftprintf(stderr, _T("%") wxLongLongFmtSpec _T("u duplicates consume altogether %") wxLongLongFmtSpec
			 _T("u bytes (%") wxLongLongFmtSpec _T("u kb, %") wxLongLongFmtSpec _T("u mb)\n"), 
			stats.nDuplicateFiles.GetValue(), stats.nWastedSpace.GetValue(), 
			stats.nWastedSpace.GetValue()/1024, stats.nWastedSpace.GetValue()/1024/1024);
	}
}

void DisplayHelp()
{
	_ftprintf(stderr, _T("dupf: Finds duplicate files\n"));
	_ftprintf(stderr, _T("Copyright (c) Matthias Boehm 2007-2008\n\n"));
	_ftprintf(stderr, _T("dupf [GENERAL OPTIONS] Path1 [PATH-OPTIONS] [Path2 [PATH-OPTIONS]] ...\n\n"));
	_ftprintf(stderr, _T("General options  : \n\n"));
	_ftprintf(stderr, _T("-r   | --reverse  : small files first (default is big files first)\n"));
	_ftprintf(stderr, _T("-o x | --out x    : Print results to file x\n"));
	_ftprintf(stderr, _T("-q   | --quiet    : No progress display\n"));
	_ftprintf(stderr, _T("-h   | --help     : Display this help\n"));
	_ftprintf(stderr, _T("\nOptions for each path: \n\n"));
	_ftprintf(stderr, _T("       --min x    : Ignore files smaller than x (in bytes)\n"));
	_ftprintf(stderr, _T("       --max x    : Ignore files larger than x (in bytes)\n"));
	_ftprintf(stderr, _T("-n   | --norecurse: do not recurse into subdirectories\n"));
	_ftprintf(stderr, _T("-h   | --hidden   : include hidden files in search (default: off)\n"));
	_ftprintf(stderr, _T("-i   | --include x: search files which match the file mask\n"));
	_ftprintf(stderr, _T("-e   | --exclude x: exclude files which match mask\n"));
	_ftprintf(stderr, _T("-z   | --zero     : include files with length 0 (empty files)\n"));
}
