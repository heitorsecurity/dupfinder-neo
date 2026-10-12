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



inline bool	operator ==(const LARGE_INTEGER &a, const LARGE_INTEGER &b)
{
	return (a.QuadPart == b.QuadPart);
}

inline bool operator != (const LARGE_INTEGER &a, const LARGE_INTEGER &b)
{
	return (a.QuadPart != b.QuadPart);
}

inline bool	operator <=(const LARGE_INTEGER &a, const LARGE_INTEGER &b)
{
	return (a.QuadPart <= b.QuadPart);
}

inline bool	operator >=(const LARGE_INTEGER &a, const LARGE_INTEGER &b)
{
	return (a.QuadPart >= b.QuadPart);
}

inline bool	operator <(const LARGE_INTEGER &a, const LARGE_INTEGER &b)
{
	return (a.QuadPart < b.QuadPart);
}

inline bool	operator >(const LARGE_INTEGER &a, const LARGE_INTEGER &b)
{
	return (a.QuadPart > b.QuadPart);
}

inline bool	operator <(const long &a, const LARGE_INTEGER &b)
{
	return (a < b.QuadPart);
}

inline bool	operator >(const long &a, const LARGE_INTEGER &b)
{
	return (a > b.QuadPart);
}

inline bool	operator <=(const long &a, const LARGE_INTEGER &b)
{
	return (a <= b.QuadPart);
}

inline bool	operator >=(const long &a, const LARGE_INTEGER &b)
{
	return (a >= b.QuadPart);
}

inline bool	operator <(const LARGE_INTEGER &a, const long &b)
{
	return (a.QuadPart < b);
}

inline bool	operator >(const LARGE_INTEGER &a, const long &b)
{
	return (a.QuadPart > b);
}

inline bool	operator <=(const LARGE_INTEGER &a, const long &b)
{
	return (a.QuadPart <= b);
}

inline bool	operator >=(const LARGE_INTEGER &a, const long &b)
{
	return (a.QuadPart >= b);
}

inline LARGE_INTEGER operator +(const LARGE_INTEGER &a, const LARGE_INTEGER &b)
{
	LARGE_INTEGER c;
	c.QuadPart = a.QuadPart + b.QuadPart;
	return c;
}

inline LARGE_INTEGER operator -(const LARGE_INTEGER &a, const LARGE_INTEGER &b)
{
	LARGE_INTEGER c;
	c.QuadPart = a.QuadPart - b.QuadPart;
	return c;
}

inline LARGE_INTEGER operator /(const LARGE_INTEGER &a, const LARGE_INTEGER &b)
{
	LARGE_INTEGER c;
	c.QuadPart = a.QuadPart / b.QuadPart;
	return c;
}

inline LARGE_INTEGER operator *(const LARGE_INTEGER &a, const LARGE_INTEGER &b)
{
	LARGE_INTEGER c;
	c.QuadPart = a.QuadPart * b.QuadPart;
	return c;
}
