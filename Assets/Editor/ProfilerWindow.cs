using System;
using System.Collections.Generic;
using UnityEditor.Callbacks;
#if UNITY_5_6_OR_NEWER
using UnityEditor.IMGUI.Controls;
#endif
using UnityEngine;
using UnityEditor.TreeViewExamples;
using UnityEditor.MemoryProfiler;
using MemoryProfilerWindow;

namespace UnityEditor.MemoryProfiler2
{

    class ProfilerWindow : EditorWindow
    {
        [NonSerialized]
        bool m_Initialized;
#if UNITY_5_6_OR_NEWER
        [SerializeField]
        TreeViewState m_TreeViewState; // Serialized in the window layout file so it survives assembly reloading
        [SerializeField]
        MultiColumnHeaderState m_MultiColumnHeaderState;
        ProfilerTreeView m_TreeView;
        TreeModel<MemoryElement> m_TreeModel;
#endif
        string m_Status = "Profiling";
        bool bCheckHeapOnly = false;
        bool bshowPlainData = false;
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
        public static ProfilerWindow GetWindow()
        {
            var window = GetWindow<ProfilerWindow>();
            window.titleContent = new GUIContent("Profiler");
            window.Focus();
            window.Repaint();
            return window;
        }

        

        public enum FilterType
        {
            Mesh,
            Texture,
            All,
            None
        }
        private FilterType actualInputType = FilterType.Mesh;
        private FilterType oldInputType = FilterType.None;
        private Vector2 scrollViewVector = Vector2.zero;

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

        public Rect fullCanvasRect
        {
            get { return new Rect(0, 20f, position.width, position.height-20f); }
        }

        Rect bottomToolbarRect
		{
			get { return new Rect(10f, position.height - 20f, position.width * .4f, 16f); }
		}
#if UNITY_5_6_OR_NEWER
		public ProfilerTreeView treeView
		{
			get { return m_TreeView; }
		}
#endif
        void InitIfNeeded ()
		{
			if (!m_Initialized)
			{
                //Init the Node View
                m_nodeView = new ProfilerNodeView(this);
#if UNITY_5_6_OR_NEWER
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
#endif
				// Register the callback for snapshots.
				UnityEditor.MemoryProfiler.MemorySnapshot.OnSnapshotReceived += IncomingSnapshot;

				if (_unpackedCrawl == null && _packedCrawled != null && _packedCrawled.valid)
					Unpack();

				m_Initialized = true;
			}
		}

#if UNITY_5_6_OR_NEWER
		private void OnDoubleClickCell ( int id )
		{
            if (m_TreeView.GetSelection().Count > 0)
            {
                Debug.Log("ON CLICK CELL " + m_TreeView.GetSelection()[0]);
                MemoryElement tmp = m_TreeModel.Find(id);
                Debug.Log("ME: " + tmp.name);
                m_nodeView.CreateNode(tmp.memData, _unpackedCrawl);
            }
		}
#endif
        void Unpack()
		{
			_unpackedCrawl = CrawlDataUnpacker.Unpack(_packedCrawled);
			m_Status = "Loading snapshot in Grid .....";
#if !UNITY_5_6_OR_NEWER
            m_nodeView.ClearNodeView();
            bCheckHeapOnly = true;
#endif
            if (bCheckHeapOnly)
            {
                m_nodeView.CreateTreelessView(_unpackedCrawl);
            }
            else
            {
                Array.Sort(_unpackedCrawl.nativeObjects, new NativeUnityEngineObjectComparer());
                Array.Sort(_unpackedCrawl.managedObjects, new ManagedObjectComparer());
                m_nodeView.bShowMemHeap = false;
#if UNITY_5_6_OR_NEWER
                m_TreeModel.SetData ( populateData ( _unpackedCrawl.allObjects.Length ) );
			    m_TreeView.Reload ();
#endif
            }
            //Debug.Log("Snapshot Loaded! " + _unpackedCrawl);
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
                if (tmp[i] is NativeUnityEngineObject)
                {
                    NativeUnityEngineObject nuo = tmp[i] as NativeUnityEngineObject;
                    root = new MemoryElement(nuo.caption, 0, nuo.instanceID, nuo.className, "Native", tmp[i].size);
                }
                else if(tmp[i] is ManagedObject)
                {
                    ManagedObject mo = tmp[i] as ManagedObject;
                    root = new MemoryElement(mo.caption, 0, 0, "Managed", "Managed", mo.size);
                }
                else if (tmp[i] is GCHandle)
                {
                    GCHandle gch = tmp[i] as GCHandle;
                    root = new MemoryElement(gch.caption, 0, 0, "GC Handle", "GC Handle", gch.size);
                }
                else if (tmp[i] is StaticFields)
                {
                    StaticFields sf = tmp[i] as StaticFields;
                    root = new MemoryElement(sf.caption, 0, 0, "GC Handle", sf.typeDescription.name, sf.size);
                }
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
            if (!bCheckHeapOnly)
            {
                DoCanvasView(canvasRect);
            }
            else
            {
                if (bshowPlainData)
                {
                    DrawPlainData();
                }
                else
                {
                    DoCanvasView(fullCanvasRect);
                }
            }
            TopToolBar (topToolbarRect);
           
#if UNITY_5_6_OR_NEWER
            if (!bCheckHeapOnly)
            {
                SearchBar(searchBarRect);
                DoTreeView(multiColumnTreeViewRect);
                BottomToolBar(bottomToolbarRect);
            }
#else
            DoLegacyTreeView(multiColumnTreeViewRect);
#endif
        }

