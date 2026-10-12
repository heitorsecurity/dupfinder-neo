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
 
 // DllMain: initializes (including, but not only) (indirectly) wxTheApp
 // by calling wxEntryStart.
 
extern "C"
BOOL WINAPI DllMain(HINSTANCE, DWORD fdReason, LPVOID) {
	BOOL bResult = TRUE;
	int argc = 0;
	wxChar **argv = NULL;

	switch(fdReason) {
	case DLL_PROCESS_ATTACH:
		bResult = ::wxEntryStart(argc, argv);
		break;
	case DLL_PROCESS_DETACH:
		::wxEntryCleanup();
		break;
	default:
		break;
	}
	
	return bResult;
}
