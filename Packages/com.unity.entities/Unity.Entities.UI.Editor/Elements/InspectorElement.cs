using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    /// <summary>
    /// Makes an element that can generate a UI hierarchy for a given object. Inspector types deriving from
    /// <see cref="Inspector{T}"/> will be considered for the root target only. When generating the UI hierarchy for
    /// properties of the target, inspector types deriving from <see cref="PropertyInspector{TValue}"/> and
    /// <see cref="PropertyInspector{TValue, TAttribute}"/> will be considered.
    /// </summary>
    [UxmlElement]
    internal sealed partial class InspectorElement : BindingContextElement
    {
        /// <summary>
        /// Creates a new instance of <see cref="InspectorElement"/> with the provided target value.
        /// </summary>
        /// <param name="value">The target.</param>
        /// <typeparam name="TValue">The target type.</typeparam>
        /// <returns>The new instance.</returns>
        public static InspectorElement MakeWithValue<TValue>(TValue value)
        {
            var element = new InspectorElement();
            element.SetTarget(value);
            return element;
        }
    }
}
