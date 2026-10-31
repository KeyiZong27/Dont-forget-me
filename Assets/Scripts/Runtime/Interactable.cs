using System;
using UnityEngine;

namespace DontForgetMe
{
    public sealed class Interactable : MonoBehaviour
    {
        public string Prompt { get; set; }
        public float Range { get; set; } = 2.6f;
        public Func<bool> IsAvailable { get; set; }
        public Action OnInteract { get; set; }

        public bool Available => isActiveAndEnabled && (IsAvailable == null || IsAvailable());
    }
}
