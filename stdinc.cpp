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

/* this file is for precompiled header */

#include "stdinc.h"

#ifdef __BORLANDC__

// for borland, because brc32 doesn't work
// and i don't wont to use ilink32
#ifdef _DEBUG

#ifdef _UNICODE

#ifdef WXUSINGDLL
#pragma resource "bccud_shared\\rsrc.res"
#else
#pragma resource "bccud\\rsrc.res"
#endif

#else /* !UNICODE */

#ifdef WXUSINGDLL
#pragma resource "bccd_shared\\rsrc.res"
#else
#pragma resource "bccd\\rsrc.res"
#endif

#endif /* UNICODE */

#else /* !_DEBUG */

#ifdef _UNICODE

#ifdef WXUSINGDLL
#pragma resource "bccu_shared\\rsrc.res"
#else
#pragma resource "bccu\\rsrc.res"
#endif

#else /* !UNICODE */

#ifdef WXUSINGDLL
#pragma resource "bcc_shared\\rsrc.res"
#else
#pragma resource "bcc\\rsrc.res"
#endif

#endif /* UNICODE */

#endif /* DEBUG */

#endif /* BORLANDC */

