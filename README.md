# Unity Support Colombia: Memory Profiler Extension
Extension for the existing Unity memory profiler project, this is a WIP project made by the Unity Support Team in Colombia, which uses as a base the Memory Profiler project found in https://bitbucket.org/Unity-Technologies/memoryprofiler and uses the new memory profiler API introduced in Unity5.3a4, adding filters and search options with a node viewer for the objects and their references.

In order to use it, copy the files in your project, then you will see a new Editor menu called: Support Memory Tools from which you can take,load, save and analyze memory snapshots

* Requires Unity 5.6 or newer in order to use the filters and table views
* For checking the Heap Memory Information and also for export objects list data to external .csv files (for example the list of all managed or native Objects) you could use any version of Unity that supports the API: (e.g 5.3,5.4,5.5,5.6,2017) 
* Requires a IL2CPP build
* It uses the same snapshots taken with the Memory Profiler project from bitbucket (You can use as an example memory snapshot which is included in the repository)
* It's recommended to read snapshots taken from the same Unity version (for example analyse 5.6 snapshots using 5.6 editor,etc) 
* In the Node View you can zoom in / zoom out with the mouse wheel, use the mouse middle button to pan, and the left button to move each nodes, use the mouse right button to clear the graph
* You only can expand Nodes from Native Objects
* You can search though a existing snapshot by using the following search options in the Tree/Node View Panel (You can also combine them to create complex filters):
	
	i: string         filter by InstanceID  
	
	n: string         filter by resource name
	
	t: string         filter by type (managed, native, static, etc).
	
	s: string         filter by size (for example s: 10.7 will wind all the 10.7MB elements)
	
	s:< string        filter by less or equal size
	
	s:> string        filter by greater or equal size
	
	c: string         filter by class name


![Alt text](/Documentation/Images/TreeView.jpg?raw=true "Memory Profiler Tree/Node Window")
![Alt text](/Documentation/Images/HeapView.jpg?raw=true "Memory Profiler Heap Window")
![Alt text](/Documentation/Images/PlainData.jpg?raw=true "Memory Profiler Plain Data Window")

