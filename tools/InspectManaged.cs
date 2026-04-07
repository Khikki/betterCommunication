using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class InspectManaged
{
    private static int Main(string[] args)
    {
        string managedPath = args.Length > 0 ? args[0] : null;
        if (string.IsNullOrWhiteSpace(managedPath) || !Directory.Exists(managedPath))
        {
            Console.Error.WriteLine("Usage: InspectManaged <ManagedPath> [term1] [term2] ...");
            return 1;
        }

        string[] terms = args.Skip(1).ToArray();
        if (terms.Length == 0)
        {
            terms = new[] { "Cosmetic", "Customize", "Shop", "Preview", "Unlock", "Achievement" };
        }

        string[] exactTypes = terms
            .Where(delegate(string term)
            {
                return term.StartsWith("type:", StringComparison.OrdinalIgnoreCase);
            })
            .Select(delegate(string term)
            {
                return term.Substring(5);
            })
            .Where(delegate(string term)
            {
                return !string.IsNullOrWhiteSpace(term);
            })
            .ToArray();

        terms = terms
            .Where(delegate(string term)
            {
                return !term.StartsWith("type:", StringComparison.OrdinalIgnoreCase);
            })
            .ToArray();

        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs eventArgs)
        {
            string assemblyName = new AssemblyName(eventArgs.Name).Name + ".dll";
            string candidatePath = Path.Combine(managedPath, assemblyName);
            if (File.Exists(candidatePath))
            {
                return Assembly.LoadFrom(candidatePath);
            }

            return null;
        };

        List<Assembly> assemblies = new List<Assembly>();
        foreach (string dllPath in Directory.GetFiles(managedPath, "*.dll"))
        {
            try
            {
                assemblies.Add(Assembly.LoadFrom(dllPath));
            }
            catch
            {
            }
        }

        foreach (Assembly assembly in assemblies
            .Where(delegate(Assembly assembly)
            {
                string name = assembly.GetName().Name;
                return string.Equals(name, "GameAssembly", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(name, "SharedAssembly", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(delegate(Assembly assembly)
            {
                return assembly.GetName().Name;
            }, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine("=== Assembly: {0} ===", assembly.GetName().Name);

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(delegate(Type type) { return type != null; }).ToArray();
            }

            foreach (Type type in types
                .Where(delegate(Type type)
                {
                    if (exactTypes.Any(delegate(string exactType)
                    {
                        return string.Equals(type.FullName, exactType, StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(type.Name, exactType, StringComparison.OrdinalIgnoreCase);
                    }))
                    {
                        return true;
                    }

                    return terms.Any(delegate(string term)
                    {
                        return Contains(type.FullName, term);
                    });
                })
                .OrderBy(delegate(Type type)
                {
                    return type.FullName;
                }, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine("[Type] {0}", type.FullName);

                bool dumpAllMembers = exactTypes.Any(delegate(string exactType)
                {
                    return string.Equals(type.FullName, exactType, StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(type.Name, exactType, StringComparison.OrdinalIgnoreCase);
                });

                foreach (FieldInfo field in type.GetFields(AllMembers).OrderBy(delegate(FieldInfo field)
                {
                    return field.Name;
                }, StringComparer.OrdinalIgnoreCase))
                {
                    string fieldTypeName = SafeGetTypeName(delegate
                    {
                        return field.FieldType;
                    });
                    if (dumpAllMembers || MatchesAny(field.Name, fieldTypeName, terms))
                    {
                        Console.WriteLine("  [Field] {0} {1}", fieldTypeName, field.Name);
                    }
                }

                foreach (PropertyInfo property in type.GetProperties(AllMembers).OrderBy(delegate(PropertyInfo property)
                {
                    return property.Name;
                }, StringComparer.OrdinalIgnoreCase))
                {
                    string propertyTypeName = SafeGetTypeName(delegate
                    {
                        return property.PropertyType;
                    });
                    if (dumpAllMembers || MatchesAny(property.Name, propertyTypeName, terms))
                    {
                        Console.WriteLine("  [Property] {0} {1}", propertyTypeName, property.Name);
                    }
                }

                foreach (MethodInfo method in type.GetMethods(AllMembers).OrderBy(delegate(MethodInfo method)
                {
                    return method.Name;
                }, StringComparer.OrdinalIgnoreCase))
                {
                    string returnTypeName = SafeGetTypeName(delegate
                    {
                        return method.ReturnType;
                    });
                    ParameterInfo[] parametersInfo = SafeGetParameters(method);
                    bool matchesParameters = parametersInfo.Any(delegate(ParameterInfo parameter)
                    {
                        return MatchesAny(parameter.Name, SafeGetTypeName(delegate
                        {
                            return parameter.ParameterType;
                        }), terms);
                    });

                    if (!dumpAllMembers && !MatchesAny(method.Name, returnTypeName, terms) && !matchesParameters)
                    {
                        continue;
                    }

                    string parameters = string.Join(", ", parametersInfo.Select(delegate(ParameterInfo parameter)
                    {
                        return SafeGetTypeName(delegate
                        {
                            return parameter.ParameterType;
                        }) + " " + parameter.Name;
                    }).ToArray());
                    Console.WriteLine("  [Method] {0} {1}({2})", returnTypeName, method.Name, parameters);
                }
            }
        }

        return 0;
    }

    private static bool MatchesAny(string first, string second, string[] terms)
    {
        return terms.Any(delegate(string term)
        {
            return Contains(first, term) || Contains(second, term);
        });
    }

    private static bool Contains(string value, string term)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string SafeGetTypeName(Func<Type> getter)
    {
        try
        {
            Type type = getter();
            return type != null ? type.Name : "<null>";
        }
        catch (Exception ex)
        {
            return "<" + ex.GetType().Name + ">";
        }
    }

    private static ParameterInfo[] SafeGetParameters(MethodInfo method)
    {
        try
        {
            return method.GetParameters();
        }
        catch
        {
            return new ParameterInfo[0];
        }
    }

    private const BindingFlags AllMembers =
        BindingFlags.Public |
        BindingFlags.NonPublic |
        BindingFlags.Instance |
        BindingFlags.Static |
        BindingFlags.DeclaredOnly;
}
