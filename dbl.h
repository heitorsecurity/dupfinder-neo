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


#ifndef __DBL_H_123
#define __DBL_H_123

#include "file.h"
#include "os_cc_specific.h"

struct DuplicatesGroup
{
	wxULongLong size;
	list<File> files;
};

struct SearchPathInfo
{
	wxString path;
	wxULongLong nMinSize;
	wxULongLong nMaxSize; // if 0, no max size
	bool bRecursive;
	bool bHidden;
	wxString Include;
	wxString Exclude;
	bool bEmptyFiles;
};

enum ScanTargetMode {
	SCAN_MODE_DUPLICATES_ONLY = 0,
	SCAN_MODE_EMPTY_DIRS_ONLY = 1,
	SCAN_MODE_BOTH = 2
};

struct EmptyDirInfo {
	wxString path;
	bool isSystemProtected;
	time_t modifiedTime;
};

struct CommonFolderSuggestion {
	wxString folderPath;
	wxString primaryFolderPath;
	int duplicateFilesCount;
	wxULongLong wastedBytes;
	bool isCompleteDuplicateFolder;
	wxString recommendation;
};

class DupFinderDlg2;

// no DLLEXPORT because of linking! 
struct GuiInfo {
	// control
	bool bContinue;
	bool bPause;

	// the dialog
	wxWindow *dialog2;

	// vars for step 1
	wxStaticText *wStep1;

	wxTextCtrl *out;
	wxApp * theApp;

	wxStaticText *nfiles;
	// wxStaticText *cfiles;

	// vars for step 2
	wxStaticText *wStep2;

	wxStaticText *wSpeed;
	wxStaticText *wProgress;
	wxGauge *wProgressGauge;

	// fonts 
	wxFont normalfont, boldfont;
};

struct DuplicateFilesStats
{
	wxULongLong nDuplicateFiles;
	wxULongLong nWastedSpace;
	wxULongLong nFilesWithDuplicates;
	wxULongLong nEmptyDirsFound;
	wxULongLong nEmptyDirsRemoved;
};

// this is the class which does all the work

class DUPF_DLLEXPORT DuplicateFilesFinder : public wxExtDirTraverser
{
public:
	DuplicateFilesFinder(GuiInfo * _gui, bool _bQuiet);
	~DuplicateFilesFinder();

	void AddPath(const SearchPathInfo &path);

	void Reset();

	void FindDuplicateFiles();

	list<DuplicatesGroup> &GetDuplicates();

	void SetGui(GuiInfo *gui) { m_gui = gui; }

	void CalculateStats(DuplicateFilesStats &) const;

	const list<SearchPathInfo> &GetPaths() { return m_paths; }

	// New modern features
	void SetScanMode(ScanTargetMode mode) { m_scanMode = mode; }
	ScanTargetMode GetScanMode() const { return m_scanMode; }

	void SetProtectSystemDirs(bool protect) { m_protectSystemDirs = protect; }
	bool GetProtectSystemDirs() const { return m_protectSystemDirs; }

	const list<EmptyDirInfo> &GetEmptyDirs() const { return m_emptyDirs; }
	list<EmptyDirInfo> &GetEmptyDirs() { return m_emptyDirs; }

	static bool IsSystemProtectedDir(const wxString &dirPath);
	bool DeleteEmptyDir(const wxString &dirPath);

	list<CommonFolderSuggestion> AnalyzeCommonFolders() const;

private:
	// private structures

	struct fileinfoequal
	{
		list<File> files;
	};

	struct fileinfosize
	{
		wxULongLong size;
		list<File> files;
		list<fileinfoequal> equalfiles;
	};

	
	struct less_fileinfosize : public less<fileinfosize> {
		bool operator () (const fileinfosize &a, const fileinfosize &b) const {
			return a.size > b.size;
		}
	};

	typedef multiset<fileinfosize, less_fileinfosize> multiset_fileinfosize;

private: // private variables

