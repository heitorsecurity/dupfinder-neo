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

using namespace std;
#include "os_cc_specific.h"
#include "dbl.h"
#include "file.h"
#include "dllexport.h"

/********* options ************/

#if defined(__UNIX__) && !defined(_WIN32)

// for unix, there is no native routine implemented, 
// so use wxWidgets
#define FINDFILES_USE_WXWIDGETS

#else

// for windows, there is a native routine, 
// which is much faster than the wxWidget's one

#ifndef FINDFILES_USE_WXWIDGETS
// #define FINDFILES_USE_WXWIDGETS
#endif

#endif /* defined(__UNIX__) && !defined(_WIN32) */


// option, which controls profiling
// on windows platforms
// include "profile.h" always after this definition!
// #define PROFILE

#include "profile.h"


// for testing
// #define TEST
// #define RANDOM_NOISE

#define REFRESH_INTERVAL 1 /* in seconds */


#ifdef PROFILE
LARGE_INTEGER all;
LARGE_INTEGER disk;
LARGE_INTEGER comparetime;
LARGE_INTEGER fileseek;
LARGE_INTEGER fileopen;
LARGE_INTEGER fileread;
#endif /* defined(PROFILE) */


#ifdef TEST
#include "filetest.cpp"
#endif


/******************************************************************************************/
/********************************                       ***********************************/
/******************************** DuplicateFilesFinder  ***********************************/
/********************************    public methods     ***********************************/
/********************************                       ***********************************/
/******************************************************************************************/


DuplicateFilesFinder::DuplicateFilesFinder(GuiInfo * gui, bool bQuiet) 
	: m_quiet(bQuiet), m_gui(gui)
{ 
	Reset(); 
}

DuplicateFilesFinder::~DuplicateFilesFinder()
{
}

void DuplicateFilesFinder::AddPath(const SearchPathInfo &path)
{
	m_paths.push_back(path);
}

void DuplicateFilesFinder::Reset()
{
	m_paths.clear();
	m_duplicates.clear();
	m_sortedbysize.clear();
	m_emptyDirs.clear();
	m_visitedDirs.clear();
	m_dirsWithFiles.clear();
	m_dirsWithSubdirs.clear();

	m_scanMode = SCAN_MODE_DUPLICATES_ONLY;
	m_protectSystemDirs = true;

	m_bFirst = true;

	m_state = m_prevState 
		= DUPF_STATE_NOT_STARTED_YET;
	
	m_nBytesRead = m_nPrevBytesRead
		= m_nFilesRead 
		= m_nPrevFilesRead
		= m_nSizesDone 
		= m_nPrevSizesDone 
		= m_nBytesDone
		= m_nPrevBytesDone
		= 0;

	m_nSumFiles = m_nSumSizes
		= m_nSumBytes
		= 0;

	m_curdir = wxEmptyString;
	m_output = wxEmptyString;

	m_current_spi = NULL;
}

list<DuplicatesGroup> &DuplicateFilesFinder::GetDuplicates()
{
	// only return duplicates if the process of finding
	// them really has been successful and is finished
	assert(m_state == DUPF_STATE_FINISHED); 
	return m_duplicates;
}

void DuplicateFilesFinder::FindDuplicateFiles() {

	if(m_scanMode == SCAN_MODE_EMPTY_DIRS_ONLY) {
		m_state = DUPF_STATE_FIND_FILES;
		FindEmptyDirectories();
		m_state = DUPF_STATE_FINISHED;
		return;
	}

	m_state = DUPF_STATE_FIND_FILES;

	FindFiles();

	if(m_scanMode == SCAN_MODE_BOTH) {
		FindEmptyDirectories();
	}

	if(YieldAndTestAbort()) {
		return;
	}

	// Would be nice, but MUCH too slow!
	// m_state = DUPF_STATE_REMOVE_DOUBLE_FILES
	// 
	// RemoveDoubleFiles();

	RemoveUnimportantSizes();

	m_state = DUPF_STATE_COMPARE_FILES;

	GetEqualFiles();

	if(YieldAndTestAbort()) {
		return;
	}

	ConstructResultList();

	// Really finished!
	m_state = DUPF_STATE_FINISHED;
}

