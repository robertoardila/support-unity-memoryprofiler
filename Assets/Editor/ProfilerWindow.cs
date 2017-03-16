using System;
using System.Collections.Generic;
using UnityEditor.Callbacks;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEditor.TreeViewExamples;
using UnityEditor.MemoryProfiler;
using MemoryProfilerWindow;

namespace UnityEditor.MemoryProfiler2
{

	class ProfilerWindow : EditorWindow
	{
		[NonSerialized] bool m_Initialized;
		[SerializeField] TreeViewState m_TreeViewState; // Serialized in the window layout file so it survives assembly reloading
		[SerializeField] MultiColumnHeaderState m_MultiColumnHeaderState;
		ProfilerTreeView m_TreeView;
		TreeModel<MemoryElement> m_TreeModel;
		string m_Status = "Profiling";
		//MyTreeAsset m_MyTreeAsset;

		[NonSerialized]
		UnityEditor.MemoryProfiler.PackedMemorySnapshot _snapshot;
		[SerializeField]
		PackedCrawlerData _packedCrawled;
		[NonSerialized]
		CrawledMemorySnapshot _unpackedCrawl;
        [NonSerialized]
        private ProfilerNodeView m_nodeView;

        [MenuItem("VMemory Tools/Profiler")]
		public static ProfilerWindow GetWindow ()
		{
			var window = GetWindow<ProfilerWindow>();
			window.titleContent = new GUIContent("Profiler");
			window.Focus();
			window.Repaint();
			return window;
		}
        

		/*[OnOpenAsset]
		public static bool OnOpenAsset (int instanceID, int line)
		{
			var myTreeAsset = EditorUtility.InstanceIDToObject (instanceID) as MyTreeAsset;
			if (myTreeAsset != null)
			{
				var window = GetWindow ();
				window.SetTreeAsset(myTreeAsset);
				return true;
			}
			return false; // we did not handle the open
		}*/

		/*void SetTreeAsset (MyTreeAsset myTreeAsset)
		{
			m_MyTreeAsset = myTreeAsset;
			m_Initialized = false;
		}*/

		Rect topToolbarRect
		{
			get { return new Rect (10f, 0f, position.width * .4f, 20f); }
		}

		Rect searchBarRect
		{
			get { return new Rect (10f, 22f, position.width * .4f, 20f); }
		}

		Rect multiColumnTreeViewRect
		{
			get { return new Rect(10f, 45f, position.width * .4f, position.height - 45f - 20f); }
		}

		public Rect canvasRect
		{
			get { return new Rect(.4f * position.width + 10f, 0f, position.width * .6f, position.height); }
		}

		Rect bottomToolbarRect
		{
			get { return new Rect(10f, position.height - 20f, position.width * .4f, 16f); }
		}

		public ProfilerTreeView treeView
		{
			get { return m_TreeView; }
		}

		void InitIfNeeded ()
		{
			if (!m_Initialized)
			{
                //Init the Node View
                m_nodeView = new ProfilerNodeView(this);

                // Check if it already exists (deserialized from window layout file or scriptable object)
                if (m_TreeViewState == null)
					m_TreeViewState = new TreeViewState();

				var headerState = ProfilerTreeView.CreateDefaultMultiColumnHeaderState(multiColumnTreeViewRect.width);
				if (MultiColumnHeaderState.CanOverwriteSerializedFields(m_MultiColumnHeaderState, headerState))
					MultiColumnHeaderState.OverwriteSerializedFields(m_MultiColumnHeaderState, headerState);
				m_MultiColumnHeaderState = headerState;

				var multiColumnHeader = new MultiColumnHeader (headerState); 
				m_TreeModel = new TreeModel<MemoryElement>(GetData());
				m_TreeView = new ProfilerTreeView(m_TreeViewState, multiColumnHeader, m_TreeModel);
				m_TreeView.doubleClickedCallback += OnDoubleClickCell;

				// Register the callback for snapshots.
				UnityEditor.MemoryProfiler.MemorySnapshot.OnSnapshotReceived += IncomingSnapshot;

				if (_unpackedCrawl == null && _packedCrawled != null && _packedCrawled.valid)
					Unpack();

				m_Initialized = true;
			}
		}

		private void OnDoubleClickCell ( int id )
		{
			Debug.Log ("ON CLICK CELL " + m_TreeView.GetSelection()[0]);
			MemoryElement tmp = m_TreeModel.Find (id);
			Debug.Log ("ME: " + tmp.name);
            m_nodeView.CreateNode(tmp.memData,_unpackedCrawl);
		}

		void Unpack()
		{
			_unpackedCrawl = CrawlDataUnpacker.Unpack(_packedCrawled);
			m_Status = "Loading snapshot in Grid .....";

			m_TreeModel.SetData ( populateData ( _unpackedCrawl.allObjects.Length ) );
			m_TreeView.Reload ();
			m_Status = "Snapshot Loaded!";
		}

