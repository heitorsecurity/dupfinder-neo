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
#include "dupfgui.h"
#include "dbl.h"
#include "dialog1.h"

class DupFinderApp :public wxApp {
public:
	virtual bool OnInit();
	virtual int OnExit();

};

IMPLEMENT_APP(DupFinderApp)


bool DupFinderApp::OnInit() {
	DupFinderDlg *maindlg = new DupFinderDlg (NULL);

	DUPF_INITIALIZE_COM

	maindlg->Show(true);
	SetTopWindow(maindlg);

	return true;
}

int DupFinderApp::OnExit() {

	DUPF_UNINITIALIZE_COM

	return 0;
}





