void DuplicateFilesFinder::CalculateStats(DuplicateFilesStats &stats) const
{
	list<DuplicatesGroup>::const_iterator it;

	stats.nDuplicateFiles = 0;
	stats.nWastedSpace = 0;
	stats.nFilesWithDuplicates = 0;
	stats.nEmptyDirsFound = (wxULongLong)m_emptyDirs.size();
	stats.nEmptyDirsRemoved = 0;

	for(it = m_duplicates.begin(); 
		it != m_duplicates.end(); 
		it++) {

		if(it->files.size() > 1) {
			stats.nFilesWithDuplicates++;
			stats.nDuplicateFiles += it->files.size()-1;
			stats.nWastedSpace += wxULongLong(it->files.size()-1)*it->size;
		}
	}
}

/******************************************************************************************/
/********************************                        **********************************/
/******************************** wxExtDirTraverser impl **********************************/
/********************************                        **********************************/
/******************************************************************************************/

wxDirTraverseResult DuplicateFilesFinder::OnFile(const wxString &filename, const wxULongLong *pSize)
{
	assert(m_current_spi);
	const SearchPathInfo *spi = m_current_spi;

	if(IsSymLink(filename)) {
		return UpdateInfo(NULL);
	}

	bool bMatches = false;
	for(list<wxString>::iterator it = m_ExcludeMasks.begin(); it != m_ExcludeMasks.end(); it ++) {
		if(filename.Matches(*it) ) {
			bMatches = true;
			break;
		}
	}

	if(bMatches) { 
		return UpdateInfo(NULL);
	}

	File f;
	fileinfosize fis;
	multiset_fileinfosize::iterator it2;
	wxULongLong size;
	STARTTIME(__OnFile);
	STARTTIME(__findsize);
	if(pSize == NULL) {
		// slow
		size = wxFileName::GetSize(filename);
	} else {
		size = *pSize;
	}
	STOPTIME(__findsize);
		
	bool bFitsMinSize = size >= spi->nMinSize;
	bool bFitsMaxSize = size <= spi->nMaxSize || spi->nMaxSize == 0;

	assert(spi->nMaxSize >= spi->nMinSize || spi->nMaxSize == 0);
		
	if(size != wxInvalidSize && 
		(size != 0 || spi->bEmptyFiles) &&
		bFitsMinSize &&
		bFitsMaxSize) {
		// init structure
		STARTTIME(__insert);
		f.SetName(filename);

		fis.size = size;
		it2 = m_sortedbysize.find(fis);

			
		if(it2 != m_sortedbysize.end()) {
			unconst(*it2).files.push_back(f);
		}
		else {
			fis.size = size;
			fis.files.push_back(f);
			m_sortedbysize.insert(fis);
			m_nSumSizes++;
		}
		STOPTIME(__insert);
	}
	STOPTIME(__OnFile);

	m_nSumFiles++;
	m_nSumBytes += size;

	return UpdateInfo(NULL);
}

wxDirTraverseResult DuplicateFilesFinder::OnDir(const wxString &dirname)
{
	// do NOT follow links! 
	if(IsSymLink(dirname)) {
		return wxDIR_IGNORE;
	}
		
	return UpdateInfo(&dirname);
}

wxDirTraverseResult DuplicateFilesFinder::UpdateInfo(const wxString *dirname) 
{
	if(YieldAndTestAbort()) {
		return wxDIR_STOP;
	}

	if(dirname) {
		m_curdir = *dirname;
	}

	UpdateStatusDisplay();

	return wxDIR_CONTINUE;
}

/******************************************************************************************/
/********************************                       ***********************************/
/******************************** DuplicateFilesFinder  ***********************************/
/********************************   internal methods    ***********************************/
/********************************                       ***********************************/
/******************************************************************************************/

bool DuplicateFilesFinder::Traverse(const SearchPathInfo *spi) 
{
	int flags = wxDIR_FILES | 
		(spi->bRecursive ? wxDIR_DIRS : 0 ) | 
		(spi->bHidden ? wxDIR_HIDDEN : 0);

	wxStringTokenizer tok_excl(spi->Exclude, GetPathSepChar());

	m_ExcludeMasks.clear();

	while (tok_excl.HasMoreTokens()) {
		m_ExcludeMasks.push_back(tok_excl.GetNextToken());
	}

	m_current_spi = spi;

#if defined( FINDFILES_USE_WXWIDGETS )
	bool bSuccess = true;
	wxStringTokenizer tok(spi->Include, GetPathSepChar());
	while(tok.HasMoreTokens()) {
		wxString curmask = tok.GetNextToken();
		wxDir root(spi->path);
		size_t result = root.Traverse(*this, curmask, flags);
		bSuccess = bSuccess && result != (size_t)-1;
	}

	return bSuccess;
#else
	wxStringTokenizer tok(spi->Include, GetPathSepChar());
	int length = tok.CountTokens();
	wxString *masks = new wxString[length];

	int i = 0;
	while(tok.HasMoreTokens()) {
		masks[i] = tok.GetNextToken();
		i++;
	}
	::Traverse(spi->path, masks, length, flags, *this);

	delete [] masks;

	return true;
#endif /* FINDFILES_USE_WXWIDGETS */
}


