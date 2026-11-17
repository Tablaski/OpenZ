using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OpenZ.Sim
{
    public static partial class DuelReplica
    {
        // These are value snapshots, never references to mutable lists or map entries.
        // Checking every value before reusing its bytes also catches direct field writes.
        sealed class ValueState
        {
            long number;
            string text;
            bool initialized;
            ValueState[] children;
            public bool Number(long value)
            {
                bool changed = !initialized || number != value;
                initialized = true; number = value; return changed;
            }
            public bool Text(string value)
            {
                bool changed = !initialized || !string.Equals(text, value, StringComparison.Ordinal);
                initialized = true; text = value; return changed;
            }
            public void Prepare(int count)
            {
                if (count < 0) { children = null; return; }
                if (children == null || children.Length != count)
                {
                    var next = new ValueState[count];
                    if (children != null) Array.Copy(children, next, Math.Min(count, children.Length));
                    for (int i = 0; i < count; i++) if (next[i] == null) next[i] = new ValueState();
                    children = next;
                }
            }
            public ValueState Child(int index) => children[index];
        }
        sealed class ObjectState
        {
            public readonly ZObject Object;
            public readonly ZKind Kind;
            public readonly ValueState[] Values;
            public bool Described, Live;
            public string Type;
            public ObjectState(ZObject obj, int count)
            {
                Object = obj; Kind = obj.Kind; Values = new ValueState[count];
                for (int i = 0; i < count; i++) Values[i] = new ValueState();
            }
        }
        static Func<ZObject, ValueState, bool> BuildTracker(Slot slot)
        {
#if ENABLE_IL2CPP
            // AOT uses the same recursive value checks without runtime code generation.
            return (obj, state) => TrackValue(slot.Type, slot.Get(obj), state);
#else
            var obj = Expression.Parameter(typeof(ZObject), "obj");
            var state = Expression.Parameter(typeof(ValueState), "state");
            Expression value = slot.Field != null ? (Expression)Expression.Field(obj, slot.Field) : Expression.Property(obj, slot.Property);
            return Expression.Lambda<Func<ZObject, ValueState, bool>>(TrackExpression(slot.Type, value, state), obj, state).Compile();
#endif
        }
#if !ENABLE_IL2CPP
        static Expression TrackExpression(Type type, Expression value, Expression state)
        {
            Expression number = null;
            if (type.IsEnum) number = Expression.Convert(Expression.Convert(value, typeof(int)), typeof(long));
            else if (type == typeof(bool)) number = Expression.Condition(value, Expression.Constant(1L), Expression.Constant(0L));
            else if (type == typeof(int) || type == typeof(long)) number = Expression.Convert(value, typeof(long));
            else if (type == typeof(double)) number = Expression.Call(typeof(BitConverter), nameof(BitConverter.DoubleToInt64Bits), null, value);
            else if (type == typeof(float)) number = Expression.Convert(Expression.Call(typeof(BitConverter), nameof(BitConverter.SingleToInt32Bits), null, value), typeof(long));
            else if (type == typeof(ZObject)) number = Expression.Condition(Expression.Equal(value, Expression.Constant(null, type)),
                Expression.Constant(-1L), Expression.Convert(Expression.Field(value, nameof(ZObject.Id)), typeof(long)));
            if (number != null) return Expression.Call(state, nameof(ValueState.Number), null, number);
            if (type == typeof(string)) return Expression.Call(state, nameof(ValueState.Text), null, value);
            if (type == typeof(ZMapData.Obj) || type == typeof(ZWaypoint))
            {
                var fields = type.GetFields().Where(f => !f.IsStatic).OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
                var changed = Expression.Variable(typeof(bool), "changed");
                var body = new List<Expression>();
                if (type == typeof(ZMapData.Obj)) body.Add(Expression.Assign(changed,
                    Expression.Call(state, nameof(ValueState.Number), null, Expression.Constant(1L))));
                else body.Add(Expression.Assign(changed, Expression.Constant(false)));
                body.Add(Expression.Call(state, nameof(ValueState.Prepare), null, Expression.Constant(fields.Length)));
                for (int i = 0; i < fields.Length; i++)
                {
                    var child = Expression.Call(state, nameof(ValueState.Child), null, Expression.Constant(i));
                    body.Add(Expression.OrAssign(changed, TrackExpression(fields[i].FieldType, Expression.Field(value, fields[i]), child)));
                }
                body.Add(changed);
                Expression result = Expression.Block(new[] { changed }, body);
                if (type == typeof(ZMapData.Obj)) result = Expression.Condition(Expression.Equal(value, Expression.Constant(null, type)),
                    Expression.Block(Expression.Call(state, nameof(ValueState.Prepare), null, Expression.Constant(-1)),
                        Expression.Call(state, nameof(ValueState.Number), null, Expression.Constant(0L))), result);
                return result;
            }
            var element = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
            var item = Expression.Parameter(element, "item"); var itemState = Expression.Parameter(typeof(ValueState), "itemState");
            var track = Expression.Lambda(typeof(Func<,,>).MakeGenericType(element, typeof(ValueState), typeof(bool)),
                TrackExpression(element, item, itemState), item, itemState).Compile();
            return Expression.Call(typeof(DuelReplica).GetMethod(nameof(TrackList), BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(element),
                value, state, Expression.Constant(track));
        }
        static bool TrackList<T>(IList<T> values, ValueState state, Func<T, ValueState, bool> track)
        {
            int count = values?.Count ?? -1;
            bool changed = state.Number(count); state.Prepare(count);
            for (int i = 0; i < count; i++) changed |= track(values[i], state.Child(i));
            return changed;
        }
#endif
#if ENABLE_IL2CPP
        static bool TrackValue(Type type, object value, ValueState state)
        {
            if (type.IsEnum) return state.Number(Convert.ToInt32(value));
            if (type == typeof(bool)) return state.Number((bool)value ? 1 : 0);
            if (type == typeof(int)) return state.Number((int)value);
            if (type == typeof(long)) return state.Number((long)value);
            if (type == typeof(float)) return state.Number(BitConverter.SingleToInt32Bits((float)value));
            if (type == typeof(double)) return state.Number(BitConverter.DoubleToInt64Bits((double)value));
            if (type == typeof(string)) return state.Text((string)value);
            if (type == typeof(ZObject)) return state.Number(((ZObject)value)?.Id ?? -1);
            if (type == typeof(ZMapData.Obj) || type == typeof(ZWaypoint))
            {
                bool changed = false;
                if (type == typeof(ZMapData.Obj))
                {
                    changed = state.Number(value == null ? 0 : 1);
                    if (value == null) { state.Prepare(-1); return changed; }
                }
                var fields = type == typeof(ZMapData.Obj) ? MapObjectFields : WaypointFields;
                state.Prepare(fields.Length);
                for (int i = 0; i < fields.Length; i++) changed |= TrackValue(fields[i].FieldType, fields[i].GetValue(value), state.Child(i));
                return changed;
            }
            var list = (IList)value; int count = list?.Count ?? -1;
            bool listChanged = state.Number(count); state.Prepare(count);
            var element = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
            for (int i = 0; i < count; i++) listChanged |= TrackValue(element, list[i], state.Child(i));
            return listChanged;
        }
#endif
    }
}
