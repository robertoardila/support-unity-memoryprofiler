#if UNITY_5_6
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEditor.TreeViewExamples;

namespace UnityEditor.MemoryProfiler2
{

	internal class TreeViewItem<T> : TreeViewItem where T : MemoryElement
	{
		public T data { get; set; }

		public TreeViewItem (int id, int depth, string displayName, T data) : base (id, depth, displayName)
		{
			this.data = data;
		}
	}

	internal class TreeViewWithTreeModel<T> : TreeView where T : MemoryElement
	{
		TreeModel<T> m_TreeModel;
		readonly List<TreeViewItem> m_Rows = new List<TreeViewItem>(100);
		public event Action treeChanged;

		public TreeModel<T> treeModel { get { return m_TreeModel; } }
		public event Action<IList<TreeViewItem>>  beforeDroppingDraggedItems;


		public TreeViewWithTreeModel (TreeViewState state, TreeModel<T> model) : base (state)
		{
			Init (model);
		}

		public TreeViewWithTreeModel (TreeViewState state, MultiColumnHeader multiColumnHeader, TreeModel<T> model)
			: base(state, multiColumnHeader)
		{
			Init (model);
		}

		void Init (TreeModel<T> model)
		{
			m_TreeModel = model;
			m_TreeModel.modelChanged += ModelChanged;
		}

		void ModelChanged ()
		{
			if (treeChanged != null)
				treeChanged ();

			Reload ();
		}

		protected override TreeViewItem BuildRoot()
		{
			int depthForHiddenRoot = -1;
			return new TreeViewItem(m_TreeModel.root.id, depthForHiddenRoot, m_TreeModel.root.name);
		}

		protected override IList<TreeViewItem> BuildRows (TreeViewItem root)
		{
			if (m_TreeModel.root == null)
			{
				Debug.LogError ("tree model root is null. did you call SetData()?");
			}

			m_Rows.Clear ();
			if (!string.IsNullOrEmpty(searchString))
			{
				Search (m_TreeModel.root, searchString, m_Rows);
			}
			else
			{
				if (m_TreeModel.root.hasChildren)
					AddChildrenRecursive(m_TreeModel.root, 0, m_Rows);
			}

			// We still need to setup the child parent information for the rows since this 
			// information is used by the TreeView internal logic (navigation, dragging etc)
			SetupParentsAndChildrenFromDepths (root, m_Rows);

			return m_Rows;
		}

		void AddChildrenRecursive (T parent, int depth, IList<TreeViewItem> newRows)
		{
			foreach (T child in parent.children)
			{
				var item = new TreeViewItem<T>(child.id, depth, child.name, child);
				newRows.Add(item);

				if (child.hasChildren)
				{
					if (IsExpanded(child.id))
					{
						AddChildrenRecursive (child, depth + 1, newRows);
					}
					else
					{
						item.children = CreateChildListForCollapsedParent();
					}
				}
			}
		}

		void Search(T searchFromThis, string search, List<TreeViewItem> result)
		{
			if (string.IsNullOrEmpty(search))
				throw new ArgumentException("Invalid search: cannot be null or empty", "search");

			const int kItemDepth = 0; // tree is flattened when searching

			if (result == null)
				result = new List<TreeViewItem> ();
			
			Stack<T> stack = new Stack<T>();
			foreach (var element in searchFromThis.children)
				stack.Push((T)element);

			if (search.IndexOf (":") != -1) 
			{
				search = search.Replace (": ", ":");
				string[] strings = search.Split (' ');
				for (int i = 0; i < strings.Length; i++) 
				{
					if (strings [i].IndexOf (":") == -1) 
					{
						stack = SearchByName (strings [i], stack);
						continue;
					}

					string keyValue = strings [i].Substring (2);
					if (keyValue == string.Empty)
						continue;
					
					// Resource Name (Asset Name)
					if (strings [i].IndexOf ("n:") != -1) 
					{
						stack = SearchByName (keyValue, stack);
					}
					// InstanceID
					else if (strings [i].IndexOf ("i:") != -1) 
					{
						stack = SearchByInstanceId (keyValue, stack);
					}
					// ClassName (Texture2D)
					else if (strings [i].IndexOf ("c:") != -1) 
					{
						stack = SearchByClassName (keyValue, stack);
					}
					// Type (NativeUnityObject)
					else if (strings [i].IndexOf ("t:") != -1) 
					{
						stack = SearchByType (keyValue, stack);
					}
					// Size
					else if (strings [i].IndexOf ("s:") != -1) 
					{
						stack = SearchBySize (keyValue, stack);
					}
				}
			} 
			else 
			{
				stack = SearchByName (search, stack);
			}

			while (stack.Count > 0) 
			{
				T current = stack.Pop ();
				result.Add (new TreeViewItem<T> (current.id, kItemDepth, current.name, current));
			}

			SortSearchResult(result);
		}

		private Stack<T> SearchBySize (string search, Stack<T> stack)
		{
			Stack<T> result = new Stack<T>();
			// 0 = equal, 1 = less_than, 2 = high_than
			int opperator = 0;

			if (search [0] == '<')
				opperator = 1;
			else if (search [0] == '>')
				opperator = 2;

			if (opperator != 0 && search.Length == 1)
				return result;

			Debug.Log ("Number: " + search.Substring (opperator == 0 ? 0 : 1));
			float value = float.Parse (search.Substring ( opperator == 0 ? 0 : 1 ));
			while (stack.Count > 0) 
			{
				T current = stack.Pop ();
				if (opperator == 0 && current.size == value)
					result.Push (current);
				else if (opperator == 1 && current.size <= value)
					result.Push (current);
				else if (opperator == 2 && current.size >= value)
					result.Push (current);
			}

			return result;
		}

