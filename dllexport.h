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

#ifndef __DUPF_DLL_EXPORT
#define __DUPF_DLL_EXPORT

// cygwin does use unix makefile. 
// which doesn't build dlls, but
// defined _WIN32 nevertheless
#if defined( _WIN32) && !defined(__CYGWIN__)

#if defined(DUPF_BUILDING_DLL)
	#define DUPF_DLLEXPORT __declspec(dllexport)
#else
	#define DUPF_DLLEXPORT __declspec(dllimport)
#endif

#else // other than Win32

	#define DUPF_DLLEXPORT
#endif


#endif
