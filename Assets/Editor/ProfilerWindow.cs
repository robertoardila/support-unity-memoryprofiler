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

        [MenuItem("Support Memory Tools/Profiler")]
        public static ProfilerWindow GetWindow()
        {
            var window = GetWindow<ProfilerWindow>();
            window.titleContent = new GUIContent("Profiler");
            window.Focus();
            window.Repaint();
            return window;
        }

        GUIStyle blueColorStyle;
        GUIStyle redColorStyle;
        GUIStyle greenColorStyle;

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
        public void InitButonStyles()
        {
            blueColorStyle = new GUIStyle(GUI.skin.button);
            blueColorStyle.normal.textColor = Color.green;
            greenColorStyle = new GUIStyle(GUI.skin.button);
            greenColorStyle.normal.textColor = Color.red;
            redColorStyle = new GUIStyle(GUI.skin.button);
            redColorStyle.normal.textColor = Color.yellow;
        }

        void InitIfNeeded ()
		{
            InitButonStyles();

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
                MemoryElement tmp = m_TreeModel.Find(id);
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
           

            if (!bCheckHeapOnly)
            {
                SearchBar(searchBarRect);
                DoTreeView(multiColumnTreeViewRect);
                BottomToolBar(bottomToolbarRect);
            }
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

                        if (GUILayout.Button("Show Tree/Node View", style))
                        {
                            bCheckHeapOnly = false;
                            m_nodeView.bShowMemHeap = false;
                            m_nodeView.ClearNodeView();
                            m_TreeView.Reload();
                        }

                    
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
                if (GUILayout.Button("Save full list of MANAGED objects data to an external .csv file",blueColorStyle))
                {
                    string exportPath = EditorUtility.SaveFilePanel("Save Snapshot Info", Application.dataPath, "MANAGED SnapshotExport ("+ DateTime.Now.ToString("dd-MM-yyyy hh-mm-ss") + ").csv", "csv");
                    if (!String.IsNullOrEmpty(exportPath))
                    {
                        System.IO.StreamWriter sw = new System.IO.StreamWriter(exportPath);
                        sw.WriteLine(" Managed Objects , Size , Caption , Type , Number of References (Total), Referenced By (Total), Address ");
                        for (int i = 0; i < _unpackedCrawl.managedObjects.Length; i++)
                        {
                            ManagedObject managedObject = _unpackedCrawl.managedObjects[i];
                            sw.WriteLine("Managed,"+managedObject.size + "," + CleanStrings(managedObject.caption) + "," + CleanStrings(managedObject.typeDescription.name) + ","+managedObject.references.Length+","+managedObject.referencedBy.Length+","+managedObject.address);
                        }
                        sw.Flush();
                        sw.Close();
                    }
                }
                if (GUILayout.Button("Save full list of NATIVE objects data to an external .csv file",greenColorStyle))
                {
                    string exportPath = EditorUtility.SaveFilePanel("Save Snapshot Info", Application.dataPath, "NATIVE SnapshotExport (" + DateTime.Now.ToString("dd-MM-yyyy hh-mm-ss") + ").csv", "csv");
                    if (!String.IsNullOrEmpty(exportPath))
                    {
                        System.IO.StreamWriter sw = new System.IO.StreamWriter(exportPath);
                        sw.WriteLine(" Native Objects , Size , Caption , Class Name , Name , Number of References (Total), Referenced By (Total), InstanceID ");
                        for (int i = 0; i < _unpackedCrawl.nativeObjects.Length; i++)
                        {
                            NativeUnityEngineObject nativeObject = _unpackedCrawl.nativeObjects[i];
                            sw.WriteLine("Native," + nativeObject.size + "," + CleanStrings(nativeObject.caption) + "," + CleanStrings(nativeObject.className) + "," + CleanStrings(nativeObject.name) + "," + nativeObject.references.Length + "," + nativeObject.referencedBy.Length + "," + nativeObject.instanceID);
                        }
                        sw.Flush();
                        sw.Close();
                    }
                }
                if (GUILayout.Button("Save full list of All objects data to an external .csv file",redColorStyle))
                {
                    string exportPath = EditorUtility.SaveFilePanel("Save Snapshot Info", Application.dataPath, "ALL SnapshotExport ("+ DateTime.Now.ToString("dd-MM-yyyy hh-mm-ss") + ").csv", "csv");
                    if (!String.IsNullOrEmpty(exportPath))
                    {
                        System.IO.StreamWriter sw = new System.IO.StreamWriter(exportPath);
                        sw.WriteLine(" Object , Size , Caption , Type , Number of References (Total), Referenced By (Total), Address (Managed) or InstanceID (Native) ");
                        for (int i = 0; i < _unpackedCrawl.managedObjects.Length; i++)
                        {
                            ManagedObject managedObject = _unpackedCrawl.managedObjects[i];
                            sw.WriteLine("Managed," + managedObject.size + "," + CleanStrings(managedObject.caption) + "," + CleanStrings(managedObject.typeDescription.name) + "," + managedObject.references.Length + "," + managedObject.referencedBy.Length + "," + managedObject.address);
                        }
                        for (int i = 0; i < _unpackedCrawl.nativeObjects.Length; i++)
                        {
                            NativeUnityEngineObject nativeObject = _unpackedCrawl.nativeObjects[i];
                            sw.WriteLine("Native," + nativeObject.size + "," + CleanStrings(nativeObject.caption) + "," + CleanStrings(nativeObject.className) + "," + nativeObject.references.Length + "," + nativeObject.referencedBy.Length + "," + nativeObject.instanceID);
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

        public string CleanStrings(string text)
        {
            return text.Replace(",", " ");
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
