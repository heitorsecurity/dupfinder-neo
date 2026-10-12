#
#   Duplicate Files Finder - search for duplicate files
#   Copyright (C) 2007-2008 Matthias Boehm
#
#   This program is free software; you can redistribute it and/or modify
#   it under the terms of the GNU General Public License as published by
#   the Free Software Foundation; either version 2 of the License, or
#   (at your option) any later version.
#
#   This program is distributed in the hope that it will be useful,
#   but WITHOUT ANY WARRANTY; without even the implied warranty of
#   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
#   GNU General Public License for more details.
#
#   You should have received a copy of the GNU General Public License
#   along with this program; if not, write to the Free Software
#   Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.
#

# -------------------------------- note ------------------------------
# Watcom doesn't support STL, so currently you can't compile the 
# program with Watcom!
# ---------------------------- end note ------------------------------

!ifndef debug
debug = 1
!endif
!ifndef unicode
unicode =1
!endif

outdir = wat
name = dupf
namegui = $(name)gui
wxdir = C:\wx
version = 

!if $(unicode)==1
version = $(version)u
!endif

!if $(debug)==1
version = $(version)d
!else
!endif

outdir = $(outdir)$(version)

opt = -wx -xs -fo"$(outdir)\\"  -i="$(wxdir)\include" -i="$(wxdir)\lib\wat_lib\msw$(version)" -d__WXMSW__ -dWIN32 /fh="$(outdir)\stdinc.pch" -q -bm

!if $(debug)==1
dbg = -d3 -d_DEBUG -dDEBUG -od -d__WXDEBUG__
!else
dbg = -Ox -dNDEBUG -ox
!endif

!if $(unicode)==1
uniopt = -d_UNICODE -dUNICODE -dwxUSE_UNICODE=1 
!else
uniopt = 
!endif

opt = $(opt) $(dbg) $(uniopt)

libs =  shlwapi.lib ole32.lib uuid.lib user32.lib advapi32.lib shell32.lib
libsgui = $(libs) gdi32.lib comctl32.lib comdlg32.lib oleaut32.lib rpcrt4.lib
headers = dbl.h largeint.h ulargeint.h os_cc_specific.h os_cc_specific_includes.h stdinc.h file.h
headersgui = $(headers) dupfgui.h dialog1.h dialog2.h dialog3.h

all: $(outdir) $(outdir)\$(name).exe $(outdir)\$(namegui).exe

$(outdir): 
	-@if not exist $(outdir) mkdir $(outdir)

$(outdir)\$(name).exe: $(outdir)\stdinc.obj $(outdir)\dbl.obj $(outdir)\dupf.obj $(outdir)\os_cc_specific.obj \
	$(outdir)\file.obj
	___ $(opt) -Fe"$(outdir)\$(name).exe" $** $(libs) /link /LIBPATH:"$(wxdir)\lib\vc_lib"

$(outdir)\$(namegui).exe: $(outdir)\stdinc.obj $(outdir)\dbl.obj $(outdir)\dupfgui.obj $(outdir)\os_cc_specific.obj \
	$(outdir)\dialog1.obj $(outdir)\dialog2.obj $(outdir)\dialog3.obj $(outdir)\file.obj
	wpp386 $(opt) -Fe"$(outdir)\$(namegui).exe" $** $(libsgui) /link /LIBPATH:"$(wxdir)\lib\vc_lib"

$(outdir)\dbl.obj: dbl.cpp $(headers)
	wpp386 $(opt) dbl.cpp

$(outdir)\dupf.obj: dupf.cpp $(headers)
	wpp386 $(opt) dupf.cpp

$(outdir)\os_cc_specific.obj: os_cc_specific.cpp $(headers)
	wpp386 $(opt) os_cc_specific.cpp

$(outdir)\file.obj: file.cpp $(headers)
	wpp386 $(opt) file.cpp

$(outdir)\stdinc.obj: stdinc.cpp stdinc.h os_cc_specific_includes.h
	wpp386 $(opt) -fhw stdinc.cpp

$(outdir)\dupfgui.obj: dupfgui.cpp $(headersgui) dbl.cpp
	wpp386 $(opt) dupfgui.cpp

$(outdir)\dialog1.obj: dialog1.cpp $(headersgui)
	wpp386 $(opt) dialog1.cpp

$(outdir)\dialog2.obj: dialog2.cpp $(headersgui)
	wpp386 $(opt) dialog2.cpp

$(outdir)\dialog3.obj: dialog3.cpp $(headersgui)
	wpp386 $(opt) dialog3.cpp

run:
	-$(outdir)\$(name).exe
