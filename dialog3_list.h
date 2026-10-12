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

#ifndef DIALOG_3_H_LIST
#define DIALOG_3_H_LIST

#include "stdinc.h"
using namespace std;
#include "dbl.h"
#include "dialog1.h"

class DupFinderDlg3 : public wxDialog
{
public:
	DupFinderDlg3(DupFinderDlg *, DuplicateFilesFinder &);
	
	~DupFinderDlg3();

	void OnClose(wxCloseEvent &);
	void OnSize(wxSizeEvent &);

	void OnStore(wxCommandEvent &);

	void CreateControls();
	void DisplayResults();

	void OnInitDialog(wxInitDialogEvent &);

	void OnListItemRightClick(wxListEvent &);
	void OnListItemActivated(wxListEvent &);

	void OnOpenFile(wxCommandEvent &); 
	void OnOpenDir(wxCommandEvent &); 
	void OnCopyFileName(wxCommandEvent &);
	void OnDelete(wxCommandEvent &);
	void OnSymLink(wxCommandEvent &);
	void OnHardLink(wxCommandEvent &);

	void OpenDir(long i);
	void OpenFile(long i);

	void ReturnToParent();

	void OnListKeyDown(wxListEvent &);

	void DeleteFiles(const list<long> &);

	void GetSelectedFilenameCount(long &count);
	long GetFirstSelectedFilename();
	long GetNextSelectedFilename(long i);

	void DeleteOrphanedHeaders();

	void OnCancel(wxCommandEvent &);

	void OnApply(wxCommandEvent &);
	void OnShowAll(wxCommandEvent &);

	void RestrictViewToDir(const wxString &);

	void ClearList();

	void OnRestToDir(wxCommandEvent &);
	void OnRestToSDir(wxCommandEvent &);
	
	void MenuRestToDir(bool);

	void OnGetDir(wxCommandEvent &);

	void CreateLink(bool (*)(const wxString &, const wxString &), const wxString &);

	bool IsMatching(const wxString &);

	void OnMaskChange(wxCommandEvent &);
	void OnDirChange(wxCommandEvent &);

	void OnDeleteButThis(wxCommandEvent &);

	void OnIdle(wxIdleEvent &);

	void DeleteSelection();

	long GetHeader(long);

	bool IsValidItem(long);

	void RefreshStats();

	void OnToggleDisplayOptions(wxCommandEvent &);

	void UpdateView();

	// Modern features: Empty dirs & Common folder suggestions
	void DisplayEmptyDirs();
	void DisplayCommonFolders();
	void OnDeleteSelectedEmptyDirs(wxCommandEvent &);
	void OnDeleteAllSafeEmptyDirs(wxCommandEvent &);
	void OnApplyFolderSuggestion(wxCommandEvent &);

private:
	static bool bHardLinkWarning;

	DuplicateFilesFinder &dupfinder;
	list<DuplicatesGroup> &duplicates;
	list<CommonFolderSuggestion> commonFolders;

	wxListView *wResultList;
	wxListView *wEmptyDirList;
	wxListView *wCommonFoldersList;

	wxButton   *wBtnDelEmptyDirs;
	wxButton   *wBtnDelAllEmptyDirs;
	wxButton   *wBtnApplySuggestion;

	wxCheckBox *wConfDelete;
	wxTextCtrl *wDirName;
	wxCheckBox *wSubDirs;
	wxCheckBox *wRestrictToDir;
	wxCheckBox *wRestrictToMask;
	wxTextCtrl *wMask;
	wxGauge    *wProgress;
	wxStaticBoxSizer *wStats;
	wxBoxSizer *m_wOptions;
	wxToggleButton *m_wShowOptions;
	
	DupFinderDlg *parent;

	wxFileName RestrictToDir;
	bool bRestrictToDir;
	bool bRestrictToMask;

	long rightClickedItem;

	bool bFirstIdle;
	
	DECLARE_EVENT_TABLE()
};


#endif