void	DuplicateFilesFinder::FindFiles()
{
	fileinfosize fis;
	list<File>::iterator it;
	multiset_fileinfosize::iterator it2;
	list<SearchPathInfo>::iterator it3;

#ifdef PROFILE
	LARGE_INTEGER __all = {0, 0};
#endif


	if(!m_quiet) {
		wxLogMessage(_T("Step 1: Searching files... "));
	}

	for (it3 = m_paths.begin(); it3 != m_paths.end(); it3++) {
		if(!m_quiet) {
			wxLogMessage(_T("        ... in \"%s\" ... "), it3->path.c_str());
		}
		
#ifdef PROFILE
		__OnFile.QuadPart = 0;
		__OnDir.QuadPart = 0;
		__findsize.QuadPart = 0;
		__normalize.QuadPart = 0;
		__finddir.QuadPart = 0;
		__insert.QuadPart = 0;
#endif

		STARTTIME(__all);
		Traverse(&(*it3));
		STOPTIME(__all);

		if(YieldAndTestAbort()) {
			return;
		}

		
#ifdef PROFILE
		_ftprintf(stderr, _T("all:      % 8.3f\n"), SECONDS(__all));
		_ftprintf(stderr, _T("OnFile:   % 8.3f (%.3f %%) \n"), SECONDS(traverser.__OnFile), 
			SECONDS(traverser.__OnFile)/SECONDS(__all)*100.0);
		_ftprintf(stderr, _T("OnDir:    % 8.3f (%.3f %%) \n"), SECONDS(traverser.__OnDir), 
			SECONDS(traverser.__OnDir)/SECONDS(__all)*100.0);
		_ftprintf(stderr, _T("Rest:     % 8.3f (%.3f %%) \n\n"), SECONDS(__all)-SECONDS(traverser.__OnFile)-SECONDS(traverser.__OnDir), 
			(SECONDS(__all)-SECONDS(traverser.__OnFile)-SECONDS(traverser.__OnDir))/SECONDS(__all)*100.0);
		_ftprintf(stderr, _T("findsize: % 8.3f (%.3f %%) \n\n"), SECONDS(traverser.__findsize), 
			SECONDS(traverser.__findsize)/SECONDS(traverser.__OnFile)*100.0);
		_ftprintf(stderr, _T("insert:   % 8.3f (%.3f %%) \n"), SECONDS(traverser.__insert), 
			SECONDS(traverser.__insert)/SECONDS(traverser.__OnFile)*100.0);
		_ftprintf(stderr, _T("normalize:% 8.3f (%.3f %%) \n"), SECONDS(traverser.__normalize), 
			SECONDS(traverser.__normalize)/SECONDS(traverser.__OnDir)*100.0);
		_ftprintf(stderr, _T("finddir:  % 8.3f (%.3f %%) \n"), SECONDS(traverser.__finddir), 
			SECONDS(traverser.__finddir)/SECONDS(traverser.__OnDir)*100.0);

#endif
	}

	UpdateStatusDisplay();
}