		private IList<MemoryElement> populateData (int numTotalElements)
		{
			var treeElements = new List<MemoryElement>(numTotalElements+1);
			var root = new MemoryElement("Root", -1, 0, "Root", "Root", 0f);
			treeElements.Add(root);

			if (_unpackedCrawl == null)
				return treeElements;

			ThingInMemory[] tmp = _unpackedCrawl.allObjects;

			for (int i = 0; i < numTotalElements; ++i)
			{
				root = new MemoryElement(tmp[i].caption, 0, tmp[i].instanceID, tmp[i].className, tmp[i].type, tmp[i].size);
                root.memData = tmp[i];
				treeElements.Add(root);
			}

			return treeElements;
		}

		void IncomingSnapshot(PackedMemorySnapshot snapshot)
		{
            m_nodeView.ClearNodeView();
			m_Status = "Unpacking snapshot.... OK.";
			_snapshot = snapshot;

			_packedCrawled = new Crawler().Crawl(_snapshot);
			Unpack();
		}

		IList<MemoryElement> GetData ()
		{
			return populateData (1);//ProfilerWindow.GenerateRandomTree(130); 
		}

		public static List<MemoryElement> GenerateRandomTree(int numTotalElements)
		{
			int numRootChildren = numTotalElements / 4;
			var treeElements = new List<MemoryElement>(numTotalElements);

			var root = new MemoryElement("Aircraft_1", -1, 700, "Texture2D", "NativeUnityEngineObject", 10.7f);
			treeElements.Add(root);
			for (int i = 0; i < numRootChildren; ++i)
			{
				root = new MemoryElement("Aircraft_" + i, 0, 700+i, "Texture2D", "NativeUnityEngineObject", 10.7f + i);
				treeElements.Add(root);
			}

			return treeElements;
		}

		void OnGUI ()
		{
			InitIfNeeded();
            DoCanvasView(canvasRect);
            TopToolBar (topToolbarRect);
			SearchBar (searchBarRect);
			DoTreeView (multiColumnTreeViewRect);
			
			BottomToolBar (bottomToolbarRect);
		}

		void SearchBar (Rect rect)
		{
			treeView.searchString = SearchField.OnGUI(rect, treeView.searchString);
		}

		void DoTreeView (Rect rect)
		{
			m_TreeView.OnGUI(rect);
		}

		void DoCanvasView ( Rect rect )
		{
            m_nodeView.DrawProfilerNodeView(rect);
            Repaint();
		}

		void TopToolBar (Rect rect)
		{
			GUILayout.BeginArea (rect);
			using (new EditorGUILayout.HorizontalScope ()) 
			{
				var style = "miniButton";
				if (GUILayout.Button("Take Snapshot", style))
				{
					m_Status = "Taking snapshot.....";
					UnityEditor.MemoryProfiler.MemorySnapshot.RequestNewSnapshot();
				}

				if (GUILayout.Button("Load Snapshot", style))
				{
					m_Status = "Loading snapshot.....";
					PackedMemorySnapshot packedSnapshot = PackedMemorySnapshotUtility.LoadFromFile();
					if(packedSnapshot != null)
						IncomingSnapshot(packedSnapshot);
				}

				if (GUILayout.Button("Save Snapshot", style))
				{
					m_Status = "Saving snapshot.....";
					PackedMemorySnapshotUtility.SaveToFile(_snapshot);
				}
			}

			GUILayout.EndArea();
		}

		void BottomToolBar (Rect rect)
		{
			GUILayout.BeginArea (rect);
			using (new EditorGUILayout.HorizontalScope ()) 
			{
				GUILayout.Label (m_Status);
			}
			GUILayout.EndArea();
		}
	}


	internal static class SearchField
	{
		static class Styles
		{
			public static GUIStyle searchField = "SearchTextField";
			public static GUIStyle searchFieldCancelButton = "SearchCancelButton";
			public static GUIStyle searchFieldCancelButtonEmpty = "SearchCancelButtonEmpty";
		}

		public static string OnGUI(Rect position, string text)
		{
			// Search field 
			Rect textRect = position;
			textRect.width -= 15;
			text = EditorGUI.TextField(textRect, GUIContent.none, text, Styles.searchField);

			// Cancel button
			Rect buttonRect = position;
			buttonRect.x += position.width - 15;
			buttonRect.width = 15;
			if (GUI.Button(buttonRect, GUIContent.none, text != "" ? Styles.searchFieldCancelButton : Styles.searchFieldCancelButtonEmpty) && text != "")
			{
				text = "";
				GUIUtility.keyboardControl = 0;
			}
			return text;
		}
	}

}
