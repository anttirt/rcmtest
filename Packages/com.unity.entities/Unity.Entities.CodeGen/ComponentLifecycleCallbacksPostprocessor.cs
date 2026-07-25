using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.Burst;
using Unity.CompilationPipeline.Common.Diagnostics;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using ParameterAttributes = Mono.Cecil.ParameterAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace Unity.Entities.CodeGen
{
    /// <summary>
    /// IL Post Processor for component lifecycle callbacks (IDebugOnAdded, IDebugOnRemoved).
    /// Generates static wrapper methods and registration code, similar to ISystemPostProcessor.
    /// </summary>
    internal class ComponentLifecycleCallbacksPostprocessor : EntitiesILPostProcessor
    {
        // Must run after StaticTypeRegistryPostProcessor
        public override int SortWeight => 1;

        struct ComponentCallbackInfo
        {
            public TypeReference ComponentType;
            public MethodDefinition OnAddedWrapper;
            public MethodDefinition OnRemovedWrapper;
            public bool HasOnAdded;
            public bool HasOnRemoved;
            public bool OnAddedIsBurst;
            public bool OnRemovedIsBurst;
        }        
        
        protected override bool PostProcessImpl(TypeDefinition[] componentSystemTypes)
        {
            return false;
        }

        protected override bool PostProcessUnmanagedImpl(TypeDefinition[] unmanagedComponentSystemTypes)
        {
            var componentTypes = runnerOfMe.CollectedComponentTypes;
            if (componentTypes == null || componentTypes.Count == 0)
                return false;

            var changes = false;
            var componentCallbacks = new List<ComponentCallbackInfo>();

            foreach (var typeDef in componentTypes)
            {
                // Check for IDebugOnAdded and IDebugOnRemoved interfaces
                var hasOnAdded = typeDef.TypeImplements(runnerOfMe._IDebugOnAddedDef);
                var hasOnRemoved = typeDef.TypeImplements(runnerOfMe._IDebugOnRemovedDef);

                if (!hasOnAdded && !hasOnRemoved)
                    continue;

                changes = true;

                // Generate wrapper methods for this component type
                var componentCallback = GenerateWrappers(typeDef, hasOnAdded, hasOnRemoved);
                if (componentCallback.HasValue)
                    componentCallbacks.Add(componentCallback.Value);
            }

            if (changes && componentCallbacks.Count > 0)
            {
                // Generate registration code
                GenerateRegistrationCode(componentCallbacks);
            }

            return changes;
        }

        ComponentCallbackInfo? GenerateWrappers(TypeDefinition componentType, bool hasOnAdded, bool hasOnRemoved)
        {
            var componentCallback = new ComponentCallbackInfo
            {
                ComponentType = LaunderTypeRef(componentType),
                HasOnAdded = hasOnAdded,
                HasOnRemoved = hasOnRemoved
            };

            if (hasOnAdded)
                componentCallback.OnAddedWrapper = TryGenerateLifecycleWrapper(componentType, "OnAdded", out componentCallback.OnAddedIsBurst);

            if (hasOnRemoved)
                componentCallback.OnRemovedWrapper = TryGenerateLifecycleWrapper(componentType, "OnRemoved", out componentCallback.OnRemovedIsBurst);

            return componentCallback;
        }

        MethodDefinition TryGenerateLifecycleWrapper(TypeDefinition componentType, string methodName, out bool isBurst)
        {
            MethodDefinition method = null;
            foreach (var m in componentType.Methods)
            {
                // Look for: public static void OnAdded(Entity entity, in T component)
                if (m.Name != methodName || !m.IsStatic || !m.IsPublic || m.Parameters.Count != 2)
                    continue;

                // Validate return type is void
                if (m.ReturnType.FullName != "System.Void")
                    continue;

                // Validate first parameter is Entity
                var param0 = m.Parameters[0];
                if (param0.ParameterType.FullName != "Unity.Entities.Entity")
                    continue;

                // Validate second parameter is 'in T' (ByReference with In attribute)
                var param1 = m.Parameters[1];
                if (!param1.ParameterType.IsByReference)
                    continue;
                if (!param1.IsIn)
                    continue;
                var elementType = ((ByReferenceType)param1.ParameterType).ElementType;
                if (elementType.FullName != componentType.FullName)
                    continue;

                method = m;
                break;
            }

            if (method != null)
                return GenerateWrapperMethod(componentType, method, methodName, out isBurst);

            _diagnosticMessages.Add(new DiagnosticMessage
            {
                DiagnosticType = DiagnosticType.Error,
                MessageData = $"Type {componentType.FullName} implements IDebug{methodName} but does not have a public static {methodName}(Entity entity, in {componentType.Name} component) method. " +
                              $"Lifecycle callback methods must be public, static, return void, and take Entity and an in reference to the component."
            });
            isBurst = false;
            return null;
        }

        MethodDefinition GenerateWrapperMethod(
            TypeDefinition componentType,
            MethodDefinition targetMethod,
            string methodName,
            out bool isBurst)
        {
            var mod = AssemblyDefinition.MainModule;
            var intPtrRef = mod.ImportReference(typeof(IntPtr));
            var entityRef = mod.ImportReference(runnerOfMe._EntityDef);
            var entityPtrRef = new PointerType(entityRef);

            MethodDefinition toPointerMethod = null;
            foreach (var m in intPtrRef.Resolve().Methods)
            {
                if (m.Name == nameof(IntPtr.ToPointer))
                {
                    toPointerMethod = m;
                    break;
                }
            }
            var intPtrToPointer = mod.ImportReference(toPointerMethod);

            // Create wrapper method name: __codegen__OnAdded_ComponentName
            var wrapperName = $"__codegen__{methodName}_{componentType.Name}";

            // Create static wrapper method
            // Signature: void wrapper(Entity* entityPtr, IntPtr componentPtr)
            var wrapperMethod = new MethodDefinition(
                wrapperName,
                MethodAttributes.Static | MethodAttributes.Assembly,
                mod.ImportReference(typeof(void)));

            wrapperMethod.Parameters.Add(new ParameterDefinition("entityPtr", ParameterAttributes.None, entityPtrRef));
            wrapperMethod.Parameters.Add(new ParameterDefinition("componentPtr", ParameterAttributes.None, intPtrRef));

            // Transfer BurstCompile attribute if present, preserving all settings
            isBurst = false;
            CustomAttribute burstAttribute = null;
            foreach (var attr in targetMethod.CustomAttributes)
            {
                if (attr.AttributeType.Name == "BurstCompileAttribute")
                {
                    burstAttribute = attr;
                    break;
                }
            }

            if (burstAttribute != null)
            {
                // Add BurstCompile to component type if not already present
                bool hasBurstCompile = false;
                foreach (var attr in componentType.CustomAttributes)
                {
                    if (attr.AttributeType.Name == "BurstCompileAttribute")
                    {
                        hasBurstCompile = true;
                        break;
                    }
                }
                if (!hasBurstCompile)
                {
                    componentType.CustomAttributes.Add(new CustomAttribute(
                        mod.ImportReference(typeof(BurstCompileAttribute).GetConstructor(Type.EmptyTypes))));
                }

                // Copy the attribute with all its settings to the wrapper
                wrapperMethod.CustomAttributes.Add(new CustomAttribute(
                    mod.ImportReference(burstAttribute.Constructor),
                    burstAttribute.GetBlob()));
                isBurst = true;
            }

            // Add MonoPInvokeCallback attribute for IL2CPP support
            var monoPInvokeCallbackAttr = new CustomAttribute(mod.ImportReference(runnerOfMe._monoPInvokeAttributeCtorDef));

            // Add MonoPInvokeCallback attribute with ComponentLifecycleDelegate type
            var typeManagerType = runnerOfMe._TypeManagerDef;
            TypeDefinition componentLifecycleDelegateType = null;
            foreach (var t in typeManagerType.NestedTypes)
            {
                if (t.Name == "ComponentLifecycleDelegate")
                {
                    componentLifecycleDelegateType = t;
                    break;
                }
            }

            // Note: ComponentLifecycleDelegate should always exist; if not, it's a serious bug
            if (componentLifecycleDelegateType != null)
            {
                monoPInvokeCallbackAttr.ConstructorArguments.Add(
                    new CustomAttributeArgument(mod.ImportReference(typeof(Type)), mod.ImportReference(componentLifecycleDelegateType)));
                wrapperMethod.CustomAttributes.Add(monoPInvokeCallbackAttr);
            }

            // Add Preserve attribute
            wrapperMethod.CustomAttributes.Add(new CustomAttribute(mod.ImportReference(runnerOfMe._preserveAttributeCtorDef)));

            // Generate IL:
            // targetMethod(*entityPtr, ref *(ComponentType*)componentPtr);
            var il = wrapperMethod.Body.GetILProcessor();

            // Load entity by dereferencing Entity* (arg 0)
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldobj, entityRef);

            // Convert componentPtr to pointer and load as ref (arg 1)
            il.Emit(OpCodes.Ldarga, 1);
            il.Emit(OpCodes.Call, intPtrToPointer);

            // Call the actual OnAdded/OnRemoved method
            var targetMethodRef = mod.ImportReference(targetMethod);
            il.Emit(OpCodes.Call, targetMethodRef);
            il.Emit(OpCodes.Ret);

            // Add wrapper to a registration type (not to component type itself, to avoid modifying packages)
            // We'll add it to the registration class later in GenerateRegistrationCode
            return wrapperMethod;
        }

        void GenerateRegistrationCode(List<ComponentCallbackInfo> componentCallbacks)
        {
            var mod = AssemblyDefinition.MainModule;

            // Create registration class with unique name
            var autoClassName = $"__ComponentLifecycleCallbacks__{TypeHash.FNV1A64(AssemblyDefinition.FullName)}";
            var registrationClass = new TypeDefinition(
                "",
                autoClassName,
                TypeAttributes.Class,
                mod.ImportReference(typeof(object)));

            registrationClass.IsBeforeFieldInit = false;

            // Add BurstCompile attribute to the class
            var burstCompileAttrCtor = typeof(BurstCompileAttribute).GetConstructor(Type.EmptyTypes);
            registrationClass.CustomAttributes.Add(new CustomAttribute(mod.ImportReference(burstCompileAttrCtor)));

            // Add Preserve attribute
            registrationClass.CustomAttributes.Add(new CustomAttribute(mod.ImportReference(runnerOfMe._preserveAttributeCtorDef)));

            // Add all wrapper methods to the registration class
            foreach (var componentCallback in componentCallbacks)
            {
                if (componentCallback.OnAddedWrapper != null)
                    registrationClass.Methods.Add(componentCallback.OnAddedWrapper);
                if (componentCallback.OnRemovedWrapper != null)
                    registrationClass.Methods.Add(componentCallback.OnRemovedWrapper);
            }

            mod.Types.Add(registrationClass);

            // Create EarlyInit method
            var earlyInitMethod = new MethodDefinition(
                "EarlyInit",
                MethodAttributes.Static | MethodAttributes.Public | MethodAttributes.HideBySig,
                mod.ImportReference(typeof(void)));

            earlyInitMethod.Body.InitLocals = false;

            // Add initialization attributes based on build target
            if (Array.IndexOf(Defines, "UNITY_EDITOR") < 0)
            {
                // Player: RuntimeInitializeOnLoadMethod
                var loadTypeEnumType = mod.ImportReference(typeof(UnityEngine.RuntimeInitializeLoadType));
                var runtimeInitAttrCtor = mod.ImportReference(
                    typeof(UnityEngine.RuntimeInitializeOnLoadMethodAttribute).GetConstructor(
                        new[] { typeof(UnityEngine.RuntimeInitializeLoadType) }));
                var runtimeInitAttr = new CustomAttribute(runtimeInitAttrCtor);
                runtimeInitAttr.ConstructorArguments.Add(
                    new CustomAttributeArgument(loadTypeEnumType, UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded));
                earlyInitMethod.CustomAttributes.Add(runtimeInitAttr);
            }
            else
            {
                // Editor: InitializeOnLoadMethod
                var editorInitAttrCtor = mod.ImportReference(
                    typeof(UnityEditor.InitializeOnLoadMethodAttribute).GetConstructor(Type.EmptyTypes));
                earlyInitMethod.CustomAttributes.Add(new CustomAttribute(editorInitAttrCtor));
            }

            registrationClass.Methods.Add(earlyInitMethod);

            // Generate IL for EarlyInit method
            GenerateEarlyInitIL(earlyInitMethod, componentCallbacks);
        }

        void GenerateEarlyInitIL(MethodDefinition earlyInitMethod, List<ComponentCallbackInfo> componentCallbacks)
        {
            var mod = AssemblyDefinition.MainModule;
            var il = earlyInitMethod.Body.GetILProcessor();

            // Get references to TypeManager
            var typeManagerType = runnerOfMe._TypeManagerDef;

            // Import RegisterComponentLifecycleCallback method
            MethodDefinition registerMethodDef = null;
            foreach (var m in typeManagerType.Methods)
            {
                if (m.Name == "RegisterComponentLifecycleCallback")
                {
                    registerMethodDef = m;
                    break;
                }
            }
            var registerMethod = mod.ImportReference(registerMethodDef);

            if (registerMethod == null)
            {
                _diagnosticMessages.Add(new DiagnosticMessage
                {
                    DiagnosticType = DiagnosticType.Error,
                    MessageData = "Could not find TypeManager.RegisterComponentLifecycleCallback method"
                });
                return;
            }

            // Get ComponentLifecycleDelegate constructor
            TypeDefinition delegateType = null;
            foreach (var t in typeManagerType.NestedTypes)
            {
                if (t.Name == "ComponentLifecycleDelegate")
                {
                    delegateType = t;
                    break;
                }
            }
            MethodDefinition delegateCtorDef = null;
            if (delegateType != null)
            {
                foreach (var c in delegateType.GetConstructors())
                {
                    if (c.Parameters.Count == 2)
                    {
                        delegateCtorDef = c;
                        break;
                    }
                }
            }
            var delegateCtor = mod.ImportReference(delegateCtorDef);

            if (delegateCtor == null)
            {
                _diagnosticMessages.Add(new DiagnosticMessage
                {
                    DiagnosticType = DiagnosticType.Error,
                    MessageData = "Could not find ComponentLifecycleDelegate constructor"
                });
                return;
            }

            // Get Type.GetTypeFromHandle method
            var typeType = mod.ImportReference(typeof(Type)).Resolve();
            MethodDefinition getTypeFromHandleDef = null;
            foreach (var m in typeType.Methods)
            {
                if (m.Name == "GetTypeFromHandle")
                {
                    getTypeFromHandleDef = m;
                    break;
                }
            }
            var getTypeFromHandle = mod.ImportReference(getTypeFromHandleDef);

            foreach (var componentCallback in componentCallbacks)
            {
                // Push typeof(ComponentType) using ldtoken + Type.GetTypeFromHandle
                // Note: We pass Type instead of TypeIndex to avoid cross-assembly generic method call issues
                il.Emit(OpCodes.Ldtoken, mod.ImportReference(componentCallback.ComponentType));
                il.Emit(OpCodes.Call, getTypeFromHandle);

                // Create OnAdded delegate (or push null)
                if (componentCallback.OnAddedWrapper != null)
                {
                    il.Emit(OpCodes.Ldnull);
                    il.Emit(OpCodes.Ldftn, componentCallback.OnAddedWrapper);
                    il.Emit(OpCodes.Newobj, delegateCtor);
                }
                else
                {
                    il.Emit(OpCodes.Ldnull);
                }

                // Create OnRemoved delegate (or push null)
                if (componentCallback.OnRemovedWrapper != null)
                {
                    il.Emit(OpCodes.Ldnull);
                    il.Emit(OpCodes.Ldftn, componentCallback.OnRemovedWrapper);
                    il.Emit(OpCodes.Newobj, delegateCtor);
                }
                else
                {
                    il.Emit(OpCodes.Ldnull);
                }

                // Push boolean parameters
                il.Emit(componentCallback.HasOnAdded ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                il.Emit(componentCallback.HasOnRemoved ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                il.Emit(componentCallback.OnAddedIsBurst ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                il.Emit(componentCallback.OnRemovedIsBurst ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);

                // Call TypeManager.RegisterComponentLifecycleCallback
                il.Emit(OpCodes.Call, registerMethod);
            }

            il.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// Launders a type reference to be properly scoped for the current assembly.
        /// Without this, using ldtoken on types from other assemblies can cause:
        /// "System.BadImageFormatException: Expected reference type but got type kind 17"
        /// See ECMA-335 section II.7.3 for details on proper type reference scoping.
        /// </summary>
        TypeReference LaunderTypeRef(TypeReference typeRef)
        {
            var mod = AssemblyDefinition.MainModule;
            return TypeReferenceExtensions.LaunderTypeRef(typeRef, mod);
        }
    }
}
