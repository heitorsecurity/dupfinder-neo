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


#ifdef _WIN32

#define WIN32_LEAN_AND_MEAN 
#include <windows.h>

// Cygwin does not support Unicode yet
#ifndef __CYGWIN__
#include <tchar.h>
#endif

#include <shlobj.h>

#endif /* _Win32 */

/* perhaps better if !defined(_WIN32) && defined(__GNUC__) ???? */
#ifdef __UNIX__

// Unix includes here

#endif /* !defined(_WIN32) */

