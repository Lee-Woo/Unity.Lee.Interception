

//#define DEBUG_SAVE_GENERATED_ASSEMBLY

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Interception.Interceptors.InstanceInterceptors.InterfaceInterception;

namespace Unity.Interception.Interceptors.TypeInterceptors.VirtualMethodInterception.InterceptingClassGeneration
{
    /// <summary>
    /// Class that handles generating the dynamic types used for interception.
    /// </summary>
    public partial class InterceptingClassGenerator
    {
        private static readonly AssemblyBuilder AssemblyBuilder;

        private readonly Type _typeToIntercept;
        private readonly IEnumerable<Type> _additionalInterfaces;
        private Type _targetType;
        private GenericParameterMapper _mainTypeMapper;

        private FieldBuilder _proxyInterceptionPipelineField;
        private TypeBuilder _typeBuilder;

        static InterceptingClassGenerator()
        {
            AssemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("Unity_ILEmit_DynamicClasses"),
#if DEBUG_SAVE_GENERATED_ASSEMBLY
                AssemblyBuilderAccess.RunAndSave);
#else
                AssemblyBuilderAccess.Run);
#endif
        }

        /// <summary>
        /// Create a new <see cref="InterceptingClassGenerator"/> that will generate a
        /// wrapper class for the requested <paramref name="typeToIntercept"/>.
        /// </summary>
        /// <param name="typeToIntercept">Type to generate the wrapper for.</param>
        /// <param name="additionalInterfaces">Additional interfaces the proxy must implement.</param>
        public InterceptingClassGenerator(Type typeToIntercept, params Type[] additionalInterfaces)
        {
            _typeToIntercept = typeToIntercept;
            _additionalInterfaces = additionalInterfaces;
            CreateTypeBuilder();
        }

        /// <summary>
        /// Create the wrapper class for the given type.
        /// </summary>
        /// <returns>Wrapper type.</returns>
        public Type GenerateType()
        {
            AddMethods();
            AddProperties();
            AddEvents();
            AddConstructors();

            int memberCount = 0;
            HashSet<Type> implementedInterfaces = GetImplementedInterfacesSet();
            foreach (var @interface in _additionalInterfaces)
            {
                memberCount =
                    new InterfaceImplementation(_typeBuilder, @interface, _proxyInterceptionPipelineField, true)
                        .Implement(implementedInterfaces, memberCount);
            }

            Type result = _typeBuilder.CreateTypeInfo().AsType();
#if DEBUG_SAVE_GENERATED_ASSEMBLY
            assemblyBuilder.Save("Unity_ILEmit_DynamicClasses.dll");
#endif
            return result;
        }

        private void AddMethods()
        {
            int methodNum = 0;
            foreach (MethodInfo method in GetMethodsToIntercept())
            {
                new MethodOverride(_typeBuilder, _proxyInterceptionPipelineField, method, _targetType, _mainTypeMapper, methodNum++).AddMethod();
            }
        }

        private IEnumerable<MethodInfo> GetMethodsToIntercept()
        {
            List<MethodInfo> methodsToIntercept = new List<MethodInfo>();
            foreach (MethodInfo method in _typeToIntercept.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!method.IsSpecialName && MethodOverride.MethodCanBeIntercepted(method))
                {
                    methodsToIntercept.Add(method);
                }
            }

            MethodSorter sorter = new MethodSorter(_typeToIntercept, methodsToIntercept);
            foreach (MethodInfo method in sorter)
            {
                yield return method;
            }
        }

        private void AddProperties()
        {
            // We don't actually add new properties to this class. We just override
            // the get / set methods as available. Inheritance makes sure the properties
            // show up properly on the derived class.

            int propertyCount = 0;
            foreach (PropertyInfo property in _typeToIntercept.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                OverridePropertyMethod(property.GetGetMethod(true), propertyCount);
                OverridePropertyMethod(property.GetSetMethod(true), propertyCount);
                ++propertyCount;
            }
        }

        private void OverridePropertyMethod(MethodInfo method, int count)
        {
            if (method != null && MethodOverride.MethodCanBeIntercepted(method))
            {
                new MethodOverride(_typeBuilder, _proxyInterceptionPipelineField, method, _targetType, _mainTypeMapper, count).AddMethod();
            }
        }

        private void AddEvents()
        {
            // We don't actually add new events to this class. We just override
            // the add / remove methods as available. Inheritance makes sure the events
            // show up properly on the derived class.

            int eventCount = 0;
            foreach (EventInfo eventInfo in _typeToIntercept.GetEvents(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                OverrideEventMethod(eventInfo.GetAddMethod(), eventCount);
                OverrideEventMethod(eventInfo.GetRemoveMethod(), eventCount);
                ++eventCount;
            }
        }

        private void OverrideEventMethod(MethodInfo method, int count)
        {
            if (method != null && MethodOverride.MethodCanBeIntercepted(method))
            {
                new MethodOverride(_typeBuilder, _proxyInterceptionPipelineField, method, _targetType, _mainTypeMapper, count).AddMethod();
            }
        }

        private void AddConstructors()
        {
            BindingFlags bindingFlags =
                _typeToIntercept.IsAbstract
                    ? BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic
                    : BindingFlags.Public | BindingFlags.Instance;

            foreach (ConstructorInfo ctor in _typeToIntercept.GetConstructors(bindingFlags))
            {
                AddConstructor(ctor);
            }
        }

        private void AddConstructor(ConstructorInfo ctor)
        {
            if (!(ctor.IsPublic || ctor.IsFamily || ctor.IsFamilyOrAssembly))
            {
                return;
            }

            MethodAttributes attributes =
                (ctor.Attributes
                & ~MethodAttributes.ReservedMask
                & ~MethodAttributes.MemberAccessMask)
                | MethodAttributes.Public;

            ParameterInfo[] parameters = ctor.GetParameters();

            Type[] paramTypes = parameters.Select(item => item.ParameterType).ToArray();

            ConstructorBuilder ctorBuilder = _typeBuilder.DefineConstructor(
                attributes, ctor.CallingConvention, paramTypes);

            for (int i = 0; i < parameters.Length; i++)
            {
                ctorBuilder.DefineParameter(i + 1, parameters[i].Attributes, parameters[i].Name);
            }

            ILGenerator il = ctorBuilder.GetILGenerator();

            // Initialize pipeline field
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Newobj, InterceptionBehaviorPipelineMethods.Constructor);
            il.Emit(OpCodes.Stfld, _proxyInterceptionPipelineField);

            // call base class constructor
            il.Emit(OpCodes.Ldarg_0);
            for (int i = 0; i < paramTypes.Length; ++i)
            {
                il.Emit(OpCodes.Ldarg, i + 1);
            }

            il.Emit(OpCodes.Call, ctor);

            il.Emit(OpCodes.Ret);
        }

        private void CreateTypeBuilder()
        {
            TypeAttributes newAttributes = _typeToIntercept.Attributes;
            newAttributes = FilterTypeAttributes(newAttributes);

            Type baseClass = GetGenericType(_typeToIntercept);

            ModuleBuilder moduleBuilder = InterceptorClassGenerator.CreateModuleBuilder(AssemblyBuilder);
            _typeBuilder = moduleBuilder.DefineType(
                "DynamicModule.ns.Wrapped_" + _typeToIntercept.Name + "_" + Guid.NewGuid().ToString("N"),
                newAttributes,
                baseClass);

            _mainTypeMapper = DefineGenericArguments(_typeBuilder, baseClass);

            if (_typeToIntercept.IsGenericType)
            {
                var definition = _typeToIntercept.GetGenericTypeDefinition();
                var mappedParameters = definition.GetGenericArguments().Select(t => _mainTypeMapper.Map(t)).ToArray();
                _targetType = definition.MakeGenericType(mappedParameters);
            }
            else
            {
                _targetType = _typeToIntercept;
            }

            _proxyInterceptionPipelineField = InterceptingProxyImplementor.ImplementIInterceptingProxy(_typeBuilder);
        }

        private static Type GetGenericType(Type typeToIntercept)
        {
            if (typeToIntercept.IsGenericType)
            {
                return typeToIntercept.GetGenericTypeDefinition();
            }
            return typeToIntercept;
        }

        private static GenericParameterMapper DefineGenericArguments(TypeBuilder typeBuilder, Type baseClass)
        {
            if (!baseClass.IsGenericType)
            {
                return GenericParameterMapper.DefaultMapper;
            }
            Type[] genericArguments = baseClass.GetGenericArguments();

            GenericTypeParameterBuilder[] genericTypes = typeBuilder.DefineGenericParameters(
                genericArguments.Select(t => t.Name).ToArray());

            for (int i = 0; i < genericArguments.Length; ++i)
            {
                genericTypes[i].SetGenericParameterAttributes(genericArguments[i].GenericParameterAttributes);
                var interfaceConstraints = new List<Type>();
                foreach (Type constraint in genericArguments[i].GetGenericParameterConstraints())
                {
                    if (constraint.IsClass)
                    {
                        genericTypes[i].SetBaseTypeConstraint(constraint);
                    }
                    else
                    {
                        interfaceConstraints.Add(constraint);
                    }
                }
                if (interfaceConstraints.Count > 0)
                {
                    genericTypes[i].SetInterfaceConstraints(interfaceConstraints.ToArray());
                }
            }

            return new GenericParameterMapper(genericArguments, genericTypes.Cast<Type>().ToArray());
        }

        private static TypeAttributes FilterTypeAttributes(TypeAttributes attributes)
        {
            if ((attributes & TypeAttributes.NestedPublic) != 0)
            {
                attributes &= ~TypeAttributes.NestedPublic;
                attributes |= TypeAttributes.Public;
            }

            attributes &= ~TypeAttributes.ReservedMask;
            attributes &= ~TypeAttributes.Abstract;

            return attributes;
        }

        private HashSet<Type> GetImplementedInterfacesSet()
        {
            HashSet<Type> implementedInterfaces = new HashSet<Type>();
            AddToImplementedInterfaces(_typeToIntercept, implementedInterfaces);
            return implementedInterfaces;
        }

        private static void AddToImplementedInterfaces(Type type, HashSet<Type> implementedInterfaces)
        {
            if (!implementedInterfaces.Contains(type))
            {
                if (type.IsInterface)
                {
                    implementedInterfaces.Add(type);
                }

                foreach (var @interface in type.GetInterfaces())
                {
                    AddToImplementedInterfaces(@interface, implementedInterfaces);
                }
            }
        }
    }
}
