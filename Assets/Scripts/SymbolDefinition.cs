using System;
using SlotGame.Core;
using UnityEngine;

namespace SlotGame
{
    /// <summary>
    /// Binds a <see cref="SymbolType"/> to its visual sprite.
    /// A plain serializable class (rather than a ScriptableObject) so the whole
    /// mapping can live on the SlotMachineController inspector in one list.
    /// </summary>
    [Serializable]
    public class SymbolDefinition
    {
        [Tooltip("Logical symbol used by the RNG and the pay table.")]
        public SymbolType Type;

        [Tooltip("Sprite rendered on the reel.")]
        public Sprite Sprite;
    }
}
