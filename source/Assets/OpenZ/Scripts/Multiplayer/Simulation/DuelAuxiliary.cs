using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OpenZ.Sim
{
    public sealed partial class ZWorld
    {
        internal void DuelWriteAuxiliary(BinaryWriter writer)
        {
            DuelAuxiliary.WriteList(writer, missiles); DuelAuxiliary.WriteList(writer, newMissiles);
            DuelAuxiliary.WriteList(writer, crocs); DuelAuxiliary.WriteList(writer, CrocNotes);
            writer.Write(CrocNoteCount);
            writer.Write(dragonPass != null);
            if (dragonPass != null) DuelAuxiliary.Write(writer, dragonPass);
        }
        internal void DuelReadAuxiliary(BinaryReader reader)
        {
            DuelAuxiliary.ReadList(reader, missiles); DuelAuxiliary.ReadList(reader, newMissiles);
            DuelAuxiliary.ReadList(reader, crocs); DuelAuxiliary.ReadList(reader, CrocNotes);
            CrocNoteCount = reader.ReadInt32();
            dragonPass = reader.ReadBoolean() ? DuelAuxiliary.Read<ZDragonPass>(reader) : null;
            if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Trailing auxiliary state.");
        }
    }
    internal static class DuelAuxiliary
    {
        static class Fields<T>
        {
            internal static readonly FieldInfo[] All = typeof(T).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => (f.IsPublic || f.Name == "P") && (f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType == typeof(double[])))
                .OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
        }
        internal static void WriteList<T>(BinaryWriter writer, List<T> list)
        { writer.Write(list.Count); foreach (var value in list) Write(writer, value); }
        internal static void ReadList<T>(BinaryReader reader, List<T> list) where T : new()
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > 4096) throw new InvalidDataException("Invalid auxiliary count.");
            // Keep crocodile object identities: their views can retain references.
            for (int i = 0; i < count; i++)
            {
                object value = i < list.Count ? (object)list[i] : new T();
                ReadInto<T>(reader, value);
                if (i < list.Count) list[i] = (T)value; else list.Add((T)value);
            }
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
        }
        internal static void Write<T>(BinaryWriter writer, T value)
        {
            foreach (var field in Fields<T>.All)
            {
                object v = field.GetValue(value); Type t = field.FieldType;
                if (t.IsEnum) writer.Write(Convert.ToInt32(v));
                else if (t == typeof(int)) writer.Write((int)v);
                else if (t == typeof(bool)) writer.Write((bool)v);
                else if (t == typeof(double)) writer.Write((double)v);
                else if (t == typeof(float)) writer.Write((float)v);
                else if (t == typeof(byte)) writer.Write((byte)v);
                else if (t == typeof(sbyte)) writer.Write((sbyte)v);
                else if (t == typeof(double[]))
                { var a = (double[])v; writer.Write(a?.Length ?? -1); if (a != null) foreach (double n in a) writer.Write(n); }
                else throw new InvalidDataException("Unsupported auxiliary field " + field.Name);
            }
        }
        internal static T Read<T>(BinaryReader reader) where T : new()
        { object value = new T(); ReadInto<T>(reader, value); return (T)value; }
        static void ReadInto<T>(BinaryReader reader, object value)
        {
            foreach (var field in Fields<T>.All)
            {
                Type t = field.FieldType; object v;
                if (t.IsEnum) v = Enum.ToObject(t, reader.ReadInt32());
                else if (t == typeof(int)) v = reader.ReadInt32();
                else if (t == typeof(bool)) v = reader.ReadBoolean();
                else if (t == typeof(double)) v = reader.ReadDouble();
                else if (t == typeof(float)) v = reader.ReadSingle();
                else if (t == typeof(byte)) v = reader.ReadByte();
                else if (t == typeof(sbyte)) v = reader.ReadSByte();
                else if (t == typeof(double[]))
                {
                    int count = reader.ReadInt32();
                    if (count < -1 || count > 4096) throw new InvalidDataException("Invalid auxiliary array.");
                    var a = count < 0 ? null : new double[count];
                    if (a != null) for (int i = 0; i < a.Length; i++) a[i] = reader.ReadDouble();
                    v = a;
                }
                else throw new InvalidDataException("Unsupported auxiliary field " + field.Name);
                field.SetValue(value, v);
            }
        }
    }
}
