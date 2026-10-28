using UnityEngine;

namespace Dice.UnityContent
{
    /** Lets the inspector choose the concrete class of a [SerializeReference] field or array element among its field type's subclasses. */
    public sealed class SelectImplementationAttribute : PropertyAttribute
    {
    }
}
