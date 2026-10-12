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

#include "dialog3.h"
#include "os_cc_specific.h"
#include "os_cc_specific_gui.h"

DupFinderDlg3::DupFinderDlg3(DupFinderDlg *_parent, 
		DuplicateFilesFinder &_dupf) 
	: wxDialog(NULL, -1, _T("Duplicate Files Finder - Results"), wxDefaultPosition, wxDefaultSize, 
		wxDEFAULT_DIALOG_STYLE | wxRESIZE_BORDER | wxMAXIMIZE_BOX | wxMINIMIZE_BOX) , 
		dupfinder(_dupf), duplicates(_dupf.GetDuplicates()), parent(_parent), 
		RestrictToDir(_T("")), bRestrictToDir(false), 
		bRestrictToMask(false), bFirstIdle(true)
{
}

DupFinderDlg3::~DupFinderDlg3() {
	ClearList();
}

bool DupFinderDlg3::bHardLinkWarning = true;

enum {
	TYPE_HEADER, 
	TYPE_ITEM, 
	TYPE_NONE
};

#define MAX_PROGRESS 1000

class ItemData {
public:
	ItemData(int _type ) : type(_type) { data.mygroup = NULL; }
	~ItemData() {}

	void SetGroup(DuplicatesGroup *_mygroup) {
		// assert(type == TYPE_HEADER);
		data.mygroup = _mygroup;
	}

	DuplicatesGroup *GetGroup() {
		// assert(type == TYPE_HEADER);
		return data.mygroup;
	}

	void SetIt(list<File>::iterator &it) {
		assert(type  == TYPE_ITEM);
		data.myself = it;
	}

	list<File>::iterator GetIt() {
		assert(type == TYPE_ITEM);
		return data.myself;
	}

	int GetType() const { return type; }


private:
	int type;
	
	struct _data {
	// union _data {
		DuplicatesGroup *mygroup;
		list<File>::iterator myself;
	} data;

};


enum {
	ID_RESULTLIST = 1, 
	ID_REVERSE, 
	ID_STORE, 
	ID_CONFDELETE, 
	ID_SHOWALL, 
	ID_APPLY, 
	ID_DIRNAME, 
	ID_GETDIR, 
	ID_SUBDIRS, 
	ID_RESTTODIR,
	ID_RESTTOMASK, 
	ID_MASK, 
	ID_PROGRESS, 
	ID_DISPLAYOPTIONS, 
	ID_EMPTYDIRLIST,
	ID_COMMONDIRLIST,
	ID_DEL_EMPTY_DIRS,
	ID_DEL_ALL_EMPTY_DIRS,
	ID_APPLY_FOLDER_SUGGESTION,

	// menu

	// single menu 
	ID_MENU_OPENFILE, 
	ID_MENU_OPENDIR, 
	ID_MENU_COPYFILENAME, 
	ID_MENU_DELETE, 
	ID_MENU_HARDLINK, 
	ID_MENU_SYMLINK, 
	ID_MENU_RESTTODIR, 
	ID_MENU_RESTTOSDIR, 
	ID_MENU_DELETEBUTTHIS
};

BEGIN_EVENT_TABLE(DupFinderDlg3, wxDialog)
	EVT_CLOSE(			DupFinderDlg3::OnClose)
	EVT_SIZE(			DupFinderDlg3::OnSize)
	EVT_BUTTON(ID_STORE, 		DupFinderDlg3::OnStore)
	EVT_INIT_DIALOG(		DupFinderDlg3::OnInitDialog)
	EVT_LIST_ITEM_ACTIVATED(ID_RESULTLIST, DupFinderDlg3::OnListItemActivated)
	EVT_LIST_ITEM_RIGHT_CLICK(ID_RESULTLIST, DupFinderDlg3::OnListItemRightClick)
	EVT_LIST_KEY_DOWN(ID_RESULTLIST, DupFinderDlg3::OnListKeyDown)
	EVT_BUTTON(wxID_CANCEL, 	DupFinderDlg3::OnCancel)
	EVT_BUTTON(ID_APPLY, 		DupFinderDlg3::OnApply)
	EVT_BUTTON(ID_SHOWALL, 		DupFinderDlg3::OnShowAll) 
	EVT_TEXT_ENTER(ID_DIRNAME, 	DupFinderDlg3::OnApply)
	EVT_TEXT_ENTER(ID_MASK, 	DupFinderDlg3::OnApply) 
	EVT_TEXT(ID_DIRNAME, 		DupFinderDlg3::OnDirChange)
	EVT_BUTTON(ID_GETDIR, 		DupFinderDlg3::OnGetDir)
	EVT_TEXT(ID_MASK, 		DupFinderDlg3::OnMaskChange)
	EVT_IDLE(			DupFinderDlg3::OnIdle)
	EVT_TOGGLEBUTTON(ID_DISPLAYOPTIONS, DupFinderDlg3::OnToggleDisplayOptions)

	EVT_BUTTON(ID_DEL_EMPTY_DIRS, 	DupFinderDlg3::OnDeleteSelectedEmptyDirs)
	EVT_BUTTON(ID_DEL_ALL_EMPTY_DIRS, DupFinderDlg3::OnDeleteAllSafeEmptyDirs)
	EVT_BUTTON(ID_APPLY_FOLDER_SUGGESTION, DupFinderDlg3::OnApplyFolderSuggestion)

	// Menu events
	EVT_MENU(ID_MENU_OPENFILE, 	DupFinderDlg3::OnOpenFile)
	EVT_MENU(ID_MENU_OPENDIR, 	DupFinderDlg3::OnOpenDir)
	EVT_MENU(ID_MENU_RESTTODIR,	DupFinderDlg3::OnRestToDir)
	EVT_MENU(ID_MENU_RESTTOSDIR, 	DupFinderDlg3::OnRestToSDir) 
	EVT_MENU(ID_MENU_COPYFILENAME, 	DupFinderDlg3::OnCopyFileName)
	EVT_MENU(ID_MENU_DELETE, 	DupFinderDlg3::OnDelete)
	EVT_MENU(ID_MENU_SYMLINK, 	DupFinderDlg3::OnSymLink)
	EVT_MENU(ID_MENU_HARDLINK, 	DupFinderDlg3::OnHardLink)
	EVT_MENU(ID_MENU_DELETEBUTTHIS, DupFinderDlg3::OnDeleteButThis)
