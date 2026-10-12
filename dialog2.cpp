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

#include "dialog2.h"
#include "dialog3.h"
#include "dbl.h"

enum {
	ID_SEARCHDIRNAME = 1, 
	ID_NFILES, 
	ID_SHOWMESSAGES, 
	// ID_CFILES, 
	ID_PROGRESS, 
	ID_SPEED, 
	ID_STEP1, 
	ID_STEP2, 
	ID_PAUSE, 
	ID_PROGRESS_GAUGE
};

BEGIN_EVENT_TABLE(DupFinderDlg2, wxDialog)
	EVT_CLOSE(			DupFinderDlg2::OnClose)
	EVT_SIZE( 			DupFinderDlg2::OnSize)
	EVT_INIT_DIALOG(		DupFinderDlg2::OnInitDialog)
	EVT_IDLE(			DupFinderDlg2::OnIdle)
	EVT_BUTTON(wxID_CANCEL, 	DupFinderDlg2::OnCancel)
	EVT_CHECKBOX(ID_SHOWMESSAGES, 	DupFinderDlg2::OnShowMessages)
	EVT_BUTTON(ID_PAUSE, 		DupFinderDlg2::OnPause)
END_EVENT_TABLE()

DupFinderDlg2::DupFinderDlg2(DuplicateFilesFinder &dupf, DupFinderDlg *parent) : 
	wxDialog(NULL, -1, _T("Duplicate Files Finder - Searching"), wxDefaultPosition, wxDefaultSize,  
	/* no parent because of icon !!! */
	(wxDEFAULT_DIALOG_STYLE | wxRESIZE_BORDER | wxMINIMIZE_BOX | wxMAXIMIZE_BOX) & ~wxCLOSE_BOX ), 
	m_dupfinder(dupf), m_started(false), m_parent(parent)
{
	
}

DupFinderDlg2::~DupFinderDlg2() 
{
}

void DupFinderDlg2::OnClose(wxCloseEvent &WXUNUSED(event)) 
{
	// assert(false);
	// EndModal(0);
}

void DupFinderDlg2::CreateControls()
{
	wxBoxSizer *topsizer = new wxBoxSizer(wxVERTICAL);

	wxStaticBoxSizer *progresssizer = new wxStaticBoxSizer(wxVERTICAL, this, _T("Progress"));
	wxBoxSizer *nfilessizer = new wxBoxSizer(wxHORIZONTAL);
	// wxBoxSizer *cfilessizer = new wxBoxSizer(wxHORIZONTAL);
	wxBoxSizer *progress2sizer = new wxBoxSizer(wxHORIZONTAL);
	wxBoxSizer *progress2sizer_2 = new wxBoxSizer(wxHORIZONTAL);
	wxBoxSizer *controls = new wxBoxSizer(wxHORIZONTAL);

	const int wxTOPLEFT = wxLEFT | wxTOP;
	const int wxTOPLEFTRIGHT = wxLEFT | wxTOP | wxRIGHT;
	// const int wxTOPRIGHT = wxRIGHT | wxTOP;

	topsizer->Add(
		new wxStaticText(this, wxID_STATIC, _T("Step 2: \nSearching for duplicates")), 
		0, 
		wxTOPLEFTRIGHT | wxEXPAND, 
		10);

	progresssizer->Add(
		m_wStep1 = new wxStaticText(this, ID_STEP1, _T("1) Searching files: In directory ")), 
		0, 
		wxTOPLEFTRIGHT | wxEXPAND, 
		10);

	progresssizer->Add(
		m_wDirName = new wxTextCtrl(this, ID_SEARCHDIRNAME, _T("----"), 
			wxDefaultPosition, wxSize(300, wxDefaultSize.GetHeight()), wxTE_READONLY), 
		0, 
		wxTOPLEFTRIGHT | wxEXPAND, 
		10);

	nfilessizer->Add(
		new wxStaticText(this, wxID_STATIC, _T("Files: ")), 
		0, 
		wxTOP,
		10);

	nfilessizer->Add(
		m_wnFiles = new wxStaticText(this, ID_NFILES, _T("")), 
		1, 
		wxTOPLEFT, 
		10);

	/* cfilessizer->Add(
		new wxStaticText(this, wxID_STATIC, _T("Considered files: ")), 
		0, 
		wxTOP,
		10);

	cfilessizer->Add(
		m_wcFiles = new wxStaticText(this, ID_CFILES, _T("")), 
		1, 
		wxTOPLEFT, 
		10);
	*/

	progress2sizer->Add(
		new wxStaticText(this, wxID_STATIC, _T("Progress: ")), 
		0, 
		wxTOPLEFT, 
		10);

	// control has content "\n\n" because status output contains 
	// three lines as well (for sizer)
	progress2sizer->Add(
		m_wProgress = new wxStaticText(this, ID_PROGRESS, _T("\n\n")), 
		1, 
		wxTOPLEFT, 
		10);

	progress2sizer_2->Add(
		new wxStaticText(this, wxID_STATIC, _T("Speed: ")), 
		0, 
		wxTOPLEFT, 
		10);

	progress2sizer_2->Add(
		m_wSpeed = new wxStaticText(this, ID_SPEED, _T("")), 
		1,
		wxTOPLEFTRIGHT, 
		10);
		


	progresssizer->Add(
		nfilessizer, 
		0, 
		wxLEFT | wxRIGHT, 
		10);

	/* progresssizer->Add(
		cfilessizer, 
		0, 
		wxLEFT | wxRIGHT, 
		10); */

	progresssizer->Add(
		m_wStep2 = new wxStaticText(this, ID_STEP2, _T("2) Comparing files: ")), 
		0, 
		wxTOPLEFTRIGHT | wxEXPAND, 
		10);

	progresssizer->Add(
		progress2sizer, 
		0, 
		wxEXPAND, 
		10);

	progresssizer->Add(
		progress2sizer_2, 
		0, 
		wxEXPAND, 
		10);

	progresssizer->Add(
		m_wProgressGauge = new wxGauge(this, ID_PROGRESS_GAUGE, 1000), 
		0, 
		wxEXPAND | wxTOPLEFTRIGHT | wxBOTTOM, 
		10);

	// show it when the progress display works fine
	// progresssizer->Hide(m_wProgressGauge);

	topsizer->Add(
		progresssizer, 
		1, 
		wxTOPLEFTRIGHT | wxEXPAND, 
		10);

	controls->Add(
		new wxCheckBox(this, ID_SHOWMESSAGES, _T("&Show messages")), 
		1, 
		wxTOPLEFT | wxALIGN_CENTER_VERTICAL, 
		10); 

	controls->Add(
		m_wPause = new wxButton(this, ID_PAUSE, _T("&Pause")), 
		0, 
		wxTOPLEFT | wxALIGN_RIGHT, 
		10);

	controls->Add(
		new wxButton(this, wxID_CANCEL, _T("&Cancel")), 
		0, 
		wxTOPLEFTRIGHT | wxALIGN_RIGHT, 
		10);

	topsizer->Add(
		controls, 
		0, 
		wxBOTTOM | wxEXPAND, 
		10);

	// disable <enter> ?
	SetAffirmativeId(wxID_ANY);

	SetSizer(topsizer);
	topsizer->SetSizeHints(this);

	dynamic_cast<wxCheckBox *>(FindWindow(ID_SHOWMESSAGES))->SetValue(true);
}