void	DuplicateFilesFinder::GetEqualFiles()
{
	list<File>::iterator it, it3;
	multiset_fileinfosize::iterator it2;
	list<fileinfoequal>::iterator it4;

#ifdef TEST
	list<File>::iterator __it3;
	list<File>::iterator __it5, __it;
	multiset_fileinfosize::iterator __it2;
	list<fileinfoequal>::iterator __it4;
#endif /*defined(TEST) */

#ifdef RANDOM_NOISE
	{
		srand(time(NULL));
		const int randmax = 10000;
		for(int i = 0; i < randmax; i++) {
			int j, k;
			list<File>::iterator st1, st2;
			int a, b;
			a = rand() % m_sortedbysize.size();
			for(it2 = m_sortedbysize.begin(), j = 0; j < a; it2++, j++) {}
			b = rand() % it2->files.size();
			for(k = 0, it = it2->files.begin(); k < b; k++, it++) { }
			st1 = it;
			a = rand() % m_sortedbysize.size();
			for(it2 = m_sortedbysize.begin(), j = 0; j < a; it2++, j++) {}
			b = rand() % it2->files.size();
			for(k = 0, it = it2->files.begin(); k < b; k++, it++) {}
			st2 = it;

			wxString tmp = st2->GetName();
			st2->SetName(st1->GetName());
			st1->SetName(tmp);
		}
	}
#endif /* RANDOM_NOISE */
	

	if(!m_quiet) {
		wxLogMessage(_T("Step 2: Comparing files with same size for equality... "));
	}

	// m_nSizesDone = 0; // Reset() resets this automatically

	STARTTIME(comparetime);

	for(it2 = m_sortedbysize.begin(); it2 != m_sortedbysize.end(); it2++) {
		// printf("size %" I64 ": %i file(s) \n", it2->size.QuadPart, it2->files.size());
		m_nSizesDone++;
		UpdateStatusDisplay();
		assert(it2->files.size() > 1);
		if(it2->files.size() > 1) { /* in fact, this isn't neccesarry any more */
			bool bDeleted3;
			bool bFirstDouble;
			fileinfoequal fiq;
			list<File>::iterator ittmp;
			bool bEqual;
			for(it = unconst(*it2).files.begin(); it != unconst(*it2).files.end(); /*it++*/) {
				bFirstDouble = true;
				it3 = it;
				for(it3++; it3 != it2->files.end(); bDeleted3 ? it3 : it3++) {
					bDeleted3 = false;
					bEqual = CompareFiles(*it, *it3, it2->size);

					if(YieldAndTestAbort()) {
						return;
					}

#ifdef TEST
					if(bEqual != comparefiles0(*it, *it3)) { assert(0 == 1); abort(); }
#endif
					if(bEqual) {
						if(bFirstDouble) {
							bFirstDouble = false;
							fiq.files.clear();
							fiq.files.push_back(*it);
							fiq.files.push_back(*it3);
							unconst(*it2).equalfiles.push_back(fiq);
						}
						else {
							unconst(*it2).equalfiles.back().files.push_back(*it3);
						}

						bDeleted3 = true;
						ittmp = it3;
						it3++;
						ittmp->Close();
						unconst(*it2).files.erase(ittmp);

						m_nBytesDone += it2->size;
						m_nFilesRead++;
					}

					UpdateStatusDisplay();

					// nComparedBytes.QuadPart += (*it).size.QuadPart;
				}
#ifdef TEST
				/* no doubles found */
				if(bFirstDouble) {
					bool bNotEqual = true;
					__it3 = it;
					for(__it3++; __it3 != it2->files.end(); __it3++) {
						bEqual = comparefiles0(*it, *__it3);
						if(bEqual) {
							bNotEqual = false;
							break;
						}
					}
					assert(bNotEqual);
					if(!bNotEqual) { abort(); }
				}
#endif
				unconst(*it2).files.front().Close();
				unconst(*it2).files.pop_front();
				it = unconst(*it2).files.begin();

				m_nBytesDone += it2->size;
				m_nFilesRead++;
			} /* for it */
		} /* if size > 1 */
		/* Close files */
		for(it = unconst(*it2).files.begin(); it != unconst(*it2).files.end(); it++) {
			it->Close();
		} 
		unconst(*it2).files.clear();
	}

	STOPTIME(comparetime);

	if(!m_quiet) {
		ResetLine();
		wxLogMessage(_T("        done. \n"));
	}

#ifdef TEST
	for(__it2 = m_sortedbysize.begin(); __it2 != m_sortedbysize.end(); __it2++) {
		for(__it4 = __it2->equalfiles.begin(); __it4 != __it2->equalfiles.end(); __it4++) {
			for(__it = __it4->files.begin(); __it != __it4->files.end(); __it++) {
				__it5 = __it;
				for(__it5++; __it5 != __it4->files.end(); __it5++) {
					if(!comparefiles0((*__it), (*__it5))) {
						_ftprintf(stderr, _T("Error: %s != %s\n"), 
							__it->GetName().c_str(), 
							__it5->GetName().c_str());
					}
				}
			}
		}
	}
#endif

}