END_EVENT_TABLE()

void DupFinderDlg3::OnInitDialog(wxInitDialogEvent  &event) 
{
	wxDialog::OnInitDialog(event);

	CreateControls();

	UpdateView();

	CenterOnScreen();
}

void DupFinderDlg3::OnIdle(wxIdleEvent &WXUNUSED(event)) {
	if(bFirstIdle) {
		// this MUST be first
		// for avoiding double calls to DisplayResults
		bFirstIdle = false;
		DisplayResults();
		DisplayEmptyDirs();
		DisplayCommonFolders();
	}
}

void DupFinderDlg3::CreateControls() {
	
	wxBoxSizer *topsizer = new wxBoxSizer(wxVERTICAL);
	wxBoxSizer *savesizer = new wxBoxSizer(wxHORIZONTAL);
	wxBoxSizer *restrictsizer = new wxBoxSizer(wxHORIZONTAL);
	wxStaticBoxSizer *restrictcontrolssizer = new wxStaticBoxSizer(wxVERTICAL, this,
		_T("Update list"));
	wxStaticBoxSizer *restrictdetailssizer = new wxStaticBoxSizer(wxVERTICAL, this, 
		_T("Show only files and their duplicates..."));
	wxFlexGridSizer *dirsizer = new wxFlexGridSizer(2, 2, 10, 10);
	wxBoxSizer *dirhorzsizer = new wxBoxSizer(wxHORIZONTAL);
	wxBoxSizer *masksizer = new wxBoxSizer(wxHORIZONTAL);
	wxBoxSizer *controlssizer = new wxBoxSizer(wxHORIZONTAL);
	wxBoxSizer *expandsizer = new wxBoxSizer(wxHORIZONTAL);
	wxStaticBoxSizer *resultssizer = new wxStaticBoxSizer(wxVERTICAL, this, _T("R&esults"));
	wStats = resultssizer;
	m_wOptions = restrictsizer;
	
	const int wxTOPLEFT = wxTOP | wxLEFT;
	const int wxTOPLEFTRIGHT = wxTOP | wxLEFT | wxRIGHT;

	// key shortcuts
	// edaihmscop
	// acdehimosp

	topsizer->Add(
		new wxStaticText(this, wxID_STATIC, _T("Step 3: \nThe results. Select ")
			_T("one or more files and right click on items in the list ")
			_T("for a list of actions. \n") ), 
		0, 
		wxTOPLEFTRIGHT, 
		10);

	resultssizer->Add(
		wResultList = new wxListView(this, ID_RESULTLIST, 
			wxDefaultPosition, wxSize(wxDefaultSize.GetWidth(), 180), 
			wxBORDER_SUNKEN | wxLC_REPORT | wxLC_NO_HEADER), 
		1, 
		wxTOPLEFTRIGHT | wxEXPAND, 
		10);

	savesizer->Add(
		m_wShowOptions = new wxToggleButton(this, ID_DISPLAYOPTIONS, _T("Show o&ptions")), 
		0, 
		wxALIGN_CENTER_VERTICAL, 
		10);

	savesizer->Add(
		new wxStaticText(this, wxID_STATIC, _T("Store the upper\nlist to a file: ")), 
		0, 
		wxLEFT | wxALIGN_CENTER_VERTICAL, 
		10);

	savesizer->Add(
		new wxButton(this, ID_STORE, _T("&Store")), 
		0, 
		wxLEFT | wxALIGN_CENTER_VERTICAL, 
		10);

	savesizer->AddStretchSpacer(1);

	savesizer->Add(
		wConfDelete = new wxCheckBox(this, ID_CONFDELETE, 
			_T("Show &confirmation message when deleting")), 
		0, 
		wxLEFT | wxALIGN_CENTER_VERTICAL, 
		10);

	dirsizer->Add(
		wRestrictToDir = new wxCheckBox(this, ID_RESTTODIR, _T("... in &directory: ")), 
		0, 
		wxALIGN_CENTER_VERTICAL, 
		10);

	dirhorzsizer->Add(
		wDirName = new wxTextCtrl(this, ID_DIRNAME, _T(""), 
			wxDefaultPosition, wxDefaultSize, wxTE_PROCESS_ENTER), 
		1, 
		wxALIGN_CENTER_VERTICAL, 
		10);

	dirhorzsizer->Add(
		new wxButton(this, ID_GETDIR, _T("&...")), 
		0, 
		wxLEFT | wxALIGN_CENTER_VERTICAL, 
		10);

	dirsizer->Add(
		dirhorzsizer, 
		1, 
		wxEXPAND, 
		10);

	dirsizer->AddStretchSpacer();

	dirsizer->Add(
		wSubDirs = new wxCheckBox(this, ID_SUBDIRS, _T(" and &its subdir's")), 
		1, 
		wxALIGN_CENTER_VERTICAL, 
		10);

	dirsizer->AddGrowableCol(1);

	masksizer->Add(
		wRestrictToMask = new wxCheckBox(this, ID_RESTTOMASK, _T("... which &match that mask: ")), 
		0, 
		wxALIGN_CENTER_VERTICAL, 
		10);

	masksizer->Add(
		wMask = new wxTextCtrl(this, ID_MASK, _T("")), 
		1, 
		wxLEFT | wxALIGN_CENTER_VERTICAL, 
		10);

	restrictcontrolssizer->Add(
		new wxButton(this, ID_APPLY, _T("&Apply")), 
		0, 
		wxTOPLEFT, 
		10);

	restrictcontrolssizer->Add(
		new wxButton(this, ID_SHOWALL, _T("S&how all")), 
		0, 
		wxTOPLEFT | wxRIGHT, 
		10);

	restrictdetailssizer->Add(
		dirsizer, 
		1, 
		wxTOPLEFTRIGHT | wxEXPAND, 
		10);

	restrictdetailssizer->Add(
		masksizer, 
		0, 
		wxTOPLEFTRIGHT | wxBOTTOM | wxEXPAND, 
		10);

	restrictsizer->Add(
		restrictdetailssizer, 
		1, 
		wxEXPAND);

	restrictsizer->Add(
		restrictcontrolssizer, 
		0, 
		wxEXPAND | wxLEFT, 
		10);

	controlssizer->AddStretchSpacer(1);

	controlssizer->Add(
		new wxButton(this, wxID_CANCEL, _T("Cl&ose")), 
		0, 
		wxALIGN_RIGHT);

	expandsizer->Add(
		wProgress = new wxGauge(this, ID_PROGRESS, MAX_PROGRESS), 
		1000, 
		wxALIGN_CENTER_VERTICAL, 
		10);

	wProgress->Hide();

	/*
	expandsizer->AddStretchSpacer(1);

	*/

	resultssizer->Add(
		expandsizer, 
		0, 
		wxTOPLEFTRIGHT | wxEXPAND, 
		10);

	resultssizer->Add(
		restrictsizer, 
		0, 
		wxTOPLEFTRIGHT| wxEXPAND, 
		10);

	resultssizer->Add(
		savesizer, 
		0, 
		wxTOPLEFTRIGHT | wxEXPAND | wxBOTTOM, 
		10);

	// Empty directories section
	wxStaticBoxSizer *emptysizer = new wxStaticBoxSizer(wxVERTICAL, this, _T("Pastas Vazias Encontradas"));
	emptysizer->Add(
		wEmptyDirList = new wxListView(this, ID_EMPTYDIRLIST, wxDefaultPosition, 
			wxSize(wxDefaultSize.GetWidth(), 90), wxBORDER_SUNKEN | wxLC_REPORT),
		1, wxEXPAND | wxALL, 5);
	wEmptyDirList->InsertColumn(0, _T("Caminho da Pasta"), wxLIST_FORMAT_LEFT, 550);
	wEmptyDirList->InsertColumn(1, _T("Status de Seguranca"), wxLIST_FORMAT_LEFT, 160);

	wxBoxSizer *emptybtnsizer = new wxBoxSizer(wxHORIZONTAL);
	emptybtnsizer->Add(
		wBtnDelEmptyDirs = new wxButton(this, ID_DEL_EMPTY_DIRS, _T("Apagar Pastas Vazias Selecionadas")),
		0, wxRIGHT, 10);
	emptybtnsizer->Add(
		wBtnDelAllEmptyDirs = new wxButton(this, ID_DEL_ALL_EMPTY_DIRS, _T("Apagar Todas as Pastas Vazias Nao-Sistema")),
		0, 0, 0);
	emptysizer->Add(emptybtnsizer, 0, wxALL, 5);

	resultssizer->Add(emptysizer, 0, wxTOPLEFTRIGHT | wxEXPAND, 10);

	// Common folders analysis section
	wxStaticBoxSizer *commonsizer = new wxStaticBoxSizer(wxVERTICAL, this, _T("Analise de Pastas em Comum e Sugestoes de Exclusao"));
	commonsizer->Add(
		wCommonFoldersList = new wxListView(this, ID_COMMONDIRLIST, wxDefaultPosition, 
			wxSize(wxDefaultSize.GetWidth(), 90), wxBORDER_SUNKEN | wxLC_REPORT | wxLC_SINGLE_SEL),
		1, wxEXPAND | wxALL, 5);
	wCommonFoldersList->InsertColumn(0, _T("Pasta com Duplicatas"), wxLIST_FORMAT_LEFT, 260);
	wCommonFoldersList->InsertColumn(1, _T("Pasta Principal / Original"), wxLIST_FORMAT_LEFT, 260);
	wCommonFoldersList->InsertColumn(2, _T("Duplicatas"), wxLIST_FORMAT_RIGHT, 80);
	wCommonFoldersList->InsertColumn(3, _T("Sugestao do Sistema"), wxLIST_FORMAT_LEFT, 350);

	wxBoxSizer *commonbtnsizer = new wxBoxSizer(wxHORIZONTAL);
	commonbtnsizer->Add(
		wBtnApplySuggestion = new wxButton(this, ID_APPLY_FOLDER_SUGGESTION, _T("Selecionar Duplicatas Desta Pasta para Excluir")),
		0, 0, 0);
	commonsizer->Add(commonbtnsizer, 0, wxALL, 5);

	resultssizer->Add(commonsizer, 0, wxTOPLEFTRIGHT | wxEXPAND | wxBOTTOM, 10);

	topsizer->Add(
		resultssizer, 
		1, 
		wxTOPLEFTRIGHT | wxEXPAND, 
		10);

	topsizer->Add(
		controlssizer, 
		0, 
		wxTOPLEFTRIGHT | wxBOTTOM | wxEXPAND, 
		10);

	wResultList->InsertColumn(0, _T(""), wxLIST_FORMAT_LEFT, 1000);

	wConfDelete->SetValue(true);

	SetSizer(topsizer);
	topsizer->SetSizeHints(this);

	m_wShowOptions->SetValue(false);
}

