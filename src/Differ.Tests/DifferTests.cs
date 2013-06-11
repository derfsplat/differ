using Ploeh.AutoFixture;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using Xunit;

namespace Differ
{
    public class DifferTests
    {
        enum SimpleEnum
        {
            First,
            Second
        }
        [Serializable]
        class SimpleType
        {
            public string StringValue { get; set; }
            public int IntValue { get; set; }
            public SimpleEnum SimpleEnumValue { get; set; }
        }

        [Fact]
        public void Differ_WhenPropsDiffer_FindsDifferences()
        {
            Fixture fixture = new Fixture();
            //in the real world, we'd be comparing the updated value with what's currently in the repository
            var @out = fixture.CreateAnonymous<SimpleType>();
            var outChanged = fixture.CreateAnonymous<SimpleType>();

            var changes = Differ<SimpleType>.Diff(@out, outChanged).ToList();

            Assert.Equal(3, changes.Count);
            changes.ForEach(change =>
            {
                var name = change.Name;
                var original = @out.GetType().GetProperty(name).GetValue(@out, null);
                var changed = outChanged.GetType().GetProperty(name).GetValue(outChanged, null);

                Assert.Equal(original, change.PreviousValue);
                Assert.Equal(changed, change.NewValue);
            });
        }

        [Fact]
        public void Differ_WhenPropsEqual_SkipsProps()
        {
            Fixture fixture = new Fixture();
            var @out = fixture.CreateAnonymous<SimpleType>();
            var outChanged = DeepCloneObject.DeepClone<SimpleType>(@out);

            var changes = Differ<SimpleType>.Diff(@out, outChanged).ToList();

            Assert.Equal(0, changes.Count);
        }
    }

    internal class DeepCloneObject
    {
        public static T DeepClone<T>(T obj)
        {
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, obj);
                ms.Position = 0;

                return (T)formatter.Deserialize(ms);
            }
        }
    }
}
