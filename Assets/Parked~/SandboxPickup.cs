using UnityEngine;

namespace DontForgetMe
{
    public sealed class SandboxPickup : MonoBehaviour
    {
        public SandboxItem? Item { get; set; }
        public SandboxBackpack Backpack { get; set; }
        public string Label => Backpack != null
            ? "背包（" + Backpack.Contents.Count + "/" + Backpack.Capacity + "）"
            : SandboxItems.Name(Item.Value);
    }
}
