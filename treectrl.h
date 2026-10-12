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

// Emulator for either wxTreeCtrl (simple wrapper) or wxListView (emulation)

#ifndef __DUPF_TREECTRL_H
#define __DUPF_TREECTRL_H

#include "stdinc.h"


#ifdef DUPF_USE_TREECTRL
	typedef wxTreeItemData TreeItemData;
	typedef wxTreeItemId TreeItemId;
	typedef wxTreeItemIdValue TreeItemIdValue;
	typedef wxArrayTreeItemIds ArrayTreeItemIds;

	#define EVT_DUPF_TREE_ITEM_ACTIVATED	EVT_TREE_ITEM_ACTIVATED
	#define EVT_DUPF_TREE_ITEM_RIGHT_CLICK	EVT_TREE_ITEM_RIGHT_CLICK
	#define EVT_DUPF_TREE_KEY_DOWN	EVT_TREE_KEY_DOWN
#else /*!DUPF_USE_TREECTRL */
	typedef class {

	} TreeItemData;

	/*
	typedef class/struct {

	} TreeItemId ;

	typedef class/struct {
	
	} TreeItemIdValue ;
	*/

	#define EVT_DUPF_TREE_ITEM_ACTIVATED	EVT_LIST_ITEM_ACTIVATED
	#define EVT_DUPF_TREE_ITEM_RIGHT_CLICK	EVT_LIST_ITEM_RIGHT_CLICK
	#define EVT_DUPF_TREE_KEY_DOWN	EVT_LIST_ITEM_KEY_DOWN
#endif /*DUPF_USE_TREECTRL */

class TreeCtrl
{
public:
	TreeCtrl(wxWindow *, wxWindowID, const wxPoint&, const wxSize&);
	~TreeCtrl();

	void Freeze();
	void Thaw();

	TreeItemId AddRoot(const wxString &, TreeItemData * =NULL);
	TreeItemId AppendItem(const TreeItemId &, const wxString &, TreeItemData * =NULL);

	TreeItemId GetRootItem(void);

	void SetItemData(const TreeItemId &, TreeItemData *);
	TreeItemData *GetItemData(const TreeItemId &);

	void SetItemBackgroundColour(const TreeItemId &, const wxColour &);

	unsigned int GetChildrenCount(const TreeItemId &, bool =true) const;

	void ExpandAllChildren(const TreeItemId &);
	void Expand(const TreeItemId &);
	void Collapse(const TreeItemId &);

	wxTreeItemId GetFirstChild(const TreeItemId &, TreeItemIdValue &);
	wxTreeItemId GetNextChild(const TreeItemId &, TreeItemIdValue &);
	wxString GetItemText(const TreeItemId &);

	void PopupMenu(wxMenu *menu) { control->PopupMenu(menu); }

	void GetSelections(ArrayTreeItemIds &);

	void Delete(const TreeItemId &);
	void DeleteAllItems();

	TreeItemId GetItemParent(const TreeItemId &);

	wxWindow *GetControl() { return control; }

private:

	// the tree/list ctrl
#ifdef DUPF_USE_TREECTRL
	wxTreeCtrl *control;
#else
	wxListView *control;
#endif

};

#endif /* defined  (__DUPF_TREECTRL_H) */
