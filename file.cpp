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
#include "file.h"
#include "dbl.h"

const size_t File::MAXCACHESIZE = BASEBUFSIZE << 8;
const size_t File::BUFSIZE = BASEBUFSIZE << 7;

File::File(){
	init();
}

File::File(const wxString &_name) {
	init();
	SetName(_name);
}

void File::SetName(const wxString & _name) { 
	assert(data);
	data->name = _name;

	// wxFileName normfn(_name);
	// normfn.Normalize(wxPATH_NORM_ALL & ~wxPATH_NORM_LONG); // NORM_LONG is slow
	// normfn.Normalize(wxPATH_NORM_CASE);
	// data->normname = normfn.GetFullPath();
}

void File::init() {
	data = new filedata;
	data->extdata = NULL;
	data->rcount = 1;
	assert(File::MAXCACHESIZE % File::BUFSIZE == 0);
}

File::~File() {
	ReleaseData();
}

File::File(const File & rhs) {
	data = rhs.data;
	data->rcount++;
}

File & File::operator = (const File &rhs) {
	if(&rhs != this) {
		ReleaseData();
		data = rhs.data;
		data->rcount++;
	}
	return *this;
}

void File::ReleaseData() {
	assert(data);
	assert(data->rcount >= 1);

	data->rcount--;

	if(data->rcount == 0) {
		// delete object

		ReleaseExtData();

		delete data;
	}
}

// get the amount of "Sectors" (of length BUFSIZE) a file of length value needs
size_t File::RoundUpToBufSize(size_t value) {
	return value % BUFSIZE == 0 ? value : (value / BUFSIZE + 1) *BUFSIZE;
}

bool File::Open(const wxULongLong &size) {
	bool bResult;

	if(!data->extdata) {
		data->extdata = new filedata::extfiledata;

		size_t maxcachesize = RoundUpToBufSize(min(size, wxULongLong(File::MAXCACHESIZE)).ToULong());
		data->extdata->cache= new char [maxcachesize];
		data->extdata->maxcachesize = maxcachesize;
		data->extdata->cachesize = 0;
		data->extdata->pos = 0;
		data->extdata->size = size;
		data->extdata->bChangeToDiskRead = true;
		
		bResult = data->extdata->file.Open(data->name);
	}
	else {
		bResult = data->extdata->file.IsOpened();
	}
	return bResult;
}


bool File::Read(char **buffer, size_t &ncount) {
	assert(data->extdata);

	assert(ncount == File::BUFSIZE);

	if(!data->extdata->file.IsOpened()) {
		return false;
	}

	if(wxULongLong(data->extdata->pos) < data->extdata->cachesize) {
		ncount = min(data->extdata->cachesize - data->extdata->pos, File::BUFSIZE);
		*buffer = data->extdata->cache+data->extdata->pos;
	}
	else { /* data->extdata->pos >= data->extdata->cachesize */
		
		bool bWriteToCache = wxULongLong(data->extdata->pos) < data->extdata->maxcachesize;
		if(bWriteToCache) {
			*buffer = data->extdata->cache + data->extdata->pos;
			assert(data->extdata->maxcachesize - data->extdata->pos >= ncount);
		}

		if(data->extdata->bChangeToDiskRead) {
			data->extdata->pos = data->extdata->file.Seek(data->extdata->pos);
			if(data->extdata->pos == wxInvalidOffset) {
				return false;
			}
			data->extdata->bChangeToDiskRead = false;
		}
		ncount = data->extdata->file.Read(*buffer, ncount);

		if(ncount == (size_t)wxInvalidOffset) {
			return false;
		}

		if(bWriteToCache) {
			data->extdata->cachesize += ncount;
		}
	}

	data->extdata->pos += ncount;

	return true;
}

bool File::Restart() {
	assert(data->extdata);

	if(!data->extdata->file.IsOpened()) {
		return false;
	}

	wxFileOffset new_pos = data->extdata->file.Seek(0);

	if(new_pos != wxInvalidOffset) {
		data->extdata->pos = new_pos;
	}

	data->extdata->bChangeToDiskRead = true;

	return new_pos != wxInvalidOffset;
}

void File::Close() {
	ReleaseExtData();
}

void File::ReleaseExtData()
{
	if(data->extdata) {
		if(data->extdata->file.IsOpened()) {
			data->extdata->file.Close();
		}
		delete [] data->extdata->cache;
		delete data->extdata;
	}
	data->extdata = NULL;
}






