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



inline bool	operator ==(const ULARGE_INTEGER &a, const ULARGE_INTEGER &b)
{
	return (a.QuadPart == b.QuadPart);
}

inline bool operator != (const ULARGE_INTEGER &a, const ULARGE_INTEGER &b)
{
	return (a.QuadPart != b.QuadPart);
}

inline bool	operator <=(const ULARGE_INTEGER &a, const ULARGE_INTEGER &b)
{
	return (a.QuadPart <= b.QuadPart);
}

inline bool	operator >=(const ULARGE_INTEGER &a, const ULARGE_INTEGER &b)
{
	return (a.QuadPart >= b.QuadPart);
}

inline bool	operator <(const ULARGE_INTEGER &a, const ULARGE_INTEGER &b)
{
	return (a.QuadPart < b.QuadPart);
}

inline bool	operator >(const ULARGE_INTEGER &a, const ULARGE_INTEGER &b)
{
	return (a.QuadPart > b.QuadPart);
}

inline bool	operator <(const DWORD &a, const ULARGE_INTEGER &b)
{
	return (a < b.QuadPart);
}

inline bool	operator >(const DWORD &a, const ULARGE_INTEGER &b)
{
	return (a > b.QuadPart);
}

inline bool	operator <=(const DWORD &a, const ULARGE_INTEGER &b)
{
	return (a <= b.QuadPart);
}

inline bool	operator >=(const DWORD &a, const ULARGE_INTEGER &b)
{
	return (a >= b.QuadPart);
}

inline bool	operator <(const ULARGE_INTEGER &a, const DWORD &b)
{
	return (a.QuadPart < b);
}

inline bool	operator >(const ULARGE_INTEGER &a, const DWORD &b)
{
	return (a.QuadPart > b);
}

inline bool	operator <=(const ULARGE_INTEGER &a, const DWORD &b)
{
	return (a.QuadPart <= b);
}

inline bool	operator >=(const ULARGE_INTEGER &a, const DWORD &b)
{
	return (a.QuadPart >= b);
}

inline ULARGE_INTEGER operator +(const ULARGE_INTEGER &a, const ULARGE_INTEGER &b)
{
	ULARGE_INTEGER c;
	c.QuadPart = a.QuadPart + b.QuadPart;
	return c;
}

inline ULARGE_INTEGER operator -(const ULARGE_INTEGER &a, const ULARGE_INTEGER &b)
{
	ULARGE_INTEGER c;
	c.QuadPart = a.QuadPart - b.QuadPart;
	return c;
}

inline ULARGE_INTEGER operator /(const ULARGE_INTEGER &a, const ULARGE_INTEGER &b)
{
	ULARGE_INTEGER c;
	c.QuadPart = a.QuadPart / b.QuadPart;
	return c;
}

inline ULARGE_INTEGER operator *(const ULARGE_INTEGER &a, const ULARGE_INTEGER &b)
{
	ULARGE_INTEGER c;
	c.QuadPart = a.QuadPart * b.QuadPart;
	return c;
}


