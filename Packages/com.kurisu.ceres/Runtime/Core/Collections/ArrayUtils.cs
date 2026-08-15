using System;
using System.Collections;
using System.Collections.Generic;
namespace Ceres.Collections
{
    // Code from UnityEditor.ArrayUtility
    // Helpers for builtin arrays ...
    // These are O(n) operations (where List<T>() is used) - the arrays are actually copied (http://msdn.microsoft.com/en-us/library/fkbw11z0.aspx)
    // but its pretty helpful for now
    public static class ArrayUtils
    {
        //appends ''item'' to the end of ''array''
        public static void Add<T>(ref T[] array, T item)
        {
            Array.Resize(ref array, array.Length + 1);
            array[^1] = item;
        }

        //compares two arrays
        public static bool ArrayEquals<T>(T[] lhs, T[] rhs)
        {
            if (lhs == null || rhs == null)
                return lhs == rhs;

            if (lhs.Length != rhs.Length)
                return false;

            for (int i = 0; i < lhs.Length; i++)
            {
                if (!lhs[i].Equals(rhs[i]))
                    return false;
            }
            return true;
        }

        //compares two arrays
        public static bool ArrayReferenceEquals<T>(T[] lhs, T[] rhs)
        {
            if (lhs == null || rhs == null)
                return lhs == rhs;

            if (lhs.Length != rhs.Length)
                return false;

            for (int i = 0; i < lhs.Length; i++)
            {
                if (!ReferenceEquals(lhs[i], rhs[i]))
                    return false;
            }
            return true;
        }

        //appends items to the end of array
        public static void AddRange<T>(ref T[] array, T[] items)
        {
            int size = array.Length;
            Array.Resize(ref array, array.Length + items.Length);
            for (int i = 0; i < items.Length; i++)
                array[size + i] = items[i];
        }

        //inserts item ''item'' at position ''index''
        public static void Insert<T>(ref T[] array, int index, T item)
        {
            ArrayList a = new();
            a.AddRange(array);
            a.Insert(index, item);
            array = a.ToArray(typeof(T)) as T[];
        }

        public static void Reorder<T>(ref T[] array, int fromIndex, int toIndex)
        {
            ArrayList a = new();
            a.AddRange(array);
            var item = array[fromIndex];
            a.RemoveAt(fromIndex);
            a.Insert(toIndex, item);
            array = a.ToArray(typeof(T)) as T[];
        }

        //removes ''item'' from ''array''
        public static void Remove<T>(ref T[] array, T item)
        {
            List<T> newList = new(array);
            newList.Remove(item);
            array = newList.ToArray();
        }

        public static List<T> FindAll<T>(T[] array, Predicate<T> match)
        {
            List<T> list = new(array);
            return list.FindAll(match);
        }

        public static T Find<T>(T[] array, Predicate<T> match)
        {
            List<T> list = new(array);
            return list.Find(match);
        }

        //Find the index of the first element that satisfies the predicate
        public static int FindIndex<T>(T[] array, Predicate<T> match)
        {
            List<T> list = new(array);
            return list.FindIndex(match);
        }

        //index of first element with value ''value''
        public static int IndexOf<T>(T[] array, T value)
        {
            List<T> list = new(array);
            return list.IndexOf(value);
        }

        //index of the last element with value ''value''
        public static int LastIndexOf<T>(T[] array, T value)
        {
            List<T> list = new(array);
            return list.LastIndexOf(value);
        }

        //remove element at position ''index''
        public static void RemoveAt<T>(ref T[] array, int index)
        {
            List<T> list = new(array);
            list.RemoveAt(index);
            array = list.ToArray();
        }

        //determines if the array contains the item
        public static bool Contains<T>(T[] array, T item)
        {
            List<T> list = new(array);
            return list.Contains(item);
        }

        //Clears the array
        public static void Clear<T>(ref T[] array)
        {
            Array.Clear(array, 0, array.Length);
            Array.Resize(ref array, 0);
        }
    }
}