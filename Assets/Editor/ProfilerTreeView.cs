#if UNITY_5_6_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor.TreeViewExamples;


namespace UnityEditor.MemoryProfiler2
{

	internal class ProfilerTreeView : TreeViewWithTreeModel<MemoryElement>
	{
		const float kRowHeights = 20f;
		const float kToggleWidth = 18f;
		public bool showControls { get; set; }

		public System.Action<int> doubleClickedCallback { get; set; }

		/*static Texture2D[] s_TestIcons =
		{
			EditorGUIUtility.FindTexture ("Folder Icon"),
			EditorGUIUtility.FindTexture ("AudioSource Icon"),
			EditorGUIUtility.FindTexture ("Camera Icon"),
			EditorGUIUtility.FindTexture ("Windzone Icon"),
			EditorGUIUtility.FindTexture ("GameObject Icon")

		};*/

		// All columns
		// Type, ClassName, InstanceID, Size, Name
		enum MyColumns
		{
			Type,
			ClassName,
			InstanceID,
			Size,
			Name
		}

		public enum SortOption
		{
			Type,
			ClassName,
			InstanceID,
			Size,
			Name
		}

		// Sort options per column
		SortOption[] m_SortOptions = 
		{
			SortOption.Type, 
			SortOption.ClassName, 
			SortOption.InstanceID, 
			SortOption.Size, 
			SortOption.Name
		};

		public static void TreeToList (TreeViewItem root, IList<TreeViewItem> result)
		{
			if (root == null)
				throw new NullReferenceException("root");
			if (result == null)
				throw new NullReferenceException("result");

			result.Clear();

			if (root.children == null)
				return;

			Stack<TreeViewItem> stack = new Stack<TreeViewItem>();
			for (int i = root.children.Count - 1; i >= 0; i--)
				stack.Push(root.children[i]);

			while (stack.Count > 0)
			{
				TreeViewItem current = stack.Pop();
				result.Add(current);

				if (current.hasChildren && current.children[0] != null)
				{
					for (int i = current.children.Count - 1; i >= 0; i--)
					{
						stack.Push(current.children[i]);
					}
				}
			}
		}

		protected override void DoubleClickedItem(int id)
		{
			if (doubleClickedCallback != null)
				doubleClickedCallback (id);
		}

		public ProfilerTreeView (TreeViewState state, MultiColumnHeader multicolumnHeader, TreeModel<MemoryElement> model) : base (state, multicolumnHeader, model)
		{
			Assert.AreEqual(m_SortOptions.Length , Enum.GetValues(typeof(MyColumns)).Length, "Ensure number of sort options are in sync with number of MyColumns enum values");

			// Custom setup
			rowHeight = kRowHeights;
			columnIndexForTreeFoldouts = 2;
			showAlternatingRowBackgrounds = true;
			showBorder = true;
			customFoldoutYOffset = (kRowHeights - EditorGUIUtility.singleLineHeight) * 0.5f; // center foldout in the row since we also center content. See RowGUI
			extraSpaceBeforeIconAndLabel = kToggleWidth;
			multicolumnHeader.sortingChanged += OnSortingChanged;

			Reload();
		}


		// Note we We only build the visible rows, only the backend has the full tree information. 
		// The treeview only creates info for the row list.
		protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
		{
			var rows = base.BuildRows (root);
			SortIfNeeded (root, rows);
			return rows;
		}

		void OnSortingChanged (MultiColumnHeader multiColumnHeader)
		{
			SortIfNeeded (rootItem, GetRows());
		}

		void SortIfNeeded (TreeViewItem root, IList<TreeViewItem> rows)
		{
			if (multiColumnHeader.sortedColumnIndex == -1)
			{
				return; // No column to sort for (just use the order the data are in)
			}

			SortOption sortOption = m_SortOptions[multiColumnHeader.sortedColumnIndex];
			bool sortAscending = multiColumnHeader.IsSortedAscending(multiColumnHeader.sortedColumnIndex);

			// Sort the roots of the existing tree items
			SortVisibleRoots(sortOption, sortAscending);
			TreeToList(root, rows);
			Repaint();
		}

		public void SortVisibleRoots (SortOption sortOption, bool sortedAscending)
		{
			int direction = sortedAscending ? 1 : -1;
			if (!rootItem.hasChildren)
				return;
			rootItem.children.Sort((x, y) => Compare(x, y, sortOption) * direction);
		}

		public int Compare (TreeViewItem x, TreeViewItem y, SortOption sortOption)
		{
			if (x == y) return 0;
			if (x == null) return -1;
			if (y == null) return 1;

			MemoryElement memoryElementX = ((TreeViewItem<MemoryElement>)x).data;
			MemoryElement memoryElementY = ((TreeViewItem<MemoryElement>)y).data;
			switch (sortOption)
			{
			case SortOption.Name:
				return EditorUtility.NaturalCompare(memoryElementX.name, memoryElementY.name);
			case SortOption.Type:
				return EditorUtility.NaturalCompare(memoryElementX.type, memoryElementY.type);
				//memoryElementX.floatValue1.CompareTo(memoryElementY.floatValue1);
			case SortOption.InstanceID:
				return memoryElementX.id.CompareTo(memoryElementY.id);
			case SortOption.Size:
				return memoryElementX.size.CompareTo(memoryElementY.size);
			case SortOption.ClassName:
				return EditorUtility.NaturalCompare(memoryElementX.className, memoryElementY.className);
				//memoryElementX.floatValue3.CompareTo(memoryElementY.floatValue3);
			}
			return 0;
		}