wxULongLong DuplicateFilesFinder::ComputeSampleHash(File &file, const wxULongLong &size) {
	if(size == 0) return 0;
	if(!file.Open(size)) return 0;
	if(!file.Restart()) return 0;

	static char *sampleBuf = NULL;
	if(sampleBuf == NULL) {
		sampleBuf = new char[File::GetBufSize()];
	}

	char *pb = sampleBuf;
	size_t nRead = File::GetBufSize();
	wxULongLong_t hash = 14695981039346656037ULL;

	if(file.Read(&pb, nRead)) {
		size_t sampleLen = min(nRead, (size_t)4096);
		for(size_t i = 0; i < sampleLen; i++) {
			hash ^= (unsigned char)pb[i];
			hash *= 1099511628211ULL;
		}
	}

	file.Restart();
	return wxULongLong(hash);
}

bool	DuplicateFilesFinder::CompareFiles(File &f1, File &f2, const wxULongLong &size) {
	if(size == 0) return true;

	// Fast pre-filter: Sample hash comparison
	wxULongLong h1 = ComputeSampleHash(f1, size);
	wxULongLong h2 = ComputeSampleHash(f2, size);
	if(h1 != h2) {
		return false;
	}

	// 100% accurate byte-by-byte comparison
	bool bResult;
	static char *b1, *b2;
	char *pb1, *pb2;
	if(b1 == NULL) { b1 = new char[File::GetBufSize()]; }
	if(b2 == NULL) { b2 = new char[File::GetBufSize()]; }
	unsigned int BUFSIZE = File::GetBufSize();
	unsigned int n1, n2;

	if(!f1.Open(size)) {
		return false;
	}
	if(!f2.Open(size)) {
		return false;
	}

	// seek to the beginning
	if( !f1.Restart() ) {
		return false;
	}
	if( !f2.Restart() ) {
		return false;
	}

	while(1) {
		UpdateStatusDisplay();

		if(YieldAndTestAbort()) {
			return false;
		}

		n1 = n2 = BUFSIZE;
		bool br1, br2;

		pb1 = b1;
		pb2 = b2;

		br1 = f1.Read(&pb1, n1);
		br2 = f2.Read(&pb2, n2);

		m_nBytesRead += n1 + n2;
		

		if(n1 != n2 || !br1 || !br2) {
			bResult = false;
			goto End;
		}

		if(memcmp(pb1, pb2, n1) != 0) {
			bResult = false;
			goto End;
		}

		if(n1 < BUFSIZE)
			break;
	}

	bResult = true;

End:	return bResult;
}