	// paths to search in
	list<SearchPathInfo> m_paths;

	// display options
	// console output?
	bool m_quiet;
	// for status display
	GuiInfo * m_gui;

	// the final results
	list<DuplicatesGroup> m_duplicates;

	// internal data for computation
	// of duplicate files
	multiset_fileinfosize m_sortedbysize;
	list<wxString> m_ExcludeMasks;
	const SearchPathInfo *m_current_spi;

	enum DupFinderState { 
		DUPF_STATE_ERROR = -1, 
		DUPF_STATE_NOT_STARTED_YET = 0, 
		DUPF_STATE_FIND_FILES, 
		// DUPF_STATE_REMOVE_DOUBLE_FILES, 
		DUPF_STATE_COMPARE_FILES, 
		DUPF_STATE_FINISHED
	};
	DupFinderState m_state, m_prevState;

	// variables, which determine
	// whether status display 
	// should be updated
	time_t m_tlast;
	bool m_bFirst;
	
	// in console version last 
	// string written on console
	// (needed for deleteline)
	wxString m_output;

	// statistics (speed)
	wxULongLong m_nBytesRead;
	wxULongLong m_nPrevBytesRead;
	wxULongLong m_nFilesRead;
	wxULongLong m_nPrevFilesRead;
	wxULongLong m_nSizesDone;
	wxULongLong m_nPrevSizesDone;

	// All bytes (not these only read)
	// Range from 0 to nSumBytes!
	wxULongLong m_nBytesDone; 
	wxULongLong m_nPrevBytesDone; 

	// total amount of files/bytes to read/sizes to process
	wxULongLong m_nSumFiles;
	wxULongLong m_nSumSizes;
	wxULongLong m_nSumBytes;

	// current directory searched in for status display
	wxString m_curdir;

	// Modern features: Scan Mode, Empty Directory Finder, System Protection
	ScanTargetMode m_scanMode;
	bool m_protectSystemDirs;
	list<EmptyDirInfo> m_emptyDirs;
	set<wxString> m_visitedDirs;
	set<wxString> m_dirsWithFiles;
	set<wxString> m_dirsWithSubdirs;

private:
	// private procedures
	
	void	FindFiles();
	void	FindEmptyDirectories();
	void	GetEqualFiles();
	void 	ConstructResultList();

	bool	YieldAndTestAbort();

	bool 	CompareFiles(File &, File &, const wxULongLong &);
	static wxULongLong ComputeSampleHash(File &file, const wxULongLong &size);

	bool 	Traverse(const SearchPathInfo *);

	// void RemoveDoubleFiles();
	void RemoveUnimportantSizes();

	// status output
	void UpdateStatusDisplay();
	void ResetLine();

	// prevent direct copies
	DuplicateFilesFinder(const DuplicateFilesFinder &) : wxExtDirTraverser() {}
	DuplicateFilesFinder &operator =(const DuplicateFilesFinder &) { return *this; }

public: // wxExtDirTraverser Funktionen

	virtual wxDirTraverseResult OnFile(const wxString &, const wxULongLong *);

	virtual wxDirTraverseResult OnFile(const wxString & filename) {
		return OnFile(filename, NULL);
	}

	virtual wxDirTraverseResult OnExtFile(const FileData &data) {
		return OnFile(data.name, &data.size);
	}

	virtual wxDirTraverseResult OnDir(const wxString &dirname);

private:
	wxDirTraverseResult UpdateInfo(const wxString *dirname);


#ifdef PROFILE
	public:
		LARGE_INTEGER __OnFile;
		LARGE_INTEGER __OnDir;
		LARGE_INTEGER __findsize;
		LARGE_INTEGER __normalize;
		LARGE_INTEGER __finddir;
		LARGE_INTEGER __insert;
#endif

};

#endif /* defined(__DBL_H_123) */

