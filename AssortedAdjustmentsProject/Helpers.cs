using Base.Core;
using Base.Defs;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    public static class Helpers
    {
        /// <summary>
        /// Creates a new definition by cloning an existing one and registering it with the DefRepository.
        /// </summary>
        public static T CreateDefFromClone<T>(T source, string guid, string name) where T : BaseDef
        {
            try
            {
                DefRepository repo = GameUtl.GameComponent<DefRepository>();
                if (repo.GetDef(guid) != null)
                {
                    if (repo.GetDef(guid) is T existing)
                        return existing;
                    else
                        throw new InvalidOperationException($"A def with GUID '{guid}' already exists but is not of type {typeof(T).Name}.");
                }

                T result = (T)repo.CreateDef(guid, source, null);
                result.name = name;

                Debug.Log($"[AAP] Created new def: {result.name} of type {result.GetType().Name}");
                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] Failed to create def clone '{name}': {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Copies all public instance fields from source to target using reflection.
        /// </summary>
        public static void CopyFields(object source, object target)
        {
            if (source == null || target == null) return;
            var fields = source.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                field.SetValue(target, field.GetValue(source));
            }
        }

        /// <summary>
        /// Adds a BaseDef to an array field on a target object, preserving the exact array element type.
        /// </summary>
        public static void AddDefToArrayField(object target, string fieldName, BaseDef defToAdd)
        {
            var field = Traverse.Create(target).Field(fieldName);
            var currentValue = field.GetValue();

            if (currentValue == null)
            {
                field.SetValue(new BaseDef[] { defToAdd });
                return;
            }

            if (!(currentValue is Array currentArray))
            {
                Debug.LogWarning($"[AAP] Field '{fieldName}' is not an array, cannot add element.");
                return;
            }

            // Check if the def is already present (reference equality)
            foreach (var item in currentArray)
            {
                if (ReferenceEquals(item, defToAdd))
                    return;
            }

            // Create a new array of the exact runtime element type
            Type elementType = currentArray.GetType().GetElementType();
            Array newArray = Array.CreateInstance(elementType, currentArray.Length + 1);
            Array.Copy(currentArray, newArray, currentArray.Length);
            newArray.SetValue(defToAdd, currentArray.Length);

            field.SetValue(newArray);
        }

        /// <summary>
        /// Adds an element to an array field of a specific type, preserving the exact array element type.
        /// </summary>
        public static void AddToArrayField<T>(object target, string fieldName, T item)
        {
            var field = Traverse.Create(target).Field(fieldName);
            var currentValue = field.GetValue();

            if (currentValue == null)
            {
                field.SetValue(new T[] { item });
                return;
            }

            if (!(currentValue is Array currentArray))
            {
                Debug.LogWarning($"[AAP] Field '{fieldName}' is not an array, cannot add element.");
                return;
            }

            // Check for duplicates (reference equality)
            foreach (var existing in currentArray)
            {
                if (ReferenceEquals(existing, item))
                    return;
            }

            Type elementType = currentArray.GetType().GetElementType();
            Array newArray = Array.CreateInstance(elementType, currentArray.Length + 1);
            Array.Copy(currentArray, newArray, currentArray.Length);
            newArray.SetValue(item, currentArray.Length);

            field.SetValue(newArray);
        }
    }
}