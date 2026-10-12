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


#ifndef __DIALOG_1
#define __DIALOG_1

#include "stdinc.h"
using namespace std;

#include "dbl.h"

#define DUPF_NUMBER_OF_COLS 8

class DupFinderDlg : public wxDialog {
public:
	DupFinderDlg(wxWindow *);
	~DupFinderDlg();

	void OnClose(wxCloseEvent &);
	void OnSize(wxSizeEvent &);

	void OnEscape(wxCommandEvent &);
	void OnOk(wxCommandEvent &);

	void OnDlgChange(wxCommandEvent &WXUNUSED(event));
	void OnListSelChange(wxListEvent &WXUNUSED(event));
	
	void InitControls();
	void UpdateView();
	void CreateControls();

	void UpdateList();

	void OnDirAdd(wxCommandEvent &);
	void OnDirRemove(wxCommandEvent &);
	void OnRemoveAll(wxCommandEvent &);
	void OnChangeDir(wxCommandEvent &);

	bool GetInformation(SearchPathInfo &);

	void AddDir(const SearchPathInfo  &);

	void OnGetDir(wxCommandEvent &);

	void OnDirNameEnter(wxCommandEvent &event) { OnDirAdd(event); }

	void OnInitDialog(wxInitDialogEvent &event);

	void OnAbout(wxCommandEvent &);

	void ReturnToMe();

	void SetDefaults();

	void OnDefaults(wxCommandEvent &);

	void UpdateControls();

	void PrepareListItem(const SearchPathInfo &, wxListItem c[DUPF_NUMBER_OF_COLS]);

	ScanTargetMode GetSelectedScanMode() const;
	bool GetSelectedProtectSystemDirs() const;

private:
	void CleanUp();

	wxListView *m_wDirList;
	wxTextCtrl *m_wDirName;
	wxCheckBox *m_wRecursive;
	wxCheckBox *m_wHidden;
	wxTextCtrl *m_wMinSize;
	wxTextCtrl *m_wInclude;
	wxTextCtrl *m_wExclude;
	wxTextCtrl *m_wMaxSize;
	wxCheckBox *m_wEmptyFiles;

	// New Mode selectors
	wxRadioButton *m_wRadioDupsOnly;
	wxRadioButton *m_wRadioBoth;
	wxRadioButton *m_wRadioEmptyDirsOnly;
	wxCheckBox    *m_wProtectSystemDirs;

	DuplicateFilesFinder m_dupfinder;

	DECLARE_EVENT_TABLE()
};


#endif