		private Stack<T> SearchByType (string search, Stack<T> stack)
		{
			Stack<T> result = new Stack<T>();
			while (stack.Count > 0) 
			{
				T current = stack.Pop ();

				if (current.type.IndexOf (search, StringComparison.OrdinalIgnoreCase) >= 0) 
				{
					result.Push(current);
				}
			}

			return result;
		}

		private Stack<T> SearchByClassName (string search, Stack<T> stack)
		{
			Stack<T> result = new Stack<T>();
			while (stack.Count > 0) 
			{
				T current = stack.Pop ();

				if (current.className.IndexOf (search, StringComparison.OrdinalIgnoreCase) >= 0) 
				{
					result.Push(current);
				}
			}

			return result;
		}

		private Stack<T> SearchByName (string search, Stack<T> stack)
		{
			Stack<T> result = new Stack<T>();
			while (stack.Count > 0) 
			{
				T current = stack.Pop ();

				if (current.name.IndexOf (search, StringComparison.OrdinalIgnoreCase) >= 0) 
				{
					result.Push(current);
				}
			}

			return result;
		}

		private Stack<T> SearchByInstanceId (string search, Stack<T> stack)
		{
			Stack<T> result = new Stack<T>();
			while (stack.Count > 0) 
			{
				T current = stack.Pop ();

				if (current.id.ToString().IndexOf (search, StringComparison.OrdinalIgnoreCase) >= 0) 
				{
					//result.Add ( new TreeViewItem<T> (current.id, 0, current.name, current));
					result.Push ( current );
				}
			}

			return result;
		}

		protected virtual void SortSearchResult (List<TreeViewItem> rows)
		{
			rows.Sort (); // sort by displayName by default, can be overriden for multicolumn solutions
		}

		protected override IList<int> GetAncestors (int id)
		{
			return m_TreeModel.GetAncestors(id);
		}

		protected override IList<int> GetDescendantsThatHaveChildren (int id)
		{
			return m_TreeModel.GetDescendantsThatHaveChildren(id);
		}


		// Dragging
		//-----------

		const string k_GenericDragID = "GenericDragColumnDragging";

		protected override bool CanStartDrag (CanStartDragArgs args)
		{
			return true;
		}

		protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
		{
			if (hasSearch)
				return;

			DragAndDrop.PrepareStartDrag();
			var draggedRows = GetRows().Where(item => args.draggedItemIDs.Contains(item.id)).ToList();
			DragAndDrop.SetGenericData(k_GenericDragID, draggedRows);
			DragAndDrop.objectReferences = new UnityEngine.Object[] { }; // this IS required for dragging to work
			string title = draggedRows.Count == 1 ? draggedRows[0].displayName : "< Multiple >";
			DragAndDrop.StartDrag (title);
		}

		protected override DragAndDropVisualMode HandleDragAndDrop (DragAndDropArgs args)
		{
			// Check if we can handle the current drag data (could be dragged in from other areas/windows in the editor)
			var draggedRows = DragAndDrop.GetGenericData(k_GenericDragID) as List<TreeViewItem>;
			if (draggedRows == null)
				return DragAndDropVisualMode.None;

			// Parent item is null when dragging outside any tree view items.
			switch (args.dragAndDropPosition)
			{
			case DragAndDropPosition.UponItem:
			case DragAndDropPosition.BetweenItems:
				{
					bool validDrag = ValidDrag(args.parentItem, draggedRows);
					if (args.performDrop && validDrag)
					{
						T parentData = ((TreeViewItem<T>)args.parentItem).data;
						OnDropDraggedElementsAtIndex(draggedRows, parentData, args.insertAtIndex == -1 ? 0 : args.insertAtIndex);
					}
					return validDrag ? DragAndDropVisualMode.Move : DragAndDropVisualMode.None;
				}

			case DragAndDropPosition.OutsideItems:
				{
					if (args.performDrop)
						OnDropDraggedElementsAtIndex(draggedRows, m_TreeModel.root, m_TreeModel.root.children.Count);

					return DragAndDropVisualMode.Move;
				}
			default:
				Debug.LogError("Unhandled enum " + args.dragAndDropPosition);
				return DragAndDropVisualMode.None;
			}
		}

		public virtual void OnDropDraggedElementsAtIndex (List<TreeViewItem> draggedRows, T parent, int insertIndex)
		{
			if (beforeDroppingDraggedItems != null)
				beforeDroppingDraggedItems (draggedRows);

			var draggedElements = new List<TreeElement> ();
			foreach (var x in draggedRows)
				draggedElements.Add (((TreeViewItem<T>) x).data);

			var selectedIDs = draggedElements.Select (x => x.id).ToArray();
			m_TreeModel.MoveElements (parent, insertIndex, draggedElements);
			SetSelection(selectedIDs, TreeViewSelectionOptions.RevealAndFrame);
		}


		bool ValidDrag(TreeViewItem parent, List<TreeViewItem> draggedItems)
		{
			TreeViewItem currentParent = parent;
			while (currentParent != null)
			{
				if (draggedItems.Contains(currentParent))
					return false;
				currentParent = currentParent.parent;
			}
			return true;
		}

	}

}
#endif