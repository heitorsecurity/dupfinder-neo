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


#ifndef __OS_CC_SPECIFIC_H_123
#define __OS_CC_SPECIFIC_H_123

#include "dllexport.h"

/*****************************************************************************/

// secure function unknown to !Microsoft Visual C++
#ifndef _MSC_VER

	// secure not needed (only with strings)
	#define _stscanf_s _stscanf

#endif /* !def(_MSC_VER) */


/*****************************************************************************/

// cygwin/gcc (on unix!), except mingw, don't have a _t -> wide/single char mapping (tchar.h)

#if defined (__CYGWIN__) || (defined(__GNUC__) && !defined(__MINGW32__) )

	#if defined(_UNICODE) || defined(UNICODE)

		#ifndef _T
		#define _T(a) L ## a
		#endif

		#define _TCHAR wchar_t
		#define _tmain wmain
		#define _ftprintf fwprintf
		#define _stscanf swscanf

	#else // !defined(UNICODE)

		#ifndef _T
		#define _T(a) a
		#endif

		#define _TCHAR char
		#define _tmain main
		#define _ftprintf fprintf
		#define _stscanf sscanf

	#endif // defined (UNICODE)

#endif /* defined (__CYGWIN__) || */

/*****************************************************************************/

// MingW/unix gcc/cygwin (all gcc!)  don't support unicode wmain
//
// the following macro does that:
//
// loading *_TCHAR* argv over to *wxChar*
// 
// note: no need exceptionally to delete argv, because 1) it is only a small piece
// of memory, and 2) there would be a delete [] argv necessarry
// in front of every return instruction
// perhaps if i have the time, i'll change it...

#if defined(__GNUC__) // or that?

	#define DECLARE_MAIN int main(int argc, char *_argv[]) { \
		/* xxx yyy;  uncomment this for testing */ \
		_argv++; \
		argc--; \
		 \
		wxString *argv = new wxString[argc]; \
		{ int i; \
			for(i = 0; i < argc; i++) { \
				argv[i] = wxString(_argv[i], wxConvLocal); \
			} \
		}
#else

	#define DECLARE_MAIN int _tmain(int argc, _TCHAR * _argv[]) { \
		_argv++; \
		argc--; \
		 \
		wxString *argv = new wxString[argc]; \
		{ int i;  \
			for(i = 0; i < argc; i++) { \
				argv[i] = _argv[i]; \
			} \
		}
#endif /* defined(__MINGW32__) */

/*****************************************************************************/

// for initilizing COM system (needed for symbolic links)

#ifdef _WIN32
	#define DUPF_INITIALIZE_COM ::CoInitialize(NULL);
	#define DUPF_UNINITIALIZE_COM ::CoUninitialize();
#else
	#define DUPF_INITIALIZE_COM
	#define DUPF_UNINITIALIZE_COM
#endif


/*****************************************************************************/

// structures

struct DUPF_DLLEXPORT FileData
{
	wxString name;
	wxULongLong size;
	// more is not needed
};

class DUPF_DLLEXPORT wxExtDirTraverser : public wxDirTraverser
{
public:
	virtual wxDirTraverseResult OnExtFile(const FileData &) {
		return wxDIR_CONTINUE;
	}
	
};

/*****************************************************************************/

// unconst: for mingw/gcc unix/cygwin only!
// unix' STL library doesn't work like all other compilers' , 
// sets don't support iterator (they return const_iterator in any case)
// but i can safely change every other element that the size (because
// the size is the element by which the set is sorted)

#ifdef __GNUC__
	template <class T> T & unconst(const T & x) {
		return const_cast<T &>(x);
	}
#else /*!def(__GNUC__) */
	// else return identity
	#define unconst(a) (a)
#endif


/*****************************************************************************/


// prototypes

bool DUPF_DLLEXPORT IsSymLink(const wxString &);
bool DUPF_DLLEXPORT IsSymLinkSupported();
bool DUPF_DLLEXPORT CreateSymLink(const wxString &, const wxString &);

// bool DUPF_DLLEXPORT IsHardLink(const wxString &);  // currently not needed/ (supported (?))
bool DUPF_DLLEXPORT IsHardLinkSupported();
bool DUPF_DLLEXPORT CreateHardLink(const wxString &, const wxString &);

void DUPF_DLLEXPORT Traverse(const wxString &, const wxString *, int, /* wxString (*)[], int, */
	int, wxExtDirTraverser &);

wxChar DUPF_DLLEXPORT GetPathSepChar();

bool DUPF_DLLEXPORT StringToULongLong(const wxString&, wxULongLong &);

// would be nice if i had this...
// bool DUPF_DLLEXPORT MoveToTrash(const wxString &);

#endif /* defined(OS_CC_SPECIFIC_H) */