        void DoLegacyTreeView(Rect rect)
        {
            //Event e = Event.current;
            //EditorGUILayout.BeginVertical();
            //actualInputType = (FilterType)EditorGUILayout.EnumPopup("Select filter type: ", actualInputType);

            //if (actualInputType != oldInputType)
            //{
            //    FilterList(actualInputType);
            //}
            //scrollViewVector = GUI.BeginScrollView(new Rect(5, 50, 225, 500), scrollViewVector, new Rect(0, 40, 225, 18 * filteredList.Count));
            //for (int i = 0; i < filteredList.Count; i++)
            //{
            //    EditorGUILayout.LabelField(filteredList[i].text);
            //    if (e.type == EventType.Repaint)
            //    {
            //        filteredList[i].myAreaRect = GUILayoutUtility.GetLastRect();
            //    }
            //}
            //EditorGUILayout.EndVertical();
            //oldInputType = actualInputType;
            //GUI.EndScrollView();
        }
#if UNITY_5_6_OR_NEWER
        void SearchBar (Rect rect)
		{
			treeView.searchString = SearchField.OnGUI(rect, treeView.searchString);
		}

		void DoTreeView (Rect rect)
		{
			m_TreeView.OnGUI(rect);
		}
#endif
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
                    bCheckHeapOnly = false;
                    m_Status = "Taking snapshot.....";
					UnityEditor.MemoryProfiler.MemorySnapshot.RequestNewSnapshot();
				}

                if (GUILayout.Button("Load Snapshot", style))
				{
                    bCheckHeapOnly = false;
                    m_Status = "Loading snapshot.....";
					PackedMemorySnapshot packedSnapshot = PackedMemorySnapshotUtility.LoadFromFile();
                    //Debug.Log("Unlock!!!!!!!!!!!! " + packedSnapshot);
                    if (packedSnapshot != null)
						IncomingSnapshot(packedSnapshot);
				}

                if (_snapshot != null)
                {
                    if (GUILayout.Button("Save Snapshot", style))
                    {
                        m_Status = "Saving snapshot.....";
                        PackedMemorySnapshotUtility.SaveToFile(_snapshot);
                    }

                }

