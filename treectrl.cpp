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
#include "treectrl.h"

#ifdef DUPF_USE_TREECTRL

/********** Tree ctrl implementation, simple wrapper ******************/

TreeCtrl::TreeCtrl(wxWindow *parent, wxWindowID id, const wxPoint &pos, 
	const wxSize &size) {
	control = new wxTreeCtrl(parent, id, pos, size, 
		wxBORDER_SUNKEN /*| wxTR_MULTIPLE */ | wxTR_HIDE_ROOT 
			| wxTR_LINES_AT_ROOT | wxTR_HAS_BUTTONS );
}

TreeCtrl::~TreeCtrl() {
	delete control;
}

void TreeCtrl::Freeze() {
	// don't freeze tree ctrl
}

void TreeCtrl::Thaw() {
	// see TreeCtrl::Freeze
}


TreeItemId TreeCtrl::AddRoot(const wxString &text, TreeItemData *data) {
	return control->AddRoot(text, -1, -1, data);
}

void TreeCtrl::SetItemData(const TreeItemId &id, TreeItemData *data) {
	control->SetItemData(id, data);
}

TreeItemData *TreeCtrl::GetItemData(const TreeItemId &id) {
	return control->GetItemData(id);
}

TreeItemId TreeCtrl::AppendItem(const TreeItemId &id, const wxString &text, TreeItemData *data) {
	return control->AppendItem(id, text, -1, -1, data);
}

TreeItemId TreeCtrl::GetRootItem(void) {
	return control->GetRootItem();
}

void TreeCtrl::SetItemBackgroundColour(const TreeItemId &id, const wxColour &data) {
	control->SetItemBackgroundColour(id, data);
}

unsigned int TreeCtrl::GetChildrenCount(const TreeItemId &id, bool rec) const {
	return control->GetChildrenCount(id, rec);
}

void TreeCtrl::ExpandAllChildren(const TreeItemId &WXUNUSED(id) ) {
	wxLogMessage(_T("ExpandAllChildren with TreeCtrl deactivated (too slow)! "));
	// control->ExpandAllChildren(id);
}

void TreeCtrl::Expand(const TreeItemId &id) {
	control->Expand(id);
}

void TreeCtrl::Collapse(const TreeItemId &id) {
	control->Collapse(id);
}

wxTreeItemId TreeCtrl::GetFirstChild(const TreeItemId &id, TreeItemIdValue &cookie) {
	return control->GetFirstChild(id, cookie);
}

wxTreeItemId TreeCtrl::GetNextChild(const TreeItemId &id, TreeItemIdValue &cookie) {
	return control->GetNextChild(id, cookie);
}

wxString TreeCtrl::GetItemText(const TreeItemId &id) {
	return control->GetItemText(id);
}

void TreeCtrl::GetSelections(ArrayTreeItemIds &sel) {
	control->GetSelections(sel);
}

void TreeCtrl::Delete(const TreeItemId &id) {
	return control->Delete(id);
}

void TreeCtrl::DeleteAllItems() {
	return control->DeleteAllItems();
}

TreeItemId TreeCtrl::GetItemParent(const TreeItemId &id)  {
	return control->GetItemParent(id);
}

#else /* defined(DUPF_USE_TREECTRL) */

/********* List view implementation, emulation ************************/

void TreeCtrl::Freeze() {
	control->Freeze();
}

void TreeCtrl::Thaw() {
	control->Thaw();

}


#endif /* defined(DUPF_USE_TREECTRL) */