void DuplicateFilesFinder::UpdateStatusDisplay()
{
	time_t tcurrent;
	wxString tmp;
	wxString tmp2;

	tcurrent = time(NULL);

	if(tcurrent - m_tlast >= REFRESH_INTERVAL || m_bFirst) {
		if(m_gui) {
			if(m_prevState != m_state) {
				switch(m_state) {
				case DUPF_STATE_FIND_FILES:
					m_gui->wStep1->SetFont(m_gui->boldfont);
					break;
				case DUPF_STATE_COMPARE_FILES:
					m_gui->wStep1->SetFont(m_gui->normalfont);

					m_gui->out->Disable();
					m_gui->out->SetValue(_T(""));

					m_gui->wStep2->SetFont(m_gui->boldfont);

					break;
				default:
					break;
				}
			} /* if (m_prevState != m_state) */

			if((m_prevState == DUPF_STATE_FIND_FILES &&
				m_state > DUPF_STATE_FIND_FILES) || 
				m_state == DUPF_STATE_FIND_FILES) {
				tmp.Printf(_T("%") wxLongLongFmtSpec _T("u file(s), %")
					wxLongLongFmtSpec _T("u size(s)"), 
					m_nSumFiles.GetValue(), 
					m_nSumSizes.GetValue());

				m_gui->nfiles->SetLabel(tmp);
			}
				
			switch(m_state) {
			case DUPF_STATE_FIND_FILES:
				m_gui->out->SetValue(m_curdir);

				break;
			case DUPF_STATE_COMPARE_FILES:
				tmp.Printf(_T("%") wxLongLongFmtSpec _T("u file(s), %")
					wxLongLongFmtSpec _T("u size(s)"), 
					m_nSumFiles.GetValue(), 
					m_nSumSizes.GetValue());
				m_gui->nfiles->SetLabel(tmp);

				// tmp.Printf(_T("%") wxLongLongFmtSpec _T("u file(s), %u sizes"), 
				// 	nFiles2.GetValue(), m_sortedbysize.size());
				// m_gui->cfiles->SetLabel(tmp);

				tmp.Printf(_T("size %") wxLongLongFmtSpec _T("u/%")
					wxLongLongFmtSpec _T("u (%.2f %%)\n"), 
					m_nSizesDone.GetValue(), m_nSumSizes.GetValue(), 
					m_nSizesDone.ToDouble()/m_nSumSizes.ToDouble()*100);

				tmp2.Printf(_T("%") wxLongLongFmtSpec _T("u files / ")
					_T("%") wxLongLongFmtSpec _T("u files (%.2f %%)\n"), 
					m_nFilesRead.GetValue(), 
					m_nSumFiles.GetValue(), 
					m_nFilesRead.ToDouble()/m_nSumFiles.ToDouble()*100.0);
				tmp += tmp2;
				
				tmp2.Printf(_T("%.2f mb / %.2f mb (%.2f %%)"),
					m_nBytesDone.ToDouble()/1024/1024, 
					m_nSumBytes.ToDouble()/1024/1024, 
					m_nBytesDone.ToDouble()/m_nSumBytes.ToDouble()*100);
				tmp += tmp2;
				
				m_gui->wProgress->SetLabel(tmp);

				tmp.Printf(_T("%.2f mb/sec, %.2f files/sec"), 
					(m_nBytesRead-m_nPrevBytesRead).ToDouble()/REFRESH_INTERVAL/1024/1024, 
					(m_nFilesRead - m_nPrevFilesRead).ToDouble()/REFRESH_INTERVAL);
				m_gui->wSpeed->SetLabel(tmp);

				// m_gui->wProgressGauge->SetValue((m_nBytesDone*1000/m_nSumBytes).GetValue());
				m_gui->wProgressGauge->SetValue((m_nFilesRead*1000/m_nSumFiles).ToULong());

				m_gui->dialog2->GetSizer()->Layout();
				// m_gui->dialog2->GetSizer()->SetSizeHints(m_gui->dialog2);
				break;
			default:
				break;
			}
		}
		else if(!m_quiet && m_state == DUPF_STATE_COMPARE_FILES) {
			ResetLine();
			m_output.Printf(_T(" %.2f mb/sec, "), (m_nBytesRead-m_nPrevBytesRead).ToDouble()
				/REFRESH_INTERVAL/1024.0/1024.0);

			tmp.Printf(/* _T("size %")
				wxLongLongFmtSpec _T("u/%")
				wxLongLongFmtSpec _T("u, ") */
				_T("file %")
				wxLongLongFmtSpec _T("u/%")
				wxLongLongFmtSpec _T("u (%.2f %%)"),
				// m_nSizesDone.GetValue(), m_nSumSizes.GetValue(), 
				m_nFilesRead.GetValue(), m_nSumFiles.GetValue(), 
				m_nFilesRead.ToDouble()/m_nSumFiles.ToDouble()*100.0);

			m_output += tmp;

			_ftprintf(stderr, _T("%s"), m_output.c_str());

		}

		m_nPrevBytesRead = m_nBytesRead;
		m_nPrevSizesDone = m_nSizesDone;
		m_nPrevFilesRead = m_nFilesRead;
		m_nPrevBytesDone = m_nBytesDone;
		m_prevState = m_state;
		m_tlast = tcurrent;
		m_bFirst = false;
	}
	// Sleep(1);
}

void DuplicateFilesFinder::ResetLine() 
{
	if(!m_quiet) {
		int n = m_output.Length();
		int t;
		for(t = 0; t < n; t++) {
			_ftprintf(stderr, _T("\b"));
		}
		for(t = 0; t < n; t++) {
			_ftprintf(stderr, _T(" "));
		}
		for(t = 0; t < n; t++) {
			_ftprintf(stderr, _T("\b"));
		}
	}
}

void DuplicateFilesFinder::ConstructResultList()
{
	multiset_fileinfosize::iterator it1;
	list<fileinfoequal>::iterator it2;
	DuplicatesGroup dupl;

	for(it1 = m_sortedbysize.begin(); 
		!m_sortedbysize.empty(); 
		it1 = m_sortedbysize.begin()) {
		dupl.size = it1->size;
		for(it2 = unconst(*it1).equalfiles.begin(); 
			!it1->equalfiles.empty(); 
			it2 = unconst(*it1).equalfiles.begin()) {

			dupl.files = it2->files;
			// immediately delete memory not needed, to be keeping memory 
			// usage low
			m_duplicates.push_back(dupl);
			unconst(*it1).equalfiles.erase(it2);
		}
		m_sortedbysize.erase(it1);
	}

	assert(m_sortedbysize.empty());
}