void DupFinderDlg3::OnClose(wxCloseEvent &WXUNUSED(event)) {

	int result;

	result = ::wxMessageBox(_T("Do you really want to close the results page? "), 
		_T("Confirm close"), wxYES_NO | wxICON_QUESTION, this);

	if(result == wxYES) {
		ReturnToParent();
	}
}

void DupFinderDlg3::OnSize(wxSizeEvent &WXUNUSED(event)) {
	wxSizer *pSizer = GetSizer();
	if(pSizer) {
		pSizer->SetDimension(0, 0, 
			GetClientSize().GetWidth(), 
			GetClientSize().GetHeight());
	}
}

bool DupFinderDlg3::IsMatching(const wxString & string)
{
	bool bMatching = true;

	if(bRestrictToDir && bMatching) {
		wxFileName cur;	

		cur = string;

		// a great speed gain by not using wxPATH_NORM_LONG
		// on windows, but short filesnames are not supported
		cur.Normalize (
			wxPATH_NORM_ALL & ~wxPATH_NORM_LONG);
			cur.Normalize(wxPATH_NORM_CASE);

		bMatching = (wSubDirs->GetValue() ? 
			cur.GetPath().StartsWith(RestrictToDir.GetPath()) :
			cur.GetPath() == RestrictToDir.GetPath());
	}

	if(bRestrictToMask && bMatching) {
		wxFileName name = string;

		bMatching = name.GetFullName().Matches(wMask->GetValue());
	}

	return bMatching;
}

