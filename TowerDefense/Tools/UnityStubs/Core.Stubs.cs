// Workstream 1 (Core & Combat) additions to the UnityEngine stubs.
// Real Unity 6 signatures only; see UnityEngine.Stubs.cs for the rules.
#pragma warning disable CS0067, CS0108, CS0114, CS1591
using System.Collections.Generic;

namespace UnityEngine
{
    public partial class Component
    {
        public void GetComponents<T>(List<T> results) { }
        public void GetComponentsInChildren<T>(bool includeInactive, List<T> result) { }
    }

    public sealed partial class GameObject
    {
        public void GetComponents<T>(List<T> results) { }
        public void GetComponentsInChildren<T>(bool includeInactive, List<T> results) { }
    }
}