bool DuplicateFilesFinder::YieldAndTestAbort()
{
	if(m_gui) {
		m_gui->theApp->Yield();
		while(m_gui->bPause && m_gui->bContinue) {
			wxMilliSleep(10);
			m_gui->theApp->Yield();
		}

		if(!m_gui->bContinue) {
			m_state = DUPF_STATE_ERROR;
			return true;
		}
	}
	return false;
}

#if 0
void DuplicateFilesFinder::RemoveDoubleFiles() {
	multiset_fileinfosize::iterator it;
	list<File>::iterator it2;
	list<File>::iterator it3;
	bool bDeleted;

	for(it = m_sortedbysize.begin(); it != m_sortedbysize.end(); it++) {
		for(it2 = it->files.begin(); it2 != it->files.end(); it2++) {
			it3 = it2;
			for(it3++; it3 != it->files.end(); bDeleted ? it3 : it3++) {
				bDeleted = false;
				if(it2->GetNormName() == it3->GetNormName()) {
					list<File>::iterator it3tmp;
					it3tmp = it3;
					it3++;
					it->files.erase(it3tmp);
					bDeleted = true;
				}
			}
		}
	}
}
#endif

void DuplicateFilesFinder::RemoveUnimportantSizes() {

	multiset_fileinfosize::iterator it2;
	bool bDeleted;
	multiset_fileinfosize::iterator it2tmp;
	
	/* sort all sizes out where only one file has that particular size */

	for(it2 = m_sortedbysize.begin(); it2 != m_sortedbysize.end(); bDeleted ? it2 : it2++) {
		if(it2->files.size() > 1) {
			bDeleted = false;
		}
		else { 
			m_nSumBytes -= it2->size;

			it2tmp = it2;
			it2++;

			m_sortedbysize.erase(it2tmp);
			bDeleted = true;

			m_nSumFiles--;
			m_nSumSizes--;
		}
	}

	if(!m_quiet) {
		wxLogMessage(_T("        %") wxLongLongFmtSpec _T("u files have to be compared. \n"), 
			m_nSumFiles.GetValue());
	}
}

bool DuplicateFilesFinder::IsSystemProtectedDir(const wxString &dirPath)
{
	wxString lower = dirPath.Lower();
	lower.Replace(_T("/"), _T("\\"));
	while(lower.EndsWith(_T("\\"))) {
		lower = lower.Left(lower.Length() - 1);
	}

	// Protect root drive letters (e.g. "c:", "d:")
	if(lower.Length() <= 3 && lower.Find(_T(':')) != wxNOT_FOUND) {
		return true;
	}

	// Strictly protect Windows critical operating system directories
	if(lower.Find(_T("\\windows")) != wxNOT_FOUND ||
	   lower.Find(_T("\\system32")) != wxNOT_FOUND ||
	   lower.Find(_T("\\syswow64")) != wxNOT_FOUND ||
	   lower.Find(_T("\\winsxs")) != wxNOT_FOUND ||
	   lower.Find(_T("\\program files")) != wxNOT_FOUND ||
	   lower.Find(_T("\\program files (x86)")) != wxNOT_FOUND ||
	   lower.Find(_T("\\programdata")) != wxNOT_FOUND ||
	   lower.Find(_T("\\$recycle.bin")) != wxNOT_FOUND ||
	   lower.Find(_T("\\system volume information")) != wxNOT_FOUND ||
	   lower.Find(_T("\\recovery")) != wxNOT_FOUND ||
	   lower.Find(_T("\\$winreagent")) != wxNOT_FOUND ||
	   lower.Find(_T("\\boot")) != wxNOT_FOUND ||
	   lower.Find(_T("\\appdata\\local\\microsoft")) != wxNOT_FOUND ||
	   lower.Find(_T("\\appdata\\roaming\\microsoft")) != wxNOT_FOUND) {
		return true;
	}

	return false;
}

bool DuplicateFilesFinder::DeleteEmptyDir(const wxString &dirPath)
{
	if(m_protectSystemDirs && IsSystemProtectedDir(dirPath)) {
		return false;
	}

	if(!wxFileName::DirExists(dirPath)) {
		return false;
	}

	wxDir dir(dirPath);
	if(dir.IsOpened()) {
		if(dir.HasFiles() || dir.HasSubDirs()) {
			return false;
		}
	}

	return wxRmdir(dirPath);
}