struct less_fileiterator : public less<list<File>::iterator > {
	bool operator () (const list<File>::iterator &a, const list<File>::iterator &b) const {
		return a->GetName() < b->GetName();
	}
};

void DupFinderDlg3::DisplayResults() {
	list<DuplicatesGroup>::iterator it;
	list<File>::iterator it3;
	wxFont font, boldfont;
	bool bHaveFont = false;
	long item;
	ItemData *itemdata;
	list<DuplicatesGroup>::size_type i, size;
	unsigned int percentage = 0;

	if(duplicates.empty()) {
		wxMessageBox(_T("There are no double files! "), _T("Duplicate Files Finder"), 
			 wxOK | wxICON_INFORMATION, this);

		ReturnToParent();
		return;
	}

	// enable progress display
	wProgress->Show();
	GetSizer()->Layout();
	this->Enable(false);

	// disable all repaint until the end of the function
	wResultList->Freeze();
	
	ClearList();

	size = duplicates.size();

	for(it = duplicates.begin(), i = 0; it != duplicates.end(); it++, i++) {
		bool bDisplay;
		multiset<list<File>::iterator, less_fileiterator> matching;

		bool bRestrict = bRestrictToMask || bRestrictToDir;

		// don't include items which have only one element left ?
		if(it->files.size() == 1 && false) {
			bDisplay = false; 
		} else {
			if(bRestrict) {
				// test if it should be displayed
				bDisplay = false;


				for(it3 = it->files.begin(); it3 != it->files.end(); it3++) {
					
					// is in in the directory?
					if(IsMatching(it3->GetName()) ) {
						bDisplay = true;
						matching.insert(it3);
						// break;
					}
				}
			}
			else {
				bDisplay = true;
			}
		}

		if(bDisplay) {
			wxString tmp;
			tmp.Printf(_T("%u equal files of size %") wxLongLongFmtSpec _T("u"), 
				it->files.size(), it->size.GetValue());

			item = wResultList->InsertItem(wResultList->GetItemCount()+1, tmp);

			if(!bHaveFont) {
				boldfont = font = wResultList->GetItemFont(item);
				boldfont.SetWeight(wxFONTWEIGHT_BOLD);
				boldfont.SetUnderlined(true);
				bHaveFont = true;
			}
			
			wResultList->SetItemFont(item, boldfont);

			itemdata = new ItemData(TYPE_HEADER);
			itemdata->SetGroup(&*it);

			SetListItemData(wResultList, item, itemdata);

			for(it3 = it->files.begin(); it3 != it->files.end(); it3++) {
				item = wResultList->InsertItem(wResultList->GetItemCount()+1, it3->GetName());
				itemdata = new ItemData(TYPE_ITEM);
				itemdata->SetGroup(&*it);
				itemdata->SetIt(it3);
					
				// matching ?
				if(matching.find(it3) != matching.end()) {
					wResultList->SetItemBackgroundColour(item, wxColor(250, 120, 120));
				}

				SetListItemData(wResultList, item, itemdata);
			}
		}

		if((i*MAX_PROGRESS)/size != percentage) {
			
			percentage = (i*MAX_PROGRESS)/size;
			wProgress->SetValue(percentage);
			wxTheApp->Yield();
		}
	}

	// no items in list?
	if(wResultList->GetItemCount() == 0) {
		itemdata = new ItemData(TYPE_NONE);
		item = wResultList->InsertItem(0, _T("No items in this view. "));
		SetListItemData(wResultList, item, itemdata);
	}

	DeleteOrphanedHeaders();

	RefreshStats();

	// enable all again
	wProgress->Hide();
	GetSizer()->Layout();
	this->Enable(true);

	// enable repaint again
	wResultList->Thaw();
	wResultList->Refresh();

}

void DupFinderDlg3::OnStore(wxCommandEvent &WXUNUSED(event))
{
	wxFileDialog * fdlg = new wxFileDialog(this, _T("Save as..."), _T(""), 
		_T("results.txt"), _T("Textfiles (*.txt)|*.txt|All files (*.*)|*.*"), 
		wxFD_SAVE | wxFD_OVERWRITE_PROMPT);

	if(fdlg->ShowModal() == wxID_OK) {
		wxFile outfile;
		if(outfile.Create(fdlg->GetPath(), true)) {
			long count, i;
			count = wResultList->GetItemCount();

			for(i = 0; i < count; i++) {
				ItemData *data = (ItemData *)wResultList->GetItemData(i);
				wxString tmp;
				if(data->GetType() == TYPE_ITEM) {
					tmp.Printf(_T("  \"%s\"\r\n"), wResultList->GetItemText(i).c_str());
				}
				else {
					// header item
					tmp.Printf(_T("- %s\r\n"), wResultList->GetItemText(i).c_str());
				}
				outfile.Write(tmp);
			}
			outfile.Close();
		}
	}

	delete fdlg;
}


void DupFinderDlg3::OnListItemActivated(wxListEvent &event) 
{
	ItemData *data = (ItemData *)event.GetData();
	if(data->GetType() == TYPE_ITEM) {
		OpenDir(event.GetIndex());
	}
}

void DupFinderDlg3::OpenDir(long i) {
	if(i >= 0 && i < wResultList->GetItemCount()) {
		ItemData *data = (ItemData *)wResultList->GetItemData(i);
		if(data->GetType() == TYPE_ITEM) {
			wxFileName filename = data->GetIt()->GetName();

			wxString path = wxString(_T("file:///")) + filename.GetPathWithSep();

			::wxLaunchDefaultBrowser(path);
		}
	}
}

