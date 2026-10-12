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

#ifndef _FileTest_H_123
#define _FileTest_H_123

#define BASEBUFSIZE 512

class FileTest
{
public:
	FileTest();
	FileTest(const wxString &);
	~FileTest();

	FileTest(const FileTest &);
	FileTest & operator = (const FileTest &);

	void SetName(const wxString & _name) { data->name = _name; }
	wxString GetName() const { return data->name; }

	bool Read(char *, size_t &);

	static size_t GetBufSize() { return FileTest::BUFSIZE; }

	bool Open();
	void Close();

	FileTest & operator  =(const File &f ) {
		SetName(f.GetName());
		return *this;
	}

private:
	struct extFileTestdata {
		wxFile FileTest;
	};

	struct FileTestdata
	{
		int rcount;
		extFileTestdata *extdata;
		wxString name;
	};


	void ReleaseData();
	void ReleaseExtData();

	void init();

	static const size_t BUFSIZE;

	FileTestdata *data;

};

#endif
