void DuplicateFilesFinder::FindEmptyDirectories()
{
	m_emptyDirs.clear();

	for(list<SearchPathInfo>::iterator itPath = m_paths.begin(); itPath != m_paths.end(); ++itPath) {
		if(!wxFileName::DirExists(itPath->path)) {
			continue;
		}

		list<wxString> queue;
		queue.push_back(itPath->path);
		list<wxString> allDirs;

		while(!queue.empty()) {
			wxString cur = queue.front();
			queue.pop_front();

			if(m_protectSystemDirs && IsSystemProtectedDir(cur)) {
				continue;
			}

			wxDir dir(cur);
			if(!dir.IsOpened()) {
				continue;
			}

			wxString sub;
			bool bCont = dir.GetFirst(&sub, wxEmptyString, wxDIR_DIRS | (itPath->bHidden ? wxDIR_HIDDEN : 0));
			while(bCont) {
				wxString subPath = cur;
				if(!subPath.EndsWith(_T("\\")) && !subPath.EndsWith(_T("/"))) {
					subPath += (GetPathSepChar() == _T(':') ? _T('/') : _T('\\'));
				}
				subPath += sub;

				if(!(m_protectSystemDirs && IsSystemProtectedDir(subPath))) {
					queue.push_back(subPath);
					allDirs.push_back(subPath);
				}

				bCont = dir.GetNext(&sub);
			}

			if(YieldAndTestAbort()) {
				return;
			}
		}

		// Check directories bottom-up
		for(list<wxString>::reverse_iterator rit = allDirs.rbegin(); rit != allDirs.rend(); ++rit) {
			wxDir d(*rit);
			if(d.IsOpened()) {
				if(!d.HasFiles() && !d.HasSubDirs()) {
					EmptyDirInfo info;
					info.path = *rit;
					info.isSystemProtected = IsSystemProtectedDir(*rit);
					info.modifiedTime = 0;
					m_emptyDirs.push_back(info);
				}
			}
		}
	}
}

list<CommonFolderSuggestion> DuplicateFilesFinder::AnalyzeCommonFolders() const
{
	list<CommonFolderSuggestion> result;
	map<wxString, set<wxString> > folderFiles;
	map<wxString, wxULongLong> folderWasted;

	for(list<DuplicatesGroup>::const_iterator it = m_duplicates.begin(); it != m_duplicates.end(); ++it) {
		if(it->files.size() > 1) {
			for(list<File>::const_iterator f = it->files.begin(); f != it->files.end(); ++f) {
				wxFileName fn(f->GetName());
				wxString dir = fn.GetPath();
				folderFiles[dir].insert(fn.GetFullName());
				folderWasted[dir] += it->size;
			}
		}
	}

	for(map<wxString, set<wxString> >::iterator itA = folderFiles.begin(); itA != folderFiles.end(); ++itA) {
		wxString dirA = itA->first;
		const set<wxString> &filesA = itA->second;

		for(map<wxString, set<wxString> >::iterator itB = folderFiles.begin(); itB != folderFiles.end(); ++itB) {
			if(itA == itB) continue;
			wxString dirB = itB->first;
			const set<wxString> &filesB = itB->second;

			int matches = 0;
			for(set<wxString>::const_iterator s = filesA.begin(); s != filesA.end(); ++s) {
				if(filesB.find(*s) != filesB.end()) {
					matches++;
				}
			}

			if(matches > 0 && (matches == (int)filesA.size() || matches >= 3)) {
				CommonFolderSuggestion sug;
				sug.folderPath = dirA;
				sug.primaryFolderPath = dirB;
				sug.duplicateFilesCount = matches;
				sug.wastedBytes = folderWasted[dirA];
				sug.isCompleteDuplicateFolder = (matches == (int)filesA.size());

				if(sug.isCompleteDuplicateFolder) {
					sug.recommendation = _T("Todos os arquivos duplicados desta pasta estao presentes em '") + dirB + 
						_T("'. Sugere-se a exclusao dos duplicados desta pasta.");
				} else {
					sug.recommendation = wxString::Format(_T("%d arquivos duplicados compartilhados com '%s'."), matches, dirB.c_str());
				}

				result.push_back(sug);
				break;
			}
		}
	}

	return result;
}