void DupFinderDlg3::OpenFile(long i) {
	if(i >= 0 && i < wResultList->GetItemCount()) {
		ItemData *data = (ItemData *)wResultList->GetItemData(i);
		if(data->GetType() == TYPE_ITEM) {
			::wxLaunchDefaultBrowser(data->GetIt()->GetName());
		}
	}
}

void DupFinderDlg3::OnListItemRightClick(wxListEvent &event)
{
	rightClickedItem = event.GetIndex();

	if(!IsValidItem(rightClickedItem)) {
		return;
	}

	wxMenu * popupmenu = new wxMenu();

	if(((ItemData *)event.GetData())->GetType() == TYPE_ITEM) {
		bool bAddSep;	
		
		popupmenu->Append(ID_MENU_OPENFILE, _T("&Open"));
		// the following commented works, 
		// but doesn't look very nice
		/* wxMenuItem *default = new wxMenuItem(popupmenu, ID_MENU_OPENDIR, 
			_T("O&pen containing folder")); 
		wxFont font = popupmenu->FindItem(ID_MENU_OPENFILE)->GetFont();
		font.SetWeight(wxFONTWEIGHT_BOLD);
		default->SetFont(font); 
		popupmenu->Append(default); */
		popupmenu->Append(ID_MENU_OPENDIR, _T("O&pen containing folder"));
		popupmenu->AppendSeparator();
		popupmenu->Append(ID_MENU_RESTTODIR, _T("Sho&w only files in this folder"));
		popupmenu->Append(ID_MENU_RESTTOSDIR, _T("Show onl&y files in this folder and subfolders"));
		popupmenu->AppendSeparator();
		bAddSep = false;
		if(IsSymLinkSupported()) {
			popupmenu->Append(ID_MENU_SYMLINK, _T("C&reate symbolic links to this file"));
			bAddSep = true;
		}
		if(IsHardLinkSupported()) {
			popupmenu->Append(ID_MENU_HARDLINK, _T("Create &hard links to this file"));
			bAddSep = true;
		}
		popupmenu->Append(ID_MENU_DELETEBUTTHIS, _T("Delete all duplicates to this file"));
		if(bAddSep) {
			popupmenu->AppendSeparator();
		}
	}
	
	popupmenu->Append(ID_MENU_COPYFILENAME, _T("&Copy filename(s) to clipboard"));
	popupmenu->AppendSeparator();
	popupmenu->Append(ID_MENU_DELETE, _T("&Delete"));
		
	wResultList->PopupMenu(popupmenu);

	delete popupmenu; 
}


void DupFinderDlg3::OnOpenFile(wxCommandEvent &WXUNUSED(event)) 
{
	OpenFile(rightClickedItem);
}

void DupFinderDlg3::OnOpenDir(wxCommandEvent &WXUNUSED(event))
{
	OpenDir(rightClickedItem);
}

void DupFinderDlg3::GetSelectedFilenameCount(long &count)
{
	long i;

	count = 0;
	for(i = wResultList->GetFirstSelected(); i != -1; i = wResultList->GetNextSelected(i)) {
		if(((ItemData *)wResultList->GetItemData(i))->GetType() == TYPE_ITEM) {
			count++;
		}
	}

}

long DupFinderDlg3::GetFirstSelectedFilename()
{
	long i;
	for(i = wResultList->GetFirstSelected(); i != -1; i = wResultList->GetNextSelected(i)) {
		if(((ItemData *)wResultList->GetItemData(i))->GetType() == TYPE_ITEM) {
			break;
		}
	}
	return i;
}

long DupFinderDlg3::GetNextSelectedFilename(long i)
{
	for(i = wResultList->GetNextSelected(i); i != -1; i = wResultList->GetNextSelected(i)) {
		if(((ItemData *)wResultList->GetItemData(i))->GetType() == TYPE_ITEM) {
			break;
		}
	}
	return i;
}

void DupFinderDlg3::OnCopyFileName(wxCommandEvent &WXUNUSED(event))
{
	wxString filename, tmp;
	long i, j, count;
	
	GetSelectedFilenameCount(count);
	
	if(count == 0) return;

	if(count > 1) {
		for(i = GetFirstSelectedFilename(), j = 0; i != -1; i = GetNextSelectedFilename(i), j++) {
			tmp.Printf(_T("\"%s\"%s"), 
				((ItemData *)wResultList->GetItemData(i))
					->GetIt()->GetName().c_str(), 
				j == count-1 ? _T("") : _T(" ") );
			filename.Append(tmp);
		}
	}
	else {
		filename = ((ItemData *)wResultList->GetItemData(GetFirstSelectedFilename()) )
			->GetIt()->GetName();
	}

	wxTextDataObject * wxfile = new wxTextDataObject(filename);

	if(wxTheClipboard->Open()) {
	
		wxTheClipboard->SetData(wxfile);
		wxTheClipboard->Close();
	}
}

void DupFinderDlg3::OnDelete(wxCommandEvent &WXUNUSED(event))
{
	DeleteSelection();
}

void DupFinderDlg3::DeleteSelection() {
	long i, count;
	list<long> delete_this;

	GetSelectedFilenameCount(count);

	i = GetFirstSelectedFilename();

	while(i != -1) {
		ItemData *data = (ItemData *)wResultList->GetItemData(i);

		if(data->GetType() == TYPE_ITEM) {
			delete_this.push_back(i);
		}
		
		i = GetNextSelectedFilename(i);
	}

	DeleteFiles(delete_this);
}


