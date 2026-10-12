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

bin_PROGRAMS = dupf dupfgui
dupf_SOURCES = dupf.cpp dbl.cpp os_cc_specific.cpp file.cpp
dupf_CXXFLAGS = `wx-config --static --debug --cxxflags`
dupf_LDADD = @WX_LIBS@
#dupf_DEPENDENCIES = stdinc.h.gch
dupfgui_CXXFLAGS = `wx-config --static --debug --cxxflags`
dupfgui_SOURCES = dupfgui.cpp dbl.cpp os_cc_specific.cpp dialog1.cpp dialog2.cpp dialog3.cpp file.cpp
dupfgui_LDADD = @WX_LIBS@
#dupfgui_DEPENDENCIES = stdinc.h.gch

#stdinc.h.gch: stdinc.cpp stdinc.h
#	`wx-config --cxx` `wx-config --cxxflags` -g -c -Wall -x c++ stdinc.h