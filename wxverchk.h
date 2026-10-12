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

// This file checks that the wxWidgets used is recent enough. 
// Better getting a straightforward error than getting 
// compilation errors and not knowing why...

#ifdef _WIN32

#if !wxCHECK_VERSION(2,8,3)
#error "You have to have at least wxWidgets 2.8.3 on Windows! Update your version of wxWidgets! "
#endif

#endif /*_WIN32*/

#ifndef _WIN32

#if !wxCHECK_VERSION(2,7,1)
#error "You have to have at least wxWidgets 2.7.1! Update your version of wxWidgets! "
#endif

#endif /*!_WIN32*/