void DupFinderDlg3::DeleteFiles(const list<long> & delete_this)
{
	list<long>::size_type count;
	wxString tmp;
	wxString filename;
	int result;
	list<long>::const_iterator it;
	list<long> success_delete;

	count = delete_this.size();

	if(count == 0) { return; }

	if(wConfDelete->GetValue()) {

		if(count == 1) {
			tmp.Printf(_T("Do you really want to delete \n\"%s?\" "), 
				((ItemData *)wResultList->GetItemData(delete_this.front()))
					->GetIt()->GetName().c_str() );
		}
		else {
			tmp.Printf(_T("Do you really want to delete these %i files? "), count);
		}

		result = wxMessageBox(tmp, _T("Confirmation"), wxYES_NO | wxICON_QUESTION);
	}
	else { 
		result = wxYES;
	}

	if(result == wxYES) {

		for(it = delete_this.begin(); 
			it != delete_this.end();
			it++) {
			ItemData *data = (ItemData *)wResultList->GetItemData(*it);

			if(data->GetType() == TYPE_ITEM) {
				filename = data->GetIt()->GetName();
	
				bool bResult = wxRemoveFile(filename);

				if(bResult) {
					success_delete.push_back(*it);
					data->GetGroup()->files.erase(data->GetIt());
				}
				else {
					tmp.Printf(_T("Error: cannot delete \"%s\"! "), filename.c_str());
					wxMessageBox(tmp, _T("Error"), wxICON_ERROR);
				}
			}
		}
	}

	list<long>::reverse_iterator rit;

	// from the bottom to the top!
	for(rit = success_delete.rbegin(); rit != success_delete.rend(); rit++) {
		wResultList->DeleteItem(*rit);
	}
	
	DeleteOrphanedHeaders();

	RefreshStats();
}

void DupFinderDlg3::ReturnToParent() {

	Hide();
	parent->ReturnToMe();
	Destroy();
}

void DupFinderDlg3::OnListKeyDown(wxListEvent &event)
{
	long focus = wResultList->GetFocusedItem();

	switch(event.GetKeyCode()) {
	case WXK_RETURN:
		OpenDir(focus);
		break;
	case WXK_DELETE:
		DeleteSelection();
		break;
	}
}

void DupFinderDlg3::DeleteOrphanedHeaders()
{
	long i, count;
	list<long> delete_this;

	count = wResultList->GetItemCount();
	
	for(i = 0; i < count; i++) {
		ItemData *data = (ItemData *)wResultList->GetItemData(i);
		if(data->GetType() == TYPE_HEADER /*&& data->bExpanded*/) {
			if(i == count-1) {
				delete_this.push_back(i);
			}
			else if(((ItemData *)wResultList->GetItemData(i+1))->GetType() == TYPE_HEADER ) {
				delete_this.push_back(i);
			}
		}
	}

	list<long>::reverse_iterator it;

	// delete from the end to the top, 
	// so that the item indexes remain valid!!!!
	for(it = delete_this.rbegin(); it != delete_this.rend(); it++) {
		wResultList->DeleteItem(*it);
	}
}


void DupFinderDlg3::OnCancel(wxCommandEvent &WXUNUSED(event))
{
	Close();
}

void DupFinderDlg3::OnApply(wxCommandEvent &WXUNUSED(event))
{
	bRestrictToDir = wRestrictToDir->GetValue();
	bRestrictToMask = wRestrictToMask->GetValue();

	if(bRestrictToDir) {
		wxString dirname = wDirName->GetValue();

		if(!wxFileName::DirExists(dirname)) {
			wxMessageBox(_T("Error: Directory does not exist. ")
			_T("\nPlease enter a valid directory. "), _T("Error"), 
			wxOK | wxICON_ERROR);
			return;
		}

		RestrictViewToDir(dirname);
	} 
	
	DisplayResults();
}

void DupFinderDlg3::RestrictViewToDir(const wxString &dirname)
{
	bRestrictToDir = true;

	RestrictToDir = wxFileName::DirName(dirname);
		
	RestrictToDir.Normalize(wxPATH_NORM_ALL & ~wxPATH_NORM_LONG);
	RestrictToDir.Normalize(wxPATH_NORM_CASE);
}

void DupFinderDlg3::OnShowAll(wxCommandEvent &WXUNUSED(event))
{
	bRestrictToDir = false;
	bRestrictToMask = false;
	DisplayResults();
}

void DupFinderDlg3::ClearList() {
	long count = wResultList->GetItemCount();
	long i;

	for(i = 0; i < count; i++) {
		delete (ItemData *)wResultList->GetItemData(i);
	}

	wResultList->DeleteAllItems();
}

void DupFinderDlg3::MenuRestToDir(bool bSubDirs)
{
	if(!IsValidItem(rightClickedItem) ) {
		return;
	}

	ItemData *data = (ItemData *)wResultList->GetItemData(rightClickedItem);
	if(data->GetType() == TYPE_ITEM) {
		wxFileName filename = data->GetIt()->GetName();

		wxString path = filename.GetPath();

		wSubDirs->SetValue(bSubDirs);
		wRestrictToDir->SetValue(true);
		wDirName->SetValue(path);

		RestrictViewToDir(path);

		DisplayResults();
	}
}

void DupFinderDlg3::OnRestToDir(wxCommandEvent & WXUNUSED(event)) 
{
	MenuRestToDir(false);
}

void DupFinderDlg3::OnRestToSDir(wxCommandEvent &WXUNUSED(event)) 
{
	MenuRestToDir(true);
}

void DupFinderDlg3::OnGetDir(wxCommandEvent &WXUNUSED(event)) {
	wxDirDialog dirch(this);

	dirch.SetWindowStyle(wxDD_DIR_MUST_EXIST);

	if(bRestrictToDir) {
		dirch.SetPath(RestrictToDir.GetPath());
	} else {
		dirch.SetPath(wDirName->GetValue());
	}

	int ret = dirch.ShowModal();

	if(ret == wxID_OK) {
		wDirName->SetValue(dirch.GetPath());
	}
}

void DupFinderDlg3::OnSymLink(wxCommandEvent & WXUNUSED(event)) {
	CreateLink(CreateSymLink, _T("symbolic"));
}

void DupFinderDlg3::OnHardLink(wxCommandEvent & WXUNUSED(event)) {

	int result;

	if(bHardLinkWarning) {
		result = wxMessageBox(_T("Hardlinks should be used with caution, because they ")
			_T("indectectably connect files, and if you edit one, all others will ")
			_T("be changed as well. As hardlinks can not easily be detected, the ")
			_T("hardlinked files will reappear when you repeat the search. \n")
			_T("Do you want to continue anyway? "), 
			_T("Warning"), wxYES_NO | wxICON_WARNING, this);

		if(result == wxNO) {
			return;
		}

		if(result == wxYES) {
			bHardLinkWarning = false;
		}
	}

	CreateLink(CreateHardLink, _T("hard"));
}

