using System;
using UnityEngine;
using UnityEditor.TreeViewExamples;
using Random = UnityEngine.Random;
using MemoryProfilerWindow;

namespace UnityEditor.MemoryProfiler2
{
	// Type (Native Unity Engine Object, etc.), ClassName (Texture2D), InstanceID - id (700), size (10.7MB), Name (Textura1)
	[Serializable]
	class MemoryElement : TreeElement
	{
		[SerializeField] float m_Size;
		[SerializeField] string m_ClassName;
		[SerializeField] string m_Type;
        public ThingInMemory memData;

		public bool enabled;

		public MemoryElement (string name, int depth, int id, string className, string type, float size) : base (name, depth, id)
		{
			m_ClassName = className;
			m_Type = type;
			m_Size = size;
			enabled = true;
		}

		public float size
		{
			get { return m_Size; } set { m_Size = value; }
		}

		public string className
		{
			get { return m_ClassName; } set { m_ClassName = value; }
		}

		public string type
		{
			get { return m_Type; } set { m_Type = value; }
		}
	}
}
