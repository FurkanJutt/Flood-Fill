using System;

namespace FloodFill.ThreeD.Solver
{
    // One color per original region is necessary: gameplay permits moving any component.
    internal sealed class FloodSolverState : IEquatable<FloodSolverState>
    {
        internal readonly byte[] Colors;
        private readonly int hash;
        internal FloodSolverState(byte[] ownedColors)
        {
            Colors = ownedColors;
            unchecked
            {
                uint h = 2166136261;
                for (int i = 0; i < Colors.Length; i++) h = (h ^ Colors[i]) * 16777619;
                hash = (int)h;
            }
        }
        public bool Equals(FloodSolverState other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other == null || hash != other.hash || Colors.Length != other.Colors.Length) return false;
            for (int i = 0; i < Colors.Length; i++) if (Colors[i] != other.Colors[i]) return false;
            return true;
        }
        public override bool Equals(object obj) => Equals(obj as FloodSolverState);
        public override int GetHashCode() => hash;
    }
}