void DupFinderDlg3::CreateLink(bool (*link_func)(const wxString &, const wxString &), const wxString &type)
{
	if(!IsValidItem(rightClickedItem)) {
		return;
	}

	ItemData *target_data = (ItemData *)wResultList->GetItemData(rightClickedItem);
	if(target_data->GetType() != TYPE_ITEM) {
		return ;
	}

	bool bStickyError, bError, bFatalError = false;
	long i;
	list<long> remove_me;

	i = GetHeader(rightClickedItem)+1;

	bStickyError = false;
	for((void)i; i < wResultList->GetItemCount(); i++) { 
		if(((ItemData *)wResultList->GetItemData(i))->GetType() != TYPE_ITEM) {
			break;
		}
		bError = false;
		if(i != rightClickedItem) {
			ItemData *data = (ItemData *)wResultList->GetItemData(i);
			wxFileName file = data->GetIt()->GetName();

			wxString tmpfile = wxFileName::CreateTempFileName(file.GetPathWithSep());

			if(tmpfile.Length() != 0) {
				bool bResult = wxRenameFile(file.GetFullPath(), tmpfile);
				
				if(!bResult) {
					bResult = wxRemoveFile(tmpfile);
					if(!bResult) {
						wxString tmp;
						tmp.Printf(_T("Cannot delete %s! "), tmpfile.c_str());
						wxMessageBox(tmp, _T("Error"), wxOK | wxICON_ERROR, this);
					}
					bError = true;
				} else {
					bResult = link_func(target_data->GetIt()->GetName(), file.GetFullPath());

					if(!bResult) {
						bError = true;

						// restore old state
						bFatalError = wxRenameFile(tmpfile, file.GetFullPath()) == false;
					}
					else {
						bool bResult = wxRemoveFile(tmpfile);
						if(!bResult) {
							wxString tmp;
							tmp.Printf(_T("Cannot delete %s! "), tmpfile.c_str());
							wxMessageBox(tmp, _T("Error"), wxOK | wxICON_ERROR, this);
						}
					}
				}
			}
			else {
				bError = true;
			}
			if(!bError) {
				data->GetGroup()->files.erase(data->GetIt());
				remove_me.push_back(i);
			}
			else {
				bStickyError = true;
			}
		}
	}

	// from the bottom to the top! 
	for(list<long>::reverse_iterator rit = remove_me.rbegin(); 
		rit != remove_me.rend(); 
		rit++) {
		wResultList->DeleteItem(*rit);
	}

	if(bStickyError) {
		wxString tmp;
		tmp.Printf(_T("Not all %s links could be created! "), type.c_str());
		wxMessageBox(tmp, _T("Error"), 
			wxOK | wxICON_ERROR, this);
	}
	if(bFatalError) {
		// this should never happen
		wxMessageBox(_T("Some actions could not be finished. Some files are in ")
			_T("undefined state. I recommend repeating the whole search. "), 
			_T("Sorry, an error which actually (almost) cannot happen"), wxOK | wxICON_ERROR, this);
	}

	RefreshStats();
}

void DupFinderDlg3::OnMaskChange(wxCommandEvent &WXUNUSED(event)) 
{
	wRestrictToMask->SetValue(wMask->GetValue().Length() != 0);
}

void DupFinderDlg3::OnDirChange(wxCommandEvent &WXUNUSED(event))
{
	wRestrictToDir->SetValue(wDirName->GetValue().Length() != 0);
}

void DupFinderDlg3::OnDeleteButThis(wxCommandEvent &WXUNUSED(event))
{
	ItemData *target_data = (ItemData *)wResultList->GetItemData(rightClickedItem);
	long i, header;
	long count;
	list<long> delete_this;

	if(!IsValidItem(rightClickedItem)) {
		return;
	}

	if(target_data->GetType() != TYPE_ITEM) {
		return;
	}

	header = GetHeader(rightClickedItem);

	count = wResultList->GetItemCount();

	for(i = header+1; i < count; i++) {
		if(((ItemData *)wResultList->GetItemData(i))->GetType() == TYPE_HEADER) {
			break;
		}

		if(i != rightClickedItem) {
			delete_this.push_back(i);
		}
	}

	DeleteFiles(delete_this);
}

long DupFinderDlg3::GetHeader(long item) 
{
	long i;

	assert(item >= 0);

	for(i = item; 
		i >= 0 && ((ItemData *)wResultList->GetItemData(i))->GetType() != TYPE_HEADER; 
		i--) { }

	return i;
}

bool DupFinderDlg3::IsValidItem(long item)
{
	return (item >= 0 && item < wResultList->GetItemCount());
}

void DupFinderDlg3::RefreshStats()
{
	// display stats
	wxString tmp;
	DuplicateFilesStats stats;
	wxString Plural_duplicates;
	wxString Plural_files;
	wxString Verb_consume;

	dupfinder.CalculateStats(stats);

	if(stats.nDuplicateFiles == 1) {
		Plural_duplicates = _T("duplicate");
		Verb_consume = _T("consumes");
	}
	else {
		Plural_duplicates = _T("duplicates");
		Verb_consume = _T("consume");
	}

	if(stats.nFilesWithDuplicates == 1) {
		Plural_files = _T("file");
	}
	else {
		Plural_files = _T("files");
	}

	tmp.Printf(_T("R&esults (%") wxLongLongFmtSpec _T("u %s of %") 
		wxLongLongFmtSpec _T("u %s %s %.2f mb)"), 
		stats.nDuplicateFiles.GetValue(), 
		Plural_duplicates.c_str(), 
		stats.nFilesWithDuplicates.GetValue(), 
		Plural_files.c_str(), 
		Verb_consume.c_str(), 
		((double)stats.nWastedSpace.GetValue())/1024/1024);

	wStats->GetStaticBox()->SetLabel(tmp);
}