void DupFinderDlg2::OnSize(wxSizeEvent &WXUNUSED(event)) 
{
	wxSizer *pSizer = GetSizer();
	if(pSizer) {
		pSizer->SetDimension(0, 0, 
			GetClientSize().GetWidth(), GetClientSize().GetHeight());
		pSizer->Layout();
	}
}

void DupFinderDlg2::OnInitDialog(wxInitDialogEvent &event) {
	wxDialog::OnInitDialog(event);	

	CreateControls();
	CenterOnScreen();
}


void DupFinderDlg2::OnIdle(wxIdleEvent &WXUNUSED(event)) {
	// start search if the first idle
	// event is caught
	// a bit dangerous, doing it that way?
	// because what if not becoming idle?

	// that is so to say the main function 
	// of the dialog
	// all in the following if
	// is executed once (!), the results are displayed, 
	// if there are any, 
	// and then the dialog box is closed again

	if(!m_started) {
		// do not change this and LEAVE THIS AT THE BEGINNING
		m_started = true;

		// do not pass messages to old (gui) log target!
		wxLogWindow *logw = new wxLogWindow(this, _T("Messages"), true, false);
		wxLog::SetActiveTarget(logw);
		// log window shall not obscure the main window
		this->Raise();

		m_guii.dialog2 = this;

		m_guii.out = m_wDirName;
		m_guii.nfiles = m_wnFiles;
		m_guii.bContinue = true;
		m_guii.bPause = false;
		m_guii.theApp = wxTheApp;
		// m_guii.cfiles = m_wcFiles;
		m_guii.wStep1 = m_wStep1;

		m_guii.wSpeed = m_wSpeed;
		m_guii.wProgress = m_wProgress;
		m_guii.wStep2 = m_wStep2;
		m_guii.wProgressGauge = m_wProgressGauge;

		m_guii.normalfont = m_wStep1->GetFont();
		
		m_guii.boldfont = m_guii.normalfont;
		m_guii.boldfont.SetWeight(wxFONTWEIGHT_BOLD); 
		

		m_dupfinder.SetGui(&m_guii);

		m_dupfinder.FindDuplicateFiles();

		if(!m_guii.bContinue) {
			ReturnToStart();
			return;
		}

		
		DupFinderDlg3 * resultdlg;

		Hide();
		RestoreLogTarget();

		resultdlg = new DupFinderDlg3(m_parent, m_dupfinder);

		resultdlg->Show();

		Destroy();
	}
}

void DupFinderDlg2::OnCancel(wxCommandEvent &WXUNUSED(event)) {
	int result = wxMessageBox(_T("Do you really want to cancel? "), 
		_T("Confirmation"), wxYES_NO | wxICON_QUESTION, this);

	if(result == wxYES) {
		m_guii.bContinue = false;
	}
	// no EndModal / Destroy here!
}



void DupFinderDlg2::OnShowMessages(wxCommandEvent &WXUNUSED(event)) {
	bool bCheck = 
		dynamic_cast<wxCheckBox *>(FindWindow(ID_SHOWMESSAGES))->GetValue();

	dynamic_cast<wxLogWindow *>(wxLog::GetActiveTarget())->Show(bCheck);
}

void DupFinderDlg2::ReturnToStart() {

	Hide();
	RestoreLogTarget();
	m_parent->ReturnToMe();
	Destroy();
}


void DupFinderDlg2::RestoreLogTarget() {
	wxLogWindow *logw = dynamic_cast<wxLogWindow *>(wxLog::GetActiveTarget());
	wxLog::SetActiveTarget(NULL);
	delete logw;
}

void DupFinderDlg2::OnPause(wxCommandEvent &WXUNUSED(event)) {
	
	if(m_guii.bPause) {
		// continue with process
		m_wPause->SetLabel(_T("&Pause"));
		
	} else {

		m_wPause->SetLabel(_T("C&ontinue"));

	}


	m_guii.bPause = !m_guii.bPause;

}
















