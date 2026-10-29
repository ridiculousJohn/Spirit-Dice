using UnityEngine;

namespace Dice.UnityContent
{
    /**
     * Lets the inspector choose the concrete class of a [SerializeReference] field or array element among its field type and
     * its subclasses, or clear it.
     */
    public sealed class SelectImplementationAttribute : PropertyAttribute
    {
    }
}