void DupFinderDlg3::OnToggleDisplayOptions(wxCommandEvent &WXUNUSED(event))
{
	UpdateView();
}

void DupFinderDlg3::UpdateView()
{
	m_wOptions->Show(m_wShowOptions->GetValue());
	GetSizer()->Layout();
}

void DupFinderDlg3::DisplayEmptyDirs()
{
	if(!wEmptyDirList) return;
	wEmptyDirList->DeleteAllItems();

	const list<EmptyDirInfo> &emptyDirs = dupfinder.GetEmptyDirs();
	int index = 0;
	for(list<EmptyDirInfo>::const_iterator it = emptyDirs.begin(); it != emptyDirs.end(); ++it, ++index) {
		long item = wEmptyDirList->InsertItem(index, it->path);
		wEmptyDirList->SetItem(item, 1, it->isSystemProtected ? _T("Protegida pelo Sistema") : _T("Livre para Exclusao"));
		if(it->isSystemProtected) {
			wEmptyDirList->SetItemBackgroundColour(item, wxColor(255, 230, 230));
		}
	}

	if(emptyDirs.empty()) {
		wEmptyDirList->InsertItem(0, _T("Nenhuma pasta vazia encontrada."));
	}
}

void DupFinderDlg3::DisplayCommonFolders()
{
	if(!wCommonFoldersList) return;
	wCommonFoldersList->DeleteAllItems();

	commonFolders = dupfinder.AnalyzeCommonFolders();
	int index = 0;
	for(list<CommonFolderSuggestion>::const_iterator it = commonFolders.begin(); it != commonFolders.end(); ++it, ++index) {
		long item = wCommonFoldersList->InsertItem(index, it->folderPath);
		wCommonFoldersList->SetItem(item, 1, it->primaryFolderPath);
		wCommonFoldersList->SetItem(item, 2, wxString::Format(_T("%d"), it->duplicateFilesCount));
		wCommonFoldersList->SetItem(item, 3, it->recommendation);
		if(it->isCompleteDuplicateFolder) {
			wCommonFoldersList->SetItemBackgroundColour(item, wxColor(230, 255, 230));
		}
	}

	if(commonFolders.empty()) {
		wCommonFoldersList->InsertItem(0, _T("Nenhuma redundancia em lote entre pastas detectada."));
	}
}

void DupFinderDlg3::OnDeleteSelectedEmptyDirs(wxCommandEvent &WXUNUSED(event))
{
	long item = wEmptyDirList->GetFirstSelected();
	if(item == -1) {
		wxMessageBox(_T("Selecione ao menos uma pasta vazia para excluir."), _T("Aviso"), wxOK | wxICON_INFORMATION, this);
		return;
	}

	int confirm = wxMessageBox(_T("Deseja realmente excluir as pastas vazias selecionadas?"), 
		_T("Confirmar Exclusao"), wxYES_NO | wxICON_QUESTION, this);
	if(confirm != wxYES) return;

	list<long> toDelete;
	while(item != -1) {
		toDelete.push_back(item);
		item = wEmptyDirList->GetNextSelected(item);
	}

	for(list<long>::reverse_iterator rit = toDelete.rbegin(); rit != toDelete.rend(); ++rit) {
		wxString path = wEmptyDirList->GetItemText(*rit);
		if(dupfinder.DeleteEmptyDir(path)) {
			wEmptyDirList->DeleteItem(*rit);
		} else {
			wxMessageBox(_T("Nao foi possivel apagar a pasta: ") + path, _T("Erro"), wxOK | wxICON_ERROR, this);
		}
	}
}

void DupFinderDlg3::OnDeleteAllSafeEmptyDirs(wxCommandEvent &WXUNUSED(event))
{
	const list<EmptyDirInfo> &emptyDirs = dupfinder.GetEmptyDirs();
	if(emptyDirs.empty()) {
		wxMessageBox(_T("Nenhuma pasta vazia na lista."), _T("Aviso"), wxOK, this);
		return;
	}

	int confirm = wxMessageBox(_T("Deseja excluir TODAS as pastas vazias que nao pertencem ao sistema?"), 
		_T("Confirmar Limpeza de Pastas Vazias"), wxYES_NO | wxICON_WARNING, this);
	if(confirm != wxYES) return;

	int deleted = 0;
	for(list<EmptyDirInfo>::const_iterator it = emptyDirs.begin(); it != emptyDirs.end(); ++it) {
		if(!it->isSystemProtected) {
			if(dupfinder.DeleteEmptyDir(it->path)) {
				deleted++;
			}
		}
	}

	wxMessageBox(wxString::Format(_T("%d pastas vazias foram apagadas com sucesso!"), deleted), _T("Concluido"), wxOK, this);
	dupfinder.FindEmptyDirectories();
	DisplayEmptyDirs();
}

void DupFinderDlg3::OnApplyFolderSuggestion(wxCommandEvent &WXUNUSED(event))
{
	long selected = wCommonFoldersList->GetFirstSelected();
	if(selected == -1) {
		wxMessageBox(_T("Selecione uma linha de pasta sugerida primeiro."), _T("Aviso"), wxOK, this);
		return;
	}

	wxString folderToClean = wCommonFoldersList->GetItemText(selected);
	if(folderToClean.IsEmpty()) return;

	long count = wResultList->GetItemCount();
	int marked = 0;
	for(long i = 0; i < count; i++) {
		ItemData *data = (ItemData *)wResultList->GetItemData(i);
		if(data && data->GetType() == TYPE_ITEM) {
			wxString filePath = data->GetIt()->GetName();
			if(filePath.StartsWith(folderToClean)) {
				wResultList->Select(i, true);
				marked++;
			} else {
				wResultList->Select(i, false);
			}
		} else {
			wResultList->Select(i, false);
		}
	}

	wxMessageBox(wxString::Format(_T("%d duplicatas da pasta '%s' foram selecionadas no painel principal para exclusao."), marked, folderToClean.c_str()), 
		_T("Sugestao Aplicada"), wxOK, this);
}