		/*int GetIcon1Index(TreeViewItem<MyTreeElement> item)
		{
			return (int)(Mathf.Min(0.99f, item.data.floatValue1) * s_TestIcons.Length);
		}

		int GetIcon2Index (TreeViewItem<MyTreeElement> item)
		{
			return Mathf.Min(item.data.text.Length, s_TestIcons.Length-1);
		}*/

		protected override void RowGUI (RowGUIArgs args)
		{
			var item = (TreeViewItem<MemoryElement>) args.item;

			for (int i = 0; i < args.GetNumVisibleColumns (); ++i)
			{
				CellGUI(args.GetCellRect(i), item, (MyColumns)args.GetColumn(i), ref args);
			}
		}

		void CellGUI (Rect cellRect, TreeViewItem<MemoryElement> item, MyColumns column, ref RowGUIArgs args)
		{
			// Center cell rect vertically (makes it easier to place controls, icons etc in the cells)
			CenterRectUsingSingleLineHeight(ref cellRect);

			switch (column)
			{
			case MyColumns.Type:
				DefaultGUI.Label (cellRect, item.data.type, args.selected, args.focused);
				break;
			case MyColumns.ClassName:
				DefaultGUI.Label (cellRect, item.data.className, args.selected, args.focused);
				break;

			case MyColumns.InstanceID:
				DefaultGUI.LabelRightAligned (cellRect, item.data.id.ToString(), args.selected, args.focused);
				break;
			case MyColumns.Size:
				DefaultGUI.LabelRightAligned (cellRect, item.data.size.ToString("f2"), args.selected, args.focused);
				break;
			case MyColumns.Name:
				DefaultGUI.Label (cellRect, item.data.name, args.selected, args.focused);
				break;
			}
		}

		// Rename
		//--------

		/*protected override bool CanRename(TreeViewItem item)
		{
			// Only allow rename if we can show the rename overlay with a certain width (label might be clipped by other columns)
			Rect renameRect = GetRenameRect (treeViewRect, 0, item);
			return renameRect.width > 30;
		}

		protected override void RenameEnded(RenameEndedArgs args)
		{
			// Set the backend name and reload the tree to reflect the new model
			if (args.acceptedRename)
			{
				var element = treeModel.Find(args.itemID);
				element.name = args.newName;
				Reload();
			}
		}

		protected override Rect GetRenameRect (Rect rowRect, int row, TreeViewItem item)
		{
			Rect cellRect = GetCellRectForTreeFoldouts (rowRect);
			CenterRectUsingSingleLineHeight(ref cellRect);
			return base.GetRenameRect (cellRect, row, item);
		}*/

		// Misc
		//--------

		protected override bool CanMultiSelect (TreeViewItem item)
		{
			return true;
		}

		public static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState(float treeViewWidth)
		{
			var columns = new[] 
			{
				new MultiColumnHeaderState.Column 
				{
					headerContent = new GUIContent("Type", "The type (Native|Managed|Static)"),
					headerTextAlignment = TextAlignment.Center,
					sortedAscending = true,
					sortingArrowAlignment = TextAlignment.Left,
					width = 120, 
					minWidth = 120,
					//maxWidth = 120,
					autoResize = true,
					allowToggleVisibility = true
				},
				new MultiColumnHeaderState.Column 
				{
					headerContent = new GUIContent("ClassName", "The Class Name of the resource"),
					headerTextAlignment = TextAlignment.Center,
					sortedAscending = true,
					sortingArrowAlignment = TextAlignment.Left,
					width = 100, // adjusted below
					minWidth = 100,
					maxWidth = 100,
					autoResize = false,
					allowToggleVisibility = true
				},
				new MultiColumnHeaderState.Column 
				{
					headerContent = new GUIContent("InstanceID", "The Instance ID, it should be unique"),
					headerTextAlignment = TextAlignment.Center,
					sortedAscending = true,
					sortingArrowAlignment = TextAlignment.Right,
					width = 120, // adjusted below
					minWidth = 100,
					maxWidth = 120,
					autoResize = false,
					allowToggleVisibility = true
				},
				new MultiColumnHeaderState.Column 
				{
					headerContent = new GUIContent("Size (bytes)", "The size in bytes in memory."),
					headerTextAlignment = TextAlignment.Center,
					sortedAscending = true,
					sortingArrowAlignment = TextAlignment.Left,
					width = 60,
					minWidth = 60,
					autoResize = false,
					allowToggleVisibility = true
				},
				new MultiColumnHeaderState.Column 
				{
					headerContent = new GUIContent("Name", "Resource name (if exists!)"),
					headerTextAlignment = TextAlignment.Center,
					sortedAscending = true,
					sortingArrowAlignment = TextAlignment.Left,
					width = 60,
					minWidth = 60,
					autoResize = true
				}
			};

			Assert.AreEqual(columns.Length, Enum.GetValues(typeof(MyColumns)).Length, "Number of columns should match number of enum values: You probably forgot to update one of them.");

			// Set name column width (flexible)
			int nameColumn = (int)MyColumns.Name;
			columns[nameColumn].width = treeViewWidth - GUI.skin.verticalScrollbar.fixedWidth;
			for (int i = 0; i < columns.Length; ++i)
				if (i != nameColumn)
					columns[nameColumn].width -= columns[i].width;

			if (columns[nameColumn].width < 60f)
				columns[nameColumn].width = 60f;

			var state =  new MultiColumnHeaderState(columns);
			return state;
		}
	}
}
#endif