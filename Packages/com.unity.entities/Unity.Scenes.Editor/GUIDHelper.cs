

namespace Unity.Scenes.Editor
{
    static class GUIDHelper
    {
        public static UnityEngine.GUID UnityEditorResources = new UnityEngine.GUID("0000000000000000d000000000000000");
        public static UnityEngine.GUID UnityBuiltinResources = new UnityEngine.GUID("0000000000000000e000000000000000");
        public static UnityEngine.GUID UnityBuiltinExtraResources = new UnityEngine.GUID("0000000000000000f000000000000000");

        public static bool IsBuiltin(in UnityEngine.GUID g) =>
            g == UnityEditorResources ||
            g == UnityBuiltinResources ||
            g == UnityBuiltinExtraResources;

        public static bool IsBuiltinResources(in UnityEngine.GUID g) =>
            g == UnityBuiltinResources;

        public static bool IsBuiltinExtraResources(in UnityEngine.GUID g) =>
            g == UnityBuiltinExtraResources;

        public static unsafe void PackBuiltinExtraWithFileIdent(ref UnityEngine.GUID guid, long fileIdent)
        {
            fixed(void* ptr = &guid)
            {
                var asHash = (Entities.Hash128*)ptr;
                asHash->Value.w = (uint)fileIdent;
            }
        }

    }
}
