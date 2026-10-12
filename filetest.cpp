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
#include "filetest.h"
#include "dbl.h"

const size_t FileTest::BUFSIZE = BASEBUFSIZE << 7;

FileTest::FileTest(){
	init();
}

FileTest::FileTest(const wxString &_name) {
	init();
	data->name = _name;
}

void FileTest::init() {
	data = new FileTestdata;
	data->extdata = NULL;
	data->rcount = 1;
}

FileTest::~FileTest() {
	ReleaseData();
}

FileTest::FileTest(const FileTest & rhs) {
	data = rhs.data;
	data->rcount++;
}

FileTest & FileTest::operator = (const FileTest &rhs) {
	if(&rhs != this) {
		ReleaseData();
		data = rhs.data;
		data->rcount++;
	}
	return *this;
}

void FileTest::ReleaseData() {
	assert(data);
	assert(data->rcount >= 1);

	data->rcount--;

	if(data->rcount == 0) {
		// delete object

		ReleaseExtData();

		delete data;
	}
}

bool FileTest::Open() {
	bool bResult;

	if(!data->extdata) {
		data->extdata = new extFileTestdata;

		bResult = data->extdata->FileTest.Open(data->name);
	}
	else {
		bResult = data->extdata->FileTest.IsOpened();
	}
	return bResult;
}


bool FileTest::Read(char *buffer, size_t &ncount) {
	assert(data->extdata);

	assert(ncount == FileTest::BUFSIZE);

	if(!data->extdata->FileTest.IsOpened()) {
		return false;
	}

	ncount = data->extdata->FileTest.Read(buffer, ncount);

	return ncount != wxInvalidOffset;
}

void FileTest::Close() {
	ReleaseExtData();
	init();
}

void FileTest::ReleaseExtData()
{
	if(data->extdata) {
		if(data->extdata->FileTest.IsOpened()) {
			data->extdata->FileTest.Close();
		}
		delete data->extdata;
	}
}

bool	comparefiles0(File &_f1, File &_f2) {
	bool bResult;
	static char *b1, *b2;
	if(b1 == NULL) { b1 = new char[FileTest::GetBufSize()]; }
	if(b2 == NULL) { b2 = new char[FileTest::GetBufSize()]; }
	size_t BUFSIZE = FileTest::GetBufSize();
	size_t n1, n2;
	FileTest f1, f2;
	f1 = _f1; f2 = _f2;

	if(!f1.Open()) {
		return false;
	}
	if(!f2.Open()) {
		return false;
	}

	while(1) {
		n1 = n2 = BUFSIZE;
		bool br1, br2;

		br1 = f1.Read(b1, n1);
		br2 = f2.Read(b2, n2);

		if(n1 != n2 || !br1 || !br2) {
			bResult = false;
			goto End;
		}

		if(memcmp(b1, b2, n1) != 0) {
			bResult = false;
			goto End;
		}

		if(n1 < BUFSIZE)
			break;

	}

	f1.Close();
	f2.Close();

	bResult = true;

End:	return bResult;
}