                if (_unpackedCrawl != null)
                {
#if UNITY_5_6_OR_NEWER
                    //if (bCheckHeapOnly)
                    //{

                        if (GUILayout.Button("Show Tree/Node View", style))
                        {
                            bCheckHeapOnly = false;
                            m_nodeView.bShowMemHeap = false;
                            m_nodeView.ClearNodeView();
                            m_TreeView.Reload();
                        }

                    //}
                    //else
                    {
#endif
                        if (GUILayout.Button("Show Heap Usage", style))
                        {
                            bCheckHeapOnly = true;
                            bshowPlainData = false;
                            m_nodeView.ClearNodeView();
                            m_nodeView.CreateTreelessView(_unpackedCrawl);
                        }

                        if (GUILayout.Button("Show Plain Data", style))
                        {
                            bCheckHeapOnly = true;
                            bshowPlainData = true;
                        }
#if UNITY_5_6_OR_NEWER
                    }
#endif
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

        public void DrawPlainData()
        {
            if (_unpackedCrawl != null)
            {
                GUILayout.Label(" ");
                if (GUILayout.Button("Save full list of elements data to an external .csv file"))
                {
                    string exportPath = EditorUtility.SaveFilePanel("Save Snapshot Info", Application.dataPath, "SnapshotExport.csv", "csv");
                    if (!String.IsNullOrEmpty(exportPath))
                    {
                        System.IO.StreamWriter sw = new System.IO.StreamWriter(exportPath);
                        sw.WriteLine("Managed Objects");
                        for (int i = 0; i < _unpackedCrawl.managedObjects.Length; i++)
                        {
                            ManagedObject managedObject = _unpackedCrawl.managedObjects[i];
                            sw.WriteLine("Address: " + managedObject.address + ", Caption: " + managedObject.caption + ", Type:, " + managedObject.typeDescription.name + ",Size:," + managedObject.size);
                        }
                        sw.WriteLine("Native Objects");
                        for (int i = 0; i < _unpackedCrawl.nativeObjects.Length; i++)
                        {
                            NativeUnityEngineObject nativeObject = _unpackedCrawl.nativeObjects[i];
                            sw.WriteLine("InstanceID: " + nativeObject.instanceID + ", Name: " + nativeObject.name + ", Class Name:, " + nativeObject.className + ",Size:," + nativeObject.size );
                        }
                        sw.Flush();
                        sw.Close();
                    }
                }
                GUILayout.Label(" ");
                GUILayout.Label("Managed Objects (Total: "+ _unpackedCrawl.managedObjects.Length + ") - First 10 Elements: ");
                GUILayout.Label(" ");
                for (int i = 0; i < _unpackedCrawl.managedObjects.Length && i < 10; i++)
                {
                    ManagedObject managedObject = _unpackedCrawl.managedObjects[i];
                    GUILayout.Label("Address: " + managedObject.address + ", Caption: " + managedObject.caption + ", Size: " + managedObject.size);
                }
                GUILayout.Label(" ");
                GUILayout.Label("Native Objects (Total: "+ _unpackedCrawl.nativeObjects.Length + ") - First 10 Elements:");
                GUILayout.Label(" ");
                for (int i = 0; i < _unpackedCrawl.nativeObjects.Length && i < 10; i++)
                {
                    NativeUnityEngineObject nativeObject = _unpackedCrawl.nativeObjects[i];
                    GUILayout.Label("InstanceID: " + nativeObject.instanceID + ", Name: " + nativeObject.name + ", Size: " + nativeObject.size);
                }
            }
        }

	}


    public class NativeUnityEngineObjectComparer : System.Collections.Generic.IComparer<NativeUnityEngineObject>
    {
        public int Compare(NativeUnityEngineObject x, NativeUnityEngineObject y)
        {
            if (x.instanceID < y.instanceID) return -1;
            if (x.instanceID > y.instanceID) return 1;
            if (x.instanceID == y.instanceID) return 0;

            return 0;
        }
    }

    public class ManagedObjectComparer : System.Collections.Generic.IComparer<ManagedObject>
    {
        public int Compare(ManagedObject x, ManagedObject y)
        {
            if (x.address < y.address) return -1;
            if (x.address > y.address) return 1;
            if (x.address == y.address) return 0;

            return 0;
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
