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
#include "os_cc_specific_gui.h"

// this file contains all other os_cc_specific stuff,
// but is kept seperate because of gui functions, which 
// would increase size of the command line program 
// very much, because all gui functions would be linked in


void AboutBox(const wxString &dev, const wxString &copyr, const wxString & desc, 
	const wxString &name, const wxString &version)
{
	wxAboutDialogInfo info;

	info.AddDeveloper(dev);
	info.SetCopyright(copyr);
	info.SetDescription(desc);
	info.SetName(name);
	info.SetVersion(version);
	
#ifdef __MINGW32__
	wxString tmp;
	tmp += name + _T(" ") + version + _T("\n");
	tmp += desc + _T("\n\n");
	tmp += copyr + _T("\n");
	tmp += _T("Developed by ") + dev + _T(".");
	wxMessageBox(tmp);
#else
	// mingw has problems with this
	wxAboutBox(info);
#endif

}

void SetListItemData(wxListView * list, long item, void *data)
{
#if wxCHECK_VERSION(2,8,4)
	// that line breaks compatibility versions < 2.8.4
	list->SetItemPtrData(item, (wxUIntPtr)data);
#else
	list->SetItemData(item, (long)data);
#endif
}

