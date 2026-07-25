namespace Unity.Entities
{
#if UNITY_EDITOR    
    /// <summary>
    /// Add this tag component to an Entity to hide it in the Hierarchy window. <br />
    /// <b>Note:</b> The tag component is only available in the editor.
    /// Make sure to strip any use of it out with <b>#if UNITY_EDITOR</b> to prevent build compilation errors. 
    /// </summary>
    internal struct HideInHierarchy : IComponentData { }
#endif
}