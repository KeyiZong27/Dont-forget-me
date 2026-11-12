using UnityEngine;

namespace DontForgetMe
{
    // Something lying in the rubbish by the gate that can be snatched up and swung. What it is
    // decides how much it hurts, how far it sends someone back and how far it reaches.
    public sealed class Weapon
    {
        public enum Material { Metal, Wood, Masonry, Plastic }

        // What it is called, and the counting word that goes in front of it.
        public string Name;
        public string Measure;
        // Origin at the grip, +z toward the end that does the hitting.
        public Transform Root;
        public float Length;
        public float Damage;
        // Shove strength, on the scale where a bare two-handed push is 1.
        public float Power;
        public bool TwoHanded;
        public Material Made;
        public bool Taken;

        // How far from the body it lands, and how long before it can be swung again.
        public float Reach => 1.25f + Length * 0.6f;
        public float Recovery => TwoHanded ? 1.15f : 0.8f + Length * 0.25f;
    }
}
