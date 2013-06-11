using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Differ
{
    public static class Differ<T>
    {
        static Dictionary<Type, Func<T, T, List<Change>>> differs = new Dictionary<Type, Func<T, T, List<Change>>>();

        public static IEnumerable<Change> Diff(T original, T changed)
        {
            Func<T, T, List<Change>> differ;
            if (!differs.TryGetValue(original.GetType(), out differ))
                differ = GenerateDiffer();
            foreach (var pair in differ(original, changed))
            {
                yield return pair;
            }
        }

        private static Func<T, T, List<Change>> GenerateDiffer()
        {
            var dm = new DynamicMethod("DoDiff", typeof(List<Change>), new Type[] { typeof(T), typeof(T) }, true);

            var il = dm.GetILGenerator();
            // change list
            il.DeclareLocal(typeof(List<Change>));
            il.DeclareLocal(typeof(Change));
            il.DeclareLocal(typeof(object)); // current boxed change
            il.DeclareLocal(typeof(object)); // original

            il.Emit(OpCodes.Newobj, typeof(List<Change>).GetConstructor(Type.EmptyTypes));
            // [list]
            il.Emit(OpCodes.Stloc_0);

            foreach (var prop in RelevantProperties())
            {
                //get friendly name, if htere is one
                var display = prop.GetCustomAttributes(typeof(DisplayNameAttribute), false).FirstOrDefault() as DisplayNameAttribute;
                string name = prop.Name;
                if (display != null) name = string.IsNullOrWhiteSpace(display.DisplayName) ? prop.Name : display.DisplayName;

                // []
                il.Emit(OpCodes.Ldarg_0);
                // [original]
                il.Emit(OpCodes.Callvirt, prop.GetGetMethod());

                il.Emit(OpCodes.Dup);
                if (prop.PropertyType != typeof(string))
                {
                    il.Emit(OpCodes.Box, prop.PropertyType);
                }
                il.Emit(OpCodes.Stloc_3);

                // [original prop val]
                il.Emit(OpCodes.Ldarg_1);

                // [original prop val, current]
                il.Emit(OpCodes.Callvirt, prop.GetGetMethod());
                // [original prop val, current prop val]

                il.Emit(OpCodes.Dup);
                // [original prop val, current prop val, current prop val]

                if (prop.PropertyType != typeof(string))
                {
                    il.Emit(OpCodes.Box, prop.PropertyType);
                    // [original prop val, current prop val, current prop val boxed]
                }

                il.Emit(OpCodes.Stloc_2);
                // [original prop val, current prop val]

                il.EmitCall(OpCodes.Call, typeof(Differ<T>).GetMethod("AreEqual", BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(new Type[] { prop.PropertyType }), null);
                // [result] 

                Label skip = il.DefineLabel();
                il.Emit(OpCodes.Brtrue_S, skip);
                // []

                il.Emit(OpCodes.Newobj, typeof(Change).GetConstructor(Type.EmptyTypes));
                // [change]
                il.Emit(OpCodes.Dup);
                // [change,change]

                il.Emit(OpCodes.Stloc_1);
                // [change]

                il.Emit(OpCodes.Ldstr, name);
                // [change, name]
                il.Emit(OpCodes.Callvirt, typeof(Change).GetMethod("set_Name"));
                // []

                il.Emit(OpCodes.Ldloc_1);
                // [change]

                il.Emit(OpCodes.Ldloc_3);
                // [change] [original prop val]

                il.Emit(OpCodes.Callvirt, typeof(Change).GetMethod("set_PreviousValue"));
                // []

                il.Emit(OpCodes.Ldloc_1);
                // [change]

                il.Emit(OpCodes.Ldloc_2);
                // [change, boxed]

                il.Emit(OpCodes.Callvirt, typeof(Change).GetMethod("set_NewValue"));
                // []

                il.Emit(OpCodes.Ldloc_0);
                // [change list]
                il.Emit(OpCodes.Ldloc_1);
                // [change list, change]
                il.Emit(OpCodes.Callvirt, typeof(List<Change>).GetMethod("Add"));
                // []

                il.MarkLabel(skip);
            }

            il.Emit(OpCodes.Ldloc_0);
            // [change list]
            il.Emit(OpCodes.Ret);

            return (Func<T, T, List<Change>>)dm.CreateDelegate(typeof(Func<T, T, List<Change>>));
        }

        static List<PropertyInfo> RelevantProperties()
        {
            return typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p =>
                    p.GetSetMethod() != null &&
                    p.GetGetMethod() != null &&
                    (p.PropertyType.IsValueType ||
                        p.PropertyType == typeof(string) ||
                        (p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>)))
                    ).ToList();
        }

        private static bool AreEqual<U>(U first, U second)
        {
            if (first == null && second == null) return true;
            if (first == null && second != null) return false;
            return first.Equals(second);
        }

        public class Change
        {
            public string Name { get; set; }
            public object NewValue { get; set; }
            public object PreviousValue { get; set; }
        }
    }

    public static class ChangeExtensions
    {
        public static string ToFriendlyDescription<T>(this IEnumerable<Differ<T>.Change> changes)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendFormat("{0} had the following values changed:{1}", typeof(T).Name, Environment.NewLine);
            changes.ToList().ForEach(c =>
            {
                sb.AppendFormat("{0}: {1} -> {2}{3}", c.Name, c.PreviousValue, c.NewValue, Environment.NewLine);
            });

            return sb.ToString();
        }
    }
}
